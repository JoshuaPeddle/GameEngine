namespace GameEngine.Core;

public enum PointerGesture { Tap, Up, Down, Left, Right }

public static class SwipeGesture
{
    public const double DefaultThreshold = 20.0;

    public static PointerGesture Recognize(Vec2 start, Vec2 end, double threshold = DefaultThreshold) =>
        Classify(start, end, threshold) switch
        {
            GeKeys.W => PointerGesture.Up,
            GeKeys.S => PointerGesture.Down,
            GeKeys.A => PointerGesture.Left,
            GeKeys.D => PointerGesture.Right,
            _ => PointerGesture.Tap
        };

    public static GeKeys Classify(Vec2 start, Vec2 end, double threshold = DefaultThreshold)
    {
        double deltaX = end.X - start.X;
        double deltaY = end.Y - start.Y;

        if (Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY)) < threshold)
            return GeKeys.Space;

        if (Math.Abs(deltaX) > Math.Abs(deltaY))
            return deltaX > 0 ? GeKeys.D : GeKeys.A;

        return deltaY > 0 ? GeKeys.S : GeKeys.W;
    }
}
