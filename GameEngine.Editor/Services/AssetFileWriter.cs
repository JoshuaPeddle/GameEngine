using GameEngine.Editor.Models;
using System.Collections.Generic;

namespace GameEngine.Editor.Services
{
    public static class AssetFileWriter
    {
       

        public static void WriteAssetFiles(string ProjectFolder, AssetCollection assetCollection)
        {
            // First write the textures
            foreach (Texture texture in assetCollection.Textures)
            {
                string textureLine = "Texture " + texture.Name + " " + texture.Path;
                System.IO.File.WriteAllText(ProjectFolder + "/assets.txt", textureLine);
            }
            // More to come

        }
    }

    public class AssetCollection
    {
        public readonly List<Texture> Textures;
        // More to come
        public AssetCollection(List<Texture> textures)
        {
            Textures = textures;
        }
    }
}


/* Asset file example:
 * 
 * 
Texture TexJeep images/jeep.png
Texture TexGrenade images/grenade.png

Animation Grenade TexGrenade 4 250
Animation JeepBack TexJeep 1 1
 * 
 */