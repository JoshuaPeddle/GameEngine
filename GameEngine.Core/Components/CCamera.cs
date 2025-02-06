namespace GameEngine.Core.Components
{
    public class CCamera : Component
    {
        public Vec2 Position { get; set; } = new Vec2(0, 0);
        public float Zoom { get; set; } = 1.0f;

        // If you want to clamp, store the min/max bounds here
        public float MinX { get; set; } = float.NegativeInfinity;
        public float MaxX { get; set; } = float.PositiveInfinity;
        public float MinY { get; set; } = float.NegativeInfinity;
        public float MaxY { get; set; } = float.PositiveInfinity;
    }
}
