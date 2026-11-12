using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using GameEngine.UI.Avalonia;

namespace GameEngine.Demo;

public sealed class UiGalleryView : UserControl
{
    private readonly SceneUiGallery scene;
    private readonly Dictionary<int, Control[]> pageCache = new();
    private readonly GameScrollViewer scroll = new();
    private readonly StackPanel pages = new() { Spacing = 12 };
    private readonly ResourceCounter wallet = new() { Label = "SALVAGE" };
    private static readonly GameIconSource RelayIcon = GameIconSource.FromPath("M12 2L21 7V17L12 22 3 17V7Z M3 7L12 12 21 7 M12 12V22 M7 5L17 19");
    private readonly GameItemCard upgrade = new() { Title = "Prism relay", Description = "Split the light. Each pulse finds a second target.", Cost = 2400, IconSource = RelayIcon };
    private readonly GameCooldown cooldown = new() { Maximum = 30 };
    private readonly GameLabel rank = Label("PRISM RELAY / RANK 03", GameTextRole.Caption, GameInk.Accent);
    private readonly GameToast toast = new() { VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(20, 80, 20, 0) };
    public GameSheet Sheet { get; } = new();
    public GameTabs Navigation { get; } = new();
    public GameScreen Screen { get; } = new();
    public ResourceCounter Wallet => wallet;
    public GameItemCard UpgradeCard => upgrade;
    public GameToast Toast => toast;
    public void ScrollToEnd() => scroll.ScrollToEnd();
    public UiGalleryView(SceneUiGallery scene, bool bright = false)
    {
        this.scene = scene;
        GameUi.SetTheme(this, bright ? GameTheme.Daybreak : GameTheme.NightCoast);
        var content = new StackPanel { Spacing = 16, Margin = new Thickness(20, 20, 20, 16) };
        var top = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12 };
        var wordmark = new StackPanel { Spacing = 2, Children = { Label("A U R O R A", GameTextRole.Title), Label("KEEPERS OF THE COAST", GameTextRole.Caption, GameInk.Muted) } };
        top.Children.Add(wordmark);
        var theme = Button(GameIconKind.Settings, "Change theme", () => GameUi.SetTheme(this,
            GameUi.GetTheme(this) == GameTheme.NightCoast ? GameTheme.Daybreak : GameTheme.NightCoast));
        Grid.SetColumn(theme, 1); top.Children.Add(theme); content.Children.Add(top);
        Navigation.Items.Add(new GameTab { Header = "Workshop" });
        Navigation.Items.Add(new GameTab { Header = "Settings" });
        Navigation.Items.Add(new GameTab { Header = "Library" });
        Navigation.SelectionChanged += (_, _) => ShowPage(Navigation.SelectedIndex);
        content.Children.Add(Navigation);
        pages.Margin = new Thickness(20, 0, 20, 24); scroll.Content = pages;
        var layout = new Grid { RowDefinitions = new RowDefinitions("Auto,*"), Children = { content, scroll } };
        Grid.SetRow(scroll, 1);
        var root = new Grid { Children = { layout, Sheet, toast } };
        Screen.Child = root; Content = Screen;
        Sheet.Title = "Return to the coast";
        Sheet.Body = new StackPanel { Spacing = 16, Children =
        {
            new GameBadge { Text = "EXPEDITION COMPLETE", Ink = GameInk.Legendary },
            Label("A light worth keeping.", GameTextRole.Display),
            Label("Your keepers worked through the night. Collect their salvage and build the next beacon.", GameTextRole.Body, GameInk.Muted),
            new ResourceCounter { Label = "AWAY EARNINGS", Value = 4820 },
            Action("Collect salvage", GameIconKind.Coin, () => { Sheet.Close(); toast.Show("4.82K salvage collected"); }),
            Action("Stay a little longer", GameIconKind.Back, Sheet.Close, GameButtonKind.Secondary)
        } };
        upgrade.PurchaseRequested += (_, _) => { Send(scene.Upgrade); toast.Show("Prism relay upgraded"); };
        AttachedToVisualTree += (_, _) => { scene.Changed += QueueRefresh; Refresh(); };
        DetachedFromVisualTree += (_, _) => scene.Changed -= QueueRefresh;
        Navigation.SelectedIndex = 0; ShowPage(0); Refresh();
    }
    private void QueueRefresh() => Dispatcher.UIThread.Post(() => { if (TopLevel.GetTopLevel(this) is not null) Refresh(); });
    private void Send(Action action) { if (scene.Engine is { } engine) engine.Post(_ => action()); else action(); }
    private void Refresh()
    {
        var state = scene.State; wallet.Value = state.Salvage; upgrade.Balance = state.Salvage;
        rank.Text = $"PRISM RELAY / RANK {state.Rank:00}"; cooldown.Remaining = state.ResearchRemaining;
    }
    public void ShowPage(int index)
    {
        pages.Children.Clear();
        if (pageCache.TryGetValue(index, out var cached)) pages.Children.AddRange(cached);
        else
        {
            if (index == 1) Settings(); else if (index == 2) Library(); else Workshop();
            pageCache[index] = pages.Children.ToArray();
        }
        scroll.Offset = default;
    }
    private void Workshop()
    {
        var hero = new Grid { Height = 164, ClipToBounds = true };
        hero.Children.Add(new CoastIllustration());
        hero.Children.Add(new StackPanel { Spacing = 6, Margin = new Thickness(18), VerticalAlignment = VerticalAlignment.Bottom,
            Children = { Label("EXPEDITION / 07", GameTextRole.Caption, GameInk.Accent), Label("The night coast", GameTextRole.Title), Label("One beacon. A thousand tomorrows.", GameTextRole.Caption, GameInk.Muted) } });
        pages.Children.Add(new GamePanel { Padding = new Thickness(0), Content = hero, Ornaments = true });
        var resources = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), ColumnSpacing = 8 };
        resources.Children.Add(wallet);
        var wave = new ResourceCounter { Label = "WAVE RECORD", Value = 28 }; Grid.SetColumn(wave, 1); resources.Children.Add(wave);
        pages.Children.Add(resources);
        pages.Children.Add(rank); pages.Children.Add(upgrade);
        pages.Children.Add(new GameProgress { Label = "NEXT KEEPER RANK", Value = 68 });
        pages.Children.Add(Action("Begin expedition", GameIconKind.Play, Sheet.Open));
        pages.Children.Add(Label("NIGHT COAST COLLECTION  /  01", GameTextRole.Caption, GameInk.Muted));
    }
    private void Settings()
    {
        pages.Children.Add(Label("Make it yours.", GameTextRole.Display));
        pages.Children.Add(Label("A quieter coast, or a little more thunder.", GameTextRole.Body, GameInk.Muted));
        var audio = new StackPanel { Spacing = 8 };
        audio.Children.Add(Label("SOUND & TOUCH", GameTextRole.Caption, GameInk.Accent));
        audio.Children.Add(Label("Music", GameTextRole.Title));
        var slider = new GameSlider { Value = 64 }; AutomationProperties.SetName(slider, "Music volume"); audio.Children.Add(slider);
        audio.Children.Add(new GameToggle { Content = "Sound enabled", IsChecked = true });
        audio.Children.Add(new GameToggle { Content = "Haptics", IsChecked = false });
        var reduce = new GameToggle { Content = "Reduced motion", IsChecked = GameUi.GetReducedMotion(this) };
        reduce.Click += (_, _) => GameUi.SetReducedMotion(this, reduce.IsChecked == true); audio.Children.Add(reduce);
        pages.Children.Add(new GamePanel { Content = audio });
        var info = new GameInfoPopover { Text = "Research continues while you are away. A completed study strengthens every future expedition." };
        pages.Children.Add(Action("About research", GameIconKind.Info, info.Toggle, GameButtonKind.Secondary));
        pages.Children.Add(info); pages.Children.Add(cooldown);
        pages.Children.Add(Action("Start a 30s study", GameIconKind.Research, () => Send(scene.BeginResearch)));
        pages.Children.Add(Action("Preview reward sheet", GameIconKind.Trophy, Sheet.Open, GameButtonKind.Secondary));
        pages.Children.Add(Action("Send a test notification", GameIconKind.Check, () => toast.Show("Your changes are saved"), GameButtonKind.Secondary));
    }
    private void Library()
    {
        pages.Children.Add(Label("Little details.\nLasting character.", GameTextRole.Display));
        pages.Children.Add(Label("THE KEEPER'S TOOLKIT", GameTextRole.Caption, GameInk.Accent));
        var icons = new UniformGrid { Columns = 4, Rows = 4 };
        foreach (var icon in Enum.GetValues<GameIconKind>())
        {
            var captured = icon;
            icons.Children.Add(Button(icon, icon.ToString(), () => toast.Show(captured.ToString())));
        }
        pages.Children.Add(icons);
        var badges = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var (text, ink) in new[] { ("RARE", GameInk.Rare), ("EPIC", GameInk.Epic), ("LEGENDARY", GameInk.Legendary) })
            badges.Children.Add(new GameBadge { Text = text, Ink = ink, Margin = new Thickness(0, 0, 6, 0) });
        pages.Children.Add(badges);
        pages.Children.Add(new GameItemCard { Title = "Storm lens", Description = "Pierce the storm with concentrated light.", Cost = 18000, Balance = 12840, Icon = GameIconKind.Lock });
        pages.Children.Add(new GameInfoPopover { Text = "A lock always tells you what is missing. Colour is never the only signal.", IsVisible = true });
        pages.Children.Add(new GameItemRow { Title = "Tidal memory", Cost = 900, Balance = 12840, Icon = GameIconKind.Research });
    }
    private static GameLabel Label(string text, GameTextRole role, GameInk ink = GameInk.Normal) => new() { Text = text, Role = role, Ink = ink };
    private static GameButton Button(GameIconKind icon, string name, Action action)
    {
        var button = new GameButton { Kind = GameButtonKind.Icon, Icon = icon };
        AutomationProperties.SetName(button, name); button.Click += (_, _) => action(); return button;
    }
    private static GameButton Action(string text, GameIconKind icon, Action action, GameButtonKind kind = GameButtonKind.Primary)
    {
        var button = new GameButton { Content = text, Icon = icon, Kind = kind };
        button.Click += (_, _) => action(); return button;
    }
}

