using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace GameEngine.UI.Avalonia;

public sealed class GameScreen : Border
{
    public static readonly StyledProperty<Thickness> SafeInsetsProperty = AvaloniaProperty.Register<GameScreen, Thickness>(nameof(SafeInsets));
    public Thickness SafeInsets { get => GetValue(SafeInsetsProperty); set => SetValue(SafeInsetsProperty, value); }
    private IInsetsManager? insets;
    public GameScreen() => Apply();
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        insets = TopLevel.GetTopLevel(this)?.InsetsManager;
        if (insets is not null) insets.SafeAreaChanged += OnSafeAreaChanged;
        Apply();
    }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (insets is not null) insets.SafeAreaChanged -= OnSafeAreaChanged;
        insets = null;
        base.OnDetachedFromVisualTree(e);
    }
    private void OnSafeAreaChanged(object? sender, SafeAreaChangedArgs e) => Apply();
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SafeInsetsProperty || change.Property == GameUi.ThemeProperty) Apply();
    }
    private void Apply()
    {
        var native = insets?.SafeAreaPadding ?? default;
        Padding = new Thickness(Math.Max(native.Left, SafeInsets.Left), Math.Max(native.Top, SafeInsets.Top),
            Math.Max(native.Right, SafeInsets.Right), Math.Max(native.Bottom, SafeInsets.Bottom));
        Background = GameUi.Brush(GameUi.GetTheme(this).Palette.Background);
    }
}

public sealed class GameSheet : Grid
{
    private readonly Border scrim = new() { Background = new SolidColorBrush(Color.Parse("#08121B"), .72) };
    private readonly GamePanel panel = new() { Ornaments = true, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(12) };
    private readonly GameLabel heading = new() { Role = GameTextRole.Title, VerticalAlignment = VerticalAlignment.Center };
    private readonly ContentControl body = new() { HorizontalContentAlignment = HorizontalAlignment.Stretch };
    private readonly GameButton close = new() { Kind = GameButtonKind.Icon, Icon = GameIconKind.Close };
    private readonly MotionDriver motion;
    private IInputElement? previousFocus;
    public bool IsOpen { get; private set; }
    public bool HasActiveMotion => motion.IsActive;
    public Control? Body { get => body.Content as Control; set => body.Content = value; }
    public string Title { get => heading.Text ?? ""; set => heading.Text = value; }
    public event EventHandler? Closed;

    public GameSheet()
    {
        IsVisible = false;
        KeyboardNavigation.SetTabNavigation(this, KeyboardNavigationMode.Cycle);
        AutomationProperties.SetName(close, "Close sheet");
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8 };
        header.Children.Add(heading); Grid.SetColumn(close, 1); header.Children.Add(close);
        var contents = new StackPanel { Spacing = 20, Children = { header, body } };
        panel.Content = new ScrollViewer { Content = contents, HorizontalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        Children.Add(scrim); Children.Add(panel);
        motion = new MotionDriver(this, progress =>
        {
            scrim.Opacity = progress; panel.Opacity = progress;
            panel.RenderTransform = new TranslateTransform(0, 36 * (1 - progress));
        });
        motion.Completed = () => { if (!IsOpen) { IsVisible = false; previousFocus?.Focus(); previousFocus = null; Closed?.Invoke(this, EventArgs.Empty); } };
        scrim.PointerPressed += (_, e) => { Close(); e.Handled = true; };
        close.Click += (_, _) => Close();
        KeyDown += (_, e) => { if (e.Key == Key.Escape) { Close(); e.Handled = true; } };
        SizeChanged += (_, _) => panel.MaxHeight = Math.Max(48, Bounds.Height - 24);
    }
    public void Open()
    {
        if (IsOpen) return;
        previousFocus = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        IsOpen = true; IsVisible = true; close.Focus();
        motion.Start(motion.Value, 1, GameUi.GetTheme(this).Motion.Enter);
    }
    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        GameUi.GetFeedback(this)?.Invoke(GameFeedback.Dismiss);
        motion.Start(motion.Value, 0, GameUi.GetTheme(this).Motion.Exit);
    }
}

public sealed class GameToast : GamePanel
{
    private readonly GameLabel message = new();
    private readonly DispatcherTimer expiry = new();
    private readonly MotionDriver motion;
    private bool showing;
    public bool HasActiveMotion => motion.IsActive;
    public GameToast()
    {
        IsVisible = false; IsHitTestVisible = false; Kind = SurfaceKind.Raised;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 12 };
        row.Children.Add(new GameIcon { Kind = GameIconKind.Check, VerticalAlignment = VerticalAlignment.Center });
        Grid.SetColumn(message, 1); row.Children.Add(message); Content = row;
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
        motion = new MotionDriver(this, progress => { Opacity = progress; RenderTransform = new TranslateTransform(0, 8 * (1 - progress)); });
        motion.Completed = () => { if (!showing) IsVisible = false; };
        expiry.Tick += (_, _) => Dismiss();
        DetachedFromVisualTree += (_, _) => { expiry.Stop(); showing = false; IsVisible = false; };
    }
    public void Show(string text, double seconds = 3)
    {
        if (!double.IsFinite(seconds) || seconds <= 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        expiry.Stop(); message.Text = text; AutomationProperties.SetName(this, text);
        showing = true; IsVisible = true;
        motion.Start(0, 1, GameUi.GetTheme(this).Motion.Enter);
        expiry.Interval = TimeSpan.FromSeconds(seconds); expiry.Start();
    }
    public void Dismiss()
    {
        expiry.Stop(); showing = false;
        motion.Start(motion.Value, 0, GameUi.GetTheme(this).Motion.Exit);
    }
}

public sealed class GameInfoPopover : GamePanel
{
    private readonly GameLabel message = new() { Ink = GameInk.Muted };
    public string Text { get => message.Text ?? ""; set => message.Text = value; }
    public GameInfoPopover()
    {
        Kind = SurfaceKind.Raised; IsVisible = false;
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 12 };
        row.Children.Add(new GameIcon { Kind = GameIconKind.Info }); Grid.SetColumn(message, 1); row.Children.Add(message);
        Content = row;
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
        KeyDown += (_, e) => { if (e.Key == Key.Escape) { IsVisible = false; e.Handled = true; } };
    }
    public void Toggle() => IsVisible = !IsVisible;
}
