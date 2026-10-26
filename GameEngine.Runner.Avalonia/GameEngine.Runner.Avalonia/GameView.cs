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
            AvaloniaProperty.Register<GameView, bool>(nameof(AudioEnabled), defaultValue: true);

        public static readonly StyledProperty<int> TargetFrameRateProperty =
            AvaloniaProperty.Register<GameView, int>(nameof(TargetFrameRate),
                defaultValue: OperatingSystem.IsBrowser() || IsMobile() ? 60 : 240,
                validate: value => value > 0);

        public static readonly StyledProperty<double> FixedTimeStepProperty =
            AvaloniaProperty.Register<GameView, double>(nameof(FixedTimeStep),
                defaultValue: IsMobile() ? 1.0 / 60 : Core.Engine.DefaultFixedTimeStep,
                validate: value => double.IsFinite(value) && value > 0);

        private static bool IsMobile() => OperatingSystem.IsAndroid() || OperatingSystem.IsIOS();

        // The engine of the most recently attached view. A convenience for hosts that have no
        // reference to the control — the browser head's JSExport interop, for one.
        public static Engine? Current { get; private set; }

        private Engine? _gameEngine;

        private static readonly Dictionary<Key, GeKeys> KeyMap = BuildKeyMap();
        private readonly HashSet<GeKeys> _heldKeys = new();
        private readonly Dictionary<long, GeKeys> _syntheticKeys = new();
        private long _nextSyntheticSource;

        private readonly Dictionary<long, (IPointer Pointer, Point Start, Point Last)> _contacts = new();
        private TopLevel? _topLevel;
        public HostLifecycle? Lifecycle { get; set; }
        private HostLifecycle? _attachedLifecycle;
        private const double SwipeThreshold = 20.0;
        private const int SyntheticKeyHoldMs = 100;

        private int _invalidationsPending = 0;
        private int _firstPresentReported;
        private bool _started;
        private bool _ownsEngine;
        private Action? _previousInvalidateAction;
        private CancellationTokenSource _syntheticKeyReleases = new();
        private readonly Action _queueInvalidate;

        public GameView()
        {
            _queueInvalidate = QueueInvalidate;

            IsHitTestVisible = true;
            Focusable = true;

            LostFocus += OnLostFocus;

            PointerPressed += OnPointerPressed;
            PointerMoved += OnPointerMoved;
            PointerReleased += OnPointerReleased;
            PointerCaptureLost += OnPointerCaptureLost;

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

        public int TargetFrameRate
        {
            get => GetValue(TargetFrameRateProperty);
            set => SetValue(TargetFrameRateProperty, value);
        }

        public double FixedTimeStep
        {
            get => GetValue(FixedTimeStepProperty);
            set => SetValue(FixedTimeStepProperty, value);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (!_ownsEngine || _gameEngine is not { } engine) return;
            if (change.Property == TargetFrameRateProperty)
            {
                int rate = TargetFrameRate;
                engine.Post(target => target.TargetFrameRate = rate);
            }
            else if (change.Property == FixedTimeStepProperty)
            {
                double step = FixedTimeStep;
                engine.Post(target => target.FixedTimeStep = step);
            }
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
            _attachedLifecycle = Lifecycle ?? App.Lifecycle;
            _attachedLifecycle.Changed += OnHostStateChanged;
            engine.SetHostAttached(true);
            engine.SetHostState(_attachedLifecycle.State);
            _topLevel = TopLevel.GetTopLevel(this);
            if (_topLevel != null) _topLevel.BackRequested += OnBackRequested;

            if (_ownsEngine)
            {
                engine.TargetFrameRate = TargetFrameRate;
                engine.FixedTimeStep = FixedTimeStep;
                engine.Start();
            }
        }

        // Detachment ends the view's use of its engine. One it constructed is stopped and
        // disposed here, because nothing else will; one it was handed keeps running for its
        // owner and only has this view's paint callback taken back off it. Reattaching builds
        // a fresh owned engine, or re-attaches to the supplied one.
        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            ReleaseHeldKeys();
            CancelContacts();
            if (_attachedLifecycle != null) _attachedLifecycle.Changed -= OnHostStateChanged;
            if (_topLevel != null) _topLevel.BackRequested -= OnBackRequested;
            _attachedLifecycle = null;
            _topLevel = null;
            _gameEngine?.SetHostAttached(false);

            _syntheticKeyReleases.Cancel();
            _syntheticKeyReleases.Dispose();
            _syntheticKeyReleases = new CancellationTokenSource();

            ReleaseEngine();

            base.OnDetachedFromVisualTree(e);
        }

        private Engine EnsureEngine()
        {
            if (_gameEngine != null)
                return _gameEngine;

            var supplied = Engine;
            if (supplied != null)
            {
                _previousInvalidateAction = supplied.InvalidateAction;
                supplied.InvalidateAction = _queueInvalidate;
                _gameEngine = supplied;
                _ownsEngine = false;
            }
            else
            {
                var assetSource = AssetSource ?? App.AssetSource ?? new FileAssetSource();
                _gameEngine = new Engine(_queueInvalidate, AudioEnabled, assetSource, App.HostServicesFactory?.Invoke() ?? new HostServices());
                _ownsEngine = true;

                var scene = Scene ?? SceneFactory?.Invoke() ?? App.StartupScene?.Invoke();
                if (scene != null)
                    _gameEngine.ChangeScene(scene);
            }

            Current = _gameEngine;
            return _gameEngine;
        }

        private void ReleaseEngine()
        {
            var engine = _gameEngine;
            if (engine == null)
                return;

            _gameEngine = null;
            _started = false;

            if (ReferenceEquals(Current, engine))
                Current = null;

            if (_ownsEngine)
            {
                engine.Stop();
                engine.Dispose();
            }
            else if (ReferenceEquals(engine.InvalidateAction, _queueInvalidate))
            {
                engine.InvalidateAction = _previousInvalidateAction;
            }

            _ownsEngine = false;
            _previousInvalidateAction = null;
        }

        private void ReleaseHeldKeys()
        {
            var input = Input;

            foreach (var key in _heldKeys)
                input?.KeyUp(key);

            _heldKeys.Clear();
            foreach (var key in _syntheticKeys) input?.KeyUp(key.Value, key.Key);
            _syntheticKeys.Clear();
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

        private async Task ReleaseKeyAfterTapAsync(GeKeys key, long sourceId, CancellationToken cancellation)
        {
            try
            {
                await Task.Delay(SyntheticKeyHoldMs, cancellation);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (_syntheticKeys.Remove(sourceId)) Input?.KeyUp(key, sourceId);
        }

        // TryGet rather than Get: the container is empty between an engine's disposal and the
        // last queued UI event that still refers to it.
        private InputSystem? Input => _gameEngine?.Systems.TryGet<InputSystem>();

        private void OnSizeChanged(object? sender, EventArgs args)
        {
            var size = App.InputViewportSize?.Invoke() ?? new Vec2(Bounds.Width, Bounds.Height);
            _gameEngine?.SizeChanged((int)size.X, (int)size.Y);
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

        private void OnHostStateChanged(HostState state)
        {
            if (state != HostState.Active)
            {
                ReleaseHeldKeys();
                CancelContacts();
            }
            _gameEngine?.SetHostState(state);
        }

        private void OnBackRequested(object? sender, RoutedEventArgs e)
        {
            e.Handled = _gameEngine?.RequestBack() == true;
        }

        private void OnLostFocus(object? sender, RoutedEventArgs e)
        {
            ReleaseHeldKeys();
            CancelContacts();
        }

        private void CancelContacts()
        {
            var contacts = new List<(IPointer Pointer, Point Start, Point Last)>(_contacts.Values);
            _contacts.Clear();
            foreach (var contact in contacts)
            {
                Input?.PointerCancelled(new PointerCancelEvent(new Vec2(contact.Last.X, contact.Last.Y), contact.Pointer.Id, true));
                contact.Pointer.Capture(null);
            }
        }

        private void OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
        {
            if (_contacts.Remove(e.Pointer.Id, out var contact))
                Input?.PointerCancelled(new PointerCancelEvent(new Vec2(contact.Last.X, contact.Last.Y), e.Pointer.Id, true));
        }

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (_gameEngine?.HostState != HostState.Active) return;
            Focus();
            var point = e.GetPosition(this);
            _contacts[e.Pointer.Id] = (e.Pointer, point, point);
            e.Pointer.Capture(this);
            Input?.PointerPressed(new PointerPressEvent(new Vec2(point.X, point.Y), e.Pointer.Id, true));
        }

        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            var point = e.GetPosition(this);
            bool captured = _contacts.TryGetValue(e.Pointer.Id, out var contact);
            if (captured) _contacts[e.Pointer.Id] = (e.Pointer, contact.Start, point);
            Input?.PointerMoved(new PointerMoveEvent(new Vec2(point.X, point.Y), e.Pointer.Id, captured));
        }

        private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            var point = e.GetPosition(this);
            bool captured = _contacts.Remove(e.Pointer.Id, out var contact);
            Input?.PointerReleased(new PointerReleaseEvent(new Vec2(point.X, point.Y), e.Pointer.Id, captured));
            e.Pointer.Capture(null);
            if (captured && _gameEngine?.InputManager.ReleaseGesturesEnabled == true)
            {
                var key = Core.SwipeGesture.Classify(new Vec2(contact.Start.X, contact.Start.Y), new Vec2(point.X, point.Y), SwipeThreshold);
                long sourceId = ++_nextSyntheticSource;
                _syntheticKeys[sourceId] = key;
                Input?.KeyDown(key, sourceId);
                _ = ReleaseKeyAfterTapAsync(key, sourceId, _syntheticKeyReleases.Token);
            }
        }

        public override void Render(DrawingContext context)
        {
            var engine = _gameEngine;
            if (engine == null || engine.IsDisposed)
                return;

            context.Custom(new CustomDrawOp(
                new Rect(0, 0, Bounds.Width, Bounds.Height),
                engine,
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
            // The engine can be disposed between this operation being queued and the render
            // thread reaching it, which empties its container.
            var renderSystem = _engine.Systems.TryGet<RenderSystem>();
            if (renderSystem == null)
                return;

            using var lease = leaseFeature.Lease();
            var canvas = lease.SkCanvas;
            renderSystem.DrawEntitiesToCanvas(canvas, _engine.GetRenderSnapshot());

            // Resume updates and report that the first visible frame reached the platform surface.
            _reportFirstPresent();
        }
    }
}
