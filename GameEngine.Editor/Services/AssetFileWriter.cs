using GameEngine.Editor.Models;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace GameEngine.Editor.Services
{
    public static class AssetFileWriter
    {
        public static async Task WriteAssetFilesAsync(string projectFolder, AssetCollection assetCollection)
        {
            StringBuilder sb = new();

            foreach (Texture texture in assetCollection.Textures)
            {
                string textureRelativePath = GetTextureAssetFilePath(texture);
                string textureLine = $"Texture {texture.Name} {textureRelativePath}";
                sb.AppendLine(textureLine);
            }

            foreach (Animation animation in assetCollection.Animations)
            {
                string textureName = animation.Texture.Name;
                string animationLine = $"Animation {animation.Name} {textureName} {animation.FrameCount} {animation.Delay}";
                sb.AppendLine(animationLine);
            }
            string assetFilePath = Path.Combine(projectFolder, "assets.txt");
            await File.WriteAllTextAsync(assetFilePath, sb.ToString());

            await CopyTexturesToAssetsFolderAsync(projectFolder, assetCollection.Textures);
        }

        private static string GetTextureAssetFilePath(Texture texture)
        {
            return Path.Combine("textures", Path.GetFileName(texture.Path)).Replace("\\", "/");
        }

        private static string GetTextureRelativePath(Texture texture)
        {
            return Path.Combine("assets", "textures", Path.GetFileName(texture.Path));
        }

        private static async Task CopyTexturesToAssetsFolderAsync(string projectFolder, List<Texture> textures)
        {
            var tasks = new List<Task>();

            foreach (Texture texture in textures)
            {

                var bitmap = await texture.Bitmap;
                bitmap.Dispose();

                string relativePath = GetTextureRelativePath(texture);
                string destinationPath = Path.Combine(projectFolder, relativePath);

                string destinationDirectory = Path.GetDirectoryName(destinationPath);
                if (!Directory.Exists(destinationDirectory))
                    Directory.CreateDirectory(destinationDirectory);

                tasks.Add(CopyFileAsync(texture.Path, destinationPath));
            }

            await Task.WhenAll(tasks);
        }

        private static async Task CopyFileAsync(string sourceFilePath, string destinationFilePath)
        {
            using MemoryStream sourceMemoryStream = new();
            using (FileStream sourceStream = new(sourceFilePath, FileMode.Open, FileAccess.Read))
            {
                await sourceStream.CopyToAsync(sourceMemoryStream);
            }

            sourceMemoryStream.Position = 0;

            using FileStream destinationStream = new(destinationFilePath, FileMode.Create, FileAccess.Write);
            await sourceMemoryStream.CopyToAsync(destinationStream);
        }
    }

    public class AssetCollection
    {
        public List<Texture> Textures { get; }
        public List<Animation> Animations { get; }

        // Additional asset types can be added here
        public AssetCollection(List<Texture> textures, List<Animation> animations)
        {
            Textures = textures;
            Animations = animations;
        }
    }
}
