namespace GameEngine.Core.Systems;

public sealed record SoundPlaybackOptions
{
    public int MaxConcurrentVoices { get; }
    public double MinimumRetriggerInterval { get; }
    public int Priority { get; }
    public int MaxEventsPerFrame { get; }

    public SoundPlaybackOptions(int maxConcurrentVoices = 2, double minimumRetriggerInterval = 0.05,
        int priority = 0, int maxEventsPerFrame = 1)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxConcurrentVoices, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxEventsPerFrame, 1);
        if (!double.IsFinite(minimumRetriggerInterval) || minimumRetriggerInterval < 0)
            throw new ArgumentOutOfRangeException(nameof(minimumRetriggerInterval));
        MaxConcurrentVoices = maxConcurrentVoices;
        MinimumRetriggerInterval = minimumRetriggerInterval;
        Priority = priority;
        MaxEventsPerFrame = maxEventsPerFrame;
    }
}
