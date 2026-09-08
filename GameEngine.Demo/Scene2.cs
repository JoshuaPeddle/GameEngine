using GameEngine.Core;
using GameEngine.Core.Components;
using GameEngine.Core.Systems;
using System;
using System.IO;

namespace GameEngine.Demo
{
    public class Scene2 : Scene
    {
        private Assets? assets;
        private Assets Assets => assets
            ?? throw new InvalidOperationException("Scene2 has not been initialized.");

        private Entity? playerEntity;
        private Entity? secondEntity;
        private Entity? grenadeEntity;

        public override void Initialize(EntityManager entityManager, InputManager inputManager, AudioSystem? audioPlayer, Action<Scene> ResetScene)
        {
            assets ??= new("assets.json", AssetSource);

            inputManager.AddAction(GeKeys.W, "Up");
            inputManager.BindGestureAction(PointerGesture.Up, "Up");
            inputManager.AddAction(GeKeys.S, "Down");
            inputManager.BindGestureAction(PointerGesture.Down, "Down");
            inputManager.AddAction(GeKeys.A, "Left");
            inputManager.BindGestureAction(PointerGesture.Left, "Left");
            inputManager.AddAction(GeKeys.D, "Right");
            inputManager.BindGestureAction(PointerGesture.Right, "Right");

            playerEntity = entityManager.CreateEntity("player");
            playerEntity.AddComponent(new CAnimation(assets.GetAnimation("JeepBack")));
            playerEntity.AddComponent(new CTransform(new Vec2(100, 100)));
            playerEntity.AddComponent(new CBoundingBox(new Vec2(50, 80), false, false));
            playerEntity.AddComponent<CMovement>();
            playerEntity.AddComponent<CInput>();
            inputManager.ActionMapper.MapActionToComponent<CInput>("Up", playerEntity, (input, isActive) => input.Up = isActive);
            inputManager.ActionMapper.MapActionToComponent<CInput>("Down", playerEntity, (input, isActive) => input.Down = isActive);
            inputManager.ActionMapper.MapActionToComponent<CInput>("Left", playerEntity, (input, isActive) => input.Left = isActive);
            inputManager.ActionMapper.MapActionToComponent<CInput>("Right", playerEntity, (input, isActive) => input.Right = isActive);


            secondEntity = entityManager.CreateEntity("second");
            secondEntity.AddComponent(new CAnimation(assets.GetAnimation("JeepBack")));
            secondEntity.AddComponent(new CTransform(new Vec2(500, 300)));
            secondEntity.AddComponent(new CBoundingBox(new Vec2(50, 80), true, true));


            grenadeEntity = entityManager.CreateEntity("grenade");
            grenadeEntity.AddComponent(new CAnimation(assets.GetAnimation("Grenade")));
            grenadeEntity.AddComponent(new CTransform(new Vec2(150, 300)));
            grenadeEntity.AddComponent(new CBoundingBox(new Vec2(40, 40), true, true));

            grenadeEntity = entityManager.CreateEntity("stoneBlock");
            grenadeEntity.AddComponent(new CAnimation(assets.GetAnimation("StoneBlock")));
            grenadeEntity.AddComponent(new CTransform(new Vec2(250, 300)));
            grenadeEntity.AddComponent(new CBoundingBox(new Vec2(60, 60), true, true));


            LoadLevel("levels/level1.txt", entityManager);
        }


        private void LoadLevel(string levelFilePath, EntityManager entityManager)
        {
            var filestream = Assets.Open(levelFilePath);

            var lines = 
                new StreamReader(filestream).ReadToEnd()
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                // Skip empty lines or comments
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                    continue;

                var parts = line.Split(' ');
                if (parts.Length != 6)
                {
                    throw new Exception($"Invalid line in level file: {line}");
                }

                string type = parts[0];
                string animationName = parts[1];
                double x = double.Parse(parts[2]);
                double y = double.Parse(parts[3]);
                bool blocksMovement = parts[4] == "1";
                bool blocksVision = parts[5] == "1";

                if (type == "Tile")
                {
                    // Create entity
                    var entity = entityManager.CreateEntity("tile");
                    var animation = Assets.GetAnimation(animationName);

                    // Get the frame dimensions from the animation
                    var sourceRect = animation.GetSourceRect(0);
                    double frameWidth = sourceRect.Width;
                    double frameHeight = sourceRect.Height;

                    // Set the position based on tile size
                    entity.AddComponent(new CTransform(new Vec2(x * frameWidth, y * frameHeight)));
                    entity.AddComponent(new CAnimation(animation));
                    entity.AddComponent(new CBoundingBox(new Vec2(frameWidth, frameHeight), blocksVision, blocksMovement));
                }
                else
                {
                    // Handle other entity types if necessary
                    throw new Exception($"Unknown entity type: {type}");
                }
            }
        }
    }
}
