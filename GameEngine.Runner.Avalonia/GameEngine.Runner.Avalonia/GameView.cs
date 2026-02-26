using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using GameEngine.Core;
using GameEngine.Core.Systems;
using GameEngine.Demo;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using static GameEngine.Core.Pointer;
using Avalonia.Threading;
using System.Threading;

namespace GameEngine.Runner.Avalonia
{
    public class GameView : Control
    {
        public static Engine _gameEngine;
        private readonly IDisposable? _keyboardHook;
        

        private Point? _pointerStartPosition;
        private const double SwipeThreshold = 20.0;

        private int _invalidationsPending = 0;
        private bool _started;

        public GameView()
        {
            IsHitTestVisible = true;

            if (OperatingSystem.IsAndroid() || OperatingSystem.IsBrowser())
                _gameEngine = new Engine(QueueInvalidate, audioEnabled: false);
            else
                _gameEngine = new Engine(QueueInvalidate);

            _gameEngine.ChangeScene(new SceneMenu());

            if (!OperatingSystem.IsBrowser())
            {
                _keyboardHook = KeyboardHookHelper.Create(_gameEngine);
            }

            PointerPressed += OnPointerPressed;
            PointerMoved += OnPointerMoved;
            PointerReleased += OnPointerReleased;

            Loaded += OnSizeChanged;
            SizeChanged += OnSizeChanged;

            AttachedToVisualTree += (_, __) =>
            {
                if (_started) return;
                _started = true;

                Engine.TargetFrameRate = OperatingSystem.IsBrowser() ? 60 : 14400;
                _gameEngine.Start();
            };
        }

        private void QueueInvalidate()
        {
            if (Interlocked.Exchange(ref _invalidationsPending, 1) == 0)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    _invalidationsPending = 0;
                    InvalidateVisual();
                }, DispatcherPriority.Render);
            }
        }

        private void OnSizeChanged(object? sender, EventArgs args)
        {
            _gameEngine.SizeChanged((int)Bounds.Width, (int)Bounds.Height);
        }

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            var point = e.GetPosition(this);
            _pointerStartPosition = point;
            _gameEngine.Systems.Get<InputSystem>().PointerPressed(new PointerPressEvent(new Vec2(point.X, point.Y)));
        }

        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            if (_pointerStartPosition.HasValue)
            {
                var point = e.GetPosition(this);
                _gameEngine.Systems.Get<InputSystem>().PointerMoved(new PointerMoveEvent(new Vec2(point.X, point.Y)));
            }
        }

        private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_pointerStartPosition.HasValue)
            {
                var endPosition = e.GetPosition(this);
                _gameEngine.Systems.Get<InputSystem>().PointerReleased(new PointerReleaseEvent(new Vec2(endPosition.X, endPosition.Y)));

                var startPosition = _pointerStartPosition.Value;

                var deltaX = endPosition.X - startPosition.X;
                var deltaY = endPosition.Y - startPosition.Y;
                var distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);

                if (distance < SwipeThreshold)
                {
                    _gameEngine.Systems.Get<InputSystem>().KeyDown(GeKeys.Space);
                    Task.Delay(100).ContinueWith(_ => _gameEngine.Systems.Get<InputSystem>().KeyUp(GeKeys.Space));
                }
                else
                {
                    if (Math.Abs(deltaX) > Math.Abs(deltaY))
                    {
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

        public override void Render(DrawingContext context)
        {
            context.Custom(new CustomDrawOp(new Rect(0, 0, Bounds.Width, Bounds.Height), _gameEngine));
        }
    }

    class CustomDrawOp : ICustomDrawOperation
    {
        public Rect Bounds { get; set; }
        private readonly Engine _engine;

        public CustomDrawOp(Rect bounds, Engine engine)
        {
            Bounds = bounds;
            _engine = engine;
        }

        public void Dispose() { }

        public bool Equals(ICustomDrawOperation? other) => false;

        public bool HitTest(Point p) => Bounds.Contains(p);

        public void Render(ImmediateDrawingContext context)
        {
            var leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (leaseFeature == null)
                return;
            using var lease = leaseFeature.Lease();
            var canvas = lease.SkCanvas;
            _engine.Systems.Get<RenderSystem>().DrawEntitiesToCanvas(canvas);

            // Resume updates immediately after first visible frame
            _engine.NotifyFirstPresent();
        }
    }
}