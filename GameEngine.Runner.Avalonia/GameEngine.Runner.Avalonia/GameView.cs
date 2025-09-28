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
using SharpHook;
using SharpHook.Native;
using SharpHook.Reactive;
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
        private readonly SimpleReactiveGlobalHook _keyboardHook;

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
                _keyboardHook = new SimpleReactiveGlobalHook(GlobalHookType.Keyboard, runAsyncOnBackgroundThread: true);
                ConfigureKeyEvents();
                _keyboardHook.RunAsync();
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

                _gameEngine.TargetFrameRate = OperatingSystem.IsBrowser() ? 60 : 14400;
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

        public void ConfigureKeyEvents()
        {
            _keyboardHook.KeyPressed
                .Subscribe(KeyPressed);

            _keyboardHook.KeyReleased
                .Subscribe(KeyReleased);
        }

        void KeyPressed(KeyboardHookEventArgs args)
        {
            if (KeyMap.TryGetValue(args.Data.KeyCode, out GeKeys value))
                _gameEngine.Systems.Get<InputSystem>().KeyDown(value);
        }

        void KeyReleased(KeyboardHookEventArgs args)
        {
            if (KeyMap.TryGetValue(args.Data.KeyCode, out GeKeys value))
                _gameEngine.Systems.Get<InputSystem>().KeyUp(value);
        }

        public Dictionary<KeyCode, GeKeys> KeyMap { get; private set; } = new Dictionary<KeyCode, GeKeys>()
        {
            { KeyCode.VcW, GeKeys.W },
            { KeyCode.VcA, GeKeys.A },
            { KeyCode.VcS, GeKeys.S },
            { KeyCode.VcD, GeKeys.D },
            { KeyCode.VcSpace, GeKeys.Space }
        };
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