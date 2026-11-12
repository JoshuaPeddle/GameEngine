using GameEngine.Core.Systems;
using System.Diagnostics.CodeAnalysis;

namespace GameEngine.Core
{
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)]
    public abstract class Scene
    {
        private bool? hasCustomUpdate;
        public virtual FrameActivity FrameActivity =>
            (hasCustomUpdate ??= GetType().GetMethod(nameof(Update), [typeof(EntityManager), typeof(SystemContainer), typeof(double)])?.DeclaringType != typeof(Scene))
                ? FrameActivity.Active : FrameActivity.Idle;

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
