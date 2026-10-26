using static GameEngine.Core.Pointer;

namespace GameEngine.Core.Tests.Unit.Systems;

public class MultiTouchTests
{
    private static InputManager Input() => new()
    {
        RealResolution = new Vec2(800, 600), VirtualResolution = new Vec2(100, 100)
    };

    [TestCase(false)]
    [TestCase(true)]
    public void CrossingContactsLiftInEitherOrderWithoutOverwritingEachOther(bool reverse)
    {
        var input = Input();
        var events = new List<(PointerEventType, long, Vec2, bool)>();
        foreach (var type in Enum.GetValues<PointerEventType>())
            input.BindPointerAction(type, e => events.Add((type, e.ContactId, e.Position, e.IsCaptured)));
        input.HandlePointerEvent(PointerEventType.Press, new PointerPressEvent(new Vec2(200, 200), 17, true));
        input.HandlePointerEvent(PointerEventType.Press, new PointerPressEvent(new Vec2(600, 400), 18, true));
        input.HandlePointerEvent(PointerEventType.Move, new PointerMoveEvent(new Vec2(600, 400), 17, true));
        input.HandlePointerEvent(PointerEventType.Move, new PointerMoveEvent(new Vec2(200, 200), 18, true));
        input.DispatchPointerEvents();
        Assert.That(input.Contacts[17].Position, Is.EqualTo(new Vec2(83.33333333333333, 66.66666666666667)));
        long first = reverse ? 18 : 17;
        long last = reverse ? 17 : 18;
        input.HandlePointerEvent(PointerEventType.Release, new PointerReleaseEvent(new Vec2(800, 200), first, true));
        input.DispatchPointerEvents();
        Assert.That(input.Contacts.Keys, Is.EqualTo(new[] { last }));
        input.HandlePointerEvent(PointerEventType.Cancel, new PointerCancelEvent(new Vec2(0, 200), last, true));
        input.DispatchPointerEvents();
        Assert.That(input.Contacts, Is.Empty);
        Assert.That(events.Select(e => e.Item1), Is.EqualTo(new[] { PointerEventType.Press, PointerEventType.Press, PointerEventType.Move, PointerEventType.Move, PointerEventType.Release, PointerEventType.Cancel }));
        Assert.That(events.All(e => e.Item4), Is.True);
        input.HandlePointerEvent(PointerEventType.Press, new PointerPressEvent(new Vec2(300, 300), 17));
        input.DispatchPointerEvents();
        Assert.That(input.Contacts.Keys, Is.EqualTo(new long[] { 17 }));
    }

    [Test]
    public void MoveFloodCannotDiscardPressReleaseOrCancelEvenOutsideTheViewport()
    {
        var input = Input();
        var terminal = new List<long>();
        input.BindPointerAction(PointerEventType.Release, e => terminal.Add(e.ContactId));
        input.BindPointerAction(PointerEventType.Cancel, e => terminal.Add(e.ContactId));
        input.HandlePointerEvent(PointerEventType.Press, new PointerEvent(new Vec2(300, 300), 1, true));
        for (int i = 0; i < 10_000; i++)
            input.HandlePointerEvent(PointerEventType.Move, new PointerEvent(new Vec2(400, 400), 1, true));
        input.HandlePointerEvent(PointerEventType.Press, new PointerEvent(new Vec2(300, 300), 2, true));
        input.HandlePointerEvent(PointerEventType.Release, new PointerEvent(new Vec2(-100, 100), 1, true));
        input.HandlePointerEvent(PointerEventType.Cancel, new PointerEvent(new Vec2(900, 100), 2, true));
        input.DispatchPointerEvents();
        Assert.That(terminal, Is.EqualTo(new long[] { 1, 2 }));
        Assert.That(input.Contacts, Is.Empty);
    }

    [Test]
    public void TerminalEventsSurviveAnInvalidatedViewport()
    {
        var input = Input();
        int cancellations = 0;
        input.BindPointerAction(PointerEventType.Cancel, _ => cancellations++);
        input.HandlePointerEvent(PointerEventType.Press, new PointerEvent(new Vec2(300, 300)));
        input.DispatchPointerEvents();
        input.RealResolution = Vec2.Zero;
        input.CancelAllContacts();
        input.DispatchPointerEvents();
        Assert.That(cancellations, Is.EqualTo(1));
        Assert.That(input.Contacts, Is.Empty);
    }

    [Test]
    public void SinglePointerDefaultsToContactZeroAndGestureOptInResetsWithTheScene()
    {
        var input = Input();
        Assert.That(input.ReleaseGesturesEnabled, Is.False);
        input.HandlePointerEvent(PointerEventType.Press, new PointerPressEvent(new Vec2(300, 300)));
        input.DispatchPointerEvents();
        Assert.That(input.Contacts.Keys, Is.EqualTo(new long[] { 0 }));
        input.ReleaseGesturesEnabled = true;
        input.Reset();
        Assert.That(input.ReleaseGesturesEnabled, Is.False);
        Assert.That(input.Contacts, Is.Empty);
    }
    [Test]
    public void PhysicalAndSyntheticSourcesReleaseIndependently()
    {
        var input = Input();
        input.AddAction(GeKeys.Space, "tap");
        input.HandleKeyPress(GeKeys.Space);
        input.HandleKeyPress(GeKeys.Space, 1);
        input.HandleKeyRelease(GeKeys.Space, 1);
        Assert.That(input.IsActionActive("tap"), Is.True);
        input.HandleKeyPress(GeKeys.Space, 2);
        input.HandleKeyRelease(GeKeys.Space);
        Assert.That(input.IsActionActive("tap"), Is.True);
        input.HandleKeyRelease(GeKeys.Space, 2);
        Assert.That(input.IsActionActive("tap"), Is.False);
    }

}
