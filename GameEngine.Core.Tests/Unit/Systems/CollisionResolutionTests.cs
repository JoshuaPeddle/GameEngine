using GameEngine.Core.Components;
using GameEngine.Core.Systems;

namespace GameEngine.Core.Tests.Unit.Systems;

public class CollisionResolutionTests
{
    private static Entity Add(EntityManager manager, string tag, Vec2 position, bool solid)
    {
        var entity = manager.CreateEntity(tag);
        entity.AddComponent(new CTransform(position));
        entity.AddComponent(new CBoundingBox(new Vec2(20, 20), false, solid));
        return entity;
    }

    private static (Vec2 mover, Vec2 solid) ResolveOverlap(bool solidFirst)
    {
        var manager = new EntityManager();
        Entity mover, solid;

        if (solidFirst)
        {
            solid = Add(manager, "solid", new Vec2(10, 0), solid: true);
            mover = Add(manager, "mover", new Vec2(0, 0), solid: false);
        }
        else
        {
            mover = Add(manager, "mover", new Vec2(0, 0), solid: false);
            solid = Add(manager, "solid", new Vec2(10, 0), solid: true);
        }

        manager.Update();
        new PhysicsSystem().Update(manager, 1.0 / 60.0);

        return (mover.GetComponent<CTransform>().Position,
                solid.GetComponent<CTransform>().Position);
    }

    [Test]
    public void SolidEntity_PushesOutTheNonSolidOne()
    {
        var (mover, solid) = ResolveOverlap(solidFirst: false);

        Assert.Multiple(() =>
        {
            Assert.That(mover.X, Is.EqualTo(-10).Within(0.001), "the non-solid entity absorbs the whole separation");
            Assert.That(solid.X, Is.EqualTo(10).Within(0.001), "the solid entity does not move");
        });
    }

    [Test]
    public void Resolution_DoesNotDependOnCreationOrder()
    {
        var moverFirst = ResolveOverlap(solidFirst: false);
        var solidFirst = ResolveOverlap(solidFirst: true);

        Assert.Multiple(() =>
        {
            Assert.That(solidFirst.mover.X, Is.EqualTo(moverFirst.mover.X).Within(0.001));
            Assert.That(solidFirst.solid.X, Is.EqualTo(moverFirst.solid.X).Within(0.001));
        });
    }

    [Test]
    public void MovingSolid_BacksOutOfStationarySolid()
    {
        var manager = new EntityManager();

        var mover = manager.CreateEntity("mover");
        var moverTransform = new CTransform(new Vec2(0, 0), new Vec2(100, 0));
        mover.AddComponent(moverTransform);
        mover.AddComponent(new CBoundingBox(new Vec2(20, 20), false, true));
        mover.AddComponent<CMovement>();

        var wall = manager.CreateEntity("wall");
        wall.AddComponent(new CTransform(new Vec2(30, 0)));
        wall.AddComponent(new CBoundingBox(new Vec2(20, 20), false, true));
        manager.Update();

        var movement = new MovementSystem();
        var physics = new PhysicsSystem();
        for (int frame = 0; frame < 20; frame++)
        {
            movement.Update(manager, 0.016);
            physics.Update(manager, 0.016);
        }

        Assert.Multiple(() =>
        {
            Assert.That(moverTransform.Position.X, Is.EqualTo(10).Within(0.001),
                "the mover stops with its edge against the wall");
            Assert.That(wall.GetComponent<CTransform>().Position.X, Is.EqualTo(30).Within(0.001),
                "the wall does not budge");
        });
    }

    [Test]
    public void TwoSolids_SeparateSymmetrically()
    {
        var manager = new EntityManager();
        var a = Add(manager, "a", new Vec2(0, 0), solid: true);
        var b = Add(manager, "b", new Vec2(10, 0), solid: true);
        manager.Update();

        new PhysicsSystem().Update(manager, 1.0 / 60.0);

        Assert.Multiple(() =>
        {
            Assert.That(a.GetComponent<CTransform>().Position.X, Is.EqualTo(-5).Within(0.001));
            Assert.That(b.GetComponent<CTransform>().Position.X, Is.EqualTo(15).Within(0.001));
        });
    }

