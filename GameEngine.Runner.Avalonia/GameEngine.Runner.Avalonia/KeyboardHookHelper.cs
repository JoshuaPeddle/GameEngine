using GameEngine.Core;
using GameEngine.Core.Systems;
using SharpHook.Data;
using SharpHook.Reactive;
using System;
using System.Collections.Generic;

namespace GameEngine.Runner.Avalonia;

/// <summary>
/// Isolates all SharpHook types into a separate class so they are never loaded
/// on platforms that don't support native desktop hooks (for example Android,
/// iOS, and Browser/WASM).
/// </summary>
internal static class KeyboardHookHelper
{
    private static readonly Dictionary<KeyCode, GeKeys> KeyMap = new()
    {
        { KeyCode.VcW, GeKeys.W },
        { KeyCode.VcA, GeKeys.A },
        { KeyCode.VcS, GeKeys.S },
        { KeyCode.VcD, GeKeys.D },
        { KeyCode.VcSpace, GeKeys.Space }
    };

    public static IDisposable Create(Engine engine)
    {
        var hook = new ReactiveGlobalHook(GlobalHookType.Keyboard, runAsyncOnBackgroundThread: true);

        hook.KeyPressed.Subscribe(args =>
        {
            if (KeyMap.TryGetValue(args.Data.KeyCode, out var value))
                engine.Systems.Get<InputSystem>().KeyDown(value);
        });

        hook.KeyReleased.Subscribe(args =>
        {
            if (KeyMap.TryGetValue(args.Data.KeyCode, out var value))
                engine.Systems.Get<InputSystem>().KeyUp(value);
        });

        hook.RunAsync();
        return hook;
    }
}
