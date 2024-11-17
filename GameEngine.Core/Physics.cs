namespace GameEngine.Core
{
    public class Physics
    {
        public static Vec2 GetOverlap(Entity entity1, Entity entity2)
        {
            return GetOverlapInternal(entity1, entity2, usePreviousPosition: false);
        }

        public static Vec2 GetPreviousOverlap(Entity entity1, Entity entity2)
        {
            return GetOverlapInternal(entity1, entity2, usePreviousPosition: true);
        }

        private static Vec2 GetOverlapInternal(Entity entity1, Entity entity2, bool usePreviousPosition)
        {
            var t1 = entity1.GetComponent<Components.CTransform>();
            var t2 = entity2.GetComponent<Components.CTransform>();

            var b1 = entity1.GetComponent<Components.CBoundingBox>();
            var b2 = entity2.GetComponent<Components.CBoundingBox>();

            var position1 = usePreviousPosition ? t1.PreviousPosition : t1.Position;
            var position2 = t2.Position;

            return CalculateOverlap(position1, b1.Size, position2, b2.Size);
        }

        private static Vec2 CalculateOverlap(Vec2 position1, Vec2 size1, Vec2 position2, Vec2 size2)
        {
            float left1 = position1.X;
            float right1 = position1.X + size1.X;
            float top1 = position1.Y;
            float bottom1 = position1.Y + size1.Y;

            float left2 = position2.X;
            float right2 = position2.X + size2.X;
            float top2 = position2.Y;
            float bottom2 = position2.Y + size2.Y;

            float overlapX = Math.Min(right1, right2) - Math.Max(left1, left2);
            float overlapY = Math.Min(bottom1, bottom2) - Math.Max(top1, top2);

            overlapX = Math.Max(0, overlapX);
            overlapY = Math.Max(0, overlapY);

            return new Vec2(overlapX, overlapY);
        }
    }
}

