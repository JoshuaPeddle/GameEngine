using Android.App;
using Android.Content.PM;
using Avalonia;
using Avalonia.Android;
using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Platform.Storage;
using Java.IO;
using System.IO;

namespace GameEngine.Runner.Avalonia.Android
{
    [Activity(
        Label = "GameEngine.Runner.Avalonia.Android",
        Theme = "@style/MyTheme.NoActionBar",
        Icon = "@drawable/icon",
        MainLauncher = true,
        ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
    public class MainActivity : AvaloniaMainActivity<App>
    {
        protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
        {
            //Stream input = Assets.Open("Resources/asset.txt");


            var list3 = Assets.List("");
            var list2= Assets.List("game");

            var list4 = Assets.List("game/images");

            var list5 = Assets.List("game/sounds");

            var assets = Assets.Open("assets.txt");

            var text = new StreamReader(assets).ReadToEnd();

            App.Test = "Yee";

            GameView.Test = text;

            GameEngine.Core.Assets._fileFetcher = LoadFile;

            return base.CustomizeAppBuilder(builder)
                .WithInterFont();
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
}
