using SkiaSharp;

namespace GameEngine.Core;

public enum SpriteBlend
{
    Normal,
    Additive
}

public readonly record struct SpriteInstance(
    Vec2 Position, SKRect Region, float Scale = 1, float Rotation = 0, SKColor? Color = null)
{
    internal SKRotationScaleMatrix Matrix
    {
        get
        {
            float angle = Rotation * MathF.PI / 180;
            float cosine = MathF.Cos(angle) * Scale;
            float sine = MathF.Sin(angle) * Scale;
            return new SKRotationScaleMatrix(cosine, sine,
                (float)Position.X - cosine * Region.Width / 2 + sine * Region.Height / 2,
                (float)Position.Y - sine * Region.Width / 2 - cosine * Region.Height / 2);
        }
    }
}

public sealed class SpriteBatch
{
    private readonly SpriteInstance[] _instances;

    internal SpriteBatch(SKImage texture, int capacity, int layer, SpriteBlend blend)
    {
        ArgumentNullException.ThrowIfNull(texture);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        if (!Enum.IsDefined(blend)) throw new ArgumentOutOfRangeException(nameof(blend));
        Texture = texture;
        Layer = layer;
        Blend = blend;
        _instances = new SpriteInstance[capacity];
    }

    public SKImage Texture { get; }
    public int Layer { get; set; }
    public SpriteBlend Blend { get; }
    public int Count { get; private set; }
    public int Capacity => _instances.Length;
    public Span<SpriteInstance> Instances => _instances.AsSpan(0, Count);

    public int Add(in SpriteInstance instance)
    {
        if (Count == Capacity) throw new InvalidOperationException("Sprite batch capacity reached.");
        _instances[Count] = instance;
        return Count++;
    }

    public void Clear() => Count = 0;

    internal void CopyTo(RenderSnapshot snapshot)
    {
        if (Count == 0) return;
        var batch = snapshot.BeginAtlasBatch(Texture, Blend, Layer);
        foreach (ref readonly var instance in Instances)
            batch.Add(instance);
    }
}
