using System.Reflection;
using GameEngine.Core;
using GameEngine.Core.Components;
using GameEngine.Core.Systems;

namespace GameEngine.Demo.Tests;

/// <summary>
/// Drives a real <see cref="Engine"/> headlessly so scenes can be exercised the way the
/// runners exercise them — same system order, same scene-change path, same snapshot
/// publication — but with a fixed delta and no thread, so results are deterministic.
/// </summary>
public sealed class SceneHarness
{
    /// <summary>60fps, matching what the demos were tuned against.</summary>
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

    /// <summary>
    /// Loads a scene and runs it up to its first live frame. Audio is off: these tests never
    /// assert on sound, and a mixer is not available on every machine.
    /// </summary>
    public static SceneHarness Load(Scene scene)
    {
        var engine = new Engine(audioEnabled: false);
        engine.SizeChanged(800, 600);
        engine.ChangeScene(scene);

        // Applies the queued scene change; the engine then holds until a paint happens.
        engine.Tick(FrameSeconds);

        // Stand in for a runner presenting that first frame.
        engine.NotifyFirstPresent();

        return new SceneHarness(engine, scene);
    }

    /// <summary>Advance <paramref name="frames"/> frames, asserting the engine stays healthy.</summary>
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

            AssertStateIsFinite(frame);
        }

        return this;
    }

    /// <summary>
    /// A scene that produces NaN or infinite positions still "runs" — it just renders nothing
    /// sensible. Catching it here is the difference between a green suite and a working game.
    /// </summary>
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

    /// <summary>Builds the render snapshot the UI thread would read, and returns its entries.</summary>
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

    /// <summary>Every concrete <see cref="Scene"/> the demo assembly ships.</summary>
    public static IEnumerable<Type> AllDemoSceneTypes() =>
        typeof(ScenePong).Assembly
            .GetTypes()
            .Where(t => t.IsSubclassOf(typeof(Scene)) && !t.IsAbstract)
            .Where(t => t.GetConstructor(Type.EmptyTypes) != null)
            .OrderBy(t => t.Name);

    public static Scene Instantiate(Type sceneType) =>
        (Scene)Activator.CreateInstance(sceneType)!;
}

/// <summary>
/// Scenes resolve assets relative to the working directory, so point it at the test binary,
/// where the csproj has staged assets.txt, the textures and the levels.
/// </summary>
[SetUpFixture]
public class AssetEnvironment
{
    [OneTimeSetUp]
    public void PointWorkingDirectoryAtStagedAssets()
    {
        Directory.SetCurrentDirectory(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!);

        // Nothing here should depend on a fetcher another suite installed.
        Assets._fileFetcher = null;
    }
}
