using SkiaSharp;

namespace GameEngine.Core
{
    public class Assets
    {
        private readonly Dictionary<string, SKBitmap> textures = [];
        private Dictionary<string, Sound> sounds = new(); 
        private readonly Dictionary<string, Animation> animations = [];

        public static Func<string, Stream>? _fileFetcher;

        public static Stream OpenAsset(string path) => _fileFetcher?.Invoke(path) ?? File.OpenRead(path);

        public Assets(string path)
        {
            LoadFromPath(path);
        }

        private void LoadFromPath(string path)
        {
            // Read each line
            // If the line begins with Texture, load a texture
            // If the line begins with Sound, load a sound
            // If the line begins with Font, load a font
            // If the line begins with Animation, load an animation
            if (_fileFetcher is not null) // Allow a custom file fetcher to be used. Useful for Android and iOS.
            {
                using var stream = _fileFetcher(path);
                using var reader = new StreamReader(stream);

                string? line;
                while ((line = reader.ReadLine()) != null)
                    ParseLine(line);
            }
            else
            {
                var lines = File.ReadAllLines(path);
                foreach (var line in lines)
                {
                    ParseLine(line);
                }
            }
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
            if (_fileFetcher is not null)
            {
                var stream = _fileFetcher(path);
                var bitmap = SKBitmap.Decode(stream) ?? throw new FailedToLoadTextureException($"Failed to load texture {name} from {path}");
                textures.Add(name, bitmap);
                return;
            }
            else{
                var bitmap = SKBitmap.Decode(Path.Combine("assets", path)) ?? throw new FailedToLoadTextureException($"Failed to load texture {name} from {path}");
                textures.Add(name, bitmap);
            }
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
