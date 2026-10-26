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

        public void Update(EntityManager entityManager, double deltaSeconds)
        {
            inputManager.DispatchPointerEvents();
            inputManager.DoActions();
        }

        public void KeyDown(GeKeys key) => KeyDown(key, 0);

        public void KeyDown(GeKeys key, long sourceId)
        {
            inputManager?.HandleKeyPress(key, sourceId);
        }

        public void KeyUp(GeKeys key) => KeyUp(key, 0);

        public void KeyUp(GeKeys key, long sourceId)
        {
            inputManager?.HandleKeyRelease(key, sourceId);
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

        public void PointerCancelled(PointerEvent pointerEvent)
        {
            inputManager.HandlePointerEvent(PointerEventType.Cancel, pointerEvent);
        }

        public void SetRealDimensions(int width, int height)
        {
            inputManager.RealResolution = new Vec2(width, height);
        }
    }
}
