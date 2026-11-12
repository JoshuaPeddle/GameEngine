using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace GameEngine.UI.Avalonia;

public sealed record GamePalette(Color Background, Color Surface, Color Raised, Color Ink,
    Color Muted, Color Accent, Color OnAccent, Color Danger, Color Rare, Color Epic, Color Legendary, Color Rim);

public sealed record GameTypeScale(double Caption = 12, double Body = 16, double Title = 22, double Display = 38);
public sealed record GameSpacing(double Small = 8, double Medium = 16, double Large = 24);
public sealed record GameRadii(double Control = 12, double Panel = 20);
public enum GameEasing { Linear, OutCubic, InOutCubic }
public sealed record GameMotion(double Press = .10, double Enter = .28, double Exit = .18,
    double Counter = .45, GameEasing Easing = GameEasing.OutCubic);

public sealed record GameTheme(string Name, GamePalette Palette)
{
    public GameTypeScale Type { get; init; } = new();
    public GameSpacing Space { get; init; } = new();
    public GameRadii Radii { get; init; } = new();
    public GameMotion Motion { get; init; } = new();
    public double Stroke { get; init; } = 1;
    public double Glow { get; init; } = .18;
    public FontFamily DisplayFont { get; init; } = GameFonts.Display;
    public FontFamily TextFont { get; init; } = GameFonts.Text;

    private static Color C(string value) => Color.Parse(value);
    public static GameTheme NightCoast { get; } = new("Night coast", new(
        C("#081722"), C("#122C39"), C("#204454"), C("#F0F8F4"), C("#AFCCD2"),
        C("#9CF0CD"), C("#102D2A"), C("#FFB3AA"), C("#9ED9FF"), C("#D8BCFF"), C("#F6D28B"), C("#547681")));
    public static GameTheme Daybreak { get; } = new("Daybreak", new(
        C("#F4EDDC"), C("#FFF9EA"), C("#E6DDC8"), C("#253A36"), C("#51645D"),
        C("#216B58"), C("#FFFFFF"), C("#A52E3E"), C("#235C91"), C("#704099"), C("#795419"), C("#8B9884")))
    { Glow = .10, Radii = new(14, 22) };

    public static double Contrast(Color first, Color second)
    {
        static double Channel(byte value) => value <= 10 ? value / 3294.6 : Math.Pow((value / 255d + .055) / 1.055, 2.4);
        static double L(Color c) => .2126 * Channel(c.R) + .7152 * Channel(c.G) + .0722 * Channel(c.B);
        double a = L(first), b = L(second);
        return (Math.Max(a, b) + .05) / (Math.Min(a, b) + .05);
    }
}

public static class GameFonts
{
    public static FontFamily Display { get; } = new("avares://GameEngine.UI.Avalonia/Fonts#Oxanium");
    public static FontFamily Text { get; } = new("avares://GameEngine.UI.Avalonia/Fonts#Atkinson Hyperlegible");
}

public sealed class GameUi : AvaloniaObject
{
    public static readonly AttachedProperty<GameTheme> ThemeProperty =
        AvaloniaProperty.RegisterAttached<GameUi, Control, GameTheme>("Theme", GameTheme.NightCoast, inherits: true);
    public static readonly AttachedProperty<double> TextScaleProperty =
        AvaloniaProperty.RegisterAttached<GameUi, Control, double>("TextScale", 1, inherits: true,
            validate: value => double.IsFinite(value) && value >= 1 && value <= 3);
    public static readonly AttachedProperty<bool> ReducedMotionProperty =
        AvaloniaProperty.RegisterAttached<GameUi, Control, bool>("ReducedMotion", false, inherits: true);
    public static readonly AttachedProperty<Action<GameFeedback>?> FeedbackProperty =
        AvaloniaProperty.RegisterAttached<GameUi, Control, Action<GameFeedback>?>("Feedback", inherits: true);

    public static GameTheme GetTheme(Control control) => control.GetValue(ThemeProperty);
    public static void SetTheme(Control control, GameTheme value) => control.SetValue(ThemeProperty, value);
    public static double GetTextScale(Control control) => control.GetValue(TextScaleProperty);
    public static void SetTextScale(Control control, double value) => control.SetValue(TextScaleProperty, value);
    public static bool GetReducedMotion(Control control) => control.GetValue(ReducedMotionProperty);
    public static void SetReducedMotion(Control control, bool value) => control.SetValue(ReducedMotionProperty, value);
    public static Action<GameFeedback>? GetFeedback(Control control) => control.GetValue(FeedbackProperty);
    public static void SetFeedback(Control control, Action<GameFeedback>? value) => control.SetValue(FeedbackProperty, value);
    internal static IBrush Brush(Color value) => new SolidColorBrush(value);
}

public enum GameFeedback { Activate, Toggle, Adjust, Dismiss }
