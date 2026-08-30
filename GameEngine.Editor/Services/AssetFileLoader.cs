using Avalonia.Media.Imaging;
using GameEngine.Core;
using GameEngine.Editor.Models;
using Animation = GameEngine.Editor.Models.Animation;
using Sound = GameEngine.Editor.Models.Sound;
using System.IO;
using System.Threading.Tasks;

namespace GameEngine.Editor.Services
{
    public static class AssetFileLoader
    {
        public async static Task<AssetCollection> LoadAssetCollectionAsync(string projectFileFolder)
        {
            var manifestPath = ManifestPath(projectFileFolder);
            if (manifestPath == null)
                return new AssetCollection([], [], []);

            var text = await File.ReadAllTextAsync(manifestPath);
            var manifest = manifestPath.EndsWith(".json", System.StringComparison.OrdinalIgnoreCase)
                ? AssetManifest.ParseJson(text)
                : AssetManifest.ParseLines(text);

            var assetCollection = new AssetCollection([], [], []);

            foreach (var texture in manifest.Textures)
            {
                var texturePath = Path.Combine(projectFileFolder, "assets", texture.Path);
                assetCollection.Textures.Add(
                    new Texture(texture.Name, texturePath, Task.FromResult(new Bitmap(texturePath))));
            }

            foreach (var animation in manifest.Animations)
            {
                var texture = assetCollection.Textures.Find(t => t.Name == animation.Texture);
                if (texture == null)
                    continue;

                assetCollection.Animations.Add(
                    new Animation(animation.Name, texture, animation.Frames, (int)animation.FrameDelayMs));
            }

            foreach (var sound in manifest.Sounds)
            {
                assetCollection.Sounds.Add(
                    new Sound(sound.Name, Path.Combine(projectFileFolder, "assets", sound.Path)));
            }

            return assetCollection;
        }

        internal static string? ManifestPath(string projectFileFolder)
        {
            var current = Path.Combine(projectFileFolder, AssetManifest.DefaultFileName);
            if (File.Exists(current))
                return current;

            var legacy = Path.Combine(projectFileFolder, AssetManifest.LegacyFileName);
            return File.Exists(legacy) ? legacy : null;
        }
    }
}
