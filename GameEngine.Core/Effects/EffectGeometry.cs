using SkiaSharp;

namespace GameEngine.Core.Effects;

public static class EffectGeometry
{
    public static void Trail(SKCanvas canvas, ReadOnlySpan<SKPoint> points, float width, SKColor color)
    {
        if (points.Length < 2) return;
        using var paint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round };
        for (int i = 1; i < points.Length; i++)
        {
            float t = i / (float)(points.Length - 1);
            paint.StrokeWidth = Math.Max(.1f, width * t);
            paint.Color = color.WithAlpha((byte)(color.Alpha * t));
            canvas.DrawLine(points[i - 1], points[i], paint);
        }
    }
    public static void Beam(SKCanvas canvas, SKPoint from, SKPoint to, float width, SKColor color)
    {
        using var gradient = SKShader.CreateLinearGradient(from, to, [color.WithAlpha(0), color, SKColors.White], [0, .65f, 1], SKShaderTileMode.Clamp);
        using var paint = new SKPaint { IsAntialias = true, Shader = gradient, StrokeWidth = width, StrokeCap = SKStrokeCap.Round };
        canvas.DrawLine(from, to, paint);
    }
    public static void Ring(SKCanvas canvas, SKPoint center, float radius, float progress, SKColor color)
    {
        progress = Math.Clamp(progress, 0, 1);
        using var paint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 3 * (1 - progress) + .5f, Color = color.WithAlpha((byte)(color.Alpha * (1 - progress))) };
        canvas.DrawCircle(center, radius * (1 - (1 - progress) * (1 - progress)), paint);
    }
    public static SKPoint[] Lightning(SKPoint from, SKPoint to, int seed, int segments = 12, float spread = 12)
    {
        if (segments < 1 || segments > 128 || !float.IsFinite(spread) || spread < 0) throw new ArgumentOutOfRangeException(nameof(segments));
        var random = new Random(seed);
        var points = new SKPoint[segments + 1];
        float dx = to.X - from.X, dy = to.Y - from.Y;
        float length = MathF.Max(1, MathF.Sqrt(dx * dx + dy * dy));
        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            float offset = i == 0 || i == segments ? 0 : (random.NextSingle() * 2 - 1) * spread;
            points[i] = new(from.X + dx * t - dy / length * offset, from.Y + dy * t + dx / length * offset);
        }
        return points;
    }
    public static void Shatter(ParticleEmitter emitter, SKPoint center, float radius, int fragments, int seed)
    {
        if (fragments < 0 || fragments > 256 || !float.IsFinite(radius) || radius < 0) throw new ArgumentOutOfRangeException(nameof(fragments));
        var random = new Random(seed);
        for (int i = 0; i < fragments; i++)
        {
            float angle = i * MathF.Tau / Math.Max(1, fragments);
            float x = MathF.Cos(angle), y = MathF.Sin(angle), speed = 45 + random.NextSingle() * 110;
            emitter.Spawn(new ParticleSpawn(new Vec2(center.X + x * radius, center.Y + y * radius), new Vec2(x * speed, y * speed), .4f + random.NextSingle() * .5f, .25f + random.NextSingle() * .3f, Rotation: angle * 180 / MathF.PI));
        }
    }
}
