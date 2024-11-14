using SkiaSharp.Views.Desktop;
using System.Diagnostics;
using GameEngine.Core.Systems;

namespace GameEngine.Core
{
    public class Engine
    {
        public SKControl SkMain;
        Scene? currentScene;
        private readonly List<ISystem> systems = new List<ISystem>();
        private readonly EntityManager entityManager = new EntityManager();
        private readonly InputManager inputManager = new InputManager();
        private readonly ActionMapper actionMapper;
        private Stopwatch stopwatch;
        private long lastUpdateTime;

        public Engine(SKControl skMain, Size size, Point location)
        {
            SkMain = skMain;
            SkMain.Size = size;
            SkMain.Location = location;
            actionMapper = new ActionMapper(inputManager);

            var inputSystem = new InputSystem(inputManager, actionMapper);
            systems.Add(inputSystem);
            SkMain.KeyDown += new KeyEventHandler(inputSystem.OnKeyDown);
            SkMain.KeyUp += new KeyEventHandler(inputSystem.OnKeyUp);
            systems.Add(new MovementSystem());
            systems.Add(new AnimationSystem());
            systems.Add(new RenderSystem(SkMain, entityManager));
        }

        public async Task Start()
        {
            stopwatch = new Stopwatch();
            stopwatch.Start();
            lastUpdateTime = 0;

            while (true)
            {
                await Task.Delay(1);
                float deltaTime = CalculateDeltaTime();

                Update(deltaTime); 
                SkMain.Invalidate();
                SkMain.Update();
            }
        }

        private void Update(float deltaTime)
        {
            foreach (ISystem system in systems)
            {
                system.Update(entityManager, deltaTime);
            }
            entityManager.Update();
        }

        public void ChangeScene(Scene scene, bool endScene = false)
        {
            currentScene = scene;
            currentScene.Initialize(entityManager, inputManager, actionMapper);
        }
    
        private float CalculateDeltaTime()
        {
            long currentTime = stopwatch.ElapsedMilliseconds;
            float deltaTime = currentTime - lastUpdateTime;
            lastUpdateTime = currentTime;
            return deltaTime;
        }
    }
}
