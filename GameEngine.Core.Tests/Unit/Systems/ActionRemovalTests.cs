namespace GameEngine.Core.Tests.Unit.Systems;

public class ActionRemovalTests
{
    private static InputManager SharedAction(out List<bool> observed)
    {
        var input = new InputManager();
        input.AddAction(GeKeys.D, "Right");
        input.AddAction(GeKeys.Right, "Right");

        var seen = new List<bool>();
        input.BindAction("Right", isActive => seen.Add(isActive));
        observed = seen;
        return input;
    }

    [Test]
    public void RemovingOneKey_LeavesTheOtherKeyDrivingTheSameAction()
    {
        var input = SharedAction(out var observed);

        input.RemoveAction(GeKeys.D);
        input.HandleKeyPress(GeKeys.Right);
        input.DoActions();

        Assert.Multiple(() =>
        {
            Assert.That(input.IsActionActive("Right"), Is.True);
            Assert.That(observed, Is.EqualTo(new[] { true }));
        });
    }

    [Test]
    public void RemovingOneKey_LeavesTheActionHeldWhenAnotherKeyIsStillDown()
    {
        var input = SharedAction(out _);
        input.HandleKeyPress(GeKeys.D);
        input.HandleKeyPress(GeKeys.Right);

        input.RemoveAction(GeKeys.D);

        Assert.That(input.IsActionActive("Right"), Is.True);
    }

    [Test]
    public void RemovingAHeldKey_ReleasesTheActionWhenItWasTheOnlyOneDown()
    {
        var input = SharedAction(out _);
        input.HandleKeyPress(GeKeys.D);

        input.RemoveAction(GeKeys.D);

        Assert.That(input.IsActionActive("Right"), Is.False);
    }

    [Test]
    public void RemovingTheLastKey_LeavesTheActionInactiveRatherThanHeld()
    {
        var input = SharedAction(out var observed);
        input.HandleKeyPress(GeKeys.D);
        input.HandleKeyPress(GeKeys.Right);

        input.RemoveAction(GeKeys.D);
        input.RemoveAction(GeKeys.Right);
        input.DoActions();

        Assert.Multiple(() =>
        {
            Assert.That(input.IsActionActive("Right"), Is.False);
            Assert.That(observed, Is.EqualTo(new[] { false }));
        });
    }

    [Test]
    public void ARemovedKey_NoLongerDrivesItsAction()
    {
        var input = SharedAction(out _);

        input.RemoveAction(GeKeys.D);
        input.HandleKeyPress(GeKeys.D);

        Assert.That(input.IsActionActive("Right"), Is.False);
    }

    [Test]
    public void RebindingAKeyAfterRemoval_KeepsTheExistingCallbacks()
    {
        var input = SharedAction(out var observed);
        input.RemoveAction(GeKeys.D);

        input.AddAction(GeKeys.D, "Right");
        input.HandleKeyPress(GeKeys.D);
        input.DoActions();

        Assert.Multiple(() =>
        {
            Assert.That(input.IsActionActive("Right"), Is.True);
            Assert.That(observed, Is.EqualTo(new[] { true }));
        });
    }

    [Test]
    public void MappingAnotherKeyToAHeldAction_DoesNotReleaseIt()
    {
        var input = new InputManager();
        input.AddAction(GeKeys.D, "Right");
        input.HandleKeyPress(GeKeys.D);

        input.AddAction(GeKeys.Right, "Right");

        Assert.That(input.IsActionActive("Right"), Is.True);
    }

    [Test]
    public void RemovingTheAction_RemovesEveryKeyAndEveryCallback()
    {
        var input = SharedAction(out var observed);
        input.HandleKeyPress(GeKeys.D);

        input.RemoveAction("Right");
        input.HandleKeyPress(GeKeys.Right);
        input.DoActions();

        Assert.Multiple(() =>
        {
            Assert.That(input.IsActionActive("Right"), Is.False);
            Assert.That(observed, Is.Empty);
        });
    }

    [Test]
    public void ResettingTheSceneClearsEveryMappingAndCallback()
    {
        var input = SharedAction(out var observed);
        input.HandleKeyPress(GeKeys.D);

        input.Reset();
        input.HandleKeyPress(GeKeys.Right);
        input.DoActions();

        Assert.Multiple(() =>
        {
            Assert.That(input.IsActionActive("Right"), Is.False);
            Assert.That(observed, Is.Empty);
        });
    }
}
