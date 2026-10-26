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
        protected override void OnCreate(global::Android.OS.Bundle? savedInstanceState)
        {
            App.HostServicesFactory = () =>
            {
                var backend = new AndroidHostServices(this);
                return new GameEngine.Core.HostServices(backend, backend);
            };
            base.OnCreate(savedInstanceState);
        }

        protected override void OnResume()
        {
            base.OnResume();
            App.Lifecycle.SetState(GameEngine.Core.HostState.Active);
        }

        protected override void OnPause()
        {
            App.Lifecycle.SetState(GameEngine.Core.HostState.Inactive);
            base.OnPause();
        }

        protected override void OnStop()
        {
            App.Lifecycle.SetState(GameEngine.Core.HostState.Background);
            base.OnStop();
        }

        protected override void OnStart()
        {
            base.OnStart();
            App.Lifecycle.SetState(GameEngine.Core.HostState.Inactive);
        }
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
                return Assets!.Open(Path.GetFileName(path));

            return Assets!.Open("game/" + path);
        }
    }
}
