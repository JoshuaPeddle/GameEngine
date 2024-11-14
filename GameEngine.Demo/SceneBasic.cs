using GameEngine.Core;
using GameEngine.Core.Components;

namespace GameEngine.Demo
{
    public class SceneBasic : Scene
    {
        private readonly Assets assets = new("assets.txt");

        private Entity? playerEntity;
        private Entity testEntity;

        public override void Initialize(EntityManager entityManager, InputManager inputManager, ActionMapper actionMapper)
        {
            inputManager.AddAction(Keys.W, "Up");
            inputManager.AddAction(Keys.S, "Down");
            inputManager.AddAction(Keys.A, "Left");
            inputManager.AddAction(Keys.D, "Right");

            playerEntity = entityManager.CreateEntity("player");
            playerEntity.AddComponent(new CAnimation(assets.GetAnimation("JeepBack")));
            playerEntity.AddComponent<CTransform>();
            var playerInput = playerEntity.AddComponent<CInput>();
            actionMapper.MapActionToComponent<CInput>("Up", playerEntity, (input, isActive) => input.Up = isActive);
            actionMapper.MapActionToComponent<CInput>("Down", playerEntity, (input, isActive) => input.Down = isActive);
            actionMapper.MapActionToComponent<CInput>("Left", playerEntity, (input, isActive) => input.Left = isActive);
            actionMapper.MapActionToComponent<CInput>("Right", playerEntity, (input, isActive) => input.Right = isActive);

            testEntity = entityManager.CreateEntity("grenade");
            testEntity.AddComponent(new CAnimation(assets.GetAnimation("Grenade")));
            testEntity.AddComponent<CTransform>();
        }
    }
}
