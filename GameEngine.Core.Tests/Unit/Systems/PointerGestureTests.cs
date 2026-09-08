using GameEngine.Core.Systems;
using static GameEngine.Core.Pointer;

namespace GameEngine.Core.Tests.Unit.Systems;

public class PointerGestureTests
{
    private static InputManager Input() => new()
    {
        RealResolution = new Vec2(800, 600),
        VirtualResolution = new Vec2(800, 600)
    };

    private static void Drag(InputManager input, Vec2 start, Vec2 end)
    {
        input.HandlePointerEvent(PointerEventType.Press, new PointerPressEvent(start));
        input.HandlePointerEvent(PointerEventType.Move, new PointerMoveEvent(end));
        input.HandlePointerEvent(PointerEventType.Release, new PointerReleaseEvent(end));
        input.DispatchPointerEvents();
    }

    [Test]
    public void RawClicksAndHoverNeverSynthesizeKeyboardActions()
    {
        var input = Input();
        input.AddAction(GeKeys.Space, "Jump");
        var events = new List<PointerEventType>();
        foreach (var type in Enum.GetValues<PointerEventType>())
            input.BindPointerAction(type, _ => events.Add(type));
        input.HandlePointerEvent(PointerEventType.Move, new PointerMoveEvent(new Vec2(50, 50)));
        Drag(input, new Vec2(50, 50), new Vec2(50, 50));
        Assert.That(events, Is.EqualTo(new[] { PointerEventType.Move, PointerEventType.Press, PointerEventType.Move, PointerEventType.Release }));
        Assert.That(input.IsActionActive("Jump"), Is.False);
    }

    [TestCase(100, 50, PointerGesture.Right)]
    [TestCase(0, 50, PointerGesture.Left)]
    [TestCase(50, 0, PointerGesture.Up)]
    [TestCase(50, 100, PointerGesture.Down)]
    [TestCase(50, 50, PointerGesture.Tap)]
    public void ExplicitGesturesRunForSimulationTimeWithoutAKeyboardBinding(int x, int y, PointerGesture gesture)
    {
        var input = Input();
        input.BindGestureAction(gesture, "Move", 0.1);
        var states = new List<bool>();
        input.BindAction("Move", states.Add);
        Drag(input, new Vec2(50, 50), new Vec2(x, y));
        input.DoActions(0.05);
        input.DoActions(0.05);
        input.DoActions(0.05);
        Assert.That(states, Is.EqualTo(new[] { true, true, false }));
    }

    [Test]
    public void ExpiringGestureDoesNotReleaseAPhysicalKey()
    {
        var input = Input();
        input.AddAction(GeKeys.D, "Right");
        input.BindGestureAction(PointerGesture.Right, "Right");
        input.HandleKeyPress(GeKeys.D);
        Drag(input, new Vec2(50, 50), new Vec2(100, 50));
        input.DoActions(1);
        Assert.That(input.IsActionActive("Right"), Is.True);
        input.HandleKeyRelease(GeKeys.D);
        Assert.That(input.IsActionActive("Right"), Is.False);
    }

    [Test]
    public void SceneResetDropsPendingGesturesBindingsAndStartPosition()
    {
        var input = Input();
        input.BindGestureAction(PointerGesture.Tap, "Jump");
        Drag(input, new Vec2(50, 50), new Vec2(50, 50));
        input.Reset();
        input.BindGestureAction(PointerGesture.Tap, "Jump");
        input.HandlePointerEvent(PointerEventType.Release, new PointerReleaseEvent(new Vec2(50, 50)));
        input.DispatchPointerEvents();
        Assert.That(input.IsActionActive("Jump"), Is.False);
    }

    [Test]
    public void GesturesUseVirtualDistancesAndIgnoreLetterboxStarts()
    {
        var input = Input();
        input.RealResolution = new Vec2(1600, 1600);
        input.BindGestureAction(PointerGesture.Tap, "Tap");
        Drag(input, new Vec2(100, 100), new Vec2(100, 250));
        Assert.That(input.IsActionActive("Tap"), Is.False);
        Drag(input, new Vec2(100, 250), new Vec2(130, 250));
        Assert.That(input.IsActionActive("Tap"), Is.True);
    }

    [Test]
    public void RemovingAnActionRemovesItsGestureBindingAndActivePulse()
    {
        var input = Input();
        input.BindGestureAction(PointerGesture.Tap, "Tap");
        Drag(input, new Vec2(50, 50), new Vec2(50, 50));
        input.RemoveAction("Tap");
        Drag(input, new Vec2(50, 50), new Vec2(50, 50));
        Assert.That(input.IsActionActive("Tap"), Is.False);
    }
}
