using System.Text.Json;

namespace GameEngine.Core.Components
{
    // Reads one component object out of a level file, reporting every problem in terms
    // the author can act on: which property, what was expected, and what is allowed here.
    internal readonly struct ComponentReader(ComponentSchema schema, JsonElement data)
    {
        public void Validate()
        {
            if (data.ValueKind != JsonValueKind.Object)
                throw new LevelSchemaException(
                    $"Component '{schema.Type}' must be a JSON object, but was {Describe(data.ValueKind)}.");

            foreach (var property in data.EnumerateObject())
            {
                if (property.NameEquals("type") || property.NameEquals("$comment"))
                    continue;

                var known = schema.Find(property.Name)
                    ?? throw new LevelSchemaException(UnknownPropertyMessage(property.Name));

                RequireKind(known, property.Value);
            }

            foreach (var required in schema.Required)
            {
                if (!TryGet(required.Name, out _))
                    throw new LevelSchemaException(
                        $"Component '{schema.Type}' is missing required property '{required.Name}'. "
                        + $"{required.Description} "
                        + $"Required here: {Join(schema.Required.Select(p => p.Name))}.");
            }
        }

        public Vec2 Vector(string name) =>
            Vector(name, new Vec2(0, 0));

        public Vec2 Vector(string name, Vec2 fallback) =>
            TryGet(name, out var element)
                ? new Vec2(Coordinate(element, name, "x"), Coordinate(element, name, "y"))
                : fallback;

        public double Number(string name, double fallback = 0) =>
            TryGet(name, out var element) ? element.GetDouble() : fallback;

        public int Integer(string name, int fallback = 0) =>
            TryGet(name, out var element) ? element.GetInt32() : fallback;

        public bool Boolean(string name) =>
            TryGet(name, out var element) && element.GetBoolean();

        public string Text(string name, string fallback = "") =>
            TryGet(name, out var element) ? element.GetString() ?? fallback : fallback;

        // Validate() has already rejected anything unknown, so a case-insensitive lookup here
        // only ever matches the property the schema named.
        private bool TryGet(string name, out JsonElement value)
        {
            if (data.TryGetProperty(name, out value))
                return true;

            foreach (var property in data.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }

            value = default;
            return false;
        }

        private void RequireKind(ComponentProperty property, JsonElement value)
        {
            var ok = property.Kind switch
            {
                ComponentValueKind.Number => value.ValueKind == JsonValueKind.Number,
                ComponentValueKind.Integer => value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out _),
                ComponentValueKind.Boolean => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
                ComponentValueKind.Text => value.ValueKind == JsonValueKind.String,
                ComponentValueKind.Vector => IsVector(value),
                _ => true
            };

            if (!ok)
                throw new LevelSchemaException(
                    $"Component '{schema.Type}' property '{property.Name}' must be {Expectation(property.Kind)}, "
                    + $"but was {Describe(value.ValueKind)}. {property.Description}");
        }

        private static bool IsVector(JsonElement value) =>
            value.ValueKind == JsonValueKind.Object
            && value.TryGetProperty("x", out var x) && x.ValueKind == JsonValueKind.Number
            && value.TryGetProperty("y", out var y) && y.ValueKind == JsonValueKind.Number;

        private double Coordinate(JsonElement vector, string name, string axis)
        {
            if (!vector.TryGetProperty(axis, out var element) || element.ValueKind != JsonValueKind.Number)
                throw new LevelSchemaException(
                    $"Component '{schema.Type}' property '{name}' needs a numeric \"{axis}\"; "
                    + $"got {vector.GetRawText()}.");

            return element.GetDouble();
        }

        private string UnknownPropertyMessage(string name)
        {
            var message = $"Component '{schema.Type}' has no property '{name}'. "
                + $"Known properties: {Join(schema.PropertyNames)}.";

            var suggestion = Suggest.Closest(name, schema.PropertyNames);
            return suggestion == null ? message : $"{message} Did you mean '{suggestion}'?";
        }

        private static string Join(IEnumerable<string> names)
        {
            var listed = string.Join(", ", names);
            return listed.Length == 0 ? "(none)" : listed;
        }

        private static string Expectation(ComponentValueKind kind) => kind switch
        {
            ComponentValueKind.Number => "a number",
            ComponentValueKind.Integer => "a whole number",
            ComponentValueKind.Boolean => "true or false",
            ComponentValueKind.Text => "a string",
            ComponentValueKind.Vector => "an object like { \"x\": 0, \"y\": 0 }",
            _ => "a value"
        };

        private static string Describe(JsonValueKind kind) => kind switch
        {
            JsonValueKind.Object => "an object",
            JsonValueKind.Array => "an array",
            JsonValueKind.String => "a string",
            JsonValueKind.Number => "a number",
            JsonValueKind.True or JsonValueKind.False => "a boolean",
            JsonValueKind.Null => "null",
            _ => "undefined"
        };
    }

    internal static class Suggest
    {
        // Only offers a name that is a plausible typo of what was written; a wrong guess is
        // worse than none when the reader is an agent that will act on it.
        public static string? Closest(string written, IEnumerable<string> candidates)
        {
            string? best = null;
            var bestDistance = int.MaxValue;

            foreach (var candidate in candidates)
            {
                var distance = Distance(written, candidate);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }

            var budget = Math.Max(1, written.Length / 3);
            return bestDistance <= budget ? best : null;
        }

        private static int Distance(string a, string b)
        {
            var previous = new int[b.Length + 1];
            var current = new int[b.Length + 1];

            for (var j = 0; j <= b.Length; j++)
                previous[j] = j;

            for (var i = 1; i <= a.Length; i++)
            {
                current[0] = i;
                for (var j = 1; j <= b.Length; j++)
                {
                    var substitution = char.ToLowerInvariant(a[i - 1]) == char.ToLowerInvariant(b[j - 1]) ? 0 : 1;
                    current[j] = Math.Min(
                        Math.Min(current[j - 1] + 1, previous[j] + 1),
                        previous[j - 1] + substitution);
                }

                (previous, current) = (current, previous);
            }

            return previous[b.Length];
        }
    }
}
