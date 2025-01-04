using GameEngine.Core.Components;
using GameEngine.Core.Systems;

namespace GameEngine.Core.Tests.Integration.Systems;

public class PhysicsAndMovementSystemTests
{
    [Test]
    public void PhysicsAndMovement_EntityStopsAtWall()
    {
        var entityManager = new EntityManager();
        var movementSystem = new MovementSystem();
        var physicsSystem = new PhysicsSystem();

        // Setup an entity at x=0 with velocity pointing right
        var entity = entityManager.CreateEntity("_");
        entity.AddComponent(new CTransform(new Vec2(50, 50), new Vec2(100, 0)));
        entity.AddComponent(new CBoundingBox(new Vec2(10, 10), true, true));
        // Setup a "wall" entity
        var wall = entityManager.CreateEntity("_");
        wall.AddComponent(new CTransform(new Vec2(60, 50)));
        wall.AddComponent(new CBoundingBox(new Vec2(10, 10), true, true));


        for (int i = 0; i < 10; i++)
        {
            movementSystem.Update(entityManager, 16.0); // 16 ms per frame
            physicsSystem.Update(entityManager, 16.0);
            entityManager.Update();
        }

        var transform = entity.GetComponent<CTransform>();
        Assert.LessOrEqual(transform.Position.X, 60);
    }
}
