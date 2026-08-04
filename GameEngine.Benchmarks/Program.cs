using System.Collections.Concurrent;
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

// GE-23: does Entity.Components need to be a ConcurrentDictionary? The editor no longer
// reads components off the engine thread, so the question is purely what the concurrent
// type costs. Both variants are exercised with the access pattern Entity actually uses.
[MemoryDiagnoser]
public class ComponentStorageBenchmarks
{
    private const int Entities = 1_000;

    private static readonly Type[] ComponentTypes =
    [
        typeof(CTransform), typeof(CBoundingBox), typeof(CAnimation), typeof(CInput)
    ];

    private ConcurrentDictionary<Type, Component>[] _concurrent = null!;
    private Dictionary<Type, Component>[] _plain = null!;

    [GlobalSetup]
    public void Setup()
    {
        _concurrent = new ConcurrentDictionary<Type, Component>[Entities];
        _plain = new Dictionary<Type, Component>[Entities];

        for (int i = 0; i < Entities; i++)
        {
            _concurrent[i] = new ConcurrentDictionary<Type, Component>();
            _plain[i] = new Dictionary<Type, Component>();
            FillConcurrent(_concurrent[i]);
            FillPlain(_plain[i]);
        }
    }

    private static void FillConcurrent(ConcurrentDictionary<Type, Component> store)
    {
        store[typeof(CTransform)] = new CTransform(Vec2.Zero);
        store[typeof(CBoundingBox)] = new CBoundingBox(new Vec2(32, 32), false, false);
        store[typeof(CInput)] = new CInput();
    }

    private static void FillPlain(Dictionary<Type, Component> store)
    {
        store[typeof(CTransform)] = new CTransform(Vec2.Zero);
        store[typeof(CBoundingBox)] = new CBoundingBox(new Vec2(32, 32), false, false);
        store[typeof(CInput)] = new CInput();
    }

    [Benchmark(Description = "Create 1000 entity stores (ConcurrentDictionary)")]
    public int CreateConcurrent()
    {
        int count = 0;
        for (int i = 0; i < Entities; i++)
        {
            var store = new ConcurrentDictionary<Type, Component>();
            FillConcurrent(store);
            count += store.Count;
        }
        return count;
    }

    [Benchmark(Description = "Create 1000 entity stores (Dictionary)")]
    public int CreatePlain()
    {
        int count = 0;
        for (int i = 0; i < Entities; i++)
        {
            var store = new Dictionary<Type, Component>();
            FillPlain(store);
            count += store.Count;
        }
        return count;
    }

    [Benchmark(Description = "Per-frame lookups (ConcurrentDictionary)")]
    public int LookupConcurrent()
    {
        int hits = 0;
        foreach (var store in _concurrent)
        {
            foreach (var type in ComponentTypes)
            {
                if (store.TryGetValue(type, out _))
                    hits++;
            }
        }
        return hits;
    }

    [Benchmark(Description = "Per-frame lookups (Dictionary)")]
    public int LookupPlain()
    {
        int hits = 0;
        foreach (var store in _plain)
        {
            foreach (var type in ComponentTypes)
            {
                if (store.TryGetValue(type, out _))
                    hits++;
            }
        }
        return hits;
    }

    [Benchmark(Description = "Enumerate keys on removal (ConcurrentDictionary)")]
    public int EnumerateConcurrent()
    {
        int seen = 0;
        foreach (var store in _concurrent)
        {
            foreach (var _ in store.Keys)
                seen++;
        }
        return seen;
    }

    [Benchmark(Description = "Enumerate keys on removal (Dictionary)")]
    public int EnumeratePlain()
    {
        int seen = 0;
        foreach (var store in _plain)
        {
            foreach (var _ in store.Keys)
                seen++;
        }
        return seen;
    }
}
