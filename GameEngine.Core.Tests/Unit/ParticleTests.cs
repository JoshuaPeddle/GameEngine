using GameEngine.Core.Systems;
using SkiaSharp;

namespace GameEngine.Core.Tests.Unit;

public class ParticleTests
{
    private SKBitmap _bitmap = null!;
    private SKImage _image = null!;

    [SetUp]
    public void Setup()
    {
        _bitmap = new SKBitmap(8, 8);
        _bitmap.Erase(SKColors.White);
        _bitmap.SetImmutable();
        _image = SKImage.FromBitmap(_bitmap);
    }

    [TearDown]
    public void Cleanup()
    {
        _image.Dispose();
        _bitmap.Dispose();
    }

    private ParticleOptions Options => new()
    {
        Texture = _image, Region = new SKRect(0, 0, 8, 8), Spread = 0,
        MinSpeed = 10, MaxSpeed = 10, Life = 2, StartScale = 2, EndScale = 0,
        StartColor = SKColors.White, EndColor = new SKColor(255, 0, 0, 0)
    };

    private static ParticleState[] States(ParticleStore store)
    {
        var states = new ParticleState[store.Count];
        store.CopyTo(states);
        return states;
    }

    [Test]
    public void BurstMovesFadesScalesAndExpiresInSeconds()
    {
        using var system = new ParticleSystem(4);
        var emitter = system.CreateEmitter(Options);
        emitter.Position = new Vec2(20, 30);
        emitter.Burst(2);
        system.Store.Update(1);
        var state = States(system.Store)[0];
        Assert.Multiple(() =>
        {
            Assert.That(system.Store.Count, Is.EqualTo(2));
            Assert.That(state.Position, Is.EqualTo(new Vec2(30, 30)));
            Assert.That(state.Age, Is.EqualTo(1));
            Assert.That(state.Scale, Is.EqualTo(1));
            Assert.That(state.Color.Alpha, Is.EqualTo(127));
            Assert.That(state.Color.Green, Is.EqualTo(127));
        });
        system.Store.Update(1);
        Assert.That(system.Store.Count, Is.Zero);
        Assert.That(system.Store.Expired, Is.EqualTo(2));
        emitter.Burst(4);
        Assert.That(system.Store.Count, Is.EqualTo(4));
        Assert.That(system.Store.Dropped, Is.Zero);
    }

    [Test]
    public void CapacityDropsOldestEvenAfterSlotsAreRecycled()
    {
        using var system = new ParticleSystem(3);
        var shortLived = system.CreateEmitter(Options with { Life = 0.5f });
        var emitter = system.CreateEmitter(Options);
        shortLived.Burst(1);
        emitter.Position = new Vec2(1, 0);
        emitter.Burst(2);
        system.Store.Update(0.5);
        emitter.Position = new Vec2(20, 0);
        emitter.Burst(1);
        emitter.Position = new Vec2(30, 0);
        emitter.Burst(2);
        var states = States(system.Store);
        Assert.That(states.Select(p => p.Position.X), Is.EqualTo(new double[] { 20, 30, 30 }));
        Assert.That(system.Store.Dropped, Is.EqualTo(2));
        Assert.That(system.Store.Spawned, Is.EqualTo(6));
        Assert.That(system.Store.Expired, Is.EqualTo(1));
    }

    [Test]
    public void FixedSeedAndStepsReproduceRadiusAndConeEmission()
    {
        using var first = new ParticleSystem(64);
        using var second = new ParticleSystem(64);
        var options = Options with { Shape = ParticleShape.Radius, Radius = 20, Spread = 90, MaxSpeed = 80 };
        var a = first.CreateEmitter(options, 123);
        var b = second.CreateEmitter(options, 123);
        a.Rate = b.Rate = 17;
        for (int i = 0; i < 120; i++)
        {
            first.Update(new EntityManager(), 1.0 / 60);
            second.Update(new EntityManager(), 1.0 / 60);
        }
        Assert.That(first.Store.Spawned, Is.EqualTo(34));
        Assert.That(States(first.Store), Is.EqualTo(States(second.Store)));
        Assert.That(States(first.Store).Select(p => p.Velocity.Y).Distinct().Count(), Is.GreaterThan(1));
    }

    [Test]
    public void ContinuousEmissionAccumulatesFractionsAndCanBeStopped()
    {
        using var system = new ParticleSystem(10);
        var emitter = system.CreateEmitter(Options);
        emitter.Rate = 2.5;
        for (int i = 0; i < 4; i++) system.Update(new EntityManager(), 0.1);
        Assert.That(system.Store.Count, Is.EqualTo(1));
        emitter.Enabled = false;
        system.Update(new EntityManager(), 1);
        Assert.That(system.Store.Spawned, Is.EqualTo(1));
        system.RemoveEmitter(emitter);
        emitter.Enabled = true;
        system.Update(new EntityManager(), 1);
        Assert.That(system.Store.Count, Is.Zero);
    }

