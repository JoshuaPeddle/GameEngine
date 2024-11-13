namespace GameEngine.Core.Components
{
    public class CTransform : Component
    {
        public Vec2 Position = new Vec2(0.0f, 0.0f);
        public Vec2 PreviousPosition = new Vec2(0.0f, 0.0f);
        public Vec2 Scale = new Vec2(1.0f, 1.0f);
        public Vec2 Velocity = new Vec2(0.0f, 0.0f);
        public float AngleDeg = 0; // in degrees
    }
}
