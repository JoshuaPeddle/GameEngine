using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace GameEngine.UI.Avalonia;

public sealed class ResourceCounter : GamePanel
{
    public static readonly StyledProperty<double> ValueProperty = AvaloniaProperty.Register<ResourceCounter, double>(nameof(Value),
        validate: double.IsFinite);
    public static readonly StyledProperty<string> LabelProperty = AvaloniaProperty.Register<ResourceCounter, string>(nameof(Label), "SALVAGE");
    public static readonly StyledProperty<GameIconSource?> IconSourceProperty = AvaloniaProperty.Register<ResourceCounter, GameIconSource?>(nameof(IconSource));
    public GameIconSource? IconSource { get => GetValue(IconSourceProperty); set => SetValue(IconSourceProperty, value); }
    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    private readonly GameLabel number = new() { Role = GameTextRole.Title };
    private readonly GameLabel label = new() { Role = GameTextRole.Caption, Ink = GameInk.Muted };
    private readonly MotionDriver roll;
    private double rollFrom, rollTo;
    public bool HasActiveMotion => roll.IsActive;
    public string DisplayText => number.Text ?? "";
    public ResourceCounter()
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 12 };
        var icon = new GameIcon { Kind = GameIconKind.Coin, VerticalAlignment = VerticalAlignment.Center };
        icon.Bind(GameIcon.SourceProperty, new Binding(nameof(IconSource)) { Source = this });
        row.Children.Add(icon);
        var stack = new StackPanel { Children = { label, number } }; Grid.SetColumn(stack, 1); row.Children.Add(stack);
        Content = row;
        roll = new MotionDriver(this, v =>
        {
            number.Text = Format(v);
            double remaining = rollTo == rollFrom ? 0 : Math.Abs((rollTo - v) / (rollTo - rollFrom));
            number.RenderTransform = new TranslateTransform(0, -4 * remaining);
        });
        label.Text = Label; number.Text = Format(Value);
    }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty && roll is not null)
        {
            rollFrom = roll.Value; rollTo = Value;
            roll.Start(rollFrom, rollTo, GameUi.GetTheme(this).Motion.Counter);
            AutomationProperties.SetName(this, $"{Label}: {Value.ToString(CultureInfo.InvariantCulture)}");
        }
        if (change.Property == LabelProperty) label.Text = Label;
    }
    public static string Format(double value)
    {
        if (!double.IsFinite(value)) throw new ArgumentOutOfRangeException(nameof(value));
        string[] suffixes = ["", "K", "M", "B", "T", "Qa", "Qi"];
        double magnitude = Math.Abs(value);
        if (magnitude >= 1e21) return value.ToString("0.##e+0", CultureInfo.InvariantCulture);
        int tier = 0;
        while (magnitude >= 999.5 && tier < suffixes.Length - 1) { value /= 1000; magnitude /= 1000; tier++; }
        return value.ToString(tier == 0 ? "N0" : magnitude < 10 ? "0.##" : magnitude < 100 ? "0.#" : "0", CultureInfo.InvariantCulture) + suffixes[tier];
    }
}

public sealed class GameBadge : GamePanel
{
    public static readonly StyledProperty<string> TextProperty = AvaloniaProperty.Register<GameBadge, string>(nameof(Text), "RARE");
    public static readonly StyledProperty<GameInk> InkProperty = GameLabel.InkProperty.AddOwner<GameBadge>();
    public string Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public GameInk Ink { get => GetValue(InkProperty); set => SetValue(InkProperty, value); }
    public static readonly StyledProperty<GameIconSource?> IconSourceProperty = AvaloniaProperty.Register<GameBadge, GameIconSource?>(nameof(IconSource));
    public static readonly StyledProperty<GameIconKind?> IconProperty = GameButton.IconProperty.AddOwner<GameBadge>();
    public GameIconSource? IconSource { get => GetValue(IconSourceProperty); set => SetValue(IconSourceProperty, value); }
    public GameIconKind? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
    private readonly GameIcon glyph = new() { Width = 16, Height = 16, VerticalAlignment = VerticalAlignment.Center };
    public GameBadge()
    {
        Padding = new Thickness(12, 6); HorizontalAlignment = HorizontalAlignment.Left;
        var label = new GameLabel { Role = GameTextRole.Caption };
        label.Bind(TextBlock.TextProperty, new Binding(nameof(Text)) { Source = this });
        label.Bind(GameLabel.InkProperty, new Binding(nameof(Ink)) { Source = this });
        glyph.Bind(GameIcon.SourceProperty, new Binding(nameof(IconSource)) { Source = this });
        glyph.Bind(GameIcon.ForegroundProperty, new Binding(nameof(GameLabel.Foreground)) { Source = label });
        glyph.IsVisible = IconSource is not null || Icon.HasValue;
        Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { glyph, label } };
    }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (glyph is null) return;
        if (change.Property == IconProperty || change.Property == IconSourceProperty)
        {
            glyph.Kind = Icon ?? GameIconKind.Check;
            glyph.IsVisible = IconSource is not null || Icon.HasValue;
        }
    }

}

