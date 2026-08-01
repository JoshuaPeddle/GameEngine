using GameEngine.Core.Components;
using GameEngine.Core.Systems;

namespace GameEngine.Core.Tests.Unit.Systems;

/// <summary>
/// GE-03: resolution only ever pushed the entity that happened to come first in the pair
/// loop, and only when the other one was solid. Which entity that was depended on HashSet
/// slot order, so in Pong the ball was pushed out of a paddle or passed straight through it
/// depending on internal hashing state.
/// <para>
/// <see cref="CBoundingBox.BlockMovement"/> means "I am solid; things get pushed out of me",
/// which is how every demo scene uses it: players and balls are false, platforms, paddles
/// and walls are true.
/// </para>
/// </summary>
public class CollisionResolutionTests
{
    private static Entity Add(EntityManager manager, string tag, Vec2 position, bool solid)
    {
        var entity = manager.CreateEntity(tag);
        entity.AddComponent(new CTransform(position));
        entity.AddComponent(new CBoundingBox(new Vec2(20, 20), false, solid));
        return entity;
    }

    /// <summary>Overlapping pair, resolved once. <paramref name="solidFirst"/> flips creation order.</summary>
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
        // Arrange / Act: the same overlap, built both ways round.
        var moverFirst = ResolveOverlap(solidFirst: false);
        var solidFirst = ResolveOverlap(solidFirst: true);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(solidFirst.mover.X, Is.EqualTo(moverFirst.mover.X).Within(0.001));
            Assert.That(solidFirst.solid.X, Is.EqualTo(moverFirst.solid.X).Within(0.001));
        });
    }

    [Test]
    public void MovingSolid_BacksOutOfStationarySolid()
    {
        // Arrange: both solid, but only one is moving. Static geometry must not be shoved
        // aside by something running into it.
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

        // Act: step until the mover reaches the wall.
        var movement = new MovementSystem();
        var physics = new PhysicsSystem();
        for (int frame = 0; frame < 20; frame++)
        {
            movement.Update(manager, 0.016);
            physics.Update(manager, 0.016);
        }

        // Assert
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
        // Arrange: this is how BrickBreaker and Snake work — the scene reacts to the event
        // and does its own bouncing.
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
    public void Resolution_SeparatesAlongTheShallowAxis()
    {
        // Arrange: deep overlap horizontally, shallow vertically -> separate vertically.
        var manager = new EntityManager();
        var mover = manager.CreateEntity("mover");
        mover.AddComponent(new CTransform(new Vec2(0, 0)));
        mover.AddComponent(new CBoundingBox(new Vec2(40, 40), false, false));

        var ground = manager.CreateEntity("ground");
        ground.AddComponent(new CTransform(new Vec2(0, 35)));
        ground.AddComponent(new CBoundingBox(new Vec2(40, 40), false, true));
        manager.Update();

        // Act
        new PhysicsSystem().Update(manager, 1.0 / 60.0);

        // Assert
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
