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

        public void OnKeyDown(object? sender, KeyEventArgs e)
        {
            Keys key = e.KeyCode;
            inputManager?.HandleKeyPress(key);
        }

        public void OnKeyUp(object? sender, KeyEventArgs e)
        {
            Keys key = e.KeyCode;
            inputManager?.HandleKeyRelease(key);
        }
    }
}
