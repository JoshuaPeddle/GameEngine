using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using GameEngine.Core;
using GameEngine.Core.Systems;

namespace GameEngine.Runner.Avalonia.Tests;

public class GameViewLifetimeTests
{
    private sealed class CountingScene : Scene
    {
        public int Initializations;

        public override void Initialize(
            EntityManager entityManager,
            InputManager inputManager,
            AudioSystem? audioSystem,
            Action<Scene?> resetScene)
        {
            Initializations++;
        }
    }

    private static Window WindowWith(GameView view)
    {
        var window = new Window { Width = 200, Height = 200, Content = view };
        window.Show();
        return window;
    }

    private static GameView OwnedView() =>
        new() { AudioEnabled = false, AssetSource = new DelegateAssetSource(_ => Stream.Null), SceneFactory = () => new CountingScene() };

    [AvaloniaTest]
    public void AHostCanUseCssInputDimensionsOnAHighDpiSurface()
    {
        var previous = App.InputViewportSize;
        var view = OwnedView();
        Window? window = null;
        try
        {
            App.InputViewportSize = () => new Vec2(1280, 800);
            window = WindowWith(view);
            view.Measure(new global::Avalonia.Size(2560, 1600));
            view.Arrange(new global::Avalonia.Rect(0, 0, 2560, 1600));
            var engine = GameView.Current!;
            Assert.That(engine.InputManager.RealResolution, Is.EqualTo(new Vec2(1280, 800)));
            engine.InputManager.VirtualResolution = new Vec2(1280, 800);
            Vec2? received = null;
            engine.InputManager.BindPointerAction(GameEngine.Core.Pointer.PointerEventType.Press, e => received = e.Position);
            engine.InputManager.HandlePointerEvent(GameEngine.Core.Pointer.PointerEventType.Press, new GameEngine.Core.Pointer.PointerPressEvent(new Vec2(380, 412)));
            engine.InputManager.DispatchPointerEvents();
            Assert.That(received, Is.EqualTo(new Vec2(380, 412)));
        }
        finally
        {
            if (window != null) { window.Content = null; window.Close(); }
            App.InputViewportSize = previous;
        }
    }

    [AvaloniaTest]
    public void AnOwnedEngine_IsStoppedAndDisposedWhenTheViewLeavesTheTree()
    {
        var view = OwnedView();
        var window = WindowWith(view);

        var engine = GameView.Current!;
        window.Content = null;

        Assert.Multiple(() =>
        {
            Assert.That(engine.IsStopped, Is.True);
            Assert.That(engine.IsDisposed, Is.True);
            Assert.That(GameView.Current, Is.Null);
        });
    }

    [AvaloniaTest]
    public void AnOwnedEngine_IsReplacedWhenTheViewReattaches()
    {
        var view = OwnedView();
        var window = WindowWith(view);
        var first = GameView.Current!;

        window.Content = null;
        window.Content = view;
        var second = GameView.Current!;

        Assert.Multiple(() =>
        {
            Assert.That(second, Is.Not.SameAs(first));
            Assert.That(second.IsDisposed, Is.False);
            Assert.That(first.IsDisposed, Is.True);
        });
    }

    [AvaloniaTest]
    public void ASuppliedEngine_SurvivesDetachmentAndKeepsItsOwnersCallback()
    {
        var invalidations = 0;
        using var engine = new Engine(() => invalidations++, audioEnabled: false, new DelegateAssetSource(_ => Stream.Null));
        var owner = engine.InvalidateAction;

        var view = new GameView { Engine = engine };
        var window = WindowWith(view);
        Assert.That((object?)engine.InvalidateAction, Is.Not.SameAs(owner));

        window.Content = null;

        Assert.Multiple(() =>
        {
            Assert.That(engine.IsStopped, Is.False);
            Assert.That(engine.IsDisposed, Is.False);
            Assert.That((object?)engine.InvalidateAction, Is.SameAs(owner));
        });
    }

