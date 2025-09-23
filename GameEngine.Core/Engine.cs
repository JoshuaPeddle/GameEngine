using GameEngine.Core.Systems;
using System.Diagnostics;

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
        private bool _audioEnabled;

        private volatile bool _isRunning = true;

        public bool IsRunning => _isRunning;

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
                    FpsSmoothingSamples = 1000
                }));
            if (_audioEnabled)
                Systems.Add(new AudioSystem());
        }

        public async Task Start()
        {
            stopwatch.Start();
            lastUpdateTime = 0;

            while (true)
            {
                await Task.Delay(1);

                if (!_isRunning)
                {
                    lastUpdateTime = stopwatch.Elapsed.TotalSeconds;
                    continue;
                }

                Update(CalculateDeltaTime());
                InvalidateAction?.Invoke();
            }
        }

        public void SetRunning(bool running) 
        {
            _isRunning = running;
        }

        private void Update(double deltaTime)
        {
            foreach (ISystem system in Systems.Systems)
            {
                system.Update(EntityManager, deltaTime);
            }

            var physicsSystem = Systems.Get<PhysicsSystem>();
            currentScene?.Update(EntityManager, physicsSystem, deltaTime);
            currentScene?.Update(EntityManager, Systems, deltaTime);
            EntityManager.Update();
        }

        public void ChangeScene(Scene scene)
        {
            currentScene = scene;
            Systems.Dispose();
            stopwatch.Restart();
            var realResolution = InputManager.RealResolution;
            InitializeSystems();
            currentScene.Initialize(EntityManager, InputManager, Systems.TryGet<AudioSystem>(), ResetScene);
            var renderSystem = Systems.Get<RenderSystem>();
            InputManager.VirtualResolution = new Vec2(currentScene.VirtualWidth, currentScene.VirtualHeight);
            InputManager.RealResolution = realResolution;
            renderSystem.SetVirtualDimensions(currentScene.VirtualWidth, currentScene.VirtualHeight);
        }

        public void ResetScene(Scene? scene = null)
        {
            EntityManager.Clear();
            if (scene != null)
                ChangeScene(scene);
            else
                ChangeScene(currentScene!);
        }

        private double CalculateDeltaTime()
        {
            double currentTime = stopwatch.Elapsed.TotalSeconds;
            double deltaTime = currentTime - lastUpdateTime;
            lastUpdateTime = currentTime;
            return deltaTime * 1000;
        }

        public void SizeChanged(int width, int height)
        {
            var inputSystem = Systems.Get<InputSystem>();
            inputSystem.SetRealDimensions(width, height);
        }
    }
}
