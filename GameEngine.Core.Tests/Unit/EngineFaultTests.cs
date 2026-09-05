using GameEngine.Core.Components;
using GameEngine.Core.Systems;

namespace GameEngine.Core.Tests.Unit;

public class EngineFaultTests
{
    private sealed class ThrowingSystem : ISystem
    {
        public bool Armed = true;
        public int Updates;

        public void Update(EntityManager entityManager, double deltaSeconds)
        {
            Updates++;
            if (Armed)
                throw new InvalidOperationException("system exploded");
        }
    }

    private sealed class WorkingScene : Scene
    {
        public override void Initialize(EntityManager entityManager, InputManager inputManager,
            AudioSystem? audioPlayer, Action<Scene?> resetScene)
        {
            var entity = entityManager.CreateEntity("survivor");
            entity.AddComponent(new CTransform(Vec2.Zero));
        }
    }

    private sealed class ThrowingInitializeScene : Scene
    {
        public override void Initialize(EntityManager entityManager, InputManager inputManager,
            AudioSystem? audioPlayer, Action<Scene?> resetScene)
        {
            throw new InvalidOperationException("scene could not load");
        }
    }

    private sealed class ThrowingUpdateScene : Scene
    {
        public override void Initialize(EntityManager entityManager, InputManager inputManager,
            AudioSystem? audioPlayer, Action<Scene?> resetScene)
        {
        }

        public override void Update(EntityManager entityManager, SystemContainer systems, double deltaSeconds)
        {
            throw new InvalidOperationException("scene update exploded");
        }
    }

    private static Engine Running(Scene scene)
    {
        var engine = new Engine(audioEnabled: false);
        engine.ChangeScene(scene);
        engine.Tick(0);
        engine.NotifyFirstPresent();
        return engine;
    }

    [Test]
    public void AFailingSceneInitializationIsReportedAndDoesNotThrowAtTheHost()
    {
        using var engine = new Engine(audioEnabled: false);
        EngineFault? reported = null;
        engine.FaultAction = fault => reported = fault;

        engine.ChangeScene(new ThrowingInitializeScene());
        Assert.DoesNotThrow(() => engine.Tick(0));

        Assert.Multiple(() =>
        {
            Assert.That(engine.IsFaulted, Is.True);
            Assert.That(reported, Is.Not.Null);
            Assert.That(reported!.Operation, Is.EqualTo("scene initialization"));
            Assert.That(reported.SceneName, Is.EqualTo(nameof(ThrowingInitializeScene)));
            Assert.That(reported.Exception.Message, Is.EqualTo("scene could not load"));
        });
    }

    [Test]
    public void AFailingSceneUpdateNamesTheSceneAndTheOperation()
    {
        using var engine = Running(new ThrowingUpdateScene());

        engine.Tick(0.016);

        Assert.Multiple(() =>
        {
            Assert.That(engine.Fault, Is.Not.Null);
            Assert.That(engine.Fault!.Operation, Is.EqualTo("scene update"));
            Assert.That(engine.Fault.SceneName, Is.EqualTo(nameof(ThrowingUpdateScene)));
            Assert.That(engine.Fault.SystemName, Is.Null);
        });
    }

    [Test]
    public void AFailingSystemNamesTheSystem()
    {
        using var engine = Running(new WorkingScene());
        engine.Systems.Add(new ThrowingSystem());

        engine.Tick(0.016);

        Assert.Multiple(() =>
        {
            Assert.That(engine.Fault!.SystemName, Is.EqualTo(nameof(ThrowingSystem)));
            Assert.That(engine.Fault.Operation, Is.EqualTo("system update"));
            Assert.That(engine.Fault.ToString(), Does.Contain("system exploded"));
        });
    }

    [Test]
    public void AFaultedEngineStopsSimulatingRatherThanThrowingEveryFrame()
    {
        using var engine = Running(new WorkingScene());
        var thrower = new ThrowingSystem();
        engine.Systems.Add(thrower);

        engine.Tick(0.016);
        int updatesWhenFaulted = thrower.Updates;

        for (int frame = 0; frame < 10; frame++)
            Assert.That(engine.Tick(0.016), Is.False);

        Assert.That(thrower.Updates, Is.EqualTo(updatesWhenFaulted));
    }

    [Test]
    public void AFaultedEngineKeepsItsLastSnapshotSoTheHostCanStillPaint()
    {
        using var engine = Running(new WorkingScene());
        engine.Tick(0.016);
        engine.Systems.Add(new ThrowingSystem());

        engine.Tick(0.016);
        var snapshot = engine.GetRenderSnapshot();

        Assert.Multiple(() =>
        {
            Assert.That(engine.IsFaulted, Is.True);
            Assert.That(snapshot.Entries.Length, Is.EqualTo(1));
        });
    }

    [Test]
    public void LoadingAnotherSceneClearsTheFault()
    {
        using var engine = new Engine(audioEnabled: false);
        engine.ChangeScene(new ThrowingInitializeScene());
        engine.Tick(0);
        Assert.That(engine.IsFaulted, Is.True);

        engine.ChangeScene(new WorkingScene());
        engine.Tick(0);
        engine.NotifyFirstPresent();
        engine.Tick(0.016);

        Assert.Multiple(() =>
        {
            Assert.That(engine.IsFaulted, Is.False);
            Assert.That(engine.EntityManager.GetEntitiesWithTag("survivor"), Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void AFailedInitializationLeavesNoEntitiesFromTheSceneThatFailed()
    {
        using var engine = new Engine(audioEnabled: false);
        engine.ChangeScene(new WorkingScene());
        engine.Tick(0);
        engine.NotifyFirstPresent();

        engine.ChangeScene(new ThrowingInitializeScene());
        engine.Tick(0);

        Assert.That(engine.EntityManager.GetEntities(), Is.Empty);
    }

    [Test]
    public void RepeatedSceneChangesAfterAFaultKeepWorking()
    {
        using var engine = new Engine(audioEnabled: false);

        for (int reload = 0; reload < 5; reload++)
        {
            engine.ChangeScene(new ThrowingInitializeScene());
            engine.Tick(0);
            Assert.That(engine.IsFaulted, Is.True);

            engine.ChangeScene(new WorkingScene());
            engine.Tick(0);
            engine.NotifyFirstPresent();
            engine.Tick(0.016);
            Assert.That(engine.IsFaulted, Is.False);
        }
    }
}
