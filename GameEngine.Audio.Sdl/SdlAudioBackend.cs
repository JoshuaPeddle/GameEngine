using GameEngine.Core.Systems;
using static SDL2.SDL;
using static SDL2.SDL_mixer;

namespace GameEngine.Audio.Sdl
{
    /// <summary>
    /// SDL2_mixer. Construct one on a desktop head and register it:
    /// <c>AudioBackends.Factory = SdlAudioBackend.Create;</c>. It never throws on a machine
    /// without a device or without the native mixer — it reports itself unavailable and the
    /// game runs silently.
    /// </summary>
    public sealed class SdlAudioBackend : IAudioBackend
    {
        private const int BgmChannel = 0;
        private const int LoopForever = -1;
        private const int SfxChannels = 31;

        private readonly Dictionary<string, IntPtr> _chunks = [];
        private readonly string _assetRoot;
        private readonly string? _unavailableReason;
        private bool _disposed;

        public SdlAudioBackend(string assetRoot = "assets")
        {
            _assetRoot = assetRoot;
            _unavailableReason = TryOpenDevice();
        }

        /// <summary>Always returns a backend; an unusable one carries the reason.</summary>
        public static IAudioBackend Create()
        {
            if (!IsSupportedPlatform)
                return new UnavailableAudioBackend(
                    "SDL2_mixer",
                    "SDL2_mixer has no native binary for this platform.");

            return new SdlAudioBackend();
        }

        public static bool IsSupportedPlatform =>
            !OperatingSystem.IsBrowser()
            && !OperatingSystem.IsAndroid()
            && !OperatingSystem.IsIOS();

        public string Name => "SDL2_mixer";

        public bool IsAvailable => !_disposed && _unavailableReason == null;

        public string? UnavailableReason => _disposed
            ? "The SDL audio backend has been disposed."
            : _unavailableReason;

        private string? TryOpenDevice()
        {
            try
            {
                if (SDL_Init(SDL_INIT_AUDIO) != 0)
                    return $"SDL could not start its audio subsystem: {SDL_GetError()}";

                if (Mix_OpenAudio(44100, MIX_DEFAULT_FORMAT, 2, 1024) != 0)
                    return $"SDL_mixer could not open an audio device: {Mix_GetError()}";

                if (Mix_AllocateChannels(1 + SfxChannels) != 1 + SfxChannels)
                    return $"SDL_mixer could not allocate mixing channels: {Mix_GetError()}";

                return null;
            }
            catch (DllNotFoundException exception)
            {
                return $"The SDL2_mixer native library is not present: {exception.Message}";
            }
            catch (Exception exception)
            {
                return $"SDL2_mixer failed to start: {exception.Message}";
            }
        }

        public void Play(string soundName, string path, SoundType soundType)
        {
            if (!IsAvailable)
                return;

            var chunk = GetOrDecode(soundName, path);
            if (chunk == IntPtr.Zero)
                return;

            if (soundType == SoundType.BGM)
            {
                Mix_PlayChannel(BgmChannel, chunk, LoopForever);
                return;
            }

            for (int channel = 1; channel <= SfxChannels; channel++)
            {
                if (Mix_Playing(channel) == 0)
                {
                    Mix_PlayChannel(channel, chunk, 0);
                    return;
                }
            }
        }

        // Decoded once per sound and reused. Freeing a chunk while a channel is still playing
        // it would be a use-after-free, so nothing is released until StopAll has run.
        private IntPtr GetOrDecode(string soundName, string path)
        {
            if (_chunks.TryGetValue(soundName, out var cached))
                return cached;

            var chunk = Mix_LoadWAV(Path.Combine(_assetRoot, path));
            _chunks[soundName] = chunk;
            return chunk;
        }

        public void StopAll()
        {
            if (_disposed || _unavailableReason != null)
                return;

            Mix_HaltChannel(-1);
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;

            if (_unavailableReason != null)
                return;

            Mix_HaltChannel(-1);

            foreach (var chunk in _chunks.Values)
            {
                if (chunk != IntPtr.Zero)
                    Mix_FreeChunk(chunk);
            }
            _chunks.Clear();

            Mix_CloseAudio();
            SDL_QuitSubSystem(SDL_INIT_AUDIO);
        }
    }
}
