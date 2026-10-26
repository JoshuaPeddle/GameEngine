import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const source = await readFile(new URL('../GameEngine.Runner.Avalonia/GameEngine.Runner.Avalonia.Browser/wwwroot/hostInterop.js', import.meta.url), 'utf8');
const { installHost } = await import('data:text/javascript;base64,' + Buffer.from(source).toString('base64'));
const flush = () => new Promise(setImmediate);

function install(wakeLock, visible = true) {
    globalThis.document = Object.assign(new EventTarget(), { visibilityState: visible ? 'visible' : 'hidden' });
    globalThis.window = new EventTarget();
    Object.defineProperty(globalThis, 'navigator', { configurable: true, value: { wakeLock } });
    const states = [];
    installHost({ SetVisible: state => states.push(state) });
    return { host: globalThis.gameEngineHost, states };
}

function sentinel() {
    const value = new EventTarget();
    value.released = false;
    value.release = async () => { value.released = true; value.dispatchEvent(new Event('release')); };
    return value;
}

test('visibility suspends, releases wake lock and reacquires only when visible', async () => {
    const locks = [];
    const { host, states } = install({ request: async () => { const lock = sentinel(); locks.push(lock); return lock; } });
    host.setAwake(true);
    await flush();
    assert.equal(host.isActive(), true);
    document.visibilityState = 'hidden';
    document.dispatchEvent(new Event('visibilitychange'));
    await flush();
    assert.equal(host.isActive(), false);
    assert.equal(locks[0].released, true);
    document.visibilityState = 'visible';
    document.dispatchEvent(new Event('visibilitychange'));
    await flush();
    assert.equal(locks.length, 2);
    assert.deepEqual(states, [true, false, true]);
    window.dispatchEvent(new Event('pagehide'));
    await flush();
    assert.equal(host.isActive(), false);
    window.dispatchEvent(new Event('pageshow'));
    await flush();
    assert.equal(host.isActive(), true);
    host.setAwake(false);
});

test('unsupported and denied wake locks report failure without claiming activity', async () => {
    let { host } = install(undefined);
    host.setAwake(true);
    await flush();
    assert.equal(host.isSupported(), false);
    assert.match(host.reason(), /unavailable/);
    ({ host } = install({ request: async () => { throw new Error('denied'); } }));
    host.setAwake(true);
    await flush();
    assert.equal(host.isActive(), false);
    assert.match(host.reason(), /denied/);
});

test('a delayed lock is immediately released after a pause or disposal', async () => {
    let complete;
    const { host, states } = install({ request: () => new Promise(resolve => { complete = resolve; }) });
    host.setAwake(true);
    host.setAwake(false);
    const lock = sentinel();
    complete(lock);
    await flush();
    assert.equal(host.isActive(), false);
    assert.equal(lock.released, true);
    window.dispatchEvent(new Event('blur'));
    assert.equal(states.at(-1), false);
    window.dispatchEvent(new Event('focus'));
    assert.equal(states.at(-1), true);
});

test('the template uses the same tested browser host bridge', async () => {
    const template = await readFile(new URL('../templates/game/src/GameTemplate.Browser/wwwroot/hostInterop.js', import.meta.url), 'utf8');
    assert.equal(template, source);
});
