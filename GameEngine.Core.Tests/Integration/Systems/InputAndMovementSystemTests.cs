using GameEngine.Core.Components;
using GameEngine.Core.Systems;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameEngine.Core.Tests.Integration.Systems
{
    public class InputAndMovementSystemTests
    {

        [Test]
        public void InputCausesEntityMovement_IntegrationTest()
        {
            var entityManager = new EntityManager();
            var inputManager = new InputManager();
            var actionMapper = new ActionMapper(inputManager);
            var movementSystem = new MovementSystem();

            // 2. Create an entity that should move when "MoveForward" is active
            var entity = entityManager.CreateEntity("_");
            entity.AddComponent(new CTransform(new Vec2(0, 0), new Vec2(0, 0)));
            entity.AddComponent<CMovement>();
            entityManager.Update();

            // 3. Add an action to input manager & map it so that pressing W sets velocity
            //    This is purely an example approach. Your actual logic may differ.
            inputManager.AddAction(GeKeys.W, "MoveForward");

            actionMapper.MapActionToComponent<CTransform>(
                "MoveForward",
                entity,
                (velocityComp, isActive) =>
                {
                    velocityComp.Velocity = isActive ? new Vec2(10, 0) : Vec2.Zero;
                },
                oneShot: false
            );

            // 4. Press W
            inputManager.HandleKeyPress(GeKeys.W);
            inputManager.DoActions(); // triggers callback -> sets velocity to (10,0)

            movementSystem.Update(entityManager, 100);

            // 6. Assert that position changed as we expect (10 px/sec for 0.1 seconds = 1 px)
            var transform = entity.GetComponent<CTransform>();
            Assert.Multiple(() =>
            {
                Assert.That(transform.Position.X, Is.EqualTo(1.0));
                Assert.That(transform.Position.Y, Is.EqualTo(0.0));
            });

            // 7. Release W
            inputManager.HandleKeyRelease(GeKeys.W);
            inputManager.DoActions();

            // Next update, velocity should be (0,0)
            movementSystem.Update(entityManager, 100);
            entityManager.Update();

            Assert.Multiple(() =>
            {
                // Position should remain the same
                Assert.That(transform.Position.X, Is.EqualTo(1.0));
                Assert.That(transform.Position.Y, Is.EqualTo(0.0));
            });
        }
    }
}
