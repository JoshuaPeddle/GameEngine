using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;

namespace GameEngine.UI.Avalonia;

public sealed class GameTab : TabItem
{
    private readonly GameSurface surface = new() { Kind = SurfaceKind.Quiet };
    private readonly GameLabel label = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    public GameTab()
    {
        MinWidth = 48; MinHeight = 48;
        Template = new FuncControlTemplate<GameTab>((owner, _) =>
        {
            label.Bind(TextBlock.TextProperty, new Binding(nameof(Header)) { Source = owner });
            return new Grid { Children = { surface, new Border { Padding = new Thickness(12, 10), Child = label } } };
        });
    }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (surface is null || label is null) return;
        if (change.Property == IsSelectedProperty || change.Property == IsPointerOverProperty || change.Property == IsFocusedProperty)
        {
            surface.Kind = IsSelected ? SurfaceKind.Raised : SurfaceKind.Quiet;
            surface.State = IsFocused ? SurfaceState.Focused : IsPointerOver ? SurfaceState.Hover : SurfaceState.Normal;
            label.Ink = IsSelected ? GameInk.Accent : GameInk.Muted;
        }
    }
}

public sealed class GameTabs : TabControl
{
    public GameTabs()
    {
        ItemsPanel = new FuncTemplate<Panel?>(() => new UniformGrid { Rows = 1 });
        Template = new FuncControlTemplate<GameTabs>((owner, scope) =>
        {
            var headers = new ItemsPresenter { Name = "PART_ItemsPresenter" };
            scope.Register(headers.Name, headers);
            var content = new ContentPresenter { Name = "PART_SelectedContentHost", Margin = new Thickness(0, 12, 0, 0) };
            scope.Register(content.Name, content);
            content.Bind(ContentPresenter.ContentProperty, new Binding(nameof(SelectedContent)) { Source = owner });
            return new StackPanel { Children = { headers, content } };
        });
        SelectionChanged += (_, _) => GameUi.GetFeedback(this)?.Invoke(GameFeedback.Activate);
    }
}

public sealed class GameItemCard : GamePanel
{
    public static readonly StyledProperty<string> TitleProperty = AvaloniaProperty.Register<GameItemCard, string>(nameof(Title), "Upgrade");
    public static readonly StyledProperty<string> DescriptionProperty = AvaloniaProperty.Register<GameItemCard, string>(nameof(Description), "");
    public static readonly StyledProperty<double> CostProperty = AvaloniaProperty.Register<GameItemCard, double>(nameof(Cost), 0,
        validate: v => double.IsFinite(v) && v >= 0);
    public static readonly StyledProperty<double> BalanceProperty = AvaloniaProperty.Register<GameItemCard, double>(nameof(Balance), 0,
        validate: v => double.IsFinite(v) && v >= 0);
    public static readonly StyledProperty<bool> LockedProperty = AvaloniaProperty.Register<GameItemCard, bool>(nameof(Locked));
    public static readonly StyledProperty<GameIconKind> IconProperty = AvaloniaProperty.Register<GameItemCard, GameIconKind>(nameof(Icon), GameIconKind.Upgrade);
    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    public double Cost { get => GetValue(CostProperty); set => SetValue(CostProperty, value); }
    public double Balance { get => GetValue(BalanceProperty); set => SetValue(BalanceProperty, value); }
    public bool Locked { get => GetValue(LockedProperty); set => SetValue(LockedProperty, value); }
    public GameIconKind Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public bool CanAfford => !Locked && Balance >= Cost;
    public GameButton ActionButton { get; } = new() { Icon = GameIconKind.Coin };
    public event EventHandler? PurchaseRequested;
    private readonly GameLabel status = new() { Role = GameTextRole.Caption, Ink = GameInk.Muted };
    public GameItemCard()
    {
        var icon = new GameIcon { Width = 32, Height = 32 };
        icon.Bind(GameIcon.KindProperty, new Binding(nameof(Icon)) { Source = this });
        var title = new GameLabel { Role = GameTextRole.Title };
        title.Bind(TextBlock.TextProperty, new Binding(nameof(Title)) { Source = this });
        var description = new GameLabel { Ink = GameInk.Muted };
        description.Bind(TextBlock.TextProperty, new Binding(nameof(Description)) { Source = this });
        var heading = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 12 };
        heading.Children.Add(new GamePanel { Kind = SurfaceKind.Raised, Padding = new Thickness(12), Content = icon });
        var words = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center, Children = { title, status } };
        Grid.SetColumn(words, 1); heading.Children.Add(words);
        Content = new StackPanel { Spacing = 12, Children = { heading, description, ActionButton } };
        ActionButton.Click += (_, _) => { if (CanAfford) PurchaseRequested?.Invoke(this, EventArgs.Empty); };
        Refresh();
    }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CostProperty || change.Property == BalanceProperty || change.Property == LockedProperty) Refresh();
    }
    private void Refresh()
    {
        if (ActionButton is null) return;
        ActionButton.IsEnabled = CanAfford;
        ActionButton.Content = $"Install  ·  {ResourceCounter.Format(Cost)}";
        ActionButton.Icon = Locked ? GameIconKind.Lock : GameIconKind.Coin;
        status.Text = Locked ? "RESEARCH REQUIRED" : CanAfford ? "READY TO INSTALL" : $"NEED {ResourceCounter.Format(Cost - Balance)} MORE";
        status.Ink = CanAfford ? GameInk.Accent : GameInk.Danger;
    }
}
