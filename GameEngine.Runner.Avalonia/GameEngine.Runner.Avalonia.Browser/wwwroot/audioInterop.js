export function installAudio() {
    const sessions = new Map();
    let nextId = 1;
    const url = path => new URL(`./audio/${path.split('/').map(encodeURIComponent).join('/')}`, document.baseURI).href;

    class AudioSession {
        constructor() {
            this.context = new AudioContext();
            this.buffers = new Map();
            this.media = new Map();
            this.voices = new Map();
            this.error = null;
            this.disposed = false;
            this.unlocked = false;
            this.unlock = () => {
                if (document.hidden || this.disposed) return;
                this.unlocked = true;
                this.context.resume().then(() => {
                    for (const voice of this.voices.values()) this.start(voice);
                }).catch(error => { this.error = error.message; });
            };
            this.visibility = () => {
                if (document.hidden) {
                    for (const voice of [...this.voices.values()]) {
                        if (voice.music) voice.element?.pause();
                        else this.stop(voice.id);
                    }
                    this.context.suspend().catch(error => { this.error = error.message; });
                } else if (this.unlocked) this.unlock();
            };
            document.addEventListener('pointerdown', this.unlock, true);
            document.addEventListener('keydown', this.unlock, true);
            document.addEventListener('visibilitychange', this.visibility);
        }
        preload(path, music) {
            if (music) {
                if (!this.media.has(path)) {
                    const element = new Audio(url(path));
                    element.preload = 'auto';
                    element.loop = true;
                    element.addEventListener('error', () => { this.error = `Cannot load music: ${path}`; });
                    const node = this.context.createMediaElementSource(element);
                    this.media.set(path, { element, node });
                    element.load();
                }
                return;
            }
            if (!this.buffers.has(path)) {
                const promise = fetch(url(path)).then(response => {
                    if (!response.ok) throw new Error(`Cannot load sound: ${path} (${response.status})`);
                    return response.arrayBuffer();
                }).then(data => this.context.decodeAudioData(data));
                this.buffers.set(path, promise);
                promise.catch(error => { this.error = error.message; });
            }
        }
        play(id, path, music, volume) {
            if (document.hidden && !music) return false;
            this.stop(id);
            this.preload(path, music);
            const gain = this.context.createGain();
            gain.gain.value = volume;
            gain.connect(this.context.destination);
            const voice = { id, path, music, gain, requested: performance.now(), started: false };
            this.voices.set(id, voice);
            if (music) {
                Object.assign(voice, this.media.get(path));
                voice.node.connect(gain);
                this.start(voice);
            } else {
                this.buffers.get(path).then(buffer => {
                    if (this.disposed || this.voices.get(id) !== voice) return;
                    voice.buffer = buffer;
                    this.start(voice);
                }).catch(() => { if (this.voices.get(id) === voice) this.stop(id); });
            }
            return true;
        }
        start(voice) {
            if (this.disposed || document.hidden || this.context.state !== 'running' || this.voices.get(voice.id) !== voice) return;
            if (voice.music) {
                voice.element.play().catch(error => { this.error = error.message; });
                return;
            }
            if (voice.started || !voice.buffer) return;
            if (performance.now() - voice.requested > 250) { this.stop(voice.id); return; }
            const source = this.context.createBufferSource();
            source.buffer = voice.buffer;
            source.connect(voice.gain);
            voice.source = source;
            voice.started = true;
            source.onended = () => { if (this.voices.get(voice.id) === voice) this.stop(voice.id); };
            source.start();
        }
        playing(id) {
            const voice = this.voices.get(id);
            if (!voice) return false;
            if (!voice.music && !voice.started && performance.now() - voice.requested > 250) this.stop(id);
            return this.voices.has(id);
        }
        stop(id) {
            const voice = this.voices.get(id);
            if (!voice) return;
            this.voices.delete(id);
            if (voice.source) { voice.source.onended = null; voice.source.stop(); voice.source.disconnect(); }
            if (voice.element) { voice.element.pause(); voice.element.currentTime = 0; voice.node.disconnect(); }
            voice.gain.disconnect();
        }
        stopAll() { for (const id of [...this.voices.keys()]) this.stop(id); }
        dispose() {
            this.disposed = true;
            document.removeEventListener('pointerdown', this.unlock, true);
            document.removeEventListener('keydown', this.unlock, true);
            document.removeEventListener('visibilitychange', this.visibility);
            this.stopAll();
            for (const { element, node } of this.media.values()) {
                node.disconnect();
                element.removeAttribute('src');
                element.load();
            }
            this.media.clear();
            this.buffers.clear();
            this.context.close().catch(() => {});
        }
    }
    globalThis.gameEngineAudio = {
        create: () => {
            if (!globalThis.AudioContext) return 0;
            try { const id = nextId++; sessions.set(id, new AudioSession()); return id; }
            catch { return 0; }
        },
        error: id => sessions.get(id)?.error ?? null,
        preload: (id, path, music) => sessions.get(id)?.preload(path, music),
        play: (id, voice, path, music, volume) => sessions.get(id)?.play(voice, path, music, volume) ?? false,
        playing: (id, voice) => sessions.get(id)?.playing(voice) ?? false,
        volume: (id, voice, volume) => {
            const session = sessions.get(id);
            const state = session?.voices.get(voice);
            if (state) state.gain.gain.setValueAtTime(volume, session.context.currentTime);
        },
        stop: (id, voice) => sessions.get(id)?.stop(voice),
        stopAll: id => sessions.get(id)?.stopAll(),
        dispose: id => { sessions.get(id)?.dispose(); sessions.delete(id); }
    };
}
