using GameEngine.Core;
using GameEngine.Editor.Models;
using Animation = GameEngine.Editor.Models.Animation;
using Sound = GameEngine.Editor.Models.Sound;
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
            var manifest = new AssetManifest { Schema = ExistingSchema(projectFolder) };

            foreach (Texture texture in assetCollection.Textures)
                manifest.Textures.Add(new TextureEntry(texture.Name, GetTextureAssetFilePath(texture)));

            foreach (Animation animation in assetCollection.Animations)
                manifest.Animations.Add(new AnimationEntry(
                    animation.Name, animation.Texture.Name, animation.FrameCount, animation.Delay));

            foreach (Sound sound in assetCollection.Sounds)
            {
                string soundRelativePath = Path.Combine("sounds", Path.GetFileName(sound.Path)).Replace("\\", "/");
                manifest.Sounds.Add(new SoundEntry(sound.Name, soundRelativePath));
            }

            string assetFilePath = Path.Combine(projectFolder, AssetManifest.DefaultFileName);
            await File.WriteAllTextAsync(assetFilePath, manifest.ToJson());

            await CopyTexturesToAssetsFolderAsync(projectFolder, assetCollection.Textures);
        }

        // Rewriting the manifest must not drop the "$schema" pointer the project was set up with.
        private static string? ExistingSchema(string projectFolder)
        {
            var path = Path.Combine(projectFolder, AssetManifest.DefaultFileName);
            if (!File.Exists(path))
                return null;

            try
            {
                return AssetManifest.ParseJson(File.ReadAllText(path)).Schema;
            }
            catch (System.Exception)
            {
                return null;
            }
        }

        private static string GetTextureAssetFilePath(Texture texture)
        {
            return Path.Combine("images", Path.GetFileName(texture.Path)).Replace("\\", "/");
        }

        private static string GetTextureRelativePath(Texture texture)
        {
            return Path.Combine("assets", "images", Path.GetFileName(texture.Path));
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
        public List<Sound> Sounds { get; }
        public AssetCollection(List<Texture> textures, List<Animation> animations, List<Sound> sounds)
        {
            Textures = textures;
            Animations = animations;
            Sounds = sounds;
        }
    }
}
