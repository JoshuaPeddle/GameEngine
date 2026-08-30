namespace GameEngine.Core.Components
{
    public enum ComponentValueKind
    {
        Number,
        Integer,
        Boolean,
        Text,
        Vector
    }

    public sealed record ComponentProperty(
        string Name,
        ComponentValueKind Kind,
        bool Required,
        string Description);

    public sealed record ComponentSchema(
        string Type,
        string Summary,
        IReadOnlyList<ComponentProperty> Properties)
    {
        public IEnumerable<string> PropertyNames => Properties.Select(p => p.Name);

        public IEnumerable<ComponentProperty> Required => Properties.Where(p => p.Required);

        public ComponentProperty? Find(string name) =>
            Properties.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    // The one place the level vocabulary is written down. The loader validates against it,
    // the published JSON Schema is generated from it, and a test asserts the two agree —
    // so the engine, the format and its documentation cannot drift apart.
    public static class ComponentSchemas
    {
        private static ComponentProperty Required(string name, ComponentValueKind kind, string description) =>
            new(name, kind, true, description);

        private static ComponentProperty Optional(string name, ComponentValueKind kind, string description) =>
            new(name, kind, false, description);

        private static readonly ComponentSchema[] All =
        [
            new("CTransform",
                "Where the entity is, how fast it is moving, and how it is drawn.",
                [
                    Required("position", ComponentValueKind.Vector, "Position in virtual pixels."),
                    Optional("velocity", ComponentValueKind.Vector, "Velocity in virtual pixels per second."),
                    Optional("scale", ComponentValueKind.Vector, "Draw scale; defaults to 1 on both axes."),
                    Optional("rotation", ComponentValueKind.Number, "Rotation in degrees; defaults to 0."),
                    Optional("layer", ComponentValueKind.Integer, "Draw order within the scene; defaults to 0.")
                ]),
            new("CBoundingBox",
                "An axis-aligned collision box. blockMovement means solid.",
                [
                    Required("size", ComponentValueKind.Vector, "Box size in virtual pixels."),
                    Required("blockVision", ComponentValueKind.Boolean, "Whether the box blocks line of sight."),
                    Required("blockMovement", ComponentValueKind.Boolean, "Whether the box is solid and pushes others out.")
                ]),
            new("CAnimation",
                "Plays an animation declared in the asset manifest.",
                [
                    Required("animationName", ComponentValueKind.Text, "Name of an Animation entry in the asset manifest.")
                ]),
            new("CInput",
                "Directional input flags a scene maps keys onto.",
                []),
            new("CMovement",
                "Acceleration and speed cap, in virtual pixels per second.",
                [
                    Required("speed", ComponentValueKind.Number, "Acceleration applied while an input direction is held."),
                    Required("maxSpeed", ComponentValueKind.Number, "Maximum speed along either axis.")
                ]),
            new("CText",
                "Text drawn at the entity's position.",
                [
                    Optional("text", ComponentValueKind.Text, "The string to draw; defaults to empty."),
                    Optional("size", ComponentValueKind.Integer, "Font size in virtual pixels; defaults to 24.")
                ]),
            new("CCamera",
                "View position and zoom. Tag the entity 'camera' to make it the active one.",
                [
                    Optional("position", ComponentValueKind.Vector, "Camera centre in virtual pixels; defaults to the origin."),
                    Optional("zoom", ComponentValueKind.Number, "Zoom factor; defaults to 1.")
                ]),
            new("CGravity",
                "Downward acceleration, in virtual pixels per second squared.",
                [
                    Optional("acceleration", ComponentValueKind.Number, "Downward acceleration; defaults to 200.")
                ])
        ];

        private static readonly Dictionary<string, ComponentSchema> ByType =
            All.ToDictionary(s => s.Type, StringComparer.Ordinal);

        public static IReadOnlyList<ComponentSchema> Known => All;

        public static IEnumerable<string> KnownTypes => All.Select(s => s.Type);

        public static ComponentSchema? Find(string type) => ByType.GetValueOrDefault(type);
    }
}
