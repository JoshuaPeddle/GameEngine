using SkiaSharp;

namespace GameEngine.Core.Effects;

public readonly record struct CameraEffectState(SKPoint Offset, float Zoom);

public sealed class CameraEffects
{
    private double time;
    private double shakeEnd, shakeDuration;
    private float shakeAmplitude;
    private double punchEnd, punchDuration;
    private float punch;
    private double stopRemaining;
    private double scale = 1;
    public CameraEffectState State { get; private set; } = new(default, 1);
    public void Shake(float amplitude, double duration)
    {
        Validate(duration); Validate(amplitude);
        shakeAmplitude = amplitude; shakeDuration = duration; shakeEnd = time + duration;
    }
    public void ZoomPunch(float amount, double duration)
    {
        Validate(duration); Validate(amount);
        punch = amount; punchDuration = duration; punchEnd = time + duration;
    }
    public void HitStop(double duration, double timeScale = 0)
    {
        Validate(duration);
        if (!double.IsFinite(timeScale) || timeScale < 0 || timeScale > 1) throw new ArgumentOutOfRangeException(nameof(timeScale));
        stopRemaining = duration; scale = timeScale;
    }
    public double Advance(double realSeconds)
    {
        Validate(realSeconds);
        double stopped = Math.Min(realSeconds, stopRemaining);
        stopRemaining -= stopped;
        time += realSeconds;
        float decay = shakeDuration > 0 ? (float)Math.Clamp((shakeEnd - time) / shakeDuration, 0, 1) : 0;
        float zoomDecay = punchDuration > 0 ? (float)Math.Clamp((punchEnd - time) / punchDuration, 0, 1) : 0;
        State = new(new SKPoint(shakeAmplitude * decay * MathF.Sin((float)time * 93), shakeAmplitude * decay * MathF.Sin((float)time * 117)), 1 + punch * zoomDecay * zoomDecay);
        return realSeconds - stopped + stopped * scale;
    }
    private static void Validate(double value)
    {
        if (!double.IsFinite(value) || value < 0) throw new ArgumentOutOfRangeException(nameof(value));
    }
}
