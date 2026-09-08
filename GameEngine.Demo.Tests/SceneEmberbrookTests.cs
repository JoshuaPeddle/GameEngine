using GameEngine.Core;
using GameEngine.Core.Components;
using GameEngine.Demo.Emberbrook;

namespace GameEngine.Demo.Tests;

public partial class SceneEmberbrookTests
{
    private sealed class MemorySaveStore : IEmberbrookSaveStore
    {
        public EmberbrookSave? Save;
        public void Write(EmberbrookSave save) => Save = save;
        public EmberbrookSave Read() => Save ?? throw new FileNotFoundException("No saved adventure yet.");
    }

    private static (SceneHarness Harness, SceneEmberbrook Scene) Load()
    {
        var scene = new SceneEmberbrook(new MemorySaveStore());
        var harness = SceneHarness.Load(scene);
        Assert.That(harness.Engine.IsFaulted, Is.False, harness.Engine.Fault?.Exception.ToString());
        return (harness, scene);
    }

    private static void Click(SceneHarness harness, Vec2 point)
    {
        var viewport = ViewportTransform.Create(harness.Input.RealResolution, new Vec2(1280, 800), GameEngine.Core.Systems.ScalingStrategy.Letterbox);
        var real = new Vec2(point.X * viewport.ScaleX + viewport.OffsetX, point.Y * viewport.ScaleY + viewport.OffsetY);
        harness.PointerGesture(real, real);
    }

    private static string WorkbenchText(SceneHarness harness) => string.Join(" ",
        Enumerable.Range(0, 7).Select(i => harness.Require("emberWorkbenchDetail" + i).GetComponent<CText>().Text));

    private static void Visit(SceneHarness harness, SceneEmberbrook scene, string id)
    {
        var site = scene.World.Sites.Single(s => s.Id == id);
        if (site.RespawnSeconds > 0) harness.Run((int)Math.Ceiling(site.RespawnSeconds * 60) + 1);
        if (site.Kind is SiteKind.Forge or SiteKind.Campfire) { scene.World.Select(site.Cell); harness.Run(1); }
        else Click(harness, SceneEmberbrook.Position(site.Cell));
        for (int frame = 0; frame < 1000 && scene.World.Activity != "Idle"; frame++) harness.Run(1);
        Assert.That(scene.World.Activity, Is.EqualTo("Idle"), $"Did not finish interacting with {id}");
        Assert.That(harness.Engine.IsFaulted, Is.False, harness.Engine.Fault?.Exception.ToString());
        if (site.Kind is SiteKind.Forge or SiteKind.Campfire)
        {
            var recipe = site.Kind == SiteKind.Campfire ? Recipe.GrilledTrout
                : scene.World.Count(Item.Bar) >= 2 && (!scene.World.HasSword || !scene.World.HasShield)
                    ? scene.World.HasSword ? Recipe.Shield : Recipe.Sword : Recipe.Bar;
            string name = EmberbrookWorld.Recipes.Single(r => r.Id == recipe).Name;
            harness.Run(1);
            for (int i = 0; i < 5 && !WorkbenchText(harness).Contains(name, StringComparison.Ordinal); i++)
                Click(harness, new Vec2(1180, 529));
            Assert.That(WorkbenchText(harness), Does.Contain(name));
            Click(harness, new Vec2(1180, 564));
            harness.Run(60);
        }
        if (site.Kind == SiteKind.Elder) Click(harness, new Vec2(1128, 564));
        if (site.Kind == SiteKind.Merchant) Click(harness, new Vec2(1180, 529));
        if (site.Kind == SiteKind.Bank) scene.World.Deposit(Item.Ore);
    }

