using GameEngine.Core.Systems;

namespace GameEngine.Core
{
    public abstract class Scene
    {
        public abstract void Initialize(EntityManager entityManager, InputManager inputManager, AudioSystem audioPlayer, Action ResetScene);
        public virtual void Update(EntityManager entityManager, PhysicsSystem physicsSystem, double deltaTime) { }
    }
}
