using GameEngine.Core;
using Microsoft.Extensions.Logging;
using SkiaSharp.Views.Maui.Controls.Hosting;
using System.IO;

namespace GameEngine.Runner.Maui
{
    public static class MauiProgram
    {
        public static IAssetSource AssetSource { get; } = new DelegateAssetSource(OpenPackagedAsset);

        private static Stream OpenPackagedAsset(string path)
        {
            if (path.Contains("assets.txt") || path.Contains("levels"))
            {
                return FileSystem.OpenAppPackageFileAsync("Assets/" + path).GetAwaiter().GetResult();
            }

            return FileSystem.OpenAppPackageFileAsync("Assets/game/" + path).GetAwaiter().GetResult();
        }

        public static MauiApp CreateMauiApp()
        {
            var appDirectory = AppContext.BaseDirectory;
            Directory.SetCurrentDirectory(appDirectory);

            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseSkiaSharp()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if DEBUG
    		builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
