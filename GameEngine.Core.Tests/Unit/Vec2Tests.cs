namespace GameEngine.Core.Tests.Unit;

public class Vec2Tests
{
    [Test]
    public void Normalize_OfTheZeroVector_IsZeroRatherThanNaN()
    {
        var normalized = Vec2.Zero.Normalize();

        Assert.Multiple(() =>
        {
            Assert.That(double.IsNaN(normalized.X), Is.False);
            Assert.That(double.IsNaN(normalized.Y), Is.False);
            Assert.That(normalized, Is.EqualTo(Vec2.Zero));
        });
    }

    [Test]
    public void Normalize_ProducesAUnitVector()
    {
        var normalized = new Vec2(3, 4).Normalize();

        Assert.Multiple(() =>
        {
            Assert.That(normalized.Length(), Is.EqualTo(1).Within(1e-12));
            Assert.That(normalized.X, Is.EqualTo(0.6).Within(1e-12));
            Assert.That(normalized.Y, Is.EqualTo(0.8).Within(1e-12));
        });
    }

    [Test]
    public void Normalize_KeepsFullPrecision()
    {
        var normalized = new Vec2(1e-8, 0).Normalize();

        Assert.That(normalized.X, Is.EqualTo(1).Within(1e-12));
    }

    [Test]
    public void MagnitudeAndLengthAgree()
    {
        var vector = new Vec2(3, 4);

        Assert.Multiple(() =>
        {
            Assert.That(vector.Length(), Is.EqualTo(5).Within(1e-12));
            Assert.That(vector.Magnitude(), Is.EqualTo(5).Within(1e-12));
            Assert.That(vector.LengthSquared(), Is.EqualTo(25).Within(1e-12));
        });
    }

    [Test]
    public void DistanceTo_IsSymmetric()
    {
        var a = new Vec2(1, 2);
        var b = new Vec2(4, 6);

        Assert.Multiple(() =>
        {
            Assert.That(a.DistanceTo(b), Is.EqualTo(5).Within(1e-12));
            Assert.That(b.DistanceTo(a), Is.EqualTo(5).Within(1e-12));
        });
    }

    [Test]
    public void Lerp_InterpolatesBetweenEndpoints()
    {
        var from = new Vec2(0, 10);
        var to = new Vec2(10, 20);

        Assert.Multiple(() =>
        {
            Assert.That(Vec2.Lerp(from, to, 0), Is.EqualTo(from));
            Assert.That(Vec2.Lerp(from, to, 1), Is.EqualTo(to));
            Assert.That(Vec2.Lerp(from, to, 0.5), Is.EqualTo(new Vec2(5, 15)));
        });
    }

    [Test]
    public void Rotate_TurnsCounterToTheAxes()
    {
        var rotated = new Vec2(1, 0).Rotate(90);

        Assert.Multiple(() =>
        {
            Assert.That(rotated.X, Is.EqualTo(0).Within(1e-6));
            Assert.That(rotated.Y, Is.EqualTo(1).Within(1e-6));
        });
    }

    [Test]
    public void DotAndCross()
    {
        var a = new Vec2(1, 2);
        var b = new Vec2(3, 4);

        Assert.Multiple(() =>
        {
            Assert.That(a.Dot(b), Is.EqualTo(11).Within(1e-12));
            Assert.That(a.Cross(b), Is.EqualTo(-2).Within(1e-12));
        });
    }

    [Test]
    public void EqualityMatchesHashing()
    {
        var a = new Vec2(1.5, -2.5);
        var b = new Vec2(1.5, -2.5);

        Assert.Multiple(() =>
        {
            Assert.That(a == b, Is.True);
            Assert.That(a.Equals(b), Is.True);
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
            Assert.That(a != new Vec2(1.5, 2.5), Is.True);
        });
    }
}
