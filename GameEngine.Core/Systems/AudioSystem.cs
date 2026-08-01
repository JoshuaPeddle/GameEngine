using static GameEngine.Core.Exceptions;
using static SDL2.SDL; // https://github.com/ppy/SDL2-CS/blob/master/src/SDL2_mixer.cs
using static SDL2.SDL_mixer; // https://github.com/libsdl-org/SDL_mixer/

namespace GameEngine.Core.Systems
{
    public enum SoundType { BGM, SoundEffect }

    /// <summary>
    /// Audio playback through SDL2_mixer.
    /// <para>
    /// Background music plays on channel 0; sound effects take the first free channel from 1
    /// upwards, and are dropped when every channel is busy. Decoded chunks are cached for the
    /// lifetime of the system, so playing a sound is a pointer lookup rather than a file read.
    /// </para>
    /// <para>
    /// Construct through <see cref="TryCreate"/> rather than the constructor unless failing to
    /// initialise should be fatal: SDL2_mixer is unavailable on browser and mobile, and even on
    /// desktop it fails when there is no audio device or the native library is missing.
    /// </para>
    /// </summary>
    public class AudioSystem : ISystem, IDisposable
    {
        private const int BgmChannel = 0;
        private const int LoopForever = -1;

        private readonly int _numSfxChannels = 31;
        private readonly Dictionary<string, IntPtr> _chunkCache = [];

        private Assets? _assets;
        private bool _disposed;

        /// <summary>
        /// Platforms where SDL2_mixer can run at all. Browser and mobile have no usable
        /// backend, and attempting to initialise there fails inside native code.
        /// </summary>
        public static bool IsSupportedPlatform =>
            !OperatingSystem.IsBrowser()
            && !OperatingSystem.IsAndroid()
            && !OperatingSystem.IsIOS();

        /// <summary>
        /// Returns a ready audio system, or null when audio is unavailable on this platform or
        /// fails to initialise. Never throws — a machine with no sound device should still run
        /// the game.
        /// </summary>
        public static AudioSystem? TryCreate()
        {
            if (!IsSupportedPlatform)
                return null;

            try
            {
                return new AudioSystem();
            }
            catch (Exception)
            {
                // No device, no native library, or a mixer that refused the format. Silence is
                // the right outcome; taking the engine down with it is not.
                return null;
            }
        }

        public AudioSystem()
        {
            InitializeSDL();
        }

        private void InitializeSDL()
        {
            if (SDL_Init(SDL_INIT_AUDIO) != 0)
                throw new Exception($"SDL_Init Error: {SDL_GetError()}");
            if (Mix_OpenAudio(44100, MIX_DEFAULT_FORMAT, 2, 1024) != 0)
                throw new Exception($"Mix_OpenAudio Error: {Mix_GetError()}");
            if (Mix_AllocateChannels(1 + _numSfxChannels) != 1 + _numSfxChannels)
                throw new Exception($"Mix_AllocateChannels Error: {Mix_GetError()}");
        }

        public void Play(string assetName, SoundType soundType)
        {
            if (_disposed)
                return;

            IntPtr chunk = GetOrLoadChunk(assetName);

            if (soundType == SoundType.BGM)
            {
                // -1 loops indefinitely. This used to pass 100, which was a stand-in for
                // "enough times that nobody notices".
                Mix_PlayChannel(BgmChannel, chunk, LoopForever);
                return;
            }

            for (int channel = 1; channel <= _numSfxChannels; channel++)
            {
                if (Mix_Playing(channel) == 0)
                {
                    Mix_PlayChannel(channel, chunk, 0);
                    return;
                }
            }

            // Every effect channel is busy. Dropping this sound beats cutting one that is
            // already playing.
        }

        /// <summary>
        /// Decoded chunks are cached and reused. This previously called Mix_LoadWAV on every
        /// single play, putting a file read and a decode on the engine thread each time a
        /// sound effect fired.
        /// </summary>
        private IntPtr GetOrLoadChunk(string assetName)
        {
            if (_chunkCache.TryGetValue(assetName, out IntPtr cached))
                return cached;

            _assets ??= new Assets("assets.txt");
            var sound = _assets.GetSound(assetName);

            IntPtr chunk = Mix_LoadWAV(Path.Combine("assets", sound.Path));
            if (chunk == IntPtr.Zero)
                throw new FailedToLoadSoundException(
                    $"Failed to load sound '{assetName}' from '{sound.Path}': {Mix_GetError()}");

            _chunkCache[assetName] = chunk;
            return chunk;
        }

        /// <summary>
        /// Nothing to reclaim per frame: chunks live in the cache until disposal, and SDL
        /// frees channels itself when playback ends.
        /// </summary>
        public void Update(EntityManager entityManager, double deltaSeconds) { }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;

            Mix_HaltChannel(-1); // stop playback before the chunks it is reading go away

            foreach (IntPtr chunk in _chunkCache.Values)
                Mix_FreeChunk(chunk);
            _chunkCache.Clear();

            Mix_CloseAudio();
            SDL_QuitSubSystem(SDL_INIT_AUDIO);
            GC.SuppressFinalize(this);
        }
    }
}
