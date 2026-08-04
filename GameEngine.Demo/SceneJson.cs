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
            assets ??= new("assets.txt", AssetSource);
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
            inputManager.AddAction(GeKeys.S, "Down");
            inputManager.AddAction(GeKeys.A, "Left");
            inputManager.AddAction(GeKeys.D, "Right");
            inputManager.AddAction(GeKeys.Space, "PlaySound");
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

    // Example of a more advanced scene that might have multiple levels
    public class MultiLevelScene : Scene
    {
        private Assets? assets;
        private AudioSystem? audioSystem;
        private LevelLoader? levelLoader;
        private int currentLevelIndex = 0;
        private readonly string[] levelPaths = { "levels/level1.json", "levels/level2.json", "levels/level3.json" };

        public override void Initialize(EntityManager entityManager, InputManager inputManager, AudioSystem? audioPlayer, Action<Scene> ResetScene)
        {
            assets ??= new("assets.txt", AssetSource);
            audioSystem = audioPlayer;
            levelLoader = LevelManager.CreateLoader(assets);
            levelLoader.RegisterEntityHandler("player", SceneJson.WirePlayerInput);

            SetupInputActions(inputManager);
            LoadCurrentLevel(entityManager, inputManager);
        }

        private void SetupInputActions(InputManager inputManager)
        {
            inputManager.AddAction(GeKeys.W, "Up");
            inputManager.AddAction(GeKeys.S, "Down");
            inputManager.AddAction(GeKeys.A, "Left");
            inputManager.AddAction(GeKeys.D, "Right");
            inputManager.AddAction(GeKeys.Space, "PlaySound");
            inputManager.AddAction(GeKeys.N, "NextLevel");
            inputManager.AddAction(GeKeys.P, "PrevLevel");
        }

        private void LoadCurrentLevel(EntityManager entityManager, InputManager inputManager)
        {
            if (currentLevelIndex >= 0 && currentLevelIndex < levelPaths.Length)
            {
                // Clear existing entities
                entityManager.Clear();

                // Load new level
                var levelFile = LevelFile.LoadFromFile(levelPaths[currentLevelIndex], AssetSource);
                levelLoader!.LoadLevel(levelFile, entityManager, inputManager, audioSystem);

                Console.WriteLine($"Loaded level {currentLevelIndex + 1}: {levelFile.Metadata.Name}");
            }
        }

        public void NextLevel(EntityManager entityManager, InputManager inputManager)
        {
            if (currentLevelIndex < levelPaths.Length - 1)
            {
                currentLevelIndex++;
                LoadCurrentLevel(entityManager, inputManager);
            }
        }

        public void PreviousLevel(EntityManager entityManager, InputManager inputManager)
        {
            if (currentLevelIndex > 0)
            {
                currentLevelIndex--;
                LoadCurrentLevel(entityManager, inputManager);
            }
        }
    }
}