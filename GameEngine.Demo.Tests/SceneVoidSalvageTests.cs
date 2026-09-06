using GameEngine.Core;
using GameEngine.Core.Components;
using GameEngine.Core.Systems;

namespace GameEngine.Demo.Tests;

public class SceneVoidSalvageTests
{
    private static (SceneHarness Harness, SceneVoidSalvage Scene) Load()
    {
        var scene = new SceneVoidSalvage();
        var harness = SceneHarness.Load(scene);
        harness.PressAndRelease(GeKeys.Space, 1);
        harness.Run(50);
        return (harness, scene);
    }

    [Test]
    public void MenuLaunchWaitsForInput()
    {
        var harness = SceneHarness.Load(new SceneMenu());
        var item = harness.Entities.GetEntities().Single(e => e.TryGetComponent<CText>()?.Text == "Void Salvage");
        for (int i = 0; i < int.Parse(item.Tag[5..]); i++) harness.PressAndRelease(GeKeys.S, 1);
        harness.PressAndRelease(GeKeys.Space, 1);
        harness.Engine.NotifyFirstPresent();
        harness.Run(300);
        Assert.That(harness.Entities.GetEntitiesWithTag("salvageCore"), Is.Empty);
        Assert.That(harness.Require("salvageMessage").GetComponent<CText>().Text, Does.Contain("LAUNCH"));
        harness.PressAndRelease(GeKeys.Space, 1);
        Assert.That(harness.Entities.GetEntitiesWithTag("salvageCore"), Has.Count.EqualTo(9));
    }

    [Test]
    public void CargoHasCapacitySlowsMovementAndBanksOnlyAtDock()
    {
        var (harness, scene) = Load();
        harness.Engine.Systems.Get<InputSystem>().KeyDown(GeKeys.D);
        harness.Run(10);
        double emptySpeed = harness.Require("salvagePlayer").GetComponent<CTransform>().Velocity.Length();
        harness.Engine.Systems.Get<InputSystem>().KeyUp(GeKeys.D);
        Collect(harness, 4);
        Assert.That(scene.Cargo, Is.EqualTo(3));
        Assert.That(scene.Delivered, Is.Zero);
        harness.Engine.Systems.Get<InputSystem>().KeyDown(GeKeys.Right);
        harness.Run(10);
        Assert.That(harness.Require("salvagePlayer").GetComponent<CTransform>().Velocity.Length(), Is.LessThan(emptySpeed * 0.8));
        harness.Engine.Systems.Get<InputSystem>().KeyUp(GeKeys.Right);
        Dock(harness);
        Assert.That(scene.Cargo, Is.Zero);
        Assert.That(scene.Delivered, Is.EqualTo(3));
        Assert.That(scene.Score, Is.EqualTo(750));
    }

    [Test]
    public void CampaignUpgradesAndVictoryFreezeTheClock()
    {
        var (harness, scene) = Load();
        for (int sortie = 1; sortie <= 3; sortie++)
        {
            for (int trip = 0; trip < 5 && scene.Phase == SalvagePhase.Flight; trip++)
            {
                Collect(harness, Math.Min(scene.Capacity, scene.Quota - scene.Delivered));
                Dock(harness);
            }
            Assert.That(scene.Sortie, Is.EqualTo(sortie));
            if (sortie == 3) break;
            Assert.That(scene.Phase, Is.EqualTo(SalvagePhase.Upgrade));
            double time = scene.RemainingSeconds;
            harness.Run(120);
            Assert.That(scene.RemainingSeconds, Is.EqualTo(time));
            harness.PressAndRelease(sortie == 1 ? GeKeys.F : GeKeys.G, 1);
            harness.Run(50);
            Assert.That(scene.Capacity, Is.EqualTo(4));
        }
        Assert.That(scene.Phase, Is.EqualTo(SalvagePhase.Victory));
        int score = scene.Score;
        harness.PressAndRelease(GeKeys.D, 120);
        Assert.That(scene.Score, Is.EqualTo(score));
        Assert.That(harness.Entities.GetEntitiesWithTag("salvageRaider"), Is.Empty);
        Assert.That(harness.Engine.IsRunning, Is.True);
    }

    [Test]
    public void DamageScattersRecoverableCargoAndHasGraceTime()
    {
        var (harness, scene) = Load();
        harness.Run(160);
        Collect(harness, 2);
        var raider = harness.Entities.GetEntitiesWithTag("salvageRaider").First();
        raider.GetComponent<CTransform>().Position = harness.PositionOf("salvagePlayer");
        raider.GetComponent<CTransform>().Velocity = Vec2.Zero;
        harness.Run(1);
        Assert.That(scene.Hull, Is.EqualTo(4));
        Assert.That(scene.Cargo, Is.Zero);
        Assert.That(harness.Entities.GetEntitiesWithTag("salvageCore").Count(e => e.Active), Is.EqualTo(9));
        raider.GetComponent<CTransform>().Position = harness.PositionOf("salvagePlayer");
        harness.Run(1);
        Assert.That(scene.Hull, Is.EqualTo(4));
    }

