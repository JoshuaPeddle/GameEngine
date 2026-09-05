using Avalonia;
using Avalonia.Browser;
using Avalonia.Platform;
using GameEngine.Core;
using GameEngine.Core.Systems;
using GameEngine.Runner.Avalonia;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Threading.Tasks;

// wwwroot/keyboardInterop.js forwards the page's keyboard events here. The map is built from
// the engine's own key names, so a key added to GeKeys arrives without touching this file.
public static partial class KeyboardInterop
{
    public static Dictionary<string, GeKeys> KeyMap { get; } = BuildKeyMap();

    private static Dictionary<string, GeKeys> BuildKeyMap()
    {
        var map = new Dictionary<string, GeKeys>(StringComparer.OrdinalIgnoreCase);

        foreach (var engineKey in Enum.GetValues<GeKeys>())
            map[DomNameOf(engineKey)] = engineKey;

        return map;
    }

    private static string DomNameOf(GeKeys key) => key switch
    {
        GeKeys.Space => " ",
        GeKeys.Up or GeKeys.Down or GeKeys.Left or GeKeys.Right => "Arrow" + key,
        _ => key.ToString().ToLowerInvariant()
    };

    [SupportedOSPlatform("browser")]
    [JSExport]
    public static void HandleKeyDown(string key)
    {
        if (KeyMap.TryGetValue(key, out var value))
            GameView.Current?.Systems.TryGet<InputSystem>()?.KeyDown(value);
    }

    [SupportedOSPlatform("browser")]
    [JSExport]
    public static void HandleKeyUp(string key)
    {
        if (KeyMap.TryGetValue(key, out var value))
            GameView.Current?.Systems.TryGet<InputSystem>()?.KeyUp(value);
    }
}

internal sealed partial class Program
{
    private static Task Main(string[] args)
    {
        App.AssetSource = new DelegateAssetSource(LoadFile);
        App.StartupScene = () => new GameTemplate.MainScene();

        return BuildAvaloniaApp()
            .With(new BrowserPlatformOptions
            {
                RenderingMode = [BrowserRenderingMode.WebGL2, BrowserRenderingMode.WebGL1, BrowserRenderingMode.Software2D]
            })
            .UseSkia()
            .StartBrowserAppAsync("out");
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>();

    private static Stream LoadFile(string path)
    {
        if (AssetManifest.IsManifestPath(path))
            return AssetLoader.Open(new Uri("avares://GameTemplate.Browser/Assets/" + Path.GetFileName(path)));

        return AssetLoader.Open(new Uri("avares://GameTemplate.Browser/Assets/game/" + path));
    }
}
