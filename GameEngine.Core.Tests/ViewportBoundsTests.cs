using GameEngine.Core.Effects;
using GameEngine.Core.Systems;
using SkiaSharp;

namespace GameEngine.Core.Tests;

// A host's damage clip is not its viewport. These render the same frame into a full canvas and
// into partial clips of it, and require identical layout and identical pixels inside the clip.
public class ViewportBoundsTests
{
    private const int Width = 108, Height = 234;

    private sealed class Probe : IDisposable
    {
        public SKMatrix Environment, World;
        public SKSize Resolution;
        public SKRectI EnvironmentTarget;
        public readonly RenderSystem Renderer;
        private readonly RuntimeShader background;
        private readonly RuntimeShader? grade;
        private readonly RenderEffects effects;
        private readonly SKPaint band = new() { Color = new SKColor(40, 200, 120) };
        private readonly SKPaint ring = new() { Color = new SKColor(250, 220, 90) };

        public Probe(int cap = 0, bool postProcess = false)
        {
            background = new RuntimeShader("uniform float2 resolution; half4 main(float2 p) { return half4(p.x/resolution.x, p.y/resolution.y, .3, 1); }", SKColors.Navy);
            grade = postProcess ? new RuntimeShader("uniform shader content; uniform float2 resolution; half4 main(float2 p) { half4 c=content.eval(p); return half4(c.b, c.g, p.y/resolution.y, c.a); }", SKColors.Navy) : null;
            effects = new RenderEffects
            {
                Background = background,
                PostProcess = grade,
                BackgroundLayers = [new((canvas, frame) =>
                {
                    Environment = canvas.TotalMatrix; Resolution = frame.Resolution; EnvironmentTarget = canvas.DeviceClipBounds;
                    canvas.DrawRect(0, 30, frame.Resolution.Width, 30, band);
                })],
                Layers = [new((canvas, _) => { World = canvas.TotalMatrix; canvas.DrawCircle(36, 90, 20, ring); })]
            };
            Renderer = new RenderSystem(new RenderOptions { VirtualWidth = 72, VirtualHeight = 152, MaxRenderWidth = cap, ScalingStrategy = ScalingStrategy.Stretch, Effects = effects });
        }

        public void Dispose()
        {
            Renderer.Dispose(); effects.Dispose(); background.Dispose(); grade?.Dispose(); band.Dispose(); ring.Dispose();
        }
    }

