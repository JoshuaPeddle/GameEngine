using Avalonia.Media.Imaging;
using System.Threading.Tasks;

namespace GameEngine.Editor.Models
{
    public class Texture
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public Task<Bitmap> Bitmap { get; set; }
    }
}
