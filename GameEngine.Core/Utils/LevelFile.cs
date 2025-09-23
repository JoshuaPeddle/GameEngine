using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using GameEngine.Core.Components;
using GameEngine.Core.Systems;

namespace GameEngine.Core.Utils
{
    // Data structures that mirror the JSON format
    public class ComponentData
    {
        public string Type { get; set; } = string.Empty;
        public JsonElement Data { get; set; }
    }

    public class EntityData
    {
        public string Tag { get; set; } = string.Empty;
        public List<ComponentData> Components { get; set; } = new();
    }

    public class LevelMetadata
    {
        public string Name { get; set; } = "Untitled Level";
        public string Version { get; set; } = "1.0";
        public string Description { get; set; } = "";
        public Dictionary<string, object> Properties { get; set; } = new();
    }

    // Main level file class - pure data container
    public class LevelFile
    {
        public LevelMetadata Metadata { get; set; } = new();
        public List<EntityData> Entities { get; set; } = new();

        // Static factory methods for loading
        public static LevelFile LoadFromFile(string filePath)
        {
            Stream fileStream;
            if (Assets._fileFetcher != null)
                fileStream = Assets._fileFetcher(filePath);
            else
                fileStream = File.OpenRead(filePath);
            using (fileStream)
            {
                using var reader = new StreamReader(fileStream);
                var json = reader.ReadToEnd();
                return LoadFromJson(json);
            }
        }

        public static LevelFile LoadFromJson(string json)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            };

            // Parse the JSON manually to handle the component data structure
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            
            var levelFile = new LevelFile();
            
            // Handle metadata if present
            if (root.TryGetProperty("metadata", out var metadataElement))
            {
                levelFile.Metadata = JsonSerializer.Deserialize<LevelMetadata>(metadataElement, options) ?? new LevelMetadata();
            }
            
            // Get entities array (either from "entities" property or root if it's just an array)
            JsonElement entitiesElement;
            if (root.TryGetProperty("entities", out entitiesElement))
            {
                // New format with metadata
            }
            else if (root.ValueKind == JsonValueKind.Array)
            {
                // Direct array format
                entitiesElement = root;
            }
            else
            {
                throw new InvalidDataException("Invalid level format - expected entities array");
            }
            
            // Parse entities
            foreach (var entityElement in entitiesElement.EnumerateArray())
            {
                var entityData = new EntityData
                {
                    Tag = entityElement.GetProperty("tag").GetString() ?? throw new InvalidDataException("Entity tag is required")
                };
                
                // Parse components
                foreach (var componentElement in entityElement.GetProperty("components").EnumerateArray())
                {
                    var componentData = new ComponentData
                    {
                        Type = componentElement.GetProperty("type").GetString() ?? throw new InvalidDataException("Component type is required"),
                        Data = componentElement.Clone() // Store the entire component JSON
                    };
                    entityData.Components.Add(componentData);
                }
                
                levelFile.Entities.Add(entityData);
            }
            
            return levelFile;
        }

        public void SaveToFile(string filePath)
        {
            var json = ToJson();
            File.WriteAllText(filePath, json);
        }

        public string ToJson()
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            return JsonSerializer.Serialize(this, options);
        }

        // Validation methods
        public void Validate()
        {
            foreach (var entity in Entities)
            {
                if (string.IsNullOrEmpty(entity.Tag))
                    throw new InvalidDataException("All entities must have a tag");

                foreach (var component in entity.Components)
                {
                    if (string.IsNullOrEmpty(component.Type))
                        throw new InvalidDataException($"All components in entity '{entity.Tag}' must have a type");
                }
            }
        }
    }

    // Handles the instantiation of entities from level data
    public class LevelLoader
    {
        private readonly ComponentFactory componentFactory;
        private readonly Dictionary<string, Action<Entity, InputManager, AudioSystem?>> specialEntityHandlers;

        public LevelLoader(ComponentFactory componentFactory)
        {
            this.componentFactory = componentFactory;
            specialEntityHandlers = new Dictionary<string, Action<Entity, InputManager, AudioSystem?>>
            {
                { "player", HandlePlayerEntity }
                // Add more special entity handlers here
            };
        }

        public void LoadLevel(LevelFile levelFile, EntityManager entityManager, InputManager? inputManager = null, AudioSystem? audioSystem = null)
        {
            levelFile.Validate();

            foreach (var entityData in levelFile.Entities)
            {
                var entity = CreateEntity(entityData, entityManager);
                
                // Handle special entity types
                if (specialEntityHandlers.TryGetValue(entityData.Tag, out var handler))
                {
                    handler(entity, inputManager!, audioSystem);
                }
            }
        }

        private Entity CreateEntity(EntityData entityData, EntityManager entityManager)
        {
            var entity = entityManager.CreateEntity(entityData.Tag);

            foreach (var componentData in entityData.Components)
            {
                try
                {
                    var component = componentFactory.CreateComponent(componentData.Type, componentData.Data);
                    entity.AddComponent(component);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Failed to create component '{componentData.Type}' for entity '{entityData.Tag}': {ex.Message}", ex);
                }
            }

            return entity;
        }

        // Special handlers for entities that need extra setup
        private void HandlePlayerEntity(Entity entity, InputManager inputManager, AudioSystem? audioSystem)
        {
            if (!entity.HasComponent<CInput>()) return;

            // Map input actions
            MapInputActions(entity, inputManager, audioSystem);
        }

        private void MapInputActions(Entity entity, InputManager inputManager, AudioSystem? audioSystem)
        {
            inputManager.ActionMapper.MapActionToComponent<CInput>("Up", entity, (input, isActive) => input.Up = isActive, true);
            inputManager.ActionMapper.MapActionToComponent<CInput>("Down", entity, (input, isActive) => input.Down = isActive, true);
            inputManager.ActionMapper.MapActionToComponent<CInput>("Left", entity, (input, isActive) => input.Left = isActive, true);
            inputManager.ActionMapper.MapActionToComponent<CInput>("Right", entity, (input, isActive) => input.Right = isActive, true);
            
            if (audioSystem != null)
            {
                inputManager.ActionMapper.MapActionToComponent<CInput>("PlaySound", entity, (input, isActive) =>
                {
                    if (isActive)
                    {
                        audioSystem.Play("Hit", SoundType.SoundEffect);
                    }
                }, true);
            }
        }

        // Allow adding custom entity handlers at runtime
        public void RegisterEntityHandler(string entityTag, Action<Entity, InputManager, AudioSystem?> handler)
        {
            specialEntityHandlers[entityTag] = handler;
        }
    }

    // Helper class for level management operations
    public static class LevelManager
    {
        public static LevelLoader CreateLoader(Assets assets)
        {
            var componentFactory = new ComponentFactory(assets);
            return new LevelLoader(componentFactory);
        }

        public static void LoadLevelIntoScene(string levelPath, EntityManager entityManager, InputManager inputManager, AudioSystem? audioSystem, Assets assets)
        {
            var levelFile = LevelFile.LoadFromFile(levelPath);
            var loader = CreateLoader(assets);
            loader.LoadLevel(levelFile, entityManager, inputManager, audioSystem);
        }
    }
}