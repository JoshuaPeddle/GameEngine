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

// The browser delivers keyboard events through JS rather than through the Avalonia control,
// so the map is built here from the engine's own key names: letters are the lowercase name,
// Space is " ", and the arrows are DOM's "ArrowUp" and friends.
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
        if (KeyMap.TryGetValue(key, out GeKeys value))
            GameView.Current?.Systems.TryGet<InputSystem>()?.KeyDown(value);
    }

    [SupportedOSPlatform("browser")]
    [JSExport]
    public static void HandleKeyUp(string key)
    {
        if (KeyMap.TryGetValue(key, out GeKeys value))
            GameView.Current?.Systems.TryGet<InputSystem>()?.KeyUp(value);
    }
}

internal sealed partial class Program
{
    private static Task Main(string[] args)
    {
        App.AssetSource = new GameEngine.Core.DelegateAssetSource(LoadFile);
        App.StartupScene = () => new GameEngine.Demo.SceneMenu();

        return BuildAvaloniaApp()
            .With(new BrowserPlatformOptions
            {
                RenderingMode = [BrowserRenderingMode.WebGL2, BrowserRenderingMode.WebGL1, BrowserRenderingMode.Software2D],
            })
            .UseSkia()
            .StartBrowserAppAsync("out");
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>();

    public static Stream LoadFile(string path)
    {
        if (AssetManifest.IsManifestPath(path))
            return AssetLoader.Open(new Uri(
                "avares://GameEngine.Runner.Avalonia.Browser/Assets/" + Path.GetFileName(path)));

        return AssetLoader.Open(new Uri("avares://GameEngine.Runner.Avalonia.Browser/Assets/game/" + path));
    }
}
