using GameEngine.Core;
using GameEngine.Demo.Emberbrook;
using System.Text.Json;

namespace GameEngine.Demo.Tests;

public partial class SceneEmberbrookTests
{
    private static void ReadyForTrail(SceneEmberbrook scene)
    {
        ReadyForFeast(scene);
        scene.World.Restore(scene.World.Capture() with { RiverQuestStage = 2, Charm = RiverCharm.Might, Meals = 4 });
    }

    [Test]
    public void ForestUnlocksWoodcuttingAndBeaconsPersistWithoutDoubleCharging()
    {
        var (harness, scene) = Load();
        ReadyForTrail(scene);
        Visit(harness, scene, "elder");
        Visit(harness, scene, "trailGate");
        Assert.That(scene.World.Region, Is.EqualTo(Region.Forest));
        Visit(harness, scene, "beacon0");
        Assert.That(scene.World.BeaconCount, Is.Zero);
        Visit(harness, scene, "ash1");
        Visit(harness, scene, "ash1");
        Assert.That(scene.World.WoodcuttingXp, Is.EqualTo(20));
        Visit(harness, scene, "beacon0");
        Assert.That(scene.World.BeaconCount, Is.EqualTo(1));
        Assert.That(scene.World.Count(Item.Log), Is.Zero);
        Visit(harness, scene, "beacon0");
        Assert.That(scene.World.BeaconCount, Is.EqualTo(1));
        harness.PressAndRelease(GeKeys.S, 1);
        Visit(harness, scene, "trailExit");
        harness.PressAndRelease(GeKeys.L, 1);
        Assert.That(scene.World.Region, Is.EqualTo(Region.Forest));
        Assert.That(scene.World.LitBeacons, Is.EqualTo(1));
    }

    [Test]
    public void HartRequiresBeaconsAndDefeatRewardsOnlyOnce()
    {
        var (harness, scene) = Load();
        ReadyForTrail(scene);
        var world = scene.World;
        world.Restore(world.Capture() with { Region = Region.Forest, X = 19, Y = 7, TrailQuestStage = 1 });
        Visit(harness, scene, "hart");
        Assert.That(world.Sites.Single(s => s.Id == "hart").Hull, Is.EqualTo(80));
        world.Restore(world.Capture() with { LitBeacons = 7, CombatXp = 1500 });
        world.Sites.Single(s => s.Id == "hart").Hull = 1;
        Visit(harness, scene, "hart");
        Assert.That(world.HartDefeated, Is.True);
        world.Restore(world.Capture() with { Region = Region.Village, X = 5, Y = 5 });
        int coins = world.Coins;
        Visit(harness, scene, "elder");
        Assert.That(world.TrailQuestStage, Is.EqualTo(3));
        Assert.That(world.Coins, Is.EqualTo(coins + 120));
        Visit(harness, scene, "elder");
        Assert.That(world.Coins, Is.EqualTo(coins + 120));
        world.Restore(world.Capture());
        Assert.That(world.Sites.Single(s => s.Id == "hart").Hull, Is.Zero);
    }

    [Test]
    public void ExpandedBankConservesEveryItemAndRespectsPackCapacity()
    {
        var (harness, scene) = Load();
        var world = scene.World;
        world.Restore(world.Capture() with { Ore = 2, Bars = 2, Trout = 2, Meals = 2, Logs = 2 });
        world.DepositAll();
        Assert.That(world.PackUsed, Is.EqualTo(9));
        Visit(harness, scene, "bank");
        world.DepositAll();
        Assert.That(world.BankTotal, Is.EqualTo(12));
        Assert.That(world.PackUsed, Is.Zero);
        var saved = world.Capture();
        world.Restore(saved);
        foreach (var item in Enum.GetValues<Item>()) world.Withdraw(item);
        Assert.That(world.PackUsed, Is.EqualTo(9));
        Assert.That(world.BankTotal, Is.Zero);
        foreach (var item in Enum.GetValues<Item>()) Assert.That(world.Count(item), Is.EqualTo(item == Item.SmokedTrout ? 0 : 2));
    }

    [Test]
    public void CurrentSaveRejectsImpossibleForestProgress()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            var expected = new EmberbrookWorld().Capture();
            var store = new EmberbrookFileSaveStore(path);
            store.Write(expected);
            Assert.That(store.Read(), Is.EqualTo(expected));
            foreach (var invalid in new[] { expected with { LitBeacons = 8 }, expected with { TrailQuestStage = 2 }, expected with { BankLogs = -1 }, expected with { Region = Region.Forest } })
                Assert.Throws<InvalidDataException>(() => store.Write(invalid));
        }
        finally { File.Delete(path); }
    }
}
