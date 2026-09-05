using SkiaSharp;

namespace GameEngine.Core
{
    // Owns every texture it decodes and every scaled animation it derives, and outlives the
    // engines that draw from it: a render snapshot the host is still painting holds these
    // bitmaps by reference, so nothing here is released when an entity, an animation or a
    // whole scene goes away. Dispose an Assets only after the engines using it are disposed.
    public class Assets : IDisposable
    {
        private readonly Dictionary<string, SKBitmap> textures = [];
        private readonly Dictionary<string, Sound> sounds = [];
        private readonly Dictionary<string, Animation> animations = [];
        private readonly Dictionary<string, Animation> scaledAnimations = [];

        private readonly IAssetSource source;
        private bool disposed;

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

        // Scaled variants are cached rather than rebuilt, because each one decodes a bitmap of
        // its own: without this, every scene reload leaked one per scaled entity.
        public Animation GetAnimation(string name, Vec2 scaleSize)
        {
            var key = $"{name}@{scaleSize.X}x{scaleSize.Y}";

            if (!scaledAnimations.TryGetValue(key, out var scaled))
            {
                scaled = GetAnimation(name).AsScaledAnimation(scaleSize);
                scaledAnimations[key] = scaled;
            }

            return scaled;
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

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;

            foreach (var scaled in scaledAnimations.Values)
                scaled.Dispose();
            scaledAnimations.Clear();

            foreach (var texture in textures.Values)
                texture.Dispose();
            textures.Clear();

            animations.Clear();
            sounds.Clear();

            GC.SuppressFinalize(this);
        }
    }
}
