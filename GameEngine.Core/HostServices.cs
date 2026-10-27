namespace GameEngine.Core;

public enum HostState { Active, Inactive, Background }
public enum LifecycleEvent { Pause, Background, Foreground, Resume }
public enum HapticCue { Placement, Rejection, Reward }

public sealed class BackRequestEventArgs : EventArgs
{
    public bool Handled { get; set; }
}

public interface IKeepAwakeBackend
{
    bool IsSupported { get; }
    bool IsActive { get; }
    string? UnavailableReason { get; }
    void SetActive(bool active);
}

public interface IHapticsBackend
{
    bool Enabled { get; set; }
    void SetForeground(bool foreground);
    bool IsSupported { get; }
    string? UnavailableReason { get; }
    bool Play(HapticCue cue);
}

public sealed class HostServices : IDisposable
{
    private readonly IKeepAwakeBackend? _keepAwake;
    private readonly IHapticsBackend? _haptics;
    private bool _foreground;
    private bool _keepAwakeEnabled;
    private bool _disposed;

    public HostServices(IKeepAwakeBackend? keepAwake = null, IHapticsBackend? haptics = null)
    {
        _keepAwake = keepAwake;
        _haptics = haptics;
    }

    public bool KeepAwakeSupported => _keepAwake?.IsSupported == true;
    public bool KeepAwakeActive => _keepAwake?.IsActive == true;
    public string? KeepAwakeUnavailableReason => _keepAwake?.UnavailableReason ?? (KeepAwakeSupported ? null : "No keep-awake backend registered.");
    public bool HapticsSupported => _haptics?.IsSupported == true;
    public string? HapticsUnavailableReason => _haptics?.UnavailableReason ?? (HapticsSupported ? null : "No haptics backend registered.");
    private bool _hapticsEnabled;
    public bool HapticsEnabled
    {
        get => _haptics?.Enabled ?? _hapticsEnabled;
        set
        {
            _hapticsEnabled = value;
            if (_haptics != null) _haptics.Enabled = value;
        }
    }

    public bool KeepAwakeEnabled
    {
        get => _keepAwakeEnabled;
        set
        {
            _keepAwakeEnabled = value;
            UpdateKeepAwake();
        }
    }

    internal void SetForeground(bool foreground)
    {
        _foreground = foreground;
        _haptics?.SetForeground(foreground && !_disposed);
        UpdateKeepAwake();
    }

    private void UpdateKeepAwake() => _keepAwake?.SetActive(!_disposed && _foreground && _keepAwakeEnabled);

    public bool PlayHaptic(HapticCue cue) => !_disposed && _foreground && HapticsEnabled && _haptics?.Play(cue) == true;

    public void Dispose()
    {
        _disposed = true;
        _haptics?.SetForeground(false);
        UpdateKeepAwake();
    }
}
