using GameEngine.Core.Components;
using GameEngine.Core.Systems;

namespace GameEngine.Core.Tests.Unit.Systems;

public class BroadPhaseLifetimeTests
{
    private static Entity Box(EntityManager manager, string tag, Vec2 position, Vec2 size, bool solid = false)
    {
        var entity = manager.CreateEntity(tag);
        entity.AddComponent(new CTransform(position));
        entity.AddComponent(new CBoundingBox(size, false, solid));
        return entity;
    }

    // What a frame does: MovementSystem records where every transform started the step before
    // anything moves it, and PhysicsSystem reads that to know the path a body took.
    private static void Move(Entity entity, Vec2 to)
    {
        var transform = entity.GetComponent<CTransform>();
        transform.PreviousPosition = transform.Position;
        transform.Position = to;
    }

    [Test]
    public void TravellingBodiesDoNotGrowRetainedGridStorage()
    {
        var manager = new EntityManager();
        var first = Box(manager, "a", new Vec2(0, 0), new Vec2(20, 20));
        var second = Box(manager, "b", new Vec2(0, 60), new Vec2(20, 20));
        manager.Update();

        var physics = new PhysicsSystem();
        physics.Update(manager, 1.0 / 60.0);

        int highWater = 0;

        for (int frame = 0; frame < 10_000; frame++)
        {
            Move(first, first.GetComponent<CTransform>().Position + new Vec2(5, 0));
            Move(second, second.GetComponent<CTransform>().Position + new Vec2(5, 0));
            physics.Update(manager, 1.0 / 60.0);
            highWater = Math.Max(highWater, physics.RetainedBroadPhaseCells);
        }

        Assert.Multiple(() =>
        {
            Assert.That(physics.OccupiedBroadPhaseCells, Is.LessThanOrEqualTo(16),
                "two small boxes never occupy more than a handful of cells, swept path included");
            Assert.That(highWater, Is.LessThanOrEqualTo(96),
                "retained storage must stay near demand rather than track distance travelled");
        });
    }

    [Test]
    public void RetainedStorageSettlesAtTheBusiestFrameRatherThanTheLatest()
    {
        var manager = new EntityManager();
        var crowd = new List<Entity>();
        for (int i = 0; i < 200; i++)
            crowd.Add(Box(manager, "crowd" + i, new Vec2(i * 30, 0), new Vec2(20, 20)));
        manager.Update();

        var physics = new PhysicsSystem();
        physics.Update(manager, 1.0 / 60.0);
        int busiest = physics.RetainedBroadPhaseCells;

        foreach (var entity in crowd.Skip(1))
            entity.Active = false;
        manager.Update();
        physics.Update(manager, 1.0 / 60.0);

        Assert.Multiple(() =>
        {
            Assert.That(busiest, Is.GreaterThan(100));
            Assert.That(physics.OccupiedBroadPhaseCells, Is.LessThanOrEqualTo(4));
            Assert.That(physics.RetainedBroadPhaseCells, Is.LessThan(busiest),
                "cells nothing occupies are dropped once the map drifts past demand");
        });
    }

    [Test]
    public void CollisionsAreStillFoundAfterLongTravel()
    {
        var manager = new EntityManager();
        var mover = Box(manager, "mover", new Vec2(0, 0), new Vec2(20, 20));
        Box(manager, "target", new Vec2(50_000, 0), new Vec2(20, 20));
        manager.Update();

        var physics = new PhysicsSystem();

        for (int frame = 0; frame < 4_999; frame++)
        {
            Move(mover, new Vec2(frame * 10, 0));
            physics.Update(manager, 1.0 / 60.0);
        }

        Assert.That(physics.CollisionEvents, Is.Empty);

        Move(mover, new Vec2(50_000, 0));
        physics.Update(manager, 1.0 / 60.0);

        Assert.That(physics.CollisionEvents, Has.Count.EqualTo(1));
    }

    // Resolution moves bodies after the grid is built, so a later pair is tested against a
    // position the broad phase never saw. The grid must not miss the contact that follows.
    [Test]
    public void SolidContactsResolveWhenAnEarlierResolutionMovedABody()
    {
        var manager = new EntityManager();
        var pushed = Box(manager, "pushed", new Vec2(10, 0), new Vec2(20, 20), solid: true);
        Box(manager, "left", new Vec2(0, 0), new Vec2(20, 20), solid: true);
        Box(manager, "right", new Vec2(28, 0), new Vec2(20, 20), solid: true);
        manager.Update();

        var physics = new PhysicsSystem();
        physics.Update(manager, 1.0 / 60.0);

        Assert.Multiple(() =>
        {
            Assert.That(physics.CollisionEvents, Has.Count.GreaterThanOrEqualTo(2));
            Assert.That(double.IsFinite(pushed.GetComponent<CTransform>().Position.X), Is.True);
        });
    }

    [Test]
    public void CollisionOrderIsStableAcrossRepeatedFrames()
    {
        var manager = new EntityManager();
        for (int i = 0; i < 40; i++)
            Box(manager, "e" + i, new Vec2(i * 8, (i % 3) * 8), new Vec2(20, 20));
        manager.Update();

        var physics = new PhysicsSystem();

        physics.Update(manager, 1.0 / 60.0);
        var first = physics.CollisionEvents.Select(e => (e.A.Id, e.B.Id)).ToList();

        physics.Update(manager, 1.0 / 60.0);
        var second = physics.CollisionEvents.Select(e => (e.A.Id, e.B.Id)).ToList();

        Assert.That(second, Is.EqualTo(first));
    }

    [Test]
    public void ALongWallMixedWithSmallCollidersStillFindsEveryContact()
    {
        var manager = new EntityManager();
        Box(manager, "wall", new Vec2(0, 100), new Vec2(4_000, 20));
        for (int i = 0; i < 50; i++)
            Box(manager, "pebble" + i, new Vec2(i * 80, 110), new Vec2(10, 10));
        manager.Update();

        var physics = new PhysicsSystem();
        physics.Update(manager, 1.0 / 60.0);

        Assert.That(physics.CollisionEvents, Has.Count.EqualTo(50));
    }
}