    [Test]
    public void MenuOpensTheRpgAndLetterboxedClicksWalkAcrossTheBridge()
    {
        var menu = SceneHarness.Load(new SceneMenu());
        menu.PressAndRelease(GeKeys.Space, 1);
        Assert.That(menu.Entities.GetEntityWithTag("emberPlayer"), Is.Not.Null);
        var (harness, scene) = Load();
        var destination = new Cell(18, 7);
        Click(harness, SceneEmberbrook.Position(destination));
        bool crossedBridge = false;
        for (int frame = 0; frame < 400 && scene.World.Player != destination; frame++)
        {
            harness.Run(1);
            Assert.That(scene.World.IsWalkable(scene.World.Player), Is.True);
            if (scene.World.Player.X is 11 or 12)
            {
                crossedBridge = true;
                Assert.That(scene.World.Player.Y, Is.AnyOf(6, 7));
            }
        }
        Assert.That(crossedBridge, Is.True);
        Assert.That(scene.World.Player, Is.EqualTo(destination));
        harness.Run(30);
        Assert.That(harness.PositionOf("emberPlayer").DistanceTo(SceneEmberbrook.Position(destination)), Is.LessThan(0.01));
    }

    [Test]
    public void UnreachableClickAndLetterboxBarsDoNotChangeProgress()
    {
        var (harness, scene) = Load();
        var start = scene.World.Player;
        Click(harness, SceneEmberbrook.Position(new Cell(11, 2)));
        Assert.That(scene.World.Message, Does.Contain("No route"));
        harness.Input.HandlePointerEvent(Pointer.PointerEventType.Press, new Pointer.PointerPressEvent(new Vec2(200, 10)));
        harness.Run(60);
        Assert.That(scene.World.Player, Is.EqualTo(start));
        Assert.That(scene.World.QuestStage, Is.Zero);
    }

    [Test]
    public void FullQuestUsesPointerNavigationGatheringSmithingAndCombat()
    {
        var (harness, scene) = Load();
        Visit(harness, scene, "elder");
        Assert.That(scene.World.QuestStage, Is.EqualTo(1));
        for (int i = 0; i < 6; i++) Visit(harness, scene, "ore" + (i % 3 + 1));
        Assert.That(scene.World.Count(Item.Ore), Is.EqualTo(6));
        Assert.That(scene.World.MiningLevel, Is.EqualTo(3));
        Visit(harness, scene, "forge");
        Visit(harness, scene, "forge");
        Visit(harness, scene, "forge");
        Assert.That(scene.World.HasSword, Is.True);
        Assert.That(scene.World.Count(Item.Ore), Is.Zero);
        Assert.That(scene.World.Count(Item.Bar), Is.Zero);
        for (int i = 1; i <= 3; i++) Visit(harness, scene, "mossling" + i);
        Assert.That(scene.World.Kills, Is.EqualTo(3));
        Assert.That(scene.World.CombatLevel, Is.EqualTo(2));
        int coins = scene.World.Coins;
        Visit(harness, scene, "elder");
        Assert.That(scene.World.QuestStage, Is.EqualTo(2));
        Assert.That(scene.World.Coins, Is.EqualTo(coins + 40));
        Visit(harness, scene, "elder");
        Assert.That(scene.World.Coins, Is.EqualTo(coins + 40), "quest reward cannot be repeated");
        Assert.That(harness.Snapshot(), Is.Not.Empty);
        Assert.That(harness.Entities.GetEntities().Count, Is.LessThan(750));
    }

    [Test]
    public void MiningCooldownPackCapacityAndBankTransfersConserveItems()
    {
        var (harness, scene) = Load();
        Visit(harness, scene, "ore1");
        int xp = scene.World.MiningXp;
        Click(harness, SceneEmberbrook.Position(scene.World.Sites.Single(s => s.Id == "ore1").Cell));
        harness.Run(30);
        Assert.That(scene.World.Count(Item.Ore), Is.EqualTo(1));
        Assert.That(scene.World.MiningXp, Is.EqualTo(xp));
        scene.World.Restore(scene.World.Capture() with { Ore = 50, Rations = 2 });
        Visit(harness, scene, "ore2");
        Assert.That(scene.World.PackUsed, Is.EqualTo(12));
        Assert.That(scene.World.MiningXp, Is.EqualTo(xp));
        scene.World.WithdrawOre();
        Assert.That(scene.World.Message, Does.Contain("Walk to"));
        Visit(harness, scene, "bank");
        Assert.That(scene.World.Count(Item.Ore), Is.Zero);
        Assert.That(scene.World.BankedOre, Is.EqualTo(50));
        scene.World.WithdrawOre();
        Assert.That(scene.World.Count(Item.Ore), Is.EqualTo(50));
        Assert.That(scene.World.BankedOre, Is.Zero);
        Assert.That(scene.World.PackUsed, Is.EqualTo(12));
    }

