using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Avalonia.Threading;
using GameEngine.UI.Avalonia;

namespace GameEngine.Demo.Tests;

public sealed class CustomIconTests
{
    private static readonly GameIconSource Source = GameIconSource.FromPath("M12 3L21 19H3Z M12 9V14", 24);

    [AvaloniaTest]
    public void SourceFlowsThroughEveryIconHostAndCanReturnToBuiltIns()
    {
        var card = new GameItemCard { IconSource = Source, IconForeground = Brushes.Coral };
        var row = new GameItemRow { IconSource = Source };
        var button = new GameButton { IconSource = Source, Content = "Custom" };
        var badge = new GameBadge { IconSource = Source };
        var counter = new ResourceCounter { IconSource = Source };
        var panel = new StackPanel { Children = { card, row, button, badge, counter } };
        var window = new Window { Content = panel, Width = 360, Height = 800 };
        window.Show(); window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
        try
        {
            foreach (var host in new Control[] { card, row, button, badge, counter })
                Assert.That(host.GetVisualDescendants().OfType<GameIcon>().Count(i => ReferenceEquals(i.Source, Source)), Is.EqualTo(1), host.GetType().Name);
            Assert.That(card.GetVisualDescendants().OfType<GameIcon>().First().Foreground, Is.SameAs(Brushes.Coral));
            button.IconSource = null; button.Icon = GameIconKind.Play;
            var glyph = button.GetVisualDescendants().OfType<GameIcon>().Single();
            Assert.That(glyph.Source, Is.Null); Assert.That(glyph.Kind, Is.EqualTo(GameIconKind.Play)); Assert.That(glyph.IsVisible, Is.True);
            badge.IconSource = null;
            Assert.That(badge.GetVisualDescendants().OfType<GameIcon>().Single().IsVisible, Is.False);
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public void SharedVectorRendersAtMultipleSizesAndRespondsToThemeAndTint()
    {
        var icon = new GameIcon { Source = Source, Width = 48, Height = 48 };
        var window = new Window { Content = icon, Width = 72, Height = 48 };
        window.Show(); window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
        byte[] Render(int density)
        {
            using var bitmap = new RenderTargetBitmap(new PixelSize(72 * density, 48 * density), new Vector(96 * density, 96 * density));
            bitmap.Render(window);
            using var stream = new MemoryStream(); bitmap.Save(stream, new PngBitmapEncoderOptions()); return stream.ToArray();
        }
        try
        {
            var night = Render(1);
            Assert.That(Render(3).Length, Is.GreaterThan(night.Length));
            GameUi.SetTheme(window, GameTheme.Daybreak with { Stroke = 2, Glow = 0 });
            Assert.That(Render(1), Is.Not.EqualTo(night));
            icon.Foreground = Brushes.Coral;
            var tinted = Render(1);
            Assert.That(tinted, Is.Not.EqualTo(night));
            Assert.That(Render(1), Is.EqualTo(tinted));
            Assert.That(icon.Source, Is.SameAs(Source));
            Assert.That(GameUiDiagnostics.ActiveAnimationTimers, Is.Zero);
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public void InvalidPathAndViewportFailAtCreation()
    {
        Assert.Throws<ArgumentException>(() => GameIconSource.FromPath(""));
        Assert.Throws<ArgumentOutOfRangeException>(() => GameIconSource.FromPath("M1 1L2 2", 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => GameIconSource.FromPath("M1 1L2 2", double.NaN));
        Assert.Catch(() => GameIconSource.FromPath("not a path"));
    }
}
