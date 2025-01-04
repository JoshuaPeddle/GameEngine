using GameEngine.Core;
using GameEngine.Core.Systems;
using GameEngine.Demo;
using SkiaSharp.Views.Desktop;

namespace GameEngine
{
    public partial class MainView : Form
    {

        private readonly Engine _gameEngine;
        public MainView()
        {
            InitializeComponent();
            skglControl1.PaintSurface += OnPaintSurface;
            skglControl1.Size = new Size(1161, 671);
            skglControl1.Location = new Point(12, 12);

            _gameEngine = new Engine(skglControl1.Invalidate);


            skglControl1.KeyDown += KeyPressed;
            skglControl1.KeyUp += KeyReleased;

            _gameEngine.ChangeScene(new SceneJson());
            _gameEngine.Start(); // Dont await this, it will block the UI thread
        }

        void KeyPressed(object? sender, KeyEventArgs args)
        {
            if (KeyMap.TryGetValue(args.KeyCode, out GeKeys value))
                _gameEngine.Systems.Get<InputSystem>().KeyDown(value);

        }

        void KeyReleased(object? sender, KeyEventArgs args)
        {
            
            if (KeyMap.TryGetValue(args.KeyCode, out GeKeys value))
                _gameEngine.Systems.Get<InputSystem>().KeyUp(value);
        }

        public Dictionary<Keys, GeKeys> KeyMap { get; private set; } = new Dictionary<Keys, GeKeys>()
        {
            { Keys.W, GeKeys.W },
            { Keys.A, GeKeys.A },
            { Keys.S, GeKeys.S },
            { Keys.D, GeKeys.D },
            { Keys.Space, GeKeys.Space }
        };

        private void OnPaintSurface(object? sender, SKPaintGLSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;
            _gameEngine.Systems.Get<RenderSystem>().DrawEntitiesToCanvas(canvas);
        }
    }
}
