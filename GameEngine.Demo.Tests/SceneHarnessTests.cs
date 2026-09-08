using GameEngine.Core;
using GameEngine.Core.Systems;

namespace GameEngine.Demo.Tests;

public class SceneHarnessTests
{
    private sealed class FaultScene(bool initialize = false) : Scene
    {
        public bool Armed;
        public override void Initialize(EntityManager entities, InputManager input, AudioSystem? audio, Action<Scene?> reset)
        {
            if (initialize) throw new InvalidOperationException("initialization failed");
        }
        public override void Update(EntityManager entities, SystemContainer systems, double seconds)
        {
            if (Armed) throw new InvalidOperationException("update failed");
        }
    }

    [Test]
    public void LoadingRejectsCapturedInitializationFaults()
    {
        var failure = Assert.Throws<AssertionException>(() => SceneHarness.Load(new FaultScene(true)));
        Assert.That(failure!.Message, Does.Contain("initialization failed"));
    }

    [Test]
    public void RunningRejectsCapturedUpdateFaults()
    {
        var scene = new FaultScene();
        var harness = SceneHarness.Load(scene);
        using var engine = harness.Engine;
        scene.Armed = true;
        var failure = Assert.Throws<AssertionException>(() => harness.Run(1));
        Assert.That(failure!.Message, Does.Contain("update failed"));
    }

    [Test]
    public void ExpectedFaultsCanBeInspectedAndRecoveredFromExplicitly()
    {
        var scene = new FaultScene();
        var harness = SceneHarness.Load(scene);
        using var engine = harness.Engine;
        scene.Armed = true;
        harness.ExpectFault(1, "update failed");
        engine.ChangeScene(new FaultScene());
        harness.Run(1);
        Assert.That(engine.Fault, Is.Null);
    }
}
