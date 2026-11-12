using GameEngine.Core.Effects;
using SkiaSharp;

namespace GameEngine.Core.Tests.Unit;

public class StaticBackgroundTests
{
    [Test]
    public void CacheRetainsPixelsUntilInvalidatedAndRebuildsForCameraResolutionAndScale()
    {
        int draws = 0;
        var color = SKColors.Red;
        using var paint = new SKPaint();
        using var effects = new RenderEffects
        {
            CacheBackground = true,
            BackgroundLayers = [new((canvas, _) => { draws++; paint.Color = color; canvas.DrawRect(0, 0, 64, 64, paint); })]
        };
        using var bitmap = new SKBitmap(64, 64);
        using var canvas = new SKCanvas(bitmap);
        var frame = new EffectFrame(0, new(64, 64), default);
        effects.DrawBackground(canvas, frame);
        color = SKColors.Blue;
        effects.DrawBackground(canvas, frame with { Time = 10 });
        Assert.That(draws, Is.EqualTo(1));
        Assert.That(bitmap.GetPixel(32, 32), Is.EqualTo(SKColors.Red));
        effects.InvalidateBackground();
        effects.DrawBackground(canvas, frame);
        Assert.That(draws, Is.EqualTo(2));
        Assert.That(bitmap.GetPixel(32, 32), Is.EqualTo(SKColors.Blue));
        effects.DrawBackground(canvas, frame with { Camera = new(1, 0) });
        Assert.That(draws, Is.EqualTo(3));
        effects.DrawBackground(canvas, frame with { Resolution = new(32, 32) });
        Assert.That(draws, Is.EqualTo(4));
        effects.BackgroundScale = .5f;
        effects.DrawBackground(canvas, frame with { Resolution = new(32, 32) });
        Assert.That(draws, Is.EqualTo(5));
        effects.CacheBackground = false;
        effects.DrawBackground(canvas, frame);
        effects.DrawBackground(canvas, frame);
        Assert.That(draws, Is.EqualTo(7));
        effects.CacheBackground = true;
        effects.DrawBackground(canvas, frame);
        Assert.That(draws, Is.EqualTo(8));
    }

    [Test]
    public void CachedAndDirectBackgroundsHaveIdenticalTransparentComposition()
    {
        using var paint = new SKPaint { Color = SKColors.Lime.WithAlpha(100) };
        using var effects = new RenderEffects { BackgroundLayers = [new((c, _) => c.DrawRect(8, 8, 16, 16, paint))] };
        using var bitmap = new SKBitmap(32, 32);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Navy);
        effects.DrawBackground(canvas, new(0, new(32, 32), default));
        var expected = bitmap.GetPixel(16, 16);
        effects.CacheBackground = true;
        canvas.Clear(SKColors.Navy);
        effects.DrawBackground(canvas, new(0, new(32, 32), default));
        Assert.That(bitmap.GetPixel(16, 16), Is.EqualTo(expected));
        Assert.That(bitmap.GetPixel(0, 0), Is.EqualTo(SKColors.Navy));
    }
}
