using GameEngine.Core.Components;
using GameEngine.Core.Systems;

namespace GameEngine.Core.Tests.Unit.Systems
{
    public class MovementSystemTests
    {
        [Test]
        public void MovementSystem_UpdatesPositionBasedOnVelocity()
        {
            // Arrange
            var entityManager = new EntityManager();
            var movementSystem = new MovementSystem();
            var entity = entityManager.CreateEntity("_");
            entity.AddComponent(new CTransform(new Vec2(0, 0), new Vec2(10, 0)));
            entity.AddComponent<CMovement>();

            // Act
            movementSystem.Update(entityManager, deltaMs: 100); // 100 ms
            entityManager.Update(); // commit changes if your ECS defers them

            // Assert
            var transform = entity.GetComponent<CTransform>();
            Assert.Multiple(() =>
            {
                Assert.That(transform.Position.X, Is.EqualTo(1.0)); // 10 px/sec * 0.1 sec
                Assert.That(transform.Position.Y, Is.EqualTo(0.0));
            });
        }
    }
}