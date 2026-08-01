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
                    { "CMovement", CreateCMovement },
                    { "CText", CreateCText },
                    { "CCamera", CreateCCamera },
                    { "CGravity", CreateCGravity }
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

            var transform = new CTransform(new Vec2(x, y));

            if (data.TryGetProperty("rotation", out var rotation))
                transform.Rotation = rotation.GetDouble();

            if (data.TryGetProperty("layer", out var layer))
                transform.Layer = layer.GetInt32();

            if (data.TryGetProperty("scale", out var scale))
                transform.Scale = new Vec2(
                    scale.GetProperty("x").GetDouble(),
                    scale.GetProperty("y").GetDouble());

            return transform;
        }

        private Component CreateCText(JsonElement data)
        {
            string text = data.TryGetProperty("text", out var value) ? value.GetString() ?? string.Empty : string.Empty;
            int size = data.TryGetProperty("size", out var sizeValue) ? sizeValue.GetInt32() : 24;

            return new CText(text, size);
        }

        private Component CreateCCamera(JsonElement data)
        {
            var camera = new CCamera();

            if (data.TryGetProperty("position", out var position))
                camera.Position = new Vec2(
                    position.GetProperty("x").GetDouble(),
                    position.GetProperty("y").GetDouble());

            if (data.TryGetProperty("zoom", out var zoom))
                camera.Zoom = (float)zoom.GetDouble();

            return camera;
        }

        private Component CreateCGravity(JsonElement data)
        {
            var gravity = new CGravity();

            if (data.TryGetProperty("acceleration", out var acceleration))
                gravity.Acceleration = acceleration.GetDouble();

            return gravity;
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
