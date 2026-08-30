using System.Text;
using System.Text.Json;
using GameEngine.Core.Components;

namespace GameEngine.Core.Utils
{
    // Generates the published JSON Schema for the level format from the same component
    // vocabulary the loader validates against, so the two cannot disagree.
    public static class LevelSchema
    {
        public const string Url =
            "https://raw.githubusercontent.com/JoshuaPeddle/GameEngine/master/GameEngine.Core/levels/level.schema.json";

        public static string Generate()
        {
            using var buffer = new MemoryStream();
            using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartObject();
                writer.WriteString("$schema", "https://json-schema.org/draft/2020-12/schema");
                writer.WriteString("$id", Url);
                writer.WriteString("title", "GameEngine level");
                writer.WriteString("description",
                    "A list of entities and the components they are built from. "
                    + "The loader rejects any key not described here.");
                writer.WriteString("type", "object");
                WriteStringArray(writer, "required", ["entities"]);
                writer.WriteBoolean("additionalProperties", false);

                writer.WriteStartObject("properties");
                WriteTyped(writer, "$schema", "string", "Points editors and agents at this schema.");
                WriteRef(writer, "metadata", "#/$defs/metadata", "Optional descriptive information about the level.");
                writer.WriteStartObject("entities");
                writer.WriteString("type", "array");
                writer.WriteString("description", "Every entity the level creates, in spawn order.");
                writer.WriteStartObject("items");
                writer.WriteString("$ref", "#/$defs/entity");
                writer.WriteEndObject();
                writer.WriteEndObject();
                writer.WriteEndObject();

                writer.WriteStartObject("$defs");
                WriteVectorDef(writer);
                WriteMetadataDef(writer);
                WriteEntityDef(writer);
                WriteComponentDef(writer);

                foreach (var schema in ComponentSchemas.Known)
                    WriteComponentSchema(writer, schema);

                writer.WriteEndObject();
                writer.WriteEndObject();
            }

            return Encoding.UTF8.GetString(buffer.ToArray()) + "\n";
        }

        private static void WriteVectorDef(Utf8JsonWriter writer)
        {
            writer.WriteStartObject("vector");
            writer.WriteString("type", "object");
            writer.WriteString("description", "A two-dimensional value in virtual pixels.");
            WriteStringArray(writer, "required", ["x", "y"]);
            writer.WriteBoolean("additionalProperties", false);
            writer.WriteStartObject("properties");
            WriteTyped(writer, "x", "number", null);
            WriteTyped(writer, "y", "number", null);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        private static void WriteMetadataDef(Utf8JsonWriter writer)
        {
            writer.WriteStartObject("metadata");
            writer.WriteString("type", "object");
            writer.WriteBoolean("additionalProperties", false);
            writer.WriteStartObject("properties");
            WriteTyped(writer, "name", "string", "Display name of the level.");
            WriteTyped(writer, "version", "string", "Author-chosen version string; the engine does not interpret it.");
            WriteTyped(writer, "description", "string", "What the level is for.");
            writer.WriteStartObject("properties");
            writer.WriteString("type", "object");
            writer.WriteString("description",
                "Free-form values for the game to read. The engine passes them through untouched.");
            writer.WriteEndObject();
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        private static void WriteEntityDef(Utf8JsonWriter writer)
        {
            writer.WriteStartObject("entity");
            writer.WriteString("type", "object");
            WriteStringArray(writer, "required", ["tag", "components"]);
            writer.WriteBoolean("additionalProperties", false);
            writer.WriteStartObject("properties");
            WriteTyped(writer, "tag", "string",
                "Groups entities. Scenes look entities up by tag; it need not be unique.");
            writer.WriteStartObject("components");
            writer.WriteString("type", "array");
            writer.WriteStartObject("items");
            writer.WriteString("$ref", "#/$defs/component");
            writer.WriteEndObject();
            writer.WriteEndObject();
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        private static void WriteComponentDef(Utf8JsonWriter writer)
        {
            writer.WriteStartObject("component");
            writer.WriteStartArray("oneOf");
            foreach (var schema in ComponentSchemas.Known)
            {
                writer.WriteStartObject();
                writer.WriteString("$ref", $"#/$defs/{schema.Type}");
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        private static void WriteComponentSchema(Utf8JsonWriter writer, ComponentSchema schema)
        {
            writer.WriteStartObject(schema.Type);
            writer.WriteString("title", schema.Type);
            writer.WriteString("description", schema.Summary);
            writer.WriteString("type", "object");
            WriteStringArray(writer, "required",
                ["type", .. schema.Required.Select(p => p.Name)]);
            writer.WriteBoolean("additionalProperties", false);

            writer.WriteStartObject("properties");
            writer.WriteStartObject("type");
            writer.WriteString("const", schema.Type);
            writer.WriteEndObject();
            WriteTyped(writer, "$comment", "string", "Ignored by the loader.");

            foreach (var property in schema.Properties)
            {
                if (property.Kind == ComponentValueKind.Vector)
                    WriteRef(writer, property.Name, "#/$defs/vector", property.Description);
                else
                    WriteTyped(writer, property.Name, JsonTypeOf(property.Kind), property.Description);
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        private static string JsonTypeOf(ComponentValueKind kind) => kind switch
        {
            ComponentValueKind.Number => "number",
            ComponentValueKind.Integer => "integer",
            ComponentValueKind.Boolean => "boolean",
            ComponentValueKind.Text => "string",
            _ => "object"
        };

        private static void WriteTyped(Utf8JsonWriter writer, string name, string type, string? description)
        {
            writer.WriteStartObject(name);
            writer.WriteString("type", type);
            if (description != null)
                writer.WriteString("description", description);
            writer.WriteEndObject();
        }

        private static void WriteRef(Utf8JsonWriter writer, string name, string reference, string description)
        {
            writer.WriteStartObject(name);
            writer.WriteString("$ref", reference);
            writer.WriteString("description", description);
            writer.WriteEndObject();
        }

        private static void WriteStringArray(Utf8JsonWriter writer, string name, IEnumerable<string> values)
        {
            writer.WriteStartArray(name);
            foreach (var value in values)
                writer.WriteStringValue(value);
            writer.WriteEndArray();
        }
    }
}
