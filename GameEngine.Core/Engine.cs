using SkiaSharp.Views.Desktop;

namespace GameEngine.Core
{
    public class Engine
    {
        public SKControl SkMain;
        Scene? currentScene;
        readonly int simulationSpeed = 1;

        public Engine(SKControl skMain, Size size, Point location)
        {
            SkMain = skMain;
            SkMain.Size = size;
            SkMain.Location = location;
            SkMain.PaintSurface += new EventHandler<SKPaintSurfaceEventArgs>(Paint);
            SkMain.KeyPress += new KeyPressEventHandler(OnKeyPress);
        }

        public async Task Start()
        {
            while (true)
            {
                await Task.Delay(1);//await Task.Delay(1000 / (simulationSpeed * 60));
                Update();
                SkMain.Invalidate();
            }
        }

        private void Update()
        {
            currentScene?.Simulate();
        }

        public void ChangeScene(Scene scene, bool endScene = false)
        {
            currentScene = scene;
        }

        private void Paint(object? sender, SKPaintSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;
            currentScene?.Render(canvas);
        }

        private void OnKeyPress(object? sender, KeyPressEventArgs e)
        {
            Keys key = (Keys)char.ToUpper(e.KeyChar);
            currentScene?.ExecuteAction(key);
        }
    }
}
