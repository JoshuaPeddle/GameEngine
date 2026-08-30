using SkiaSharp;

namespace GameEngine.Core
{
    public class Assets
    {
        private readonly Dictionary<string, SKBitmap> textures = [];
        private readonly Dictionary<string, Sound> sounds = [];
        private readonly Dictionary<string, Animation> animations = [];

        private readonly IAssetSource source;

        public Assets(string manifestPath, IAssetSource source)
        {
            ArgumentNullException.ThrowIfNull(source);
            this.source = source;
            Load(AssetManifest.Load(manifestPath, source));
        }

        public IAssetSource Source => source;

        public Stream Open(string path) => source.Open(path);

        private void Load(AssetManifest manifest)
        {
            foreach (var texture in manifest.Textures)
                LoadTexture(texture.Name, texture.Path);

            foreach (var animation in manifest.Animations)
                LoadAnimation(animation);

            foreach (var sound in manifest.Sounds)
                sounds.Add(sound.Name, new Sound(sound.Name, sound.Path));
        }

        public SKBitmap GetTexture(string name)
        {
            return textures[name];
        }
        public SKBitmap GetFont(string name)
        {
            throw new NotImplementedException();
            //return fonts[name];
        }
        public Animation GetAnimation(string name)
        {
            return animations[name];
        }

        public Sound GetSound(string name)
        {
            return sounds[name];
        }

        private void LoadTexture(string name, string path)
        {
            using var stream = source.Open(path);
            var bitmap = SKBitmap.Decode(stream)
                ?? throw new FailedToLoadTextureException($"Failed to load texture {name} from {path}");
            textures.Add(name, bitmap);
        }

        private void LoadAnimation(AnimationEntry entry)
        {
            if (!textures.TryGetValue(entry.Texture, out var texture))
                throw new InvalidDataException(
                    $"Animation '{entry.Name}' needs texture '{entry.Texture}', which the manifest does not declare. "
                    + $"Declared textures: {string.Join(", ", textures.Keys)}.");

            animations.Add(entry.Name, new Animation(texture, entry.Frames, entry.FrameDelayMs));
        }
    }
}
