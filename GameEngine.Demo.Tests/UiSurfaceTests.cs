using GameEngine.UI.Avalonia;
using SkiaSharp;
using Avalonia.Media;

namespace GameEngine.Demo.Tests;

[NonParallelizable]
public sealed class UiSurfaceTests
{
    [Test]
    public void ResizingPreservesCornerGeometryAndReusesTheTile()
    {
        using var surface = SKSurface.Create(new SKImageInfo(1000, 1000));
        var key = new SurfaceKey(GameTheme.NightCoast);
        SurfaceCache.Draw(surface.Canvas, key, new SKRect(0, 0, 120, 120));
        long generated = SurfaceCache.GeneratedCount;
        using var small = surface.Snapshot();
        using var smallBitmap = SKBitmap.FromImage(small);
        surface.Canvas.Clear(SKColors.Transparent);
        SurfaceCache.Draw(surface.Canvas, key, new SKRect(0, 0, 800, 800));
        using var large = surface.Snapshot(); using var largeBitmap = SKBitmap.FromImage(large);
        Assert.That(SurfaceCache.GeneratedCount, Is.EqualTo(generated));
        for (int y = 0; y < 24; y++)
            for (int x = 0; x < 24; x++) Assert.That(largeBitmap.GetPixel(x, y), Is.EqualTo(smallBitmap.GetPixel(x, y)));
        Assert.That(largeBitmap.GetPixel(400, 400).Alpha, Is.EqualTo(255));
    }

    [Test]
    public void SurfaceCacheBoundsNativeMemoryAcrossThemeChanges()
    {
        using var surface = SKSurface.Create(new SKImageInfo(100, 100));
        for (int i = 0; i < 100; i++)
            SurfaceCache.Draw(surface.Canvas, new SurfaceKey(GameTheme.NightCoast with { Name = $"Theme {i}" }), new SKRect(0, 0, 100, 100));
        Assert.That(SurfaceCache.Count, Is.EqualTo(SurfaceCache.Capacity));
        SurfaceCache.Clear(); Assert.That(SurfaceCache.Count, Is.Zero);
    }

    [Test]
    public void RenderedButtonTextBandsRetainReadableContrast()
    {
        using var surface = SKSurface.Create(new SKImageInfo(320, 64));
        foreach (var theme in new[] { GameTheme.NightCoast, GameTheme.Daybreak })
            foreach (var state in Enum.GetValues<SurfaceState>())
                foreach (var kind in new[] { SurfaceKind.Primary, SurfaceKind.Raised })
                {
                    surface.Canvas.Clear(SKColors.Transparent);
                    SurfaceCache.Draw(surface.Canvas, new SurfaceKey(theme, kind, state), new SKRect(0, 0, 320, 64));
                    using var image = surface.Snapshot(); using var bitmap = SKBitmap.FromImage(image);
                    var ink = state == SurfaceState.Disabled ? theme.Palette.Muted : kind == SurfaceKind.Primary ? theme.Palette.OnAccent : theme.Palette.Ink;
                    for (int y = 20; y <= 44; y += 4)
                    {
                        var pixel = bitmap.GetPixel(160, y);
                        var background = Color.FromRgb(pixel.Red, pixel.Green, pixel.Blue);
                        Assert.That(GameTheme.Contrast(ink, background), Is.GreaterThanOrEqualTo(4.5), $"{theme.Name}/{kind}/{state}/{y}");
                    }
                }
    }
}
