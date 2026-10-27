using SkiaSharp;

namespace GameEngine.Core.Effects;

public readonly record struct EffectFrame(float Time, SKSize Resolution, SKPoint Camera);

public sealed class RuntimeShader : IDisposable
{
    private readonly SKRuntimeEffect? effect;
    public string? Error { get; }
    public SKColor Fallback { get; }
    public RuntimeShader(string source, SKColor fallback, Action<string>? report = null)
    {
        Fallback = fallback;
        effect = SKRuntimeEffect.CreateShader(source, out var error);
        if (effect is null)
        {
            Error = $"SkSL shader compilation failed: {error}";
            report?.Invoke(Error);
        }
    }
    public SKShader? Create(in EffectFrame frame, SKShader? content = null)
    {
        if (effect is null) return null;
        var uniforms = new SKRuntimeEffectUniforms(effect);
        foreach (var uniform in effect.Uniforms)
        {
            if (uniform == "time") uniforms["time"] = frame.Time;
            else if (uniform == "resolution") uniforms["resolution"] = new[] { frame.Resolution.Width, frame.Resolution.Height };
            else if (uniform == "camera") uniforms["camera"] = new[] { frame.Camera.X, frame.Camera.Y };
        }
        var children = new SKRuntimeEffectChildren(effect);
        if (content is not null) children["content"] = content;
        return effect.ToShader(uniforms, children);
    }
    public void Dispose() => effect?.Dispose();
}
