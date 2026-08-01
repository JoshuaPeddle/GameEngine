using GameEngine.Core;
using GameEngine.Core.Components;

namespace GameEngine.Demo.Tests;

public class PongWedgeTests
{
    private static (SceneHarness harness, CTransform ball) WedgedBall(
        Vec2 ballPosition, Vec2 ballVelocity, Vec2 paddlePosition)
    {
        var harness = SceneHarness.Load(new ScenePong());

        var paddle = harness.Require("paddle1").GetComponent<CTransform>();
        paddle.Position = paddlePosition;

        var ball = harness.Require("ball").GetComponent<CTransform>();
        ball.Position = ballPosition;
        ball.Velocity = ballVelocity;

        return (harness, ball);
    }

    [Test]
    public void BallWedgedBetweenPaddleAndTopWall_Escapes()
    {
        var (harness, ball) = WedgedBall(
            ballPosition: new Vec2(50, 25),
            ballVelocity: new Vec2(-600, -200),
            paddlePosition: new Vec2(35, 30));

        harness.Run(240);

        Assert.That(ball.Velocity.Length(), Is.GreaterThan(1.0),
            $"the ball is stuck at {ball.Position} with velocity {ball.Velocity}");
    }

    [Test]
    public void BallWedgedBetweenPaddleAndBottomWall_Escapes()
    {
        var (harness, ball) = WedgedBall(
            ballPosition: new Vec2(50, 545),
            ballVelocity: new Vec2(-600, 200),
            paddlePosition: new Vec2(35, 470));

        harness.Run(240);

        Assert.That(ball.Velocity.Length(), Is.GreaterThan(1.0),
            $"the ball is stuck at {ball.Position} with velocity {ball.Velocity}");
    }

    [TestCase(35, 25, 0)]
    [TestCase(35, 30, 0)]
    [TestCase(35, 35, -150)]
    [TestCase(35, 40, 0)]
    [TestCase(35, 50, 0)]
    public void BallPinnedByAPaddleHeldAgainstTheTopWall_Escapes(double ballX, double ballY, double ballVelocityY)
    {
        var harness = SceneHarness.Load(new ScenePong());
        var paddle = harness.Require("paddle1");
        var paddleTransform = paddle.GetComponent<CTransform>();
        var paddleInput = paddle.GetComponent<CInput>();
        var ball = harness.Require("ball").GetComponent<CTransform>();

        paddleTransform.Position = new Vec2(35, 30);
        ball.Position = new Vec2(ballX, ballY);
        ball.Velocity = new Vec2(-600, ballVelocityY);

        int stillFrames = 0;
        int longestStillRun = 0;

        for (int frame = 0; frame < 400; frame++)
        {
            paddleInput.Up = true;
            harness.Run(1);
            paddleInput.Up = true;

            stillFrames = ball.Velocity.Length() < 1.0 ? stillFrames + 1 : 0;
            longestStillRun = Math.Max(longestStillRun, stillFrames);
        }

        Assert.That(longestStillRun, Is.LessThan(5),
            $"ball sat motionless for {longestStillRun} frames at {ball.Position}");
    }

    [Test]
    public void BallDrivenIntoTheCornerRepeatedly_NeverStalls()
    {
        var harness = SceneHarness.Load(new ScenePong());
        var ball = harness.Require("ball").GetComponent<CTransform>();
        var paddle = harness.Require("paddle1").GetComponent<CTransform>();

        var random = new Random(4);

        for (int attempt = 0; attempt < 40; attempt++)
        {
            paddle.Position = new Vec2(35, random.Next(30, 470));
            ball.Position = new Vec2(random.Next(40, 70), random.Next(25, 60));
            ball.Velocity = new Vec2(-600, random.Next(-400, 400));

            harness.Run(60);

            Assert.That(ball.Velocity.Length(), Is.GreaterThan(1.0),
                $"attempt {attempt}: ball stalled at {ball.Position}");
        }
    }

    [Test]
    public void BallStaysInsideThePlayfield()
    {
        var harness = SceneHarness.Load(new ScenePong());
        var ball = harness.Require("ball").GetComponent<CTransform>();
        ball.Velocity = new Vec2(-600, 350);

        for (int frame = 0; frame < 2000; frame++)
        {
            harness.Run(1);

            Assert.That(ball.Position.Y, Is.InRange(-40.0, 640.0),
                $"ball left the playfield vertically on frame {frame}");
        }
    }
}
