using GameEngine.Core.Components;
using GameEngine.Core.Systems;
using SkiaSharp;

namespace GameEngine.Core.Tests.Unit;

public class SpriteBatchTests
{
    private SKBitmap _bitmap = null!;
    private SKImage _image = null!;

    [SetUp]
    public void Setup()
    {
        _bitmap = new SKBitmap(16, 8);
        _bitmap.Erase(SKColors.Red);
        _bitmap.SetImmutable();
        _image = SKImage.FromBitmap(_bitmap);
    }

    [TearDown]
    public void Cleanup()
    {
        _image.Dispose();
        _bitmap.Dispose();
    }

    private static SpriteInstance Sprite(double x = 20) => new(new Vec2(x, 20), new SKRect(0, 0, 8, 8));

    [Test]
    public void GroupsAdjacentTexturesAndBlendsWithoutReorderingLayers()
    {
        var entities = new EntityManager();
        entities.CreateSpriteBatch(_image, 2, 5).Add(Sprite());
        entities.CreateSpriteBatch(_image, 2, 5).Add(Sprite());
        entities.CreateSpriteBatch(_image, 2, -2, SpriteBlend.Additive).Add(Sprite());
        entities.CreateSpriteBatch(_image, 2, -2).Add(Sprite());
        using var other = SKImage.FromBitmap(_bitmap);
        entities.CreateSpriteBatch(other, 2, -2).Add(Sprite());
        var snapshot = new RenderSnapshot();
        entities.BuildRenderSnapshot(snapshot);
        var groups = snapshot.AtlasBatches.ToArray();
        Assert.That(groups.Select(g => g.Layer), Is.EqualTo(new[] { -2, -2, -2, 5 }));
        Assert.That(groups.Select(g => g.Count), Is.EqualTo(new[] { 1, 1, 1, 2 }));
        Assert.That(groups[0].Blend, Is.EqualTo(SpriteBlend.Additive));
        Assert.That(groups[2].Texture, Is.SameAs(other));
    }

    [Test]
    public void SeparatedTranslucentGroupsKeepTheirRegistrationOrder()
    {
        var entities = new EntityManager();
        entities.CreateSpriteBatch(_image, 1).Add(Sprite(1));
        entities.CreateSpriteBatch(_image, 1, blend: SpriteBlend.Additive).Add(Sprite(2));
        entities.CreateSpriteBatch(_image, 1).Add(Sprite(3));
        var snapshot = new RenderSnapshot();
        entities.BuildRenderSnapshot(snapshot);
        Assert.That(snapshot.AtlasBatches.Length, Is.EqualTo(3));
        Assert.That(snapshot.AtlasBatches[0].Transforms[0].TX, Is.EqualTo(-3));
        Assert.That(snapshot.AtlasBatches[2].Transforms[0].TX, Is.EqualTo(-1));
    }

    [Test]
    public void SnapshotCopiesValuesAndClearsUnusedSlotsOnReuse()
    {
        var entities = new EntityManager();
        var batch = entities.CreateSpriteBatch(_image, 100);
        for (int i = 0; i < 100; i++) batch.Add(Sprite(i));
        var first = new RenderSnapshot();
        var second = new RenderSnapshot();
        entities.BuildRenderSnapshot(first);
        batch.Clear();
        batch.Add(Sprite(1000));
        entities.BuildRenderSnapshot(second);
        Assert.That(first.AtlasBatches[0].Count, Is.EqualTo(100));
        Assert.That(first.AtlasBatches[0].Transforms[0].TX, Is.EqualTo(-4));
        entities.BuildRenderSnapshot(first);
        Assert.That(first.AtlasBatches[0].Count, Is.EqualTo(1));
        using var surface = SKSurface.Create(new SKImageInfo(64, 64));
        new RenderSystem(new RenderOptions { VirtualWidth = 64, VirtualHeight = 64 })
            .DrawEntitiesToCanvas(surface.Canvas, first);
        using var result = surface.Snapshot();
        using var pixels = SKBitmap.FromImage(result);
        Assert.That(pixels.GetPixel(20, 20), Is.EqualTo(SKColors.White));
        entities.RemoveSpriteBatch(batch);
        entities.BuildRenderSnapshot(first);
        Assert.That(first.AtlasBatches.Length, Is.Zero);
    }

