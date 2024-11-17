using GameEngine.Core;
using GameEngine.Core.Components;

namespace GameEngine.Demo
{
    public class SceneBasic : Scene
    {
        private readonly Assets assets = new("assets.txt");

        private Entity? playerEntity;
        private Entity? grenadeEntity;

        public override void Initialize(EntityManager entityManager, InputManager inputManager, ActionMapper actionMapper)
        {
            inputManager.AddAction(Keys.W, "Up");
            inputManager.AddAction(Keys.S, "Down");
            inputManager.AddAction(Keys.A, "Left");
            inputManager.AddAction(Keys.D, "Right");

            playerEntity = entityManager.CreateEntity("player");
            playerEntity.AddComponent(new CAnimation(assets.GetAnimation("JeepBack")));
            playerEntity.AddComponent(new CTransform(Vec2.Zero));
            var playerInput = playerEntity.AddComponent<CInput>();
            actionMapper.MapActionToComponent<CInput>("Up", playerEntity, (input, isActive) => input.Up = isActive);
            actionMapper.MapActionToComponent<CInput>("Down", playerEntity, (input, isActive) => input.Down = isActive);
            actionMapper.MapActionToComponent<CInput>("Left", playerEntity, (input, isActive) => input.Left = isActive);
            actionMapper.MapActionToComponent<CInput>("Right", playerEntity, (input, isActive) => input.Right = isActive);
            playerEntity.AddComponent(new CBoundingBox(new Vec2(50, 80), false, false));

            grenadeEntity = entityManager.CreateEntity("grenade");
            grenadeEntity.AddComponent(new CAnimation(assets.GetAnimation("Grenade")));
            grenadeEntity.AddComponent(new CTransform(Vec2.Zero));

            Test_AddBunchOfEntities(entityManager);
        }

        private void Test_AddBunchOfEntities(EntityManager entityManager)
        {
            for (int i = 0; i < 3000; i++)
            {
                var entity = entityManager.CreateEntity("entity" + i);
                entity.AddComponent(new CAnimation(assets.GetAnimation("Grenade")));

                var transform = new CTransform(Vec2.Zero);
                var maxWidth = 800;
                var maxHeight = 600;

                var random = new Random();
                entity.AddComponent(new CBoundingBox(new Vec2(20, 20), true, true));

                transform.Position = new Vec2(random.Next(100,maxWidth), random.Next(100,maxHeight));
                entity.AddComponent(transform);
            }
        }
    }
}
