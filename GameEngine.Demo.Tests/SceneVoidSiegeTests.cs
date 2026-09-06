using GameEngine.Core;
using GameEngine.Core.Components;
using GameEngine.Core.Systems;

namespace GameEngine.Demo.Tests;

public class SceneVoidSiegeTests
{
    private static (SceneHarness Harness, SceneVoidSiege Scene) Load(bool start = true)
    {
        var scene = new SceneVoidSiege();
        var harness = SceneHarness.Load(scene);
        if (start) harness.PressAndRelease(GeKeys.Space, 1);
        return (harness, scene);
    }

    [Test]
    public void StartsOnInstructionsAndLaunchesFromTheMenu()
    {
        var harness = SceneHarness.Load(new SceneMenu());
        var item = harness.Entities.GetEntities().Single(e => e.Tag.StartsWith("scene")
            && e.TryGetComponent<CText>()?.Text == "Void Siege");
        int index = int.Parse(item.Tag[5..]);
        for (int i = 0; i < index; i++) harness.PressAndRelease(GeKeys.S, 1);
        harness.PressAndRelease(GeKeys.Space, 1);
        Assert.That(harness.Require("siegeMessage").GetComponent<CText>().Text, Does.Contain("INITIATE"));
        harness.Engine.NotifyFirstPresent();
        harness.Run(300);
        Assert.That(harness.Entities.GetEntitiesWithTag("siegeEnemy"), Is.Empty);
        harness.PressAndRelease(GeKeys.Space, 1);
        Assert.That(harness.Entities.GetEntitiesWithTag("siegeEnemy"), Has.Count.EqualTo(15));
    }

    [TestCase(GeKeys.D)]
    [TestCase(GeKeys.Right)]
    public void MovementAndDashRespectTheArena(GeKeys right)
    {
        var (harness, scene) = Load();
        var start = harness.PositionOf("siegePlayer");
        harness.PressAndRelease(right, 6);
        var afterMove = harness.PositionOf("siegePlayer");
        harness.PressAndRelease(GeKeys.Space, 8);
        Assert.That(harness.PositionOf("siegePlayer").X - afterMove.X, Is.GreaterThan(70));
        Assert.That(afterMove.X, Is.GreaterThan(start.X));
        Assert.That(scene.DashCooldown, Is.GreaterThan(1));
        harness.PressAndRelease(right, 180);
        Assert.That(harness.PositionOf("siegePlayer").X, Is.InRange(50, 1198));
        Assert.That(harness.Engine.IsRunning, Is.True);
    }

    [Test]
    public void AutoFireKillsThroughTheRealSweptCollisionPath()
    {
        var (harness, scene) = Load();
        var enemy = harness.Entities.GetEntitiesWithTag("siegeEnemy").First();
        enemy.GetComponent<CSiegeActor>().Hull = 1;
        enemy.GetComponent<CTransform>().Position = harness.PositionOf("siegePlayer") + new Vec2(0, -70);
        enemy.GetComponent<CMovement>().MaxSpeed = 0;
        harness.Run(35);
        Assert.That(enemy.Active, Is.False);
        Assert.That(scene.Score, Is.GreaterThanOrEqualTo(100));
        Assert.That(scene.EnemiesRemaining, Is.LessThan(15));
    }

    [Test]
    public void EmpHasARadiusAndCannotBeRepeatedDuringCooldown()
    {
        var (harness, scene) = Load();
        var enemies = harness.Entities.GetEntitiesWithTag("siegeEnemy").ToArray();
        var near = enemies[0];
        var far = enemies[1];
        near.GetComponent<CTransform>().Position = harness.PositionOf("siegePlayer") + new Vec2(60, 0);
        far.GetComponent<CTransform>().Position = new Vec2(60, 140);
        harness.PressAndRelease(GeKeys.E, 1);
        Assert.That(near.Active, Is.False);
        Assert.That(far.Active, Is.True);
        Assert.That(scene.PulseCooldown, Is.GreaterThan(6));
        far.GetComponent<CTransform>().Position = harness.PositionOf("siegePlayer") + new Vec2(60, 0);
        harness.PressAndRelease(GeKeys.E, 1);
        Assert.That(far.Active, Is.True);
        Assert.That(harness.Require("siegePulse").GetComponent<CAnimation>().ShouldDraw, Is.True);
    }

