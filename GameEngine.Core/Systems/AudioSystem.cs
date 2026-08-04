using static SDL2.SDL;
using static SDL2.SDL_mixer;

namespace GameEngine.Core.Systems
{
    public enum SoundType { BGM, SoundEffect }

    public class AudioSystem : ISystem, IDisposable
    {
        private const int BgmChannel = 0;
        private const int LoopForever = -1;

        private readonly int _numSfxChannels = 31;
        private readonly Dictionary<string, IntPtr> _chunkCache = [];

        private readonly IAssetSource _assetSource;
        private Assets? _assets;
        private bool _disposed;

        public static bool IsSupportedPlatform =>
            !OperatingSystem.IsBrowser()
            && !OperatingSystem.IsAndroid()
            && !OperatingSystem.IsIOS();

        public static AudioSystem? TryCreate(IAssetSource assetSource)
        {
            if (!IsSupportedPlatform)
                return null;

            try
            {
                return new AudioSystem(assetSource);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public AudioSystem(IAssetSource assetSource)
        {
            ArgumentNullException.ThrowIfNull(assetSource);
            _assetSource = assetSource;
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

        }

        private IntPtr GetOrLoadChunk(string assetName)
        {
            if (_chunkCache.TryGetValue(assetName, out IntPtr cached))
                return cached;

            _assets ??= new Assets("assets.txt", _assetSource);
            var sound = _assets.GetSound(assetName);

            IntPtr chunk = Mix_LoadWAV(Path.Combine("assets", sound.Path));
            if (chunk == IntPtr.Zero)
                throw new FailedToLoadSoundException(
                    $"Failed to load sound '{assetName}' from '{sound.Path}': {Mix_GetError()}");

            _chunkCache[assetName] = chunk;
            return chunk;
        }

        public void Update(EntityManager entityManager, double deltaSeconds) { }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;

            Mix_HaltChannel(-1);

            foreach (IntPtr chunk in _chunkCache.Values)
                Mix_FreeChunk(chunk);
            _chunkCache.Clear();

            Mix_CloseAudio();
            SDL_QuitSubSystem(SDL_INIT_AUDIO);
            GC.SuppressFinalize(this);
        }
    }
}
