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

    [Test]
    public void MultiLevelScene_ReachesEveryLevelItLists()
    {
        var scene = new MultiLevelScene();
        var harness = SceneHarness.Load(scene);
        harness.Run(2);

        for (var level = 1; level <= MultiLevelScene.LevelCount; level++)
        {
            Assert.That(scene.CurrentLevel, Is.EqualTo(level));
            Assert.That(harness.Entities.GetEntityWithTag("player"), Is.Not.Null,
                $"level {level} should declare a player");

            harness.PressAndRelease(GeKeys.N);
            harness.Run(2);
        }

        Assert.That(scene.CurrentLevel, Is.EqualTo(MultiLevelScene.LevelCount),
            "N past the last level should stay put");
    }

    [Test]
    public void MultiLevelScene_GoesBackWithP()
    {
        var scene = new MultiLevelScene();
        var harness = SceneHarness.Load(scene);
        harness.Run(2);

        harness.PressAndRelease(GeKeys.N);
        harness.Run(2);
        Assert.That(scene.CurrentLevel, Is.EqualTo(2));

        harness.PressAndRelease(GeKeys.P);
        harness.Run(2);

        Assert.Multiple(() =>
        {
            Assert.That(scene.CurrentLevel, Is.EqualTo(1));
            Assert.That(harness.Entities.GetEntityWithTag("player"), Is.Not.Null);
        });
    }

    [Test]
    public void EveryShippedLevelHasSomethingInIt()
    {
        foreach (var path in ShippedLevels())
        {
            Assert.That(LevelFile.LoadFromFile(path, new FileAssetSource()).Entities, Is.Not.Empty,
                $"{Path.GetFileName(path)} declares no entities");
        }
    }

    private static IEnumerable<string> ShippedLevels() =>
        Directory.EnumerateFiles(Path.Combine(AppContext.BaseDirectory, "levels"), "*.json")
            .OrderBy(path => path);
}
