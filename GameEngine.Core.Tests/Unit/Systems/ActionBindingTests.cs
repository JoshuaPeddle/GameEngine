using GameEngine.Core.Components;

namespace GameEngine.Core.Tests.Unit.Systems;

public class ActionBindingTests
{
    [Test]
    public void BindAction_BeforeAddAction_StillFires()
    {
        var inputManager = new InputManager();
        bool fired = false;

        inputManager.BindAction("Jump", isActive => fired |= isActive);
        inputManager.AddAction(GeKeys.Space, "Jump");
        inputManager.HandleKeyPress(GeKeys.Space);
        inputManager.DoActions();

        Assert.That(fired, Is.True, "binding order must not silently discard the binding");
    }

    [Test]
    public void BindAction_AfterAddAction_StillFires()
    {
        var inputManager = new InputManager();
        bool fired = false;

        inputManager.AddAction(GeKeys.Space, "Jump");
        inputManager.BindAction("Jump", isActive => fired |= isActive);
        inputManager.HandleKeyPress(GeKeys.Space);
        inputManager.DoActions();

        Assert.That(fired, Is.True);
    }

    [Test]
    public void BindAction_ForAnUnregisteredAction_ReportsInactive()
    {
        var inputManager = new InputManager();
        bool? observed = null;

        inputManager.BindAction("NeverRegistered", isActive => observed = isActive);
        inputManager.DoActions();

        Assert.That(observed, Is.False, "an action nothing maps to reads as inactive rather than throwing");
    }

    [Test]
    public void MultipleBindingsForOneAction_AllFire()
    {
        var inputManager = new InputManager();
        int invocations = 0;

        inputManager.AddAction(GeKeys.Space, "Jump");
        for (int i = 0; i < 4; i++)
            inputManager.BindAction("Jump", _ => invocations++);

        inputManager.DoActions();

        Assert.That(invocations, Is.EqualTo(4));
    }

    [Test]
    public void BindingAddedDuringDispatch_DoesNotDisturbTheCurrentPass()
    {
        var inputManager = new InputManager();
        inputManager.AddAction(GeKeys.Space, "Jump");

        int invocations = 0;
        inputManager.BindAction("Jump", _ =>
        {
            invocations++;
            inputManager.BindAction("Jump", _ => invocations++);
        });

        Assert.DoesNotThrow(() => inputManager.DoActions());
        Assert.That(invocations, Is.EqualTo(1));
    }

    [Test]
    public void ActionMapper_MapsThroughRegardlessOfOrder()
    {
        var inputManager = new InputManager();
        var entityManager = new EntityManager();
        var entity = entityManager.CreateEntity("player");
        var input = entity.AddComponent<CInput>();

        inputManager.ActionMapper.MapActionToComponent<CInput>("Up", entity, (c, active) => c.Up = active);
        inputManager.AddAction(GeKeys.W, "Up");

        inputManager.HandleKeyPress(GeKeys.W);
        inputManager.DoActions();

        Assert.That(input.Up, Is.True);
    }
}
