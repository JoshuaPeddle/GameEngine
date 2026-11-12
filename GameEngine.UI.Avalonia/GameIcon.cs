using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace GameEngine.UI.Avalonia;

public enum GameIconKind { Menu, Pause, Play, Speed, Settings, Sound, Close, Back, Upgrade, Coin, Lock, Check, Info, Trophy, Research, Prestige }

public sealed class GameIconSource
{
    internal Geometry Geometry { get; }
    internal double Viewport { get; }
    private GameIconSource(string path, double viewport)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!double.IsFinite(viewport) || viewport <= 0) throw new ArgumentOutOfRangeException(nameof(viewport));
        Geometry = Geometry.Parse(path);
        Viewport = viewport;
    }
    public static GameIconSource FromPath(string path, double viewport = 24) => new(path, viewport);
}

public sealed class GameIcon : Control
{
    public static readonly StyledProperty<GameIconKind> KindProperty = AvaloniaProperty.Register<GameIcon, GameIconKind>(nameof(Kind));
    public static readonly StyledProperty<GameIconSource?> SourceProperty = AvaloniaProperty.Register<GameIcon, GameIconSource?>(nameof(Source));
    public GameIconSource? Source { get => GetValue(SourceProperty); set => SetValue(SourceProperty, value); }
    public static readonly StyledProperty<IBrush?> ForegroundProperty = AvaloniaProperty.Register<GameIcon, IBrush?>(nameof(Foreground));
    public GameIconKind Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    private static readonly Geometry[] Paths = new[]
    {
        "M4 6H20 M4 12H20 M4 18H15",
        "M7 5V19 M17 5V19",
        "M7 4L20 12 7 20Z",
        "M3 5L12 12 3 19 M12 5L21 12 12 19",
        "M9 3H15L16 6 20 7 22 12 20 17 16 18 15 21H9L8 18 4 17 2 12 4 7 8 6Z M16 12A4 4 0 1 1 8 12A4 4 0 1 1 16 12",
        "M3 9H7L12 4V20L7 15H3Z M16 8Q21 12 16 16 M19 4Q27 12 19 20",
        "M5 5L19 19 M19 5L5 19",
        "M14 4L6 12 14 20 M6 12H21",
        "M4 11L12 3 20 11 M12 3V21 M5 20H19",
        "M21 12A9 9 0 1 1 3 12A9 9 0 1 1 21 12 M12 6L16 12 12 18 8 12Z",
        "M6 10H18V21H6Z M8 10V7A4 4 0 0 1 16 7V10 M12 14V17",
        "M4 12L9 17 20 6",
        "M21 12A9 9 0 1 1 3 12A9 9 0 1 1 21 12 M12 11V17 M12 7V7.5",
        "M7 3H17V9Q17 15 12 15Q7 15 7 9Z M7 5H3V8Q3 11 7 11 M17 5H21V8Q21 11 17 11 M12 15V21 M7 21H17",
        "M9 3H15 M10 3V10L4 19Q3 21 6 21H18Q21 21 20 19L14 10V3 M8 14H16",
        "M12 2L15 9 22 12 15 15 12 22 9 15 2 12 9 9Z M3 3L5 5 M19 19L21 21"
    }.Select(Geometry.Parse).ToArray();
    static GameIcon() => AffectsRender<GameIcon>(KindProperty, SourceProperty, ForegroundProperty, GameUi.ThemeProperty);
    public GameIcon() { Width = 24; Height = 24; IsHitTestVisible = false; }
    public override void Render(DrawingContext context)
    {
        var theme = GameUi.GetTheme(this);
        double size = Math.Min(Bounds.Width, Bounds.Height);
        double viewport = Source?.Viewport ?? 24;
        double scale = size / viewport;
        if (scale <= 0) return;
        using var transform = context.PushTransform(Matrix.CreateScale(scale, scale) *
            Matrix.CreateTranslation((Bounds.Width - size) / 2, (Bounds.Height - size) / 2));
        var geometry = Source?.Geometry ?? Paths[(int)Kind];
        var brush = Foreground ?? GameUi.Brush(theme.Palette.Accent);
        double stroke = 1.6 * theme.Stroke * viewport / 24;
        if (theme.Glow > 0)
        {
            using var opacity = context.PushOpacity(Math.Clamp(theme.Glow, 0, 1) * .3);
            context.DrawGeometry(null, new Pen(brush, stroke * 2.4, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), geometry);
        }
        context.DrawGeometry(null, new Pen(brush, stroke,
            lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), geometry);
    }
}
