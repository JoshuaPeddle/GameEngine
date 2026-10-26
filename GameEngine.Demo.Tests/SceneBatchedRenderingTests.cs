using GameEngine.Core.Systems;
using SkiaSharp;

namespace GameEngine.Demo.Tests;

public class SceneBatchedRenderingTests
{
    [Test]
    public void SwarmAndSparksStayBoundedAndMoveThroughTheRealTick()
    {
        var harness = SceneHarness.Load(new SceneBatchedRendering());
        using var engine = harness.Engine;
        var initial = engine.GetRenderSnapshot().AtlasBatches[0].Transforms[0];
        harness.Run(300);
        var snapshot = engine.GetRenderSnapshot();
        Assert.That(engine.Fault, Is.Null);
        Assert.That(engine.EntityManager.GetEntities().Count, Is.EqualTo(2));
        Assert.That(snapshot.AtlasBatches.Length, Is.EqualTo(3));
        Assert.That(snapshot.AtlasBatches[1].Count, Is.EqualTo(500));
        Assert.That(snapshot.AtlasBatches[0].Transforms[0], Is.EqualTo(initial));
        Assert.That(snapshot.AtlasBatches[1].Transforms[0].TX, Is.Not.EqualTo(476));
        Assert.That(engine.Systems.Get<ParticleSystem>().Store.Count, Is.InRange(1700, 1900));
        using var surface = SKSurface.Create(new SKImageInfo(960, 640));
        engine.Systems.Get<RenderSystem>().DrawEntitiesToCanvas(surface.Canvas, snapshot);
        using var image = surface.Snapshot();
        using var bitmap = SKBitmap.FromImage(image);
        int orangePixels = 0;
        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
            {
                var color = bitmap.GetPixel(x, y);
                if (color.Red > color.Blue + 20) orangePixels++;
            }
        Assert.That(orangePixels, Is.GreaterThan(500));
    }
}
