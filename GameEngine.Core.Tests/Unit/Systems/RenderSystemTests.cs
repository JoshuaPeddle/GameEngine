using GameEngine.Core.Systems;

namespace GameEngine.Core.Tests.Unit.Systems;

public class RenderSystemTests
{
    private static RenderSystem NewRenderSystem(int smoothingSamples)
    {
        var manager = new EntityManager();
        manager.Update();
        return new RenderSystem(manager, new RenderOptions
        {
            DrawFps = true,
            FpsSmoothingSamples = smoothingSamples
        });
    }

    [Test]
    public void Fps_IsDerivedFromSeconds()
    {
        // Arrange: GE-05 — the counter used to divide 1000 by a millisecond delta. With the
        // engine on seconds it must divide 1 by a second delta, or it reads 1000x low.
        var manager = new EntityManager();
        manager.Update();
        var renderSystem = NewRenderSystem(smoothingSamples: 1);

        // Act
        renderSystem.Update(manager, 1.0 / 60.0);

        // Assert
        Assert.That(renderSystem.Fps, Is.EqualTo(60).Within(0.01));
    }

    [Test]
    public void Fps_SmoothsOverTheConfiguredWindow()
    {
        // Arrange
        var manager = new EntityManager();
        manager.Update();
        var renderSystem = NewRenderSystem(smoothingSamples: 4);

        // Act: two frames at 100fps then two at 50fps.
        renderSystem.Update(manager, 1.0 / 100.0);
        renderSystem.Update(manager, 1.0 / 100.0);
        renderSystem.Update(manager, 1.0 / 50.0);
        renderSystem.Update(manager, 1.0 / 50.0);

        // Assert
        Assert.That(renderSystem.Fps, Is.EqualTo(75).Within(0.01));
    }

    [Test]
    public void Fps_RunningTotalMatchesAPlainAverage()
    {
        // Arrange: GE-13 replaced a per-frame re-sum with a running total. Guard against
        // the two drifting apart.
        var manager = new EntityManager();
        manager.Update();
        const int window = 30;
        var renderSystem = NewRenderSystem(smoothingSamples: window);

        var recent = new Queue<double>();
        var random = new Random(11);

        // Act / Assert
        for (int frame = 0; frame < 500; frame++)
        {
            double deltaSeconds = 1.0 / random.Next(20, 300);
            renderSystem.Update(manager, deltaSeconds);

            recent.Enqueue(1.0 / deltaSeconds);
            if (recent.Count > window) recent.Dequeue();

            Assert.That(renderSystem.Fps, Is.EqualTo(recent.Average()).Within(1e-9));
        }
    }

    [Test]
    public void Fps_IgnoresNonPositiveDeltas()
    {
        var manager = new EntityManager();
        manager.Update();
        var renderSystem = NewRenderSystem(smoothingSamples: 1);
        renderSystem.Update(manager, 1.0 / 60.0);

        Assert.DoesNotThrow(() => renderSystem.Update(manager, 0));
        Assert.That(renderSystem.Fps, Is.EqualTo(60).Within(0.01), "a zero delta must not disturb the reading");
    }
}
