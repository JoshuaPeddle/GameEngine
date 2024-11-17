using GameEngine.Core;
using GameEngine.Core.Components;

namespace GameEngine.Demo
{
    public class Scene2 : Scene
    {
        private readonly Assets assets = new("assets.txt");

        private Entity? playerEntity;
        private Entity? secondEntity;
        private Entity? grenadeEntity;

        public override void Initialize(EntityManager entityManager, InputManager inputManager, ActionMapper actionMapper)
        {
            inputManager.AddAction(Keys.W, "Up");
            inputManager.AddAction(Keys.S, "Down");
            inputManager.AddAction(Keys.A, "Left");
            inputManager.AddAction(Keys.D, "Right");

            playerEntity = entityManager.CreateEntity("player");
            playerEntity.AddComponent(new CAnimation(assets.GetAnimation("JeepBack")));
            playerEntity.AddComponent<CTransform>();
            playerEntity.AddComponent(new CBoundingBox(new Vec2(50, 80), false, false));
            playerEntity.AddComponent<CInput>();
            actionMapper.MapActionToComponent<CInput>("Up", playerEntity, (input, isActive) => input.Up = isActive);
            actionMapper.MapActionToComponent<CInput>("Down", playerEntity, (input, isActive) => input.Down = isActive);
            actionMapper.MapActionToComponent<CInput>("Left", playerEntity, (input, isActive) => input.Left = isActive);
            actionMapper.MapActionToComponent<CInput>("Right", playerEntity, (input, isActive) => input.Right = isActive);


            secondEntity = entityManager.CreateEntity("second");
            secondEntity.AddComponent(new CAnimation(assets.GetAnimation("JeepBack")));
            secondEntity.AddComponent(new CTransform() { Position = new Vec2(500, 300) });
            secondEntity.AddComponent(new CBoundingBox(new Vec2(50, 80), true, true));


            grenadeEntity = entityManager.CreateEntity("grenade");
            grenadeEntity.AddComponent(new CAnimation(assets.GetAnimation("Grenade")));
            grenadeEntity.AddComponent(new CTransform() { Position = new Vec2(150, 300) });
            grenadeEntity.AddComponent(new CBoundingBox(new Vec2(40, 40), true, true));
        }
    }
}
