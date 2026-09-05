using GameEngine.Core.Systems;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;
using System.Threading;

namespace GameEngine.Core
{
    public class Engine : IDisposable
    {
        private Action? _invalidateAction;

        // Settable so a host can attach itself to an engine it did not construct, which is what
        // embedding an already-running engine into a view requires.
        public Action? InvalidateAction
        {
            get => Volatile.Read(ref _invalidateAction);
            set => Volatile.Write(ref _invalidateAction, value);
        }

        private SystemContainer _systems = new();

        // Published whole. A host that reads this while a scene change rebuilds the container
        // sees either the complete previous set or the complete next one, never a container
        // that is still being filled — which is what a UI thread looking up InputSystem or
        // RenderSystem during a scene swap used to race with.
        public SystemContainer Systems => Volatile.Read(ref _systems);

        public RenderOptions RenderOptions { get; } = new()
        {
            DrawAnimations = true,
            DrawBoundingBoxes = false,
            DrawEntityCenters = false,
            DrawFps = true,
            FpsSmoothingSamples = 60
        };

        public EntityManager EntityManager;
        public InputManager InputManager;
        private readonly Stopwatch stopwatch = new Stopwatch();

        private Scene? currentScene;
        private double lastUpdateTime;
        private readonly bool _audioEnabled;
        private AudioSystem? _audioSystem;

        private volatile bool _isRunning = true;
        private volatile bool _stopped;
        private readonly object _lifecycleLock = new();
        private Thread? _runThread;
        private Task? _browserLoopTask;
        private int _disposeStarted;

        public bool IsRunning => _isRunning;
        public bool IsStopped => _stopped;
        public bool IsDisposed => Volatile.Read(ref _disposeStarted) != 0;

        // Pooled render snapshots: filled on the engine thread, read on the UI thread.
        // Three buffers are enough for a single reader — at most one is published and one is
        // held by the reader, which always leaves one free for the engine to fill. Recycling
        // them keeps steady-state rendering allocation-free.
        private readonly object _snapshotLock = new();
        private readonly List<RenderSnapshot> _snapshotPool = [new(), new(), new()];
        private RenderSnapshot? _publishedSnapshot;
        private RenderSnapshot? _readerSnapshot;
        private RenderSnapshot? _fillingSnapshot;

        /// Called by the UI/render thread to get a consistent view of entity state.
        /// The returned buffer stays valid until this method is called again, at which point
        /// the previous one is recycled — so one reader thread per engine is assumed.
        /// The lock is only held long enough to swap references (nanoseconds).
        public RenderSnapshot GetRenderSnapshot()
        {
            lock (_snapshotLock)
            {
                // Hand back the buffer from the previous paint so the engine can refill it.
                if (_readerSnapshot != null)
                    _readerSnapshot.Readers--;

                _readerSnapshot = _publishedSnapshot;
                if (_readerSnapshot == null)
                    return RenderSnapshot.Empty;

                _readerSnapshot.Readers++;
                return _readerSnapshot;
            }
        }

        /// Engine thread: fill a buffer nobody is reading with current entity state and publish it.
        private void PublishRenderSnapshot()
        {
            RenderSnapshot buffer;
            lock (_snapshotLock)
            {
                buffer = RentSnapshotBuffer();
                _fillingSnapshot = buffer;
            }

            // Filled outside the lock. The buffer is unreachable by readers (not published)
            // and excluded from any further rent, so nothing else can touch it.
            EntityManager.BuildRenderSnapshot(buffer);

            lock (_snapshotLock)
            {
                _publishedSnapshot = buffer;
                _fillingSnapshot = null;
            }
        }

        // Caller must hold _snapshotLock.
        private RenderSnapshot RentSnapshotBuffer()
        {
            foreach (var candidate in _snapshotPool)
            {
                if (candidate != _publishedSnapshot
                    && candidate != _fillingSnapshot
                    && candidate.Readers == 0)
                    return candidate;
            }

            // Only reachable if more than one reader attaches to this engine. Grows the pool
            // once and then settles, so it is not a per-frame allocation.
            var grown = new RenderSnapshot();
            _snapshotPool.Add(grown);
            return grown;
        }

        // Optional frame limiter (null = unlimited)
        public int? TargetFrameRate { get; set; } = null;

        // Scene-change handoff to engine thread
        private Scene? _pendingScene;
        private volatile bool _pauseUntilFirstPresent;

        // Clamp extreme dt spikes
        private const double MaxDeltaSeconds = 0.1;

        public IAssetSource AssetSource { get; }

