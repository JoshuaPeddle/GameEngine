using Android.App;
using Android.Content.PM;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;
using System;
using System.IO;

namespace GameEngine.Runner.Avalonia.Android
{
    [Activity(
        Label = "GameEngine.Runner.Avalonia.Android",
        Theme = "@style/MyTheme.NoActionBar",
        Icon = "@drawable/icon",
        MainLauncher = true,
        ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
    public class MainActivity : AvaloniaMainActivity
    {
    }

    [Application]
    public class AndroidApp : AvaloniaAndroidApplication<App>
    {
        protected AndroidApp(IntPtr javaReference, JniHandleOwnership transfer)
            : base(javaReference, transfer)
        {
        }

        protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
        {
            App.AssetSource = new GameEngine.Core.DelegateAssetSource(LoadFile);
            Core.Systems.AudioBackends.Factory = () => new Audio.Android.AndroidAudioBackend(this, App.AssetSource);
            App.StartupScene = () => new GameEngine.Demo.SceneMenu();
            App.FirstFramePresented = () => global::Android.Util.Log.Info(
                "GameEngineSmoke",
                "FIRST_FRAME_PRESENTED");

            return base.CustomizeAppBuilder(builder).With(new AndroidPlatformOptions
            {
                RenderingMode = [AndroidRenderingMode.Egl]
            });
        }

        private Stream LoadFile(string path)
        {
            if (Core.AssetManifest.IsManifestPath(path))
                return (Assets ?? throw new InvalidOperationException("Android assets are unavailable.")).Open(Path.GetFileName(path));

            return (Assets ?? throw new InvalidOperationException("Android assets are unavailable.")).Open("game/" + path);
        }
    }
}
