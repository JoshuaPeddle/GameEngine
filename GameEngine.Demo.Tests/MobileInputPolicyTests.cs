using GameEngine.Core;

namespace GameEngine.Demo.Tests;

public class MobileInputPolicyTests
{
    [TestCase(typeof(SceneMenu), true)]
    [TestCase(typeof(SceneTetris), true)]
    [TestCase(typeof(SceneVoidSiege), true)]
    [TestCase(typeof(SceneVoidSalvage), true)]
    [TestCase(typeof(SceneEmberbrook), false)]
    [TestCase(typeof(SceneBrickBreaker), false)]
    public void DemosDeclareTheirReleaseGesturePolicy(Type sceneType, bool enabled)
    {
        var harness = SceneHarness.Load(SceneHarness.Instantiate(sceneType));
        try
        {
            Assert.That(harness.Engine.Fault, Is.Null);
            Assert.That(harness.Input.ReleaseGesturesEnabled, Is.EqualTo(enabled));
        }
        finally { harness.Engine.Dispose(); }
    }
}
