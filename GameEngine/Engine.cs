using SkiaSharp;
using SkiaSharp.Views.Desktop;
using System.Xml.Serialization;

namespace GameEngine.WinForms
{
    public class Engine
    {
        public SKControl SkMain;
        Scene currentScene;
        int simulationSpeed = 1;

        public Engine(SKControl skMain, Size size, Point location)
        {
            SkMain = skMain;
            SkMain.Size = size;
            SkMain.Location = location;
            SkMain.PaintSurface += new EventHandler<SkiaSharp.Views.Desktop.SKPaintSurfaceEventArgs>(Paint);
            SkMain.KeyPress += new KeyPressEventHandler(OnKeyPress);
        }

        public async Task Start()
        {
            while (true)
            {
                await Task.Delay(1000 / (simulationSpeed*60));
                Update();
                SkMain.Invalidate();
            }
        }

        private void Update()
        {
            currentScene?.Simulate();
        }


        private void OnKeyPress(object sender, KeyPressEventArgs e)
        {
            Keys key = (Keys)char.ToUpper(e.KeyChar);
            currentScene.ExecuteAction(key);
        }

        public void ChangeScene(Scene scene, bool endScene = false)
        {
            currentScene = scene;
        }

        private void Paint(object sender, SKPaintSurfaceEventArgs e)
        {

            var canvas = e.Surface.Canvas;

            
            currentScene.Render(canvas);

            using var paint = new SKPaint
            {
                Color = SKColors.Black,
                IsAntialias = true,
                Style = SKPaintStyle.Fill
            };
            using var font = new SKFont
            {
                Size = 72
            };
            var coord = new SKPoint(e.Info.Width / 2, (e.Info.Height + font.Size) / 2);
            canvas.DrawText("SkiaSharp", coord.X, coord.Y, font, paint);
        }

    }
}
