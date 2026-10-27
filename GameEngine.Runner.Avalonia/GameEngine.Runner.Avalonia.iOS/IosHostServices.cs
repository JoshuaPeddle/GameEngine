using CoreHaptics;
using Foundation;
using GameEngine.Core;
using GameEngine.Runner.Avalonia;
using UIKit;
using System.Linq;

namespace GameEngine.Runner.Avalonia.iOS;

internal sealed class IosHostServices : IKeepAwakeBackend, IHapticsBackend
{
    private volatile bool _requested;
    private volatile bool _active;
    private volatile bool _foreground;
    private const string HapticsKey = "GameEngine.HapticsEnabled";
    public bool Enabled
    {
        get => NSUserDefaults.StandardUserDefaults.BoolForKey(HapticsKey);
        set => NSUserDefaults.StandardUserDefaults.SetBool(value, HapticsKey);
    }
    public void SetForeground(bool foreground) => _foreground = foreground;
    private readonly bool _hapticsSupported = CHHapticEngine.GetHardwareCapabilities().SupportsHaptics;
    public bool IsSupported => true;
    bool IHapticsBackend.IsSupported => _hapticsSupported;
    public bool IsActive => _active;
    public string? UnavailableReason => null;
    string? IHapticsBackend.UnavailableReason => _hapticsSupported ? null : "This device does not support haptics.";

    public void SetActive(bool active)
    {
        _requested = active;
        void Apply()
        {
            UIApplication.SharedApplication.IdleTimerDisabled = _requested;
            _active = _requested;
        }
        if (NSThread.IsMain) Apply();
        else UIApplication.SharedApplication.BeginInvokeOnMainThread(Apply);
    }

    public bool Play(HapticCue cue)
    {
        if (!_hapticsSupported) return false;
        UIApplication.SharedApplication.BeginInvokeOnMainThread(() =>
        {
            if (!_foreground || !Enabled || App.Lifecycle.State != HostState.Active) return;
            if (cue == HapticCue.Placement)
            {
                UIImpactFeedbackGenerator impact;
                if (System.OperatingSystem.IsIOSVersionAtLeast(17, 5))
                {
                    var view = UIApplication.SharedApplication.ConnectedScenes.OfType<UIWindowScene>()
                        .Where(scene => scene.ActivationState == UISceneActivationState.ForegroundActive)
                        .Select(scene => scene.KeyWindow?.RootViewController?.View).FirstOrDefault(view => view != null);
                    if (view == null) return;
                    impact = UIImpactFeedbackGenerator.GetFeedbackGenerator(UIImpactFeedbackStyle.Medium, view);
                }
                else impact = new UIImpactFeedbackGenerator(UIImpactFeedbackStyle.Medium);
                using (impact)
                {
                    impact.Prepare();
                    impact.ImpactOccurred();
                }
            }
            else
            {
                using var notification = new UINotificationFeedbackGenerator();
                notification.Prepare();
                notification.NotificationOccurred(cue == HapticCue.Rejection ? UINotificationFeedbackType.Error : UINotificationFeedbackType.Success);
            }
        });
        return true;
    }
}