    [Test]
    public void AtlasAndLegacyLayersInterleaveAndCameraMovesBoth()
    {
        var entities = new EntityManager();
        var background = entities.CreateEntity("legacy");
        background.AddComponent(new CTransform(new Vec2(100, 100)) { Layer = 0 });
        background.AddComponent(new CAnimation(new Animation(_bitmap, 1, 0)));
        entities.CreateSpriteBatch(_image, 1, -1).Add(new SpriteInstance(new Vec2(100, 100),
            new SKRect(0, 0, 8, 8), Color: SKColors.Black));
        entities.CreateSpriteBatch(_image, 1, 1).Add(new SpriteInstance(new Vec2(100, 100),
            new SKRect(0, 0, 8, 8), Color: SKColors.Black));
        entities.CreateEntity("camera").AddComponent(new CCamera { Position = new Vec2(100, 100), Zoom = 2 });
        entities.Update();
        var snapshot = new RenderSnapshot();
        entities.BuildRenderSnapshot(snapshot);
        using var surface = SKSurface.Create(new SKImageInfo(64, 64));
        var renderer = new RenderSystem(new RenderOptions
        {
            VirtualWidth = 64, VirtualHeight = 64, DrawBoundingBoxes = false
        });
        renderer.DrawEntitiesToCanvas(surface.Canvas, snapshot);
        using var result = surface.Snapshot();
        using var pixels = SKBitmap.FromImage(result);
        Assert.That(pixels.GetPixel(32, 32), Is.EqualTo(SKColors.Black));
        Assert.That(pixels.GetPixel(44, 32), Is.EqualTo(SKColors.Red));
    }

    [Test]
    public void RotationScaleAndAtlasRegionUseTheSpriteCentre()
    {
        var entities = new EntityManager();
        entities.CreateSpriteBatch(_image, 1).Add(new SpriteInstance(new Vec2(20, 30),
            new SKRect(8, 0, 16, 4), 2, 90));
        var snapshot = new RenderSnapshot();
        entities.BuildRenderSnapshot(snapshot);
        var matrix = snapshot.AtlasBatches[0].Transforms[0];
        Assert.That(matrix.SCos, Is.EqualTo(0).Within(0.00001));
        Assert.That(matrix.SSin, Is.EqualTo(2).Within(0.00001));
        Assert.That(matrix.TX, Is.EqualTo(24).Within(0.00001));
        Assert.That(matrix.TY, Is.EqualTo(22).Within(0.00001));
        Assert.That(snapshot.AtlasBatches[0].Regions[0].Left, Is.EqualTo(8));
    }

    [Test]
    public void AdditiveBlendAndTintAffectTheRenderedPixels()
    {
        var entities = new EntityManager();
        entities.CreateSpriteBatch(_image, 1).Add(new SpriteInstance(new Vec2(20, 20),
            new SKRect(0, 0, 8, 8), Color: SKColors.Black));
        entities.CreateSpriteBatch(_image, 1, blend: SpriteBlend.Additive).Add(new SpriteInstance(new Vec2(20, 20),
            new SKRect(0, 0, 8, 8), Color: new SKColor(128, 255, 255, 128)));
        var snapshot = new RenderSnapshot();
        entities.BuildRenderSnapshot(snapshot);
        using var surface = SKSurface.Create(new SKImageInfo(64, 64));
        new RenderSystem(new RenderOptions { VirtualWidth = 64, VirtualHeight = 64 })
            .DrawEntitiesToCanvas(surface.Canvas, snapshot);
        using var result = surface.Snapshot();
        using var pixels = SKBitmap.FromImage(result);
        Assert.That(pixels.GetPixel(20, 20).Red, Is.InRange(63, 65));
        Assert.That(pixels.GetPixel(20, 20).Green, Is.Zero);
    }

