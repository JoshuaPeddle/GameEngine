namespace GameEngine.Core.Systems;

public interface IAudioBackend : IDisposable
{
    string Name { get; }
    bool IsAvailable { get; }
    string? UnavailableReason { get; }
    void Preload(string soundName, string path, SoundType soundType);
    bool TryPlay(int voice, string soundName, string path, SoundType soundType, float volume, int priority);
    bool IsPlaying(int voice);
    void SetVolume(int voice, float volume);
    void StopVoice(int voice);
    void StopAll();
}

public class SilentAudioBackend : IAudioBackend
{
    public virtual string Name => "silent";
    public virtual bool IsAvailable => true;
    public virtual string? UnavailableReason => null;
    public virtual void Preload(string soundName, string path, SoundType soundType) { }
    public virtual bool TryPlay(int voice, string soundName, string path, SoundType soundType, float volume, int priority) => true;
    public virtual bool IsPlaying(int voice) => false;
    public virtual void SetVolume(int voice, float volume) { }
    public virtual void StopVoice(int voice) { }
    public virtual void StopAll() { }
    public virtual void Dispose() => GC.SuppressFinalize(this);
}

public sealed class UnavailableAudioBackend(string name, string reason) : SilentAudioBackend
{
    public override string Name => name;
    public override bool IsAvailable => false;
    public override string? UnavailableReason => reason;
}

public static class AudioBackends
{
    private static Func<IAudioBackend>? factory;

    public static Func<IAudioBackend>? Factory
    {
        get => Volatile.Read(ref factory);
        set => Volatile.Write(ref factory, value);
    }

    public static IAudioBackend Create()
    {
        var create = Factory;
        if (create == null)
            return new UnavailableAudioBackend("none",
                "No audio backend is registered. Set AudioBackends.Factory before constructing an engine.");
        try
        {
            return create() ?? new UnavailableAudioBackend("none", "The audio backend factory returned null.");
        }
        catch (Exception exception)
        {
            return new UnavailableAudioBackend("none", $"The audio backend failed to start: {exception.Message}");
        }
    }
}
