using GameEngine.Core.Systems;

namespace GameEngine.Core
{
    public abstract class Scene
    {
        public virtual int VirtualWidth => 1600;
        public virtual int VirtualHeight => 1600;

        public abstract void Initialize(EntityManager entityManager, InputManager inputManager, AudioSystem audioPlayer, Action<Scene?> ResetScene);
        public virtual void Update(EntityManager entityManager, PhysicsSystem physicsSystem, double deltaTime) { }
    }
}
