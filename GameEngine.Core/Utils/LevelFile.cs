using System.Text.Json;
using GameEngine.Core.Components;
using GameEngine.Core.Systems;
using System.Text;

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

        // Preserved verbatim so an editor round-trip does not strip the schema an author
        // (or their tooling) pointed the file at.
        public string? Schema { get; set; }

        private static readonly string[] RootKeys = ["$schema", "metadata", "entities"];
        private static readonly string[] EntityKeys = ["tag", "components"];
        private static readonly string[] MetadataKeys = ["name", "version", "description", "properties"];

        private static void RejectUnknownKeys(JsonElement element, string[] known, string what)
        {
            if (element.ValueKind != JsonValueKind.Object)
                return;

            foreach (var property in element.EnumerateObject())
            {
                if (known.Contains(property.Name, StringComparer.OrdinalIgnoreCase))
                    continue;

                throw new InvalidDataException(
                    $"Unknown {what} key '{property.Name}'. Known keys: {string.Join(", ", known)}.");
            }
        }

        // Static factory methods for loading
        public static LevelFile LoadFromFile(string filePath, IAssetSource source)
        {
            ArgumentNullException.ThrowIfNull(source);

            using (Stream fileStream = source.Open(filePath))
            {
                using var reader = new StreamReader(fileStream);
                var json = reader.ReadToEnd();
                return LoadFromJson(json);
            }
        }

        public static LevelFile LoadFromJson(string json)
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            var levelFile = new LevelFile();

            if (root.ValueKind == JsonValueKind.Object)
            {
                RejectUnknownKeys(root, RootKeys, "level file");

                if (root.TryGetProperty("$schema", out var schemaElement))
                    levelFile.Schema = schemaElement.GetString();
            }

            if (root.TryGetProperty("metadata", out var metadataElement))
            {
                RejectUnknownKeys(metadataElement, MetadataKeys, "level metadata");
                levelFile.Metadata = JsonSerializer.Deserialize(metadataElement, LevelMetadataJson.Default.LevelMetadata) ?? new LevelMetadata();
            }

            JsonElement entitiesElement;
            if (root.TryGetProperty("entities", out entitiesElement))
            {
                // metadata format
            }
            else if (root.ValueKind == JsonValueKind.Array)
            {
                entitiesElement = root;
            }
            else
            {
                throw new InvalidDataException("Invalid level format - expected entities array");
            }

            foreach (var entityElement in entitiesElement.EnumerateArray())
            {
                RejectUnknownKeys(entityElement, EntityKeys, "entity");

                var entityData = new EntityData
                {
                    Tag = entityElement.GetProperty("tag").GetString() ?? throw new InvalidDataException("Entity tag is required")
                };

                foreach (var componentElement in entityElement.GetProperty("components").EnumerateArray())
                {
                    if (!componentElement.TryGetProperty("type", out var typeElement))
                        throw new InvalidDataException(
                            $"Component on entity '{entityData.Tag}' has no \"type\" property.");

                    var componentData = new ComponentData
                    {
                        Type = typeElement.GetString() ?? throw new InvalidDataException(
                            $"Component on entity '{entityData.Tag}' has a null \"type\"."),
                        Data = componentElement.Clone()
                    };
                    entityData.Components.Add(componentData);
                }

                levelFile.Entities.Add(entityData);
            }

            return levelFile;
        }

        // Serialising and validating before the destination is touched, then swapping a fully
        // written sibling into place, is what keeps a rejected document from costing an author
        // the file they already had.
        public void SaveToFile(string filePath)
        {
            Validate();
            var json = ToJson();

            var fullPath = Path.GetFullPath(filePath);
            var directory = Path.GetDirectoryName(fullPath)
                ?? throw new InvalidDataException($"'{filePath}' has no containing directory.");
            Directory.CreateDirectory(directory);

            var temporaryPath = Path.Combine(
                directory,
                $"{Path.GetFileName(fullPath)}.{Guid.NewGuid():n}.tmp");

            try
            {
                File.WriteAllText(temporaryPath, json);
                File.Move(temporaryPath, fullPath, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }

        public string ToJson()
        {
            // Custom writer so that each component is written as its raw JSON (without wrapping inside a "data" object)
            var writerOptions = new JsonWriterOptions { Indented = true };
            using var ms = new MemoryStream();
            using (var writer = new Utf8JsonWriter(ms, writerOptions))
            {
                writer.WriteStartObject();

                if (Schema != null)
                    writer.WriteString("$schema", Schema);

                // metadata
                writer.WritePropertyName("metadata");
                JsonSerializer.Serialize(writer, Metadata, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

                // entities
                writer.WritePropertyName("entities");
                writer.WriteStartArray();
                foreach (var entity in Entities)
                {
                    writer.WriteStartObject();
                    writer.WriteString("tag", entity.Tag);

                    writer.WritePropertyName("components");
                    writer.WriteStartArray();
                    foreach (var comp in entity.Components)
                    {
                        writer.WriteStartObject();
                        writer.WriteString("type", comp.Type);

                        if (comp.Data.ValueKind == JsonValueKind.Object)
                        {
                            foreach (var property in comp.Data.EnumerateObject())
                            {
                                if (property.NameEquals("type"))
                                    continue;
                                property.WriteTo(writer);
                            }
                        }

                        writer.WriteEndObject();
                    }
                    writer.WriteEndArray();

                    writer.WriteEndObject();
                }
                writer.WriteEndArray();

                writer.WriteEndObject();
            }
            return Encoding.UTF8.GetString(ms.ToArray());
        }

        // The same vocabulary the loader enforces, applied to every component in the document.
        // Nothing is skipped: a component this rejects would have failed to load.
        public void Validate()
        {
            for (var entityIndex = 0; entityIndex < Entities.Count; entityIndex++)
            {
                var entity = Entities[entityIndex];
                if (string.IsNullOrEmpty(entity.Tag))
                    throw new InvalidDataException($"Entity #{entityIndex + 1} has no tag. All entities must have a tag.");

                for (var componentIndex = 0; componentIndex < entity.Components.Count; componentIndex++)
                {
                    var component = entity.Components[componentIndex];
                    var location = Where(entity.Tag, entityIndex, componentIndex);

                    if (string.IsNullOrEmpty(component.Type))
                        throw new InvalidDataException($"{location} has no type.");

                    try
                    {
                        ComponentValidator.Validate(component.Type, component.Data);
                    }
                    catch (LevelSchemaException ex)
                    {
                        throw new LevelSchemaException($"{location}: {ex.Message}", ex);
                    }
                }
            }
        }

        public static string Where(string entityTag, int entityIndex, int componentIndex) =>
            $"Entity '{entityTag}' (#{entityIndex + 1}), component #{componentIndex + 1}";
    }

    // Handles the instantiation of entities from level data
    public class LevelLoader
    {
        private readonly ComponentFactory componentFactory;
        private readonly Dictionary<string, Action<Entity, InputManager, AudioSystem?>> specialEntityHandlers;

        public LevelLoader(ComponentFactory componentFactory)
        {
            this.componentFactory = componentFactory;
            specialEntityHandlers = new Dictionary<string, Action<Entity, InputManager, AudioSystem?>>();
        }

        // onEntityCreated receives each entity with the index of the document entry it came
        // from, which is how an editor keeps its own identities attached to what it previews.
        public void LoadLevel(
            LevelFile levelFile,
            EntityManager entityManager,
            InputManager? inputManager = null,
            AudioSystem? audioSystem = null,
            Action<int, Entity>? onEntityCreated = null)
        {
            levelFile.Validate();

            for (var index = 0; index < levelFile.Entities.Count; index++)
            {
                var entityData = levelFile.Entities[index];
                var entity = CreateEntity(entityData, entityManager);

                onEntityCreated?.Invoke(index, entity);

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
            var levelFile = LevelFile.LoadFromFile(levelPath, assets.Source);
            var loader = CreateLoader(assets);
            loader.LoadLevel(levelFile, entityManager, inputManager, audioSystem);
        }
    }
}