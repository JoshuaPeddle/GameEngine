using Avalonia;
using Avalonia.iOS;
using Foundation;
using GameEngine.Core;
using GameEngine.Runner.Avalonia;

namespace GameTemplate.iOS;

[Register("AppDelegate")]
#pragma warning disable CA1711
public partial class AppDelegate : AvaloniaAppDelegate<App>
#pragma warning restore CA1711
{
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        App.AssetSource = new FileAssetSource(NSBundle.MainBundle.BundlePath);
        App.StartupScene = () => new MainScene();
        return base.CustomizeAppBuilder(builder);
    }
}
