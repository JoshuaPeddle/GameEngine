namespace GameEngine.Core.Tests.Unit.Systems
{
    public class InputManagerTests
    {
        [Test]
        public void PressingKey_UpdatesActionState_AndCallsBoundCallback()
        {
            // Arrange
            var inputManager = new InputManager();
            inputManager.AddAction(GeKeys.W, "MoveForward");

            bool callbackInvoked = false;
            bool callbackValue = false;

            // Bind the action to a callback that sets callbackInvoked and callbackValue
            inputManager.BindAction("MoveForward", isActive =>
            {
                callbackInvoked = true;
                callbackValue = isActive;
            });

            // Act
            inputManager.HandleKeyPress(GeKeys.W);

            // The bound callback is only called in DoActions()
            inputManager.DoActions();

            Assert.Multiple(() =>
            {
                // Assert
                Assert.That(callbackInvoked, Is.True, "Callback was not invoked");
                Assert.That(callbackValue, Is.True, "Callback value should be 'true' after key press");
                Assert.That(inputManager.IsActionActive("MoveForward"), Is.True, "Action state should be true after key press");
            });
        }

        [Test]
        public void ReleasingKey_UpdatesActionState_AndCallsBoundCallback()
        {
            // Arrange
            var inputManager = new InputManager();
            inputManager.AddAction(GeKeys.Space, "Jump");

            bool callbackValue = false;
            inputManager.BindAction("Jump", isActive => callbackValue = isActive);

            // Press the key first
            inputManager.HandleKeyPress(GeKeys.Space);
            inputManager.DoActions();
            Assert.That(callbackValue, Is.True);

            // Act
            // Now release the key
            inputManager.HandleKeyRelease(GeKeys.Space);
            inputManager.DoActions();

            Assert.Multiple(() =>
            {
                // Assert
                Assert.That(callbackValue, Is.False, "Callback value should be 'false' after key release");
                Assert.That(inputManager.IsActionActive("Jump"), Is.False, "Action state should be false after key release");
            });
        }

        [Test]
        public void RemovingAction_UnbindsStatesAndCallbacks()
        {
            // Arrange
            var inputManager = new InputManager();
            inputManager.AddAction(GeKeys.S, "MoveBackward");

            bool callbackInvoked = false;
            inputManager.BindAction("MoveBackward", _ => callbackInvoked = true);

            // Act
            inputManager.RemoveAction(GeKeys.S);

            // Try pressing the key for the removed action
            inputManager.HandleKeyPress(GeKeys.S);
            inputManager.DoActions();

            Assert.Multiple(() =>
            {
                // Assert
                Assert.That(callbackInvoked, Is.False, "Callback should not be invoked after action is removed");
                Assert.That(inputManager.IsActionActive("MoveBackward"), Is.False, "Removed action should not be in actionStates");
            });
        }

        [Test]
        public void TwoKeysOnOneAction_StayActiveWhileEitherIsHeld()
        {
            var inputManager = new InputManager();
            inputManager.AddAction(GeKeys.D, "MoveRight");
            inputManager.AddAction(GeKeys.Right, "MoveRight");

            inputManager.HandleKeyPress(GeKeys.Right);
            inputManager.HandleKeyPress(GeKeys.D);
            inputManager.HandleKeyRelease(GeKeys.D);

            Assert.That(inputManager.IsActionActive("MoveRight"), Is.True,
                "the arrow key is still held, so the action should still be active");

            inputManager.HandleKeyRelease(GeKeys.Right);

            Assert.That(inputManager.IsActionActive("MoveRight"), Is.False,
                "releasing the last key holding an action should end it");
        }

        [Test]
        public void ReleasingAKeyThatWasNeverPressed_DoesNotEndTheAction()
        {
            var inputManager = new InputManager();
            inputManager.AddAction(GeKeys.W, "MoveUp");
            inputManager.AddAction(GeKeys.Up, "MoveUp");

            inputManager.HandleKeyPress(GeKeys.W);
            inputManager.HandleKeyRelease(GeKeys.Up);

            Assert.That(inputManager.IsActionActive("MoveUp"), Is.True);
        }

        [Test]
        public void ResetForgetsHeldKeys()
        {
            var inputManager = new InputManager();
            inputManager.AddAction(GeKeys.N, "NextLevel");
            inputManager.HandleKeyPress(GeKeys.N);

            inputManager.Reset();
            inputManager.AddAction(GeKeys.N, "NextLevel");

            Assert.That(inputManager.IsActionActive("NextLevel"), Is.False,
                "a key held across a reset should not leave the rebuilt action active");
        }
    }
}
