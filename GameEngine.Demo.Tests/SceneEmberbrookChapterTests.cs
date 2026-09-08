using GameEngine.Core;
using GameEngine.Core.Components;
using GameEngine.Demo.Emberbrook;

namespace GameEngine.Demo.Tests;

public partial class SceneEmberbrookTests
{
    private static void Approach(SceneHarness harness, SceneEmberbrook scene, string id)
    {
        var site = scene.World.Sites.Single(s => s.Id == id);
        if (site.Kind is SiteKind.Forge or SiteKind.Campfire) { scene.World.Select(site.Cell); harness.Run(1); }
        else DesktopClick(harness, site.Cell);
        for (int frame = 0; frame < 1000 && scene.World.Activity != "Idle"; frame++) harness.Run(1);
        Assert.That(scene.World.Activity, Is.EqualTo("Idle"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void BrokenCrossingOffersTwoPaymentsAndActuallyShortensTheRoute(bool coins)
    {
        var (harness, scene) = Load();
        var world = scene.World;
        world.Restore(world.Capture() with { QuestStage = 2, HasSword = true, Coins = 60, Logs = 4, Bars = 1 });
        Approach(harness, scene, "merchant");
        Assert.That(world.Coins, Is.EqualTo(60), "Arriving at a shop must not buy anything.");
        Click(harness, new Vec2(1065, 530));
        Assert.That(world.BridgeStage, Is.EqualTo(1));
        Assert.That(world.IsWalkable(new Cell(11, 4)), Is.False);
        Click(harness, new Vec2(coins ? 1180 : 1065, 564));
        Assert.That(world.BridgeStage, Is.EqualTo(2));
        Assert.That(world.Coins, Is.EqualTo(coins ? 15 : 60));
        Assert.That(world.Count(Item.Log), Is.EqualTo(coins ? 4 : 0));
        Assert.That(world.Count(Item.Bar), Is.EqualTo(coins ? 1 : 0));
        var repaired = world.Capture();
        world.RepairBridge(coins);
        Assert.That(world.Capture(), Is.EqualTo(repaired));
        world.Restore(repaired with { X = 10, Y = 4 });
        Assert.That(world.IsWalkable(new Cell(11, 4)), Is.True);
        world.Select(new Cell(13, 4));
        world.Update(0.46);
        Assert.That(world.Player, Is.EqualTo(new Cell(13, 4)), "The repaired bridge is a direct three-step crossing.");
        world.Restore(repaired with { BridgeStage = 1, X = 10, Y = 4 });
        world.Select(new Cell(13, 4));
        world.Update(0.46);
        Assert.That(world.Player, Is.Not.EqualTo(new Cell(13, 4)), "The original route must detour to the main bridge.");
    }

    [Test]
    public void IncompleteRepairNeverConsumesPartialSuppliesAndForestEdgeIsSafe()
    {
        var (harness, scene) = Load();
        var world = scene.World;
        world.Restore(world.Capture() with { QuestStage = 2, BridgeStage = 1, Logs = 3, Bars = 1, Coins = 44 });
        Approach(harness, scene, "merchant");
        var before = world.Capture();
        world.RepairBridge(false); world.RepairBridge(true);
        Assert.That(world.Capture(), Is.EqualTo(before));
        Approach(harness, scene, "trailGate");
        Assert.That(world.Region, Is.EqualTo(Region.Forest));
        Assert.That(world.IsWalkable(new Cell(7, 8)), Is.False);
        Visit(harness, scene, "ash1");
        Assert.That(world.Count(Item.Log), Is.EqualTo(4));
        Visit(harness, scene, "beacon0");
        Assert.That(world.LitBeacons, Is.Zero);
        Assert.That(world.Count(Item.Log), Is.EqualTo(4));
        Assert.DoesNotThrow(() => world.Capture().Validate());
        Assert.That(world.Hull, Is.EqualTo(20));
        Assert.That(world.Select(new Cell(18, 7)), Is.False);
    }

    [Test]
    public void WorkbenchShowsCostsAndBatchingConsumesPerItemAndCancels()
    {
        var (harness, scene) = Load();
        var world = scene.World;
        world.Restore(world.Capture() with { Ore = 9 });
        Approach(harness, scene, "forge");
        Assert.That(world.Count(Item.Ore), Is.EqualTo(9));
        Assert.That(WorkbenchText(harness), Does.Contain("3 ore"));
        Click(harness, new Vec2(1065, 564));
        Click(harness, new Vec2(1180, 564));
        harness.Run(55);
        Assert.That(world.Count(Item.Bar), Is.EqualTo(1));
        Assert.That(world.Count(Item.Ore), Is.EqualTo(6));
        harness.PressAndRelease(GeKeys.X, 1);
        harness.Run(180);
        Assert.That(world.Count(Item.Bar), Is.EqualTo(1));
        world.StartBatch(Recipe.Bar, 5);
        harness.Run(220);
        Assert.That(world.Count(Item.Bar), Is.EqualTo(3));
        Assert.That(world.Count(Item.Ore), Is.Zero);
        Assert.That(world.BatchRemaining, Is.Zero);
        Assert.That(world.Activity, Is.EqualTo("Idle"));
    }

    [Test]
    public void FullPackCraftingIsAtomicWhenStackedIngredientsDoNotFreeASlot()
    {
        var (harness, scene) = Load();
        var world = scene.World;
        world.Restore(world.Capture() with { Rations = 11, Ore = 5 });
        Approach(harness, scene, "forge");
        var before = world.Capture();
        Assert.That(world.Craft(Recipe.Bar), Is.False);
        Assert.That(world.Capture(), Is.EqualTo(before));
        world.Restore(before with { Ore = 3 });
        Assert.That(world.Craft(Recipe.Bar), Is.True);
        Assert.That(world.PackUsed, Is.EqualTo(12));
        Assert.That(world.Count(Item.Bar), Is.EqualTo(1));
    }

    [Test]
    public void ToolsUnlockRichVeinsAndDuplicateCraftsCannotChargeAgain()
    {
        var (harness, scene) = Load();
        var world = scene.World;
        world.Restore(world.Capture() with { Bars = 2, Coins = 30 });
        Visit(harness, scene, "richOre");
        Assert.That(world.Count(Item.Ore), Is.Zero);
        Approach(harness, scene, "forge");
        Assert.That(world.Craft(Recipe.Tool), Is.True);
        Assert.That(world.HasTool, Is.True);
        Assert.That(world.Coins, Is.Zero);
        var crafted = world.Capture();
        Assert.That(world.Craft(Recipe.Tool), Is.False);
        Assert.That(world.Capture(), Is.EqualTo(crafted));
        Visit(harness, scene, "richOre");
        Assert.That(world.Count(Item.Ore), Is.EqualTo(3));
        Assert.That(world.MiningXp, Is.EqualTo(20));
    }

    [Test]
    public void SmokedFoodStacksHealsAndRequiresTheBridgeRecipe()
    {
        var (harness, scene) = Load();
        var world = scene.World;
        world.Restore(world.Capture() with { QuestStage = 2, Trout = 2, Logs = 2, Rations = 0 });
        Approach(harness, scene, "campfire");
        Assert.That(world.Craft(Recipe.SmokedTrout), Is.False);
        Assert.That(world.Count(Item.Trout), Is.EqualTo(2));
        world.Restore(world.Capture() with { BridgeStage = 2 });
        Assert.That(world.Craft(Recipe.SmokedTrout), Is.True);
        Assert.That(world.Craft(Recipe.SmokedTrout), Is.True);
        Assert.That(world.Count(Item.SmokedTrout), Is.EqualTo(2));
        Assert.That(world.PackUsed, Is.EqualTo(1));
        world.Restore(world.Capture() with { Hull = 12 });
        world.Eat();
        Assert.That(world.Hull, Is.EqualTo(20));
        Assert.That(world.Count(Item.SmokedTrout), Is.EqualTo(1));
        world.Eat();
        Assert.That(world.Count(Item.SmokedTrout), Is.EqualTo(1));
    }

    [Test]
    public void SpearTradesShieldForReachAndCannotStrikeThroughWallsOrSwapInDanger()
    {
        var (harness, scene) = Load();
        var world = scene.World;
        world.Restore(world.Capture() with { HasSword = true, HasShield = true, Bars = 2, Logs = 4 });
        Approach(harness, scene, "forge");
        Assert.That(world.Craft(Recipe.Spear), Is.True);
        world.EquipSpear();
        Assert.That(world.AttackRange, Is.EqualTo(2));
        Assert.That(world.Protection, Is.Zero);
        world.Restore(world.Capture() with { QuestStage = 2, RuinQuestStage = 1, Region = Region.Ruins, X = 15, Y = 7 });
        var enemy = world.Sites.Single(s => s.Id == "sentinel1");
        enemy.Cell = new Cell(17, 7);
        world.Select(enemy.Cell);
        world.Update(0.2);
        Assert.That(enemy.Hull, Is.EqualTo(enemy.MaxHull));
        world.EquipSpear();
        Assert.That(world.SpearEquipped, Is.True, "Cannot swap while moving or fighting.");
        world.Restore(world.Capture() with { Region = Region.Village, X = 17, Y = 8 });
        var mossling = world.Sites.Single(s => s.Id == "mossling1");
        world.Select(mossling.Cell);
        world.Update(0.91);
        Assert.That(world.Player.Distance(mossling.Cell), Is.EqualTo(2));
        Assert.That(mossling.Hull, Is.LessThan(mossling.MaxHull));
        Assert.That(world.Hull, Is.EqualTo(20));
        world.Restore(world.Capture() with { CombatXp = 30000 });
        Assert.That(world.CombatBonus, Is.EqualTo(4));
        Assert.That(world.AttackDamage, Is.EqualTo(9));
    }

    [Test]
    public void SidebarChangesDoNotCancelGatheringAndBankShowsStoredItemTypes()
    {
        var (harness, scene) = Load();
        scene.World.Restore(scene.World.Capture() with { X = 16, Y = 3 });
        DesktopClick(harness, new Cell(17, 3));
        Click(harness, new Vec2(1128, 242));
        harness.Run(150);
        Assert.That(scene.World.Count(Item.Ore), Is.EqualTo(1));
        Approach(harness, scene, "bank");
        scene.World.DepositAll();
        Click(harness, new Vec2(1040, 242));
        harness.Run(1);
        Assert.That(harness.Require("emberItemCount0").GetComponent<CText>().Text, Is.EqualTo("1"));
        Click(harness, new Vec2(1040, 278));
        Click(harness, new Vec2(1180, 564));
        Assert.That(scene.World.Count(Item.Ore), Is.EqualTo(1));
        Assert.That(scene.World.BankCount(Item.Ore), Is.Zero);
    }

    [Test]
    public void QuestSuppliesAreReservedFromSelectedSales()
    {
        var (harness, scene) = Load();
        var world = scene.World;
        world.Restore(world.Capture() with { QuestStage = 2, BridgeStage = 1, Logs = 6, Bars = 2 });
        Approach(harness, scene, "merchant");
        int coins = world.Coins;
        world.SellItem(Item.Log, 60); world.SellItem(Item.Bar, 60);
        Assert.That(world.Count(Item.Log), Is.EqualTo(4));
        Assert.That(world.Count(Item.Bar), Is.EqualTo(1));
        Assert.That(world.Coins, Is.EqualTo(coins + 5));
    }

    [Test]
    public void ContinueAndSafeMilestoneAutosavePreserveTheNewChapter()
    {
        var store = new MemorySaveStore();
        var scene = new SceneEmberbrook(store, true);
        var harness = SceneHarness.Load(scene);
        DesktopClick(harness, new Cell(18, 7));
        harness.Run(120);
        Assert.That(scene.World.Player, Is.EqualTo(new Cell(5, 7)));
        Click(harness, new Vec2(380, 412));
        Approach(harness, scene, "elder");
        Assert.That(scene.World.QuestStage, Is.Zero);
        Click(harness, new Vec2(1128, 564));
        harness.Run(2);
        Assert.That(store.Save!.QuestStage, Is.EqualTo(1));
        var resumed = new SceneEmberbrook(store, true);
        var second = SceneHarness.Load(resumed);
        Click(second, new Vec2(600, 412));
        Assert.That(resumed.World.QuestStage, Is.EqualTo(1));
        Assert.That(second.Require("emberWelcome").GetComponent<CAnimation>().ShouldDraw, Is.False);
    }

    [Test]
    public void ChapterSaveRoundTripsAndRejectsImpossibleEquipmentAndStackCounts()
    {
        var world = new EmberbrookWorld();
        var save = world.Capture() with { QuestStage = 2, BridgeStage = 2, HasSpear = true, SpearEquipped = true,
            HasTool = true, SmokedTrout = 4, BankSmokedTrout = 9, Ore = 20 };
        world.Restore(save);
        Assert.That(world.Capture(), Is.EqualTo(save));
        foreach (var invalid in new[] { save with { HasSpear = false }, save with { Ore = 61 }, save with { SmokedTrout = 25 }, save with { QuestStage = 0 } })
        {
            Assert.Throws<InvalidDataException>(() => world.Restore(invalid));
            Assert.That(world.Capture(), Is.EqualTo(save));
        }
    }
    private sealed class BrokenChapterStore : IEmberbrookSaveStore
    {
        public void Write(EmberbrookSave save) => throw new IOException("Disk is full.");
        public EmberbrookSave Read() => throw new InvalidDataException("Start a new game.");
    }

    [Test]
    public void FailedContinueAndAutosaveAreVisibleWithoutFaultingOrLosingPlay()
    {
        var scene = new SceneEmberbrook(new BrokenChapterStore(), true);
        var harness = SceneHarness.Load(scene);
        Click(harness, new Vec2(600, 412));
        Assert.That(harness.Engine.IsFaulted, Is.False);
        Assert.That(harness.Require("emberWelcome").GetComponent<CAnimation>().ShouldDraw, Is.True);
        Assert.That(scene.World.Message, Does.Contain("Start a new game"));
        Click(harness, new Vec2(380, 412));
        Approach(harness, scene, "elder");
        Click(harness, new Vec2(1128, 564));
        Assert.That(scene.World.QuestStage, Is.EqualTo(1));
        Assert.That(harness.Require("emberSaveStatus").GetComponent<CText>().Text, Does.Contain("SAVE FAILED"));
        Assert.That(harness.Engine.IsFaulted, Is.False);
        DesktopClick(harness, new Cell(5, 7));
        harness.Run(100);
        Assert.That(scene.World.Player, Is.EqualTo(new Cell(5, 7)));
        Assert.That(harness.Require("emberSaveStatus").GetComponent<CText>().Text, Does.Contain("SAVE FAILED"));
    }

    [TestCase("forge", Recipe.Bar, Item.Bar)]
    [TestCase("campfire", Recipe.GrilledTrout, Item.Meal)]
    public void CraftButtonFromSkillsStartsTheSelectedRecipeOnItsFirstClick(string station, Recipe recipe, Item output)
    {
        var (harness, scene) = Load();
        scene.World.Restore(scene.World.Capture() with { Ore = 3, Trout = 1 });
        Approach(harness, scene, station);
        Click(harness, new Vec2(1128, 242));
        Click(harness, new Vec2(1180, 564));
        Assert.That(scene.World.BatchRecipe, Is.EqualTo(recipe));
        harness.Run(60);
        Assert.That(scene.World.Count(output), Is.EqualTo(1));
    }

    [Test]
    public void VisitingCookingFireDoesNotInheritTheForgesRecipeSelection()
    {
        var (harness, scene) = Load();
        scene.World.Restore(scene.World.Capture() with { Trout = 1 });
        Approach(harness, scene, "forge");
        Click(harness, new Vec2(1180, 529));
        Approach(harness, scene, "campfire");
        Click(harness, new Vec2(1180, 564));
        Assert.That(scene.World.BatchRecipe, Is.EqualTo(Recipe.GrilledTrout));
        harness.Run(60);
        Assert.That(scene.World.Count(Item.Meal), Is.EqualTo(1));
    }

    [TestCase("forge", false, Item.Bar)]
    [TestCase("campfire", false, Item.Meal)]
    [TestCase("campfire", true, Item.SmokedTrout)]
    public void ClickingWorkstationWalksAndCraftsItsSelectedRecipe(string station, bool smoked, Item output)
    {
        var (harness, scene) = Load();
        scene.World.Restore(scene.World.Capture() with { QuestStage = 2, BridgeStage = 2, Ore = 3, Trout = 1, Logs = 1, Bars = 2 });
        if (smoked)
        {
            Approach(harness, scene, station);
            Click(harness, new Vec2(1180, 529));
            scene.World.Restore(scene.World.Capture() with { X = 5, Y = 7 });
        }
        int before = scene.World.Count(output);
        DesktopClick(harness, scene.World.Sites.Single(s => s.Id == station).Cell);
        harness.Run(500);
        Assert.That(scene.World.Count(output), Is.EqualTo(before + 1));
        Assert.That(scene.World.Activity, Is.EqualTo("Idle"));
        Assert.That(scene.World.HasSword, Is.False, "The selected bar recipe must not switch to equipment.");
    }

    [Test]
    public void WalkingAwayOrStoppingCancelsPendingWorkstationCraft()
    {
        var (harness, scene) = Load();
        scene.World.Restore(scene.World.Capture() with { Ore = 6, X = 19, Y = 7 });
        DesktopClick(harness, new Cell(6, 3));
        harness.PressAndRelease(GeKeys.X, 1);
        harness.Run(400);
        Assert.That(scene.World.Count(Item.Ore), Is.EqualTo(6));
        DesktopClick(harness, new Cell(6, 3));
        DesktopClick(harness, new Cell(18, 7));
        harness.Run(400);
        Assert.That(scene.World.Count(Item.Ore), Is.EqualTo(6));
        Assert.That(scene.World.Count(Item.Bar), Is.Zero);
    }

    [TestCase("forge", Item.Bar)]
    [TestCase("campfire", Item.Meal)]
    public void ClickingAdjacentWorkstationUsesSelectedBatchQuantity(string station, Item output)
    {
        var (harness, scene) = Load();
        scene.World.Restore(scene.World.Capture() with { Ore = 15, Trout = 5 });
        Approach(harness, scene, station);
        Click(harness, new Vec2(1065, 564));
        DesktopClick(harness, scene.World.Sites.Single(s => s.Id == station).Cell);
        harness.Run(400);
        Assert.That(scene.World.Count(output), Is.EqualTo(5));
        Assert.That(scene.World.Activity, Is.EqualTo("Idle"));
    }

}
