using GameEngine.Core.Components;

namespace GameEngine.Core
{
    public class Physics
    {
        public static Vec2 GetOverlap(Entity entity1, Entity entity2)
        {
            var t1 = entity1.GetComponent<Components.CTransform>();
            var t2 = entity2.GetComponent<Components.CTransform>();

            var b1 = entity1.GetComponent<Components.CBoundingBox>();
            var b2 = entity2.GetComponent<Components.CBoundingBox>();

            var position1 = t1.Position;
            var position2 = t2.Position;

            return CalculateOverlap(position1, b1.Size, position2, b2.Size);
        }

        public static Vec2 GetOverlap(CTransform cTransform1, CTransform cTransform2, CBoundingBox cBoundingBox1, CBoundingBox cBoundingBox2)
        {
            var position1 = cTransform1.Position;
            var position2 = cTransform2.Position;

            return CalculateOverlap(position1, cBoundingBox1.Size, position2, cBoundingBox2.Size);
        }

        public static bool IsColliding(Entity entity1, Entity entity2)
        {
            var overlap = GetOverlap(entity1, entity2);
            return overlap.X > 0 && overlap.Y > 0;
        }

        private static Vec2 CalculateOverlap(Vec2 position1, Vec2 size1, Vec2 position2, Vec2 size2)
        {
            double left1 = position1.X;
            double right1 = position1.X + size1.X;
            double top1 = position1.Y;
            double bottom1 = position1.Y + size1.Y;

            double left2 = position2.X;
            double right2 = position2.X + size2.X;
            double top2 = position2.Y;
            double bottom2 = position2.Y + size2.Y;

            double overlapX = Math.Min(right1, right2) - Math.Max(left1, left2);
            double overlapY = Math.Min(bottom1, bottom2) - Math.Max(top1, top2);

            overlapX = Math.Max(0, overlapX);
            overlapY = Math.Max(0, overlapY);

            return new Vec2(overlapX, overlapY);
        }
    }
}

