using GameEngine.Core;
using GameEngine.Core.Components;
using GameEngine.Core.Systems;

namespace GameTemplate.Tests;

// Boots a scene through a real engine with a fixed timestep, without a window. This is the
// inner loop for changing a scene or a level: write, run, read a real error.
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
        engine.SizeChanged(scene.VirtualWidth, scene.VirtualHeight);
        engine.ChangeScene(scene);
        engine.Tick(FrameSeconds);
        engine.NotifyFirstPresent();

        return new SceneHarness(engine, scene);
    }

    public SceneHarness Run(int frames)
    {
        for (var frame = 0; frame < frames; frame++)
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

    public SceneHarness PressAndRelease(GeKeys key, int framesHeld)
    {
        var input = Engine.Systems.Get<InputSystem>();
        input.KeyDown(key);
        Run(framesHeld);
        input.KeyUp(key);
        return Run(1);
    }

    public Entity Require(string tag) =>
        Entities.GetEntityWithTag(tag)
        ?? throw new AssertionException($"no entity tagged '{tag}' in {Scene.GetType().Name}");

    private void AssertStateIsFinite(int frame)
    {
        foreach (var entity in Entities.GetEntities())
        {
            if (!entity.TryGetComponent<CTransform>(out var transform))
                continue;

            var finite = double.IsFinite(transform.Position.X) && double.IsFinite(transform.Position.Y)
                && double.IsFinite(transform.Velocity.X) && double.IsFinite(transform.Velocity.Y);

            if (!finite)
                Assert.Fail($"{Scene.GetType().Name}: entity '{entity.Tag}' has non-finite state on "
                    + $"frame {frame} — position {transform.Position}, velocity {transform.Velocity}");
        }
    }
}