    [AvaloniaTest]
    public void ASuppliedEngine_IsUsableAfterTheViewReattaches()
    {
        using var engine = new Engine(null, audioEnabled: false, new DelegateAssetSource(_ => Stream.Null));
        var scene = new CountingScene();
        engine.ChangeScene(scene);

        var view = new GameView { Engine = engine };
        var window = WindowWith(view);
        window.Content = null;
        window.Content = view;

        engine.Tick(0.016);

        Assert.Multiple(() =>
        {
            Assert.That(scene.Initializations, Is.EqualTo(1));
            Assert.That(engine.Systems.TryGet<InputSystem>(), Is.Not.Null);
        });
    }

    [AvaloniaTest]
    public void TwoViewsRunSideBySideAndReleaseOnlyTheirOwnEngine()
    {
        var first = OwnedView();
        var second = OwnedView();
        var window = new Window { Width = 400, Height = 200 };
        var panel = new StackPanel();
        window.Content = panel;
        panel.Children.Add(first);
        window.Show();
        var firstEngine = GameView.Current!;

        panel.Children.Add(second);
        var secondEngine = GameView.Current!;
        Assert.That(secondEngine, Is.Not.SameAs(firstEngine));

        panel.Children.Remove(first);

        Assert.Multiple(() =>
        {
            Assert.That(firstEngine.IsDisposed, Is.True);
            Assert.That(secondEngine.IsDisposed, Is.False);
            Assert.That(GameView.Current, Is.SameAs(secondEngine));
        });

        panel.Children.Remove(second);
        Assert.That(secondEngine.IsDisposed, Is.True);
    }

    [AvaloniaTest]
    public void HeldKeysAreReleasedWhenTheViewLeavesTheTree()
    {
        using var engine = new Engine(null, audioEnabled: false, new DelegateAssetSource(_ => Stream.Null));
        engine.InputManager.AddAction(GeKeys.D, "Right");

        var view = new GameView { Engine = engine };
        var window = WindowWith(view);
        view.Focus();
        window.KeyPress(Key.D, RawInputModifiers.None, PhysicalKey.D, "d");
        Assert.That(engine.InputManager.IsActionActive("Right"), Is.True);

        window.Content = null;

        Assert.That(engine.InputManager.IsActionActive("Right"), Is.False);
    }

    [AvaloniaTest]
    public void RenderingAfterDetachmentDoesNothing()
    {
        var view = OwnedView();
        var window = WindowWith(view);
        var engine = GameView.Current!;

        window.Content = null;

        Assert.Multiple(() =>
        {
            Assert.DoesNotThrow(() => view.InvalidateVisual());
            Assert.That(engine.Systems.TryGet<RenderSystem>(), Is.Null);
        });
    }
    [AvaloniaTest]
    public void HostLifecycleReachesAnAttachedEngineAndUnsubscribesOnDetach()
    {
        using var engine = new Engine(audioEnabled: false);
        var lifecycle = new HostLifecycle();
        var view = new GameView { Engine = engine, Lifecycle = lifecycle };
        var window = WindowWith(view);
        try
        {
            lifecycle.SetState(HostState.Background);
            Assert.That(engine.Tick(0), Is.False);
            lifecycle.SetState(HostState.Active);
            Assert.That(engine.Tick(0), Is.True);
            window.Content = null;
            lifecycle.SetState(HostState.Background);
            Assert.That(engine.HostState, Is.EqualTo(HostState.Active));
        }
        finally { window.Content = null; window.Close(); }
    }

    [AvaloniaTest]
    public void ReleaseGesturesAreOptInAndDetachReleasesSyntheticKeys()
    {
        using var engine = new Engine(audioEnabled: false);
        engine.InputManager.AddAction(GeKeys.Space, "tap");
        var view = new GameView { Engine = engine };
        var window = WindowWith(view);
        try
        {
            var point = new global::Avalonia.Point(50, 50);
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Assert.That(engine.InputManager.IsActionActive("tap"), Is.False);
            engine.InputManager.ReleaseGesturesEnabled = true;
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Assert.That(engine.InputManager.IsActionActive("tap"), Is.True);
            window.Content = null;
            Assert.That(engine.InputManager.IsActionActive("tap"), Is.False);
        }
        finally { window.Content = null; window.Close(); }
    }

