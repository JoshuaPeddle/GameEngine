using System.Text;
using GameEngine.Core.Components;
using GameEngine.Core.Systems;
using SkiaSharp;

namespace GameEngine.Core.Tests.Unit;

public class FrameSizedAnimationTests
{
    private static Assets Load()
    {
        const string manifest = """
            {"textures":{"sheet":"sheet.png"},"animations":{
              "single":{"texture":"sheet","frames":1,"frameDelayMs":100},
              "clip":{"texture":"sheet","frames":4,"frameDelayMs":100}}}
            """;
        return new Assets("assets.json", new DelegateAssetSource(path =>
        {
            if (AssetManifest.IsManifestPath(path)) return new MemoryStream(Encoding.UTF8.GetBytes(manifest));
            using var bitmap = new SKBitmap(8, 2);
            bitmap.Erase(SKColors.Lime);
            for (int y = 0; y < 2; y++)
            {
                bitmap.SetPixel(0, y, SKColors.Red);
                bitmap.SetPixel(1, y, SKColors.Blue);
            }
            using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
            return new MemoryStream(data.ToArray());
        }));
    }

    [TestCase("single", 8)]
    [TestCase("clip", 2)]
    public void FrameSizeIsIndependentOfFrameCountAndBorrowsTheCachedTexture(string name, int sourceWidth)
    {
        using var assets = Load();
        var animation = assets.GetAnimationForFrame(name, new Vec2(40, 40));
        Assert.Multiple(() =>
        {
            Assert.That(animation.DrawFrameSize, Is.EqualTo(new Vec2(40, 40)));
            Assert.That(animation.GetSourceRect(0).Width, Is.EqualTo(sourceWidth));
            Assert.That(animation.Texture, Is.SameAs(assets.GetTexture("sheet")));
            Assert.That(assets.GetAnimationForFrame(name, new Vec2(40, 40)), Is.SameAs(animation));
            Assert.That(animation.Sampling, Is.EqualTo(SKFilterMode.Nearest));
        });
        animation.Dispose();
        Assert.That(assets.GetTexture("sheet").Handle, Is.Not.EqualTo(IntPtr.Zero));
    }

    [Test]
    public void RenderBoundsPickingAndAnimationTimingAgreeAtTheRequestedSize()
    {
        using var assets = Load();
        var manager = new EntityManager();
        var entity = manager.CreateEntity("clip");
        entity.AddComponent(new CTransform(new Vec2(50, 50)));
        var clip = new CAnimation(assets.GetAnimationForFrame("clip", new Vec2(40, 40)));
        entity.AddComponent(clip);
        manager.Update();
        var snapshot = new RenderSnapshot();
        manager.BuildRenderSnapshot(snapshot);
        var renderer = new RenderSystem(new RenderOptions { VirtualWidth = 100, VirtualHeight = 100, DrawFps = false, DrawBoundingBoxes = false });
        using var bitmap = new SKBitmap(100, 100);
        using var canvas = new SKCanvas(bitmap);
        renderer.DrawEntitiesToCanvas(canvas, snapshot);
        Assert.Multiple(() =>
        {
            Assert.That(RenderSystem.VisualBounds(snapshot.Entries[0]), Is.EqualTo(new SKRect(30, 30, 70, 70)));
            Assert.That(RenderSystem.PickTopmost(snapshot, new Vec2(69, 50)), Is.EqualTo(entity.Id));
            Assert.That(RenderSystem.PickTopmost(snapshot, new Vec2(71, 50)), Is.Null);
            Assert.That(bitmap.GetPixel(49, 50), Is.EqualTo(SKColors.Red));
            Assert.That(bitmap.GetPixel(51, 50), Is.EqualTo(SKColors.Blue));
        });
        clip.Update(0.11);
        manager.BuildRenderSnapshot(snapshot);
        renderer.DrawEntitiesToCanvas(canvas, snapshot);
        Assert.That(bitmap.GetPixel(50, 50), Is.EqualTo(SKColors.Lime));
    }

    [Test]
    public void SamplingVariantsRemainDistinctAndLegacySheetSizingIsPreserved()
    {
        using var assets = Load();
        var nearest = assets.GetAnimationForFrame("clip", new Vec2(40, 40));
        var linear = assets.GetAnimationForFrame("clip", new Vec2(40, 40), SKFilterMode.Linear);
        Assert.That(linear, Is.Not.SameAs(nearest));
        Assert.That(linear.Sampling, Is.EqualTo(SKFilterMode.Linear));
        var legacy = assets.GetAnimation("clip", new Vec2(40, 40));
        Assert.That(legacy.DrawFrameSize, Is.EqualTo(new Vec2(10, 40)));
        Assert.That(assets.GetAnimationForSheet("clip", new Vec2(40, 40)), Is.SameAs(legacy));
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)]
    public void InvalidFrameDimensionsAreRejected(double width)
    {
        using var assets = Load();
        Assert.Throws<ArgumentOutOfRangeException>(() => assets.GetAnimationForFrame("clip", new Vec2(width, 40)));
    }
}
