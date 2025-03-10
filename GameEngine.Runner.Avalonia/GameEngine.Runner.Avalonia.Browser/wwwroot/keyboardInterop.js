document.addEventListener("keydown",async event => {
    const { getAssemblyExports } = await globalThis.getDotnetRuntime(0);
    var exports = await getAssemblyExports("GameEngine.Runner.Avalonia.Browser.dll");
    exports.KeyboardInterop.HandleKeyDown(event.key);
});

document.addEventListener("keyup", async event => {
    const { getAssemblyExports } = await globalThis.getDotnetRuntime(0);
    var exports = await getAssemblyExports("GameEngine.Runner.Avalonia.Browser.dll");
    exports.KeyboardInterop.HandleKeyUp(event.key);
});
