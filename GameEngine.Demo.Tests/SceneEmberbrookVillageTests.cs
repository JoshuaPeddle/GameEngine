using GameEngine.Core;
using GameEngine.Core.Components;
using GameEngine.Demo.Emberbrook;
using System.Text.Json;

namespace GameEngine.Demo.Tests;

public partial class SceneEmberbrookTests
{
    private static void Button(SceneHarness harness, string tag)
    {
        var button = harness.Require(tag);
        Assert.That(button.GetComponent<CAnimation>().ShouldDraw, Is.True, tag);
        Click(harness, button.GetComponent<CTransform>().Position);
    }

    private static void ChapterComplete(SceneEmberbrook scene)
    {
        ReadyForTrail(scene);
        scene.World.Restore(scene.World.Capture() with { TrailQuestStage = 3, LitBeacons = 7, BridgeStage = 2, WoodcuttingXp = 60 });
    }

    [TestCase(0, "nell", "emberSupperBowls")]
    [TestCase(1, "orin", "emberRepairedWheel")]
    [TestCase(2, "nell", "emberTrailLunches")]
    [TestCase(3, "orin", "emberDedication")]
    public void ResidentDeliveryUsesExistingSuppliesAndChangesVillageOnce(int id, string resident, string prop)
    {
        var (harness, scene) = Load();
        ChapterComplete(scene);
        scene.World.Restore(scene.World.Capture() with { Bars = 3, Logs = 6, Meals = 2, SmokedTrout = 3 });
        Approach(harness, scene, resident);
        if (id >= 2) Button(harness, "emberResidentNext");
        int coins = scene.World.Coins;
        Button(harness, "emberResidentRequest");
        Assert.That(scene.World.AcceptedRequest, Is.EqualTo(id));
        Button(harness, "emberResidentRequest");
        Assert.That(scene.World.RequestComplete(id), Is.True);
        Assert.That(scene.World.Coins, Is.EqualTo(coins + EmberbrookWorld.Requests[id].Coins));
        Assert.That(scene.World.AcceptedRequest, Is.EqualTo(-1));
        Assert.That(harness.Require(prop).GetComponent<CAnimation>().ShouldDraw, Is.True);
        var completed = scene.World.Capture();
        Button(harness, "emberResidentRequest");
        Assert.That(scene.World.Capture(), Is.EqualTo(completed));
        scene.World.Restore(completed);
        Assert.That(scene.World.RequestComplete(id), Is.True);
    }

    [Test]
    public void RequestsCanBeAbandonedAndIncompleteHandinsNeverSpendSupplies()
    {
        var (harness, scene) = Load();
        Approach(harness, scene, "nell");
        Button(harness, "emberResidentNext");
        Button(harness, "emberResidentRequest");
        Assert.That(scene.World.AcceptedRequest, Is.EqualTo(-1), "Late request is locked.");
        Button(harness, "emberResidentNext");
        Button(harness, "emberResidentRequest");
        scene.World.Restore(scene.World.Capture() with { Meals = 1 });
        var before = scene.World.Capture();
        Button(harness, "emberResidentRequest");
        Assert.That(scene.World.Capture(), Is.EqualTo(before));
        Button(harness, "emberResidentAbandon");
        Assert.That(scene.World.Reserved(Item.Meal), Is.Zero);
        Button(harness, "emberResidentRequest");
        Assert.That(scene.World.AcceptedRequest, Is.Zero);
        scene.World.Restore(scene.World.Capture() with { QuestStage = 2 });
        Approach(harness, scene, "orin");
        Button(harness, "emberResidentRequest");
        Assert.That(scene.World.AcceptedRequest, Is.Zero, "Only one delivery may be active.");
    }

    [Test]
    public void DeliveryReservationsAddToAdventureSuppliesForBothSaleControls()
    {
        var (harness, scene) = Load();
        ReadyForFeast(scene);
        scene.World.Restore(scene.World.Capture() with { RiverQuestStage = 1, AcceptedRequest = 0, Meals = 7 });
        Approach(harness, scene, "merchant");
        scene.World.SellItem(Item.Meal, 10);
        Assert.That(scene.World.Count(Item.Meal), Is.EqualTo(5));
        scene.World.SellFish();
        Assert.That(scene.World.Count(Item.Meal), Is.EqualTo(5));
        scene.World.AbandonRequest();
        scene.World.SellFish();
        Assert.That(scene.World.Count(Item.Meal), Is.EqualTo(3));
        scene.World.Restore(scene.World.Capture() with { BridgeStage = 1, AcceptedRequest = 1, Bars = 4, Logs = 10 });
        scene.World.SellItem(Item.Bar, 10); scene.World.SellItem(Item.Log, 10);
        Assert.That(scene.World.Count(Item.Bar), Is.EqualTo(3));
        Assert.That(scene.World.Count(Item.Log), Is.EqualTo(8));
    }

