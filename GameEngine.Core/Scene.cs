using GameEngine.Core.Systems;

namespace GameEngine.Core
{
    public abstract class Scene
    {
        public Engine? Engine { get; internal set; }

        protected IAssetSource AssetSource => Engine?.AssetSource
            ?? throw new InvalidOperationException(
                $"{GetType().Name} has no engine yet; assets are only available from Initialize onwards.");

        public virtual int VirtualWidth => 1600;
        public virtual int VirtualHeight => 1600;

        public abstract void Initialize(EntityManager entityManager, InputManager inputManager, AudioSystem? audioPlayer, Action<Scene?> ResetScene);
        public virtual void Update(EntityManager entityManager, SystemContainer systems, double deltaSeconds) { }
    }
}
