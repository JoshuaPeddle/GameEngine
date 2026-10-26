using Avalonia;
using Avalonia.iOS;
using Foundation;
using System.Collections.Generic;

namespace GameEngine.Runner.Avalonia.iOS
{
    [Register("AppDelegate")]
#pragma warning disable CA1711
    public partial class AppDelegate : AvaloniaAppDelegate<App>
#pragma warning restore CA1711
    {
        private readonly List<NSObject> _sceneObservers = new();

        private void ObserveLifecycle(string name, GameEngine.Core.HostState state)
        {
            _sceneObservers.Add(NSNotificationCenter.DefaultCenter.AddObserver(new NSString(name), _ => App.Lifecycle.SetState(state)));
        }

        protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
        {
            App.HostServicesFactory = () =>
            {
                var backend = new IosHostServices();
                return new GameEngine.Core.HostServices(backend, backend);
            };
            ObserveLifecycle("UIApplicationWillResignActiveNotification", GameEngine.Core.HostState.Inactive);
            ObserveLifecycle("UIApplicationDidEnterBackgroundNotification", GameEngine.Core.HostState.Background);
            ObserveLifecycle("UIApplicationWillEnterForegroundNotification", GameEngine.Core.HostState.Inactive);
            ObserveLifecycle("UIApplicationDidBecomeActiveNotification", GameEngine.Core.HostState.Active);
            ObserveLifecycle("UISceneWillDeactivateNotification", GameEngine.Core.HostState.Inactive);
            ObserveLifecycle("UISceneDidEnterBackgroundNotification", GameEngine.Core.HostState.Background);
            ObserveLifecycle("UISceneWillEnterForegroundNotification", GameEngine.Core.HostState.Inactive);
            ObserveLifecycle("UISceneDidActivateNotification", GameEngine.Core.HostState.Active);
            App.AssetSource = new Core.FileAssetSource(NSBundle.MainBundle.BundlePath);
            App.StartupScene = () => new Demo.SceneMenu();
            return base.CustomizeAppBuilder(builder);
        }
    }
}
