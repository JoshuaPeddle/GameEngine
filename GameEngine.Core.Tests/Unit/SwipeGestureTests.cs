namespace GameEngine.Core.Tests.Unit;

public class SwipeGestureTests
{
    private static readonly Vec2 Origin = new(100, 100);

    [Test]
    public void ShortMovement_IsATap()
    {
        Assert.That(SwipeGesture.Classify(Origin, new Vec2(105, 103)), Is.EqualTo(GeKeys.Space));
    }

    [Test]
    public void NoMovement_IsATap()
    {
        Assert.That(SwipeGesture.Classify(Origin, Origin), Is.EqualTo(GeKeys.Space));
    }

    [TestCase(60, 0, GeKeys.D)]
    [TestCase(-60, 0, GeKeys.A)]
    [TestCase(0, 60, GeKeys.S)]
    [TestCase(0, -60, GeKeys.W)]
    public void LongMovement_IsADirectionalSwipe(double deltaX, double deltaY, GeKeys expected)
    {
        var end = new Vec2(Origin.X + deltaX, Origin.Y + deltaY);

        Assert.That(SwipeGesture.Classify(Origin, end), Is.EqualTo(expected));
    }

    [Test]
    public void DominantAxisWins()
    {
        Assert.Multiple(() =>
        {
            Assert.That(SwipeGesture.Classify(Origin, new Vec2(160, 130)), Is.EqualTo(GeKeys.D));
            Assert.That(SwipeGesture.Classify(Origin, new Vec2(130, 160)), Is.EqualTo(GeKeys.S));
        });
    }

    [Test]
    public void ThresholdIsHonoured()
    {
        var end = new Vec2(130, 100);

        Assert.Multiple(() =>
        {
            Assert.That(SwipeGesture.Classify(Origin, end, threshold: 20), Is.EqualTo(GeKeys.D));
            Assert.That(SwipeGesture.Classify(Origin, end, threshold: 50), Is.EqualTo(GeKeys.Space));
        });
    }
}
