using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using GameEngine.Core;
using GameEngine.Core.Components;
using GameEngine.Core.Systems;
using SkiaSharp;

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);

[MemoryDiagnoser]
public class EngineBenchmarks
{
    private EntityManager _snapshotEntities = null!;
    private EntityManager _physicsEntities = null!;
    private RenderSnapshot _snapshot = null!;
    private PhysicsSystem _physics = null!;

    [GlobalSetup]
    public void Setup()
    {
        _snapshotEntities = SceneFactory.CreateEntities(1_000, includeAnimations: true);
        _snapshot = new RenderSnapshot();
        _snapshotEntities.BuildRenderSnapshot(_snapshot);

        _physicsEntities = SceneFactory.CreatePhysicsEntities(1_000);
        _physics = new PhysicsSystem();
        _physics.Update(_physicsEntities, 1.0 / 60.0);
    }

    [Benchmark]
    public void BuildRenderSnapshot() => _snapshotEntities.BuildRenderSnapshot(_snapshot);

    [Benchmark]
    public void PhysicsUpdate() => _physics.Update(_physicsEntities, 1.0 / 60.0);
}

[MemoryDiagnoser]
public class RenderBenchmarks
{
    private SKBitmap _texture = null!;
    private SKSurface _surface = null!;
    private RenderSystem _renderer = null!;
    private RenderSnapshot _snapshot = null!;

    [GlobalSetup]
    public void Setup()
    {
        _texture = SceneFactory.CreateTexture();
        var entities = SceneFactory.CreateEntities(1_000, includeAnimations: true, _texture);
        _snapshot = new RenderSnapshot();
        entities.BuildRenderSnapshot(_snapshot);

        _surface = SKSurface.Create(new SKImageInfo(1_600, 1_600));
        _renderer = new RenderSystem(new RenderOptions
        {
            VirtualWidth = 1_600,
            VirtualHeight = 1_600,
            DrawAnimations = true,
            DrawBoundingBoxes = true,
            DrawEntityCenters = false,
            DrawFps = false
        });
    }

    [Benchmark]
    public void DrawFrame() => _renderer.DrawEntitiesToCanvas(_surface.Canvas, _snapshot);

    [GlobalCleanup]
    public void Cleanup()
    {
        _surface.Dispose();
        _texture.Dispose();
    }
}

internal static class SceneFactory
{
    public static EntityManager CreateEntities(
        int count,
        bool includeAnimations,
        SKBitmap? texture = null)
    {
        var entities = new EntityManager();
        texture ??= CreateTexture();
        var animation = new Animation(texture, frames: 1, delayMs: 100);

        for (int i = 0; i < count; i++)
        {
            var entity = entities.CreateEntity($"render-{i}");
            entity.AddComponent(new CTransform(new Vec2((i % 40) * 40, (i / 40) * 40))
            {
                Layer = i % 8,
                Rotation = i % 360
            });
            entity.AddComponent(new CBoundingBox(new Vec2(32, 32), blockVision: false, blockMove: false));

            if (includeAnimations)
                entity.AddComponent(new CAnimation(animation));
        }

        entities.Update();
        return entities;
    }

    public static EntityManager CreatePhysicsEntities(int count)
    {
        var entities = new EntityManager();
        var random = new Random(0x5EED);

        for (int i = 0; i < count; i++)
        {
            var entity = entities.CreateEntity($"physics-{i}");
            entity.AddComponent(new CTransform(new Vec2(
                random.NextDouble() * 8_000,
                random.NextDouble() * 8_000)));
            entity.AddComponent(new CBoundingBox(
                new Vec2(random.Next(12, 49), random.Next(12, 49)),
                blockVision: false,
                blockMove: false));
        }

        entities.Update();
        return entities;
    }

    public static SKBitmap CreateTexture()
    {
        var texture = new SKBitmap(32, 32);
        using var canvas = new SKCanvas(texture);
        canvas.Clear(SKColors.CornflowerBlue);
        return texture;
    }
}
