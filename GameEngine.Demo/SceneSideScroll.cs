using GameEngine.Core;
using GameEngine.Core.Components;
using GameEngine.Core.Systems;
using System;

namespace GameEngine.Demo
{
    public class SceneSideScroll : Scene
    {
        Assets assets = new Assets("assets.txt");

        public override int VirtualWidth =>  800;
        public override int VirtualHeight => 800;

        public override void Initialize(EntityManager entityManager, InputManager inputManager, AudioSystem audioPlayer, Action ResetScene)
        {
            CreateFloor(entityManager);
        }

        private void CreateFloor(EntityManager entityManager)
        {
            for (int i = 0; i < 20; i++)
            {
                var floor = entityManager.CreateEntity("floor");
                floor.AddComponent(new CTransform(new Vec2(40*i, 760)));
                floor.AddComponent(new CAnimation(assets.GetAnimation("BrickBlock")));
                floor.AddComponent(new CBoundingBox(new Vec2(40, 40), false, true));
            }
        }
    }
}
