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
        var manager = new EntityManager();
        manager.Update();
        var renderSystem = NewRenderSystem(smoothingSamples: 1);

        renderSystem.Update(manager, 1.0 / 60.0);

        Assert.That(renderSystem.Fps, Is.EqualTo(60).Within(0.01));
    }

    [Test]
    public void Fps_SmoothsOverTheConfiguredWindow()
    {
        var manager = new EntityManager();
        manager.Update();
        var renderSystem = NewRenderSystem(smoothingSamples: 4);

        renderSystem.Update(manager, 1.0 / 100.0);
        renderSystem.Update(manager, 1.0 / 100.0);
        renderSystem.Update(manager, 1.0 / 50.0);
        renderSystem.Update(manager, 1.0 / 50.0);

        Assert.That(renderSystem.Fps, Is.EqualTo(75).Within(0.01));
    }

    [Test]
    public void Fps_RunningTotalMatchesAPlainAverage()
    {
        var manager = new EntityManager();
        manager.Update();
        const int window = 30;
        var renderSystem = NewRenderSystem(smoothingSamples: window);

        var recent = new Queue<double>();
        var random = new Random(11);

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
