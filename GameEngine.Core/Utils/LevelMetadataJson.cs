using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameEngine.Core.Utils;

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip)]
[JsonSerializable(typeof(LevelMetadata))]
internal partial class LevelMetadataJson : JsonSerializerContext;
