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

    private sealed class BlockingSystem : ISystem
    {
        public readonly ManualResetEventSlim Entered = new(false);
        public readonly ManualResetEventSlim Release = new(false);

        public void Update(EntityManager entityManager, double deltaSeconds)
        {
            Entered.Set();
            Release.Wait();
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
            Assert.That(engine.IsRunning, Is.False);
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
    public void SceneChange_KeepsTheSameEntityAndInputManagers()
    {
        var (engine, _) = Started();
        var entities = engine.EntityManager;
        var input = engine.InputManager;

        engine.ChangeScene(new CountingScene());
        engine.Tick(1.0 / 60.0);
        engine.NotifyFirstPresent();

        Assert.Multiple(() =>
        {
            Assert.That(engine.EntityManager, Is.SameAs(entities), "a cached EntityManager must not go stale");
            Assert.That(engine.InputManager, Is.SameAs(input), "a cached InputManager must not go stale");
        });
    }

    [Test]
    public void SceneChange_KeepsTheReportedScreenSize()
    {
        var (engine, _) = Started();
        engine.SizeChanged(1280, 720);

        engine.ChangeScene(new CountingScene());
        engine.Tick(1.0 / 60.0);
        engine.NotifyFirstPresent();

        Assert.That(engine.InputManager.RealResolution, Is.EqualTo(new Vec2(1280, 720)));
    }

    [Test]
    public void SizeChanged_DoesNotDependOnTheTransientSystemContainer()
    {
        using var engine = new Engine(audioEnabled: false);
        engine.Systems.Dispose();

        Assert.DoesNotThrow(() => engine.SizeChanged(1080, 2400));
        Assert.That(engine.InputManager.RealResolution, Is.EqualTo(new Vec2(1080, 2400)));
    }

    [Test]
    public void SceneChange_ClearsInputBoundToThePreviousScene()
    {
        var (engine, _) = Started();
        bool firedFromOldScene = false;
        engine.InputManager.AddAction(GeKeys.Space, "Jump");
        engine.InputManager.BindAction("Jump", active => firedFromOldScene |= active);

        engine.ChangeScene(new CountingScene());
        engine.Tick(1.0 / 60.0);
        engine.NotifyFirstPresent();

        engine.InputManager.HandleKeyPress(GeKeys.Space);
        engine.Tick(1.0 / 60.0);

        Assert.That(firedFromOldScene, Is.False, "bindings from a discarded scene must not survive");
    }

    [Test]
    public void SceneChange_ClearsEntitiesFromThePreviousScene()
    {
        var (engine, _) = Started();
        int before = engine.EntityManager.GetEntities().Count;

        engine.ChangeScene(new CountingScene());
        engine.Tick(1.0 / 60.0);
        engine.NotifyFirstPresent();
        engine.Tick(1.0 / 60.0);

        Assert.That(engine.EntityManager.GetEntities(), Has.Count.EqualTo(before),
            "the new scene starts from a clean entity set");
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
    public async Task Dispose_WaitsForAnInFlightUpdateBeforeReleasingState()
    {
        var engine = new Engine(audioEnabled: false);
        var blocking = new BlockingSystem();
        engine.Systems.Add(blocking);
        Task? dispose = null;
        try
        {
            await engine.Start();
            Assert.That(blocking.Entered.Wait(TimeSpan.FromSeconds(2)), Is.True,
                "the engine loop never entered the blocking system");

            dispose = Task.Run(engine.Dispose);
            await Task.Delay(50);
            Assert.That(dispose.IsCompleted, Is.False,
                "Dispose returned while an update was still executing");
        }
        finally
        {
            blocking.Release.Set();
            dispose ??= Task.Run(engine.Dispose);
            await dispose.WaitAsync(TimeSpan.FromSeconds(2));
        }

        Assert.Multiple(() =>
        {
            Assert.That(engine.IsStopped, Is.True);
            Assert.That(engine.Systems.Systems, Is.Empty);
            Assert.That(engine.EntityManager.GetEntities(), Is.Empty);
        });
    }

    [Test]
    public void RepeatedSceneChanges_LeaveEverySystemResolvable()
    {
        using var engine = new Engine(audioEnabled: false);

        for (int reload = 0; reload < 20; reload++)
        {
            engine.ChangeScene(new CountingScene());
            engine.Tick(0.016);
            engine.NotifyFirstPresent();
            engine.Tick(0.016);

            Assert.Multiple(() =>
            {
                Assert.That(engine.Systems.TryGet<InputSystem>(), Is.Not.Null);
                Assert.That(engine.Systems.TryGet<RenderSystem>(), Is.Not.Null);
                Assert.That(engine.EntityManager.GetEntitiesWithTag("counter"), Has.Count.EqualTo(1));
            });
        }
    }

    [Test]
    public void RepeatedCreateAndDispose_LeavesNoRunningLoop()
    {
        for (int reload = 0; reload < 10; reload++)
        {
            var engine = new Engine(audioEnabled: false);
            engine.ChangeScene(new CountingScene());
            engine.Start();
            engine.NotifyFirstPresent();
            engine.Dispose();

            Assert.Multiple(() =>
            {
                Assert.That(engine.IsStopped, Is.True);
                Assert.That(engine.IsDisposed, Is.True);
                Assert.That(engine.Systems.TryGet<RenderSystem>(), Is.Null);
            });
        }
    }

    [Test]
    public void Start_IsIdempotentWhileTheLoopIsRunning()
    {
        using var engine = new Engine(audioEnabled: false);

        Assert.DoesNotThrow(() =>
        {
            engine.Start();
            engine.Start();
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
    public void DisposedEngine_CannotAcquireAnotherScene()
    {
        var engine = new Engine(audioEnabled: false);
        engine.Dispose();

        Assert.Throws<ObjectDisposedException>(() => engine.ChangeScene(new CountingScene()));
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
