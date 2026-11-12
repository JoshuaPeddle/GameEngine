using Avalonia;
using Avalonia.Headless;
using GameEngine.Demo.Tests;

[assembly: AvaloniaTestApplication(typeof(UiHeadlessSetup))]

namespace GameEngine.Demo.Tests;

public static class UiHeadlessSetup
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<Application>()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).UseSkia();
}
