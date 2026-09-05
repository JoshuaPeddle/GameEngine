using GameEngine.Core.Components;
using GameEngine.Core.Systems;
using SkiaSharp;

namespace GameEngine.Core.Tests.Unit.Systems;

public class SpriteScaleAndPickingTests
{
    private const int Virtual = 200;

    private static SKBitmap SolidTexture(int width, int height, SKColor colour)
    {
        var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(colour);
        return bitmap;
    }

    private static SKBitmap SplitTexture(int size)
    {
        var bitmap = new SKBitmap(size, size);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Red);
        using var paint = new SKPaint { Color = SKColors.Blue };
        canvas.DrawRect(new SKRect(size / 2f, 0, size, size), paint);
        return bitmap;
    }

    private static RenderSystem NewRenderSystem() =>
        new(new RenderOptions
        {
            VirtualWidth = Virtual,
            VirtualHeight = Virtual,
            DrawFps = false,
            DrawBoundingBoxes = false
        });

    private static Entity Sprite(EntityManager manager, string tag, SKBitmap texture,
        Vec2 position, Vec2? scale = null, double rotation = 0, int layer = 0)
    {
        var entity = manager.CreateEntity(tag);
        entity.AddComponent(new CTransform(position)
        {
            Scale = scale ?? new Vec2(1, 1),
            Rotation = rotation,
            Layer = layer
        });
        entity.AddComponent(new CAnimation(new Animation(texture, 1, 0)));
        return entity;
    }

    private static RenderSnapshot SnapshotOf(EntityManager manager)
    {
        manager.Update();
        var snapshot = new RenderSnapshot();
        manager.BuildRenderSnapshot(snapshot);
        return snapshot;
    }

    private static SKBitmap Render(RenderSystem renderSystem, RenderSnapshot snapshot)
    {
        var target = new SKBitmap(Virtual, Virtual);
        using var canvas = new SKCanvas(target);
        renderSystem.DrawEntitiesToCanvas(canvas, snapshot);
        return target;
    }

    private static int CountRed(SKBitmap bitmap)
    {
        int count = 0;
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                if (pixel.Red > 128 && pixel.Green < 128 && pixel.Blue < 128)
                    count++;
            }
        }
        return count;
    }

    [Test]
    public void ScaleChangesTheNumberOfPixelsASpriteCovers()
    {
        using var texture = SolidTexture(10, 10, SKColors.Red);
        var renderSystem = NewRenderSystem();

        var atOne = new EntityManager();
        Sprite(atOne, "sprite", texture, new Vec2(100, 100));
        using var single = Render(renderSystem, SnapshotOf(atOne));

        var atTwo = new EntityManager();
        Sprite(atTwo, "sprite", texture, new Vec2(100, 100), scale: new Vec2(2, 2));
        using var doubled = Render(renderSystem, SnapshotOf(atTwo));

        Assert.Multiple(() =>
        {
            Assert.That(CountRed(single), Is.EqualTo(100).Within(8));
            Assert.That(CountRed(doubled), Is.EqualTo(400).Within(24));
        });
    }

    [Test]
    public void ScaleAppliesPerAxis()
    {
        using var texture = SolidTexture(10, 10, SKColors.Red);
        var manager = new EntityManager();
        Sprite(manager, "sprite", texture, new Vec2(100, 100), scale: new Vec2(3, 1));

        using var rendered = Render(NewRenderSystem(), SnapshotOf(manager));

        Assert.That(CountRed(rendered), Is.EqualTo(300).Within(20));
    }

    [Test]
    public void NegativeScaleMirrorsTheSpriteWithoutChangingItsFootprint()
    {
        using var texture = SplitTexture(20);

        var upright = new EntityManager();
        Sprite(upright, "sprite", texture, new Vec2(100, 100));
        using var forwards = Render(NewRenderSystem(), SnapshotOf(upright));

        var mirrored = new EntityManager();
        Sprite(mirrored, "sprite", texture, new Vec2(100, 100), scale: new Vec2(-1, 1));
        using var backwards = Render(NewRenderSystem(), SnapshotOf(mirrored));

        Assert.Multiple(() =>
        {
            Assert.That(forwards.GetPixel(95, 100).Red, Is.GreaterThan(128), "unmirrored left half is red");
            Assert.That(backwards.GetPixel(95, 100).Blue, Is.GreaterThan(128), "mirrored left half is blue");
            Assert.That(CountRed(backwards), Is.EqualTo(CountRed(forwards)).Within(12));
        });
    }

    [Test]
    public void VisualSizeDoesNotChangeCollisionBounds()
    {
        var manager = new EntityManager();
        var entity = Sprite(manager, "sprite", SolidTexture(10, 10, SKColors.Red), new Vec2(0, 0),
            scale: new Vec2(4, 4));
        entity.AddComponent(new CBoundingBox(new Vec2(10, 10), false, true));

        var snapshot = SnapshotOf(manager);
        var entry = snapshot.Entries[0];

        Assert.Multiple(() =>
        {
            Assert.That(entry.BoundingBox!.Value.Width, Is.EqualTo(10));
            Assert.That(entry.BoundingBox!.Value.Height, Is.EqualTo(10));
        });
    }

    [Test]
    public void AScaledSpriteAtTheViewportEdgeIsNotCulledByItsOrigin()
    {
        using var texture = SolidTexture(10, 10, SKColors.Red);
        var manager = new EntityManager();
        var camera = manager.CreateEntity("camera");
        camera.AddComponent(new CCamera { Position = new Vec2(0, 0), Zoom = 1 });
        Sprite(manager, "sprite", texture, new Vec2(250, 0), scale: new Vec2(60, 60));

        using var rendered = Render(NewRenderSystem(), SnapshotOf(manager));

        Assert.That(CountRed(rendered), Is.GreaterThan(0),
            "a sprite whose origin is off screen but whose body is visible must still be drawn");
    }

    [Test]
    public void VisualBoundsCoverARotatedScaledSprite()
    {
        using var texture = SolidTexture(10, 10, SKColors.Red);
        var manager = new EntityManager();
        Sprite(manager, "sprite", texture, new Vec2(0, 0), scale: new Vec2(2, 2), rotation: 45);

        var bounds = RenderSystem.VisualBounds(SnapshotOf(manager).Entries[0]);

        Assert.Multiple(() =>
        {
            Assert.That(bounds.Left, Is.LessThanOrEqualTo(-14.1f));
            Assert.That(bounds.Right, Is.GreaterThanOrEqualTo(14.1f));
        });
    }

    [Test]
    public void PickingReturnsTheTopmostOverlappingSprite()
    {
        using var texture = SolidTexture(10, 10, SKColors.Red);
        var manager = new EntityManager();
        var behind = Sprite(manager, "behind", texture, new Vec2(100, 100), layer: 0);
        var infront = Sprite(manager, "infront", texture, new Vec2(100, 100), layer: 5);

        var picked = RenderSystem.PickTopmost(SnapshotOf(manager), new Vec2(100, 100));

        Assert.Multiple(() =>
        {
            Assert.That(picked, Is.EqualTo(infront.Id));
            Assert.That(picked, Is.Not.EqualTo(behind.Id));
        });
    }

    [Test]
    public void PickingUsesTheDrawnExtentOfAScaledSprite()
    {
        using var texture = SolidTexture(10, 10, SKColors.Red);
        var manager = new EntityManager();
        var entity = Sprite(manager, "sprite", texture, new Vec2(100, 100), scale: new Vec2(4, 4));
        var snapshot = SnapshotOf(manager);

        Assert.Multiple(() =>
        {
            Assert.That(RenderSystem.PickTopmost(snapshot, new Vec2(115, 100)), Is.EqualTo(entity.Id));
            Assert.That(RenderSystem.PickTopmost(snapshot, new Vec2(125, 100)), Is.Null);
        });
    }

    [Test]
    public void PickingFollowsRotation()
    {
        using var texture = SolidTexture(40, 10, SKColors.Red);
        var manager = new EntityManager();
        var entity = Sprite(manager, "sprite", texture, new Vec2(100, 100), rotation: 90);
        var snapshot = SnapshotOf(manager);

        Assert.Multiple(() =>
        {
            Assert.That(RenderSystem.PickTopmost(snapshot, new Vec2(100, 115)), Is.EqualTo(entity.Id));
            Assert.That(RenderSystem.PickTopmost(snapshot, new Vec2(115, 100)), Is.Null);
        });
    }

    [Test]
    public void PickingAccountsForCameraPositionAndZoom()
    {
        using var texture = SolidTexture(10, 10, SKColors.Red);
        var manager = new EntityManager();
        var camera = manager.CreateEntity("camera");
        camera.AddComponent(new CCamera { Position = new Vec2(500, 500), Zoom = 2 });
        var entity = Sprite(manager, "sprite", texture, new Vec2(500, 500));

        var snapshot = SnapshotOf(manager);
        var renderSystem = NewRenderSystem();
        var screenCentre = new Vec2(Virtual / 2.0, Virtual / 2.0);

        Assert.That(
            renderSystem.TryScreenToWorld(screenCentre, new Vec2(Virtual, Virtual), snapshot.ActiveCamera, out var world),
            Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(world.X, Is.EqualTo(500).Within(0.001));
            Assert.That(world.Y, Is.EqualTo(500).Within(0.001));
            Assert.That(RenderSystem.PickTopmost(snapshot, world), Is.EqualTo(entity.Id));
        });
    }

    [Test]
    public void PickingUsesTheBoundingBoxWhenAnEntityHasNoSprite()
    {
        var manager = new EntityManager();
        var entity = manager.CreateEntity("wall");
        entity.AddComponent(new CTransform(new Vec2(20, 20)));
        entity.AddComponent(new CBoundingBox(new Vec2(40, 10), false, true));

        var snapshot = SnapshotOf(manager);

        Assert.Multiple(() =>
        {
            Assert.That(RenderSystem.PickTopmost(snapshot, new Vec2(55, 25)), Is.EqualTo(entity.Id));
            Assert.That(RenderSystem.PickTopmost(snapshot, new Vec2(55, 45)), Is.Null);
        });
    }

    [Test]
    public void ASpriteOnAnEntityWithABoxIsCentredOnThatBox()
    {
        using var texture = SolidTexture(10, 10, SKColors.Red);
        var manager = new EntityManager();
        var entity = Sprite(manager, "sprite", texture, new Vec2(100, 100));
        entity.AddComponent(new CBoundingBox(new Vec2(40, 40), false, true));

        using var rendered = Render(NewRenderSystem(), SnapshotOf(manager));

        Assert.That(rendered.GetPixel(120, 120).Red, Is.GreaterThan(128),
            "the sprite is drawn at the centre of the bounding box, not at its corner");
    }
}
