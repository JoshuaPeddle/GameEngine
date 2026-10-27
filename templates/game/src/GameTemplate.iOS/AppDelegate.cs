using Avalonia;
using Avalonia.iOS;
using Foundation;
using GameEngine.Core;
using GameEngine.Runner.Avalonia;
using System.Collections.Generic;

namespace GameTemplate.iOS;

[Register("AppDelegate")]
#pragma warning disable CA1711
public partial class AppDelegate : AvaloniaAppDelegate<App>
#pragma warning restore CA1711
{
    private readonly List<NSObject> _sceneObservers = new();

    private void ObserveLifecycle(string name, HostState state)
    {
        _sceneObservers.Add(NSNotificationCenter.DefaultCenter.AddObserver(new NSString(name), _ => App.Lifecycle.SetState(state)));
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        App.HostServicesFactory = () =>
        {
            var backend = new IosHostServices();
            return new HostServices(backend, backend);
        };
        ObserveLifecycle("UIApplicationWillResignActiveNotification", HostState.Inactive);
        ObserveLifecycle("UIApplicationDidEnterBackgroundNotification", HostState.Background);
        ObserveLifecycle("UIApplicationWillEnterForegroundNotification", HostState.Inactive);
        ObserveLifecycle("UIApplicationDidBecomeActiveNotification", HostState.Active);
        ObserveLifecycle("UISceneWillDeactivateNotification", HostState.Inactive);
        ObserveLifecycle("UISceneDidEnterBackgroundNotification", HostState.Background);
        ObserveLifecycle("UISceneWillEnterForegroundNotification", HostState.Inactive);
        ObserveLifecycle("UISceneDidActivateNotification", HostState.Active);
        App.AssetSource = new FileAssetSource(NSBundle.MainBundle.BundlePath);
        App.StartupScene = () => new MainScene();
        return base.CustomizeAppBuilder(builder);
    }
}