    [Test]
    public void FieldbookWorksAcrossRegionsAndLateFindsDecorateTheVillage()
    {
        var (harness, scene) = Load();
        ChapterComplete(scene);
        foreach (var discovery in EmberbrookWorld.Discoveries)
        {
            var site = scene.World.Sites.Single(s => s.Id == discovery.Id);
            scene.World.Restore(scene.World.Capture() with { Region = site.Region, X = site.Region == Region.Village ? 5 : 2, Y = 7 });
            foreach (var enemy in scene.World.Sites.Where(s => s.IsEnemy)) { enemy.Hull = 0; enemy.RespawnSeconds = 10000; }
            Approach(harness, scene, site.Id);
            Assert.That(scene.World.Found(Array.IndexOf(EmberbrookWorld.Discoveries, discovery)), Is.True, site.Id);
        }
        Assert.That(scene.World.Discovered, Is.EqualTo(63));
        scene.World.Restore(scene.World.Capture() with { Region = Region.Village, X = 5, Y = 7 });
        Approach(harness, scene, "nell");
        Button(harness, "emberResidentTalk");
        Assert.That(scene.World.FloatShown, Is.True);
        Assert.That(harness.Require("emberReturnedFloat").GetComponent<CAnimation>().ShouldDraw, Is.True);
        Assert.That(harness.Require("emberBellmakerName").GetComponent<CText>().Text, Does.Contain("Mara"));
        Approach(harness, scene, "elder");
        Button(harness, "emberTabJournal");
        Button(harness, "emberJournalSection"); Button(harness, "emberJournalSection");
        Button(harness, "emberBookAction");
        Assert.That(scene.World.KeeperAwarded, Is.True);
        var complete = scene.World.Capture();
        Button(harness, "emberBookAction");
        Assert.That(scene.World.Capture(), Is.EqualTo(complete));
        Assert.That(harness.Require("emberExplorerBanner").GetComponent<CAnimation>().ShouldDraw, Is.True);
    }

    private static int PathLength(EmberbrookWorld world, Cell start, Cell destination)
    {
        var queue = new Queue<(Cell Cell, int Steps)>();
        var visited = new HashSet<Cell> { start };
        queue.Enqueue((start, 0));
        while (queue.TryDequeue(out var current))
        {
            if (current.Cell == destination) return current.Steps;
            foreach (var offset in new[] { new Cell(1, 0), new Cell(-1, 0), new Cell(0, 1), new Cell(0, -1) })
            {
                var next = new Cell(current.Cell.X + offset.X, current.Cell.Y + offset.Y);
                if (world.IsWalkable(next) && visited.Add(next)) queue.Enqueue((next, current.Steps + 1));
            }
        }
        return int.MaxValue;
    }

