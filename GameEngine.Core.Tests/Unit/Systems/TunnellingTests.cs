using GameEngine.Core.Components;
using GameEngine.Core.Systems;

namespace GameEngine.Core.Tests.Unit.Systems;

public class TunnellingTests
{
    private static Entity Body(EntityManager manager, string tag, Vec2 position, Vec2 size,
        Vec2 velocity = default, bool solid = true)
    {
        var entity = manager.CreateEntity(tag);
        entity.AddComponent(new CTransform(position, velocity));
        entity.AddComponent(new CBoundingBox(size, false, solid));
        entity.AddComponent(new CMovement(0, 100_000));
        return entity;
    }

    private static (EntityManager Entities, MovementSystem Movement, PhysicsSystem Physics) World()
    {
        return (new EntityManager(), new MovementSystem(), new PhysicsSystem());
    }

    private static void Step(
        (EntityManager Entities, MovementSystem Movement, PhysicsSystem Physics) world, double seconds)
    {
        world.Movement.Update(world.Entities, seconds);
        world.Physics.Update(world.Entities, seconds);
        world.Entities.Update();
    }

    [Test]
    public void AFastBodyDoesNotCrossAThinSolidWall()
    {
        var world = World();
        var bullet = Body(world.Entities, "bullet", new Vec2(0, 0), new Vec2(4, 4), new Vec2(3_000, 0));
        Body(world.Entities, "wall", new Vec2(200, -50), new Vec2(2, 100));
        world.Entities.Update();

        for (int frame = 0; frame < 30; frame++)
            Step(world, 1.0 / 60.0);

        Assert.That(bullet.GetComponent<CTransform>().Position.X, Is.LessThanOrEqualTo(200.001),
            "the bullet must stop at the wall rather than pass through it");
    }

    [Test]
    public void AFastBodyReportsTheContactItWouldHaveTunnelledThrough()
    {
        var world = World();
        var bullet = Body(world.Entities, "bullet", new Vec2(0, 0), new Vec2(4, 4), new Vec2(3_000, 0));
        var wall = Body(world.Entities, "wall", new Vec2(30, -50), new Vec2(2, 100));
        world.Entities.Update();

        Step(world, 1.0 / 60.0);

        var contact = world.Physics.CollisionEvents.Single();

        Assert.Multiple(() =>
        {
            Assert.That(new[] { contact.A.Id, contact.B.Id }, Does.Contain(bullet.Id));
            Assert.That(new[] { contact.A.Id, contact.B.Id }, Does.Contain(wall.Id));
        });
    }

    [Test]
    public void ASweptContactPreservesTheImpactVelocity()
    {
        var world = World();
        Body(world.Entities, "bullet", new Vec2(0, 0), new Vec2(4, 4), new Vec2(3_000, 0));
        Body(world.Entities, "wall", new Vec2(30, -50), new Vec2(2, 100));
        world.Entities.Update();

        Step(world, 1.0 / 60.0);

        var contact = world.Physics.CollisionEvents.Single();
        double impact = contact.A.Tag == "bullet" ? contact.VelocityA.X : contact.VelocityB.X;

        Assert.That(impact, Is.EqualTo(3_000).Within(1e-6),
            "the event must carry the speed at impact, not the speed after separation");
    }

    [Test]
    public void AFastBodyPassesThroughANonSolidTriggerButStillReportsIt()
    {
        var world = World();
        var bullet = Body(world.Entities, "bullet", new Vec2(0, 0), new Vec2(4, 4), new Vec2(3_000, 0));
        Body(world.Entities, "trigger", new Vec2(30, -50), new Vec2(2, 100), solid: false);
        world.Entities.Update();

        Step(world, 1.0 / 60.0);

        Assert.Multiple(() =>
        {
            Assert.That(world.Physics.CollisionEvents, Has.Count.EqualTo(1));
            Assert.That(bullet.GetComponent<CTransform>().Position.X, Is.GreaterThan(30));
        });
    }

    [Test]
    public void TwoBodiesClosingOnEachOtherDoNotPassThrough()
    {
        var world = World();
        var left = Body(world.Entities, "left", new Vec2(0, 0), new Vec2(4, 4), new Vec2(2_000, 0));
        var right = Body(world.Entities, "right", new Vec2(100, 0), new Vec2(4, 4), new Vec2(-2_000, 0));
        world.Entities.Update();

        for (int frame = 0; frame < 10; frame++)
            Step(world, 1.0 / 60.0);

        Assert.That(left.GetComponent<CTransform>().Position.X,
            Is.LessThanOrEqualTo(right.GetComponent<CTransform>().Position.X + 0.001),
            "the two bodies must not swap sides");
    }

    [Test]
    public void ABodyAlreadyOverlappingIsSeparatedRatherThanSwept()
    {
        var world = World();
        var mover = Body(world.Entities, "mover", new Vec2(5, 0), new Vec2(20, 20));
        Body(world.Entities, "block", new Vec2(0, 0), new Vec2(20, 20));
        world.Entities.Update();

        Step(world, 1.0 / 60.0);

        Assert.Multiple(() =>
        {
            Assert.That(world.Physics.CollisionEvents, Has.Count.EqualTo(1));
            Assert.That(world.Physics.CollisionEvents[0].Overlap.X, Is.GreaterThan(0));
            Assert.That(mover.GetComponent<CTransform>().Position.X, Is.EqualTo(12.5).Within(0.001),
                "neither body moved, so a starting overlap is shared between them");
        });
    }

    [Test]
    public void SimultaneousContactsOnBothSidesAreAllReported()
    {
        var world = World();
        Body(world.Entities, "mover", new Vec2(0, 0), new Vec2(10, 10), new Vec2(4_000, 0));
        Body(world.Entities, "wallA", new Vec2(30, -100), new Vec2(2, 105));
        Body(world.Entities, "wallB", new Vec2(30, 5), new Vec2(2, 100));
        world.Entities.Update();

        Step(world, 1.0 / 60.0);

        Assert.That(world.Physics.CollisionEvents, Has.Count.EqualTo(2));
    }

    [Test]
    public void ASlowBodyIsUnaffectedByTheSweep()
    {
        var world = World();
        var mover = Body(world.Entities, "mover", new Vec2(0, 0), new Vec2(10, 10), new Vec2(60, 0));
        Body(world.Entities, "wall", new Vec2(500, -50), new Vec2(2, 100));
        world.Entities.Update();

        for (int frame = 0; frame < 60; frame++)
            Step(world, 1.0 / 60.0);

        Assert.Multiple(() =>
        {
            Assert.That(world.Physics.CollisionEvents, Is.Empty);
            Assert.That(mover.GetComponent<CTransform>().Position.X, Is.EqualTo(60).Within(0.001));
        });
    }

    [Test]
    public void AFastBodyStopsAtAWallOnTheVerticalAxisToo()
    {
        var world = World();
        var faller = Body(world.Entities, "faller", new Vec2(0, 0), new Vec2(4, 4), new Vec2(0, 3_000));
        Body(world.Entities, "floor", new Vec2(-50, 200), new Vec2(100, 2));
        world.Entities.Update();

        for (int frame = 0; frame < 30; frame++)
            Step(world, 1.0 / 60.0);

        Assert.That(faller.GetComponent<CTransform>().Position.Y, Is.LessThanOrEqualTo(200.001));
    }
}
