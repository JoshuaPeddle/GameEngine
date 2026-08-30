using GameEngine.Core;
using GameEngine.Core.Components;
using GameEngine.Core.Utils;

namespace GameEngine.Demo.Tests;

// The shipped manifest and levels are what a new author copies from, so they have to survive
// the strict loader rather than relying on unknown keys being ignored.
public class ShippedContentTests
{
    private static Assets LoadAssets() => new(AssetManifest.DefaultFileName, new FileAssetSource());

    [Test]
    public void TheAssetManifestLoads()
    {
        Assert.DoesNotThrow(() => LoadAssets());
    }

    [TestCaseSource(nameof(ShippedLevels))]
    public void EveryShippedLevelLoadsStrictly(string path)
    {
        var loader = new LevelLoader(new ComponentFactory(LoadAssets()));
        var level = LevelFile.LoadFromFile(path, new FileAssetSource());

        Assert.DoesNotThrow(() => loader.LoadLevel(level, new EntityManager(), new InputManager()));
    }

    [TestCaseSource(nameof(ShippedLevels))]
    public void EveryShippedLevelPointsAtTheSchema(string path)
    {
        Assert.That(LevelFile.LoadFromFile(path, new FileAssetSource()).Schema, Is.Not.Null,
            $"{Path.GetFileName(path)} should carry a \"$schema\" pointer so editors and agents can check it");
    }

    private static IEnumerable<string> ShippedLevels() =>
        Directory.EnumerateFiles(Path.Combine(AppContext.BaseDirectory, "levels"), "*.json")
            .OrderBy(path => path);
}
