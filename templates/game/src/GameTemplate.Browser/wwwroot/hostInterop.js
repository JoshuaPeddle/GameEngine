export function installHost(host) {
    let wanted = false;
    let sentinel = null;
    let pending = false;
    let failure = null;
    let pageHidden = false;
    const visible = () => !pageHidden && document.visibilityState === 'visible';
    const supported = () => !!navigator.wakeLock?.request;
    async function reconcile() {
        if (!wanted || !visible()) {
            const previous = sentinel;
            sentinel = null;
            if (previous) await previous.release().catch(() => {});
            return;
        }
        if (sentinel || pending || !supported()) return;
        pending = true;
        try {
            const acquired = await navigator.wakeLock.request('screen');
            if (!wanted || !visible()) {
                await acquired.release();
                return;
            }
            sentinel = acquired;
            failure = null;
            acquired.addEventListener('release', () => {
                if (sentinel === acquired) sentinel = null;
            });
        } catch (error) {
            failure = String(error);
        } finally {
            pending = false;
        }
    }
    globalThis.gameEngineHost = {
        setAwake(active) { wanted = active; void reconcile(); },
        isSupported: supported,
        isActive: () => !!sentinel && !sentinel.released,
        reason: () => supported() ? failure : 'Screen wake lock is unavailable.'
    };
    const update = () => { host.SetVisible(visible()); void reconcile(); };
    document.addEventListener('visibilitychange', update);
    window.addEventListener('pagehide', () => { pageHidden = true; update(); });
    window.addEventListener('pageshow', () => { pageHidden = false; update(); });
    window.addEventListener('blur', () => host.SetVisible(false));
    window.addEventListener('focus', update);
    update();
}
