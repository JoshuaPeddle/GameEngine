using GameEngine.Core;
using GameEngine.Demo;
using SkiaSharp.Views.Desktop;

namespace GameEngine
{
    public partial class MainView : Form
    {

        private Engine _gameEngine;
        public MainView()
        {
            InitializeComponent();
            skglControl1.PaintSurface += OnPaintSurface;
            skglControl1.Size = new Size(1161, 671);
            skglControl1.Location = new Point(12, 12);

            _gameEngine = new Engine(skglControl1.Invalidate);

            skglControl1.KeyDown += new KeyEventHandler(_gameEngine.InputSystem.OnKeyDown);
            skglControl1.KeyUp += new KeyEventHandler(_gameEngine.InputSystem.OnKeyUp);

            _gameEngine.ChangeScene(new SceneJson());
            _gameEngine.Start(); // Dont await this, it will block the UI thread
        }

        private void OnPaintSurface(object? sender, SKPaintGLSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;
            _gameEngine.RenderSystem?.DrawEntitiesToCanvas(canvas);
        }
    }
}
