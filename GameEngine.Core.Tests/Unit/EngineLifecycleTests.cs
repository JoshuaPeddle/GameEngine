using System.Diagnostics;
using GameEngine.Core.Components;
using GameEngine.Core.Systems;

namespace GameEngine.Core.Tests.Unit;

public class EngineLifecycleTests
{
    private sealed class CountingScene : Scene
    {
        public int Updates;

        public override void Initialize(EntityManager entityManager, InputManager inputManager,
            AudioSystem? audioPlayer, Action<Scene?> resetScene)
        {
            var entity = entityManager.CreateEntity("counter");
            entity.AddComponent(new CTransform(Vec2.Zero));
        }

        public override void Update(EntityManager entityManager, SystemContainer systems, double deltaSeconds)
        {
            Updates++;
        }
    }

    private static (Engine engine, CountingScene scene) Started()
    {
        var engine = new Engine(audioEnabled: false);
        var scene = new CountingScene();
        engine.ChangeScene(scene);
        engine.Tick(1.0 / 60.0);
        engine.NotifyFirstPresent();
        return (engine, scene);
    }

    [Test]
    public void Tick_AdvancesTheScene()
    {
        var (engine, scene) = Started();

        engine.Tick(1.0 / 60.0);
        engine.Tick(1.0 / 60.0);

        Assert.That(scene.Updates, Is.EqualTo(2));
    }

    [Test]
    public void Stop_HaltsTicking()
    {
        var (engine, scene) = Started();
        engine.Tick(1.0 / 60.0);
        int before = scene.Updates;

        engine.Stop();

        Assert.Multiple(() =>
        {
            Assert.That(engine.Tick(1.0 / 60.0), Is.False);
            Assert.That(scene.Updates, Is.EqualTo(before));
            Assert.That(engine.IsStopped, Is.True);
        });
    }

    [Test]
    public void PausingIsNotStopping()
    {
        var (engine, scene) = Started();

        engine.SetRunning(false);
        engine.Tick(1.0 / 60.0);
        Assert.That(scene.Updates, Is.Zero);

        engine.SetRunning(true);
        engine.Tick(1.0 / 60.0);

        Assert.Multiple(() =>
        {
            Assert.That(scene.Updates, Is.EqualTo(1), "a paused engine resumes; a stopped one does not");
            Assert.That(engine.IsStopped, Is.False);
        });
    }

    [Test]
    public void Dispose_StopsAndReleasesSystems()
    {
        var engine = new Engine(audioEnabled: false);

        engine.Dispose();

        Assert.Multiple(() =>
        {
            Assert.That(engine.IsStopped, Is.True);
            Assert.That(engine.Systems.Systems, Is.Empty);
        });
    }

    [Test]
    public void Dispose_IsIdempotent()
    {
        var engine = new Engine(audioEnabled: false);

        Assert.DoesNotThrow(() =>
        {
            engine.Dispose();
            engine.Dispose();
        });
    }

    [Test]
    public void Stop_EndsTheBackgroundLoopThread()
    {
        var engine = new Engine(audioEnabled: false);
        engine.ChangeScene(new CountingScene());
        engine.TargetFrameRate = 120;
        engine.Start();

        var startedWithin = Stopwatch.StartNew();
        while (startedWithin.ElapsedMilliseconds < 2000 && !engine.IsRunning)
            Thread.Sleep(5);

        engine.Stop();

        var stoppedWithin = Stopwatch.StartNew();
        while (stoppedWithin.ElapsedMilliseconds < 2000 && !engine.IsStopped)
            Thread.Sleep(5);

        Assert.That(engine.IsStopped, Is.True);
        Assert.That(engine.Tick(1.0 / 60.0), Is.False);
    }
}
