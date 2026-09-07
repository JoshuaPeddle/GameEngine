using GameEngine.Core;
using GameEngine.Core.Components;
using GameEngine.Demo.Emberbrook;

namespace GameEngine.Demo.Tests;

public partial class SceneEmberbrookTests
{
    [Test]
    public void WorldSpriteSheetsAdvanceWithoutChangingTheirTileGeometry()
    {
        var (harness, scene) = Load();
        var water = harness.Entities.GetEntitiesWithTag("emberTile")
            .Single(e => e.GetComponent<CTransform>().Position == SceneEmberbrook.Position(new Cell(11, 2)));
        foreach (var entity in new[] { water, harness.Require("emberSite:campfire"), harness.Require("emberSite:elder"), harness.Require("emberSite:mossling1") })
        {
            var animation = entity.GetComponent<CAnimation>();
            Assert.That(animation.Animation.frames, Is.EqualTo(4));
            Assert.That(animation.Animation.frameWidth, Is.EqualTo(40));
            Assert.That(animation.Animation.frameHeight, Is.EqualTo(40));
            var first = animation.GetSourceRect();
            var shared = animation.Animation;
            harness.Run(18);
            Assert.That(animation.GetSourceRect(), Is.Not.EqualTo(first));
            Assert.That(animation.Animation, Is.SameAs(shared), "The clip must not restart every frame.");
        }
        Assert.That(scene.World.Player, Is.EqualTo(new Cell(5, 7)));
        Assert.That(scene.World.Hull, Is.EqualTo(20));
    }

    [Test]
    public void PlayerUsesDifferentWalkGatherAndIdleClipsWithoutMovingItsCollider()
    {
        var (harness, scene) = Load();
        harness.Run(2);
        var animation = harness.Require("emberPlayer").GetComponent<CAnimation>();
        var idle = animation.Animation;
        DesktopClick(harness, new Cell(16, 3));
        harness.Run(12);
        var walk = animation.Animation;
        Assert.That(walk, Is.Not.SameAs(idle));
        Assert.That(walk.frames, Is.EqualTo(4));
        scene.World.Restore(scene.World.Capture() with { X = 16, Y = 3 });
        DesktopClick(harness, new Cell(17, 3));
        harness.Run(12);
        var gather = animation.Animation;
        Assert.That(gather, Is.Not.SameAs(walk));
        Assert.That(gather.frameWidth, Is.EqualTo(40));
        var position = scene.World.Player;
        harness.PressAndRelease(GeKeys.X, 1);
        Assert.That(animation.Animation, Is.Not.SameAs(gather));
        harness.Run(100);
        Assert.That(scene.World.Player, Is.EqualTo(position));
        Assert.That(scene.World.Count(Item.Ore), Is.Zero);
    }

    [Test]
    public void GatheringProducesAnEffectThatExpiresAndUsesABoundedPool()
    {
        var (harness, scene) = Load();
        scene.World.Restore(scene.World.Capture() with { X = 16, Y = 3 });
        DesktopClick(harness, new Cell(17, 3));
        for (int frame = 0; frame < 200 && scene.World.Count(Item.Ore) == 0; frame++) harness.Run(1);
        var effects = harness.Entities.GetEntities().Where(e => e.Tag.StartsWith("emberEffect")).ToArray();
        Assert.That(effects, Has.Length.EqualTo(8));
        Assert.That(effects.Any(e => e.GetComponent<CAnimation>().ShouldDraw), Is.True);
        var burst = effects.Single(e => e.GetComponent<CAnimation>().ShouldDraw);
        Assert.That(burst.GetComponent<CTransform>().Position, Is.EqualTo(SceneEmberbrook.Position(new Cell(17, 3))));
        harness.Run(30);
        Assert.That(effects.All(e => !e.GetComponent<CAnimation>().ShouldDraw), Is.True);
        Assert.That(harness.Entities.GetEntities().Count(e => e.Tag.StartsWith("emberEffect")), Is.EqualTo(8));
    }

    [Test]
    public void LitBeaconUsesAnAnimatedFireWithFullSizeFrames()
    {
        var (harness, scene) = Load();
        ReadyForTrail(scene);
        scene.World.Restore(scene.World.Capture() with { Region = Region.Forest, X = 2, Y = 2, TrailQuestStage = 1, LitBeacons = 1 });
        harness.Run(3);
        var flame = harness.Require("emberSite:beacon0").GetComponent<CAnimation>();
        Assert.That(flame.Animation.frames, Is.EqualTo(4));
        Assert.That(flame.GetSourceRect().Width, Is.EqualTo(40));
        var frame = flame.GetSourceRect();
        harness.Run(12);
        Assert.That(flame.GetSourceRect(), Is.Not.EqualTo(frame));
        Assert.That(scene.World.LitBeacons, Is.EqualTo(1));
    }
    [Test]
    public void AmbientMotionIsDecorativeAndRetiresCleanlyOnRegionChanges()
    {
        var (harness, scene) = Load();
        var butterfly = harness.Entities.GetEntitiesWithTag("emberAmbient").First();
        var start = butterfly.GetComponent<CTransform>().Position;
        var progress = scene.World.Capture();
        harness.Run(40);
        Assert.That(butterfly.GetComponent<CTransform>().Position, Is.Not.EqualTo(start));
        Assert.That(scene.World.Capture(), Is.EqualTo(progress));
        Assert.That(harness.Entities.GetEntitiesWithTag("emberSmoke"), Has.Count.EqualTo(2));
        ReadyForTrail(scene);
        for (int i = 0; i < 6; i++)
        {
            scene.World.Restore(scene.World.Capture() with { Region = Region.Forest, TrailQuestStage = 1, X = 2, Y = 2 });
            harness.Run(3);
            Assert.That(harness.Entities.GetEntitiesWithTag("emberSmoke"), Is.Empty);
            Assert.That(harness.Entities.GetEntitiesWithTag("emberAmbient"), Has.Count.EqualTo(4));
            scene.World.Restore(scene.World.Capture() with { Region = Region.Ruins, X = 2, Y = 7 });
            harness.Run(3);
            Assert.That(harness.Entities.GetEntitiesWithTag("emberAmbient"), Is.Empty);
            Assert.That(harness.Entities.GetEntitiesWithTag("emberTorch"), Has.Count.EqualTo(4));
            Assert.That(harness.Entities.GetEntities().Count, Is.LessThan(750));
        }
    }

}
