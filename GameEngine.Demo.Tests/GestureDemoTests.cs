using GameEngine.Core;

namespace GameEngine.Demo.Tests;

public class GestureDemoTests
{
    [TestCase(typeof(SceneAstralRelay), "Astral")]
    [TestCase(typeof(SceneVoidSalvage), "Salvage")]
    [TestCase(typeof(SceneVoidSiege), "Siege")]
    public void KeyboardArcadeGamesRetainExplicitSwipeAndTapControls(Type type, string prefix)
    {
        var harness = SceneHarness.Load(SceneHarness.Instantiate(type));
        using var engine = harness.Engine;
        harness.PointerGesture(new Vec2(300, 300), new Vec2(400, 300));
        Assert.That(harness.Input.IsActionActive(prefix + "Right"), Is.True);
        harness.Run(10);
        harness.PointerGesture(new Vec2(300, 300), new Vec2(300, 300));
        Assert.That(harness.Input.IsActionActive(prefix + "Dash"), Is.True);
    }
}
