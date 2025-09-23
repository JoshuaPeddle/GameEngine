using System.Text.Json;

namespace GameEngine.Core.Components
{
    public class ComponentFactory
    {
        private readonly Assets assets;
        private readonly Dictionary<string, Func<JsonElement, Component>> componentCreators;

        public ComponentFactory(Assets assets)
        {
            this.assets = assets;
            componentCreators = new Dictionary<string, Func<JsonElement, Component>>
                {
                    { "CTransform", CreateCTransform },
                    { "CAnimation", CreateCAnimation },
                    { "CBoundingBox", CreateCBoundingBox },
                    { "CInput", CreateCInput },
                    { "CMovement", CreateCMovement }
                    // Add other components here
                };
        }

        public Component CreateComponent(string typeName, JsonElement data)
        {
            if (componentCreators.TryGetValue(typeName, out var creator))
            {
                return creator(data);
            }
            throw new Exception($"Unknown component type: {typeName}");
        }

        private Component CreateCTransform(JsonElement data)
        {
            var position = data.GetProperty("position");
            double x = position.GetProperty("x").GetDouble();
            double y = position.GetProperty("y").GetDouble();
            return new CTransform(new Vec2(x, y));
        }

        private Component CreateCAnimation(JsonElement data)
        {
            string animationName = data.GetProperty("animationName").GetString()!;
            var animation = assets.GetAnimation(animationName);
            return new CAnimation(animation);
        }

        private Component CreateCBoundingBox(JsonElement data)
        {
            var size = data.GetProperty("size");
            double width = size.GetProperty("x").GetDouble();
            double height = size.GetProperty("y").GetDouble();
            bool blockVision = data.GetProperty("blockVision").GetBoolean();
            bool blockMovement = data.GetProperty("blockMovement").GetBoolean();
            return new CBoundingBox(new Vec2(width, height), blockVision, blockMovement);
        }

        private Component CreateCInput(JsonElement data)
        {
            return new CInput();
        }

        private Component CreateCMovement(JsonElement element)
        {
            var speed = element.GetProperty("speed").GetDouble();
            var maxSpeed = element.GetProperty("maxSpeed").GetDouble();
            return new CMovement(speed, maxSpeed);

        }
    }
}
