using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameEngine.Core.Utils;

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    ReadCommentHandling = JsonCommentHandling.Skip, Converters = [typeof(LevelValueJsonConverter)])]
[JsonSerializable(typeof(LevelMetadata))]
[JsonSerializable(typeof(LevelFile))]
[JsonSerializable(typeof(EntityData))]
[JsonSerializable(typeof(ComponentData))]
[JsonSerializable(typeof(Dictionary<string, object>))]
internal partial class LevelMetadataJson : JsonSerializerContext;