    [Test]
    public void DashRamsThroughSweptCollisionAndCannotBeSpammed()
    {
        var (harness, scene) = Load();
        harness.Run(160);
        var player = harness.Require("salvagePlayer").GetComponent<CTransform>();
        player.Position = new Vec2(600, 350);
        harness.PressAndRelease(GeKeys.D, 1);
        harness.PressAndRelease(GeKeys.Space, 1);
        var raider = harness.Entities.GetEntitiesWithTag("salvageRaider").First();
        raider.GetComponent<CTransform>().Position = player.Position + new Vec2(70, 0);
        raider.GetComponent<CTransform>().Velocity = Vec2.Zero;
        harness.Engine.Tick(0.15);
        Assert.That(raider.Active, Is.False);
        Assert.That(scene.Score, Is.EqualTo(100));
        Assert.That(scene.Hull, Is.EqualTo(5));
        double cooldown = scene.DashCooldown;
        harness.PressAndRelease(GeKeys.Space, 1);
        Assert.That(scene.DashCooldown, Is.LessThan(cooldown));
    }

    [Test]
    public void RepeatedRaiderHitsEndTheRunAndFreezeMovement()
    {
        var (harness, scene) = Load();
        for (int frame = 0; frame < 2400 && scene.Phase == SalvagePhase.Flight; frame++)
        {
            var raider = harness.Entities.GetEntitiesWithTag("salvageRaider").FirstOrDefault(e => e.Active);
            if (raider != null)
            {
                raider.GetComponent<CTransform>().Position = harness.PositionOf("salvagePlayer");
                raider.GetComponent<CTransform>().Velocity = Vec2.Zero;
            }
            harness.Run(1);
        }
        Assert.That(scene.Phase, Is.EqualTo(SalvagePhase.Defeat));
        Assert.That(scene.Hull, Is.Zero);
        var position = harness.PositionOf("salvagePlayer");
        harness.PressAndRelease(GeKeys.D, 60);
        Assert.That(harness.PositionOf("salvagePlayer"), Is.EqualTo(position));
        Assert.That(harness.Engine.IsFaulted, Is.False);
    }

    [Test]
    public void MissedDeadlineEndsRunAndRestartAndMenuWork()
    {
        var (harness, scene) = Load();
        harness.Run(4000);
        Assert.That(scene.Phase, Is.EqualTo(SalvagePhase.Defeat));
        Assert.That(scene.RemainingSeconds, Is.Zero);
        Assert.That(scene.Hull, Is.EqualTo(5));
        Assert.That(harness.Entities.GetEntities().Count, Is.LessThan(40));
        harness.PressAndRelease(GeKeys.R, 1);
        Assert.That(harness.Require("salvageMessage").GetComponent<CText>().Text, Does.Contain("LAUNCH"));
        harness.Engine.NotifyFirstPresent();
        harness.PressAndRelease(GeKeys.Q, 1);
        Assert.That(harness.Entities.GetEntityWithTag("scene0"), Is.Not.Null);
    }

    [Test]
    public void PilotCanCompleteCampaignUsingOnlyNormalMovementInput()
    {
        var (harness, scene) = Load();
        var input = harness.Engine.Systems.Get<InputSystem>();
        int peak = 0;
        for (int frame = 0; frame < 15000 && scene.Phase is not (SalvagePhase.Defeat or SalvagePhase.Victory); frame++)
        {
            if (scene.Phase == SalvagePhase.Upgrade) harness.PressAndRelease(GeKeys.G, 1);
            var position = harness.PositionOf("salvagePlayer") + new Vec2(16, 16);
            var core = harness.Entities.GetEntitiesWithTag("salvageCore")
                .Where(e => e.Active).MinBy(e => e.GetComponent<CTransform>().Position.DistanceTo(position));
            var target = scene.Cargo >= Math.Min(scene.Capacity, scene.Quota - scene.Delivered) || core == null
                ? new Vec2(165, 400) : core.GetComponent<CTransform>().Position;
            Vec2 delta = target - position;
            foreach (var (key, active) in new[] { (GeKeys.D, delta.X > 8), (GeKeys.A, delta.X < -8),
                         (GeKeys.S, delta.Y > 8), (GeKeys.W, delta.Y < -8) })
            {
                if (active) input.KeyDown(key);
                else input.KeyUp(key);
            }
            if (scene.DashCooldown <= 0 && delta.Length() > 200) harness.PressAndRelease(GeKeys.Space, 1);
            harness.Run(1);
            peak = Math.Max(peak, harness.Entities.GetEntities().Count);
            Assert.That(harness.Engine.IsFaulted, Is.False);
        }
        Assert.That(scene.Phase, Is.EqualTo(SalvagePhase.Victory),
            $"Sortie {scene.Sortie}, banked {scene.Delivered}, hull {scene.Hull}, time {scene.RemainingSeconds}");
        Assert.That(peak, Is.LessThan(45));
    }

    private static void Collect(SceneHarness harness, int count)
    {
        var player = harness.Require("salvagePlayer").GetComponent<CTransform>();
        player.Position = new Vec2(600, 380);
        player.Velocity = Vec2.Zero;
        foreach (var core in harness.Entities.GetEntitiesWithTag("salvageCore").Where(e => e.Active).Take(count).ToArray())
            core.GetComponent<CTransform>().Position = player.Position + new Vec2(16, 16);
        harness.Run(1);
    }

    private static void Dock(SceneHarness harness)
    {
        var player = harness.Require("salvagePlayer").GetComponent<CTransform>();
        player.Position = new Vec2(149, 384);
        player.Velocity = Vec2.Zero;
        harness.Run(1);
    }
}