    [Test]
    public void InvisibleLeadingInstancesDoNotSuppressTheRestOfTheAtlas()
    {
        var entities = new EntityManager();
        var group = entities.CreateSpriteBatch(_image, 3, blend: SpriteBlend.Additive);
        group.Add(Sprite() with { Scale = 0.0000004f });
        group.Add(Sprite() with { Color = SKColors.Transparent });
        group.Add(Sprite() with { Color = new SKColor(255, 255, 255, 128) });
        var snapshot = new RenderSnapshot();
        entities.BuildRenderSnapshot(snapshot);
        Assert.That(snapshot.AtlasBatches[0].Count, Is.EqualTo(1));
        using var surface = SKSurface.Create(new SKImageInfo(64, 64));
        surface.Canvas.Clear(SKColors.Black);
        var renderer = new RenderSystem(new RenderOptions { VirtualWidth = 64, VirtualHeight = 64 });
        var background = entities.CreateSpriteBatch(_image, 1, -1);
        background.Add(new SpriteInstance(new Vec2(32, 32), new SKRect(0, 0, 8, 8), 8, Color: SKColors.Black));
        entities.BuildRenderSnapshot(snapshot);
        renderer.DrawEntitiesToCanvas(surface.Canvas, snapshot);
        using var result = surface.Snapshot();
        using var pixels = SKBitmap.FromImage(result);
        Assert.That(pixels.GetPixel(20, 20).Red, Is.InRange(127, 129));
    }

    private sealed class FaultingParticleScene(SKImage texture) : Scene
    {
        public override void Initialize(EntityManager entityManager, InputManager inputManager,
            AudioSystem? audioPlayer, Action<Scene?> resetScene)
        {
            Engine!.Systems.Get<ParticleSystem>().CreateEmitter(new ParticleOptions
            {
                Texture = texture, Region = new SKRect(0, 0, 8, 8)
            }).Burst(10);
            throw new InvalidOperationException("initialization failed");
        }
    }

    [Test]
    public void FailedSceneInitializationDoesNotPublishPartialParticleState()
    {
        using var engine = new Engine(audioEnabled: false);
        engine.ChangeScene(new FaultingParticleScene(_image));
        engine.Tick(0);
        Assert.That(engine.Fault, Is.Not.Null);
        Assert.That(engine.GetRenderSnapshot().AtlasBatches.Length, Is.Zero);
    }

    private sealed class MovingBatches(SpriteBatch batch) : ISystem
    {
        private int _frame;
        public void Update(EntityManager entityManager, double deltaSeconds)
        {
            _frame++;
            var instances = batch.Instances;
            for (int i = 0; i < instances.Length; i++) instances[i] = Sprite(_frame);
        }
    }

    [Test]
    public void ConcurrentReaderNeverSeesAtlasArraysRefilledWhileHeld()
    {
        using var engine = new Engine(audioEnabled: false) { TargetFrameRate = 500 };
        var batch = engine.EntityManager.CreateSpriteBatch(_image, 1000);
        for (int i = 0; i < 1000; i++) batch.Add(Sprite());
        engine.Systems.Add(new MovingBatches(batch));
        engine.Tick(0);
        engine.Start();
        try
        {
            for (int read = 0; read < 30; read++)
            {
                var held = engine.GetRenderSnapshot();
                float x = held.AtlasBatches[0].Transforms[0].TX;
                Thread.Sleep(10);
                var transforms = held.AtlasBatches[0].Transforms;
                for (int i = 0; i < transforms.Length; i++)
                    Assert.That(transforms[i].TX, Is.EqualTo(x));
            }
        }
        finally
        {
            engine.Stop();
        }
    }

    [Test]
    public void SharedLegacyImagesReflectMutableBitmapChangesOnTheNextFrame()
    {
        using var bitmap = new SKBitmap(8, 8);
        bitmap.Erase(SKColors.Red);
        var entities = new EntityManager();
        for (int i = 0; i < 2; i++)
        {
            var entity = entities.CreateEntity("mutable");
            entity.AddComponent(new CTransform(new Vec2(20 + i * 20, 20)));
            entity.AddComponent(new CAnimation(new Animation(bitmap, 1, 0)));
        }
        entities.Update();
        var snapshot = new RenderSnapshot();
        entities.BuildRenderSnapshot(snapshot);
        using var surface = SKSurface.Create(new SKImageInfo(64, 64));
        var renderer = new RenderSystem(new RenderOptions { VirtualWidth = 64, VirtualHeight = 64 });
        renderer.DrawEntitiesToCanvas(surface.Canvas, snapshot);
        bitmap.Erase(SKColors.Blue);
        renderer.DrawEntitiesToCanvas(surface.Canvas, snapshot);
        using var result = surface.Snapshot();
        using var pixels = SKBitmap.FromImage(result);
        Assert.That(pixels.GetPixel(20, 20), Is.EqualTo(SKColors.Blue));
        Assert.That(pixels.GetPixel(40, 20), Is.EqualTo(SKColors.Blue));
    }

