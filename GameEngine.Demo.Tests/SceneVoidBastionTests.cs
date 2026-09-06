using GameEngine.Core;
using GameEngine.Core.Components;
using GameEngine.Core.Systems;

namespace GameEngine.Demo.Tests;

public class SceneVoidBastionTests
{
    private static readonly (int Pad, BastionTowerKind Kind)[] BuildOrder =
    [
        (3, BastionTowerKind.Pulse), (4, BastionTowerKind.Rail), (6, BastionTowerKind.Pulse),
        (13, BastionTowerKind.Mortar), (7, BastionTowerKind.Cryo), (1, BastionTowerKind.Pulse),
        (2, BastionTowerKind.Rail), (10, BastionTowerKind.Rail), (8, BastionTowerKind.Mortar),
        (11, BastionTowerKind.Cryo), (14, BastionTowerKind.Rail), (5, BastionTowerKind.Mortar),
        (12, BastionTowerKind.Rail), (9, BastionTowerKind.Pulse), (0, BastionTowerKind.Pulse)
    ];

    private static (SceneHarness Harness, SceneVoidBastion Scene) Load()
    {
        var scene = new SceneVoidBastion();
        return (SceneHarness.Load(scene), scene);
    }

    [Test]
    public void PlanningWaitsForThePlayerAndRequiresATower()
    {
        var (harness, scene) = Load();
        harness.Run(300);
        harness.PressAndRelease(GeKeys.Space, 1);
        Assert.That(scene.Battle.Phase, Is.EqualTo(BastionPhase.Planning));
        Assert.That(scene.Battle.Enemies, Is.Empty);
        Assert.That(scene.Battle.Credits, Is.EqualTo(270));
        Assert.That(scene.Battle.Notice, Does.Contain("Build at least one"));
        Assert.That(harness.Require("bastionRange").GetComponent<CAnimation>().ShouldDraw, Is.True);
    }

    [Test]
    public void MouseBuildsOnTheSelectedSocketThroughLetterboxedCoordinates()
    {
        var (harness, scene) = Load();
        var input = harness.Engine.Systems.Get<InputSystem>();
        void Click(Vec2 virtualPoint)
        {
            input.PointerPressed(new Pointer.PointerPressEvent(virtualPoint * 0.625 + new Vec2(0, 50)));
            harness.Run(1);
        }
        Click(BastionBattle.Pads[3]);
        Click(new Vec2(1110, 225));
        Assert.That(scene.SelectedPad, Is.EqualTo(3));
        Assert.That(scene.Battle.Towers[3]?.Kind, Is.EqualTo(BastionTowerKind.Pulse));
        Assert.That(scene.Battle.Credits, Is.EqualTo(200));
        input.PointerPressed(new Pointer.PointerPressEvent(new Vec2(400, 10)));
        harness.Run(1);
        Assert.That(scene.SelectedPad, Is.EqualTo(3));
        Click(new Vec2(1110, 495));
        Assert.That(scene.Battle.Towers[3]?.Level, Is.EqualTo(2));
        Click(new Vec2(1110, 655));
        Assert.That(scene.Battle.Phase, Is.EqualTo(BastionPhase.Combat));
    }

    [Test]
    public void UpgradesHaveAMaximumAndRecyclingNeverCreatesCredits()
    {
        var (harness, scene) = Load();
        harness.PressAndRelease(GeKeys.A, 1);
        harness.PressAndRelease(GeKeys.A, 30);
        Assert.That(scene.Battle.Credits, Is.EqualTo(200));
        harness.PressAndRelease(GeKeys.U, 1);
        harness.PressAndRelease(GeKeys.U, 1);
        Assert.That(scene.Battle.Towers[0]?.Level, Is.EqualTo(3));
        int remaining = scene.Battle.Credits;
        harness.PressAndRelease(GeKeys.U, 1);
        Assert.That(scene.Battle.Credits, Is.EqualTo(remaining));
        int refund = scene.Battle.Towers[0]!.Refund;
        harness.PressAndRelease(GeKeys.X, 1);
        Assert.That(scene.Battle.Credits, Is.EqualTo(remaining + refund).And.LessThan(270));
        Assert.That(scene.Battle.Towers[0], Is.Null);
        harness.PressAndRelease(GeKeys.X, 1);
        Assert.That(scene.Battle.Credits, Is.EqualTo(remaining + refund));
    }

