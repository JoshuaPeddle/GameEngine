using System.Text;
using GameEngine.Core.Components;
using GameEngine.Core.Systems;
using SkiaSharp;

namespace GameEngine.Core.Tests.Unit;

public class AssetOwnershipTests
{
    private const string Manifest = """
        {
          "textures": { "Block": "block.png" },
          "animations": { "Block": { "texture": "Block", "frames": 1, "frameDelayMs": 0 } }
        }
        """;

    private static Assets NewAssets()
    {
        var source = new DelegateAssetSource(path =>
        {
            if (AssetManifest.IsManifestPath(path))
                return new MemoryStream(Encoding.UTF8.GetBytes(Manifest));

            using var bitmap = new SKBitmap(16, 16);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.Red);
            using var image = SKImage.FromBitmap(bitmap);
            var data = image.Encode(SKEncodedImageFormat.Png, 100);
            return new MemoryStream(data.ToArray());
        });

        return new Assets("assets.json", source);
    }

    private static bool IsAlive(SKBitmap bitmap) => bitmap.Handle != IntPtr.Zero;

    private sealed class BlockScene(Assets assets) : Scene
    {
        public override void Initialize(EntityManager entityManager, InputManager inputManager,
            AudioSystem? audioPlayer, Action<Scene?> resetScene)
        {
            var entity = entityManager.CreateEntity("block");
            entity.AddComponent(new CTransform(Vec2.Zero));
            entity.AddComponent(new CAnimation(assets.GetAnimation("Block")));
        }
    }

    [Test]
    public void RemovingAnEntityDoesNotReleaseTheSharedTexture()
    {
        using var assets = NewAssets();
        var texture = assets.GetTexture("Block");

        var manager = new EntityManager();
        var first = manager.CreateEntity("a");
        first.AddComponent(new CTransform(Vec2.Zero));
        first.AddComponent(new CAnimation(assets.GetAnimation("Block")));
        var second = manager.CreateEntity("b");
        second.AddComponent(new CTransform(Vec2.Zero));
        second.AddComponent(new CAnimation(assets.GetAnimation("Block")));
        manager.Update();

        first.Active = false;
        manager.Update();

        Assert.Multiple(() =>
        {
            Assert.That(IsAlive(texture), Is.True);
            Assert.That(second.GetComponent<CAnimation>().Texture.Handle, Is.EqualTo(texture.Handle));
        });
    }

    [Test]
    public void DisposingAnAnimationDoesNotReleaseTheTextureItBorrows()
    {
        using var assets = NewAssets();
        var texture = assets.GetTexture("Block");

        assets.GetAnimation("Block").Dispose();

        Assert.That(IsAlive(texture), Is.True);
    }

    [Test]
    public void RepeatedSceneChangesReuseTheSameScaledAnimation()
    {
        using var assets = NewAssets();

        var first = assets.GetAnimation("Block", new Vec2(64, 64));
        var second = assets.GetAnimation("Block", new Vec2(64, 64));

        Assert.Multiple(() =>
        {
            Assert.That(second, Is.SameAs(first), "a scaled animation decodes a bitmap, so it is cached");
            Assert.That(first.Texture.Width, Is.EqualTo(64));
        });
    }

    [Test]
    public void ASnapshotStillHeldByAReaderKeepsUsableTexturesAcrossASceneChange()
    {
        using var assets = NewAssets();
        using var engine = new Engine(audioEnabled: false);
        engine.ChangeScene(new BlockScene(assets));
        engine.Tick(0);
        engine.NotifyFirstPresent();
        engine.Tick(0.016);

        var held = engine.GetRenderSnapshot();
        var texture = held.Entries[0].Animation!.Value.Texture;

        engine.ChangeScene(new BlockScene(assets));
        engine.Tick(0);

        Assert.That(IsAlive(texture), Is.True,
            "a texture the host may still be painting must survive the scene that used it");
    }

    [Test]
    public void DisposingTheEngineDoesNotReleaseAssetsItDoesNotOwn()
    {
        using var assets = NewAssets();
        var texture = assets.GetTexture("Block");

        var engine = new Engine(audioEnabled: false);
        engine.ChangeScene(new BlockScene(assets));
        engine.Tick(0);
        engine.NotifyFirstPresent();
        engine.Tick(0.016);

        var held = engine.GetRenderSnapshot();
        engine.Dispose();

        Assert.Multiple(() =>
        {
            Assert.That(IsAlive(texture), Is.True);
            Assert.That(held.Entries[0].Animation!.Value.Texture.Handle, Is.EqualTo(texture.Handle));
        });
    }

    [Test]
    public void DisposingAssetsReleasesEveryTextureItDecoded()
    {
        var assets = NewAssets();
        var texture = assets.GetTexture("Block");
        var scaled = assets.GetAnimation("Block", new Vec2(32, 32));
        var scaledTexture = scaled.Texture;

        assets.Dispose();

        Assert.Multiple(() =>
        {
            Assert.That(IsAlive(texture), Is.False);
            Assert.That(IsAlive(scaledTexture), Is.False);
        });
    }

    [Test]
    public void DisposingAssetsTwiceIsSafe()
    {
        var assets = NewAssets();
        assets.Dispose();

        Assert.DoesNotThrow(assets.Dispose);
    }
}