internal sealed class MeterFill : Control
{
    public static readonly StyledProperty<double> FractionProperty = AvaloniaProperty.Register<MeterFill, double>(nameof(Fraction));
    public double Fraction { get => GetValue(FractionProperty); set => SetValue(FractionProperty, value); }
    static MeterFill() => AffectsRender<MeterFill>(FractionProperty, GameUi.ThemeProperty);
    public override void Render(DrawingContext context)
    {
        var p = GameUi.GetTheme(this).Palette;
        var rect = new Rect(0, (Bounds.Height - 10) / 2, Bounds.Width, 10);
        context.DrawRectangle(GameUi.Brush(p.Background), new Pen(GameUi.Brush(p.Rim), 1), rect, 5, 5);
        double width = Bounds.Width * Math.Clamp(Fraction, 0, 1);
        if (width <= 0) return;
        var fill = new Rect(rect.X, rect.Y, width, rect.Height);
        context.DrawRectangle(new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative), GradientStops =
            { new GradientStop(p.Accent, 0), new GradientStop(p.Accent, .6), new GradientStop(p.Rim, 1) } }, null, fill, 5, 5);
        context.DrawLine(new Pen(new SolidColorBrush(Colors.White, .35), 1), new Point(5, rect.Y + 2), new Point(Math.Max(5, width - 5), rect.Y + 2));
    }
}

public class GameProgress : ProgressBar
{
    public static readonly StyledProperty<string> LabelProperty = AvaloniaProperty.Register<GameProgress, string>(nameof(Label), "PROGRESS");
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    private readonly GameLabel caption = new() { Role = GameTextRole.Caption, Ink = GameInk.Muted };
    private readonly GameLabel amount = new() { Role = GameTextRole.Caption, Ink = GameInk.Accent, HorizontalAlignment = HorizontalAlignment.Right };
    private readonly MeterFill meter = new() { Height = 16, Fraction = 1 };
    public GameProgress()
    {
        MinHeight = 40;
        Template = new FuncControlTemplate<GameProgress>((_, scope) =>
        {
            var title = new Grid { Children = { caption, amount } };
            var indicator = new Border { Name = "PART_Indicator", HorizontalAlignment = HorizontalAlignment.Left, Child = meter };
            scope.Register(indicator.Name, indicator);
            var track = new Grid { Height = 16, Children = { new MeterFill(), indicator } };
            return new StackPanel { Spacing = 4, Children = { title, track } };
        });
        Refresh();
    }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty || change.Property == MinimumProperty || change.Property == MaximumProperty || change.Property == LabelProperty) Refresh();
    }
    private void Refresh()
    {
        if (meter is null) return;
        double fraction = Maximum > Minimum ? (Value - Minimum) / (Maximum - Minimum) : 0;
        caption.Text = Label; amount.Text = $"{fraction:P0}";
        AutomationProperties.SetName(this, Label);
    }
}

public sealed class GameCooldown : GameProgress
{
    public static readonly StyledProperty<double> RemainingProperty = AvaloniaProperty.Register<GameCooldown, double>(nameof(Remaining),
        validate: value => double.IsFinite(value) && value >= 0);
    public double Remaining { get => GetValue(RemainingProperty); set => SetValue(RemainingProperty, value); }
    public void Start(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds <= 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        Maximum = seconds; Remaining = seconds;
    }
    public void Advance(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        Remaining = Math.Max(0, Remaining - seconds);
    }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != RemainingProperty) return;
        Value = Math.Min(Maximum, Remaining);
        Label = Remaining > 0 ? $"READY IN {Remaining:0.0}s" : "READY";
    }
}

public sealed class GameSlider : Slider
{
    public GameSlider()
    {
        MinHeight = 48; MinWidth = 48;
        Template = new FuncControlTemplate<GameSlider>((owner, scope) =>
        {
            var meter = new MeterFill { Margin = new Thickness(12, 0) };
            var thumb = new Thumb { Width = 28, Height = 36, Template = new FuncControlTemplate<Thumb>((_, _) =>
                new Grid { Background = Brushes.Transparent, Children = { new GameSurface { Kind = SurfaceKind.Primary } } }) };
            var track = new Track { Name = "PART_Track", Thumb = thumb, Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center, Height = 48 };
            scope.Register(track.Name, track);
            foreach (string name in new[] { "PART_DecreaseButton", "PART_IncreaseButton" })
            {
                var hit = new Button { Name = name, Focusable = false,
                    Template = new FuncControlTemplate<Button>((_, _) => new Border { Background = Brushes.Transparent }) };
                scope.Register(name, hit);
                if (name == "PART_DecreaseButton") track.DecreaseButton = hit; else track.IncreaseButton = hit;
            }
            track.Bind(Track.MinimumProperty, new Binding(nameof(Minimum)) { Source = owner });
            track.Bind(Track.MaximumProperty, new Binding(nameof(Maximum)) { Source = owner });
            track.Bind(Track.ValueProperty, new Binding(nameof(Value)) { Source = owner, Mode = BindingMode.TwoWay });
            void Refresh() => meter.Fraction = Maximum > Minimum ? (Value - Minimum) / (Maximum - Minimum) : 0;
            owner.PropertyChanged += (_, e) => { if (e.Property == ValueProperty || e.Property == MaximumProperty || e.Property == MinimumProperty) Refresh(); };
            Refresh();
            return new Grid { Background = Brushes.Transparent, Children = { meter, track } };
        });
        KeyUp += (_, e) => { if (e.Key is Key.Left or Key.Right or Key.Home or Key.End) GameUi.GetFeedback(this)?.Invoke(GameFeedback.Adjust); };
        PointerReleased += (_, _) => GameUi.GetFeedback(this)?.Invoke(GameFeedback.Adjust);
    }
}