    [Test]
    public void NeitherSolid_ReportsTheCollisionButMovesNothing()
    {
        var manager = new EntityManager();
        var a = Add(manager, "ball", new Vec2(0, 0), solid: false);
        var b = Add(manager, "brick", new Vec2(10, 0), solid: false);
        manager.Update();

        var physics = new PhysicsSystem();
        physics.Update(manager, 1.0 / 60.0);

        Assert.Multiple(() =>
        {
            Assert.That(physics.CollisionEvents, Has.Count.EqualTo(1), "the event must still be reported");
            Assert.That(a.GetComponent<CTransform>().Position.X, Is.EqualTo(0).Within(0.001));
            Assert.That(b.GetComponent<CTransform>().Position.X, Is.EqualTo(10).Within(0.001));
        });
    }

    [Test]
    public void CollisionEvent_CarriesVelocityFromBeforeResolution()
    {
        var manager = new EntityManager();

        var mover = manager.CreateEntity("ball");
        var transform = new CTransform(new Vec2(0, 0), new Vec2(-600, 40));
        mover.AddComponent(transform);
        mover.AddComponent(new CBoundingBox(new Vec2(20, 20), false, false));

        var paddle = manager.CreateEntity("paddle");
        paddle.AddComponent(new CTransform(new Vec2(10, 0)));
        paddle.AddComponent(new CBoundingBox(new Vec2(20, 20), false, true));
        manager.Update();

        var physics = new PhysicsSystem();
        physics.Update(manager, 1.0 / 60.0);

        Assert.That(physics.CollisionEvents, Has.Count.EqualTo(1));
        var collision = physics.CollisionEvents[0];

        Assert.Multiple(() =>
        {
            Assert.That(collision.VelocityA.X, Is.EqualTo(-600).Within(0.001),
                "the event reports the velocity at impact");
            Assert.That(collision.VelocityA.Y, Is.EqualTo(40).Within(0.001),
                "including the axis that was not separated");
            Assert.That(transform.Velocity.X, Is.EqualTo(0).Within(0.001),
                "while the live velocity has already been zeroed by resolution");
        });
    }

    [Test]
    public void Resolution_SeparatesAlongTheShallowAxis()
    {
        var manager = new EntityManager();
        var mover = manager.CreateEntity("mover");
        mover.AddComponent(new CTransform(new Vec2(0, 0)));
        mover.AddComponent(new CBoundingBox(new Vec2(40, 40), false, false));

        var ground = manager.CreateEntity("ground");
        ground.AddComponent(new CTransform(new Vec2(0, 35)));
        ground.AddComponent(new CBoundingBox(new Vec2(40, 40), false, true));
        manager.Update();

        new PhysicsSystem().Update(manager, 1.0 / 60.0);

        var position = mover.GetComponent<CTransform>().Position;
        Assert.Multiple(() =>
        {
            Assert.That(position.Y, Is.EqualTo(-5).Within(0.001), "pushed up out of the ground");
            Assert.That(position.X, Is.EqualTo(0).Within(0.001), "no horizontal displacement");
        });
    }

    [Test]
    public void Resolution_ZeroesVelocityOnTheSeparationAxisOnly()
    {
        var manager = new EntityManager();
        var mover = manager.CreateEntity("mover");
        var transform = new CTransform(new Vec2(0, 0), new Vec2(30, 90));
        mover.AddComponent(transform);
        mover.AddComponent(new CBoundingBox(new Vec2(40, 40), false, false));

        var ground = manager.CreateEntity("ground");
        ground.AddComponent(new CTransform(new Vec2(0, 35)));
        ground.AddComponent(new CBoundingBox(new Vec2(40, 40), false, true));
        manager.Update();

        new PhysicsSystem().Update(manager, 1.0 / 60.0);

        Assert.Multiple(() =>
        {
            Assert.That(transform.Velocity.Y, Is.EqualTo(0).Within(0.001), "vertical motion stops on landing");
            Assert.That(transform.Velocity.X, Is.EqualTo(30).Within(0.001), "horizontal motion is preserved");
        });
    }
}
