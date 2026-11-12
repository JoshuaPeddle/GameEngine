using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;

namespace GameEngine.UI.Avalonia;

public enum SurfaceKind { Panel, Raised, Primary, Quiet }
public enum SurfaceState { Normal, Hover, Pressed, Disabled, Focused }

public sealed class GameSurface : Control
{
    public static readonly StyledProperty<SurfaceKind> KindProperty = AvaloniaProperty.Register<GameSurface, SurfaceKind>(nameof(Kind));
    public static readonly StyledProperty<SurfaceState> StateProperty = AvaloniaProperty.Register<GameSurface, SurfaceState>(nameof(State));
    public static readonly StyledProperty<bool> OrnamentsProperty = AvaloniaProperty.Register<GameSurface, bool>(nameof(Ornaments));
    public SurfaceKind Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }
    public SurfaceState State { get => GetValue(StateProperty); set => SetValue(StateProperty, value); }
    public bool Ornaments { get => GetValue(OrnamentsProperty); set => SetValue(OrnamentsProperty, value); }

    static GameSurface() => AffectsRender<GameSurface>(KindProperty, StateProperty, OrnamentsProperty, GameUi.ThemeProperty);
    public GameSurface() => IsHitTestVisible = false;
    public override void Render(DrawingContext context) => context.Custom(new SurfaceDraw(new Rect(Bounds.Size),
        new SurfaceKey(GameUi.GetTheme(this), Kind, State, Ornaments)));

    private sealed class SurfaceDraw(Rect bounds, SurfaceKey key) : ICustomDrawOperation
    {
        public Rect Bounds => bounds;
        public bool HitTest(Point p) => bounds.Contains(p);
        public bool Equals(ICustomDrawOperation? other) => other is SurfaceDraw draw && draw.Bounds == bounds && draw.key == key;
        private readonly SurfaceKey key = key;
        public void Dispose() { }
        public void Render(ImmediateDrawingContext context)
        {
            var feature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (feature is null) return;
            using var lease = feature.Lease();
            SurfaceCache.Draw(lease.SkCanvas, key, new SKRect(0, 0, (float)bounds.Width, (float)bounds.Height));
        }
    }
}

public readonly record struct SurfaceKey(GameTheme Theme, SurfaceKind Kind = SurfaceKind.Panel,
    SurfaceState State = SurfaceState.Normal, bool Ornaments = false);

public static class SurfaceCache
{
    private const int Pixels = 240;
    private const float Scale = 3;
    private static readonly Lock Gate = new();
    private static readonly Dictionary<SurfaceKey, SKImage> Images = new();
    private static readonly Queue<SurfaceKey> Order = new();
    public static long GeneratedCount { get; private set; }
    public static int Count { get { lock (Gate) return Images.Count; } }
    public const int Capacity = 64;

    public static void Draw(SKCanvas canvas, SurfaceKey key, SKRect bounds)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        lock (Gate)
        {
            if (!Images.TryGetValue(key, out var image))
            {
                if (Images.Count == Capacity) { var oldest = Order.Dequeue(); Images[oldest].Dispose(); Images.Remove(oldest); }
                image = Create(key); Images.Add(key, image); Order.Enqueue(key); GeneratedCount++;
            }
            canvas.DrawImageNinePatch(image, new SKRectI(78, 78, 162, 162), bounds, SKFilterMode.Linear);
        }
    }

    public static void Clear()
    {
        lock (Gate) { foreach (var image in Images.Values) image.Dispose(); Images.Clear(); Order.Clear(); }
    }

    private static SKColor C(Color c) => new(c.R, c.G, c.B, c.A);
    private static SKColor Mix(SKColor a, SKColor b, float t) => new(
        (byte)(a.Red + (b.Red - a.Red) * t), (byte)(a.Green + (b.Green - a.Green) * t),
        (byte)(a.Blue + (b.Blue - a.Blue) * t), (byte)(a.Alpha + (b.Alpha - a.Alpha) * t));

    private static SKImage Create(SurfaceKey key)
    {
        using var surface = SKSurface.Create(new SKImageInfo(Pixels, Pixels));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent); canvas.Scale(Scale);
        var theme = key.Theme; var palette = theme.Palette;
        bool primary = key.Kind == SurfaceKind.Primary && key.State != SurfaceState.Disabled;
        var fill = C(primary ? palette.Accent : key.Kind == SurfaceKind.Raised ? palette.Raised : palette.Surface);
        var rim = C(primary ? palette.Accent : palette.Rim);
        if (key.State == SurfaceState.Hover) fill = Mix(fill, C(palette.Accent), .16f);
        if (key.State == SurfaceState.Pressed) fill = Mix(fill, SKColors.Black, .13f);
        if (key.State == SurfaceState.Disabled) fill = Mix(C(palette.Surface), C(palette.Background), .3f);
        float radius = (float)Math.Clamp(key.Kind == SurfaceKind.Panel ? theme.Radii.Panel : theme.Radii.Control, 2, 23);
        var rect = new SKRect(2, 2, 78, 77);
        using var paint = new SKPaint { IsAntialias = true };
        paint.Color = C(palette.Background).WithAlpha(100);
        canvas.DrawRoundRect(new SKRect(2, 4, 78, 80), radius, radius, paint);
        using var gradient = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(0, 80),
            [Mix(fill, SKColors.White, primary ? .12f : .035f), fill, Mix(fill, SKColors.Black, .08f)], [0, .45f, 1], SKShaderTileMode.Clamp);
        paint.Shader = gradient; canvas.DrawRoundRect(rect, radius, radius, paint); paint.Shader = null;
        canvas.Save();
        using (var clip = new SKRoundRect(rect, radius)) canvas.ClipRoundRect(clip, SKClipOperation.Intersect, true);
        var random = new Random(71);
        for (int i = 0; i < 750; i++)
        {
            paint.Color = i % 2 == 0 ? SKColors.White.WithAlpha(8) : SKColors.Black.WithAlpha(7);
            canvas.DrawCircle((float)random.NextDouble() * 80, (float)random.NextDouble() * 80, .22f, paint);
        }
        using var light = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(0, 24),
            [C(palette.Accent).WithAlpha((byte)(Math.Clamp(theme.Glow, 0, 1) * 100)), SKColors.Transparent], null, SKShaderTileMode.Clamp);
        paint.Shader = light; canvas.DrawRect(rect, paint); paint.Shader = null;
        canvas.Restore();
        paint.Style = SKPaintStyle.Stroke; paint.StrokeWidth = (float)theme.Stroke;
        paint.Color = key.State == SurfaceState.Focused ? C(palette.Accent) : rim.WithAlpha(primary ? (byte)220 : (byte)140);
        canvas.DrawRoundRect(rect, radius, radius, paint);
        paint.Color = SKColors.White.WithAlpha(primary ? (byte)100 : (byte)25); paint.StrokeWidth = .6f;
        canvas.DrawRoundRect(new SKRect(3.5f, 3.5f, 76.5f, 75.5f), Math.Max(1, radius - 1.5f), Math.Max(1, radius - 1.5f), paint);
        if (key.Ornaments)
        {
            paint.Color = C(palette.Legendary); paint.StrokeWidth = 1;
            foreach (float x in new[] { 11f, 69f })
            {
                canvas.DrawLine(x - 3, 10, x + 3, 10, paint);
                canvas.DrawLine(x, 7, x, 13, paint);
                canvas.DrawLine(x - 3, 69, x + 3, 69, paint);
            }
        }
        return surface.Snapshot();
    }
}
