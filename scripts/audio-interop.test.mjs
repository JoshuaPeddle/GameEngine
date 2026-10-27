import assert from 'node:assert/strict';
import { test } from 'node:test';
import { readFile } from 'node:fs/promises';

const code = await readFile(new URL('../GameEngine.Runner.Avalonia/GameEngine.Runner.Avalonia.Browser/wwwroot/audioInterop.js', import.meta.url), 'utf8');
const { installAudio } = await import(`data:text/javascript;base64,${Buffer.from(code).toString('base64')}`);

class Node {
    connections = [];
    disconnected = false;
    connect(target) { this.connections.push(target); }
    disconnect() { this.disconnected = true; }
}
class Gain extends Node {
    gain = { value: 1, setValueAtTime(value) { this.value = value; } };
}
class Source extends Node {
    started = false;
    stopped = false;
    start() { this.started = true; }
    stop() { this.stopped = true; }
}
class Context {
    static instances = [];
    state = 'suspended';
    destination = {};
    currentTime = 0;
    gains = [];
    sources = [];
    constructor() { Context.instances.push(this); }
    createGain() { const node = new Gain(); this.gains.push(node); return node; }
    createBufferSource() { const node = new Source(); this.sources.push(node); return node; }
    createMediaElementSource() { return new Node(); }
    async decodeAudioData() { return { duration: 1 }; }
    async resume() { this.state = 'running'; }
    async suspend() { this.state = 'suspended'; }
    async close() { this.state = 'closed'; }
}
class Media {
    static instances = [];
    currentTime = 0;
    paused = true;
    constructor(url) { this.url = url; Media.instances.push(this); }
    addEventListener() { }
    load() { }
    async play() { this.paused = false; }
    pause() { this.paused = true; }
    removeAttribute() { this.url = null; }
}

function setup() {
    let now = 0;
    const listeners = new Map();
    const requests = [];
    globalThis.document = {
        baseURI: 'http://localhost/prefix/', hidden: false,
        addEventListener(name, callback) { if (!listeners.has(name)) listeners.set(name, new Set()); listeners.get(name).add(callback); },
        removeEventListener(name, callback) { listeners.get(name)?.delete(callback); }
    };
    globalThis.performance = { now: () => now };
    globalThis.AudioContext = Context;
    globalThis.Audio = Media;
    globalThis.fetch = async url => { requests.push(url); return { ok: true, arrayBuffer: async () => new ArrayBuffer(4) }; };
    installAudio();
    const api = globalThis.gameEngineAudio;
    const id = api.create();
    return {
        api, id, listeners, requests, context: Context.instances.at(-1),
        setTime(value) { now = value; },
        async event(name) { for (const callback of listeners.get(name) ?? []) callback(); await settle(); }
    };
}
async function settle() { for (let i = 0; i < 12; i++) await Promise.resolve(); }

test('preload caches decoded effects and preserves the deployment prefix', async () => {
    const { api, id, requests } = setup();
    api.preload(id, 'sounds/hit.wav', false);
    api.preload(id, 'sounds/hit.wav', false);
    await settle();
    assert.deepEqual(requests, ['http://localhost/prefix/audio/sounds/hit.wav']);
    api.dispose(id);
});

test('gesture unlock starts music but discards old effects', async () => {
    const state = setup();
    const { api, id, context } = state;
    api.play(id, 0, 'sounds/level1.wav', true, 0.3);
    api.play(id, 1, 'sounds/hit.wav', false, 0.5);
    await settle();
    const media = Media.instances.at(-1);
    assert.equal(media.paused, true);
    assert.equal(context.sources.length, 0);
    state.setTime(300);
    await state.event('pointerdown');
    assert.equal(media.paused, false);
    assert.equal(api.playing(id, 1), false);
    assert.equal(context.sources.length, 0);
    api.dispose(id);
});

test('overlapping effects, active gains, completion and replacement use independent voices', async () => {
    const state = setup();
    const { api, id, context } = state;
    await state.event('keydown');
    api.play(id, 1, 'sounds/hit.wav', false, 0.7);
    api.play(id, 2, 'sounds/hit.wav', false, 0.2);
    await settle();
    assert.equal(context.sources.filter(source => source.started).length, 2);
    api.volume(id, 1, 0);
    assert.equal(context.gains[0].gain.value, 0);
    assert.equal(context.gains[1].gain.value, 0.2);
    context.sources[0].onended();
    assert.equal(api.playing(id, 1), false);
    api.play(id, 2, 'sounds/hit.wav', false, 0.4);
    await settle();
    assert.equal(context.sources[1].stopped, true);
    assert.equal(api.playing(id, 2), true);
    api.dispose(id);
});

test('hidden pages retain music position, discard effects and resume eligible music', async () => {
    const state = setup();
    const { api, id, context } = state;
    await state.event('pointerdown');
    api.play(id, 0, 'sounds/level1.wav', true, 1);
    api.play(id, 1, 'sounds/hit.wav', false, 1);
    await settle();
    const media = Media.instances.at(-1);
    media.currentTime = 12;
    document.hidden = true;
    await state.event('visibilitychange');
    assert.equal(context.state, 'suspended');
    assert.equal(media.paused, true);
    assert.equal(media.currentTime, 12);
    assert.equal(api.playing(id, 1), false);
    assert.equal(api.play(id, 2, 'sounds/hit.wav', false, 1), false);
    document.hidden = false;
    await state.event('visibilitychange');
    assert.equal(context.state, 'running');
    assert.equal(media.paused, false);
    assert.equal(media.currentTime, 12);
    api.dispose(id);
});

test('dispose cancels pending decoding and removes subscriptions and media resources', async () => {
    const state = setup();
    const { api, id, context, listeners } = state;
    api.play(id, 1, 'sounds/hit.wav', false, 1);
    api.play(id, 0, 'sounds/level1.wav', true, 1);
    const media = Media.instances.at(-1);
    api.dispose(id);
    await settle();
    assert.equal(context.state, 'closed');
    assert.equal(context.sources.length, 0);
    assert.equal(media.paused, true);
    assert.equal(media.url, null);
    assert.equal(api.playing(id, 1), false);
    assert.equal([...listeners.values()].reduce((sum, callbacks) => sum + callbacks.size, 0), 0);
    const second = api.create();
    assert.ok(second > id);
    api.dispose(second);
});
