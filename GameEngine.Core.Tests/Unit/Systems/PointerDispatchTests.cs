using GameEngine.Core.Systems;
using static GameEngine.Core.Pointer;

namespace GameEngine.Core.Tests.Unit.Systems;

public class PointerDispatchTests
{
    private static InputManager SizedInputManager()
    {
        return new InputManager
        {
            RealResolution = new Vec2(800, 600),
            VirtualResolution = new Vec2(400, 300)
        };
    }

    [Test]
    public void HandlePointerEvent_DoesNotInvokeHandlersInline()
    {
        var inputManager = SizedInputManager();
        int invocations = 0;
        inputManager.BindPointerAction(PointerEventType.Press, _ => invocations++);

        inputManager.HandlePointerEvent(PointerEventType.Press, new PointerEvent(new Vec2(400, 300)));

        Assert.That(invocations, Is.Zero,
            "handlers must not run on the caller's thread; the runners call this from the UI thread");
    }

    [Test]
    public void DispatchPointerEvents_RunsQueuedHandlers()
    {
        var inputManager = SizedInputManager();
        int invocations = 0;
        inputManager.BindPointerAction(PointerEventType.Press, _ => invocations++);

        inputManager.HandlePointerEvent(PointerEventType.Press, new PointerEvent(new Vec2(400, 300)));
        inputManager.DispatchPointerEvents();

        Assert.That(invocations, Is.EqualTo(1));
    }

    [Test]
    public void DispatchPointerEvents_DrainsTheQueue()
    {
        var inputManager = SizedInputManager();
        int invocations = 0;
        inputManager.BindPointerAction(PointerEventType.Press, _ => invocations++);

        for (int i = 0; i < 3; i++)
            inputManager.HandlePointerEvent(PointerEventType.Press, new PointerEvent(new Vec2(400, 300)));

        inputManager.DispatchPointerEvents();
        inputManager.DispatchPointerEvents();

        Assert.That(invocations, Is.EqualTo(3), "each event is delivered exactly once");
    }

    [Test]
    public void DispatchPointerEvents_MapsRealCoordinatesToVirtual()
    {
        var inputManager = SizedInputManager();
        Vec2 received = default;
        inputManager.BindPointerAction(PointerEventType.Move, e => received = e.Position);

        inputManager.HandlePointerEvent(PointerEventType.Move, new PointerEvent(new Vec2(400, 300)));
        inputManager.DispatchPointerEvents();

        Assert.Multiple(() =>
        {
            Assert.That(received.X, Is.EqualTo(200).Within(0.001));
            Assert.That(received.Y, Is.EqualTo(150).Within(0.001));
        });
    }

    [Test]
    public void DispatchPointerEvents_DropsEventsOutsideTheLetterboxedArea()
    {
        var inputManager = new InputManager
        {
            RealResolution = new Vec2(800, 600),
            VirtualResolution = new Vec2(100, 100)
        };
        int invocations = 0;
        inputManager.BindPointerAction(PointerEventType.Press, _ => invocations++);

        inputManager.HandlePointerEvent(PointerEventType.Press, new PointerEvent(new Vec2(5, 300)));
        inputManager.DispatchPointerEvents();

        Assert.That(invocations, Is.Zero, "the left bar is outside the rendered area");
    }

    [Test]
    public void PointerEventsBeforeResolutionIsKnown_AreDroppedNotThrown()
    {
        var inputManager = new InputManager();
        int invocations = 0;
        inputManager.BindPointerAction(PointerEventType.Press, _ => invocations++);

        Assert.DoesNotThrow(() =>
        {
            inputManager.HandlePointerEvent(PointerEventType.Press, new PointerEvent(new Vec2(1, 1)));
            inputManager.DispatchPointerEvents();
        });
        Assert.That(invocations, Is.Zero);
    }

    [Test]
    public void QueuedPointerEvents_DoNotGrowWithoutBound()
    {
        var inputManager = SizedInputManager();
        int invocations = 0;
        inputManager.BindPointerAction(PointerEventType.Move, _ => invocations++);

        for (int i = 0; i < 10_000; i++)
            inputManager.HandlePointerEvent(PointerEventType.Move, new PointerEvent(new Vec2(400, 300)));

        inputManager.DispatchPointerEvents();

        Assert.That(invocations, Is.LessThan(10_000),
            "a paused engine must not let the queue grow unbounded");
        Assert.That(invocations, Is.GreaterThan(0));
    }

    [Test]
    public void InputSystemUpdate_DispatchesPointerEvents()
    {
        var inputManager = SizedInputManager();
        var inputSystem = new InputSystem(inputManager);
        var entityManager = new EntityManager();
        entityManager.Update();

        int invocations = 0;
        inputManager.BindPointerAction(PointerEventType.Press, _ => invocations++);

        inputSystem.PointerPressed(new PointerPressEvent(new Vec2(400, 300)));
        Assert.That(invocations, Is.Zero);

        inputSystem.Update(entityManager, 1.0 / 60.0);

        Assert.That(invocations, Is.EqualTo(1));
    }
}
