using GameEngine.Core;
using GameEngine.Core.Components;
using GameEngine.Core.Systems;

namespace GameEngine.Demo.Tests;

// A real bouncing scene rather than a rig: Pong's ball is the fastest thing the demos move,
// and the walls it bounces off are the ones a fast ball would leave through.
public class FastBallTests
{
    private const int WallThickness = 30;

    private static (SceneHarness Harness, CTransform Ball) PongWithBallSpeed(Vec2 velocity)
    {
        var harness = SceneHarness.Load(new ScenePong());
        var ball = harness.Require("ball").GetComponent<CTransform>();

        ball.Position = new Vec2(400, 300);
        ball.Velocity = velocity;
        harness.Require("ball").GetComponent<CMovement>().MaxSpeed = velocity.Length() * 2;

        return (harness, ball);
    }

    private static void AssertInsideTheCourt(CTransform ball, string when)
    {
        Assert.Multiple(() =>
        {
            Assert.That(ball.Position.X, Is.InRange(0.0, 800.0), $"{when}: ball left the court on X");
            Assert.That(ball.Position.Y, Is.InRange(0.0, 600.0), $"{when}: ball left the court on Y");
        });
    }

    [Test]
    public void AFastBallStaysInsideTheCourt()
    {
        var (harness, ball) = PongWithBallSpeed(new Vec2(4_000, 2_600));

        for (int frame = 0; frame < 600; frame++)
        {
            harness.Run(1);
            AssertInsideTheCourt(ball, $"frame {frame}");
        }
    }

    [Test]
    public void AVeryFastBallStillBouncesOffTheWalls()
    {
        var (harness, ball) = PongWithBallSpeed(new Vec2(0, 9_000));

        harness.Run(120);

        Assert.Multiple(() =>
        {
            Assert.That(ball.Position.Y, Is.InRange(0.0, 600.0));
            Assert.That(ball.Position.Y + 20, Is.LessThanOrEqualTo(600 - WallThickness + 0.5),
                "the ball must stay above the bottom wall it is bouncing off");
        });
    }

    [Test]
    public void ASlowBallBehavesAsItAlwaysHas()
    {
        var (harness, ball) = PongWithBallSpeed(new Vec2(-600, 120));

        harness.Run(300);

        Assert.Multiple(() =>
        {
            AssertInsideTheCourt(ball, "after 300 frames");
            Assert.That(ball.Velocity.Length(), Is.GreaterThan(1.0), "the ball must still be moving");
        });
    }

    // BrickBreaker's walls are triggers, not solids: the scene reverses the ball itself. The
    // engine's job there is to report the contact at all, which before the sweep it could not
    // do for a ball that crossed the wall inside one step.
    [Test]
    public void AFastBallReportsAWallItWouldHaveCrossedUnseen()
    {
        var harness = SceneHarness.Load(new SceneBrickBreaker());
        harness.Run(5);

        var ball = harness.Entities.GetEntities().FirstOrDefault(e => e.Tag.StartsWith("ball"));
        Assert.That(ball, Is.Not.Null, "BrickBreaker should have a ball in play after five frames");

        var transform = ball!.GetComponent<CTransform>();
        transform.Position = new Vec2(520, 400);
        transform.Velocity = new Vec2(6_000, 0);
        ball.GetComponent<CMovement>().MaxSpeed = 8_000;

        harness.Run(1);

        var contacts = harness.Engine.Systems.Get<PhysicsSystem>().CollisionEvents
            .Where(c => c.A.Tag == "wallRight" || c.B.Tag == "wallRight")
            .ToList();

        Assert.That(contacts, Is.Not.Empty,
            "a ball that crossed the right wall inside one step must still report the contact");
    }
}