    [Test]
    public void CombatCanBeRetreatedFromAndFoodHealsWithoutOverconsumption()
    {
        var (harness, scene) = Load();
        var enemy = scene.World.Sites.Single(s => s.Id == "mossling1");
        Click(harness, SceneEmberbrook.Position(enemy.Cell));
        for (int frame = 0; frame < 800 && scene.World.Hull == 20; frame++) harness.Run(1);
        Assert.That(scene.World.Hull, Is.EqualTo(17));
        Click(harness, SceneEmberbrook.Position(new Cell(16, 7)));
        harness.PressAndRelease(GeKeys.E, 1);
        Assert.That(scene.World.Hull, Is.EqualTo(20));
        Assert.That(scene.World.Count(Item.Ration), Is.EqualTo(1));
        harness.PressAndRelease(GeKeys.E, 1);
        Assert.That(scene.World.Count(Item.Ration), Is.EqualTo(1));
        harness.Run(200);
        Assert.That(scene.World.Hull, Is.EqualTo(20));
        Assert.That(scene.World.Kills, Is.Zero);
        Assert.That(scene.World.Player, Is.EqualTo(new Cell(16, 7)));
    }

    [Test]
    public void DefeatReturnsToVillageAndMerchantCannotChargeForAFullPack()
    {
        var (harness, scene) = Load();
        scene.World.Restore(scene.World.Capture() with { Hull = 1 });
        Visit(harness, scene, "mossling1");
        Assert.That(scene.World.Player, Is.EqualTo(new Cell(5, 7)));
        Assert.That(scene.World.Hull, Is.EqualTo(20));
        Assert.That(scene.World.Coins, Is.EqualTo(3));
        Assert.That(scene.World.Count(Item.Ration), Is.EqualTo(2));
        scene.World.Restore(scene.World.Capture() with { Ore = 50 });
        Visit(harness, scene, "merchant");
        Assert.That(scene.World.Coins, Is.EqualTo(3));
        scene.World.Restore(scene.World.Capture() with { Ore = 0 });
        Visit(harness, scene, "merchant");
        Assert.That(scene.World.Coins, Is.Zero);
        Assert.That(scene.World.Count(Item.Ration), Is.EqualTo(3));
        Visit(harness, scene, "merchant");
        Assert.That(scene.World.Count(Item.Ration), Is.EqualTo(3));
    }

    [Test]
    public void SaveLoadRestoresProgressAndMissingSaveDoesNotFaultEngine()
    {
        var store = new MemorySaveStore();
        var scene = new SceneEmberbrook(store);
        var harness = SceneHarness.Load(scene);
        harness.PressAndRelease(GeKeys.L, 1);
        Assert.That(scene.World.Message, Does.Contain("Could not load"));
        Visit(harness, scene, "elder");
        Visit(harness, scene, "ore1");
        harness.PressAndRelease(GeKeys.S, 1);
        var saved = scene.World.Capture();
        Visit(harness, scene, "ore2");
        harness.PressAndRelease(GeKeys.L, 1);
        Assert.That(scene.World.Capture(), Is.EqualTo(saved));
        var reloaded = new SceneEmberbrook(store);
        harness.Engine.ChangeScene(reloaded);
        harness.Run(1);
        harness.Engine.NotifyFirstPresent();
        harness.PressAndRelease(GeKeys.L, 1);
        Assert.That(reloaded.World.Capture(), Is.EqualTo(saved));
        harness.PressAndRelease(GeKeys.Q, 1);
        Assert.That(harness.Entities.GetEntityWithTag("scene0"), Is.Not.Null);
        Assert.That(harness.Engine.IsFaulted, Is.False, harness.Engine.Fault?.Exception.ToString());
    }

