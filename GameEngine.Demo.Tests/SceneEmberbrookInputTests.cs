using GameEngine.Core;
using GameEngine.Core.Systems;
using GameEngine.Demo.Emberbrook;

namespace GameEngine.Demo.Tests;

public partial class SceneEmberbrookTests
{
    private static void DesktopClick(SceneHarness harness, Cell cell)
    {
        var point = SceneEmberbrook.Position(cell);
        Click(harness, point);
        var viewport = ViewportTransform.Create(harness.Input.RealResolution, new Vec2(1280, 800), ScalingStrategy.Letterbox);
        var real = new Vec2(point.X * viewport.ScaleX + viewport.OffsetX, point.Y * viewport.ScaleY + viewport.OffsetY);
        harness.Input.HandlePointerEvent(Pointer.PointerEventType.Release, new Pointer.PointerReleaseEvent(real));
        harness.PressAndRelease(SwipeGesture.Classify(real, real), 6);
    }

    [Test]
    public void DesktopMouseReleaseDoesNotCancelWalking()
    {
        var (harness, scene) = Load();
        var destination = new Cell(18, 7);
        DesktopClick(harness, destination);
        harness.Run(300);
        Assert.That(scene.World.Player, Is.EqualTo(destination));
        Assert.That(scene.World.Activity, Is.EqualTo("Idle"));
    }

    [TestCase("ore1")]
    [TestCase("fish1")]
    [TestCase("ash1")]
    public void DesktopMouseReleaseAllowsSingleAndRepeatedGathering(string id)
    {
        var (harness, scene) = Load();
        if (id == "ash1")
        {
            ReadyForTrail(scene);
            scene.World.Restore(scene.World.Capture() with { TrailQuestStage = 1, Region = Region.Forest, X = 2, Y = 2 });
        }
        var site = scene.World.Sites.Single(s => s.Id == id);
        var item = id == "ore1" ? Item.Ore : id == "fish1" ? Item.Trout : Item.Log;
        DesktopClick(harness, site.Cell);
        harness.Run(500);
        Assert.That(scene.World.Count(item), Is.EqualTo(1), "One released click should finish one gather.");
        harness.PressAndRelease(GeKeys.M, 1);
        DesktopClick(harness, site.Cell);
        harness.Run(1000);
        Assert.That(scene.World.RepeatGathering, Is.True);
        Assert.That(scene.World.Count(item), Is.GreaterThanOrEqualTo(3), "Repeat should survive mouse release and resource regrowth.");
        harness.PressAndRelease(GeKeys.X, 1);
        int gathered = scene.World.Count(item);
        harness.Run(400);
        Assert.That(scene.World.Activity, Is.EqualTo("Idle"));
        Assert.That(scene.World.Count(item), Is.EqualTo(gathered));
    }
}
