using GameEngine.Core.Systems;
using SkiaSharp.Views.Desktop;
using System.Diagnostics;

namespace GameEngine.Core
{
    public class Engine
    {
        public SKGLControl SkMain;
        private Scene? currentScene;
        private readonly List<ISystem> systems = [];
        private readonly EntityManager entityManager = new();
        private readonly InputManager inputManager = new();
        private readonly Stopwatch stopwatch;
        private long lastUpdateTicks = 0;

        public Engine(SKGLControl skMain, Size size, Point location)
        {
            SkMain = skMain;
            SkMain.Size = size;
            SkMain.Location = location;

            var inputSystem = new InputSystem(inputManager);
            systems.Add(inputSystem);
            SkMain.KeyDown += new KeyEventHandler(inputSystem.OnKeyDown);
            SkMain.KeyUp += new KeyEventHandler(inputSystem.OnKeyUp);
            systems.Add(new MovementSystem());
            systems.Add(new PhysicsSystem());
            systems.Add(new AnimationSystem());
            systems.Add(new RenderSystem(
                SkMain, entityManager,
                new RenderOptions()
                {
                    DrawAnimations = true,
                    DrawBoundingBoxes = true,
                    DrawEntityCenters = true
                }));
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
                SkMain.Invalidate();
                SkMain.Update();
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
            currentScene.Initialize(entityManager, inputManager, new ActionMapper(inputManager));
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
