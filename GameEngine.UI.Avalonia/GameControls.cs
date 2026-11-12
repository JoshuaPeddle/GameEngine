using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Input;
using Avalonia.Media;

namespace GameEngine.UI.Avalonia;

public enum GameTextRole { Caption, Body, Title, Display }
public enum GameInk { Normal, Muted, Accent, Danger, Legendary, Rare, Epic }

public sealed class GameLabel : TextBlock
{
    public static readonly StyledProperty<GameTextRole> RoleProperty = AvaloniaProperty.Register<GameLabel, GameTextRole>(nameof(Role), GameTextRole.Body);
    public static readonly StyledProperty<GameInk> InkProperty = AvaloniaProperty.Register<GameLabel, GameInk>(nameof(Ink));
    public GameTextRole Role { get => GetValue(RoleProperty); set => SetValue(RoleProperty, value); }
    public GameInk Ink { get => GetValue(InkProperty); set => SetValue(InkProperty, value); }
    public GameLabel() { TextWrapping = TextWrapping.Wrap; FontFeatures = FontFeatureCollection.Parse("tnum"); Refresh(); }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == RoleProperty || change.Property == InkProperty || change.Property == GameUi.ThemeProperty || change.Property == GameUi.TextScaleProperty) Refresh();
    }
    private void Refresh()
    {
        var theme = GameUi.GetTheme(this); var p = theme.Palette;
        FontFamily = Role is GameTextRole.Display or GameTextRole.Title ? theme.DisplayFont : theme.TextFont;
        FontSize = GameUi.GetTextScale(this) * (Role switch
        { GameTextRole.Caption => theme.Type.Caption, GameTextRole.Title => theme.Type.Title, GameTextRole.Display => theme.Type.Display, _ => theme.Type.Body });
        Foreground = GameUi.Brush(Ink switch
        { GameInk.Muted => p.Muted, GameInk.Accent => p.Accent, GameInk.Danger => p.Danger, GameInk.Legendary => p.Legendary, GameInk.Rare => p.Rare, GameInk.Epic => p.Epic, _ => p.Ink });
    }
}

public class GamePanel : ContentControl
{
    public static readonly StyledProperty<bool> OrnamentsProperty = GameSurface.OrnamentsProperty.AddOwner<GamePanel>();
    public static readonly StyledProperty<SurfaceKind> KindProperty = GameSurface.KindProperty.AddOwner<GamePanel>();
    public bool Ornaments { get => GetValue(OrnamentsProperty); set => SetValue(OrnamentsProperty, value); }
    public SurfaceKind Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }
    public GamePanel()
    {
        SetValue(PaddingProperty, new Thickness(GameUi.GetTheme(this).Space.Medium), BindingPriority.Style);
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        Template = new FuncControlTemplate<GamePanel>((owner, scope) =>
        {
            var surface = new GameSurface();
            surface.Bind(GameSurface.KindProperty, new Binding(nameof(Kind)) { Source = owner });
            surface.Bind(GameSurface.OrnamentsProperty, new Binding(nameof(Ornaments)) { Source = owner });
            var presenter = new ContentPresenter { Name = "PART_ContentPresenter", HorizontalContentAlignment = HorizontalAlignment.Stretch };
            scope.Register(presenter.Name, presenter);
            presenter.Bind(ContentPresenter.ContentProperty, new Binding(nameof(Content)) { Source = owner });
            presenter.Bind(ContentPresenter.PaddingProperty, new Binding(nameof(Padding)) { Source = owner });
            return new Grid { Children = { surface, presenter } };
        });
    }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == GameUi.ThemeProperty) SetValue(PaddingProperty, new Thickness(GameUi.GetTheme(this).Space.Medium), BindingPriority.Style);
    }
}

public enum GameButtonKind { Primary, Secondary, Icon }

public class GameButton : Button
{
    public static readonly StyledProperty<GameButtonKind> KindProperty = AvaloniaProperty.Register<GameButton, GameButtonKind>(nameof(Kind));
    public static readonly StyledProperty<GameIconKind?> IconProperty = AvaloniaProperty.Register<GameButton, GameIconKind?>(nameof(Icon));
    private readonly ButtonSkin skin;
    public GameButtonKind Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }
    public GameIconKind? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public bool HasActiveMotion => skin.IsAnimating;
    public GameButton()
    {
        skin = new ButtonSkin(this, () => Kind == GameButtonKind.Primary ? SurfaceKind.Primary : SurfaceKind.Raised, () => Icon);
        Click += (_, _) => GameUi.GetFeedback(this)?.Invoke(GameFeedback.Activate);
    }
}