        private const int MaxQueuedEngineWork = 256;
        private readonly ConcurrentQueue<Action<Engine>> _postedWork = new();
        private int _queuedWorkCount;

        public bool Post(Action<Engine> work)
        {
            ArgumentNullException.ThrowIfNull(work);

            if (Volatile.Read(ref _disposeStarted) != 0 || _stopped)
                return false;

            if (Interlocked.Increment(ref _queuedWorkCount) > MaxQueuedEngineWork)
            {
                Interlocked.Decrement(ref _queuedWorkCount);
                return false;
            }

            _postedWork.Enqueue(work);
            return true;
        }

        private void DrainPostedWork()
        {
            while (_postedWork.TryDequeue(out var work))
            {
                Interlocked.Decrement(ref _queuedWorkCount);
                work(this);
            }
        }

        public Engine(Action? invalidateAction = null, bool audioEnabled = true, IAssetSource? assetSource = null)
        {
            InvalidateAction = invalidateAction;
            _audioEnabled = audioEnabled;
            AssetSource = assetSource ?? new FileAssetSource();
            InitializeSystems();
        }

        [MemberNotNull(nameof(EntityManager), nameof(InputManager))]
        public void InitializeSystems()
        {
            if (InputManager is null)
                InputManager = new InputManager();
            else
                InputManager.Reset();

            if (EntityManager is null)
                EntityManager = new EntityManager();
            else
                EntityManager.Clear();

            lastUpdateTime = 0;

            var built = new SystemContainer();
            built.Add(new InputSystem(InputManager));
            built.Add(new MovementSystem());
            built.Add(new PhysicsSystem());
            built.Add(new AnimationSystem());
            built.Add(new RenderSystem(RenderOptions));

            // The audio device belongs to the engine, not to a scene: reopening it on every
            // scene change would leave the previous mixer open and the reopen would fail.
            if (_audioEnabled)
            {
                _audioSystem ??= AudioSystem.TryCreate(AssetSource);
                if (_audioSystem != null)
                    built.Add(_audioSystem);
            }

            Volatile.Write(ref _systems, built);
        }

        // Start the loop without a forced 1ms delay.
        // Desktop/Mobile: a dedicated long-running thread for precise pacing.
        // Browser: async loop with Task.Delay/Yield to avoid blocking the single UI thread.
        public Task Start()
        {
            lock (_lifecycleLock)
            {
                ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposeStarted) != 0, this);
                if (_stopped)
                    throw new InvalidOperationException("A stopped engine cannot be restarted.");

                if (OperatingSystem.IsBrowser())
                    return _browserLoopTask ??= StartAsyncLoopBrowser();

                if (_runThread != null)
                    return Task.CompletedTask;

                // Use dedicated thread with high priority for better frame timing
                _runThread = new Thread(RunLoop)
                {
                    Name = "GameEngine-Main",
                    IsBackground = true,
                    Priority = ThreadPriority.AboveNormal
                };
                _runThread.Start();

