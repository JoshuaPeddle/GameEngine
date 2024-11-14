namespace GameEngine.Core.Systems
{
    public class InputSystem : ISystem
    {
        private readonly InputManager inputManager;
        private readonly ActionMapper actionMapper;

        public InputSystem(InputManager inputManager, ActionMapper actionMapper)
        {
            this.inputManager = inputManager;
            this.actionMapper = actionMapper;
        }

        public void Update(EntityManager entityManager, float deltaTime)
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
