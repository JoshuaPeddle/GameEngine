
namespace GameEngine.Core.Components
{
    public class CBoundingBox : Component
    {
        public Vec2 Size { get; set; }
        public double Width => Size.X;
        public double Height => Size.Y;
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
