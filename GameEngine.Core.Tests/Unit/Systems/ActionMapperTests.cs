namespace GameEngine.Core.Tests.Unit.Systems
{
    public class ActionMapperTests
    {
        [Test]
        public void MapActionToComponent_ContinuousAction_UpdatesComponentOnPress()
        {
            // Arrange
            var inputManager = new InputManager();
            var actionMapper = new ActionMapper(inputManager);
            var entityManager = new EntityManager();

            var entity = entityManager.CreateEntity("_");
            var myTestComponent = new MyTestComponent();
            entity.AddComponent(myTestComponent);

            // We'll say pressing 'W' sets "WasPressed" on MyTestComponent
            // and releasing 'W' sets "WasReleased" or something similar.
            void updateAction(MyTestComponent comp, bool isActive)
            {
                comp.IsPressed = isActive;
            }

            // Add the action to input manager and map it
            inputManager.AddAction(GeKeys.W, "MoveForward");
            actionMapper.MapActionToComponent<MyTestComponent>("MoveForward", entity, updateAction);

            // Act
            inputManager.HandleKeyPress(GeKeys.W);
            inputManager.DoActions(); // triggers callback

            // Assert
            Assert.That(myTestComponent.IsPressed, Is.True);

            // Act #2 - Release the key
            inputManager.HandleKeyRelease(GeKeys.W);
            inputManager.DoActions();

            // Assert #2
            Assert.That(myTestComponent.IsPressed, Is.False);
        }

        [Test]
        public void MapActionToComponent_OneShotAction_OnlyInvokesOnPressOrReleaseTransitions()
        {
            // Arrange
            var inputManager = new InputManager();
            var actionMapper = new ActionMapper(inputManager);
            var entityManager = new EntityManager();

            var entity = entityManager.CreateEntity("_");
            var myTestComponent = new MyTestComponent();
            entity.AddComponent(myTestComponent);

            bool pressedInvoked = false;
            bool releasedInvoked = false;

            // We'll say one-shot triggers once on press, and once on release
            void updateAction(MyTestComponent comp, bool isActive)
            {
                if (isActive)
                    pressedInvoked = true;
                else
                    releasedInvoked = true;
            }

            inputManager.AddAction(GeKeys.Space, "Jump");
            actionMapper.MapActionToComponent<MyTestComponent>("Jump", entity, updateAction, oneShot: true);

            // Act
            inputManager.HandleKeyPress(GeKeys.Space);
            inputManager.DoActions();

            Assert.Multiple(() =>
            {
                // Assert
                Assert.That(pressedInvoked, Is.True, "Should have triggered on press");
                Assert.That(releasedInvoked, Is.False, "Should not trigger release yet");
            });

            // Act #2
            inputManager.HandleKeyRelease(GeKeys.Space);
            inputManager.DoActions();

            // Assert #2
            Assert.That(releasedInvoked, Is.True, "Should trigger after release");
        }
    }

    public class MyTestComponent : Component
    {
        public bool IsPressed { get; set; }
    }
}
