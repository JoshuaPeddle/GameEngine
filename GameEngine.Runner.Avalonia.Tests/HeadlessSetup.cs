using Avalonia;
using Avalonia.Headless;
using GameEngine.Runner.Avalonia.Tests;

[assembly: AvaloniaTestApplication(typeof(HeadlessAppBuilder))]

namespace GameEngine.Runner.Avalonia.Tests;

public static class HeadlessAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<HeadlessApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}

public class HeadlessApp : Application
{
}
