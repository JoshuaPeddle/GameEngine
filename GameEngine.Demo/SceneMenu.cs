using GameEngine.Core;
using GameEngine.Core.Components;
using GameEngine.Core.Systems;
using System;
using System.Collections.Generic;

namespace GameEngine.Demo
{
    public class SceneMenu : Scene
    {
        public override int VirtualWidth => 800;
        public override int VirtualHeight => scenes.Count * 100 + 100;

        readonly List<Lazy<Scene>> scenes =
        [
            new Lazy<Scene>(() => new SceneBasic()),
            new Lazy<Scene>(() => new SceneSnake()),
            new Lazy<Scene>(() => new SceneSideScroll()),
            new Lazy<Scene>(() => new SceneJson()),
            new Lazy<Scene>(() => new SceneEmpty()),
            new Lazy<Scene>(() => new Scene2()),
            new Lazy<Scene>(() => new ScenePong()),
            new Lazy<Scene>(() => new ScenePointer()),
        ];

        readonly List<string> sceneNames = ["SceneBasic", "SceneSnake", "SceneSideScroll", "SceneJson", "SceneEmpty", "Scene2", "ScenePong", "ScenePointer"];

        public override void Initialize(EntityManager entityManager, InputManager inputManager, AudioSystem audioPlayer, Action<Scene> ResetScene)
        {
            inputManager.AddAction(GeKeys.W, "Up");
            inputManager.AddAction(GeKeys.S, "Down");
            inputManager.AddAction(GeKeys.Space, "Go");

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
                {
                    int currentIndex = (int)(transform.Position.Y - 100) / 100;
                    int newIndex = currentIndex - 1;
                    if (newIndex < 0)
                        newIndex = scenes.Count - 1; // Wrap to bottom
                    transform.Position = new Vec2(transform.Position.X, 100 + newIndex * 100);
                }
            }, oneShot: true);

            inputManager.ActionMapper.MapActionToComponent<CTransform>("Down", cursor, (transform, isActive) =>
            {
                if (isActive)
                {
                    int currentIndex = (int)(transform.Position.Y - 100) / 100;
                    int newIndex = (currentIndex + 1) % scenes.Count; // Wrap to top
                    transform.Position = new Vec2(transform.Position.X, 100 + newIndex * 100);
                }
            }, oneShot: true);

            inputManager.ActionMapper.MapActionToComponent<CTransform>("Go", cursor, (transform, isActive) =>
            {
                if (isActive)
                {
                    var selectedScene = scenes[(int)(transform.Position.Y - 100) / 100];
                    ResetScene(selectedScene.Value);
                }
            }, oneShot: true);
        }
    }
}
