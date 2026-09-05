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
        private Point? _pointerStartPosition;
        private const double SwipeThreshold = 20.0;
        private const int SyntheticKeyHoldMs = 100;

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

            skglControl1.MouseDown += (sender, args) => Input?.PointerPressed(new PointerPressEvent(new Vec2(args.X, args.Y)));
            skglControl1.MouseMove += (sender, args) => Input?.PointerMoved(new PointerMoveEvent(new Vec2(args.X, args.Y)));
            skglControl1.MouseUp += (sender, args) => Input?.PointerReleased(new PointerReleaseEvent(new Vec2(args.X, args.Y)));

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

        private async Task ReleaseKeyAfterTapAsync(GeKeys key)
        {
            await Task.Delay(SyntheticKeyHoldMs);
            Input?.KeyUp(key);
        }

        private void OnSizeChanged(object? sender, EventArgs args)
        {
            _gameEngine.SizeChanged(skglControl1.Width, skglControl1.Height);
        }

        private void OnPointerPressed(object? sender, MouseEventArgs e)
        {
            var point = e.Location;
            _pointerStartPosition = point;
            Input?.PointerPressed(new PointerPressEvent(new Vec2(point.X, point.Y)));
        }
        private void OnPointerMoved(object? sender, MouseEventArgs e)
        {
            if (_pointerStartPosition.HasValue)
            {
                var point = e.Location;
                Input?.PointerMoved(new PointerMoveEvent(new Vec2(point.X, point.Y)));
            }
        }

        private void OnPointerReleased(object? sender, MouseEventArgs e)
        {
            if (_pointerStartPosition.HasValue)
            {
                var endPosition = e.Location;
                Input?.PointerReleased(new PointerReleaseEvent(new Vec2(endPosition.X, endPosition.Y)));

                var startPosition = _pointerStartPosition.Value;

                var key = SwipeGesture.Classify(
                    new Vec2(startPosition.X, startPosition.Y),
                    new Vec2(endPosition.X, endPosition.Y),
                    SwipeThreshold);

                Input?.KeyDown(key);
                _ = ReleaseKeyAfterTapAsync(key);

                _pointerStartPosition = null;
            }
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
            renderSystem.DrawEntitiesToCanvas(canvas, _gameEngine.GetRenderSnapshot());

            // Resume updates after first visible frame of a new scene
            _gameEngine.NotifyFirstPresent();
        }
    }
}
