using System.Text.Json;

namespace GameEngine.Core.Components
{
    // The vocabulary check the loader performs, without the assets a component needs to be
    // instantiated. An editor can therefore reject a document before writing it using exactly
    // the rules that will be applied when the engine reads it back.
    public static class ComponentValidator
    {
        public static void Validate(string type, JsonElement data)
        {
            var schema = ComponentSchemas.Find(type)
                ?? throw new LevelSchemaException(UnknownTypeMessage(type));

            new ComponentReader(schema, data).Validate();
        }

        internal static string UnknownTypeMessage(string typeName)
        {
            var message = $"Unknown component type '{typeName}'. "
                + $"Known types: {string.Join(", ", ComponentSchemas.KnownTypes)}.";

            var suggestion = Suggest.Closest(typeName, ComponentSchemas.KnownTypes);
            return suggestion == null ? message : $"{message} Did you mean '{suggestion}'?";
        }
    }
}