internal sealed class CoastIllustration : Control
{
    static CoastIllustration() => AffectsRender<CoastIllustration>(GameUi.ThemeProperty);
    public CoastIllustration() => IsHitTestVisible = false;
    public override void Render(DrawingContext context)
    {
        var p = GameUi.GetTheme(this).Palette;
        using var scale = context.PushTransform(Matrix.CreateScale(Bounds.Width / 320, Bounds.Height / 184));
        IBrush Brush(Color c, double opacity = 1) => new SolidColorBrush(c, opacity);
        context.DrawEllipse(Brush(p.Accent, .06), null, new Point(247, 65), 79, 79);
        context.DrawEllipse(null, new Pen(Brush(p.Accent, .22), .7), new Point(247, 65), 56, 56);
        context.DrawEllipse(null, new Pen(Brush(p.Accent, .12), .7), new Point(247, 65), 67, 67);
        context.DrawEllipse(Brush(p.Legendary), null, new Point(276, 35), 10, 10);
        for (int i = 0; i < 22; i++)
            context.DrawEllipse(Brush(p.Accent, .2 + i % 4 * .1), null, new Point(15 + i * 47 % 290, 10 + i * 31 % 77), .8, .8);
        context.DrawGeometry(Brush(p.Accent, .09), null, Geometry.Parse("M245 52L90 18 90 45Z M253 52L320 35V60Z"));
        context.DrawGeometry(Brush(p.Rim, .45), null, Geometry.Parse("M158 98L183 87 204 92 221 77 258 78 288 103 310 112 300 126 200 126Z"));
        context.DrawGeometry(Brush(p.Background, .8), new Pen(Brush(p.Accent, .35), 1), Geometry.Parse("M192 106L220 94 259 96 278 110 260 119 203 119Z"));
        context.DrawGeometry(Brush(p.Muted), null, Geometry.Parse("M239 55L251 55 255 99 235 99Z"));
        context.DrawGeometry(Brush(p.Rim), null, Geometry.Parse("M247 55L251 55 255 99 247 99Z"));
        context.DrawRectangle(Brush(p.Accent), null, new Rect(236, 47, 18, 9), 2, 2);
        context.DrawGeometry(Brush(p.Legendary), null, Geometry.Parse("M234 46L245 36 256 46Z"));
        context.DrawLine(new Pen(Brush(p.Legendary), 1), new Point(245, 37), new Point(245, 30));
        for (int i = 0; i < 5; i++)
            context.DrawLine(new Pen(Brush(p.Accent, .16 - i * .025), 1), new Point(224 - i * 7, 128 + i * 7), new Point(264 + i * 7, 128 + i * 7));
    }
}
