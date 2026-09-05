namespace GameEngine.Core.Systems
{
    /// <summary>
    /// Whatever actually makes noise on this platform. Core defines the shape and never
    /// references a native audio library: a host supplies a backend through
    /// <see cref="AudioBackends.Factory"/>, and a platform with no backend gets an audio
    /// service that reports itself unavailable rather than one that is silently missing.
    /// </summary>
    public interface IAudioBackend : IDisposable
    {
        /// <summary>Names the backend in diagnostics — "SDL2_mixer", "silent", and so on.</summary>
        string Name { get; }

        bool IsAvailable { get; }

        /// <summary>Why this backend cannot play, in words a game author can act on.</summary>
        string? UnavailableReason { get; }

        /// <summary>
        /// Plays a sound, decoding it the first time and reusing the decoded copy afterwards.
        /// Called on the engine thread.
        /// </summary>
        void Play(string soundName, string path, SoundType soundType);

        void StopAll();
    }

    /// <summary>
    /// The backend a platform with no audio integration gets. It is always available and
    /// always silent, which is what keeps a game running rather than crashing where sound
    /// cannot be produced — and what a headless test wants.
    /// </summary>
    public class SilentAudioBackend : IAudioBackend
    {
        public string Name => "silent";

        public virtual bool IsAvailable => true;

        public virtual string? UnavailableReason => null;

        public virtual void Play(string soundName, string path, SoundType soundType) { }

        public virtual void StopAll() { }

        public virtual void Dispose() => GC.SuppressFinalize(this);
    }

    /// <summary>
    /// A backend that cannot play, carrying the reason. Hosts return one of these rather than
    /// throwing when a device or native library is missing.
    /// </summary>
    public sealed class UnavailableAudioBackend(string name, string reason) : SilentAudioBackend
    {
        public override bool IsAvailable => false;

        public override string? UnavailableReason => reason;

        public new string Name => name;
    }

    /// <summary>
    /// Where a host registers its audio integration, before the first engine is constructed.
    /// A factory rather than an instance, because each engine owns and disposes the backend it
    /// is given.
    /// </summary>
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
                return new UnavailableAudioBackend(
                    "none",
                    "No audio backend is registered. A host registers one by setting "
                    + "AudioBackends.Factory before the first engine is constructed.");

            try
            {
                return create() ?? new UnavailableAudioBackend("none", "The registered audio backend factory returned null.");
            }
            catch (Exception exception)
            {
                return new UnavailableAudioBackend("none", $"The registered audio backend failed to start: {exception.Message}");
            }
        }
    }
}