                return Task.CompletedTask;
            }
        }

        public bool Tick(double deltaSeconds)
        {
            if (_stopped)
                return false;

            var scene = Interlocked.Exchange(ref _pendingScene, null);
            if (scene != null)
                ApplySceneChange(scene);

            DrainPostedWork();

            if (_pauseUntilFirstPresent || !_isRunning)
                return false;

            Update(deltaSeconds);
            return true;
        }

        private async Task StartAsyncLoopBrowser()
        {
            stopwatch.Start();
            lastUpdateTime = 0;

            while (!_stopped)
            {
                await Task.Delay(1);

                if (!Tick(CalculateDeltaTime()))
                {
                    lastUpdateTime = stopwatch.Elapsed.TotalSeconds;
                    await Task.Delay(1);
                    continue;
                }

                InvalidateAction?.Invoke();
            }
        }

        private void RunLoop()
        {
            stopwatch.Start();
            lastUpdateTime = 0;

            while (!_stopped)
            {
                double frameDurationMs = TargetFrameRate.HasValue
                    ? 1000.0 / TargetFrameRate.Value
                    : 0.0;

                double frameStartMs = stopwatch.Elapsed.TotalMilliseconds;

                if (!Tick(CalculateDeltaTime()))
                {
                    lastUpdateTime = stopwatch.Elapsed.TotalSeconds;
                    Thread.Sleep(1);
                    continue;
                }

                InvalidateAction?.Invoke();

                if (TargetFrameRate.HasValue)
                {
                    double elapsedMs = stopwatch.Elapsed.TotalMilliseconds - frameStartMs;
                    double remainingMs = frameDurationMs - elapsedMs;

                    if (remainingMs > 2.0)
                    {
                        Thread.Sleep((int)remainingMs - 1);
                    }

                    int spinCount = 32; 
                    while ((stopwatch.Elapsed.TotalMilliseconds - frameStartMs) < frameDurationMs)
                    {
                        Thread.SpinWait(spinCount); 
                        spinCount = Math.Min(((int)Math.Floor(spinCount * 1.5)), 1024);
                        Thread.Yield();
                    }
                }
                else
                {
                    Thread.Yield();
                }
            }
        }

        public void SetRunning(bool running)
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposeStarted) != 0, this);
            _isRunning = running;
        }

        public void Stop()
        {
            _stopped = true;
            _isRunning = false;

            Thread? runThread;
            lock (_lifecycleLock)
                runThread = _runThread;

            // Stop is also the synchronization boundary used by the editor before unloading
            // a scene assembly. Never return while that scene can still be executing.
            if (runThread != null && runThread != Thread.CurrentThread)
                runThread.Join();
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposeStarted, 1) != 0)
                return;

            Stop();
            Systems.Dispose();
            _audioSystem?.Dispose();
            _audioSystem = null;
            InputManager.Reset();
            EntityManager.Clear();
            lock (_lifecycleLock)
            {
                currentScene = null;
                Interlocked.Exchange(ref _pendingScene, null);
            }
            GC.SuppressFinalize(this);
        }

        private void Update(double deltaSeconds)
        {
            foreach (var system in Systems.Systems)
            {
                system.Update(EntityManager, deltaSeconds);
            }

            currentScene?.Update(EntityManager, Systems, deltaSeconds);
            EntityManager.Update();

            // Snapshot all renderable state. The UI thread reads only from this snapshot,
            // avoiding cross-thread mutation.
            PublishRenderSnapshot();
        }

        // Queue the scene change; it will be applied on the engine thread
        public void ChangeScene(Scene scene)
        {
            ArgumentNullException.ThrowIfNull(scene);
            lock (_lifecycleLock)
            {
                ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposeStarted) != 0, this);
                Interlocked.Exchange(ref _pendingScene, scene);
            }
        }

        // Called from engine thread
        private void ApplySceneChange(Scene scene)
        {
            currentScene = scene;
            scene.Engine = this;

            // Rebuild systems and scene. The replaced container is simply dropped: the audio
            // system is the only disposable one in it and the engine owns that for its lifetime.
            InitializeSystems();
            currentScene.Initialize(EntityManager, InputManager, Systems.TryGet<AudioSystem>(), ResetScene);

            var renderSystem = Systems.Get<RenderSystem>();
            InputManager.VirtualResolution = new Vec2(currentScene.VirtualWidth, currentScene.VirtualHeight);
            renderSystem.SetVirtualDimensions(currentScene.VirtualWidth, currentScene.VirtualHeight);
            InputManager.ScalingStrategy = renderSystem.options.ScalingStrategy;

            // Flush the entities the scene just created and publish them. Update() is skipped
            // while paused, so without this the paint requested below would still be showing
            // the previous scene's last frame.
            EntityManager.Update();
            PublishRenderSnapshot();

            // Pause updates until first actual paint
            _pauseUntilFirstPresent = true;

            // Request a paint
            InvalidateAction?.Invoke();
        }

        public void ResetScene(Scene? scene = null)
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposeStarted) != 0, this);
            EntityManager.Clear();
            if (scene != null)
                ChangeScene(scene);
            else
                ChangeScene(currentScene!);
        }

        private double CalculateDeltaTime()
        {
            double currentTime = stopwatch.Elapsed.TotalSeconds;
            double deltaSeconds = currentTime - lastUpdateTime;
            lastUpdateTime = currentTime;

            if (deltaSeconds < 0) deltaSeconds = 0;
            if (deltaSeconds > MaxDeltaSeconds) deltaSeconds = MaxDeltaSeconds;

            return deltaSeconds;
        }

        public void SizeChanged(int width, int height)
        {
            // InputManager survives scene changes, while the SystemContainer is rebuilt.
            // Platform layout events can arrive during that rebuild, so recording the
            // dimensions must not depend on a transient InputSystem lookup.
            InputManager.RealResolution = new Vec2(width, height);
        }

        // Runners should call this after finishing a frame to resume post-scene-change
        public void NotifyFirstPresent()
        {
            if (_pauseUntilFirstPresent)
            {
                // Reset delta baseline to avoid a large dt spike on resume
                lastUpdateTime = stopwatch.Elapsed.TotalSeconds;
                _pauseUntilFirstPresent = false;
            }
        }
    }
}
