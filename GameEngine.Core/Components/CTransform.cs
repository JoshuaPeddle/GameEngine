namespace GameEngine.Core.Components
{
    public class CTransform : Component
    {
        public Vec2 Position = new(0.0f, 0.0f);
        public Vec2 PreviousPosition = new(0.0f, 0.0f);
        public Vec2 Scale = new(1.0f, 1.0f);
        public Vec2 Velocity = new(0.0f, 0.0f);
        public double Rotation = 0; // in degrees

        /// <summary>
        /// Draw order. Lower layers are painted first, so higher layers appear in front.
        /// Entities sharing a layer are drawn in spawn order. Negative values are allowed
        /// and put an entity behind the default layer.
        /// </summary>
        public int Layer = 0;

        public CTransform(Vec2 position)
        {
            Position = position;
            PreviousPosition = position;
        }

        public CTransform(Vec2 position, Vec2 velocity)
        {
            Position = position;
            PreviousPosition = position;
            Velocity = velocity;
        }
    }
}
