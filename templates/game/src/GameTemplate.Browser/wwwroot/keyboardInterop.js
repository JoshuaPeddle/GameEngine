// The browser hands keyboard events to the page, not to the Avalonia control, so they are
// forwarded into the engine here. Arrow keys and space scroll the page by default; a game
// filling the window never wants that.
const swallowed = new Set(["ArrowUp", "ArrowDown", "ArrowLeft", "ArrowRight", " "]);

async function interop() {
    const { getAssemblyExports } = await globalThis.getDotnetRuntime(0);
    const exports = await getAssemblyExports("GameTemplate.Browser.dll");
    return exports.KeyboardInterop;
}

document.addEventListener("keydown", async event => {
    if (swallowed.has(event.key)) event.preventDefault();
    (await interop()).HandleKeyDown(event.key);
});

document.addEventListener("keyup", async event => {
    if (swallowed.has(event.key)) event.preventDefault();
    (await interop()).HandleKeyUp(event.key);
});
