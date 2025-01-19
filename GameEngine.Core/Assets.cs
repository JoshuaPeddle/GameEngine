using SkiaSharp;
using static GameEngine.Core.Exceptions;

namespace GameEngine.Core
{
    public class Assets
    {
        private readonly Dictionary<string, SKBitmap> textures = [];
        private Dictionary<string, Sound> sounds = new(); 
        private readonly Dictionary<string, Animation> animations = [];

        public static Func<string, Stream>? _fileFetcher;

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
                var stream = _fileFetcher(path);
                using (var reader = new StreamReader(stream))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        ParseLine(line);
                    }
                }
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
            var parts = line.Split(' ');
            if (parts[0] == "Texture")
            {
                LoadTexture(parts[1], parts[2]);
            }
            else if (parts[0] == "Font")
            {
                LoadFont(parts[1], parts[2]);
            }
            else if (parts[0] == "Animation")
            {
                LoadAnimation(parts[1], parts[2], int.Parse(parts[3]), int.Parse(parts[4]));
            }
            else if (parts[0] == "Sound")
            {
                LoadSound(parts[1], parts[2]);
            }
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
