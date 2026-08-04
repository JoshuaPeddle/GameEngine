using SkiaSharp;

namespace GameEngine.Core
{
    public class Assets
    {
        private readonly Dictionary<string, SKBitmap> textures = [];
        private Dictionary<string, Sound> sounds = new(); 
        private readonly Dictionary<string, Animation> animations = [];

        private readonly IAssetSource source;

        public Assets(string manifestPath, IAssetSource source)
        {
            ArgumentNullException.ThrowIfNull(source);
            this.source = source;
            LoadFromPath(manifestPath);
        }

        public IAssetSource Source => source;

        public Stream Open(string path) => source.Open(path);

        private void LoadFromPath(string path)
        {
            using var stream = source.Open(path);
            using var reader = new StreamReader(stream);

            string? line;
            while ((line = reader.ReadLine()) != null)
                ParseLine(line);
        }

        private void ParseLine(string line)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
                return;

            var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            switch (parts[0])
            {
                case "Texture":
                    Require(parts, 3, line);
                    LoadTexture(parts[1], parts[2]);
                    break;
                case "Font":
                    Require(parts, 3, line);
                    LoadFont(parts[1], parts[2]);
                    break;
                case "Animation":
                    Require(parts, 5, line);
                    LoadAnimation(parts[1], parts[2], int.Parse(parts[3]), int.Parse(parts[4]));
                    break;
                case "Sound":
                    Require(parts, 3, line);
                    LoadSound(parts[1], parts[2]);
                    break;
                default:
                    throw new InvalidDataException($"Unknown asset directive '{parts[0]}' in: {line}");
            }
        }

        private static void Require(string[] parts, int count, string line)
        {
            if (parts.Length < count)
                throw new InvalidDataException(
                    $"Asset directive '{parts[0]}' needs {count - 1} arguments but got {parts.Length - 1} in: {line}");
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

        private void LoadSound(string name, string path)
        {
            sounds.Add(name, new Sound(name, path));
        }

        private void LoadFont(string name, string path)
        {
            //fonts.Add(name, new Font(path));
        }

        private void LoadAnimation(string name, string textureName, int frames, int delay)
        {
            animations.Add(name, new Animation(textures[textureName], frames, delay));
        }
    }
}