    [Test]
    public void GravityDragAndAngularVelocityAreApplied()
    {
        using var system = new ParticleSystem(1);
        var emitter = system.CreateEmitter(Options with
        {
            Gravity = new Vec2(0, 10), Drag = MathF.Log(2), Rotation = 10, AngularVelocity = 30
        });
        emitter.Burst(1);
        system.Store.Update(1);
        var state = States(system.Store)[0];
        Assert.That(state.Velocity.X, Is.EqualTo(5).Within(0.0001));
        Assert.That(state.Position.Y, Is.EqualTo(5).Within(0.0001));
        Assert.That(state.Rotation, Is.EqualTo(40));
    }

    [Test]
    public void InvalidSettingsAndDisposedStoresRejectMutation()
    {
        using var system = new ParticleSystem(1);
        Assert.Throws<ArgumentException>(() => system.CreateEmitter(Options with { Life = 0 }));
        var emitter = system.CreateEmitter(Options);
        Assert.Throws<ArgumentOutOfRangeException>(() => emitter.Rate = double.NaN);
        Assert.Throws<ArgumentOutOfRangeException>(() => system.Store.Update(double.NaN));
        system.Dispose();
        Assert.Throws<ObjectDisposedException>(() => emitter.Burst(1));
    }

    [Test]
    public void DirectEmissionCopiesBirthValuesAndUsesSharedForcesAndEndpoints()
    {
        using var system = new ParticleSystem(2);
        var options = Options with
        {
            Life = 100, StartScale = 99, StartColor = SKColors.Blue,
            EndScale = 0, EndColor = new SKColor(0, 0, 0, 0),
            Gravity = new Vec2(0, 10), AngularVelocity = 30
        };
        system.Emit(options, new ParticleSpawn(new Vec2(3, 4), new Vec2(10, 0), 2,
            Scale: 2, Color: new SKColor(200, 100, 50, 200), Rotation: 10));
        system.Update(new EntityManager(), 1);
        var state = States(system.Store)[0];
        Assert.That(state.Position, Is.EqualTo(new Vec2(13, 14)));
        Assert.That(state.Life, Is.EqualTo(2));
        Assert.That(state.Scale, Is.EqualTo(1));
        Assert.That(state.Color, Is.EqualTo(new SKColor(100, 50, 25, 100)));
        Assert.That(state.Rotation, Is.EqualTo(40));
        Assert.That(state.Region, Is.EqualTo(options.Region));
        system.Update(new EntityManager(), 1);
        Assert.That(system.Store.Count, Is.Zero);
    }

    [Test]
    public void DirectEmissionUsesOldestFirstRecyclingAndRejectsInvalidBirths()
    {
        using var system = new ParticleSystem(1);
        var options = Options;
        system.Emit(options, new ParticleSpawn(Vec2.Zero, Vec2.Zero, 1));
        system.Emit(options, new ParticleSpawn(new Vec2(5, 6), Vec2.Zero, 2));
        Assert.That(system.Store.Count, Is.EqualTo(1));
        Assert.That(system.Store.Dropped, Is.EqualTo(1));
        Assert.That(States(system.Store)[0].Position, Is.EqualTo(new Vec2(5, 6)));
        Assert.Throws<ArgumentException>(() => system.Emit(options, default));
        Assert.Throws<ArgumentException>(() => system.Emit(options,
            new ParticleSpawn(new Vec2(double.NaN, 0), Vec2.Zero, 1)));
        Assert.Throws<ArgumentException>(() => system.Emit(options with { Drag = -1 },
            new ParticleSpawn(Vec2.Zero, Vec2.Zero, 1)));
        system.Dispose();
        Assert.Throws<ObjectDisposedException>(() => system.Emit(options,
            new ParticleSpawn(Vec2.Zero, Vec2.Zero, 1)));
    }

    [Test]
    public void WarmedSimulationAndSnapshotsAllocateNothingPerParticle()
    {
        using var system = new ParticleSystem(1000);
        var emitter = system.CreateEmitter(Options with { Life = 100 });
        var entities = new EntityManager();
        var snapshot = new RenderSnapshot();
        emitter.Burst(1000);
        for (int i = 0; i < 10; i++)
        {
            system.Update(entities, 0.01);
            entities.BuildRenderSnapshot(snapshot, system);
        }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10; i++)
        {
            system.Update(entities, 0.01);
            entities.BuildRenderSnapshot(snapshot, system);
        }
        Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.Zero);
    }
}
