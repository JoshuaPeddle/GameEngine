using GameEngine.Core;
using GameEngine.Core.Components;
using GameEngine.Core.Systems;
using System;

namespace GameEngine.Demo
{
    public class SceneSideScroll : Scene
    {
        Assets? assets;
        private Assets Assets => assets
            ?? throw new InvalidOperationException("SceneSideScroll has not been initialized.");

        public override int VirtualWidth =>  4000;
        public override int VirtualHeight => 800;

        public override void Initialize(EntityManager entityManager, InputManager inputManager, AudioSystem? audioPlayer, Action<Scene> ResetScene)
        {
            assets = LoadAssets("assets.json");

            inputManager.AddAction(GeKeys.W, "Up");
            inputManager.BindGestureAction(PointerGesture.Up, "Up");
            inputManager.AddAction(GeKeys.S, "Down");
            inputManager.BindGestureAction(PointerGesture.Down, "Down");
            inputManager.AddAction(GeKeys.A, "Left");
            inputManager.BindGestureAction(PointerGesture.Left, "Left");
            inputManager.AddAction(GeKeys.D, "Right");
            inputManager.BindGestureAction(PointerGesture.Right, "Right");

            var player = entityManager.CreateEntity("player");
            player.AddComponent(new CTransform(new Vec2(40, 700)));
            player.AddComponent(new CAnimation(assets.GetAnimation("Mario")));
            player.AddComponent(new CBoundingBox(new Vec2(40, 40), false, false));
            player.AddComponent<CGravity>();
            player.AddComponent<CMovement>();
            var playerInput = player.AddComponent<CInput>();
            inputManager.ActionMapper.MapActionToComponent<CInput>("Up", player, (input, isActive) => input.Up = isActive);
            inputManager.ActionMapper.MapActionToComponent<CInput>("Down", player, (input, isActive) => input.Down = isActive);
            inputManager.ActionMapper.MapActionToComponent<CInput>("Left", player, (input, isActive) => input.Left = isActive);
            inputManager.ActionMapper.MapActionToComponent<CInput>("Right", player, (input, isActive) => input.Right = isActive);

            for (int i = 0; i < 1000; i++)
            {
                var floor = entityManager.CreateEntity("floor");
                floor.AddComponent(new CTransform(new Vec2(40 * i, 760)));
                floor.AddComponent(new CAnimation(assets.GetAnimation("BrickBlock")));
                floor.AddComponent(new CBoundingBox(new Vec2(40, 40), false, true));
            }

            var camera = entityManager.CreateEntity("camera");
            var cameraComponent = new CCamera();    
            cameraComponent.Zoom = 2.0f;
            cameraComponent.Position = new Vec2(0, 500);
            camera.AddComponent(cameraComponent);
        }

        public override void Update(EntityManager entityManager, SystemContainer systems, double deltaSeconds)
        {
            var physicsSystem = systems.Get<PhysicsSystem>();

            var camera = entityManager.GetEntityWithTag("camera");
            if (camera == null)
                return;
            var cameraTransform = camera.GetComponent<CCamera>();

            var player = entityManager.GetEntityWithTag("player");
            if (player == null)
                return;
            var playerTransform = player.GetComponent<CTransform>();

            cameraTransform.Position = new Vec2(playerTransform.Position.X, playerTransform.Position.Y);
        }
    }
}