    [Test]
    public void UnaffordableBuildingsAndInvalidSocketsDoNotSpendMoney()
    {
        var battle = new BastionBattle();
        Assert.That(battle.Build(0, BastionTowerKind.Mortar), Is.True);
        Assert.That(battle.Build(1, BastionTowerKind.Mortar), Is.True);
        Assert.That(battle.Build(2, BastionTowerKind.Pulse), Is.False);
        Assert.That(battle.Build(-1, BastionTowerKind.Pulse), Is.False);
        Assert.That(battle.Build(100, BastionTowerKind.Pulse), Is.False);
        Assert.That(battle.Build(3, (BastionTowerKind)99), Is.False);
        Assert.That(battle.Credits, Is.Zero);
        Assert.That(battle.Towers.Count(t => t != null), Is.EqualTo(2));
    }

    [Test]
    public void PauseFreezesCombatWhileAllowingConstruction()
    {
        var (harness, scene) = Load();
        harness.PressAndRelease(GeKeys.S, 1);
        harness.PressAndRelease(GeKeys.Space, 1);
        harness.Run(80);
        harness.PressAndRelease(GeKeys.P, 1);
        double[] distances = scene.Battle.Enemies.Select(e => e.Distance).ToArray();
        harness.PressAndRelease(GeKeys.Right, 1);
        harness.PressAndRelease(GeKeys.A, 1);
        harness.Run(180);
        Assert.That(scene.Battle.Towers[1], Is.Not.Null);
        Assert.That(scene.Battle.Enemies.Select(e => e.Distance), Is.EqualTo(distances));
        Assert.That(harness.Require("bastionNotice").GetComponent<CText>().Text, Does.Contain("PAUSED"));
        harness.PressAndRelease(GeKeys.P, 1);
        harness.Run(60);
        Assert.That(scene.Battle.Enemies.Select(e => e.Distance), Is.Not.EqualTo(distances));
    }

    [Test]
    public void DoubleSpeedAndDifferentTickSizesProduceTheSameSimulation()
    {
        var normal = new BastionBattle();
        var fast = new BastionBattle();
        normal.Build(3, BastionTowerKind.Pulse); fast.Build(3, BastionTowerKind.Pulse);
        normal.Launch(); fast.Launch(); fast.ToggleSpeed();
        for (int i = 0; i < 900; i++) normal.Update(1.0 / 60);
        for (int i = 0; i < 75; i++) fast.Update(0.1);
        Assert.That(fast.Kills, Is.EqualTo(normal.Kills));
        Assert.That(fast.Credits, Is.EqualTo(normal.Credits));
        Assert.That(fast.Enemies.Select(e => e.Distance), Is.EqualTo(normal.Enemies.Select(e => e.Distance)).Within(0.001));
        Assert.That(fast.Enemies.Select(e => e.Hull), Is.EqualTo(normal.Enemies.Select(e => e.Hull)).Within(0.001));
    }

    [Test]
    public void IonStormSlowsTheConvoyAndEnforcesItsCooldown()
    {
        var (harness, scene) = Load();
        scene.Battle.Build(12, BastionTowerKind.Pulse);
        harness.PressAndRelease(GeKeys.Space, 1);
        harness.Run(100);
        harness.PressAndRelease(GeKeys.E, 1);
        var enemy = scene.Battle.Enemies.First();
        Assert.That(enemy.SlowTime, Is.GreaterThan(2.9));
        Assert.That(scene.Battle.IonCooldown, Is.GreaterThan(24));
        double hull = enemy.Hull;
        harness.PressAndRelease(GeKeys.E, 1);
        Assert.That(enemy.Hull, Is.EqualTo(hull));
        double before = enemy.Distance;
        harness.Run(30);
        Assert.That(enemy.Distance - before, Is.LessThan(enemy.Speed * 0.3));
    }

    [Test]
    public void TargetPriorityChoosesTheLeadingStrongestOrNearestInRange()
    {
        var battle = new BastionBattle();
        battle.Build(1, BastionTowerKind.Cryo);
        battle.Launch();
        battle.Update(3);
        var candidates = battle.Enemies.Where(e => e.Hull > 0 && e.Position.DistanceTo(BastionBattle.Pads[1]) <= battle.Towers[1]!.Range).ToArray();
        Assert.That(candidates.Length, Is.GreaterThan(1));
        Assert.That(battle.TargetFor(1), Is.SameAs(candidates.MaxBy(e => e.Distance)));
        battle.CycleTarget(1);
        Assert.That(battle.TargetFor(1), Is.SameAs(candidates.MaxBy(e => e.Hull + e.Shield)));
        battle.CycleTarget(1);
        Assert.That(battle.TargetFor(1), Is.SameAs(candidates.MinBy(e => e.Position.DistanceTo(BastionBattle.Pads[1]))));
    }

