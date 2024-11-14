using SkiaSharp;
using static GameEngine.Core.Exceptions;

namespace GameEngine.Core
{
    public class Assets
    {

        private Dictionary<string, SKBitmap> textures = new();
        //private Dictionary<string, Sound> sounds = new(); // TODO: Implement this
        private Dictionary<string, Font> fonts = new();
        private Dictionary<string, Animation> animations = new(); // TODO: Implement this

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

            var lines = System.IO.File.ReadAllLines(path);
            foreach (var line in lines)
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
            }
        }

        public SKBitmap GetTexture(string name)
        {
            return textures[name];
        }
        public SKBitmap GetFont(string name) {
            throw new NotImplementedException();
            //return fonts[name];
        }
        public Animation GetAnimation(string name)
        {
            return animations[name];
        }

        private void LoadTexture(string name, string path)
        {
            var bitmap = SKBitmap.Decode(Path.Combine("assets", path)) ?? throw new FailedToLoadTextureException($"Failed to load texture {name} from {path}");
            textures.Add(name, bitmap);
        }

        private void LoadFont(string name, string path)
        {
            //fonts.Add(name, new Font(path));
        }

        private void LoadAnimation(string name, string textureName, int frames, int delay)
        {
            animations.Add(name, new Animation(name, textures[textureName], frames, delay));
        }
    }
}
