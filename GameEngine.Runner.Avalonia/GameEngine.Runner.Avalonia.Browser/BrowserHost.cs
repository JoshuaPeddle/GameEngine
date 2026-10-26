using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using GameEngine.Core;
using GameEngine.Runner.Avalonia;

[SupportedOSPlatform("browser")]
public sealed partial class BrowserHost : IKeepAwakeBackend
{
    [JSImport("globalThis.gameEngineHost.setAwake")]
    private static partial void SetAwake(bool active);
    [JSImport("globalThis.gameEngineHost.isSupported")]
    private static partial bool Supported();
    [JSImport("globalThis.gameEngineHost.isActive")]
    private static partial bool Active();
    [JSImport("globalThis.gameEngineHost.reason")]
    private static partial string? Reason();

    public bool IsSupported => Supported();
    public bool IsActive => Active();
    public string? UnavailableReason => Reason();
    public void SetActive(bool active) => SetAwake(active);

    [JSExport]
    public static void SetVisible(bool visible)
    {
        if (!visible) GameView.Current?.InputManager.ReleaseAllKeys();
        App.Lifecycle.SetState(visible ? HostState.Active : HostState.Background);
    }
}
