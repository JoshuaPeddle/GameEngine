using System.Text;
using GameEngine.Core.Components;
using GameEngine.Core.Systems;
using SkiaSharp;

namespace GameEngine.Core.Tests.Unit;

public class EngineAssetLifetimeTests
{
    private static IAssetSource Source(Action? textureOpened = null) => new DelegateAssetSource(path =>
    {
        if (AssetManifest.IsManifestPath(path))
            return new MemoryStream(Encoding.UTF8.GetBytes("""
                {"textures":{"block":"block.png"},"animations":{"block":{"texture":"block","frames":1,"frameDelayMs":0}}}
                """));
        textureOpened?.Invoke();
        using var bitmap = new SKBitmap(4, 4);
        bitmap.Erase(SKColors.Red);
        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        return new MemoryStream(data.ToArray());
    });

    private sealed class AssetScene(bool failUnload = false, bool failInitialize = false) : Scene
    {
        public Assets Assets = null!;
        public int Unloads;
        public override void Initialize(EntityManager entities, InputManager input, AudioSystem? audio, Action<Scene?> reset)
        {
            Assets = LoadAssets("assets.json");
            var entity = entities.CreateEntity("block");
            entity.AddComponent(new CTransform(new Vec2(20, 20)));
            entity.AddComponent(new CAnimation(Assets.GetAnimationForSheet("block", new Vec2(16, 16))));
            if (failInitialize) throw new InvalidOperationException("initialization failed");
        }
        public override void Unload()
        {
            Unloads++;
            if (failUnload) throw new InvalidOperationException("unload failed");
        }
    }

    [Test]
    public void RepeatedSceneChangesReuseDecodedAndScaledTexturesAndUnloadEachScene()
    {
        int decoded = 0;
        using var engine = new Engine(audioEnabled: false, assetSource: Source(() => decoded++));
        var first = new AssetScene();
        engine.ChangeScene(first);
        engine.Tick(0);
        var texture = first.Assets.GetTexture("block");
        var scaled = first.Assets.GetAnimationForSheet("block", new Vec2(16, 16));
        for (int i = 0; i < 20; i++)
        {
            var next = new AssetScene();
            engine.ChangeScene(next);
            engine.Tick(0);
            Assert.That(next.Assets, Is.SameAs(first.Assets));
            Assert.That(next.Assets.GetAnimationForSheet("block", new Vec2(16, 16)), Is.SameAs(scaled));
        }
        Assert.That(decoded, Is.EqualTo(1));
        Assert.That(first.Unloads, Is.EqualTo(1));
        Assert.That(first.Engine, Is.Null);
        engine.Dispose();
        Assert.That(texture.Handle, Is.EqualTo(IntPtr.Zero));
        Assert.That(scaled.Texture.Handle, Is.EqualTo(IntPtr.Zero));
    }

    [Test]
    public void EngineDisposalWaitsForEverySnapshotLeaseAndDoubleReleaseIsSafe()
    {
        var engine = new Engine(audioEnabled: false, assetSource: Source());
        var scene = new AssetScene();
        engine.ChangeScene(scene);
        engine.Tick(0);
        var first = engine.AcquireRenderSnapshot();
        var second = engine.AcquireRenderSnapshot();
        var texture = first.Snapshot.Entries[0].Animation!.Value.Texture;
        engine.Dispose();
        Assert.That(scene.Unloads, Is.EqualTo(1));
        Assert.That(texture.Handle, Is.Not.EqualTo(IntPtr.Zero));
        first.Dispose();
        first.Dispose();
        Assert.That(texture.Handle, Is.Not.EqualTo(IntPtr.Zero));
        using (var output = new SKBitmap(100, 100))
        using (var canvas = new SKCanvas(output))
            new RenderSystem(new RenderOptions { VirtualWidth = 100, VirtualHeight = 100, DrawBoundingBoxes = false }).DrawEntitiesToCanvas(canvas, second.Snapshot);
        second.Dispose();
        Assert.That(texture.Handle, Is.EqualTo(IntPtr.Zero));
        Assert.Throws<ObjectDisposedException>(() => _ = first.Snapshot);
        using var afterDisposal = engine.AcquireRenderSnapshot();
        Assert.That(afterDisposal.Snapshot.Entries.Length, Is.Zero);
        Assert.Throws<ObjectDisposedException>(() => engine.LoadAssets("assets.json"));
    }

    [Test]
    public void LeasedBuffersStayStableAcrossManySceneChanges()
    {
        using var engine = new Engine(audioEnabled: false, assetSource: Source());
        engine.ChangeScene(new AssetScene());
        engine.Tick(0);
        using var first = engine.AcquireRenderSnapshot();
        int originalId = first.Snapshot.Entries[0].EntityId;
        for (int i = 0; i < 10; i++)
        {
            engine.ChangeScene(new AssetScene());
            engine.Tick(0);
            using var current = engine.AcquireRenderSnapshot();
            Assert.That(current.Snapshot.Entries[0].EntityId, Is.Not.EqualTo(originalId));
            Assert.That(first.Snapshot.Entries[0].EntityId, Is.EqualTo(originalId));
        }
    }

    [Test]
    public void LegacyReaderCanReturnItsFinalSnapshotAfterEngineDisposal()
    {
        var engine = new Engine(audioEnabled: false, assetSource: Source());
        engine.ChangeScene(new AssetScene());
        engine.Tick(0);
        var snapshot = engine.GetRenderSnapshot();
        var texture = snapshot.Entries[0].Animation!.Value.Texture;
        engine.Dispose();
        Assert.That(texture.Handle, Is.Not.EqualTo(IntPtr.Zero));
        engine.ReleaseRenderSnapshot();
        engine.ReleaseRenderSnapshot();
        Assert.That(texture.Handle, Is.EqualTo(IntPtr.Zero));
    }

    [Test]
    public void AnUnloadFaultIsReportedOnceAndAnotherSceneCanRecover()
    {
        using var engine = new Engine(audioEnabled: false, assetSource: Source());
        var scene = new AssetScene(failUnload: true);
        engine.ChangeScene(scene);
        engine.Tick(0);
        engine.ChangeScene(new AssetScene());
        engine.Tick(0);
        Assert.That(engine.Fault!.Operation, Is.EqualTo("scene unload"));
        Assert.That(scene.Engine, Is.Null);
        engine.ChangeScene(new AssetScene());
        engine.Tick(0);
        Assert.That(engine.Fault, Is.Null);
        Assert.That(scene.Unloads, Is.EqualTo(1));
    }

    [Test]
    public void PartiallyInitializedScenesAreUnloadedAndTheirOwnedAssetsAreReleased()
    {
        var engine = new Engine(audioEnabled: false, assetSource: Source());
        var scene = new AssetScene(failInitialize: true);
        engine.ChangeScene(scene);
        engine.Tick(0);
        var texture = scene.Assets.GetTexture("block");
        engine.Dispose();
        Assert.That(scene.Unloads, Is.EqualTo(1));
        Assert.That(texture.Handle, Is.EqualTo(IntPtr.Zero));
    }
}
