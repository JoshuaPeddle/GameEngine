using Android.App;
using Android.Content.PM;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;
using GameEngine.Core;
using GameEngine.Runner.Avalonia;
using System;
using System.IO;

namespace GameTemplate.Android;

[Activity(
    Label = "GameTemplate",
    Theme = "@style/MyTheme.NoActionBar",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity
{
}

[Application]
public class GameApplication : AvaloniaAndroidApplication<App>
{
    protected GameApplication(IntPtr javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        App.AssetSource = new DelegateAssetSource(LoadFile);
        App.StartupScene = () => new MainScene();

        return base.CustomizeAppBuilder(builder).With(new AndroidPlatformOptions
        {
            RenderingMode = [AndroidRenderingMode.Egl]
        });
    }

    private Stream LoadFile(string path)
    {
        if (AssetManifest.IsManifestPath(path))
            return Assets!.Open(Path.GetFileName(path));

        return Assets!.Open("game/" + path);
    }
}
