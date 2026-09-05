using System.Text.Json;

namespace GameEngine.Core.Components
{
    public class ComponentFactory
    {
        private readonly Assets? assets;
        private readonly Dictionary<string, Func<ComponentReader, Component>> componentCreators;

        public ComponentFactory(Assets assets)
        {
            this.assets = assets;
            componentCreators = new Dictionary<string, Func<ComponentReader, Component>>(StringComparer.Ordinal)
                {
                    { "CTransform", CreateCTransform },
                    { "CAnimation", CreateCAnimation },
                    { "CBoundingBox", CreateCBoundingBox },
                    { "CInput", CreateCInput },
                    { "CMovement", CreateCMovement },
                    { "CText", CreateCText },
                    { "CCamera", CreateCCamera },
                    { "CGravity", CreateCGravity }
                };
        }

        public Component CreateComponent(string typeName, JsonElement data)
        {
            var schema = ComponentSchemas.Find(typeName);
            if (schema == null || !componentCreators.TryGetValue(typeName, out var creator))
                throw new LevelSchemaException(ComponentValidator.UnknownTypeMessage(typeName));

            var reader = new ComponentReader(schema, data);
            reader.Validate();
            return creator(reader);
        }

        private static Component CreateCTransform(ComponentReader data)
        {
            var transform = new CTransform(data.Vector("position"))
            {
                Velocity = data.Vector("velocity", new Vec2(0, 0)),
                Scale = data.Vector("scale", new Vec2(1, 1)),
                Rotation = data.Number("rotation"),
                Layer = data.Integer("layer")
            };

            return transform;
        }

        private static Component CreateCText(ComponentReader data) =>
            new CText(data.Text("text"), data.Integer("size", 24));

        private static Component CreateCCamera(ComponentReader data) =>
            new CCamera
            {
                Position = data.Vector("position", new Vec2(0, 0)),
                Zoom = (float)data.Number("zoom", 1.0)
            };

        private static Component CreateCGravity(ComponentReader data) =>
            new CGravity { Acceleration = data.Number("acceleration", 200) };

        private Component CreateCAnimation(ComponentReader data)
        {
            var animationName = data.Text("animationName");
            if (assets == null)
                throw new LevelSchemaException(
                    $"Component 'CAnimation' needs animation '{animationName}', but this loader was built without assets.");

            return new CAnimation(assets.GetAnimation(animationName));
        }

        private static Component CreateCBoundingBox(ComponentReader data) =>
            new CBoundingBox(
                data.Vector("size"),
                data.Boolean("blockVision"),
                data.Boolean("blockMovement"));

        private static Component CreateCInput(ComponentReader data) => new CInput();

        private static Component CreateCMovement(ComponentReader data) =>
            new CMovement(data.Number("speed"), data.Number("maxSpeed"));
    }
}
