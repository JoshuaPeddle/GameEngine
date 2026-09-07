using GameEngine.Core;
using GameEngine.Demo.Emberbrook;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace GameEngine.Demo.Tests;

public partial class SceneEmberbrookTests
{
    private static void ReadyForFeast(SceneEmberbrook scene)
    {
        scene.World.Restore(scene.World.Capture() with
        {
            QuestStage = 2, RuinQuestStage = 3, GuardianDefeated = true,
            HasSword = true, HasShield = true, Coins = 160, CombatXp = 75, Kills = 3
        });
    }

    [Test]
    public void RiversBountyPlaysFromFishingThroughCookingAndRewardsExactlyOnce()
    {
        var (harness, scene) = Load();
        ReadyForFeast(scene);
        Visit(harness, scene, "elder");
        Assert.That(scene.World.RiverQuestStage, Is.EqualTo(1));
        for (int i = 0; i < 5; i++) Visit(harness, scene, i % 2 == 0 ? "fish1" : "fish2");
        Assert.That(scene.World.Count(Item.Trout), Is.EqualTo(5));
        Assert.That(scene.World.FishingXp, Is.EqualTo(50));
        Assert.That(scene.World.HasPearl, Is.True);
        Assert.That(scene.World.PackUsed, Is.EqualTo(3), "the pearl needs no inventory slot");
        for (int i = 0; i < 3; i++) Visit(harness, scene, "campfire");
        Assert.That(scene.World.Count(Item.Meal), Is.EqualTo(3));
        Assert.That(scene.World.CookingLevel, Is.EqualTo(2));
        int coins = scene.World.Coins;
        Visit(harness, scene, "elder");
        Assert.That(scene.World.RiverQuestStage, Is.EqualTo(2));
        Assert.That(scene.World.Coins, Is.EqualTo(coins + 60));
        Assert.That(scene.World.Count(Item.Meal), Is.Zero);
        Assert.That(scene.World.Count(Item.Trout), Is.EqualTo(2));
        Assert.That(scene.World.HasPearl, Is.False);
        Assert.That(scene.World.Charm, Is.EqualTo(RiverCharm.Might));
        Visit(harness, scene, "elder");
        Assert.That(scene.World.Coins, Is.EqualTo(coins + 60));
        Visit(harness, scene, "fish1");
        Assert.That(scene.World.HasPearl, Is.False, "a completed quest must not generate more pearls");
        harness.PressAndRelease(GeKeys.T, 1);
        Assert.That(scene.World.Charm, Is.EqualTo(RiverCharm.Shelter));
        harness.PressAndRelease(GeKeys.S, 1);
        var saved = scene.World.Capture();
        harness.PressAndRelease(GeKeys.T, 1);
        harness.PressAndRelease(GeKeys.L, 1);
        Assert.That(scene.World.Capture(), Is.EqualTo(saved));
    }

    [Test]
    public void FeastRequiresBothMealsAndPearlWithoutTakingPartialOfferings()
    {
        var (harness, scene) = Load();
        ReadyForFeast(scene);
        Visit(harness, scene, "elder");
        scene.World.Restore(scene.World.Capture() with { Meals = 3 });
        Visit(harness, scene, "elder");
        Assert.That(scene.World.Count(Item.Meal), Is.EqualTo(3));
        Assert.That(scene.World.RiverQuestStage, Is.EqualTo(1));
        scene.World.Restore(scene.World.Capture() with { Meals = 2, HasPearl = true, FishingXp = 50 });
        Visit(harness, scene, "elder");
        Assert.That(scene.World.Count(Item.Meal), Is.EqualTo(2));
        Assert.That(scene.World.HasPearl, Is.True);
        Assert.That(scene.World.RiverQuestStage, Is.EqualTo(1));
    }

    [Test]
    public void RepeatedFishingRespectsCapacityCooldownAndCancellation()
    {
        var (harness, scene) = Load();
        scene.World.Restore(scene.World.Capture() with { Trout = 40 });
        harness.PressAndRelease(GeKeys.M, 1);
        Click(harness, SceneEmberbrook.Position(new Cell(11, 3)));
        harness.Run(4000);
        Assert.That(scene.World.Count(Item.Trout), Is.EqualTo(50));
        Assert.That(scene.World.FishingXp, Is.EqualTo(100));
        Assert.That(scene.World.Activity, Is.EqualTo("Idle"));
        scene.World.Restore(scene.World.Capture() with { Trout = 0, X = 10, Y = 3 });
        Click(harness, SceneEmberbrook.Position(new Cell(11, 3)));
        harness.Engine.Tick(20);
        Assert.That(scene.World.Count(Item.Trout), Is.EqualTo(1));
        int xp = scene.World.FishingXp;
        Click(harness, SceneEmberbrook.Position(new Cell(9, 7)));
        harness.Run(400);
        Assert.That(scene.World.FishingXp, Is.EqualTo(xp));
        Assert.That(scene.World.Player, Is.EqualTo(new Cell(9, 7)));
    }

    [Test]
    public void CookingTransformsAFullPackWithoutLosingItemsAndNeverGrantsFreeXp()
    {
        var (harness, scene) = Load();
        scene.World.Restore(scene.World.Capture() with { Trout = 1, Meals = 9 });
        Visit(harness, scene, "campfire");
        Assert.That(scene.World.Count(Item.Trout), Is.Zero);
        Assert.That(scene.World.Count(Item.Meal), Is.EqualTo(10));
        Assert.That(scene.World.PackUsed, Is.EqualTo(12));
        Assert.That(scene.World.CookingXp, Is.EqualTo(8));
        scene.World.Restore(scene.World.Capture() with { Trout = 0, Meals = 10 });
        Visit(harness, scene, "campfire");
        Assert.That(scene.World.CookingXp, Is.EqualTo(8));
        Assert.That(scene.World.Count(Item.Meal), Is.EqualTo(10));
    }

