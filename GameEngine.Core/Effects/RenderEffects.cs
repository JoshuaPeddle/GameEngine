using SkiaSharp;

namespace GameEngine.Core.Effects;

public enum BloomQuality { Off = 0, Low = 1, High = 2, Medium = 3 }

public sealed record EffectLayer(Action<SKCanvas, EffectFrame> Draw, bool Bloom = false, RuntimeShader? Shader = null);

public sealed class RenderEffects : IDisposable
{
    public RuntimeShader? Background { get; init; }
    public EffectLayer[] BackgroundLayers { get; init; } = [];
    private float backgroundScale = 1;
    public float BackgroundScale
    {
        get => backgroundScale;
        set
        {
            if (!float.IsFinite(value) || value < 0 || value > 1) throw new ArgumentOutOfRangeException(nameof(value));
            backgroundScale = value;
        }
    }
    private SKSurface? backgroundSurface;
    public RuntimeShader? Overlay { get; init; }
    public RuntimeShader? PostProcess { get; init; }
    public EffectLayer[] Layers { get; init; } = [];
    public BloomQuality Bloom { get; set; }
    public float BloomStrength { get; set; } = .8f;
    private SKSurface? glow;
    private SKSurface? layerSurface;
    private SKSurface? blurredSurface;
    private SKSurface? brightLayerSurface;
    private readonly SKPaint paint = new() { IsAntialias = true };
    private readonly SKPaint composite = new() { BlendMode = SKBlendMode.Plus };
    private readonly SKPaint blur = new();
    private SKImageFilter? filter;
    private BloomQuality allocatedQuality;

