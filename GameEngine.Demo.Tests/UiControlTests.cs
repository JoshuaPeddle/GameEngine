using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using GameEngine.UI.Avalonia;
using SkiaSharp;

namespace GameEngine.Demo.Tests;

public sealed class UiControlTests
{
    public static IEnumerable<TestCaseData> Components()
    {
        foreach (bool bright in new[] { false, true })
            foreach (string name in new[] { "primary", "secondary", "icon", "tabs", "card", "row", "counter", "progress", "cooldown", "badge", "toggle", "slider", "sheet", "toast", "popover", "panel" })
                yield return new TestCaseData(bright, name).SetName($"UiRender_{(bright ? "Daybreak" : "NightCoast")}_{name}");
    }
    private static Control Create(string name) => name switch
    {
        "primary" => new GameButton { Content = "Begin expedition", Icon = GameIconKind.Play },
        "secondary" => new GameButton { Content = "Return", Kind = GameButtonKind.Secondary },
        "icon" => new GameButton { Kind = GameButtonKind.Icon, Icon = GameIconKind.Settings },
        "tabs" => new GameTabs { Items = { new GameTab { Header = "One" }, new GameTab { Header = "Two" } } },
        "card" => new GameItemCard { Title = "Prism relay", Description = "Split the light.", Cost = 100, Balance = 200 },
        "row" => new GameItemRow { Title = "Research", Cost = 200, Balance = 100 },
        "counter" => new ResourceCounter { Value = 18420 },
        "progress" => new GameProgress { Value = 65 },
        "cooldown" => new GameCooldown { Maximum = 30, Remaining = 12 },
        "badge" => new GameBadge { Text = "LEGENDARY", Ink = GameInk.Legendary },
        "toggle" => new GameToggle { Content = "Sound", IsChecked = true },
        "slider" => new GameSlider { Value = 60 },
        "sheet" => new GameSheet { Title = "Reward", Body = new GameLabel { Text = "Collected 400 salvage." }, Height = 600 },
        "toast" => new GameToast(),
        "popover" => new GameInfoPopover { Text = "Research continues while away.", IsVisible = true },
        _ => new GamePanel { Ornaments = true, Content = new GameLabel { Text = "The coast awaits." } }
    };
    private static Window Open(Control content, double width = 320)
    {
        var window = new Window { Width = width, Height = 800, Content = content };
        window.Show(); window.UpdateLayout(); return window;
    }
    private static byte[] Render(Control control, int width = 960, int height = 2400)
    {
        using var bitmap = new RenderTargetBitmap(new PixelSize(width, height), new Vector(288, 288));
        bitmap.Render(control);
        using var stream = new MemoryStream(); bitmap.Save(stream, new PngBitmapEncoderOptions()); return stream.ToArray();
    }
    private static void PressKey(Window window, Key key)
    {
        window.KeyPress(key, RawInputModifiers.None, PhysicalKey.None, null);
        window.KeyRelease(key, RawInputModifiers.None, PhysicalKey.None, null);
    }

