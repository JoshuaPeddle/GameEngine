using System.Text.Json;

namespace GameEngine.Core
{
    public sealed record TextureEntry(string Name, string Path);

    public sealed record AnimationEntry(string Name, string Texture, int Frames, float FrameDelayMs);

    public sealed record SoundEntry(string Name, string Path);

    // The asset manifest names every texture, animation and sound a game can ask for.
    // JSON (assets.json) is the format; the positional line format (assets.txt) is still read
    // so existing projects keep working, and is reported as deprecated when something in it
    // is wrong.
    public sealed class AssetManifest
    {
        public const string DefaultFileName = "assets.json";
        public const string LegacyFileName = "assets.txt";

        private static readonly string[] SectionNames = ["$schema", "textures", "animations", "sounds"];
        private static readonly string[] AnimationKeys = ["texture", "frames", "frameDelayMs"];

        // Preserved so an editor round-trip does not strip the schema pointer.
        public string? Schema { get; set; }

        public List<TextureEntry> Textures { get; } = [];
        public List<AnimationEntry> Animations { get; } = [];
        public List<SoundEntry> Sounds { get; } = [];

        public static bool IsManifestPath(string path) =>
            path.EndsWith(DefaultFileName, StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(LegacyFileName, StringComparison.OrdinalIgnoreCase);

        public static AssetManifest Load(string path, IAssetSource source)
        {
            ArgumentNullException.ThrowIfNull(source);

            using var stream = source.Open(path);
            using var reader = new StreamReader(stream);
            var text = reader.ReadToEnd();

            return path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                ? ParseJson(text)
                : ParseLines(text);
        }

        public static AssetManifest ParseJson(string json)
        {
            var manifest = new AssetManifest();

            using var document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });

            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException(
                    $"An asset manifest must be a JSON object with {string.Join(", ", SectionNames[1..])} sections.");

            foreach (var section in root.EnumerateObject())
            {
                if (!SectionNames.Contains(section.Name, StringComparer.OrdinalIgnoreCase))
                    throw new InvalidDataException(Unknown("section", section.Name, SectionNames));
            }

            if (root.TryGetProperty("$schema", out var schema))
                manifest.Schema = schema.GetString();

            foreach (var (name, value) in Entries(root, "textures"))
                manifest.Textures.Add(new TextureEntry(name, RequireString(value, $"textures.{name}")));

            foreach (var (name, value) in Entries(root, "animations"))
                manifest.Animations.Add(ParseAnimation(name, value));

            foreach (var (name, value) in Entries(root, "sounds"))
                manifest.Sounds.Add(new SoundEntry(name, RequireString(value, $"sounds.{name}")));

            return manifest;
        }

        public static AssetManifest ParseLines(string text)
        {
            var manifest = new AssetManifest();

            foreach (var line in text.Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith('#'))
                    continue;

                var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                switch (parts[0])
                {
                    case "Texture":
                        Require(parts, 3, trimmed);
                        manifest.Textures.Add(new TextureEntry(parts[1], parts[2]));
                        break;
                    case "Font":
                        Require(parts, 3, trimmed);
                        break;
                    case "Animation":
                        Require(parts, 5, trimmed);
                        manifest.Animations.Add(new AnimationEntry(
                            parts[1], parts[2], int.Parse(parts[3]), float.Parse(parts[4])));
                        break;
                    case "Sound":
                        Require(parts, 3, trimmed);
                        manifest.Sounds.Add(new SoundEntry(parts[1], parts[2]));
                        break;
                    default:
                        throw new InvalidDataException(
                            $"Unknown asset directive '{parts[0]}' in: {trimmed}. "
                            + $"The line format is deprecated; {DefaultFileName} reports mistakes in full.");
                }
            }

            return manifest;
        }

        public string ToJson()
        {
            using var buffer = new MemoryStream();
            using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartObject();

                if (Schema != null)
                    writer.WriteString("$schema", Schema);

                writer.WriteStartObject("textures");
                foreach (var texture in Textures)
                    writer.WriteString(texture.Name, texture.Path);
                writer.WriteEndObject();

                writer.WriteStartObject("animations");
                foreach (var animation in Animations)
                {
                    writer.WriteStartObject(animation.Name);
                    writer.WriteString("texture", animation.Texture);
                    writer.WriteNumber("frames", animation.Frames);
                    writer.WriteNumber("frameDelayMs", animation.FrameDelayMs);
                    writer.WriteEndObject();
                }
                writer.WriteEndObject();

                writer.WriteStartObject("sounds");
                foreach (var sound in Sounds)
                    writer.WriteString(sound.Name, sound.Path);
                writer.WriteEndObject();

                writer.WriteEndObject();
            }

            return System.Text.Encoding.UTF8.GetString(buffer.ToArray()) + "\n";
        }

        private static AnimationEntry ParseAnimation(string name, JsonElement value)
        {
            var where = $"animations.{name}";

            if (value.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException(
                    $"{where} must be an object like "
                    + """{ "texture": "TexPlayer", "frames": 4, "frameDelayMs": 250 }.""");

            foreach (var property in value.EnumerateObject())
            {
                if (!AnimationKeys.Contains(property.Name, StringComparer.OrdinalIgnoreCase))
                    throw new InvalidDataException(Unknown($"{where} key", property.Name, AnimationKeys));
            }

            var texture = RequireString(RequireProperty(value, "texture", where), $"{where}.texture");
            var frames = RequireNumber(RequireProperty(value, "frames", where), $"{where}.frames");
            var delay = value.TryGetProperty("frameDelayMs", out var delayValue)
                ? RequireNumber(delayValue, $"{where}.frameDelayMs")
                : 0;

            if (frames < 1)
                throw new InvalidDataException($"{where}.frames must be at least 1.");

            return new AnimationEntry(name, texture, (int)frames, (float)delay);
        }

        private static IEnumerable<(string Name, JsonElement Value)> Entries(JsonElement root, string section)
        {
            if (!root.TryGetProperty(section, out var element))
                yield break;

            if (element.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException(
                    $"The '{section}' section must be an object mapping names to entries.");

            foreach (var property in element.EnumerateObject())
                yield return (property.Name, property.Value);
        }

        private static JsonElement RequireProperty(JsonElement element, string name, string where) =>
            element.TryGetProperty(name, out var value)
                ? value
                : throw new InvalidDataException($"{where} is missing required key '{name}'.");

        private static string RequireString(JsonElement element, string where) =>
            element.ValueKind == JsonValueKind.String
                ? element.GetString()!
                : throw new InvalidDataException($"{where} must be a string, but was {element.ValueKind}.");

        private static double RequireNumber(JsonElement element, string where) =>
            element.ValueKind == JsonValueKind.Number
                ? element.GetDouble()
                : throw new InvalidDataException($"{where} must be a number, but was {element.ValueKind}.");

        private static string Unknown(string what, string name, string[] known) =>
            $"Unknown asset manifest {what} '{name}'. Known: {string.Join(", ", known)}.";

        private static void Require(string[] parts, int count, string line)
        {
            if (parts.Length < count)
                throw new InvalidDataException(
                    $"Asset directive '{parts[0]}' needs {count - 1} arguments but got {parts.Length - 1} in: {line}");
        }
    }
}