public sealed class GameToggle : ToggleButton
{
    private readonly ButtonSkin skin;
    public bool HasActiveMotion => skin.IsAnimating;
    public GameToggle()
    {
        skin = new ButtonSkin(this, () => IsChecked == true ? SurfaceKind.Primary : SurfaceKind.Raised,
            () => IsChecked == true ? GameIconKind.Check : GameIconKind.Close);
        Click += (_, _) => GameUi.GetFeedback(this)?.Invoke(GameFeedback.Toggle);
    }
}

internal sealed class ButtonSkin
{
    private readonly Button owner;
    private readonly Func<SurfaceKind> kind;
    private readonly Func<GameIconKind?> icon;
    private readonly MotionDriver motion;
    private GameSurface? surface;
    private GameIcon? glyph;
    private Grid? body;
    private Grid? row;
    private double scale = 1;
    public bool IsAnimating => motion.IsActive;
    public ButtonSkin(Button owner, Func<SurfaceKind> kind, Func<GameIconKind?> icon)
    {
        this.owner = owner; this.kind = kind; this.icon = icon;
        owner.MinHeight = 48; owner.MinWidth = 48;
        owner.HorizontalContentAlignment = HorizontalAlignment.Center;
        owner.VerticalContentAlignment = VerticalAlignment.Center;
        owner.ContentTemplate = new FuncDataTemplate<string>((text, _) => new TextBlock
        { Text = text, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center });
        motion = new MotionDriver(owner, value =>
        {
            scale = value;
            if (body is not null) body.RenderTransform = new ScaleTransform(value, value);
        });
        owner.Template = new FuncControlTemplate<Button>((button, scope) =>
        {
            surface = new GameSurface();
            glyph = new GameIcon { VerticalAlignment = VerticalAlignment.Center };
            var presenter = new ContentPresenter { Name = "PART_ContentPresenter", VerticalAlignment = VerticalAlignment.Center };
            scope.Register(presenter.Name, presenter);
            presenter.Bind(ContentPresenter.ContentProperty, new Binding(nameof(Button.Content)) { Source = owner });
            presenter.Bind(ContentPresenter.ContentTemplateProperty, new Binding(nameof(Button.ContentTemplate)) { Source = owner });
            row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 10,
                VerticalAlignment = VerticalAlignment.Center, Children = { glyph, presenter } };
            Grid.SetColumn(presenter, 1);
            presenter.HorizontalContentAlignment = HorizontalAlignment.Center;
            var inset = new Border { Child = row };
            inset.Bind(Border.PaddingProperty, new Binding(nameof(Button.Padding)) { Source = owner });
            body = new Grid { Background = Brushes.Transparent, RenderTransformOrigin = RelativePoint.Center, Children = { surface, inset } };
            Refresh();
            return body;
        });
        owner.PropertyChanged += (_, e) =>
        {
            if (e.Property == Button.IsPressedProperty) motion.Start(scale, owner.IsPressed ? .96 : 1, GameUi.GetTheme(owner).Motion.Press);
            if (e.Property == GameUi.ThemeProperty || e.Property == GameUi.TextScaleProperty || e.Property == InputElement.IsPointerOverProperty ||
                e.Property == InputElement.IsEffectivelyEnabledProperty || e.Property == Button.IsPressedProperty || e.Property == InputElement.IsFocusedProperty ||
                e.Property == GameButton.KindProperty || e.Property == GameButton.IconProperty || e.Property == ToggleButton.IsCheckedProperty || e.Property == ContentControl.ContentProperty) Refresh();
        };
        Refresh();
    }
    private void Refresh()
    {
        var theme = GameUi.GetTheme(owner); var p = theme.Palette;
        owner.FontFamily = theme.DisplayFont; owner.FontSize = theme.Type.Body * GameUi.GetTextScale(owner);
        owner.FontWeight = FontWeight.SemiBold;
        owner.SetValue(TemplatedControl.PaddingProperty, new Thickness(theme.Space.Medium, theme.Space.Small + 4), BindingPriority.Style);
        var enabled = owner.IsEffectivelyEnabled;
        owner.Foreground = GameUi.Brush(!enabled ? p.Muted : kind() == SurfaceKind.Primary ? p.OnAccent : p.Ink);
        if (surface is null || glyph is null) return;
        surface.Kind = kind();
        surface.State = !enabled ? SurfaceState.Disabled : owner.IsPressed ? SurfaceState.Pressed : owner.IsFocused ? SurfaceState.Focused : owner.IsPointerOver ? SurfaceState.Hover : SurfaceState.Normal;
        glyph.IsVisible = icon().HasValue; glyph.Kind = icon() ?? GameIconKind.Play;
        glyph.Foreground = owner.Foreground;
        if (row is not null)
        {
            row.HorizontalAlignment = owner.Content is null ? HorizontalAlignment.Center : HorizontalAlignment.Stretch;
            row.ColumnSpacing = icon().HasValue && owner.Content is not null ? 10 : 0;
        }
    }
}
