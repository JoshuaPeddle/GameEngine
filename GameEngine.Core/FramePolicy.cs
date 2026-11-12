namespace GameEngine.Core;

public enum FrameActivity { Idle, Ambient, Active }

public interface IFrameActivitySource
{
    FrameActivity FrameActivity { get; }
}

public sealed class FramePolicy
{
    private readonly Action changed;
    private FrameActivity? activity;
    private int ambientFrameRate = 15;
    private int? maxFrameRate;
    private double activeGraceSeconds = .2;
    private double idleGraceSeconds = .8;

    internal FramePolicy(Action changed) => this.changed = changed;

    public FrameActivity? Activity
    {
        get => activity;
        set
        {
            if (value.HasValue && !Enum.IsDefined(value.Value)) throw new ArgumentOutOfRangeException(nameof(value));
            if (activity == value) return;
            activity = value;
            changed();
        }
    }

    public int AmbientFrameRate
    {
        get => ambientFrameRate;
        set { ValidateRate(value); ambientFrameRate = value; changed(); }
    }

    public int? MaxFrameRate
    {
        get => maxFrameRate;
        set { if (value.HasValue) ValidateRate(value.Value); maxFrameRate = value; changed(); }
    }

    public double ActiveGraceSeconds
    {
        get => activeGraceSeconds;
        set { ValidateSeconds(value); activeGraceSeconds = value; changed(); }
    }

    public double IdleGraceSeconds
    {
        get => idleGraceSeconds;
        set { ValidateSeconds(value); idleGraceSeconds = value; changed(); }
    }

    private static void ValidateRate(int value) => ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
    private static void ValidateSeconds(double value)
    {
        if (!double.IsFinite(value) || value < 0) throw new ArgumentOutOfRangeException(nameof(value));
    }
}
