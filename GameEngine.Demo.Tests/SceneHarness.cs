using GameEngine.Core;
using GameEngine.Core.Components;
using GameEngine.Core.Systems;

namespace GameEngine.Demo.Tests;

public sealed class SceneHarness
{
    public const double FrameSeconds = 1.0 / 60.0;

    public Engine Engine { get; }
    public Scene Scene { get; }
    public EntityManager Entities => Engine.EntityManager;
    public InputManager Input => Engine.InputManager;

    private SceneHarness(Engine engine, Scene scene)
    {
        Engine = engine;
        Scene = scene;
    }

    public static SceneHarness Load(Scene scene)
    {
        var engine = new Engine(audioEnabled: false);
        engine.SizeChanged(800, 600);
        engine.ChangeScene(scene);

        engine.Tick(FrameSeconds);

        try { AssertHealthy(engine, scene, "initial load"); }
        catch { engine.Dispose(); throw; }
        engine.NotifyFirstPresent();

        return new SceneHarness(engine, scene);
    }

    public SceneHarness Run(int frames)
    {
        for (int frame = 0; frame < frames; frame++)
        {
            try
            {
                Engine.Tick(FrameSeconds);
            }
            catch (Exception ex)
            {
                Assert.Fail($"{Scene.GetType().Name} threw on frame {frame}: {ex}");
            }

            AssertHealthy(Engine, Scene, $"frame {frame}");
            AssertStateIsFinite(frame);
        }

        return this;
    }

    private static void AssertHealthy(Engine engine, Scene scene, string phase)
    {
        if (engine.Fault is { } fault)
            Assert.Fail($"{scene.GetType().Name} faulted during {phase}: {fault}\n{fault.Exception}");
    }

    public SceneHarness ExpectFault(int frames, string message)
    {
        for (int frame = 0; frame < frames && Engine.Fault == null; frame++)
            Engine.Tick(FrameSeconds);
        Assert.That(Engine.Fault, Is.Not.Null, "Expected the engine to fault.");
        Assert.That(Engine.Fault!.Exception.Message, Does.Contain(message));
        return this;
    }

    public void PointerGesture(Vec2 start, Vec2 end)
    {
        Input.HandlePointerEvent(Pointer.PointerEventType.Press, new Pointer.PointerPressEvent(start));
        Run(1);
        Input.HandlePointerEvent(Pointer.PointerEventType.Move, new Pointer.PointerMoveEvent(end));
        Run(1);
        Input.HandlePointerEvent(Pointer.PointerEventType.Release, new Pointer.PointerReleaseEvent(end));
        Run(1);
    }

    private void AssertStateIsFinite(int frame)
    {
        foreach (var entity in Entities.GetEntities())
        {
            if (!entity.TryGetComponent<CTransform>(out var transform))
                continue;

            bool finite = double.IsFinite(transform.Position.X) && double.IsFinite(transform.Position.Y)
                       && double.IsFinite(transform.Velocity.X) && double.IsFinite(transform.Velocity.Y);

            if (!finite)
                Assert.Fail($"{Scene.GetType().Name}: entity '{entity.Tag}' has non-finite state on " +
                            $"frame {frame} — position {transform.Position}, velocity {transform.Velocity}");
        }
    }

    public IReadOnlyList<RenderSnapshot.Entry> Snapshot()
    {
        var snapshot = Engine.GetRenderSnapshot();
        var entries = new List<RenderSnapshot.Entry>();
        foreach (var entry in snapshot.Entries) entries.Add(entry);
        return entries;
    }

    public Entity Require(string tag)
    {
        var entity = Entities.GetEntityWithTag(tag);
        Assert.That(entity, Is.Not.Null, $"{Scene.GetType().Name} should contain an entity tagged '{tag}'");
        return entity!;
    }

    public Vec2 PositionOf(string tag) => Require(tag).GetComponent<CTransform>().Position;

    public void PressAndRelease(GeKeys key, int framesHeld = 5)
    {
        var input = Engine.Systems.Get<InputSystem>();
        input.KeyDown(key);
        Run(framesHeld);
        input.KeyUp(key);
        Run(1);
    }

    public static IEnumerable<Type> AllDemoSceneTypes() =>
        typeof(ScenePong).Assembly
            .GetTypes()
            .Where(t => t.IsSubclassOf(typeof(Scene)) && !t.IsAbstract)
            .Where(t => t.GetConstructor(Type.EmptyTypes) != null)
            .OrderBy(t => t.Name);

    public static Scene Instantiate(Type sceneType) =>
        (Scene)Activator.CreateInstance(sceneType)!;
}

[SetUpFixture]
public class AssetEnvironment
{
    [OneTimeSetUp]
    public void RunFromADirectoryWithNoAssets()
    {
        Directory.SetCurrentDirectory(Path.GetTempPath());
    }
}
