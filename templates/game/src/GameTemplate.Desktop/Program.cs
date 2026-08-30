using Avalonia;
using GameEngine.Runner.Avalonia;
using System;

namespace GameTemplate.Desktop;

internal sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        App.StartupScene = () => new MainScene();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