    [Test]
    public void EatingSelectsAppropriateFoodAndCookingImprovesHealing()
    {
        var (harness, scene) = Load();
        scene.World.Restore(scene.World.Capture() with { Hull = 4, Rations = 1, Meals = 1, CookingXp = 48 });
        harness.PressAndRelease(GeKeys.E, 1);
        Assert.That(scene.World.Hull, Is.EqualTo(20));
        Assert.That(scene.World.Count(Item.Meal), Is.Zero);
        Assert.That(scene.World.Count(Item.Ration), Is.EqualTo(1));
        harness.PressAndRelease(GeKeys.E, 1);
        Assert.That(scene.World.Count(Item.Ration), Is.EqualTo(1));
        scene.World.Restore(scene.World.Capture() with { Hull = 15, Rations = 1, Meals = 1 });
        harness.PressAndRelease(GeKeys.E, 1);
        Assert.That(scene.World.Count(Item.Ration), Is.Zero);
        Assert.That(scene.World.Count(Item.Meal), Is.EqualTo(1));
        scene.World.Restore(scene.World.Capture() with { Hull = 1, CookingXp = 0 });
        harness.PressAndRelease(GeKeys.E, 1);
        Assert.That(scene.World.Hull, Is.EqualTo(13));
    }

    [Test]
    public void FishSalesRequireBramAndPreserveQuestMeals()
    {
        var (harness, scene) = Load();
        ReadyForFeast(scene);
        scene.World.Restore(scene.World.Capture() with { RiverQuestStage = 1, Trout = 2, Meals = 5 });
        int coins = scene.World.Coins;
        harness.PressAndRelease(GeKeys.B, 1);
        Assert.That(scene.World.Coins, Is.EqualTo(coins));
        Click(harness, SceneEmberbrook.Position(new Cell(6, 8)));
        harness.Run(200);
        harness.PressAndRelease(GeKeys.B, 1);
        Assert.That(scene.World.Coins, Is.EqualTo(coins + 14));
        Assert.That(scene.World.Count(Item.Meal), Is.EqualTo(3));
        Assert.That(scene.World.Count(Item.Trout), Is.Zero);
        harness.PressAndRelease(GeKeys.B, 1);
        Assert.That(scene.World.Coins, Is.EqualTo(coins + 14));
    }

    [Test]
    public void CharmChangesDamageAndRequiresAnEarnedRewardAndIdlePlayer()
    {
        var (harness, scene) = Load();
        harness.PressAndRelease(GeKeys.T, 1);
        Assert.That(scene.World.Charm, Is.EqualTo(RiverCharm.None));
        ReadyForFeast(scene);
        scene.World.Restore(scene.World.Capture() with
        {
            RiverQuestStage = 2, Charm = RiverCharm.Might, HasShield = false, CombatXp = 0
        });
        Assert.That(scene.World.AttackDamage, Is.EqualTo(7));
        Click(harness, SceneEmberbrook.Position(new Cell(9, 7)));
        harness.PressAndRelease(GeKeys.T, 1);
        Assert.That(scene.World.Charm, Is.EqualTo(RiverCharm.Might));
        harness.Run(200);
        harness.PressAndRelease(GeKeys.T, 1);
        Assert.That(scene.World.Charm, Is.EqualTo(RiverCharm.Shelter));
        Assert.That(scene.World.AttackDamage, Is.EqualTo(5));
        Visit(harness, scene, "mossling1");
        Assert.That(scene.World.Hull, Is.EqualTo(18), "Shelter reduces the returning hit from three to two");
    }

    [Test]
    public void CurrentSaveRejectsInvalidRiverProgress()
    {
        string directory = Path.Combine(Path.GetTempPath(), "emberbrook-v2-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "save.json");
        try
        {
            var expected = new EmberbrookWorld().Capture() with
            {
                QuestStage = 2, RuinQuestStage = 3, GuardianDefeated = true, HasSword = true, HasShield = true, Coins = 108
            };
            var store = new EmberbrookFileSaveStore(path);
            var progressed = expected with { RiverQuestStage = 2, Charm = RiverCharm.Shelter, FishingXp = 90, CookingXp = 48, Trout = 2, Meals = 3 };
            store.Write(progressed);
            Assert.That(store.Read(), Is.EqualTo(progressed));
            foreach (var invalid in new[]
                     {
                         progressed with { RiverQuestStage = 0 }, progressed with { Charm = (RiverCharm)42 },
                         progressed with { HasPearl = true }, progressed with { Trout = 60 },
                         progressed with { FishingXp = -1 }, progressed with { RuinQuestStage = 1 }
                     })
                Assert.Throws<InvalidDataException>(() => store.Write(invalid));
            Assert.That(store.Read(), Is.EqualTo(progressed));
            var missing = JsonSerializer.SerializeToNode(progressed)!.AsObject();
            missing.Remove("CookingXp");
            File.WriteAllText(path, missing.ToJsonString());
            Assert.Catch<JsonException>(() => store.Read());
        }
        finally { Directory.Delete(directory, true); }
    }
}