    public void DrawBackground(SKCanvas canvas, in EffectFrame frame)
    {
        float scale = BackgroundScale;
        if (Background is not null)
        {
            if (scale == 0)
            {
                paint.Color = Background.Fallback;
                canvas.DrawRect(SKRect.Create(frame.Resolution), paint);
            }
            else if (scale == 1) DrawShader(canvas, Background, frame);
            else
            {
                int width = Math.Max(1, (int)(frame.Resolution.Width * scale));
                int height = Math.Max(1, (int)(frame.Resolution.Height * scale));
                Ensure(ref backgroundSurface, width, height, canvas.Context);
                var target = backgroundSurface!.Canvas;
                target.Clear(SKColors.Transparent);
                int saved = target.Save();
                try
                {
                    target.Scale(width / frame.Resolution.Width, height / frame.Resolution.Height);
                    DrawShader(target, Background, frame);
                }
                finally { target.RestoreToCount(saved); }
                using var image = backgroundSurface.Snapshot();
                canvas.DrawImage(image, SKRect.Create(frame.Resolution), new SKSamplingOptions(SKFilterMode.Linear));
            }
        }
        foreach (var layer in BackgroundLayers) DrawLayer(layer, canvas, frame);
    }
    private void DrawShader(SKCanvas canvas, RuntimeShader shader, in EffectFrame frame)
    {
        using var created = shader.Create(frame);
        paint.Shader = created;
        paint.Color = created is null ? shader.Fallback : SKColors.White;
        canvas.DrawRect(SKRect.Create(frame.Resolution), paint);
        paint.Shader = null;
    }
    public void DrawLayers(SKCanvas canvas, in EffectFrame frame, Action<SKCanvas>? drawBright = null)
    {
        var quality = Bloom;
        int width = Math.Max(1, (int)frame.Resolution.Width), height = Math.Max(1, (int)frame.Resolution.Height);
        if (quality != BloomQuality.Off)
        {
            int divisor = quality switch { BloomQuality.Low => 4, BloomQuality.Medium => 3, _ => 2 };
            Ensure(ref glow, Math.Max(1, width / divisor), Math.Max(1, height / divisor), canvas.Context);
            if (filter is null || allocatedQuality != quality)
            {
                filter?.Dispose();
                float sigma = quality switch { BloomQuality.Low => 3, BloomQuality.Medium => 4, _ => 5 };
                filter = SKImageFilter.CreateBlur(sigma, sigma);
                blur.ImageFilter = filter;
                allocatedQuality = quality;
            }
            glow!.Canvas.Clear(SKColors.Transparent);
        }
        foreach (var layer in Layers)
        {
            if (layer.Bloom && quality != BloomQuality.Off)
            {
                float scale = quality switch { BloomQuality.Low => .5f, BloomQuality.Medium => .75f, _ => 1 };
                Ensure(ref brightLayerSurface, Math.Max(1, (int)(width * scale)), Math.Max(1, (int)(height * scale)), canvas.Context);
                var target = brightLayerSurface!.Canvas;
                target.Clear(SKColors.Transparent);
                int saved = target.Save();
                try
                {
                    target.Scale((float)target.DeviceClipBounds.Width / width, (float)target.DeviceClipBounds.Height / height);
                    DrawComposedLayer(layer, target, frame, width, height);
                }
                finally { target.RestoreToCount(saved); }
                using var brightLayer = brightLayerSurface.Snapshot();
                canvas.DrawImage(brightLayer, SKRect.Create(frame.Resolution), new SKSamplingOptions(SKFilterMode.Linear));
                glow!.Canvas.DrawImage(brightLayer, new SKRect(0, 0, glow.Canvas.DeviceClipBounds.Width, glow.Canvas.DeviceClipBounds.Height), new SKSamplingOptions(SKFilterMode.Linear));
            }
            else DrawComposedLayer(layer, canvas, frame, width, height);
        }
        if (quality != BloomQuality.Off)
        {
            if (drawBright is not null)
            {
                int saved = glow!.Canvas.Save();
                try
                {
                    glow.Canvas.Scale((float)glow.Canvas.DeviceClipBounds.Width / width, (float)glow.Canvas.DeviceClipBounds.Height / height);
                    drawBright(glow.Canvas);
                }
                finally { glow.Canvas.RestoreToCount(saved); }
            }
            using var bright = glow!.Snapshot();
            Ensure(ref blurredSurface, bright.Width, bright.Height, canvas.Context);
            blurredSurface!.Canvas.Clear(SKColors.Transparent);
            blurredSurface.Canvas.DrawImage(bright, 0, 0, new SKSamplingOptions(SKFilterMode.Linear), blur);
            using var image = blurredSurface.Snapshot();
            composite.Color = SKColors.White.WithAlpha((byte)(Math.Clamp(BloomStrength, 0, 1) * 255));
            canvas.DrawImage(image, SKRect.Create(frame.Resolution), new SKSamplingOptions(SKFilterMode.Linear), composite);
        }
        if (Overlay is not null) DrawShader(canvas, Overlay, frame);
    }
    private void DrawComposedLayer(EffectLayer layer, SKCanvas canvas, in EffectFrame frame, int width, int height)
    {
        if (layer.Shader is null) { DrawLayer(layer, canvas, frame); return; }
        Ensure(ref layerSurface, width, height, canvas.Context);
        layerSurface!.Canvas.Clear(SKColors.Transparent);
        DrawLayer(layer, layerSurface.Canvas, frame);
        using var image = layerSurface.Snapshot();
        using var content = image.ToShader();
        using var shader = layer.Shader.Create(frame, content);
        if (shader is null) canvas.DrawImage(image, 0, 0, new SKSamplingOptions(SKFilterMode.Linear));
        else
        {
            paint.Color = SKColors.White;
            paint.Shader = shader;
            canvas.DrawRect(SKRect.Create(frame.Resolution), paint);
            paint.Shader = null;
        }
    }
    private static void DrawLayer(EffectLayer layer, SKCanvas canvas, in EffectFrame frame)
    {
        int saved = canvas.Save();
        try { layer.Draw(canvas, frame); }
        finally { canvas.RestoreToCount(saved); }
    }
    private static void Ensure(ref SKSurface? surface, int width, int height, GRRecordingContext? context)
    {
        if (surface is not null && surface.Canvas.DeviceClipBounds.Width == width && surface.Canvas.DeviceClipBounds.Height == height && surface.Context?.Handle == context?.Handle) return;
        surface?.Dispose();
        surface = (context is null ? SKSurface.Create(new SKImageInfo(width, height)) : SKSurface.Create(context, true, new SKImageInfo(width, height))) ?? throw new InvalidOperationException("Cannot allocate effect surface.");
    }
    public void Dispose()
    {
        backgroundSurface?.Dispose(); glow?.Dispose(); blurredSurface?.Dispose(); brightLayerSurface?.Dispose(); layerSurface?.Dispose(); filter?.Dispose(); paint.Dispose(); composite.Dispose(); blur.Dispose();
    }
}
