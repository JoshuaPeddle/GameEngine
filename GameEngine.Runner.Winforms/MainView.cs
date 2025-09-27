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

        // Coalesce pending invalidations
        private int _invalidationsPending = 0;

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

            this.Load += OnSizeChanged;
            this.SizeChanged += OnSizeChanged;
            skglControl1.MouseDown += OnPointerPressed;
            skglControl1.MouseMove += OnPointerMoved;
            skglControl1.MouseUp += OnPointerReleased;

            skglControl1.KeyDown += KeyPressed;
            skglControl1.KeyUp += KeyReleased;

            skglControl1.MouseDown += (sender, args) => _gameEngine.Systems.Get<InputSystem>().PointerPressed(new PointerPressEvent(new Vec2(args.X, args.Y)));
            skglControl1.MouseMove += (sender, args) => _gameEngine.Systems.Get<InputSystem>().PointerMoved(new PointerMoveEvent(new Vec2(args.X, args.Y)));
            skglControl1.MouseUp += (sender, args) => _gameEngine.Systems.Get<InputSystem>().PointerReleased(new PointerReleaseEvent(new Vec2(args.X, args.Y)));

            _gameEngine.ChangeScene(new SceneMenu());
            _gameEngine.TargetFrameRate = 1000;

            _gameEngine.Start(); // Dont await this, it will block the UI thread
        }

        private void OnSizeChanged(object? sender, EventArgs args)
        {
            _gameEngine.SizeChanged((int)Bounds.Width, (int)Bounds.Height);
        }

        private void OnPointerPressed(object? sender, MouseEventArgs e)
        {
            var point = e.Location;
            _pointerStartPosition = point;
            _gameEngine.Systems.Get<InputSystem>().PointerPressed(new PointerPressEvent(new Vec2(point.X, point.Y)));
        }
        private void OnPointerMoved(object? sender, MouseEventArgs e)
        {
            if (_pointerStartPosition.HasValue)
            {
                var point = e.Location;
                _gameEngine.Systems.Get<InputSystem>().PointerMoved(new PointerMoveEvent(new Vec2(point.X, point.Y)));
            }
        }

        private void OnPointerReleased(object? sender, MouseEventArgs e)
        {
            if (_pointerStartPosition.HasValue)
            {
                var endPosition = e.Location;
                _gameEngine.Systems.Get<InputSystem>().PointerReleased(new PointerReleaseEvent(new Vec2(endPosition.X, endPosition.Y)));

                var startPosition = _pointerStartPosition.Value;

                var deltaX = endPosition.X - startPosition.X;
                var deltaY = endPosition.Y - startPosition.Y;
                var distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);

                // If the distance is small, consider it a tap
                if (distance < SwipeThreshold)
                {
                    Console.WriteLine("Tap detected - Space key");
                    _gameEngine.Systems.Get<InputSystem>().KeyDown(GeKeys.Space);
                    // Simulate key up after a brief delay to mimic key press behavior
                    Task.Delay(100).ContinueWith(_ => _gameEngine.Systems.Get<InputSystem>().KeyUp(GeKeys.Space));
                }
                // Otherwise it's a swipe - determine direction
                else
                {
                    // Determine if horizontal or vertical swipe based on which delta is larger
                    if (Math.Abs(deltaX) > Math.Abs(deltaY))
                    {
                        // Horizontal swipe
                        if (deltaX > 0)
                        {
                            _gameEngine.Systems.Get<InputSystem>().KeyDown(GeKeys.D);
                            Task.Delay(100).ContinueWith(_ => _gameEngine.Systems.Get<InputSystem>().KeyUp(GeKeys.D));
                        }
                        else
                        {
                            _gameEngine.Systems.Get<InputSystem>().KeyDown(GeKeys.A);
                            Task.Delay(100).ContinueWith(_ => _gameEngine.Systems.Get<InputSystem>().KeyUp(GeKeys.A));
                        }
                    }
                    else
                    {
                        // Vertical swipe
                        if (deltaY > 0)
                        {
                            _gameEngine.Systems.Get<InputSystem>().KeyDown(GeKeys.S);
                            Task.Delay(100).ContinueWith(_ => _gameEngine.Systems.Get<InputSystem>().KeyUp(GeKeys.S));
                        }
                        else
                        {
                            _gameEngine.Systems.Get<InputSystem>().KeyDown(GeKeys.W);
                            Task.Delay(100).ContinueWith(_ => _gameEngine.Systems.Get<InputSystem>().KeyUp(GeKeys.W));
                        }
                    }
                }

                _pointerStartPosition = null;
            }
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

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
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

            // Resume updates after first visible frame of a new scene
            _gameEngine.NotifyFirstPresent();
        }
    }
}
