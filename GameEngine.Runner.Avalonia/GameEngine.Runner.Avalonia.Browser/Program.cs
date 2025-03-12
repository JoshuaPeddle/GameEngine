using Avalonia;
using Avalonia.Browser;
using Avalonia.Platform;
using GameEngine.Runner.Avalonia;
using System;
using System.IO;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using System.Runtime.InteropServices.JavaScript;
using GameEngine.Core.Systems;
using System.Collections.Generic;
using GameEngine.Core;

public static partial class KeyboardInterop
{
    public static Dictionary<string, GeKeys> KeyMap { get; private set; } = new Dictionary<string, GeKeys>()
    {
        { "w", GeKeys.W },
        { "a", GeKeys.A },
        { "s", GeKeys.S },
        { "d", GeKeys.D },
        { " ", GeKeys.Space }
    };

    [SupportedOSPlatform("browser")]
    [JSExport]
    public static void HandleKeyDown(string key)
    {
        if (KeyMap.TryGetValue(key, out GeKeys value))
            GameView._gameEngine.Systems.Get<InputSystem>().KeyDown(value);
    }

    [SupportedOSPlatform("browser")]
    [JSExport]
    public static void HandleKeyUp(string key)
    {
        if (KeyMap.TryGetValue(key, out GeKeys value))
            GameView._gameEngine.Systems.Get<InputSystem>().KeyUp(value);
    }
}

internal sealed partial class Program
{
    private static Task Main(string[] args)
    {
        App._fileFetcher = LoadFile;

        return BuildAvaloniaApp()
            .StartBrowserAppAsync("out");
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>();

    public static Stream LoadFile(string path)
    {
        if (path.Contains("assets.txt"))
        {
            return AssetLoader.Open(new Uri("avares://GameEngine.Runner.Avalonia.Browser/Assets/assets.txt"));
        }

        return AssetLoader.Open(new Uri("avares://GameEngine.Runner.Avalonia.Browser/Assets/game/" + path));
    }
}
