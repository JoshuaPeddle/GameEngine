using GameEngine.Core.Systems;
using static GameEngine.Core.Pointer;

namespace GameEngine.Core.Tests.Unit;

public class ViewportTransformTests
{
    private static readonly Vec2 Screen = new(1000, 400);
    private static readonly Vec2 Virtual = new(400, 300);

    [Test]
    public void Letterbox_FitsInsideAndCentres()
    {
        var viewport = ViewportTransform.Create(Screen, Virtual, ScalingStrategy.Letterbox);

        Assert.Multiple(() =>
        {
            Assert.That(viewport.ScaleX, Is.EqualTo(4.0 / 3).Within(1e-9));
            Assert.That(viewport.ScaleY, Is.EqualTo(4.0 / 3).Within(1e-9));
            Assert.That(viewport.OffsetY, Is.EqualTo(0).Within(1e-9));
            Assert.That(viewport.OffsetX, Is.GreaterThan(0));
        });
    }

    [Test]
    public void Letterbox_RoundTripsThroughVirtualSpace()
    {
        var viewport = ViewportTransform.Create(Screen, Virtual, ScalingStrategy.Letterbox);
        var real = viewport.ToReal(new Vec2(123, 45));

        Assert.That(viewport.TryToVirtual(real, out var back), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(back.X, Is.EqualTo(123).Within(1e-9));
            Assert.That(back.Y, Is.EqualTo(45).Within(1e-9));
        });
    }

    [Test]
    public void Letterbox_RejectsPositionsInTheBars()
    {
        var viewport = ViewportTransform.Create(Screen, Virtual, ScalingStrategy.Letterbox);

        Assert.That(viewport.TryToVirtual(new Vec2(2, 200), out _), Is.False);
        Assert.That(viewport.TryToVirtual(new Vec2(998, 200), out _), Is.False);
    }

    [Test]
    public void Stretch_FillsBothAxesIndependently()
    {
        var viewport = ViewportTransform.Create(Screen, Virtual, ScalingStrategy.Stretch);

        Assert.Multiple(() =>
        {
            Assert.That(viewport.ScaleX, Is.EqualTo(2.5).Within(1e-9));
            Assert.That(viewport.ScaleY, Is.EqualTo(400.0 / 300).Within(1e-9));
            Assert.That(viewport.OffsetX, Is.EqualTo(0).Within(1e-9));
            Assert.That(viewport.OffsetY, Is.EqualTo(0).Within(1e-9));
        });
    }

    [Test]
    public void Crop_CoversTheScreenAndOverflows()
    {
        var viewport = ViewportTransform.Create(Screen, Virtual, ScalingStrategy.Crop);

        Assert.Multiple(() =>
        {
            Assert.That(viewport.ScaleX, Is.EqualTo(2.5).Within(1e-9));
            Assert.That(viewport.ScaledHeight, Is.GreaterThan(Screen.Y));
            Assert.That(viewport.OffsetY, Is.LessThan(0));
        });
    }

    [TestCase(ScalingStrategy.Letterbox)]
    [TestCase(ScalingStrategy.Stretch)]
    [TestCase(ScalingStrategy.Crop)]
    public void CentreOfScreenMapsToCentreOfVirtualSpace(ScalingStrategy strategy)
    {
        var viewport = ViewportTransform.Create(Screen, Virtual, strategy);

        Assert.That(viewport.TryToVirtual(new Vec2(Screen.X / 2, Screen.Y / 2), out var centre), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(centre.X, Is.EqualTo(Virtual.X / 2).Within(1e-9));
            Assert.That(centre.Y, Is.EqualTo(Virtual.Y / 2).Within(1e-9));
        });
    }

    [Test]
    public void UnsetResolutionsProduceAnInvalidTransform()
    {
        Assert.That(ViewportTransform.Create(Vec2.Zero, Virtual, ScalingStrategy.Letterbox).IsValid, Is.False);
        Assert.That(ViewportTransform.Create(Screen, Vec2.Zero, ScalingStrategy.Letterbox).IsValid, Is.False);
    }

    [TestCase(ScalingStrategy.Letterbox)]
    [TestCase(ScalingStrategy.Stretch)]
    [TestCase(ScalingStrategy.Crop)]
    public void PointerInputAgreesWithTheRenderer(ScalingStrategy strategy)
    {
        var renderSystem = new RenderSystem(new RenderOptions
        {
            VirtualWidth = (float)Virtual.X,
            VirtualHeight = (float)Virtual.Y,
            ScalingStrategy = strategy
        });

        var inputManager = new InputManager
        {
            RealResolution = Screen,
            VirtualResolution = Virtual,
            ScalingStrategy = strategy
        };

        Vec2 received = default;
        inputManager.BindPointerAction(PointerEventType.Press, e => received = e.Position);

        var expected = renderSystem.ViewportFor(Screen);
        var probe = expected.ToReal(new Vec2(300, 200));

        inputManager.HandlePointerEvent(PointerEventType.Press, new PointerEvent(probe));
        inputManager.DispatchPointerEvents();

        Assert.Multiple(() =>
        {
            Assert.That(received.X, Is.EqualTo(300).Within(1e-6));
            Assert.That(received.Y, Is.EqualTo(200).Within(1e-6));
        });
    }
}
