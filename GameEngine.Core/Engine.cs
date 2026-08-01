using GameEngine.Core.Systems;
using System.Diagnostics;
using System.Threading;

namespace GameEngine.Core
{
    public class Engine
    {
        public Action? InvalidateAction { get; }

        public SystemContainer Systems;

        public EntityManager EntityManager;
        public InputManager InputManager;
        private readonly Stopwatch stopwatch = new Stopwatch();

        private Scene? currentScene;
        private double lastUpdateTime;
        private readonly bool _audioEnabled;

        private volatile bool _isRunning = true;

        public bool IsRunning => _isRunning;

        // Pooled render snapshots: filled on the engine thread, read on the UI thread.
        // Three buffers are enough for a single reader — at most one is published and one is
        // held by the reader, which always leaves one free for the engine to fill. Recycling
        // them keeps steady-state rendering allocation-free.
        private readonly object _snapshotLock = new();
        private readonly List<RenderSnapshot> _snapshotPool = [new(), new(), new()];
        private RenderSnapshot? _publishedSnapshot;
        private RenderSnapshot? _readerSnapshot;
        private RenderSnapshot? _fillingSnapshot;

        /// <summary>
        /// Called by the UI/render thread to get a consistent view of entity state.
        /// The returned buffer stays valid until this method is called again, at which point
        /// the previous one is recycled — so one reader thread per engine is assumed.
        /// The lock is only held long enough to swap references (nanoseconds).
        /// </summary>
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

        /// <summary>
        /// Engine thread: fill a buffer nobody is reading with current entity state and publish it.
        /// </summary>
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
        public static int? TargetFrameRate { get; set; } = null;

        // Scene-change handoff to engine thread
        private Scene? _pendingScene;
        private volatile bool _pauseUntilFirstPresent;

        // Clamp extreme dt spikes
        private const double MaxDeltaSeconds = 0.1; // cap to 100ms (10 FPS) to avoid catch-up bursts

        public Engine(Action? invalidateAction = null, bool audioEnabled = true)
        {
            InvalidateAction = invalidateAction;
            _audioEnabled = audioEnabled;
            InitializeSystems();
        }

        public void InitializeSystems()
        {
            InputManager = new InputManager();
            EntityManager = new EntityManager();
            Systems = new SystemContainer();
            lastUpdateTime = 0;

            Systems.Add(new InputSystem(InputManager));
            Systems.Add(new MovementSystem());
            Systems.Add(new PhysicsSystem());
            Systems.Add(new AnimationSystem());
            Systems.Add(new RenderSystem(
                EntityManager,
                new RenderOptions()
                {
                    DrawAnimations = true,
                    DrawBoundingBoxes = false,
                    DrawEntityCenters = false,
                    DrawFps = true,
                    // ~1 second of history at 60fps. The previous 1000 took roughly 1000 frames
                    // to converge, so a hitch you could see with your eyes never reached the
                    // counter.
                    FpsSmoothingSamples = 60
                }));
            // Audio is optional: TryCreate returns null on platforms without a mixer, or when
            // the device or native library is missing. Scenes receive a nullable AudioSystem
            // and are expected to cope with silence.
            if (_audioEnabled)
            {
                var audioSystem = AudioSystem.TryCreate();
                if (audioSystem != null)
                    Systems.Add(audioSystem);
            }
        }

        // Start the loop without a forced 1ms delay.
        // Desktop/Mobile: a dedicated long-running thread for precise pacing.
        // Browser: async loop with Task.Delay/Yield to avoid blocking the single UI thread.
        public Task Start()
        {
            if (OperatingSystem.IsBrowser())
            {
                return StartAsyncLoopBrowser();
            }

            // Use dedicated thread with high priority for better frame timing
            var thread = new Thread(RunLoop)
            {
                Name = "GameEngine-Main",
                IsBackground = true,
                Priority = ThreadPriority.AboveNormal
            };
            thread.Start();

            return Task.CompletedTask;
        }

        /// <summary>
        /// Advance the simulation by one frame, applying any queued scene change first.
        /// <para>
        /// Both run loops are built on this, and it is public so a headless harness can drive
        /// the engine deterministically — a fixed delta, no thread, no clock — which is how the
        /// demo scenes are smoke-tested. Call it only from one thread.
        /// </para>
        /// </summary>
        /// <param name="deltaSeconds">Seconds to advance by.</param>
        /// <returns>
        /// True when the simulation advanced. False when the frame was consumed by a scene
        /// change, or while paused awaiting the first paint, or while stopped — in which case
        /// the caller should not treat it as a rendered frame.
        /// </returns>
        public bool Tick(double deltaSeconds)
        {
            // Scene changes are queued from other threads and applied here, on the thread that
            // owns the simulation.
            var scene = Interlocked.Exchange(ref _pendingScene, null);
            if (scene != null)
                ApplySceneChange(scene);

            if (_pauseUntilFirstPresent || !_isRunning)
                return false;

            Update(deltaSeconds);
            return true;
        }

        private async Task StartAsyncLoopBrowser()
        {
            stopwatch.Start();
            lastUpdateTime = 0;

            while (true)
            {
                // Keep the browser responsive
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
            
            while (true)
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
            _isRunning = running;
        }

        private void Update(double deltaSeconds)
        {
            foreach (var system in Systems.Systems)
            {
                system.Update(EntityManager, deltaSeconds);
            }

            var physicsSystem = Systems.Get<PhysicsSystem>();
            currentScene?.Update(EntityManager, physicsSystem, deltaSeconds);
            currentScene?.Update(EntityManager, Systems, deltaSeconds);
            EntityManager.Update();

            // Snapshot all renderable state. The UI thread reads only from this snapshot,
            // avoiding cross-thread mutation.
            PublishRenderSnapshot();
        }

        // Queue the scene change; it will be applied on the engine thread
        public void ChangeScene(Scene scene)
        {
            Interlocked.Exchange(ref _pendingScene, scene);
        }

        // Called from engine thread
        private void ApplySceneChange(Scene scene)
        {
            currentScene = scene;

            // Rebuild systems and scene
            Systems.Dispose();
            var realResolution = InputManager.RealResolution;
            InitializeSystems();
            currentScene.Initialize(EntityManager, InputManager, Systems.TryGet<AudioSystem>(), ResetScene);

            var renderSystem = Systems.Get<RenderSystem>();
            InputManager.VirtualResolution = new Vec2(currentScene.VirtualWidth, currentScene.VirtualHeight);
            InputManager.RealResolution = realResolution;
            renderSystem.SetVirtualDimensions(currentScene.VirtualWidth, currentScene.VirtualHeight);

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
            EntityManager.Clear();
            if (scene != null)
                ChangeScene(scene);
            else
                ChangeScene(currentScene!);
        }

        /// <summary>
        /// Seconds elapsed since the previous frame. Seconds are the engine's one time unit:
        /// every <see cref="ISystem.Update"/> and <see cref="Scene.Update"/> receives them, so
        /// component values like <see cref="Components.CGravity.Acceleration"/> can be written
        /// in the physical units they document.
        /// </summary>
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
            var inputSystem = Systems.Get<InputSystem>();
            inputSystem.SetRealDimensions(width, height);
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
