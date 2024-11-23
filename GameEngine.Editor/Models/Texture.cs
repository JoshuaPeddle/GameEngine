using Avalonia.Media.Imaging;
using System.Threading.Tasks;

namespace GameEngine.Editor.Models
{
    public class Texture
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public Task<Bitmap> Bitmap { get; set; }

        public Texture(string name, string path, Task<Bitmap> bitmap)
        {
            Name = name;
            Path = path;
            Bitmap = bitmap;
        }

        public Texture()
        {
        }
    }
}
