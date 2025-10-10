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

        private const int SceneMenuX = 400;
        private const int FpsMenuX = 650;
        private const int MenuStartY = 100;
        private const int SceneItemYSpacing = 100;
        private const int FpsItemYSpacing = 50;
        private const int SceneFontSize = 24;
        private const int FpsFontSize = 20;

        private readonly List<(string, Lazy<Scene>)> scenes =
        [
            ("Basic", new Lazy<Scene>(() => new SceneBasic())),
            ("Snake", new Lazy<Scene>(() => new SceneSnake())),
            ("Side Scroll", new Lazy<Scene>(() => new SceneSideScroll())),
            ("Side Scroll 2", new Lazy<Scene>(() => new SceneSideScroll2())),
            ("Json", new Lazy<Scene>(() => new SceneJson())),
            ("Empty", new Lazy<Scene>(() => new SceneEmpty())),
            ("Scene2", new Lazy<Scene>(() => new Scene2())),
            ("Pong", new Lazy<Scene>(() => new ScenePong())),
            ("BrickBreaker", new Lazy<Scene>(() => new SceneBrickBreaker())),
            ("Pointer", new Lazy<Scene>(() => new ScenePointer())),
        ];

        private readonly int[] fpsOptions = [10, 60, 120, 144, 240, 500, 1000, 10000];
        private bool isOnFpsMenu = false;

        public override void Initialize(EntityManager entityManager, InputManager inputManager, AudioSystem audioPlayer, Action<Scene> ResetScene)
        {
            inputManager.AddAction(GeKeys.W, "Up");
            inputManager.AddAction(GeKeys.S, "Down");
            inputManager.AddAction(GeKeys.Space, "Go");
            inputManager.AddAction(GeKeys.D, "Right");
            inputManager.AddAction(GeKeys.A, "Left");

            // Create menu items
            CreateMenuItems(entityManager, scenes.Select(s => s.Item1), new Vec2(SceneMenuX, MenuStartY), SceneItemYSpacing, SceneFontSize, "scene");
            CreateMenuItems(entityManager, fpsOptions.Select(f => f.ToString()), new Vec2(FpsMenuX, MenuStartY), FpsItemYSpacing, FpsFontSize, "fps");

            // Create FPS menu title
            var fpsTitle = entityManager.CreateEntity("fpsTitle");
            fpsTitle.AddComponent(new CTransform(new Vec2(FpsMenuX, 50)));
            fpsTitle.AddComponent(new CText("FPS Control:", FpsFontSize));

            // Create cursors
            var sceneCursor = entityManager.CreateEntity("cursor");
            sceneCursor.AddComponent(new CTransform(new Vec2(SceneMenuX - 100, MenuStartY)));
            sceneCursor.AddComponent(new CText(">", SceneFontSize));

            var fpsCursor = entityManager.CreateEntity("fpsCursor");
            fpsCursor.AddComponent(new CTransform(new Vec2(FpsMenuX - 50, MenuStartY)));
            fpsCursor.AddComponent(new CText(">", FpsFontSize) { ShouldDraw = false });

            // Menu navigation
            inputManager.ActionMapper.MapActionToComponent<CTransform>("Right", sceneCursor, (transform, isActive)  =>
            {
                if (isActive && !isOnFpsMenu)
                {
                    isOnFpsMenu = true;
                    SetMenuVisibility(entityManager, true);
                }
            }, oneShot: true);

            inputManager.ActionMapper.MapActionToComponent<CTransform>("Left", sceneCursor, (transform, isActive) =>
            {
                if (isActive && isOnFpsMenu)
                {
                    isOnFpsMenu = false;
                    SetMenuVisibility(entityManager, false);
                }
            }, oneShot: true);

            // Scene menu actions
            inputManager.ActionMapper.MapActionToComponent<CTransform>("Up", sceneCursor, (transform, isActive) =>
            {
                if (isActive && !isOnFpsMenu)
                {
                    int currentIndex = (int)(transform.Position.Y - MenuStartY) / SceneItemYSpacing;
                    int newIndex = (currentIndex - 1 + scenes.Count) % scenes.Count;
                    transform.Position = new Vec2(transform.Position.X, MenuStartY + newIndex * SceneItemYSpacing);
                }
            }, oneShot: true);

            inputManager.ActionMapper.MapActionToComponent<CTransform>("Down", sceneCursor, (transform, isActive) =>
            {
                if (isActive && !isOnFpsMenu)
                {
                    int currentIndex = (int)(transform.Position.Y - MenuStartY) / SceneItemYSpacing;
                    int newIndex = (currentIndex + 1) % scenes.Count;
                    transform.Position = new Vec2(transform.Position.X, MenuStartY + newIndex * SceneItemYSpacing);
                }
            }, oneShot: true);

            inputManager.ActionMapper.MapActionToComponent<CTransform>("Go", sceneCursor, (transform, isActive) =>
            {
                if (isActive && !isOnFpsMenu)
                {
                    int selectedIndex = (int)(transform.Position.Y - MenuStartY) / SceneItemYSpacing;
                    ResetScene(scenes[selectedIndex].Item2.Value);
                }
            }, oneShot: true);

            // FPS menu actions
            inputManager.ActionMapper.MapActionToComponent<CTransform>("Up", fpsCursor, (transform, isActive) =>
            {
                if (isActive && isOnFpsMenu)
                {
                    int currentIndex = (int)(transform.Position.Y - MenuStartY) / FpsItemYSpacing;
                    int newIndex = (currentIndex - 1 + fpsOptions.Length) % fpsOptions.Length;
                    transform.Position = new Vec2(transform.Position.X, MenuStartY + newIndex * FpsItemYSpacing);
                }
            }, oneShot: true);

            inputManager.ActionMapper.MapActionToComponent<CTransform>("Down", fpsCursor, (transform, isActive) =>
            {
                if (isActive && isOnFpsMenu)
                {
                    int currentIndex = (int)(transform.Position.Y - MenuStartY) / FpsItemYSpacing;
                    int newIndex = (currentIndex + 1) % fpsOptions.Length;
                    transform.Position = new Vec2(transform.Position.X, MenuStartY + newIndex * FpsItemYSpacing);
                }
            }, oneShot: true);

            inputManager.ActionMapper.MapActionToComponent<CTransform>("Go", fpsCursor, (transform, isActive) =>
            {
                if (isActive && isOnFpsMenu)
                {
                    int selectedIndex = (int)(transform.Position.Y - MenuStartY) / FpsItemYSpacing;
                    Engine.TargetFrameRate = fpsOptions[selectedIndex];
                }
            }, oneShot: true);
        }

        private void CreateMenuItems(EntityManager entityManager, IEnumerable<string> items, Vec2 startPosition, float ySpacing, int fontSize, string tagPrefix)
        {
            int i = 0;
            foreach (var itemText in items)
            {
                var entity = entityManager.CreateEntity($"{tagPrefix}{i}");
                entity.AddComponent(new CTransform(new Vec2(startPosition.X, startPosition.Y + i * ySpacing)));
                entity.AddComponent(new CText(itemText, fontSize));
                i++;
            }
        }

        private void SetMenuVisibility(EntityManager entityManager, bool showFpsMenu)
        {
            var sceneCursorText = entityManager.GetEntityWithTag("cursor")?.GetComponent<CText>();
            var fpsCursorText = entityManager.GetEntityWithTag("fpsCursor")?.GetComponent<CText>();

            if (sceneCursorText != null)
                sceneCursorText.ShouldDraw = !showFpsMenu;
            if (fpsCursorText != null)
                fpsCursorText.ShouldDraw = showFpsMenu;
        }
    }
}