    private static SKBitmap Render(Probe probe, int width, int height, SKRect viewport, SKRect? clip = null, Action<SKCanvas>? transform = null)
    {
        var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Magenta);
        transform?.Invoke(canvas);
        if (clip is { } rect) canvas.ClipRect(rect);
        int saved = canvas.SaveCount;
        probe.Renderer.DrawEntitiesToCanvas(canvas, RenderSnapshot.Empty, viewport);
        Assert.That(canvas.SaveCount, Is.EqualTo(saved), "The canvas state is restored");
        return bitmap;
    }

    // Skia may choose a different anti-aliasing path for a shape that crosses a clip edge, so the
    // probe draws aliased shapes and only resampled comparisons need a tolerance.
    private static void AssertSamePixels(SKBitmap expected, SKBitmap actual, SKRectI region, int dx = 0, int dy = 0, int tolerance = 0)
    {
        for (int y = region.Top + 1; y < region.Bottom - 1; y++)
        for (int x = region.Left + 1; x < region.Right - 1; x++)
        {
            SKColor a = expected.GetPixel(x, y), b = actual.GetPixel(x + dx, y + dy);
            int delta = Math.Max(Math.Max(Math.Abs(a.Red - b.Red), Math.Abs(a.Green - b.Green)), Math.Max(Math.Abs(a.Blue - b.Blue), Math.Abs(a.Alpha - b.Alpha)));
            if (delta > tolerance) Assert.Fail($"Pixel ({x},{y}) differs: {a} vs {b}");
        }
    }

    private static void AssertOutsideUntouched(SKBitmap actual, SKRectI clip)
    {
        for (int y = 0; y < actual.Height; y += 3)
        for (int x = 0; x < actual.Width; x += 3)
            if (!clip.Contains(x, y)) Assert.That(actual.GetPixel(x, y), Is.EqualTo(SKColors.Magenta), $"Pixel ({x},{y}) outside the clip");
    }

    private static readonly SKRectI[] Clips =
    [
        new(20, 80, Width - 20, Height - 120),
        new(0, 60, Width, Height),
        new(0, 0, Width, Height - 90),
        new(30, 0, Width, Height),
        new(0, 0, Width - 44, Height),
        new(51, 140, 70, 190),
    ];

    private static IEnumerable<TestCaseData> ClipCases()
    {
        foreach (int cap in new[] { 0, 72 })
        foreach (bool post in new[] { false, true })
        foreach (var clip in Clips)
            yield return new TestCaseData(cap, post, clip).SetName($"Clip {clip} cap {cap} post {post}");
    }

    [TestCaseSource(nameof(ClipCases))]
    public void PartialClipKeepsTheFullCanvasLayout(int cap, bool post, SKRectI clip)
    {
        using var probe = new Probe(cap, post);
        var viewport = new SKRect(0, 0, Width, Height);
        using var full = Render(probe, Width, Height, viewport);
        var (environment, world, resolution, target) = (probe.Environment, probe.World, probe.Resolution, probe.EnvironmentTarget);
        using var partial = Render(probe, Width, Height, viewport, clip);
        Assert.Multiple(() =>
        {
            Assert.That(probe.Environment, Is.EqualTo(environment), "Environment fit");
            Assert.That(probe.World, Is.EqualTo(world), "World fit");
            Assert.That(probe.Resolution, Is.EqualTo(resolution), "Virtual resolution");
            if (cap > 0 || post) Assert.That(probe.EnvironmentTarget.Size, Is.EqualTo(target.Size), "Scene surface size");
        });
        AssertSamePixels(full, partial, clip);
        AssertOutsideUntouched(partial, clip);
    }

    [Test]
    public void EnvironmentFitIsTheSameForFullAndPartialClips()
    {
        using var probe = new Probe(cap: 720);
        foreach (var (width, height) in new[] { (1080, 2340), (1440, 3040) })
        {
            var viewport = new SKRect(0, 0, width, height);
            using var full = Render(probe, width, height, viewport);
            var environment = probe.Environment;
            using var partial = Render(probe, width, height, viewport, new SKRect(20, 80, width - 20, height - 120));
            Assert.That(probe.Environment, Is.EqualTo(environment), $"{width}x{height}");
            Assert.That(probe.Environment.ScaleY, Is.EqualTo(720f * height / width / 152).Within(1e-3), "Stretched to the whole viewport, not the clip");
        }
    }

    [Test]
    public void ResolutionCapIsSizedFromTheViewportNotTheClip()
    {
        using var probe = new Probe(cap: 54);
        var viewport = new SKRect(0, 0, Width, Height);
        foreach (var clip in Clips)
        {
            using var _ = Render(probe, Width, Height, viewport, clip);
            Assert.That(probe.EnvironmentTarget.Size, Is.EqualTo(new SKSizeI(54, 117)), $"Clip {clip}");
        }
        using var uncapped = new Probe(cap: 500);
        using var __ = Render(uncapped, Width, Height, viewport, Clips[0]);
        Assert.That(uncapped.EnvironmentTarget, Is.EqualTo(Clips[0]), "Below the cap the scene draws straight into the clipped canvas");
    }

    [TestCase(0, false)]
    [TestCase(120, false)]
    [TestCase(0, true)]
    [TestCase(120, true)]
    public void DpiAndTranslatedCanvasesMapTheLogicalViewportToDevicePixels(int cap, bool post)
    {
        using var probe = new Probe(cap, post);
        const int scale = 3, dx = 13, dy = 21;
        var logical = new SKRect(0, 0, Width, Height);
        using var device = Render(probe, Width * scale, Height * scale, new SKRect(0, 0, Width * scale, Height * scale));
        var (deviceTarget, deviceEnvironment) = (probe.EnvironmentTarget.Size, probe.Environment);
        using var full = Render(probe, Width * scale + dx, Height * scale + dy, logical, null, c => { c.Translate(dx, dy); c.Scale(scale); });
        var (environment, world) = (probe.Environment, probe.World);
        bool surface = cap > 0 || post;
        if (surface) Assert.That(probe.EnvironmentTarget.Size, Is.EqualTo(deviceTarget), "Scene surface sized in device pixels");
        var clip = new SKRect(4, 20, Width - 2, Height - 10);
        using var partial = Render(probe, Width * scale + dx, Height * scale + dy, logical, clip, c => { c.Translate(dx, dy); c.Scale(scale); });
        Assert.Multiple(() =>
        {
            Assert.That(probe.Environment, Is.EqualTo(environment), "Environment fit");
            Assert.That(probe.World, Is.EqualTo(world), "World fit");
            if (surface) Assert.That(probe.EnvironmentTarget.Size, Is.EqualTo(deviceTarget), "Scene surface size");
        });
        var deviceClip = new SKRectI(4 * scale + dx, 20 * scale + dy, (Width - 2) * scale + dx, (Height - 10) * scale + dy);
        AssertSamePixels(full, partial, deviceClip);
        var expected = surface ? deviceEnvironment : SKMatrix.CreateTranslation(dx, dy).PreConcat(deviceEnvironment);
        Assert.That(environment.Values.Zip(expected.Values).All(v => Math.Abs(v.First - v.Second) < 1e-3), Is.True,
            "A logical viewport on a DPI canvas lays out like the same viewport in device pixels");
        AssertSamePixels(device, full, new SKRectI(0, 0, Width * scale, Height * scale), dx, dy, tolerance: 16);
    }

    [Test]
    public void ViewportWithAnOffsetOriginFillsOnlyThatRectangle()
    {
        using var probe = new Probe();
        using var reference = Render(probe, Width, Height, new SKRect(0, 0, Width, Height));
        var (environment, world) = (probe.Environment, probe.World);
        using var offset = Render(probe, Width + 30, Height + 50, new SKRect(30, 50, Width + 30, Height + 50));
        Assert.Multiple(() =>
        {
            Assert.That(probe.Environment, Is.EqualTo(SKMatrix.CreateTranslation(30, 50).PreConcat(environment)));
            Assert.That(probe.World, Is.EqualTo(SKMatrix.CreateTranslation(30, 50).PreConcat(world)));
            Assert.That(offset.GetPixel(10, 10), Is.EqualTo(SKColors.Magenta));
            Assert.That(offset.GetPixel(Width + 20, 40), Is.EqualTo(SKColors.Magenta));
            Assert.That(offset.GetPixel(30 + 5, 50 + 70), Is.EqualTo(reference.GetPixel(5, 70)), "Horizon band");
        });
    }

    [Test]
    public void OverloadWithoutBoundsStillFillsTheWholeCanvas()
    {
        foreach (bool post in new[] { false, true })
        {
            using var probe = new Probe(cap: 72, post);
            using var explicitBounds = Render(probe, Width, Height, new SKRect(0, 0, Width, Height));
            using var legacy = new SKBitmap(Width, Height);
            using (var canvas = new SKCanvas(legacy)) probe.Renderer.DrawEntitiesToCanvas(canvas, RenderSnapshot.Empty);
            AssertSamePixels(explicitBounds, legacy, new SKRectI(0, 0, Width, Height));
        }
    }

    [Test]
    public void EmptyViewportDrawsNothingAndNonFiniteIsRejected()
    {
        using var probe = new Probe();
        using var bitmap = Render(probe, 16, 16, SKRect.Empty);
        Assert.That(bitmap.GetPixel(8, 8), Is.EqualTo(SKColors.Magenta));
        using var canvas = new SKCanvas(bitmap);
        Assert.Throws<ArgumentOutOfRangeException>(() => probe.Renderer.DrawEntitiesToCanvas(canvas, RenderSnapshot.Empty, new SKRect(0, 0, float.NaN, 4)));
    }
}