    [Test]
    public void MixedDefencesCanWinAllTwelveWavesThroughTheEngine()
    {
        var (harness, scene) = Load();
        var battle = scene.Battle;
        int built = 0;
        int entities = harness.Entities.GetEntities().Count;
        bool sawShell = false, sawSlow = false, sawShield = false, sawBoss = false;
        for (int frame = 0; frame < 80000 && !battle.Finished; frame++)
        {
            if (battle.Phase == BastionPhase.Planning)
            {
                while (built < BuildOrder.Length && battle.Build(BuildOrder[built].Pad, BuildOrder[built].Kind)) built++;
                if (built >= 5)
                    foreach (var entry in BuildOrder)
                        if (battle.Towers[entry.Pad] != null) battle.Upgrade(entry.Pad);
                harness.PressAndRelease(GeKeys.Space, 1);
            }
            if (battle.Enemies.Count > 10 && battle.IonCooldown <= 0) harness.PressAndRelease(GeKeys.E, 1);
            harness.Run(1);
            sawShell |= battle.Shells.Count > 0;
            sawSlow |= battle.Enemies.Any(e => e.SlowTime > 0);
            sawShield |= battle.Enemies.Any(e => e.Shield > 0);
            sawBoss |= battle.Enemies.Any(e => e.Kind == BastionEnemyKind.Warden);
            if (frame % 120 != 0) continue;
            Assert.That(harness.Engine.IsRunning, Is.True);
            Assert.That(harness.Entities.GetEntities().Count, Is.EqualTo(entities), "all render entities are pooled");
            Assert.That(battle.Enemies.Count, Is.LessThanOrEqualTo(BastionBattle.MaxEnemies));
            Assert.That(battle.Shells.Count, Is.LessThanOrEqualTo(BastionBattle.MaxShells));
            Assert.That(battle.Effects.Count, Is.LessThanOrEqualTo(BastionBattle.MaxEffects));
        }
        Assert.That(battle.Phase, Is.EqualTo(BastionPhase.Victory), $"wave {battle.Wave}, core {battle.CoreHull}");
        Assert.That(battle.Wave, Is.EqualTo(12));
        Assert.That(sawShell && sawSlow && sawShield && sawBoss, Is.True);
        int credits = battle.Credits;
        harness.PressAndRelease(GeKeys.X, 1);
        harness.PressAndRelease(GeKeys.Space, 1);
        harness.Run(120);
        Assert.That(battle.Credits, Is.EqualTo(credits));
        Assert.That(battle.Wave, Is.EqualTo(12));
    }

    [Test]
    public void UndefendedConvoysBreachTheCoreAndDefeatIsTerminal()
    {
        var battle = new BastionBattle();
        battle.Build(0, BastionTowerKind.Pulse);
        for (int wave = 0; wave < 4 && !battle.Finished; wave++)
        {
            if (battle.Towers[0] == null) battle.Build(0, BastionTowerKind.Pulse);
            battle.Launch();
            battle.Sell(0);
            for (int i = 0; i < 120 && battle.Phase == BastionPhase.Combat; i++) battle.Update(1);
        }
        Assert.That(battle.Phase, Is.EqualTo(BastionPhase.Defeat));
        Assert.That(battle.CoreHull, Is.Zero);
        int credits = battle.Credits;
        Assert.That(battle.Build(0, BastionTowerKind.Pulse), Is.False);
        Assert.That(battle.Launch(), Is.False);
        battle.Update(100);
        Assert.That(battle.Credits, Is.EqualTo(credits));
    }

    [Test]
    public void RestartResetsTheEconomyAndMenuNavigationWorks()
    {
        var (harness, _) = Load();
        harness.PressAndRelease(GeKeys.F, 1);
        harness.PressAndRelease(GeKeys.Space, 1);
        harness.Run(90);
        harness.PressAndRelease(GeKeys.R, 1);
        harness.Engine.NotifyFirstPresent();
        Assert.That(harness.Require("bastionStatus").GetComponent<CText>().Text, Does.Contain("CREDITS 270"));
        harness.PressAndRelease(GeKeys.Q, 1);
        Assert.That(harness.Require("scene0").GetComponent<CText>().Text, Is.EqualTo("Void Bastion"));
    }

    [Test]
    public void RouteHasContinuousCornersAndEndsAtTheCore()
    {
        Assert.That(BastionBattle.PointAt(-1), Is.EqualTo(BastionBattle.Route[0]));
        double distance = 0;
        for (int i = 1; i < BastionBattle.Route.Count; i++)
        {
            distance += BastionBattle.Route[i].DistanceTo(BastionBattle.Route[i - 1]);
            Assert.That(BastionBattle.PointAt(distance), Is.EqualTo(BastionBattle.Route[i]));
            Assert.That(BastionBattle.PointAt(distance + 0.01).DistanceTo(BastionBattle.PointAt(distance - 0.01)), Is.LessThan(0.021));
        }
        Assert.That(BastionBattle.PointAt(double.MaxValue), Is.EqualTo(BastionBattle.Route[^1]));
    }
}
