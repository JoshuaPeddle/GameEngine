namespace GameEngine.Core.Systems
{
    public class InputSystem : ISystem
    {
        private readonly InputManager inputManager;

        public InputSystem(InputManager inputManager)
        {
            this.inputManager = inputManager;
        }

        public void Update(EntityManager entityManager, double deltaTime)
        {
            inputManager.DoActions();
        }

        public void KeyDown(GeKeys key)
        {
            inputManager?.HandleKeyPress(key);
        }

        public void KeyUp(GeKeys key)
        {
            inputManager?.HandleKeyRelease(key);
        }
    }
}
