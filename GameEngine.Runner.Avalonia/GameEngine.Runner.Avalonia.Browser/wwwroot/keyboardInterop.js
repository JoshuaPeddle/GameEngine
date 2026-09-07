export function installKeyboard(interop) {
    const swallowed = new Set(['ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight', ' ']);
    const held = new Set();
    document.addEventListener('keydown', event => {
        if (swallowed.has(event.key)) event.preventDefault();
        held.add(event.key);
        interop.HandleKeyDown(event.key);
    });
    document.addEventListener('keyup', event => {
        if (swallowed.has(event.key)) event.preventDefault();
        held.delete(event.key);
        interop.HandleKeyUp(event.key);
    });
    window.addEventListener('blur', () => {
        for (const key of held) interop.HandleKeyUp(key);
        held.clear();
    });
}
