using System.Runtime.InteropServices.JavaScript;
using GameEngine.Core.Systems;

internal sealed partial class WebAudioBackend : IAudioBackend
{
    private readonly int id = Create();
    private bool disposed;
    public string Name => "Web Audio";
    public bool IsAvailable => !disposed && id != 0;
    public string? UnavailableReason => disposed ? "The Web Audio backend has been disposed."
        : id == 0 ? "This browser does not provide Web Audio." : GetError(id);

    public void Preload(string soundName, string path, SoundType soundType)
    {
        if (IsAvailable) Load(id, path, soundType == SoundType.BGM);
    }
    public bool TryPlay(int voice, string soundName, string path, SoundType soundType, float volume, int priority)
        => IsAvailable && Play(id, voice, path, soundType == SoundType.BGM, volume);
    public bool IsPlaying(int voice) => IsAvailable && Playing(id, voice);
    public void SetVolume(int voice, float volume) { if (IsAvailable) Volume(id, voice, volume); }
    public void StopVoice(int voice) { if (IsAvailable) Stop(id, voice); }
    public void StopAll() { if (IsAvailable) StopAllVoices(id); }
    public void Dispose()
    {
        if (disposed) return;
        if (id != 0) Release(id);
        disposed = true;
    }

    [JSImport("globalThis.gameEngineAudio.create")]
    private static partial int Create();
    [JSImport("globalThis.gameEngineAudio.error")]
    private static partial string? GetError(int id);
    [JSImport("globalThis.gameEngineAudio.preload")]
    private static partial void Load(int id, string path, bool music);
    [JSImport("globalThis.gameEngineAudio.play")]
    private static partial bool Play(int id, int voice, string path, bool music, float volume);
    [JSImport("globalThis.gameEngineAudio.playing")]
    private static partial bool Playing(int id, int voice);
    [JSImport("globalThis.gameEngineAudio.volume")]
    private static partial void Volume(int id, int voice, float volume);
    [JSImport("globalThis.gameEngineAudio.stop")]
    private static partial void Stop(int id, int voice);
    [JSImport("globalThis.gameEngineAudio.stopAll")]
    private static partial void StopAllVoices(int id);
    [JSImport("globalThis.gameEngineAudio.dispose")]
    private static partial void Release(int id);
}
