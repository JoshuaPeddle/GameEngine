using GameEngine.Core;
using GameEngine.Core.Components;
using GameEngine.Core.Systems;
using GameEngine.Core.Utils;
using System;

namespace GameEngine.Demo
{
    public class SceneJson : Scene
    {
        private Assets? assets;
        private AudioSystem? audioSystem;
        private LevelLoader? levelLoader;

        public override void Initialize(EntityManager entityManager, InputManager inputManager, AudioSystem? audioPlayer, Action<Scene> ResetScene)
        {
            assets ??= new("assets.json", AssetSource);
            audioSystem = audioPlayer;

            // Create the level loader
            levelLoader = LevelManager.CreateLoader(assets);
            levelLoader.RegisterEntityHandler("player", WirePlayerInput);

            // Set up input actions
            SetupInputActions(inputManager);

            // Load the level
            LoadLevel("levels/level1.json", entityManager, inputManager);

            // Play background music
            audioSystem?.Play("Level1", SoundType.BGM);
        }

        internal static void WirePlayerInput(Entity entity, InputManager inputManager, AudioSystem? audioSystem)
        {
            if (!entity.HasComponent<CInput>()) return;

            inputManager.ActionMapper.MapActionToComponent<CInput>("Up", entity, (input, isActive) => input.Up = isActive, true);
            inputManager.ActionMapper.MapActionToComponent<CInput>("Down", entity, (input, isActive) => input.Down = isActive, true);
            inputManager.ActionMapper.MapActionToComponent<CInput>("Left", entity, (input, isActive) => input.Left = isActive, true);
            inputManager.ActionMapper.MapActionToComponent<CInput>("Right", entity, (input, isActive) => input.Right = isActive, true);

            if (audioSystem != null)
            {
                inputManager.ActionMapper.MapActionToComponent<CInput>("PlaySound", entity, (input, isActive) =>
                {
                    if (isActive)
                        audioSystem.Play("Hit", SoundType.SoundEffect);
                }, true);
            }
        }

        private void SetupInputActions(InputManager inputManager)
        {
            inputManager.AddAction(GeKeys.W, "Up");
            inputManager.BindGestureAction(PointerGesture.Up, "Up");
            inputManager.AddAction(GeKeys.S, "Down");
            inputManager.BindGestureAction(PointerGesture.Down, "Down");
            inputManager.AddAction(GeKeys.A, "Left");
            inputManager.BindGestureAction(PointerGesture.Left, "Left");
            inputManager.AddAction(GeKeys.D, "Right");
            inputManager.BindGestureAction(PointerGesture.Right, "Right");
            inputManager.AddAction(GeKeys.Space, "PlaySound");
            inputManager.BindGestureAction(PointerGesture.Tap, "PlaySound");
        }

        private void LoadLevel(string levelFilePath, EntityManager entityManager, InputManager inputManager)
        {
            try
            {
                var levelFile = LevelFile.LoadFromFile(levelFilePath, AssetSource);
                levelLoader!.LoadLevel(levelFile, entityManager, inputManager, audioSystem);

                Console.WriteLine($"Loaded level: {levelFile.Metadata.Name}");
                if (!string.IsNullOrEmpty(levelFile.Metadata.Description))
                {
                    Console.WriteLine($"Description: {levelFile.Metadata.Description}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load level '{levelFilePath}': {ex.Message}");
                throw;
            }
        }

        // Alternative method for loading from a LevelFile object directly
        public void LoadFromLevelFile(LevelFile levelFile, EntityManager entityManager, InputManager inputManager)
        {
            levelLoader?.LoadLevel(levelFile, entityManager, inputManager, audioSystem);
        }

        // Method to register custom entity handlers if needed
        public void RegisterEntityHandler(string entityTag, Action<Entity, InputManager, AudioSystem?> handler)
        {
            levelLoader?.RegisterEntityHandler(entityTag, handler);
        }
    }

    // A second JSON scene, showing level navigation: N advances, P goes back.
    public class MultiLevelScene : Scene
    {
        private static readonly string[] LevelPaths =
        [
            "levels/level1.json",
            "levels/level2.json",
            "levels/level3.json"
        ];

        private Assets? assets;
        private AudioSystem? audioSystem;
        private InputManager? inputManager;
        private LevelLoader? levelLoader;
        private int currentLevelIndex;
        private int pendingLevelStep;

        // 1-based, so it reads like the level number the player sees.
        public int CurrentLevel => currentLevelIndex + 1;

        public static int LevelCount => LevelPaths.Length;

        public override void Initialize(EntityManager entityManager, InputManager inputManager, AudioSystem? audioPlayer, Action<Scene> ResetScene)
        {
            assets ??= new("assets.json", AssetSource);
            audioSystem = audioPlayer;
            this.inputManager = inputManager;

            SetupInputActions(inputManager);
            LoadCurrentLevel(entityManager, inputManager);
        }

        private void SetupInputActions(InputManager inputManager)
        {
            levelLoader = LevelManager.CreateLoader(assets!);
            levelLoader.RegisterEntityHandler("player", SceneJson.WirePlayerInput);

            inputManager.AddAction(GeKeys.W, "Up");
            inputManager.BindGestureAction(PointerGesture.Up, "Up");
            inputManager.AddAction(GeKeys.Up, "Up");
            inputManager.AddAction(GeKeys.S, "Down");
            inputManager.BindGestureAction(PointerGesture.Down, "Down");
            inputManager.AddAction(GeKeys.Down, "Down");
            inputManager.AddAction(GeKeys.A, "Left");
            inputManager.BindGestureAction(PointerGesture.Left, "Left");
            inputManager.AddAction(GeKeys.Left, "Left");
            inputManager.AddAction(GeKeys.D, "Right");
            inputManager.BindGestureAction(PointerGesture.Right, "Right");
            inputManager.AddAction(GeKeys.Right, "Right");
            inputManager.AddAction(GeKeys.Space, "PlaySound");
            inputManager.BindGestureAction(PointerGesture.Tap, "PlaySound");
            inputManager.AddAction(GeKeys.N, "NextLevel");
            inputManager.AddAction(GeKeys.P, "PrevLevel");

            inputManager.BindAction("NextLevel", OnPress(() => pendingLevelStep = 1));
            inputManager.BindAction("PrevLevel", OnPress(() => pendingLevelStep = -1));
        }

        private static Action<bool> OnPress(Action onPressed)
        {
            var wasActive = false;

            return isActive =>
            {
                if (isActive && !wasActive)
                    onPressed();

                wasActive = isActive;
            };
        }

        // Deferred to Update because loading a level clears the EntityManager, and input
        // dispatch runs while the systems are still walking it.
        public override void Update(EntityManager entityManager, SystemContainer systems, double deltaSeconds)
        {
            if (pendingLevelStep == 0 || inputManager == null)
                return;

            var step = pendingLevelStep;
            pendingLevelStep = 0;

            var target = currentLevelIndex + step;
            if (target < 0 || target >= LevelPaths.Length)
                return;

            currentLevelIndex = target;

            // Rebinding from scratch keeps the old level's entity bindings from piling up.
            inputManager.Reset();
            SetupInputActions(inputManager);
            LoadCurrentLevel(entityManager, inputManager);
        }

        private void LoadCurrentLevel(EntityManager entityManager, InputManager inputManager)
        {
            entityManager.Clear();

            var levelFile = LevelFile.LoadFromFile(LevelPaths[currentLevelIndex], AssetSource);
            levelLoader!.LoadLevel(levelFile, entityManager, inputManager, audioSystem);
        }
    }
}
