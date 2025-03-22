using GameEngine.Core.Systems;
using System.Diagnostics;

namespace GameEngine.Core
{
    public class Engine
    {
        public Action? InvalidateAction { get; }

        public SystemContainer Systems;

        private EntityManager entityManager;
        private InputManager inputManager;
        private readonly Stopwatch stopwatch = new Stopwatch();

        private Scene? currentScene;
        private double lastUpdateTime;
        private bool _audioEnabled;

        public Engine(Action? invalidateAction = null, bool audioEnabled = true)
        {
            InvalidateAction = invalidateAction;
            _audioEnabled = audioEnabled;
            InitializeSystems();
        }

        private void InitializeSystems()
        {
            inputManager = new InputManager();
            entityManager = new EntityManager();
            Systems = new SystemContainer();
            lastUpdateTime = 0;

            Systems.Add(new InputSystem(inputManager));
            Systems.Add(new MovementSystem());
            Systems.Add(new PhysicsSystem());
            Systems.Add(new AnimationSystem());
            Systems.Add(new RenderSystem(
                entityManager,
                new RenderOptions()
                { 
                    DrawAnimations = true,
                    DrawBoundingBoxes = false,
                    DrawEntityCenters = false,
                    DrawFps = true,
                    FpsSmoothingSamples = 1000
                }));
            if (_audioEnabled ) 
                Systems.Add(new AudioSystem());
        }

        public async Task Start()
        {
            stopwatch.Start(); 
            lastUpdateTime = 0;

            while (true)
            {
                await Task.Delay(1);
                Update(CalculateDeltaTime());
                InvalidateAction?.Invoke();
            }
        }

        private void Update(double deltaTime)
        {
       

            foreach (ISystem system in Systems.Systems)
            {
                system.Update(entityManager, deltaTime);
            }
            entityManager.Update();
            var physicsSystem = Systems.Get<PhysicsSystem>();
            currentScene?.Update(entityManager, physicsSystem, deltaTime);

        }

        public void ChangeScene(Scene scene)
        {
            currentScene = scene;
            Systems.Dispose();
            stopwatch.Restart();
            InitializeSystems();
            currentScene.Initialize(entityManager, inputManager, Systems.TryGet<AudioSystem>(), ResetScene);
            var renderSystem = Systems.Get<RenderSystem>();
            renderSystem.SetVirtualDimensions(currentScene.VirtualWidth, currentScene.VirtualHeight);
        }

        public void ResetScene(Scene? scene = null)
        {
            entityManager.Clear();
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
    }
}
