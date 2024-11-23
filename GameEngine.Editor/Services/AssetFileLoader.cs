using Avalonia.Media.Imaging;
using GameEngine.Editor.Models;
using System.IO;
using System.Threading.Tasks;

namespace GameEngine.Editor.Services
{
    public static class AssetFileLoader
    {
        public static AssetCollection LoadAssetCollection(string projectFileFolder)
        {
            string assetFilePath = Path.Combine(projectFileFolder, "assets.txt");
            if (!File.Exists(assetFilePath))
                return new AssetCollection([], []);
            string[] lines = File.ReadAllLines(assetFilePath);
            AssetCollection assetCollection = new([], []);
            foreach (string line in lines)
            {
                string[] parts = line.Split(' ');
                if (parts.Length < 2)
                    continue;
                string assetType = parts[0];
                string assetName = parts[1];
                if (assetType == "Texture")
                {
                    string texturePath = Path.Combine(projectFileFolder, "assets", parts[2]);
                    var bitmap = new Bitmap(texturePath);

                    assetCollection.Textures.Add(new Texture(assetName, texturePath, Task.FromResult(bitmap)));
                }
                else if (assetType == "Animation")
                {
                    // Animation [Name] [TextureName] [FrameCount] [Delay]
                    string textureName = parts[2];
                    Texture? texture = assetCollection.Textures.Find(t => t.Name == textureName);
                    if (texture == null)
                        continue;
                    int frameCount = int.Parse(parts[3]);
                    int delay = int.Parse(parts[4]);
                    assetCollection.Animations.Add(new Animation(assetName, texture, frameCount, delay));

                }
            }
            return assetCollection;
        }
    }
}
