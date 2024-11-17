
namespace GameEngine.Core.Components
{
    public class CBoundingBox : Component
    {
        public Vec2 Size { get; set; }
        public float Width => Size.X;
        public float Height => Size.Y;
        public bool BlockVision { get; set; }
        public bool BlockMovement { get; set; }
        public CBoundingBox(Vec2 size, bool blockVision, bool blockMove)
        {
            Size = size;
            BlockVision = blockVision;
            BlockMovement = blockMove;
        }
    }
}