    [Test]
    public void LostWayRequiresSkillAndKeepsOriginalRouteWhileSavingAtLeastEightTiles()
    {
        var (harness, scene) = Load();
        ReadyForTrail(scene);
        scene.World.Restore(scene.World.Capture() with { TrailQuestStage = 1, Region = Region.Forest, X = 6, Y = 2 });
        Approach(harness, scene, "lostWay");
        Assert.That(scene.World.LostWayOpen, Is.False);
        int before = PathLength(scene.World, new Cell(18, 2), new Cell(2, 1));
        Assert.That(before, Is.LessThan(int.MaxValue));
        scene.World.Restore(scene.World.Capture() with { WoodcuttingXp = 60 });
        Approach(harness, scene, "lostWay");
        int after = PathLength(scene.World, new Cell(18, 2), new Cell(2, 1));
        Assert.That(before - after, Is.GreaterThanOrEqualTo(8));
        Assert.That(scene.World.IsWalkable(new Cell(7, 2)), Is.True);
        scene.World.Restore(scene.World.Capture() with { X = 7, Y = 2 });
        Assert.That(scene.World.Player, Is.EqualTo(new Cell(7, 2)), "A save on the cleared trunk restores.");
        Assert.That(scene.World.BeaconCount, Is.Zero);
        Assert.That(scene.World.HartDefeated, Is.False);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ConstructionPreviewAndPaymentAreAtomicAndPersistent(bool gate)
    {
        var (harness, scene) = Load();
        ChapterComplete(scene);
        scene.World.Restore(scene.World.Capture() with { Bars = 2, Logs = 4, Coins = gate ? 119 : 24 });
        Approach(harness, scene, "merchant");
        Button(harness, "emberProjects");
        if (gate) Button(harness, "emberBookNext");
        var before = scene.World.Capture();
        Button(harness, "emberBookAction");
        Assert.That(scene.World.Capture(), Is.EqualTo(before));
        scene.World.Restore(before with { Coins = gate ? 120 : 25 });
        Button(harness, "emberBookAction");
        Assert.That(gate ? scene.World.ReturnGateBuilt : scene.World.JettyBuilt, Is.True);
        Assert.That(scene.World.Coins, Is.Zero);
        var built = scene.World.Capture();
        Button(harness, "emberBookAction");
        Assert.That(scene.World.Capture(), Is.EqualTo(built));
        scene.World.Restore(built);
        Assert.That(gate ? scene.World.ReturnGateBuilt : scene.World.JettyBuilt, Is.True);
    }

    [Test]
    public void JettyGatesSkillAndCollectsTwoFishAtomicallyWithRepeat()
    {
        var (harness, scene) = Load();
        scene.World.Restore(scene.World.Capture() with { QuestStage = 2, BridgeStage = 2, JettyBuilt = true, X = 13, Y = 5 });
        Approach(harness, scene, "jetty");
        Assert.That(scene.World.Count(Item.Trout), Is.Zero);
        scene.World.Restore(scene.World.Capture() with { FishingXp = 60, Trout = 4, Rations = 11 });
        Approach(harness, scene, "jetty");
        Assert.That(scene.World.Count(Item.Trout), Is.EqualTo(4), "Only one fish fits; neither may be collected.");
        Assert.That(scene.World.FishingXp, Is.EqualTo(60));
        scene.World.Restore(scene.World.Capture() with { Trout = 0, Rations = 10 });
        scene.World.ToggleRepeatGathering();
        DesktopClick(harness, scene.World.Sites.Single(s => s.Id == "jetty").Cell);
        harness.Run(2400);
        Assert.That(scene.World.Count(Item.Trout), Is.EqualTo(10));
        Assert.That(scene.World.FishingXp, Is.EqualTo(160));
        Assert.That(scene.World.HasPearl, Is.True);
        Assert.That(scene.World.Activity, Is.EqualTo("Idle"));
    }

    [Test]
    public void ReturnGateRejectsUnsafeClicksWithoutQueuingThenWalksHomeInSafety()
    {
        var (harness, scene) = Load();
        ChapterComplete(scene);
        scene.World.Restore(scene.World.Capture() with { ReturnGateBuilt = true, Region = Region.Forest, X = 18, Y = 9 });
        DesktopClick(harness, new Cell(22, 12));
        Assert.That(scene.World.Target, Is.Null);
        Assert.That(scene.World.Region, Is.EqualTo(Region.Forest));
        foreach (var enemy in scene.World.Sites.Where(s => s.IsEnemy)) { enemy.Hull = 0; enemy.RespawnSeconds = 10000; }
        harness.Run(100);
        Assert.That(scene.World.Region, Is.EqualTo(Region.Forest), "A rejected gate command cannot fire later.");
        DesktopClick(harness, new Cell(22, 12));
        harness.Run(300);
        Assert.That(scene.World.Region, Is.EqualTo(Region.Village));
        Assert.That(scene.World.Player, Is.EqualTo(new Cell(5, 7)));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void HomecomingCanBeSkippedOrReplayedWithoutDuplicatingRewards(bool optional)
    {
        var (harness, scene) = Load();
        ChapterComplete(scene);
        scene.World.Restore(scene.World.Capture() with { BridgeStage = optional ? 2 : 0, Discovered = optional ? 63 : 0, CompletedRequests = optional ? 15 : 0, JettyBuilt = optional, KeeperAwarded = optional });
        Approach(harness, scene, "elder");
        var before = scene.World.Capture();
        Button(harness, "emberHomecomingOffer");
        Assert.That(scene.World.HomecomingOpen, Is.True);
        DesktopClick(harness, new Cell(10, 7));
        harness.PressAndRelease(GeKeys.E, 1);
        harness.Run(180);
        Assert.That(scene.World.Capture(), Is.EqualTo(before));
        Button(harness, "emberHomeReturn");
        Assert.That(scene.World.HomecomingSeen, Is.True);
        Button(harness, "emberHomecomingOffer");
        Button(harness, "emberHomeUnfinished");
        Assert.That(scene.World.Coins, Is.EqualTo(before.Coins));
        Assert.That(scene.World.CombatXp, Is.EqualTo(before.CombatXp));
        Assert.That(harness.Require("emberBookNext").GetComponent<CAnimation>().ShouldDraw, Is.True);
    }

    [Test]
    public void VillageSaveValidatesNewProgressAndRestoresWithoutPartialMutation()
    {
        var (harness, scene) = Load();
        ChapterComplete(scene);
        var valid = scene.World.Capture() with { Discovered = 63, CompletedRequests = 15, FloatShown = true, KeeperAwarded = true, JettyBuilt = true, ReturnGateBuilt = true, HomecomingSeen = true };
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            var store = new EmberbrookFileSaveStore(path);
            store.Write(valid);
            scene.World.Restore(store.Read());
            Assert.That(scene.World.Capture(), Is.EqualTo(valid));
            foreach (var invalid in new[] { valid with { AcceptedRequest = 0 }, valid with { Discovered = 1 }, valid with { BridgeStage = 0 }, valid with { TrailQuestStage = 2 }, valid with { WoodcuttingXp = 0 } })
            {
                Assert.Throws<InvalidDataException>(() => scene.World.Restore(invalid));
                Assert.That(scene.World.Capture(), Is.EqualTo(valid));
            }
            var json = JsonSerializer.SerializeToNode(valid)!.AsObject();
            json.Remove("Discovered"); File.WriteAllText(path, json.ToJsonString());
            Assert.Throws<JsonException>(() => store.Read());
            store.Write(valid with { Version = 6 });
            File.WriteAllText(path, JsonSerializer.Serialize(valid with { Version = 5 }));
            Assert.Throws<InvalidDataException>(() => store.Read());
        }
        finally { File.Delete(path); }
    }
}
