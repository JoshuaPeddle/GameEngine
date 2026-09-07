using GameEngine.Core;
using GameEngine.Core.Components;
using GameEngine.Demo.Emberbrook;

namespace GameEngine.Demo.Tests;

public partial class SceneEmberbrookTests
{
    [Test]
    public void StopButtonAndXCancelGatheringAndClearProgress()
    {
        var (harness, scene) = Load();
        scene.World.Restore(scene.World.Capture() with { X = 16, Y = 3 });
        Click(harness, SceneEmberbrook.Position(new Cell(17, 3)));
        harness.Run(40);
        Assert.That(scene.World.ActionProgress, Is.InRange(0.3, 0.6));
        Assert.That(harness.Require("emberActionFill").GetComponent<CAnimation>().ShouldDraw, Is.True);
        Click(harness, new Vec2(695, 66));
        Assert.That(scene.World.Activity, Is.EqualTo("Idle"));
        Assert.That(scene.World.ActionProgress, Is.Zero);
        harness.Run(150);
        Assert.That(scene.World.Count(Item.Ore), Is.Zero);
        Click(harness, SceneEmberbrook.Position(new Cell(17, 3)));
        harness.Run(30);
        harness.PressAndRelease(GeKeys.X, 1);
        Assert.That(scene.World.Activity, Is.EqualTo("Idle"));
        Assert.That(harness.Require("emberActionFill").GetComponent<CAnimation>().ShouldDraw, Is.False);
    }

    [Test]
    public void HealingShowsBoundedFeedbackAndTheFullQuestFitsTheSidebar()
    {
        var (harness, scene) = Load();
        scene.World.Restore(scene.World.Capture() with { Hull = 4 });
        harness.Run(2);
        harness.PressAndRelease(GeKeys.E, 1);
        Assert.That(harness.Entities.GetEntities().Any(e => e.Tag.StartsWith("emberFloat")
            && e.GetComponent<CText>().Text == "+8" && e.GetComponent<CText>().ShouldDraw), Is.True);
        Assert.That(harness.Require("emberHealthFill").GetComponent<CTransform>().Scale.X, Is.EqualTo(0.6));
        harness.Run(80);
        Assert.That(harness.Entities.GetEntities().Where(e => e.Tag.StartsWith("emberFloat"))
            .All(e => !e.GetComponent<CText>().ShouldDraw), Is.True);
        ReadyForTrail(scene);
        scene.World.Restore(scene.World.Capture() with { TrailQuestStage = 3, LitBeacons = 7 });
        harness.Run(2);
        string displayed = string.Join(" ", Enumerable.Range(0, 4)
            .Select(i => harness.Require("emberQuest" + i).GetComponent<CText>().Text)).Trim();
        Assert.That(displayed, Is.EqualTo(scene.World.Quest));
        Assert.That(harness.Entities.GetEntities().Count(e => e.Tag.StartsWith("emberFloat")), Is.EqualTo(8));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ForestCanBeCompletedWithQuestLevelEquipmentAndDodging(bool spear)
    {
        var (harness, scene) = Load();
        ReadyForTrail(scene);
        var world = scene.World;
        world.Restore(world.Capture() with { TrailQuestStage = 1, Region = Region.Forest, X = 2, Y = 2,
            CombatXp = 105, HasSpear = spear, SpearEquipped = spear, Rations = 0, Meals = 6, Logs = 6 });
        Visit(harness, scene, "beacon0");
        Visit(harness, scene, "wolf1");
        harness.PressAndRelease(GeKeys.E, 1);
        Visit(harness, scene, "wolf2");
        harness.PressAndRelease(GeKeys.E, 1);
        Visit(harness, scene, "beacon1");
        Visit(harness, scene, "beacon2");
        Assert.That(world.BeaconCount, Is.EqualTo(3));
        var hart = world.Sites.Single(s => s.Id == "hart");
        Click(harness, SceneEmberbrook.Position(hart.Cell));
        bool dodging = false;
        int dodges = 0;
        for (int frame = 0; frame < 4000 && !world.HartDefeated; frame++)
        {
            if (world.DangerCentre is { } centre && !dodging)
            {
                var safe = new Cell(centre.X == 19 ? 18 : 19, centre.Y <= 7 ? 9 : 6);
                Assert.That(world.IsWalkable(safe), Is.True);
                Assert.That(world.IsDangerous(safe), Is.False);
                Click(harness, SceneEmberbrook.Position(safe));
                dodging = true;
                dodges++;
            }
            else if (world.DangerCentre == null && dodging)
            {
                Click(harness, SceneEmberbrook.Position(hart.Cell));
                dodging = false;
            }
            if (world.Hull <= 10) harness.PressAndRelease(GeKeys.E, 1);
            harness.Run(1);
            Assert.That(world.Region, Is.EqualTo(Region.Forest), "The adventurer should survive with ordinary quest equipment.");
        }
        Assert.That(world.HartDefeated, Is.True);
        Assert.That(dodges, Is.GreaterThan(0));
        Assert.That(harness.Engine.IsFaulted, Is.False);
    }
}
