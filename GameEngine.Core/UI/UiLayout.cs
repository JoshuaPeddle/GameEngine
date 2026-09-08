using SkiaSharp;

namespace GameEngine.Core.UI;

public static class UiLayout
{
    internal static void Validate(SKRect bounds)
    {
        if (!float.IsFinite(bounds.Left) || !float.IsFinite(bounds.Top) || !float.IsFinite(bounds.Right) || !float.IsFinite(bounds.Bottom)
            || bounds.Width <= 0 || bounds.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(bounds));
    }

    public static SKRect Inset(SKRect bounds, float padding)
    {
        Validate(bounds);
        if (!float.IsFinite(padding) || padding < 0 || padding * 2 > Math.Min(bounds.Width, bounds.Height))
            throw new ArgumentOutOfRangeException(nameof(padding));
        return new SKRect(bounds.Left + padding, bounds.Top + padding, bounds.Right - padding, bounds.Bottom - padding);
    }

    public static IReadOnlyList<SKRect> Row(SKRect bounds, int count, float gap = 0) => Split(bounds, count, gap, true);
    public static IReadOnlyList<SKRect> Column(SKRect bounds, int count, float gap = 0) => Split(bounds, count, gap, false);

    private static SKRect[] Split(SKRect bounds, int count, float gap, bool horizontal)
    {
        Validate(bounds);
        if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
        if (!float.IsFinite(gap) || gap < 0) throw new ArgumentOutOfRangeException(nameof(gap));
        float length = horizontal ? bounds.Width : bounds.Height;
        float size = (length - gap * (count - 1)) / count;
        if (!float.IsFinite(size) || size <= 0) throw new ArgumentOutOfRangeException(nameof(bounds));
        var result = new SKRect[count];
        for (int i = 0; i < count; i++)
        {
            float offset = i * (size + gap);
            result[i] = horizontal
                ? new SKRect(bounds.Left + offset, bounds.Top, bounds.Left + offset + size, bounds.Bottom)
                : new SKRect(bounds.Left, bounds.Top + offset, bounds.Right, bounds.Top + offset + size);
        }
        return result;
    }
}
