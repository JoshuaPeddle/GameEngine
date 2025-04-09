using static GameEngine.Core.Pointer;

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

        public void PointerPressed(PointerEvent pointerEvent)
        {
            inputManager?.HandlePointerEvent(PointerEventType.Press, pointerEvent);
        }

        public void PointerMoved(PointerEvent pointerEvent)
        {
            inputManager?.HandlePointerEvent(PointerEventType.Move, pointerEvent);
        }

        public void PointerReleased(PointerEvent pointerEvent)
        {
            inputManager?.HandlePointerEvent(PointerEventType.Release, pointerEvent);
        }

        public void SetRealDimensions(int width, int height)
        {
            inputManager.RealResolution = new Vec2(width, height);
        }
    }
}
