using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using GameEngine.Core;
using GameEngine.Core.Systems;
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
        private static Engine? _current;
        public static Engine? Current => _current;

        private Engine _gameEngine;
        private readonly IDisposable? _keyboardHook;
        

        private Point? _pointerStartPosition;
        private const double SwipeThreshold = 20.0;
        private const int SyntheticKeyHoldMs = 100;

        private int _invalidationsPending = 0;
        private int _firstPresentReported;
        private bool _started;

        public GameView()
        {
            IsHitTestVisible = true;

            var assetSource = App.AssetSource ?? new FileAssetSource();

            if (OperatingSystem.IsAndroid() || OperatingSystem.IsBrowser())
                _gameEngine = new Engine(QueueInvalidate, audioEnabled: false, assetSource);
            else
                _gameEngine = new Engine(QueueInvalidate, assetSource: assetSource);

            _current = _gameEngine;

            var startupScene = App.StartupScene?.Invoke();
            if (startupScene != null)
                _gameEngine.ChangeScene(startupScene);

            // SharpHook uses native desktop window-system libraries (X11, Win32,
            // or AppKit). Loading it on Android crashes before the first frame.
            if (OperatingSystem.IsWindows() ||
                OperatingSystem.IsLinux() ||
                OperatingSystem.IsMacOS())
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

                _gameEngine.TargetFrameRate = OperatingSystem.IsBrowser() ? 60 : 240;
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

        private async Task ReleaseKeyAfterTapAsync(GeKeys key)
        {
            await Task.Delay(SyntheticKeyHoldMs);
            _gameEngine.Systems.Get<InputSystem>().KeyUp(key);
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

                var key = Core.SwipeGesture.Classify(
                    new Vec2(startPosition.X, startPosition.Y),
                    new Vec2(endPosition.X, endPosition.Y),
                    SwipeThreshold);

                _gameEngine.Systems.Get<InputSystem>().KeyDown(key);
                _ = ReleaseKeyAfterTapAsync(key);

                _pointerStartPosition = null;
            }
        }

        public override void Render(DrawingContext context)
        {
            context.Custom(new CustomDrawOp(
                new Rect(0, 0, Bounds.Width, Bounds.Height),
                _gameEngine,
                ReportFirstPresent));
        }

        private void ReportFirstPresent()
        {
            _gameEngine.NotifyFirstPresent();

            if (Interlocked.Exchange(ref _firstPresentReported, 1) == 0)
                App.FirstFramePresented?.Invoke();
        }
    }

    class CustomDrawOp : ICustomDrawOperation
    {
        public Rect Bounds { get; set; }
        private readonly Engine _engine;
        private readonly Action _reportFirstPresent;

        public CustomDrawOp(Rect bounds, Engine engine, Action reportFirstPresent)
        {
            Bounds = bounds;
            _engine = engine;
            _reportFirstPresent = reportFirstPresent;
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
            _engine.Systems.Get<RenderSystem>().DrawEntitiesToCanvas(canvas, _engine.GetRenderSnapshot());

            // Resume updates and report that the first visible frame reached the platform surface.
            _reportFirstPresent();
        }
    }
}