    [Test]
    public void FileSaveRoundTripsAndRejectsCorruptionWithoutMutatingWorld()
    {
        string directory = Path.Combine(Path.GetTempPath(), "emberbrook-test-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "save.json");
        try
        {
            var store = new EmberbrookFileSaveStore(path);
            var world = new EmberbrookWorld();
            var expected = world.Capture() with { Coins = 50, MiningXp = 60, Ore = 4, BankedOre = 7 };
            store.Write(expected);
            Assert.That(store.Read(), Is.EqualTo(expected));
            store.Write(expected with { Coins = 51 });
            Assert.That(store.Read().Coins, Is.EqualTo(51));
            Assert.That(Directory.GetFiles(directory), Has.Length.EqualTo(1));
            var before = world.Capture();
            Assert.Throws<InvalidDataException>(() => world.Restore(expected with { X = 11, Y = 3 }));
            Assert.That(world.Capture(), Is.EqualTo(before));
            Assert.Throws<InvalidDataException>(() => store.Write(expected with { Ore = 100 }));
            Assert.That(store.Read().Coins, Is.EqualTo(51));
            var unknown = System.Text.Json.JsonSerializer.SerializeToNode(expected)!.AsObject();
            unknown["unknown"] = true;
            File.WriteAllText(path, unknown.ToJsonString());
            Assert.Catch<System.Text.Json.JsonException>(() => store.Read());
            File.WriteAllText(path, "not json");
            Assert.Catch<System.Text.Json.JsonException>(() => store.Read());
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
    private static void ReadyForRuins(SceneEmberbrook scene)
    {
        scene.World.Restore(scene.World.Capture() with
        {
            QuestStage = 2, HasSword = true, Kills = 3, CombatXp = 45, Coins = 60, Bars = 2, Rations = 6
        });
    }

    [Test]
    public void RepeatGatheringWaitsForRegrowthStopsAtCapacityAndCanBeCancelled()
    {
        var (harness, scene) = Load();
        scene.World.Restore(scene.World.Capture() with { Ore = 40 });
        harness.PressAndRelease(GeKeys.M, 1);
        Click(harness, SceneEmberbrook.Position(scene.World.Sites.Single(s => s.Id == "ore1").Cell));
        harness.Run(4000);
        Assert.That(scene.World.Count(Item.Ore), Is.EqualTo(50));
        Assert.That(scene.World.MiningXp, Is.EqualTo(100));
        Assert.That(scene.World.Activity, Is.EqualTo("Idle"));
        scene.World.Restore(scene.World.Capture() with { Ore = 0 });
        Click(harness, SceneEmberbrook.Position(scene.World.Sites.Single(s => s.Id == "ore1").Cell));
        harness.Run(10);
        harness.PressAndRelease(GeKeys.M, 1);
        harness.Run(300);
        Assert.That(scene.World.Count(Item.Ore), Is.Zero);
        Assert.That(scene.World.RepeatGathering, Is.False);
    }

    [Test]
    public void ShieldCraftingChecksCostAndMitigatesDamage()
    {
        var (harness, scene) = Load();
        ReadyForRuins(scene);
        scene.World.Restore(scene.World.Capture() with { Coins = 19 });
        Visit(harness, scene, "forge");
        Assert.That(scene.World.HasShield, Is.False);
        Assert.That(scene.World.Count(Item.Bar), Is.EqualTo(2));
        scene.World.Restore(scene.World.Capture() with { Coins = 20 });
        Visit(harness, scene, "forge");
        Assert.That(scene.World.HasShield, Is.True);
        Assert.That(scene.World.Count(Item.Bar), Is.Zero);
        Assert.That(scene.World.Coins, Is.Zero);
        Visit(harness, scene, "mossling1");
        Assert.That(scene.World.Hull, Is.EqualTo(19));
        Assert.That(scene.World.Capture().HasShield, Is.True);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void SunkenBellQuestPlaysThroughDodgeCombatAndReturnsForOneReward(bool spear)
    {
        var (harness, scene) = Load();
        ReadyForRuins(scene);
        scene.World.Restore(scene.World.Capture() with { HasSpear = spear, SpearEquipped = spear });
        Visit(harness, scene, "forge");
        Visit(harness, scene, "elder");
        Assert.That(scene.World.RuinQuestStage, Is.EqualTo(1));
        Visit(harness, scene, "entrance");
        harness.Run(2);
        Assert.That(scene.World.Region, Is.EqualTo(Region.Ruins));
        Assert.That(harness.Entities.GetEntityWithTag("emberSite:elder"), Is.Null);
        Visit(harness, scene, "sentinel1");
        harness.PressAndRelease(GeKeys.E, 1);
        Visit(harness, scene, "sentinel2");
        harness.PressAndRelease(GeKeys.E, 1);
        var guardian = scene.World.Sites.Single(s => s.Id == "guardian");
        Click(harness, SceneEmberbrook.Position(guardian.Cell));
        bool dodging = false;
        int dodges = 0;
        for (int frame = 0; frame < 6000 && !scene.World.GuardianDefeated; frame++)
        {
            if (scene.World.DangerCentre != null && !dodging)
            {
                var safe = new Cell(19, scene.World.Player.Y <= 7 ? 9 : 5);
                Click(harness, SceneEmberbrook.Position(safe));
                dodging = true;
                dodges++;
            }
            else if (scene.World.DangerCentre == null && dodging)
            {
                Click(harness, SceneEmberbrook.Position(guardian.Cell));
                dodging = false;
            }
            if (scene.World.Hull <= 12) harness.PressAndRelease(GeKeys.E, 1);
            harness.Run(1);
            Assert.That(scene.World.Region, Is.EqualTo(Region.Ruins), "pilot should survive the dungeon");
            Assert.That(harness.Engine.IsFaulted, Is.False, harness.Engine.Fault?.Exception.ToString());
        }
        Assert.That(scene.World.GuardianDefeated, Is.True);
        Assert.That(dodges, Is.GreaterThan(0));
        Visit(harness, scene, "relic");
        Assert.That(scene.World.RuinQuestStage, Is.EqualTo(2));
        Visit(harness, scene, "exit");
        Assert.That(scene.World.Region, Is.EqualTo(Region.Village));
        int coins = scene.World.Coins;
        Visit(harness, scene, "elder");
        Assert.That(scene.World.RuinQuestStage, Is.EqualTo(3));
        Assert.That(scene.World.Coins, Is.EqualTo(coins + 80));
        Visit(harness, scene, "elder");
        Assert.That(scene.World.Coins, Is.EqualTo(coins + 80));
        harness.PressAndRelease(GeKeys.S, 1);
        harness.PressAndRelease(GeKeys.L, 1);
        Assert.That(scene.World.GuardianDefeated, Is.True);
        Assert.That(scene.World.RuinQuestStage, Is.EqualTo(3));
        harness.Run(2);
        Assert.That(harness.Entities.GetEntities().Count, Is.LessThan(750));
    }

    [Test]
    public void RuinsGateRequiresQuestAndRegionChangesKeepEntityCountsBounded()
    {
        var (harness, scene) = Load();
        Visit(harness, scene, "entrance");
        Assert.That(scene.World.Region, Is.EqualTo(Region.Village));
        ReadyForRuins(scene);
        Visit(harness, scene, "elder");
        for (int trip = 0; trip < 5; trip++)
        {
            Visit(harness, scene, "entrance");
            Visit(harness, scene, "exit");
            harness.Run(2);
            Assert.That(harness.Entities.GetEntities().Count, Is.LessThan(750));
            Assert.That(harness.Entities.GetEntitiesWithTag("emberPlayer"), Has.Count.EqualTo(1));
        }
    }

    [Test]
    public void SentinelsPursueAndShockwavesDamageOnlyTheirMarkedArea()
    {
        var (harness, scene) = Load();
        ReadyForRuins(scene);
        scene.World.Restore(scene.World.Capture() with { Region = Region.Ruins, RuinQuestStage = 1, X = 10, Y = 7 });
        var sentinel = scene.World.Sites.Single(s => s.Id == "sentinel1");
        var start = sentinel.Cell;
        harness.Run(90);
        Assert.That(sentinel.Cell, Is.Not.EqualTo(start));
        Assert.That(sentinel.Cell.Distance(scene.World.Player), Is.EqualTo(1));
        scene.World.Restore(scene.World.Capture() with { X = 19, Y = 7, Hull = 20 });
        for (int frame = 0; frame < 200 && scene.World.DangerCentre == null; frame++) harness.Run(1);
        Assert.That(scene.World.DangerCentre, Is.Not.Null);
        Assert.That(harness.Entities.GetEntitiesWithTag("emberDanger").Count(e => e.GetComponent<CAnimation>().ShouldDraw), Is.EqualTo(9));
        Assert.That(scene.World.Hull, Is.EqualTo(20));
        harness.Run(86);
        Assert.That(scene.World.Hull, Is.EqualTo(12));
        Assert.That(scene.World.DangerCentre, Is.Null);
        for (int frame = 0; frame < 200 && scene.World.DangerCentre == null; frame++) harness.Run(1);
        Click(harness, SceneEmberbrook.Position(new Cell(19, 10)));
        harness.Run(86);
        Assert.That(scene.World.Hull, Is.EqualTo(12));
    }

    [Test]
    public void OlderSavesAreRejectedAndCurrentProgressRoundTrips()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            var expected = new EmberbrookWorld().Capture() with { QuestStage = 2, HasSword = true, Kills = 3, Coins = 48 };
            var store = new EmberbrookFileSaveStore(path);
            for (int version = 1; version <= 5; version++)
            {
                string original = System.Text.Json.JsonSerializer.Serialize(expected with { Version = version });
                File.WriteAllText(path, original);
                Assert.Throws<InvalidDataException>(() => store.Read());
                Assert.That(File.ReadAllText(path), Is.EqualTo(original));
            }
            var progress = expected with { Region = Region.Ruins, X = 19, Y = 7, RuinQuestStage = 2, HasShield = true, GuardianDefeated = true };
            store.Write(progress);
            var world = new EmberbrookWorld();
            world.Restore(store.Read());
            Assert.That(world.Capture(), Is.EqualTo(progress));
            Assert.That(world.Sites.Single(site => site.Id == "guardian").Hull, Is.Zero);
        }
        finally { File.Delete(path); }
    }

    [Test]
    public void LongTickCannotMineThroughAResourceCooldown()
    {
        var (harness, scene) = Load();
        scene.World.Restore(scene.World.Capture() with { X = 17, Y = 4 });
        harness.PressAndRelease(GeKeys.M, 1);
        Click(harness, SceneEmberbrook.Position(new Cell(17, 3)));
        harness.Engine.Tick(20);
        Assert.That(scene.World.Count(Item.Ore), Is.EqualTo(1));
        Assert.That(scene.World.MiningXp, Is.EqualTo(10));
        Assert.That(scene.World.Target!.RespawnSeconds, Is.GreaterThan(0));
    }

    [Test]
    public void DefeatedGuardianStaysDeadAndDoesNotProduceAnotherShockwave()
    {
        var (harness, scene) = Load();
        ReadyForRuins(scene);
        scene.World.Restore(scene.World.Capture() with
        {
            Region = Region.Ruins, X = 19, Y = 7, RuinQuestStage = 1, GuardianDefeated = true
        });
        harness.Run(1500);
        Assert.That(scene.World.DangerCentre, Is.Null);
        Assert.That(scene.World.Hull, Is.EqualTo(20));
        var guardian = scene.World.Sites.Single(s => s.Id == "guardian");
        Assert.That(guardian.Hull, Is.Zero);
        Assert.That(harness.Require("emberSite:guardian").GetComponent<CAnimation>().ShouldDraw, Is.False);
        Visit(harness, scene, "relic");
        Assert.That(scene.World.RuinQuestStage, Is.EqualTo(2));
    }

}
