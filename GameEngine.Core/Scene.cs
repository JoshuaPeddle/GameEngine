using SkiaSharp;

namespace GameEngine.Core
{
    public abstract class Scene
    {
        public abstract void Initialize(EntityManager entityManager, InputManager inputManager, ActionMapper actionMapper);

        public abstract void Simulate(float deltaMs);
    }
}