    [Test]
    public void ImmutableLegacyImagesAreReusedAndRendererDisposalPreservesBorrowedTextures()
    {
        var entities = new EntityManager();
        var entity = entities.CreateEntity("immutable");
        entity.AddComponent(new CTransform(new Vec2(20, 20)));
        entity.AddComponent(new CAnimation(new Animation(_bitmap, 1, 0)));
        entities.Update();
        var snapshot = new RenderSnapshot();
        entities.BuildRenderSnapshot(snapshot);
        using var surface = SKSurface.Create(new SKImageInfo(64, 64));
        using var renderer = new RenderSystem(new RenderOptions { VirtualWidth = 64, VirtualHeight = 64 });
        for (int i = 0; i < 1000; i++) renderer.DrawEntitiesToCanvas(surface.Canvas, snapshot);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 120; i++) renderer.DrawEntitiesToCanvas(surface.Canvas, snapshot);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        renderer.Dispose();
        renderer.DrawEntitiesToCanvas(surface.Canvas, snapshot);
        Assert.That(allocated, Is.Zero);
        Assert.That(_bitmap.Handle, Is.Not.EqualTo(IntPtr.Zero));
        Assert.That(_image.Handle, Is.Not.EqualTo(IntPtr.Zero));
        using var result = surface.Snapshot();
        using var pixels = SKBitmap.FromImage(result);
        Assert.That(pixels.GetPixel(20, 20), Is.EqualTo(SKColors.Red));
    }

    [Test]
    public void WarmedLayerSortingAndAtlasDrawingAllocateNothing()
    {
        var entities = new EntityManager();
        for (int i = 0; i < 20; i++)
        {
            entities.CreateSpriteBatch(_image, 1, i % 3).Add(Sprite(i));
            entities.CreateEntity("layer").AddComponent(new CTransform(Vec2.Zero) { Layer = i % 3 });
        }
        entities.Update();
        var snapshot = new RenderSnapshot();
        using var surface = SKSurface.Create(new SKImageInfo(64, 64));
        var renderer = new RenderSystem(new RenderOptions { VirtualWidth = 64, VirtualHeight = 64 });
        for (int i = 0; i < 10; i++)
        {
            entities.BuildRenderSnapshot(snapshot);
            renderer.DrawEntitiesToCanvas(surface.Canvas, snapshot);
        }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10; i++)
        {
            entities.BuildRenderSnapshot(snapshot);
            renderer.DrawEntitiesToCanvas(surface.Canvas, snapshot);
        }
        Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.Zero);
    }

    [Test]
    public void HeldEngineSnapshotSurvivesParticleRecyclingAndSceneReset()
    {
        using var engine = new Engine(audioEnabled: false);
        var particles = engine.Systems.Get<ParticleSystem>();
        var emitter = particles.CreateEmitter(new ParticleOptions
        {
            Texture = _image, Region = new SKRect(0, 0, 8, 8), Life = 1, MinSpeed = 10, MaxSpeed = 10,
            Spread = 0
        });
        emitter.Burst(10);
        engine.Tick(0);
        var held = engine.GetRenderSnapshot();
        var before = held.AtlasBatches[0].Transforms.ToArray();
        for (int i = 0; i < 20; i++) engine.Tick(0.1);
        Assert.That(held.AtlasBatches[0].Transforms.ToArray(), Is.EqualTo(before));
        engine.InitializeSystems();
        engine.Tick(0);
        Assert.That(held.AtlasBatches[0].Count, Is.EqualTo(10));
        Assert.That(engine.GetRenderSnapshot().AtlasBatches.Length, Is.Zero);
        Assert.Throws<ObjectDisposedException>(() => emitter.Burst(1));
        Assert.That(_image.Handle, Is.Not.EqualTo(IntPtr.Zero));
    }
}
