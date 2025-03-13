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

namespace GameEngine.Runner.Avalonia
{
    public class GameView : Control
    {
        public static Engine _gameEngine;
        private readonly SimpleReactiveGlobalHook _keyboardHook;

        private Point? _pointerStartPosition;
        private const double SwipeThreshold = 20.0; 

        public GameView()
        {
            IsHitTestVisible = true;

            if (OperatingSystem.IsAndroid() || OperatingSystem.IsBrowser())
                _gameEngine = new Engine(InvalidateVisual, audioEnabled: false);
            else
                _gameEngine = new Engine(InvalidateVisual);
            _gameEngine.ChangeScene(new SceneMenu());

            if (!OperatingSystem.IsBrowser())
            {
                _keyboardHook = new SimpleReactiveGlobalHook(GlobalHookType.Keyboard, runAsyncOnBackgroundThread: true);
                ConfigureKeyEvents();
                _keyboardHook.RunAsync();
            }

            this.PointerPressed += OnPointerPressed;
            this.PointerMoved += OnPointerMoved;
            this.PointerReleased += OnPointerReleased;

            _gameEngine.Start();
        }

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            var point = e.GetPosition(this);
            _pointerStartPosition = point;
        }

        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            if (_pointerStartPosition.HasValue)
            {
                var currentPosition = e.GetPosition(this);
            }
        }

        private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_pointerStartPosition.HasValue)
            {
                var endPosition = e.GetPosition(this);
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
        }
    }
}