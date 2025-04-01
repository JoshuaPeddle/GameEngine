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

        readonly List<(string,Lazy<Scene>)> scenes =
        [
            ("Basic", new Lazy<Scene>(() => new SceneBasic())),
            ("Snake", new Lazy<Scene>(() => new SceneSnake())),
            ("Side Scroll", new Lazy<Scene>(() => new SceneSideScroll())),
            ("Json", new Lazy<Scene>(() => new SceneJson())),
            ("Empty", new Lazy<Scene>(() => new SceneEmpty())),
            ("Scene2", new Lazy<Scene>(() => new Scene2())),
            ("Pong", new Lazy<Scene>(() => new ScenePong())),
            ("BrickBreaker", new Lazy<Scene>(() => new SceneBrickBreaker())),
            ("Pointer", new Lazy<Scene>(() => new ScenePointer())),
        ];

        public override void Initialize(EntityManager entityManager, InputManager inputManager, AudioSystem audioPlayer, Action<Scene> ResetScene)
        {
            inputManager.AddAction(GeKeys.W, "Up");
            inputManager.AddAction(GeKeys.S, "Down");
            inputManager.AddAction(GeKeys.Space, "Go");

            for (int i = 0; i < scenes.Count; i++)
            {
                var entity = entityManager.CreateEntity("scene" + i);
                entity.AddComponent(new CTransform(new Vec2(400, 100 + i * 100)));
                entity.AddComponent(new CText(scenes[i].Item1, 24));
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
                    ResetScene(selectedScene.Item2.Value);
                }
            }, oneShot: true);
        }
    }
}
