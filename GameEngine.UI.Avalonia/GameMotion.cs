using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace GameEngine.UI.Avalonia;

public sealed class GameTween
{
    private double start, target, elapsed, duration;
    private GameEasing easing;
    public double Value { get; private set; }
    public bool IsActive { get; private set; }

    public void Start(double from, double to, double seconds, bool reducedMotion = false, GameEasing curve = GameEasing.OutCubic)
    {
        if (!double.IsFinite(from) || !double.IsFinite(to) || !double.IsFinite(seconds) || seconds < 0)
            throw new ArgumentOutOfRangeException(nameof(seconds));
        start = from; target = to; duration = seconds; elapsed = 0; easing = curve;
        IsActive = !reducedMotion && seconds > 0 && from != to;
        Value = IsActive ? from : to;
    }

    public bool Advance(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        if (!IsActive) return false;
        elapsed += seconds;
        double t = Math.Min(1, elapsed / duration);
        double eased = easing switch
        {
            GameEasing.OutCubic => 1 - Math.Pow(1 - t, 3),
            GameEasing.InOutCubic => t < .5 ? 4 * t * t * t : 1 - Math.Pow(-2 * t + 2, 3) / 2,
            _ => t
        };
        Value = start * (1 - eased) + target * eased;
        IsActive = t < 1;
        return true;
    }

    public void Finish() { Value = target; IsActive = false; }
}

internal sealed class MotionDriver
{
    private static int activeTimers;
    private static long ticks;
    internal static int ActiveTimers => Volatile.Read(ref activeTimers);
    internal static long Ticks => Interlocked.Read(ref ticks);
    private readonly Control owner;
    private readonly Action<double> apply;
    private readonly DispatcherTimer timer;
    private readonly GameTween tween = new();
    private long last;
    public bool IsActive => tween.IsActive;
    public double Value => tween.Value;
    public Action? Completed { get; set; }

    public MotionDriver(Control owner, Action<double> apply)
    {
        this.owner = owner; this.apply = apply;
        timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1d / 60) };
        timer.Tick += (_, _) =>
        {
            Interlocked.Increment(ref ticks);
            long now = Stopwatch.GetTimestamp();
            tween.Advance(Stopwatch.GetElapsedTime(last, now).TotalSeconds);
            last = now;
            apply(tween.Value);
            if (!tween.IsActive) { StopTimer(); Completed?.Invoke(); }
        };
        owner.DetachedFromVisualTree += (_, _) => Finish();
        owner.PropertyChanged += (_, e) =>
        {
            if (e.Property == GameUi.ReducedMotionProperty && GameUi.GetReducedMotion(owner)) Finish();
        };
    }

    public void Start(double from, double to, double seconds)
    {
        StopTimer();
        tween.Start(from, to, seconds, GameUi.GetReducedMotion(owner) || TopLevel.GetTopLevel(owner) is null,
            GameUi.GetTheme(owner).Motion.Easing);
        apply(tween.Value);
        if (tween.IsActive) { last = Stopwatch.GetTimestamp(); timer.Start(); Interlocked.Increment(ref activeTimers); }
        else Completed?.Invoke();
    }

    public void Finish()
    {
        if (!tween.IsActive) return;
        StopTimer(); tween.Finish(); apply(tween.Value); Completed?.Invoke();
    }
    private void StopTimer()
    {
        if (!timer.IsEnabled) return;
        timer.Stop(); Interlocked.Decrement(ref activeTimers);
    }
}

public static class GameUiDiagnostics
{
    public static int ActiveAnimationTimers => MotionDriver.ActiveTimers;
    public static long AnimationTicks => MotionDriver.Ticks;
}
