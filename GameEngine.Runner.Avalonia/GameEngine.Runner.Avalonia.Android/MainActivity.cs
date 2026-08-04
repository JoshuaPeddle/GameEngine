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
        protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
        {
            App._fileFetcher = LoadFile;
            App.StartupScene = () => new GameEngine.Demo.SceneMenu();
            builder.With (new AndroidPlatformOptions
            {
                RenderingMode = [AndroidRenderingMode.Egl]
            });
            return base.CustomizeAppBuilder(builder);
        }

        public Stream LoadFile(string path)
        {
            if (path.Contains("assets.txt"))
            {
                return Assets.Open("assets.txt");
            }

            return Assets.Open("game/" + path);
        }
    }

    [Application]
    public class AndroidApp : AvaloniaAndroidApplication<App>
    {
        protected AndroidApp(IntPtr javaReference, JniHandleOwnership transfer)
            : base(javaReference, transfer)
        {
        }
    }
}
