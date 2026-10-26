using GameEngine.Core.Systems;
using SkiaSharp;

namespace GameEngine.Core.Effects;

public static class SceneCapture
{
    public static byte[] Render(int width, int height, double time, Action<SKCanvas, EffectFrame> draw)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        if (!double.IsFinite(time) || time < 0) throw new ArgumentOutOfRangeException(nameof(time));
        using var surface = SKSurface.Create(new SKImageInfo(width, height)) ?? throw new InvalidOperationException("Cannot allocate capture surface.");
        surface.Canvas.Clear(SKColors.Transparent);
        draw(surface.Canvas, new EffectFrame((float)time, new SKSize(width, height), default));
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
    public static byte[] Render(Engine engine, int width, int height, double time, double step = 1.0 / 60)
    {
        if (!double.IsFinite(time) || time < 0 || !double.IsFinite(step) || step <= 0) throw new ArgumentOutOfRangeException(nameof(time));
        ArgumentNullException.ThrowIfNull(engine);
        if (time / step > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(time));
        engine.Tick(0);
        engine.NotifyFirstPresent();
        int ticks = (int)Math.Floor(time / step);
        for (int i = 0; i < ticks; i++) engine.Tick(step);
        double remainder = time - ticks * step;
        if (remainder > 1e-10) engine.Tick(remainder);
        if (engine.Fault is { } fault) throw new InvalidOperationException($"Capture failed: {fault}");
        return Render(width, height, time, (canvas, _) => engine.Systems.Get<RenderSystem>().DrawEntitiesToCanvas(canvas, engine.GetRenderSnapshot()));
    }
}