    [Test]
    public void DestructibleCoverAbsorbsShots()
    {
        var (harness, _) = Load();
        var cover = harness.Entities.GetEntitiesWithTag("siegeCover").First();
        cover.GetComponent<CSiegeActor>().Hull = 1;
        cover.GetComponent<CTransform>().Position = harness.PositionOf("siegePlayer") + new Vec2(-13, -80);
        var target = harness.Entities.GetEntitiesWithTag("siegeEnemy").First();
        target.GetComponent<CTransform>().Position = harness.PositionOf("siegePlayer") + new Vec2(0, -150);
        target.GetComponent<CMovement>().MaxSpeed = 0;
        harness.Run(35);
        Assert.That(cover.Active, Is.False);
        Assert.That(target.Active, Is.True);
    }

    [Test]
    public void SplitterChildrenKeepTheWaveAlive()
    {
        var (harness, scene) = Load();
        ClearWave(harness, scene);
        harness.PressAndRelease(GeKeys.F, 1);
        foreach (var enemy in harness.Entities.GetEntitiesWithTag("siegeEnemy"))
        {
            enemy.GetComponent<CSiegeActor>().Hull = 1;
            enemy.GetComponent<CTransform>().Position = harness.PositionOf("siegePlayer") + new Vec2(60, 0);
        }
        harness.PressAndRelease(GeKeys.E, 1);
        Assert.That(scene.Phase, Is.EqualTo(SiegePhase.Combat));
        Assert.That(scene.EnemiesRemaining, Is.GreaterThan(0));
        Assert.That(harness.Entities.GetEntitiesWithTag("siegeEnemy").Any(e => e.GetComponent<CSiegeActor>().Small), Is.True);
    }

    [Test]
    public void CampaignOffersUpgradesReachesBossAndEndsInVictory()
    {
        var (harness, scene) = Load();
        for (int wave = 1; wave <= 5; wave++)
        {
            Assert.That(scene.Wave, Is.EqualTo(wave));
            if (wave == 5)
            {
                var boss = harness.Entities.GetEntitiesWithTag("siegeEnemy").Single(e => e.GetComponent<CSiegeActor>().Kind == SiegeKind.Boss);
                boss.GetComponent<CSiegeActor>().Hull = 90;
                boss.GetComponent<CSiegeActor>().Cooldown = 0;
                harness.Run(2);
                Assert.That(harness.Entities.GetEntitiesWithTag("siegeShot").Count, Is.GreaterThanOrEqualTo(24));
                Assert.That(harness.Require("siegeBossStatus").GetComponent<CText>().Text, Does.Contain("OVERDRIVE"));
            }
            ClearWave(harness, scene);
            if (wave < 5)
            {
                Assert.That(scene.Phase, Is.EqualTo(SiegePhase.Upgrade));
                int score = scene.Score;
                harness.Run(120);
                Assert.That(scene.Wave, Is.EqualTo(wave));
                Assert.That(scene.Score, Is.EqualTo(score));
                harness.PressAndRelease(wave % 2 == 0 ? GeKeys.H : GeKeys.F, 1);
                Assert.That(scene.Phase, Is.EqualTo(SiegePhase.Combat));
            }
        }
        Assert.That(scene.Phase, Is.EqualTo(SiegePhase.Victory));
        Assert.That(scene.Salvage, Is.GreaterThan(0));
        int finalScore = scene.Score;
        harness.Run(600);
        Assert.That(scene.Score, Is.EqualTo(finalScore));
        Assert.That(harness.Engine.IsRunning, Is.True);
    }

    [Test]
    public void ContactDamageHasGraceTimeAndDefeatFreezesCombat()
    {
        var (harness, scene) = Load();
        for (int frame = 0; frame < 1000 && scene.Phase == SiegePhase.Combat; frame++)
        {
            var enemy = harness.Entities.GetEntitiesWithTag("siegeEnemy").First(e => e.Active);
            enemy.GetComponent<CSiegeActor>().Hull = 10000;
            enemy.GetComponent<CTransform>().Position = harness.PositionOf("siegePlayer");
            harness.Run(1);
            if (frame == 30) Assert.That(scene.Hull, Is.EqualTo(6));
        }
        Assert.That(scene.Phase, Is.EqualTo(SiegePhase.Defeat));
        var position = harness.PositionOf("siegePlayer");
        harness.Run(120);
        Assert.That(scene.Hull, Is.Zero);
        Assert.That(harness.PositionOf("siegePlayer"), Is.EqualTo(position));
        Assert.That(harness.Entities.GetEntitiesWithTag("siegeShot"), Is.Empty);
        harness.PressAndRelease(GeKeys.R, 1);
        Assert.That(harness.Require("siegeStatus").GetComponent<CText>().Text, Does.Contain("HULL 6/6"));
        harness.Engine.NotifyFirstPresent();
        harness.PressAndRelease(GeKeys.Q, 1);
        Assert.That(harness.Entities.GetEntityWithTag("scene0"), Is.Not.Null);
    }

