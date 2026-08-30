using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
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
    // Hosts one engine. Everything it needs can be handed to it — an engine, a scene, an asset
    // source — so several views can run side by side in one process. When nothing is supplied it
    // falls back to App's statics, which is what the single-game app template wants.
    public class GameView : Control
    {
        public static readonly StyledProperty<Engine?> EngineProperty =
            AvaloniaProperty.Register<GameView, Engine?>(nameof(Engine));

        public static readonly StyledProperty<Scene?> SceneProperty =
            AvaloniaProperty.Register<GameView, Scene?>(nameof(Scene));

        public static readonly StyledProperty<IAssetSource?> AssetSourceProperty =
            AvaloniaProperty.Register<GameView, IAssetSource?>(nameof(AssetSource));

        public static readonly StyledProperty<bool> AudioEnabledProperty =
            AvaloniaProperty.Register<GameView, bool>(nameof(AudioEnabled), defaultValue: PlatformSupportsAudio());

        // The engine of the most recently attached view. A convenience for hosts that have no
        // reference to the control — the browser head's JSExport interop, for one.
        public static Engine? Current { get; private set; }

        private Engine? _gameEngine;

        private static readonly Dictionary<Key, GeKeys> KeyMap = BuildKeyMap();
        private readonly HashSet<GeKeys> _heldKeys = new();

        private Point? _pointerStartPosition;
        private const double SwipeThreshold = 20.0;
        private const int SyntheticKeyHoldMs = 100;

        private int _invalidationsPending = 0;
        private int _firstPresentReported;
        private bool _started;
        private bool _ownsEngine;

        public GameView()
        {
            IsHitTestVisible = true;
            Focusable = true;

            LostFocus += OnLostFocus;

            PointerPressed += OnPointerPressed;
            PointerMoved += OnPointerMoved;
            PointerReleased += OnPointerReleased;

            Loaded += OnSizeChanged;
            SizeChanged += OnSizeChanged;
        }

        public Engine? Engine
        {
            get => GetValue(EngineProperty);
            set => SetValue(EngineProperty, value);
        }

        public Scene? Scene
        {
            get => GetValue(SceneProperty);
            set => SetValue(SceneProperty, value);
        }

        public IAssetSource? AssetSource
        {
            get => GetValue(AssetSourceProperty);
            set => SetValue(AssetSourceProperty, value);
        }

        public bool AudioEnabled
        {
            get => GetValue(AudioEnabledProperty);
            set => SetValue(AudioEnabledProperty, value);
        }

        // Called instead of Scene when the host wants a fresh scene per view.
        public Func<Scene>? SceneFactory { get; set; }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);

            var engine = EnsureEngine();
            Focus();

            if (_started) return;
            _started = true;

            if (_ownsEngine)
            {
                engine.TargetFrameRate = OperatingSystem.IsBrowser() ? 60 : 240;
                engine.Start();
            }
        }

        private Engine EnsureEngine()
        {
            if (_gameEngine != null)
                return _gameEngine;

            var supplied = Engine;
            if (supplied != null)
            {
                supplied.InvalidateAction = QueueInvalidate;
                _gameEngine = supplied;
                _ownsEngine = false;
            }
            else
            {
                var assetSource = AssetSource ?? App.AssetSource ?? new FileAssetSource();
                _gameEngine = new Engine(QueueInvalidate, AudioEnabled, assetSource);
                _ownsEngine = true;

                var scene = Scene ?? SceneFactory?.Invoke() ?? App.StartupScene?.Invoke();
                if (scene != null)
                    _gameEngine.ChangeScene(scene);
            }

            Current = _gameEngine;
            return _gameEngine;
        }

        private static bool PlatformSupportsAudio() =>
            !OperatingSystem.IsAndroid() && !OperatingSystem.IsBrowser();

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
            Input?.KeyUp(key);
        }

        private InputSystem? Input => _gameEngine?.Systems.Get<InputSystem>();

        private void OnSizeChanged(object? sender, EventArgs args)
        {
            _gameEngine?.SizeChanged((int)Bounds.Width, (int)Bounds.Height);
        }

        private static Dictionary<Key, GeKeys> BuildKeyMap()
        {
            var map = new Dictionary<Key, GeKeys>();

            foreach (var engineKey in Enum.GetValues<GeKeys>())
            {
                if (Enum.TryParse<Key>(engineKey.ToString(), out var avaloniaKey))
                    map[avaloniaKey] = engineKey;
            }

            return map;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (KeyMap.TryGetValue(e.Key, out var key))
            {
                _heldKeys.Add(key);
                Input?.KeyDown(key);
                e.Handled = true;
            }

            base.OnKeyDown(e);
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            if (KeyMap.TryGetValue(e.Key, out var key))
            {
                _heldKeys.Remove(key);
                Input?.KeyUp(key);
                e.Handled = true;
            }

            base.OnKeyUp(e);
        }

        private void OnLostFocus(object? sender, RoutedEventArgs e)
        {
            var input = Input;

            foreach (var key in _heldKeys)
                input?.KeyUp(key);

            _heldKeys.Clear();
        }

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            Focus();

            var point = e.GetPosition(this);
            _pointerStartPosition = point;
            Input?.PointerPressed(new PointerPressEvent(new Vec2(point.X, point.Y)));
        }

        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            if (_pointerStartPosition.HasValue)
            {
                var point = e.GetPosition(this);
                Input?.PointerMoved(new PointerMoveEvent(new Vec2(point.X, point.Y)));
            }
        }

        private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_pointerStartPosition.HasValue)
            {
                var endPosition = e.GetPosition(this);
                Input?.PointerReleased(new PointerReleaseEvent(new Vec2(endPosition.X, endPosition.Y)));

                var startPosition = _pointerStartPosition.Value;

                var key = Core.SwipeGesture.Classify(
                    new Vec2(startPosition.X, startPosition.Y),
                    new Vec2(endPosition.X, endPosition.Y),
                    SwipeThreshold);

                Input?.KeyDown(key);
                _ = ReleaseKeyAfterTapAsync(key);

                _pointerStartPosition = null;
            }
        }

        public override void Render(DrawingContext context)
        {
            if (_gameEngine == null)
                return;

            context.Custom(new CustomDrawOp(
                new Rect(0, 0, Bounds.Width, Bounds.Height),
                _gameEngine,
                ReportFirstPresent));
        }

        private void ReportFirstPresent()
        {
            _gameEngine?.NotifyFirstPresent();

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
