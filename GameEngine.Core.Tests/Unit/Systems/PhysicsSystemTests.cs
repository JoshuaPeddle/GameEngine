using GameEngine.Core.Components;
using GameEngine.Core.Systems;

namespace GameEngine.Core.Tests.Unit.Systems;

public class PhysicsSystemTests
{
    /// <summary>
    /// Steps one simulated second at 60fps through physics then movement, the order
    /// <see cref="Engine"/> registers them in.
    /// </summary>
    private static void SimulateOneSecond(EntityManager manager, PhysicsSystem physics, MovementSystem movement)
    {
        const double deltaSeconds = 1.0 / 60.0;
        for (int frame = 0; frame < 60; frame++)
        {
            physics.Update(manager, deltaSeconds);
            movement.Update(manager, deltaSeconds);
        }
    }

    [Test]
    public void Gravity_AcceleratesAtTheRateItDocuments()
    {
        // Arrange: GE-05 — ProcessGravity multiplied acceleration by a delta expressed in
        // milliseconds while CGravity documented pixels per second squared, so gravity ran
        // 1000x too strong.
        var manager = new EntityManager();
        var entity = manager.CreateEntity("faller");
        var transform = new CTransform(Vec2.Zero);
        entity.AddComponent(transform);
        entity.AddComponent(new CGravity { Acceleration = 200 });
        entity.AddComponent(new CMovement(0, 1e9));
        manager.Update();

        // Act
        SimulateOneSecond(manager, new PhysicsSystem(), new MovementSystem());

        // Assert
        Assert.That(transform.Velocity.Y, Is.EqualTo(200).Within(0.001),
            "after one second at 200 px/s^2 the fall speed must be 200 px/s");
    }

    [Test]
    public void Gravity_IsFrameRateIndependent()
    {
        // Arrange
        static double FallSpeedAfterOneSecond(int fps)
        {
            var manager = new EntityManager();
            var entity = manager.CreateEntity("faller");
            var transform = new CTransform(Vec2.Zero);
            entity.AddComponent(transform);
            entity.AddComponent(new CGravity { Acceleration = 200 });
            entity.AddComponent(new CMovement(0, 1e9));
            manager.Update();

            var physics = new PhysicsSystem();
            var movement = new MovementSystem();
            double deltaSeconds = 1.0 / fps;
            for (int frame = 0; frame < fps; frame++)
            {
                physics.Update(manager, deltaSeconds);
                movement.Update(manager, deltaSeconds);
            }
            return transform.Velocity.Y;
        }

        // Act / Assert
        Assert.That(FallSpeedAfterOneSecond(30), Is.EqualTo(200).Within(0.001));
        Assert.That(FallSpeedAfterOneSecond(144), Is.EqualTo(200).Within(0.001));
    }

    [Test]
    public void CollidingEntities_RaiseACollisionEvent()
    {
        // Arrange: two overlapping boxes.
        var manager = new EntityManager();

        var a = manager.CreateEntity("a");
        a.AddComponent(new CTransform(new Vec2(0, 0)));
        a.AddComponent(new CBoundingBox(new Vec2(20, 20), false, false));

        var b = manager.CreateEntity("b");
        b.AddComponent(new CTransform(new Vec2(10, 10)));
        b.AddComponent(new CBoundingBox(new Vec2(20, 20), false, false));

        manager.Update();

        // Act
        var physics = new PhysicsSystem();
        physics.Update(manager, 1.0 / 60.0);

        // Assert
        Assert.That(physics.CollisionEvents, Has.Count.EqualTo(1));
        var collision = physics.CollisionEvents[0];
        Assert.Multiple(() =>
        {
            Assert.That(collision.Overlap.X, Is.EqualTo(10).Within(0.001));
            Assert.That(collision.Overlap.Y, Is.EqualTo(10).Within(0.001));
        });
    }

    [Test]
    public void SeparatedEntities_RaiseNoCollisionEvent()
    {
        var manager = new EntityManager();

        var a = manager.CreateEntity("a");
        a.AddComponent(new CTransform(new Vec2(0, 0)));
        a.AddComponent(new CBoundingBox(new Vec2(20, 20), false, false));

        var b = manager.CreateEntity("b");
        b.AddComponent(new CTransform(new Vec2(500, 500)));
        b.AddComponent(new CBoundingBox(new Vec2(20, 20), false, false));

        manager.Update();

        var physics = new PhysicsSystem();
        physics.Update(manager, 1.0 / 60.0);

        Assert.That(physics.CollisionEvents, Is.Empty);
    }
}
