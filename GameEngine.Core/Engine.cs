using SkiaSharp.Views.Desktop;
using System.Diagnostics;

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
            SkMain.PaintSurface += new EventHandler<SKPaintSurfaceEventArgs>(Paint);
            SkMain.KeyDown += new KeyEventHandler(OnKeyDown);
            SkMain.KeyUp += new KeyEventHandler(OnKeyUp);
            actionMapper = new ActionMapper(inputManager);
            systems.Add(new AnimationSystem());
        }

        public async Task Start()
        {
            stopwatch = new Stopwatch();
            stopwatch.Start();
            lastUpdateTime = 0;

            while (true)
            {
                await Task.Delay(1);
                //await Task.Delay(1000 / (simulationSpeed * 60));
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

            currentScene?.Simulate(deltaTime);
        }

        public void ChangeScene(Scene scene, bool endScene = false)
        {
            currentScene = scene;
            currentScene.Initialize(entityManager, inputManager, actionMapper);
        }

        private void Paint(object? sender, SKPaintSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;
            currentScene?.Render(canvas);
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            Keys key = e.KeyCode;
            currentScene?.HandleAction(key, true);
        }

        private void OnKeyUp(object? sender, KeyEventArgs e)
        {
            Keys key = e.KeyCode;
            currentScene?.HandleAction(key, false);
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
