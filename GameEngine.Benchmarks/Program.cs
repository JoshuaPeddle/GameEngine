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

// GE-88: the broad phase used to keep a bucket for every cell any collider had ever
// occupied, so a travelling world grew its grid without bound. These cover the shapes that
// stress it differently: a still world, a world in motion, spawn/despawn churn, a dense
// pile, and a long wall among small colliders.
[MemoryDiagnoser]
public class BroadPhaseBenchmarks
{
    private const int Count = 1_000;

    private EntityManager _steady = null!;
    private PhysicsSystem _steadyPhysics = null!;

    private EntityManager _moving = null!;
    private List<CTransform> _movingTransforms = null!;
    private PhysicsSystem _movingPhysics = null!;

    private EntityManager _dense = null!;
    private PhysicsSystem _densePhysics = null!;

    private EntityManager _mixed = null!;
    private PhysicsSystem _mixedPhysics = null!;

    [GlobalSetup]
    public void Setup()
    {
        _steady = SceneFactory.CreatePhysicsEntities(Count);
        _steadyPhysics = Warmed(_steady);

        _moving = SceneFactory.CreatePhysicsEntities(Count);
        _movingTransforms = _moving.GetEntitiesWithComponents<CTransform>().Select(e => e.Item2).ToList();
        _movingPhysics = Warmed(_moving);

        _dense = SceneFactory.CreateDenseOverlaps(Count);
        _densePhysics = Warmed(_dense);

        _mixed = SceneFactory.CreateWallAndPebbles(Count);
        _mixedPhysics = Warmed(_mixed);
    }

    private static PhysicsSystem Warmed(EntityManager entities)
    {
        var physics = new PhysicsSystem();
        physics.Update(entities, 1.0 / 60.0);
        return physics;
    }

    [Benchmark(Description = "Steady world, 1000 colliders")]
    public int Steady()
    {
        _steadyPhysics.Update(_steady, 1.0 / 60.0);
        return _steadyPhysics.RetainedBroadPhaseCells;
    }

    [Benchmark(Description = "Moving world, 1000 colliders travelling")]
    public int Moving()
    {
        // What a frame does before physics runs: record where the step started, then move.
        foreach (var transform in _movingTransforms)
        {
            transform.PreviousPosition = transform.Position;
            transform.Position += new Vec2(11, 7);
        }

        _movingPhysics.Update(_moving, 1.0 / 60.0);
        return _movingPhysics.RetainedBroadPhaseCells;
    }

    [Benchmark(Description = "Spawn, collide and despawn 1000 colliders")]
    public int Churn()
    {
        var entities = SceneFactory.CreatePhysicsEntities(Count);
        var physics = new PhysicsSystem();
        physics.Update(entities, 1.0 / 60.0);

        foreach (var entity in entities.GetEntities())
            entity.Active = false;
        entities.Update();
        physics.Update(entities, 1.0 / 60.0);

        return physics.RetainedBroadPhaseCells;
    }

    [Benchmark(Description = "Dense overlaps, 1000 colliders in one pile")]
    public int DenseOverlaps()
    {
        _densePhysics.Update(_dense, 1.0 / 60.0);
        return _densePhysics.CollisionEvents.Count;
    }

    [Benchmark(Description = "Long walls among small colliders")]
    public int WallsAndPebbles()
    {
        _mixedPhysics.Update(_mixed, 1.0 / 60.0);
        return _mixedPhysics.CollisionEvents.Count;
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

    public static EntityManager CreateDenseOverlaps(int count)
    {
        var entities = new EntityManager();
        var random = new Random(0x0DE5E);

        for (int i = 0; i < count; i++)
        {
            var entity = entities.CreateEntity($"dense-{i}");
            entity.AddComponent(new CTransform(new Vec2(
                random.NextDouble() * 200,
                random.NextDouble() * 200)));
            entity.AddComponent(new CBoundingBox(new Vec2(32, 32), blockVision: false, blockMove: false));
        }

        entities.Update();
        return entities;
    }

    public static EntityManager CreateWallAndPebbles(int count)
    {
        var entities = new EntityManager();

        for (int wall = 0; wall < 4; wall++)
        {
            var barrier = entities.CreateEntity($"wall-{wall}");
            barrier.AddComponent(new CTransform(new Vec2(0, wall * 500)));
            barrier.AddComponent(new CBoundingBox(new Vec2(8_000, 24), blockVision: false, blockMove: false));
        }

        var random = new Random(0x2A11);
        for (int i = 0; i < count; i++)
        {
            var pebble = entities.CreateEntity($"pebble-{i}");
            pebble.AddComponent(new CTransform(new Vec2(
                random.NextDouble() * 8_000,
                random.NextDouble() * 2_000)));
            pebble.AddComponent(new CBoundingBox(new Vec2(12, 12), blockVision: false, blockMove: false));
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

// GE-23 end to end: the microbenchmarks favour a plain Dictionary on creation and key
// enumeration and ConcurrentDictionary on lookups, so the answer depends on how much a
// scene spawns and despawns. This drives the real EntityManager on both paths.
[MemoryDiagnoser]
public class EntityChurnBenchmarks
{
    private const int Count = 1_000;

    private EntityManager _steadyState = null!;

    [GlobalSetup]
    public void Setup()
    {
        _steadyState = new EntityManager();
        Populate(_steadyState, Count);
        _steadyState.Update();
    }

    private static void Populate(EntityManager entities, int count)
    {
        for (int i = 0; i < count; i++)
        {
            var entity = entities.CreateEntity($"churn-{i}");
            entity.AddComponent(new CTransform(new Vec2(i % 100, i / 100)));
            entity.AddComponent(new CBoundingBox(new Vec2(16, 16), blockVision: false, blockMove: false));
            entity.AddComponent<CInput>();
        }
    }

    [Benchmark(Description = "Spawn 1000 entities and flush")]
    public int Spawn()
    {
        var entities = new EntityManager();
        Populate(entities, Count);
        entities.Update();
        return entities.GetEntities().Count;
    }

    [Benchmark(Description = "Spawn then despawn 1000 entities")]
    public int SpawnAndDespawn()
    {
        var entities = new EntityManager();
        Populate(entities, Count);
        entities.Update();

        foreach (var entity in entities.GetEntities())
            entity.Active = false;

        entities.Update();
        return entities.GetEntities().Count;
    }

    [Benchmark(Description = "Steady-state component lookups")]
    public int SteadyStateLookups()
    {
        int hits = 0;
        foreach (var entity in _steadyState.GetEntities())
        {
            if (entity.TryGetComponent<CTransform>(out _)) hits++;
            if (entity.TryGetComponent<CBoundingBox>(out _)) hits++;
            if (entity.TryGetComponent<CInput>(out _)) hits++;
            if (entity.TryGetComponent<CGravity>(out _)) hits++;
        }
        return hits;
    }
}