    [AvaloniaTest]
    [TestCaseSource(nameof(Components))]
    public void EachComponentRendersWithItsThemeAndSize(bool bright, string name)
    {
        var control = Create(name); control.VerticalAlignment = VerticalAlignment.Top;
        var screen = new GameScreen { Child = control };
        GameUi.SetTheme(screen, bright ? GameTheme.Daybreak : GameTheme.NightCoast);
        GameUi.SetReducedMotion(screen, true);
        var window = Open(screen);
        try
        {
            if (control is GameSheet sheet) sheet.Open();
            if (control is GameToast toast) toast.Show("Saved successfully");
            window.UpdateLayout();
            Assert.That(control.Bounds.Width, Is.InRange(48, 320));
            Assert.That(control.Bounds.Height, Is.GreaterThan(15));
            foreach (var target in control.GetVisualDescendants().Prepend(control).OfType<Control>()
                .Where(c => c is GameButton or GameToggle or GameTab or GameSlider))
            {
                Assert.That(target.Bounds.Width, Is.GreaterThanOrEqualTo(48), target.GetType().Name);
                Assert.That(target.Bounds.Height, Is.GreaterThanOrEqualTo(48), target.GetType().Name);
            }
            using var bitmap = SKBitmap.Decode(Render(window));
            Assert.That(bitmap.Pixels.Distinct().Take(30).Count(), Is.EqualTo(30));
            Assert.That(GameUiDiagnostics.ActiveAnimationTimers, Is.Zero);
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public void ButtonsRenderDistinctStatesAndOnlyActivateOnACompletedPress()
    {
        var button = new GameButton { Content = "Launch", VerticalAlignment = VerticalAlignment.Top };
        GameUi.SetReducedMotion(button, true);
        int clicks = 0, feedback = 0;
        button.Click += (_, _) => clicks++;
        GameUi.SetFeedback(button, _ => feedback++);
        var window = Open(button);
        try
        {
            var surface = button.GetVisualDescendants().OfType<GameSurface>().Single();
            var normal = Render(window);
            window.MouseMove(new Point(100, 20));
            Assert.That(surface.State, Is.EqualTo(SurfaceState.Hover));
            var hover = Render(window);
            window.MouseDown(new Point(100, 20), MouseButton.Left);
            Assert.That(button.IsPressed, Is.True);
            Assert.That(surface.State, Is.EqualTo(SurfaceState.Pressed));
            var pressed = Render(window);
            window.MouseUp(new Point(100, 20), MouseButton.Left);
            Assert.That(clicks, Is.EqualTo(1)); Assert.That(feedback, Is.EqualTo(1));
            window.MouseDown(new Point(100, 20), MouseButton.Left); window.MouseUp(new Point(100, 200), MouseButton.Left);
            Assert.That(clicks, Is.EqualTo(1));
            button.IsEnabled = false;
            window.MouseDown(new Point(100, 20), MouseButton.Left); window.MouseUp(new Point(100, 20), MouseButton.Left);
            Assert.That(clicks, Is.EqualTo(1)); Assert.That(surface.State, Is.EqualTo(SurfaceState.Disabled));
            Assert.That(normal.SequenceEqual(hover), Is.False); Assert.That(hover.SequenceEqual(pressed), Is.False); Assert.That(pressed.SequenceEqual(Render(window)), Is.False);
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public void NativeToggleSliderAndTabsSupportKeyboardAndAutomation()
    {
        var toggle = new GameToggle { Content = "Haptics" };
        var slider = new GameSlider { Value = 50, SmallChange = 5 };
        var tabs = new GameTabs { Items = { new GameTab { Header = "Workshop" }, new GameTab { Header = "Research" } } };
        var window = Open(new StackPanel { Children = { toggle, slider, tabs } });
        try
        {
            toggle.Focus(); PressKey(window, Key.Space); Assert.That(toggle.IsChecked, Is.True);
            Assert.That(ControlAutomationPeer.CreatePeerForElement(toggle), Is.InstanceOf<IToggleProvider>());
            slider.Focus(); PressKey(window, Key.Right); Assert.That(slider.Value, Is.EqualTo(55));
            Assert.That(ControlAutomationPeer.CreatePeerForElement(slider), Is.InstanceOf<IRangeValueProvider>());
            var first = (GameTab)tabs.Items[0]!; first.Focus(); PressKey(window, Key.Right);
            Assert.That(tabs.SelectedIndex, Is.EqualTo(1));
            window.MouseDown(new Point(60, 70), MouseButton.Left); window.MouseMove(new Point(200, 70)); window.MouseUp(new Point(200, 70), MouseButton.Left);
            Assert.That(slider.Value, Is.Not.EqualTo(55));
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public void AffordabilityAndLocksAreEnforcedAndExplained()
    {
        var card = new GameItemCard { Title = "Storm lens", Cost = 100, Balance = 90 };
        var window = Open(card);
        try
        {
            Assert.That(card.ActionButton.IsEnabled, Is.False);
            Assert.That(card.GetVisualDescendants().OfType<GameLabel>().Any(l => l.Text == "NEED 10 MORE"), Is.True);
            card.Balance = 100; Assert.That(card.ActionButton.IsEnabled, Is.True);
            card.Locked = true; Assert.That(card.ActionButton.IsEnabled, Is.False);
            Assert.That(card.ActionButton.Icon, Is.EqualTo(GameIconKind.Lock));
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public void LargeTextWrapsAndSafeAreaInsetsAreApplied()
    {
        var button = new GameButton { Content = "Begin the next expedition", Icon = GameIconKind.Play };
        var label = new GameLabel { Text = "Keepers of the coast" };
        var panel = new StackPanel { Children = { label, button } };
        var screen = new GameScreen { SafeInsets = new Thickness(12, 30, 12, 20), Child = panel };
        var window = Open(screen);
        try
        {
            double height = label.Bounds.Height;
            GameUi.SetTextScale(screen, 2); window.UpdateLayout();
            Assert.That(label.FontSize, Is.EqualTo(32)); Assert.That(label.Bounds.Height, Is.GreaterThan(height));
            Assert.That(panel.Bounds.X, Is.EqualTo(12)); Assert.That(panel.Bounds.Y, Is.EqualTo(30));
            Assert.That(button.Bounds.Width, Is.EqualTo(296)); Assert.That(button.Bounds.Height, Is.GreaterThan(48));
            foreach (var text in button.GetVisualDescendants().OfType<TextBlock>())
                Assert.That(text.Bounds.Width, Is.LessThanOrEqualTo(296));
            Assert.DoesNotThrow(() => Render(window));
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public void SheetTrapsFocusDismissesOnEscapeAndRestoresTheInvoker()
    {
        var button = new GameButton { Content = "Open" };
        var sheet = new GameSheet { Title = "Reward", Body = new GameButton { Content = "Collect" } };
        var root = new Grid { Children = { button, sheet } }; GameUi.SetReducedMotion(root, true);
        var window = Open(root);
        try
        {
            button.Focus(); sheet.Open(); window.UpdateLayout();
            Assert.That(sheet.IsOpen, Is.True); Assert.That(sheet.HasActiveMotion, Is.False);
            for (int i = 0; i < 5; i++)
            {
                PressKey(window, Key.Tab);
                var focused = window.FocusManager!.GetFocusedElement() as Control;
                Assert.That(focused, Is.Not.SameAs(button));
                Assert.That(focused!.GetVisualAncestors(), Does.Contain(sheet));
            }
            PressKey(window, Key.Escape);
            Assert.That(sheet.IsVisible, Is.False); Assert.That(button.IsFocused, Is.True);
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public async Task AnimationFinishesAndIdleSchedulesNoTicks()
    {
        var counter = new ResourceCounter();
        GameUi.SetTheme(counter, GameTheme.NightCoast with { Motion = new GameMotion(Counter: .04) });
        var window = Open(counter);
        try
        {
            counter.Value = 5000; Assert.That(counter.HasActiveMotion, Is.True);
            await Task.Delay(160);
            Assert.That(counter.DisplayText, Is.EqualTo("5K")); Assert.That(counter.HasActiveMotion, Is.False);
            Assert.That(GameUiDiagnostics.ActiveAnimationTimers, Is.Zero);
            long ticks = GameUiDiagnostics.AnimationTicks; await Task.Delay(120);
            Assert.That(GameUiDiagnostics.AnimationTicks, Is.EqualTo(ticks));
            counter.Value = 7000; GameUi.SetReducedMotion(counter, true);
            Assert.That(counter.DisplayText, Is.EqualTo("7K")); Assert.That(counter.HasActiveMotion, Is.False);
            GameUi.SetReducedMotion(counter, false); counter.Value = 9000; window.Content = null;
            Assert.That(counter.HasActiveMotion, Is.False); Assert.That(GameUiDiagnostics.ActiveAnimationTimers, Is.Zero);
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public async Task ToastExpiresAndPopoverCanBeDismissed()
    {
        var toast = new GameToast(); var popover = new GameInfoPopover { Text = "Saved", Focusable = true };
        var panel = new StackPanel { Children = { toast, popover } }; GameUi.SetReducedMotion(panel, true);
        var window = Open(panel);
        try
        {
            toast.Show("Saved", .04); Assert.That(toast.IsVisible, Is.True);
            await Task.Delay(120); Assert.That(toast.IsVisible, Is.False); Assert.That(toast.HasActiveMotion, Is.False);
            popover.Toggle(); popover.Focus(); PressKey(window, Key.Escape); Assert.That(popover.IsVisible, Is.False);
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public void FontsAreEmbeddedAndCounterDigitsHaveEqualAdvances()
    {
        Assert.That(FontManager.Current.TryGetGlyphTypeface(new Typeface(GameFonts.Display), out var display), Is.True);
        Assert.That(FontManager.Current.TryGetGlyphTypeface(new Typeface(GameFonts.Text), out var text), Is.True);
        Assert.That(display!.FamilyName, Is.EqualTo("Oxanium Medium")); Assert.That(text!.FamilyName, Is.EqualTo("Atkinson Hyperlegible"));
        var widths = "0123456789".Select(d => new FormattedText(d.ToString(), System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, new Typeface(GameFonts.Display), 24, Brushes.White).Width).ToArray();
        Assert.That(widths.Max() - widths.Min(), Is.LessThan(.01));
    }

    [TestCase(999, "999")]
    [TestCase(999999, "1M")]
    [TestCase(-12500, "-12.5K")]
    [TestCase(1e24, "1e+24")]
    public void BigNumbersUseStableSuffixes(double value, string expected) => Assert.That(ResourceCounter.Format(value), Is.EqualTo(expected));

    [Test]
    public void TweenHonoursSecondsRetargetingAndReducedMotion()
    {
        var tween = new GameTween(); tween.Start(10, 20, .2); tween.Advance(.1);
        Assert.That(tween.Value, Is.InRange(10.1, 19.9)); tween.Advance(.1);
        Assert.That(tween.Value, Is.EqualTo(20)); Assert.That(tween.IsActive, Is.False);
        Assert.That(tween.Advance(.1), Is.False);
        tween.Start(20, 30, .2, true); Assert.That(tween.Value, Is.EqualTo(30)); Assert.That(tween.IsActive, Is.False);
        Assert.Throws<ArgumentOutOfRangeException>(() => tween.Advance(double.NaN));
    }

    [AvaloniaTest]
    public void CooldownUsesSimulationSecondsEvenWithReducedMotion()
    {
        var cooldown = new GameCooldown(); GameUi.SetReducedMotion(cooldown, true);
        cooldown.Start(4); cooldown.Advance(1.5);
        Assert.That(cooldown.Remaining, Is.EqualTo(2.5)); Assert.That(cooldown.Value, Is.EqualTo(2.5));
        cooldown.Advance(5); Assert.That(cooldown.Label, Is.EqualTo("READY"));
    }

    [AvaloniaTest]
    public void ScrollingWorksWithoutAnApplicationControlTheme()
    {
        var scroll = new GameScrollViewer { Content = new GamePanel { Height = 1600 } };
        var window = Open(scroll);
        try
        {
            Assert.That(scroll.Extent.Height, Is.GreaterThan(scroll.Viewport.Height));
            scroll.ScrollToEnd(); window.UpdateLayout();
            Assert.That(scroll.Offset.Y, Is.GreaterThan(0));
            scroll.Offset = default;
            window.MouseWheel(new Point(150, 100), new Vector(0, -3)); window.UpdateLayout();
            Assert.That(scroll.Offset.Y, Is.GreaterThan(0));
        }
        finally { window.Close(); }
    }
}