    [AvaloniaTest]
    public void CaptureLossCancelsAContactAndHoverIsDeliveredWithoutAPress()
    {
        using var engine = new Engine(audioEnabled: false);
        engine.InputManager.VirtualResolution = new Vec2(200, 200);
        var view = new GameView { Engine = engine };
        var window = WindowWith(view);
        try
        {
            int moves = 0, cancels = 0;
            engine.InputManager.BindPointerAction(GameEngine.Core.Pointer.PointerEventType.Move, _ => moves++);
            engine.InputManager.BindPointerAction(GameEngine.Core.Pointer.PointerEventType.Cancel, _ => cancels++);
            window.MouseMove(new global::Avalonia.Point(50, 50));
            window.MouseDown(new global::Avalonia.Point(50, 50), MouseButton.Left);
            engine.InputManager.DispatchPointerEvents();
            Assert.That(moves, Is.GreaterThan(0));
            IPointer? pointer = null;
            view.PointerPressed += (_, e) => pointer = e.Pointer;
            window.MouseUp(new global::Avalonia.Point(50, 50), MouseButton.Left);
            window.MouseDown(new global::Avalonia.Point(50, 50), MouseButton.Left);
            engine.InputManager.DispatchPointerEvents();
            pointer!.Capture(window);
            engine.InputManager.DispatchPointerEvents();
            Assert.That(cancels, Is.EqualTo(1));
            Assert.That(engine.InputManager.Contacts, Is.Empty);
        }
        finally { window.Content = null; window.Close(); }
    }

    [AvaloniaTest]
    public void ThreeTouchContactsKeepTheirOwnCaptureAndGestureOrigins()
    {
        using var engine = new Engine(audioEnabled: false);
        engine.InputManager.VirtualResolution = new Vec2(200, 200);
        engine.InputManager.ReleaseGesturesEnabled = true;
        engine.InputManager.AddAction(GeKeys.D, "right");
        engine.InputManager.AddAction(GeKeys.A, "left");
        var view = new GameView { Engine = engine };
        var window = WindowWith(view);
        using var first = new global::Avalonia.Input.Pointer(100, PointerType.Touch, true);
        using var second = new global::Avalonia.Input.Pointer(101, PointerType.Touch, false);
        using var third = new global::Avalonia.Input.Pointer(102, PointerType.Touch, false);
        try
        {
            void Press(IPointer pointer, double x) => view.RaiseEvent(new PointerPressedEventArgs(
                view, pointer, window, new global::Avalonia.Point(x, 50), 0, new PointerPointProperties(), KeyModifiers.None));
            void Release(IPointer pointer, double x) => view.RaiseEvent(new PointerReleasedEventArgs(
                view, pointer, window, new global::Avalonia.Point(x, 50), 0, new PointerPointProperties(), KeyModifiers.None, MouseButton.Left));
            Press(first, 20);
            Press(second, 180);
            Press(third, 100);
            engine.InputManager.DispatchPointerEvents();
            Assert.That(engine.InputManager.Contacts.Count, Is.EqualTo(3));
            Assert.That(first.Captured, Is.SameAs(view));
            Assert.That(second.Captured, Is.SameAs(view));
            Release(first, 180);
            Assert.That(engine.InputManager.IsActionActive("right"), Is.True);
            Assert.That(engine.InputManager.IsActionActive("left"), Is.False);
            Release(second, 20);
            Assert.That(engine.InputManager.IsActionActive("left"), Is.True);
            third.Capture(window);
            engine.InputManager.DispatchPointerEvents();
            Assert.That(engine.InputManager.Contacts, Is.Empty);
        }
        finally { window.Content = null; window.Close(); }
    }

}
