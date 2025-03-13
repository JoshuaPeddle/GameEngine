using GameEngine.Core;
using GameEngine.Core.Components;
using GameEngine.Core.Systems;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GameEngine.Demo
{
    public class SceneMenu : Scene
    {
        public override int VirtualWidth => 800;
        public override int VirtualHeight => scenes.Count * 100 + 100;

        readonly List<Scene> scenes =
            [
                new SceneBasic(),
                new SceneSnake(),
                new SceneSideScroll(),
                new SceneJson(),
                new SceneEmpty(),
                new Scene2(),
            ];

        public override void Initialize(EntityManager entityManager, InputManager inputManager, AudioSystem audioPlayer, Action<Scene> ResetScene)
        {
            inputManager.AddAction(GeKeys.W, "Up");
            inputManager.AddAction(GeKeys.S, "Down");
            inputManager.AddAction(GeKeys.Space, "Go");

            var sceneNames = scenes.Select(scene => scene.GetType().Name).ToList();

            for (int i = 0; i < sceneNames.Count; i++)
            {
                var entity = entityManager.CreateEntity("scene" + i);
                entity.AddComponent(new CTransform(new Vec2(400, 100 + i * 100)));
                entity.AddComponent(new CText(sceneNames[i], 24));
            }

            var cursor = entityManager.CreateEntity("cursor");
            cursor.AddComponent(new CTransform(new Vec2(300, 100)));
            cursor.AddComponent(new CText(">", 24));

            inputManager.ActionMapper.MapActionToComponent<CTransform>("Up", cursor, (transform, isActive) =>
            {
                if (isActive)
                    transform.Position = new Vec2(transform.Position.X, transform.Position.Y - 100);
            }, oneShot: true);

            inputManager.ActionMapper.MapActionToComponent<CTransform>("Down", cursor, (transform, isActive) =>
            {
                if (isActive)
                    transform.Position = new Vec2(transform.Position.X, transform.Position.Y + 100);
            }, oneShot: true);

            inputManager.ActionMapper.MapActionToComponent<CTransform>("Go", cursor, (transform, isActive) =>
            {
                if (isActive)
                {
                    var selectedScene = scenes[(int)(transform.Position.Y - 100) / 100];
                    ResetScene(selectedScene);
                }
            }, oneShot: true);
        }
    }
}
