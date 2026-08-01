using GameEngine.Core.Systems;

namespace GameEngine.Core;

public readonly struct ViewportTransform
{
    public readonly double ScaleX;
    public readonly double ScaleY;
    public readonly double OffsetX;
    public readonly double OffsetY;
    public readonly double ScaledWidth;
    public readonly double ScaledHeight;

    public bool IsValid => ScaleX > 0 && ScaleY > 0;

    private ViewportTransform(double scaleX, double scaleY, double offsetX, double offsetY,
        double scaledWidth, double scaledHeight)
    {
        ScaleX = scaleX;
        ScaleY = scaleY;
        OffsetX = offsetX;
        OffsetY = offsetY;
        ScaledWidth = scaledWidth;
        ScaledHeight = scaledHeight;
    }

    public static ViewportTransform Create(Vec2 realResolution, Vec2 virtualResolution, ScalingStrategy strategy)
    {
        if (realResolution.X <= 0 || realResolution.Y <= 0
            || virtualResolution.X <= 0 || virtualResolution.Y <= 0)
            return default;

        double fitX = realResolution.X / virtualResolution.X;
        double fitY = realResolution.Y / virtualResolution.Y;

        double scaleX, scaleY;
        switch (strategy)
        {
            case ScalingStrategy.Stretch:
                scaleX = fitX;
                scaleY = fitY;
                break;
            case ScalingStrategy.Crop:
                scaleX = scaleY = Math.Max(fitX, fitY);
                break;
            default:
                scaleX = scaleY = Math.Min(fitX, fitY);
                break;
        }

        double scaledWidth = virtualResolution.X * scaleX;
        double scaledHeight = virtualResolution.Y * scaleY;

        return new ViewportTransform(
            scaleX, scaleY,
            (realResolution.X - scaledWidth) / 2,
            (realResolution.Y - scaledHeight) / 2,
            scaledWidth, scaledHeight);
    }

    public bool TryToVirtual(Vec2 realPosition, out Vec2 virtualPosition)
    {
        virtualPosition = default;
        if (!IsValid)
            return false;

        double x = realPosition.X - OffsetX;
        double y = realPosition.Y - OffsetY;

        if (x < 0 || x > ScaledWidth || y < 0 || y > ScaledHeight)
            return false;

        virtualPosition = new Vec2(x / ScaleX, y / ScaleY);
        return true;
    }

    public Vec2 ToReal(Vec2 virtualPosition) =>
        new((virtualPosition.X * ScaleX) + OffsetX, (virtualPosition.Y * ScaleY) + OffsetY);
}
