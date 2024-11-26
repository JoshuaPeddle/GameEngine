using GameEngine.Core.Systems;
using System.Diagnostics;

namespace GameEngine.Core
{
    public class Engine
    {
        public RenderSystem? RenderSystem { get; }
        public InputSystem? InputSystem { get; }
        public Action? InvalidateAction { get; }

        private readonly List<ISystem> systems = [];

        private readonly EntityManager entityManager = new();
        private readonly InputManager inputManager = new();
        private readonly Stopwatch stopwatch;
        private Scene? currentScene;
        private long lastUpdateTicks = 0;

        public Engine(Action? invalidateAction = null)
        {
            InvalidateAction = invalidateAction;
            InputSystem = new InputSystem(inputManager);
            systems.Add(InputSystem);

            systems.Add(new MovementSystem());
            systems.Add(new PhysicsSystem());
            systems.Add(new AnimationSystem());
            RenderSystem = new RenderSystem(
                entityManager,
                new RenderOptions()
                {
                    DrawAnimations = true,
                    DrawBoundingBoxes = true,
                    DrawEntityCenters = true
                });
            systems.Add(RenderSystem);
            stopwatch = new Stopwatch();
        }

        public async Task Start()
        {
            stopwatch.Start();
            lastUpdateTicks = 0;

            while (true)
            {
                await Task.Delay(1);
                Update(CalculateDeltaTime());
                InvalidateAction?.Invoke();
            }
        }

        private void Update(double deltaTime)
        {
            foreach (ISystem system in systems)
            {
                system.Update(entityManager, deltaTime);
            }
            entityManager.Update();
        }

        public void ChangeScene(Scene scene)
        {
            currentScene = scene;
            currentScene.Initialize(entityManager, inputManager);
        }

        private double CalculateDeltaTime()
        {
            long currentTicks = stopwatch.ElapsedTicks;
            double deltaTime = (currentTicks - lastUpdateTicks) / (double)Stopwatch.Frequency;
            lastUpdateTicks = currentTicks;
            return deltaTime * 1000;
        }
    }
}
