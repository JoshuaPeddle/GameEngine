using SkiaSharp;

namespace GameEngine.Core;

public enum ParticleShape
{
    Cone,
    Radius
}

public sealed record ParticleOptions
{
    public required SKImage Texture { get; init; }
    public required SKRect Region { get; init; }
    public SpriteBlend Blend { get; init; }
    public int Layer { get; init; }
    public ParticleShape Shape { get; init; }
    public float Radius { get; init; }
    public float Direction { get; init; }
    public float Spread { get; init; } = 360;
    public float MinSpeed { get; init; }
    public float MaxSpeed { get; init; } = 100;
    public float Life { get; init; } = 1;
    public Vec2 Gravity { get; init; }
    public float Drag { get; init; }
    public SKColor StartColor { get; init; } = SKColors.White;
    public SKColor EndColor { get; init; } = SKColors.Transparent;
    public float StartScale { get; init; } = 1;
    public float EndScale { get; init; }
    public float Rotation { get; init; }
    public float AngularVelocity { get; init; }

    internal void Validate()
    {
        ArgumentNullException.ThrowIfNull(Texture);
        if (!Enum.IsDefined(Blend) || !Enum.IsDefined(Shape))
            throw new ArgumentException("Unknown particle blend or shape.");
        if (!float.IsFinite(Life) || Life <= 0 || !float.IsFinite(Radius) || Radius < 0
            || !float.IsFinite(MinSpeed) || !float.IsFinite(MaxSpeed) || MaxSpeed < MinSpeed
            || !float.IsFinite(Drag) || Drag < 0 || !float.IsFinite(Direction) || !float.IsFinite(Spread)
            || Spread < 0 || !float.IsFinite(StartScale) || !float.IsFinite(EndScale)
            || !float.IsFinite(Rotation) || !float.IsFinite(AngularVelocity)
            || !double.IsFinite(Gravity.X) || !double.IsFinite(Gravity.Y)
            || !float.IsFinite(Region.Left) || !float.IsFinite(Region.Top)
            || !float.IsFinite(Region.Right) || !float.IsFinite(Region.Bottom)
            || Region.Width <= 0 || Region.Height <= 0)
            throw new ArgumentException("Particle options must contain finite, valid ranges.");
    }
}

public readonly record struct ParticleSpawn(Vec2 Position, Vec2 Velocity, float Life,
    float Scale = 1, SKColor? Color = null, float Rotation = 0)
{
    internal void Validate()
    {
        if (!double.IsFinite(Position.X) || !double.IsFinite(Position.Y)
            || !double.IsFinite(Velocity.X) || !double.IsFinite(Velocity.Y)
            || !float.IsFinite(Life) || Life <= 0 || !float.IsFinite(Scale) || !float.IsFinite(Rotation))
            throw new ArgumentException("Particle spawn must contain finite values and a positive lifetime.");
    }
}

public sealed class ParticleEmitter
{
    private readonly ParticleStore _store;
    private readonly Random _random;
    private double _remainder;
    private double _rate;

    internal ParticleEmitter(ParticleStore store, ParticleOptions options, int seed)
    {
        options.Validate();
        _store = store;
        Options = options;
        _random = new Random(seed);
    }

    public ParticleOptions Options { get; }
    public Vec2 Position { get; set; }
    public double Rate
    {
        get => _rate;
        set
        {
            if (!double.IsFinite(value) || value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            _rate = value;
        }
    }

    public bool Enabled { get; set; } = true;

    public void Spawn(in ParticleSpawn particle)
    {
        particle.Validate();
        _store.Spawn(Options, particle);
    }

    public void Burst(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (!double.IsFinite(Position.X) || !double.IsFinite(Position.Y))
            throw new InvalidOperationException("Emitter position must be finite.");
        for (int i = 0; i < count; i++)
        {
            float angle = (Options.Direction + (_random.NextSingle() - 0.5f) * Options.Spread) * MathF.PI / 180;
            float speed = Options.MinSpeed + _random.NextSingle() * (Options.MaxSpeed - Options.MinSpeed);
            Vec2 direction = new(MathF.Cos(angle), MathF.Sin(angle));
            Vec2 position = Position;
            if (Options.Shape == ParticleShape.Radius)
            {
                float radiusAngle = _random.NextSingle() * MathF.Tau;
                float radius = MathF.Sqrt(_random.NextSingle()) * Options.Radius;
                position += new Vec2(MathF.Cos(radiusAngle) * radius, MathF.Sin(radiusAngle) * radius);
            }
            _store.Spawn(Options, position, direction * speed);
        }
    }

    internal void Update(double deltaSeconds)
    {
        if (!Enabled) return;
        _remainder += Rate * deltaSeconds;
        int count = (int)Math.Min(int.MaxValue, Math.Floor(_remainder));
        _remainder -= count;
        Burst(count);
    }
}
