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

        private static Vec2 CalculateOverlap(in Vec2 position1, in Vec2 size1, in Vec2 position2, in Vec2 size2)
        {
            double overlapX = Math.Max(0,
                Math.Min(position1.X + size1.X, position2.X + size2.X) -
                Math.Max(position1.X, position2.X));

            double overlapY = Math.Max(0,
                Math.Min(position1.Y + size1.Y, position2.Y + size2.Y) -
                Math.Max(position1.Y, position2.Y));

            return new Vec2(overlapX, overlapY);
        }
    }
}

