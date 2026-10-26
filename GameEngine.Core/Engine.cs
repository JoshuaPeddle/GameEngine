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
        private readonly object _frameLock = new();
        private readonly SemaphoreSlim _loopWake = new(0, 1);
        private volatile HostState _hostState = HostState.Active;
        private bool _hostAttached = true;
        public HostServices HostServices { get; }
        public HostState HostState => _hostState;
        public event Action<LifecycleEvent>? LifecycleChanged;
        public event EventHandler<BackRequestEventArgs>? BackRequested;

        private void UpdateHostServices() => HostServices.SetForeground(
            _hostAttached && _hostState == HostState.Active && _isRunning && !IsFaulted && !_stopped && !IsDisposed);

        private void WakeLoop()
        {
            try { _loopWake.Release(); }
            catch (SemaphoreFullException) { }
        }

        private void NotifyLifecycle(LifecycleEvent lifecycleEvent)
        {
            try { LifecycleChanged?.Invoke(lifecycleEvent); }
            catch (Exception exception) { Fail("host lifecycle", null, exception); }
        }

        public void SetHostState(HostState state)
        {
            if (!Enum.IsDefined(state)) throw new ArgumentOutOfRangeException(nameof(state));
            lock (_frameLock)
            {
                if (IsDisposed || _stopped || state == _hostState) return;
                var previous = _hostState;
                _hostState = state;
                UpdateHostServices();
                if (previous == HostState.Active)
                {
                    InputManager.CancelAllContacts();
                    InputManager.ReleaseAllKeys();
                    InputManager.DispatchPointerEvents();
                    NotifyLifecycle(LifecycleEvent.Pause);
                }
                if (previous == HostState.Background) NotifyLifecycle(LifecycleEvent.Foreground);
                if (state == HostState.Background) NotifyLifecycle(LifecycleEvent.Background);
                if (state == HostState.Active) NotifyLifecycle(LifecycleEvent.Resume);
                lastUpdateTime = stopwatch.Elapsed.TotalSeconds;
                _accumulator.Reset();
                WakeLoop();
            }
        }

        public void SetHostAttached(bool attached)
        {
            lock (_frameLock)
            {
                _hostAttached = attached;
                UpdateHostServices();
            }
        }

        public bool RequestBack()
        {
            lock (_frameLock)
            {
                if (IsDisposed || _stopped) return false;
                var request = new BackRequestEventArgs();
                BackRequested?.Invoke(this, request);
                if (request.Handled) return true;
                if (!_isRunning) return false;
                SetRunning(false);
                return true;
            }
        }
        private Thread? _runThread;
        private Task? _browserLoopTask;
        private int _disposeStarted;

        public bool IsRunning => _isRunning;
        public bool IsStopped => _stopped;
        public bool IsDisposed => Volatile.Read(ref _disposeStarted) != 0;

        private EngineFault? _fault;

        /// <summary>What went wrong, or null while the engine is healthy.</summary>
        public EngineFault? Fault => Volatile.Read(ref _fault);

        public bool IsFaulted => Fault != null;

        // Settable like InvalidateAction, and for the same reason: a host attaches to an engine
        // it did not construct. Called on the engine thread, once, as the fault is recorded.
        private Action<EngineFault>? _faultAction;

        public Action<EngineFault>? FaultAction
        {
            get => Volatile.Read(ref _faultAction);
            set => Volatile.Write(ref _faultAction, value);
        }

        // A faulted engine stops simulating rather than throwing the same exception every
        // frame. It keeps its last snapshot so the host can still paint, and loading another
        // scene clears the fault — which is what lets an editor replace a scene that failed.
        private void Fail(string operation, string? systemName, Exception exception)
        {
            var fault = new EngineFault(operation, currentScene?.GetType().Name, systemName, exception);
            Volatile.Write(ref _fault, fault);
            HostServices.SetForeground(false);
            FaultAction?.Invoke(fault);
        }

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
            EntityManager.BuildRenderSnapshot(buffer, Fault is null ? Systems.TryGet<ParticleSystem>() : null);

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

        // Optional presentation frame limiter (null = unlimited). Presentation pacing is
        // separate from the simulation rate: this decides how often a frame is offered to the
        // host, while FixedTimeStep decides how far the simulation moves.
        public int? TargetFrameRate { get; set; } = null;

        public int ParticleCapacity { get; }

        public const double DefaultFixedTimeStep = 1.0 / 120.0;
        public const int DefaultMaxCatchUpSteps = 8;

        private FixedStepAccumulator _accumulator = new(DefaultFixedTimeStep, DefaultMaxCatchUpSteps);

        /// <summary>Seconds of simulated time in one step of the live run loop.</summary>
        public double FixedTimeStep
        {
            get => _accumulator.StepSeconds;
            set => _accumulator = new FixedStepAccumulator(value, _accumulator.MaxStepsPerFrame);
        }

        /// <summary>Most steps one host frame may run before the remaining elapsed time is dropped.</summary>
        public int MaxCatchUpSteps
        {
            get => _accumulator.MaxStepsPerFrame;
            set => _accumulator = new FixedStepAccumulator(_accumulator.StepSeconds, value);
        }

        /// <summary>Simulated time the run loop has discarded because it could not keep up.</summary>
        public double DroppedSimulationSeconds => _accumulator.DroppedSeconds;

        // Scene-change handoff to engine thread
        private Scene? _pendingScene;
        private volatile bool _pauseUntilFirstPresent;

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
            WakeLoop();
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
            : this(10000, invalidateAction, audioEnabled, assetSource, new HostServices())
        {
        }

        public Engine(int particleCapacity, Action? invalidateAction = null, bool audioEnabled = true,
            IAssetSource? assetSource = null)
            : this(particleCapacity, invalidateAction, audioEnabled, assetSource, new HostServices())
        {
        }

        public Engine(Action? invalidateAction, bool audioEnabled, IAssetSource? assetSource, HostServices hostServices)
            : this(10000, invalidateAction, audioEnabled, assetSource, hostServices)
        {
        }

        public Engine(int particleCapacity, Action? invalidateAction, bool audioEnabled, IAssetSource? assetSource,
            HostServices hostServices)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(particleCapacity);
            ArgumentNullException.ThrowIfNull(hostServices);
            ParticleCapacity = particleCapacity;
            HostServices = hostServices;
            HostServices.SetForeground(true);
            InvalidateAction = invalidateAction;
            _audioEnabled = audioEnabled;
            AssetSource = assetSource ?? new FileAssetSource();
            InitializeSystems();
        }

        [MemberNotNull(nameof(EntityManager), nameof(InputManager))]
        public void InitializeSystems()
        {
            Systems.TryGet<ParticleSystem>()?.Dispose();
            Systems.TryGet<RenderSystem>()?.Dispose();
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
            built.Add(new ParticleSystem(ParticleCapacity));
            built.Add(new RenderSystem(RenderOptions));

            // The audio device belongs to the engine, not to a scene: reopening it on every
            // scene change would leave the previous mixer open and the reopen would fail. The
            // service is always created when audio is enabled — on a platform with no backend
            // it reports why it is silent rather than being quietly absent.
            if (_audioEnabled)
            {
                _audioSystem ??= AudioSystem.Create(AssetSource);
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

                _runThread = new Thread(RunLoop)
                {
                    Name = "GameEngine-Main",
                    IsBackground = true,
                    Priority = ThreadPriority.Normal
                };
                _runThread.Start();

                return Task.CompletedTask;
            }
        }

        /// Advances the simulation by exactly <paramref name="deltaSeconds"/>, after applying
        /// any pending scene change and draining posted work. This is the single simulation
        /// primitive: the run loops reach it through <see cref="FixedStepAccumulator"/>, and a
        /// headless consumer calls it directly and owns its own pacing — nothing here clamps
        /// or splits the value it is given.
        /// Returns false when the engine is stopped or paused, in which case nothing advanced.
        public bool Tick(double deltaSeconds)
        {
            if (!double.IsFinite(deltaSeconds) || deltaSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds,
                    "Tick needs a finite, non-negative number of seconds.");

            lock (_frameLock)
            {
                if (_stopped || _hostState != HostState.Active) return false;
                PumpPendingWork();
                if (_pauseUntilFirstPresent || !_isRunning || IsFaulted) return false;
                Update(deltaSeconds);
                return true;
            }
        }

        private void PumpPendingWork()
        {
            var scene = Interlocked.Exchange(ref _pendingScene, null);
            if (scene != null)
                ApplySceneChange(scene);

            DrainPostedWork();
        }

        // Pumps pending work and then advances the simulation by whole fixed steps. Returns
        // false only while the engine is paused, which is the run loop's cue to idle.
        private bool AdvanceFrame()
        {
            lock (_frameLock)
            {
                if (_hostState == HostState.Active) PumpPendingWork();

                if (_stopped || _hostState != HostState.Active || !_isRunning || _pauseUntilFirstPresent || IsFaulted)
                {
                    lastUpdateTime = stopwatch.Elapsed.TotalSeconds;
                    _accumulator.Reset();
                    return false;
                }

                int steps = _accumulator.Advance(CalculateDeltaTime());
                for (int step = 0; step < steps; step++)
                {
                    if (!Tick(_accumulator.StepSeconds))
                        break;
                }

                return true;
            }
        }

        private async Task StartAsyncLoopBrowser()
        {
            stopwatch.Start();
            lastUpdateTime = 0;

            while (!_stopped)
            {
                await Task.Delay(1);

                if (!AdvanceFrame())
                {
                    await _loopWake.WaitAsync();
                    ResetFrameClock();
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

                if (!AdvanceFrame())
                {
                    _loopWake.Wait();
                    ResetFrameClock();
                    continue;
                }

                InvalidateAction?.Invoke();

                if (TargetFrameRate.HasValue)
                {
                    double elapsedMs = stopwatch.Elapsed.TotalMilliseconds - frameStartMs;
                    double remainingMs = frameDurationMs - elapsedMs;

                    if (remainingMs > 0)
                        _loopWake.Wait(TimeSpan.FromMilliseconds(remainingMs));
                }
                else
                {
                    Thread.Yield();
                }
            }
        }

        private void ResetFrameClock()
        {
            lock (_frameLock)
            {
                lastUpdateTime = stopwatch.Elapsed.TotalSeconds;
                _accumulator.Reset();
            }
        }

        public void SetRunning(bool running)
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposeStarted) != 0, this);
            lock (_frameLock)
            {
                _isRunning = running;
                UpdateHostServices();
                WakeLoop();
            }
        }

        public void Stop()
        {
            _stopped = true;
            _isRunning = false;
            HostServices.SetForeground(false);
            WakeLoop();

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
            HostServices.Dispose();
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
            var systems = Systems.Systems;
            for (int i = 0; i < systems.Count; i++)
            {
                try
                {
                    systems[i].Update(EntityManager, deltaSeconds);
                }
                catch (Exception exception)
                {
                    Fail("system update", systems[i].GetType().Name, exception);
                    return;
                }
            }

            try
            {
                currentScene?.Update(EntityManager, Systems, deltaSeconds);
            }
            catch (Exception exception)
            {
                Fail("scene update", null, exception);
                return;
            }

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
                WakeLoop();
            }
        }

        // Called from engine thread
        private void ApplySceneChange(Scene scene)
        {
            Volatile.Write(ref _fault, null);
            UpdateHostServices();
            currentScene = scene;
            scene.Engine = this;

            // Assets are not touched here — they belong to whoever created them, and a snapshot
            // the host is still painting may hold textures from the scene being left.
            InitializeSystems();

            try
            {
                currentScene.Initialize(EntityManager, InputManager, Systems.TryGet<AudioSystem>(), ResetScene);
            }
            catch (Exception exception)
            {
                Fail("scene initialization", null, exception);
                EntityManager.Clear();
                EntityManager.Update();
                PublishRenderSnapshot();
                InvalidateAction?.Invoke();
                return;
            }

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

            // The accumulator's catch-up budget is the one place a long frame is bounded, so
            // there is no second clamp here to disagree with it.
            return deltaSeconds < 0 ? 0 : deltaSeconds;
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
            lock (_frameLock)
            {
                if (!_pauseUntilFirstPresent) return;
                ResetFrameClock();
                _pauseUntilFirstPresent = false;
                WakeLoop();
            }
        }
    }
}
