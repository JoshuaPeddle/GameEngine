using SkiaSharp;

namespace GameEngine.Core
{
    // Where an entity is drawn, in one place, so the renderer, the culler and the editor's
    // picker all agree.
    //
    // CTransform.Position is the top-left corner of the entity's bounding box when it has one,
    // and the centre of its sprite when it does not. A sprite is drawn centred on that point,
    // and rotation and scale are applied about that centre — a negative scale mirrors on its
    // axis. CBoundingBox.Size is collision geometry in virtual pixels and is deliberately not
    // touched by CTransform.Scale: making a sprite twice as big does not silently make the
    // thing it collides with twice as big.
    public static class SpriteGeometry
    {
        public static Vec2 Center(Vec2 position, Vec2? boundingBoxSize) =>
            boundingBoxSize is { } size
                ? new Vec2(position.X + size.X / 2, position.Y + size.Y / 2)
                : position;

        public static Vec2 DrawSize(Vec2 frameSize, Vec2 scale) =>
            new(frameSize.X * Math.Abs(scale.X), frameSize.Y * Math.Abs(scale.Y));

        public static SKRect BoxBounds(Vec2 position, Vec2 size) =>
            new((float)position.X, (float)position.Y,
                (float)(position.X + size.X), (float)(position.Y + size.Y));

        // Conservative: a rotated sprite is bounded by the circle through its corners, so the
        // box grows to the half-diagonal on both axes rather than being recomputed per angle.
        public static SKRect SpriteBounds(Vec2 center, Vec2 frameSize, Vec2 scale, double rotationDegrees)
        {
            var drawn = DrawSize(frameSize, scale);
            double halfWidth = drawn.X / 2;
            double halfHeight = drawn.Y / 2;

            if (rotationDegrees % 360 != 0)
            {
                double radius = Math.Sqrt(halfWidth * halfWidth + halfHeight * halfHeight);
                halfWidth = radius;
                halfHeight = radius;
            }

            return new SKRect(
                (float)(center.X - halfWidth),
                (float)(center.Y - halfHeight),
                (float)(center.X + halfWidth),
                (float)(center.Y + halfHeight));
        }

        public static bool BoxContains(Vec2 worldPoint, Vec2 position, Vec2 size) =>
            worldPoint.X >= position.X && worldPoint.X <= position.X + size.X
            && worldPoint.Y >= position.Y && worldPoint.Y <= position.Y + size.Y;

        // Rotates and unscales the point into the sprite's own frame, so the test matches the
        // quad that was actually drawn rather than its axis-aligned bounds.
        public static bool SpriteContains(
            Vec2 worldPoint, Vec2 center, Vec2 frameSize, Vec2 scale, double rotationDegrees)
        {
            double scaleX = Math.Abs(scale.X);
            double scaleY = Math.Abs(scale.Y);
            if (scaleX <= 0 || scaleY <= 0 || frameSize.X <= 0 || frameSize.Y <= 0)
                return false;

            double dx = worldPoint.X - center.X;
            double dy = worldPoint.Y - center.Y;

            if (rotationDegrees % 360 != 0)
            {
                double radians = -rotationDegrees * Math.PI / 180.0;
                double cos = Math.Cos(radians);
                double sin = Math.Sin(radians);
                (dx, dy) = (dx * cos - dy * sin, dx * sin + dy * cos);
            }

            return Math.Abs(dx) <= frameSize.X * scaleX / 2
                && Math.Abs(dy) <= frameSize.Y * scaleY / 2;
        }

        public static SKRect Union(SKRect? first, SKRect second)
        {
            if (first is not { } existing)
                return second;

            return new SKRect(
                Math.Min(existing.Left, second.Left),
                Math.Min(existing.Top, second.Top),
                Math.Max(existing.Right, second.Right),
                Math.Max(existing.Bottom, second.Bottom));
        }
    }
}
