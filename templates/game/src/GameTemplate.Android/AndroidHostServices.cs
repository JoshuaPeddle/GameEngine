using global::Android.App;
using global::Android.OS;
using global::Android.Views;
using GameEngine.Core;
using GameEngine.Runner.Avalonia;

namespace GameTemplate.Android;

internal sealed class AndroidHostServices(Activity activity) : IKeepAwakeBackend, IHapticsBackend
{
    private volatile bool _active;
    private volatile bool _foreground;
    private readonly global::Android.Content.ISharedPreferences? _preferences = activity.GetSharedPreferences("GameEngine.Host", global::Android.Content.FileCreationMode.Private);
    public bool Enabled
    {
        get => _preferences?.GetBoolean("HapticsEnabled", false) == true;
        set
        {
            using var editor = _preferences?.Edit();
            editor?.PutBoolean("HapticsEnabled", value);
            editor?.Apply();
        }
    }
    public void SetForeground(bool foreground) => _foreground = foreground;
    private readonly bool _hapticsSupported = HasVibrator(activity);

    private static bool HasVibrator(Activity activity)
    {
        if (System.OperatingSystem.IsAndroidVersionAtLeast(31))
            return (activity.GetSystemService(global::Android.Content.Context.VibratorManagerService) as VibratorManager)?.DefaultVibrator?.HasVibrator == true;
        return (activity.GetSystemService(global::Android.Content.Context.VibratorService) as Vibrator)?.HasVibrator == true;
    }
    public bool IsSupported => true;
    bool IHapticsBackend.IsSupported => _hapticsSupported;
    public bool IsActive => _active;
    public string? UnavailableReason => null;
    string? IHapticsBackend.UnavailableReason => _hapticsSupported ? null : "This device has no vibrator.";

    public void SetActive(bool active)
    {
        _active = active;
        activity.RunOnUiThread(() =>
        {
            if (_active) activity.Window?.AddFlags(WindowManagerFlags.KeepScreenOn);
            else activity.Window?.ClearFlags(WindowManagerFlags.KeepScreenOn);
        });
    }

    public bool Play(HapticCue cue)
    {
        if (!_hapticsSupported) return false;
        activity.RunOnUiThread(() =>
        {
            if (!_foreground || !Enabled || App.Lifecycle.State != HostState.Active || activity.IsFinishing || activity.IsDestroyed) return;
            var feedback = FeedbackConstants.VirtualKey;
            if (System.OperatingSystem.IsAndroidVersionAtLeast(30))
                feedback = cue == HapticCue.Rejection ? FeedbackConstants.Reject : FeedbackConstants.Confirm;
            activity.Window?.DecorView.PerformHapticFeedback(feedback);
        });
        return true;
    }
}
