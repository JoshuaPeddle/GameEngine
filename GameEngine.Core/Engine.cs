using GameEngine.Core.Systems;
using System.Diagnostics;

namespace GameEngine.Core
{
    public class Engine
    {
        public Action? InvalidateAction { get; }

        public readonly SystemContainer Systems = new();

        private readonly EntityManager entityManager = new();
        private readonly InputManager inputManager = new();
        private readonly Stopwatch stopwatch = new();

        private Scene? currentScene;
        private double lastUpdateTime = 0;

        public Engine(Action? invalidateAction = null)
        {
            InvalidateAction = invalidateAction;
            InitializeSystems();
        }

        private void InitializeSystems()
        {
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
            currentScene.Initialize(entityManager, inputManager, Systems.Get<AudioSystem>(), ResetScene);
        }

        public void ResetScene()
        {
            entityManager.Clear();
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
