using Avalonia;
using Avalonia.iOS;
using Foundation;

namespace GameEngine.Runner.Avalonia.iOS
{
    [Register("AppDelegate")]
#pragma warning disable CA1711
    public partial class AppDelegate : AvaloniaAppDelegate<App>
#pragma warning restore CA1711
    {
        protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
        {
            App.AssetSource = new Core.FileAssetSource(NSBundle.MainBundle.BundlePath);
            App.StartupScene = () => new Demo.SceneMenu();
            return base.CustomizeAppBuilder(builder);
        }
    }
}