    [Test]
    public void AShotCannotTunnelThroughAnEnemyDuringALongStep()
    {
        var (harness, _) = Load();
        var shot = harness.Entities.GetEntitiesWithTag("siegeShot").First();
        var target = harness.Entities.GetEntitiesWithTag("siegeEnemy").First();
        target.GetComponent<CSiegeActor>().Hull = 1;
        target.GetComponent<CTransform>().Position = new Vec2(700, 350);
        target.GetComponent<CMovement>().MaxSpeed = 0;
        shot.GetComponent<CTransform>().Position = new Vec2(600, 360);
        shot.GetComponent<CTransform>().Velocity = new Vec2(1000, 0);
        harness.Engine.Tick(0.2);
        Assert.That(target.Active, Is.False);
        Assert.That(shot.Active, Is.False);
        Assert.That(harness.Engine.IsRunning, Is.True);
    }

    [Test]
    public void SustainedCombatThroughNormalInputKeepsEntityCountsBounded()
    {
        var (harness, scene) = Load();
        var input = harness.Engine.Systems.Get<InputSystem>();
        Vec2[] route = [new(1090, 600), new(180, 600), new(180, 180), new(1090, 180)];
        int waypoint = 0;
        int peak = 0;
        for (int frame = 0; frame < 12000 && scene.Phase is not (SiegePhase.Victory or SiegePhase.Defeat); frame++)
        {
            if (scene.Phase == SiegePhase.Upgrade)
                harness.PressAndRelease(scene.Hull < 4 ? GeKeys.H : GeKeys.F, 1);
            var position = harness.PositionOf("siegePlayer");
            var delta = route[waypoint] - position;
            if (delta.Length() < 30) waypoint = (waypoint + 1) % route.Length;
            foreach (var (key, active) in new[] { (GeKeys.D, delta.X > 15), (GeKeys.A, delta.X < -15),
                         (GeKeys.S, delta.Y > 15), (GeKeys.W, delta.Y < -15) })
            {
                if (active) input.KeyDown(key);
                else input.KeyUp(key);
            }
            if (scene.PulseCooldown <= 0 && harness.Entities.GetEntitiesWithTag("siegeEnemy")
                    .Any(e => e.GetComponent<CTransform>().Position.DistanceTo(position) < 200))
                harness.PressAndRelease(GeKeys.E, 1);
            harness.Run(1);
            peak = Math.Max(peak, harness.Entities.GetEntities().Count);
            Assert.That(harness.Entities.GetEntitiesWithTag("siegeShot").Count, Is.LessThanOrEqualTo(220));
            Assert.That(harness.Engine.IsRunning, Is.True);
        }
        Assert.That(scene.Phase, Is.AnyOf(SiegePhase.Victory, SiegePhase.Defeat));
        Assert.That(scene.Wave, Is.GreaterThanOrEqualTo(3));
        Assert.That(peak, Is.InRange(200, 450));
        Assert.That(harness.Entities.GetEntitiesWithTag("siegeSpark"), Has.Count.EqualTo(128));
    }

    private static void ClearWave(SceneHarness harness, SceneVoidSiege scene)
    {
        for (int frame = 0; frame < 2000 && scene.Phase == SiegePhase.Combat; frame++)
        {
            foreach (var enemy in harness.Entities.GetEntitiesWithTag("siegeEnemy"))
            {
                var actor = enemy.GetComponent<CSiegeActor>();
                actor.Hull = 1;
                actor.Cooldown = 100;
                enemy.GetComponent<CTransform>().Position = harness.PositionOf("siegePlayer") + new Vec2(0, -65);
                enemy.GetComponent<CMovement>().MaxSpeed = 0;
            }
            harness.Run(1);
        }
        Assert.That(scene.Phase, Is.Not.EqualTo(SiegePhase.Combat), "the wave must finish via projectile collisions");
    }
}
