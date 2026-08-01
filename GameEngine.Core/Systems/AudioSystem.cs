using static SDL2.SDL; // https://github.com/ppy/SDL2-CS/blob/master/src/SDL2_mixer.cs
using static SDL2.SDL_mixer; // https://github.com/libsdl-org/SDL_mixer/

namespace GameEngine.Core.Systems
{
    public enum SoundType { BGM, SoundEffect }

    /*
     * The AudioPlayer class handles audio playback in the game using the SDL2_mixer library. 
     * It supports background music (BGM) and sound effects, managing active audio channels 
     * to ensure smooth playback. 
     * 
     * - Background music plays on channel 0, while sound effects use channels 1 to _numSFXChannels.
     * - If all sound effect channels are occupied, new effects won't play.
     * - The Update method runs every frame to clean up finished audio, freeing memory as needed.
     * - Initialized with game assets, the AudioPlayer keeps track of audio via active channels.
     * 
     * Todo: Volume control, looping, and other audio features
     */
    public class AudioSystem : ISystem, IDisposable
    {
        private Assets? _assets;
        private List<(int channel, IntPtr chunkPtr)> _activeChannels = new List<(int, IntPtr)>();
        private int _numSFXChannels = 31;

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
            if (Mix_AllocateChannels(1 + _numSFXChannels) != 1 + _numSFXChannels)
                throw new Exception($"Mix_AllocateChannels Error: {Mix_GetError()}");
        }

        public void Play(string assetName, SoundType soundType)
        {
            _assets ??= new Assets("assets.txt");

            var sound = _assets.GetSound(assetName);

            var chunkPtr = Mix_LoadWAV(Path.Combine("assets", sound.Path)); //Mix_QuickLoad_WAV

            if (soundType == SoundType.BGM) // BGM always plays on channel 0
            {   
                int channelId = Mix_PlayChannel(0, chunkPtr, 100); // Todo: Figure out a better way to loop bgm
                _activeChannels.Add((channelId, chunkPtr));
            }

            else if (soundType == SoundType.SoundEffect)
            {   
                for (int i = 1; i < 1 + _numSFXChannels; i++)
                {
                    if (Mix_Playing(i) == 0)
                    {
                        int channelId = Mix_PlayChannel(i, chunkPtr, 0);
                        _activeChannels.Add((channelId, chunkPtr));
                        return;
                    }
                }
            }
        }

        public void Update(EntityManager entityManager, double deltaSeconds)
        {
            for (int i = _activeChannels.Count - 1; i >= 0; i--)
            {
                var (channelId, chunkPtr) = _activeChannels[i];
                if (Mix_Playing(channelId) == 0)
                {
                    Mix_FreeChunk(chunkPtr);
                    _activeChannels.RemoveAt(i);
                }
            }
        }

        public void Dispose()
        {
            foreach (var (_, chunkPtr) in _activeChannels)
            {
                Mix_FreeChunk(chunkPtr);
            }
            _activeChannels.Clear();
            Mix_CloseAudio();
            SDL_QuitSubSystem(SDL_INIT_AUDIO);
            GC.SuppressFinalize(this);
        }
    }
}
