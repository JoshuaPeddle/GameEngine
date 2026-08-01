using GameEngine.Core;
using GameEngine.Core.Components;

namespace GameEngine.Demo.Tests;

/// <summary>
/// Smoke tests over every shipped demo scene, plus per-scene checks on the behaviour each one
/// actually exists to demonstrate.
/// <para>
/// The generic pass is the regression net: it drives real scenes through a real engine, which
/// is what would have caught the Pong ball freezing on a paddle after the collision-resolution
/// change. A scene that loads and runs without throwing is not necessarily a scene that works.
/// </para>
/// </summary>
public class DemoSceneTests
{
    private static IEnumerable<Type> SceneTypes() => SceneHarness.AllDemoSceneTypes();

    [TestCaseSource(nameof(SceneTypes))]
    public void Scene_LoadsAndRunsWithoutFaulting(Type sceneType)
    {
        var harness = SceneHarness.Load(SceneHarness.Instantiate(sceneType));

        harness.Run(300); // five seconds at 60fps

        Assert.That(harness.Engine.IsRunning, Is.True);
    }

    [TestCaseSource(nameof(SceneTypes))]
    public void Scene_PublishesARenderableSnapshot(Type sceneType)
    {
        var harness = SceneHarness.Load(SceneHarness.Instantiate(sceneType));
        harness.Run(10);

        var entries = harness.Snapshot();

        if (sceneType == typeof(SceneEmpty))
        {
            Assert.That(entries, Is.Empty, "the empty scene is meant to have nothing in it");
            return;
        }

        Assert.That(entries, Is.Not.Empty, $"{sceneType.Name} drew nothing");
        Assert.That(entries.Select(e => e.EntityId).Distinct().Count(), Is.EqualTo(entries.Count),
            "every entry should be a distinct entity");
    }

    [TestCaseSource(nameof(SceneTypes))]
    public void Scene_SurvivesBeingReset(Type sceneType)
    {
        // ResetScene rebuilds the systems and the entity manager, which is the path most likely
        // to leave a scene holding a stale reference.
        var harness = SceneHarness.Load(SceneHarness.Instantiate(sceneType));
        harness.Run(30);

        harness.Engine.ChangeScene(SceneHarness.Instantiate(sceneType));
        harness.Engine.Tick(SceneHarness.FrameSeconds);
        harness.Engine.NotifyFirstPresent();

        harness.Run(60);
        Assert.Pass();
    }

    // ── Per-scene behaviour ─────────────────────────────────────────────

    [Test]
    public void Pong_BallKeepsRallying()
    {
        // The regression that shipped past the unit tests: the ball hit a paddle and stopped.
        var harness = SceneHarness.Load(new ScenePong());
        var ball = harness.Require("ball").GetComponent<CTransform>();

        int paddleReversals = 0;
        double previousDirection = Math.Sign(ball.Velocity.X);

        for (int frame = 0; frame < 1200; frame++)
        {
            harness.Run(1);

            double direction = Math.Sign(ball.Velocity.X);
            if (direction != 0 && direction != previousDirection)
            {
                paddleReversals++;
                previousDirection = direction;
            }

            Assert.That(Math.Abs(ball.Velocity.X), Is.GreaterThan(0.001),
                $"the ball stopped moving horizontally on frame {frame}");
        }

        Assert.That(paddleReversals, Is.GreaterThanOrEqualTo(2),
            "the ball should have reversed off paddles or walls several times");
    }

    [Test]
    public void Pong_BallBouncesOffTheTopWall()
    {
        var harness = SceneHarness.Load(new ScenePong());
        var ball = harness.Require("ball").GetComponent<CTransform>();

        ball.Position = new Vec2(400, 60);
        ball.Velocity = new Vec2(0, -300);

        harness.Run(30);

        Assert.That(ball.Velocity.Y, Is.GreaterThan(0),
            "the ball should be travelling downwards again after hitting the top wall");
    }

    [Test]
    public void SideScroll2_PlayerFallsAndComesToRestOnAPlatform()
    {
        var harness = SceneHarness.Load(new SceneSideScroll2());
        var player = harness.Require("player").GetComponent<CTransform>();
        double startY = player.Position.Y;

        harness.Run(120);

        Assert.That(player.Position.Y, Is.GreaterThan(startY), "gravity should have pulled the player down");

        // Once landed it should settle rather than sink through or jitter.
        double landedY = player.Position.Y;
        harness.Run(60);
        Assert.That(player.Position.Y, Is.EqualTo(landedY).Within(2.0), "the player should rest on the platform");
    }

    [Test]
    public void Snake_HeadAdvancesOverTime()
    {
        var harness = SceneHarness.Load(new SceneSnake());
        var head = harness.Require("SnakeHead").GetComponent<CTransform>();
        var start = head.Position;

        harness.Run(180);

        Assert.That(head.Position, Is.Not.EqualTo(start), "the snake should have moved");
    }

    [Test]
    public void BrickBreaker_HoldsTheBallUntilItIsLaunched()
    {
        // The scene opens in an aiming state: the ball sits on the paddle and only launches on
        // a pointer press. Staying still here is correct, so this pins that rather than
        // asserting motion.
        var harness = SceneHarness.Load(new SceneBrickBreaker());
        var ball = harness.Require("ball").GetComponent<CTransform>();
        var startPosition = ball.Position;

        harness.Run(300);

        Assert.Multiple(() =>
        {
            Assert.That(ball.Velocity.Length(), Is.EqualTo(0).Within(0.001), "an unlaunched ball should be still");
            Assert.That(ball.Position.X, Is.EqualTo(startPosition.X).Within(0.001));
            Assert.That(ball.Position.Y, Is.EqualTo(startPosition.Y).Within(0.001));
        });
    }

    [Test]
    public void BrickBreaker_BuildsAFieldOfBricks()
    {
        var harness = SceneHarness.Load(new SceneBrickBreaker());
        harness.Run(10);

        var bricks = harness.Entities.GetEntities().Count(e => e.Tag.StartsWith("block"));
        Assert.That(bricks, Is.GreaterThan(0), "the level should contain breakable blocks");
    }

    [Test]
    public void Json_LoadsItsLevelFromDisk()
    {
        var harness = SceneHarness.Load(new SceneJson());
        harness.Run(30);

        Assert.That(harness.Entities.GetEntities(), Is.Not.Empty, "the JSON level produced no entities");
        Assert.That(harness.Entities.GetEntityWithTag("player"), Is.Not.Null,
            "level1.json declares a player");
    }

    [Test]
    public void Basic_PlayerRespondsToInput()
    {
        var harness = SceneHarness.Load(new SceneBasic());
        harness.Run(5);

        var player = harness.Require("player").GetComponent<CTransform>();
        double startX = player.Position.X;

        harness.PressAndRelease(GeKeys.D, framesHeld: 30);

        Assert.That(player.Position.X, Is.GreaterThan(startX), "holding D should move the player right");
    }
}
