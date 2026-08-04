using GameEngine.Core.Components;
using GameEngine.Core.Systems;

namespace GameEngine.Core.Tests.Unit;

public class EngineThreadWorkTests
{
    private sealed class EmptyScene : Scene
    {
        public override void Initialize(EntityManager entityManager, InputManager inputManager,
            AudioSystem? audioPlayer, Action<Scene?> resetScene)
        {
        }
    }

    private const double Frame = 1.0 / 60.0;

    [Test]
    public void PostedWork_RunsOnTheNextTick()
    {
        using var engine = new Engine(audioEnabled: false);
        var ran = false;

        engine.Post(_ => ran = true);

        Assert.That(ran, Is.False, "work must not run on the posting thread");

        engine.Tick(Frame);

        Assert.That(ran, Is.True);
    }

    [Test]
    public void PostedWork_RunsWhileThePreviewIsPaused()
    {
        using var engine = new Engine(audioEnabled: false);
        engine.SetRunning(false);
        var ran = false;

        engine.Post(_ => ran = true);
        engine.Tick(Frame);

        Assert.That(ran, Is.True, "the editor pauses the engine and still expects to pick entities");
    }

    [Test]
    public void PostedWork_RunsInOrder()
    {
        using var engine = new Engine(audioEnabled: false);
        var order = new List<int>();

        engine.Post(_ => order.Add(1));
        engine.Post(_ => order.Add(2));
        engine.Post(_ => order.Add(3));
        engine.Tick(Frame);

        Assert.That(order, Is.EqualTo(new[] { 1, 2, 3 }));
    }

    [Test]
    public void PostedWork_SeesTheEntitiesTheEngineOwns()
    {
        using var engine = new Engine(audioEnabled: false);
        engine.ChangeScene(new EmptyScene());
        engine.Tick(Frame);

        engine.EntityManager.CreateEntity("picked").AddComponent(new CTransform(Vec2.Zero));
        engine.EntityManager.Update();

        EntitySnapshot? seen = null;
        engine.Post(e => seen = e.EntityManager.GetEntities().Single().Capture());
        engine.Tick(Frame);

        Assert.That(seen!.Tag, Is.EqualTo("picked"));
    }

    [Test]
    public void Post_IsBoundedSoAStalledEngineCannotGrowTheQueue()
    {
        using var engine = new Engine(audioEnabled: false);

        var accepted = 0;
        for (int i = 0; i < 400; i++)
        {
            if (engine.Post(_ => { }))
                accepted++;
        }

        Assert.That(accepted, Is.EqualTo(256));
    }

    [Test]
    public void Post_AfterDispose_IsRefused()
    {
        var engine = new Engine(audioEnabled: false);
        engine.Dispose();

        Assert.That(engine.Post(_ => { }), Is.False);
    }

    [Test]
    public void DrainingRestoresQueueCapacity()
    {
        using var engine = new Engine(audioEnabled: false);

        for (int i = 0; i < 256; i++)
            engine.Post(_ => { });

        Assert.That(engine.Post(_ => { }), Is.False, "the queue should be full");

        engine.Tick(Frame);

        Assert.That(engine.Post(_ => { }), Is.True, "draining frees the queue again");
    }
}
