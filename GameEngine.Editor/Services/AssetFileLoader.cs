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
                return new AssetCollection([]);
            string[] lines = File.ReadAllLines(assetFilePath);
            AssetCollection assetCollection = new([]);
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
                    //string animationPath = Path.Combine(projectFileFolder, parts[2]);
                    //assetCollection.Animations.Add(new Animation(assetName, animationPath));
                }
            }
            return assetCollection;
        }
    }
}
