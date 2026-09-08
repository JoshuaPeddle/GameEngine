using GameEngine.Core;
using GameEngine.Core.Systems;
using GameEngine.Demo;
using SkiaSharp.Views.Desktop;
using System.ComponentModel;
using static GameEngine.Core.Pointer;

namespace GameEngine
{
    public partial class MainView : Form
    {
        private readonly Engine _gameEngine;

        // Coalesce pending invalidations
        private int _invalidationsPending = 0;

        // TryGet rather than Get: the container is empty between the engine's disposal and the
        // last queued control event that still refers to it.
        private InputSystem? Input => _gameEngine.Systems.TryGet<InputSystem>();

        public MainView()
        {
            InitializeComponent();
            skglControl1.PaintSurface += OnPaintSurface;

            _gameEngine = new Engine(() =>
            {
                if (!skglControl1.IsHandleCreated) return;

                if (Interlocked.Exchange(ref _invalidationsPending, 1) == 0)
                {
                    skglControl1.BeginInvoke(new Action(() =>
                    {
                        _invalidationsPending = 0;
                        skglControl1.Invalidate();
                    }));
                }
            });

            skglControl1.SizeChanged += OnSizeChanged;
            this.Load += OnSizeChanged;
            skglControl1.MouseDown += OnPointerPressed;
            skglControl1.MouseMove += OnPointerMoved;
            skglControl1.MouseUp += OnPointerReleased;

            skglControl1.KeyDown += KeyPressed;
            skglControl1.KeyUp += KeyReleased;


            _gameEngine.ChangeScene(new SceneMenu());
            _gameEngine.TargetFrameRate = 1000;

            _gameEngine.Start(); // Dont await this, it will block the UI thread

            FormClosed += (_, _) =>
            {
                _gameEngine.InvalidateAction = null;
                _gameEngine.Stop();
                _gameEngine.Dispose();
            };
        }

        private void OnSizeChanged(object? sender, EventArgs args)
        {
            _gameEngine.SizeChanged(skglControl1.Width, skglControl1.Height);
        }

        private void OnPointerPressed(object? sender, MouseEventArgs e)
        {
            skglControl1.Focus();
            var point = e.Location;
            Input?.PointerPressed(new PointerPressEvent(new Vec2(point.X, point.Y)));
        }

        private void OnPointerMoved(object? sender, MouseEventArgs e)
        {
            var point = e.Location;
            Input?.PointerMoved(new PointerMoveEvent(new Vec2(point.X, point.Y)));
        }

        private void OnPointerReleased(object? sender, MouseEventArgs e)
        {
            var point = e.Location;
            Input?.PointerReleased(new PointerReleaseEvent(new Vec2(point.X, point.Y)));
        }

        void KeyPressed(object? sender, KeyEventArgs args)
        {
            if (KeyMap.TryGetValue(args.KeyCode, out GeKeys value))
                Input?.KeyDown(value);

        }

        void KeyReleased(object? sender, KeyEventArgs args)
        {
            
            if (KeyMap.TryGetValue(args.KeyCode, out GeKeys value))
                Input?.KeyUp(value);
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Dictionary<Keys, GeKeys> KeyMap { get; private set; } = BuildKeyMap();

        private static Dictionary<Keys, GeKeys> BuildKeyMap()
        {
            var map = new Dictionary<Keys, GeKeys>();

            foreach (var engineKey in Enum.GetValues<GeKeys>())
            {
                if (Enum.TryParse<Keys>(engineKey.ToString(), out var winFormsKey))
                    map[winFormsKey] = engineKey;
            }

            return map;
        }

        private void OnPaintSurface(object? sender, SKPaintGLSurfaceEventArgs e)
        {
            var renderSystem = _gameEngine.Systems.TryGet<RenderSystem>();
            if (renderSystem == null)
                return;

            var canvas = e.Surface.Canvas;
            using var snapshot = _gameEngine.AcquireRenderSnapshot();
            renderSystem.DrawEntitiesToCanvas(canvas, snapshot.Snapshot);

            // Resume updates after first visible frame of a new scene
            _gameEngine.NotifyFirstPresent();
        }
    }
}
