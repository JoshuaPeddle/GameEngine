using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using GameEngine.UI.Avalonia;

namespace GameEngine.Demo.Tests;

public sealed class UiLayoutTests
{
    private static Window Open(Control content)
    {
        var window = new Window { Width = 360, Height = 400, Content = content };
        window.Show(); window.UpdateLayout(); return window;
    }

    private static Rect BoundsIn(Visual visual, Visual ancestor) =>
        new(visual.TranslatePoint(default, ancestor)!.Value, visual.Bounds.Size);

    private static void AssertCentered(Control content, GamePanel panel, string name)
    {
        var block = BoundsIn(content, panel);
        double top = block.Top - panel.Padding.Top, bottom = panel.Bounds.Height - panel.Padding.Bottom - block.Bottom;
        Assert.That(Math.Abs(top - bottom), Is.LessThanOrEqualTo(1), $"{name}: {top:0.##} above, {bottom:0.##} below");
    }

    private static Grid Row(params Control[] cells)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions(string.Join(",", cells.Select(_ => "*"))), ColumnSpacing = 4, VerticalAlignment = VerticalAlignment.Top };
        for (int i = 0; i < cells.Length; i++) { Grid.SetColumn(cells[i], i); row.Children.Add(cells[i]); }
        return row;
    }

    [AvaloniaTest]
    public void PanelsInAStretchedRowCenterShorterBlocks()
    {
        var caption = new GameLabel { Role = GameTextRole.Caption, Text = "WAVE" };
        var title = new GameLabel { Role = GameTextRole.Title, Text = "19" };
        var tall = new StackPanel { Children = { new GameLabel { Role = GameTextRole.Caption, Text = "HULL" }, new GameLabel { Role = GameTextRole.Display, Text = "20" } } };
        var short1 = new GamePanel { Content = caption };
        var short2 = new GamePanel { Padding = new Thickness(8, 6), Content = title };
        var high = new GamePanel { Padding = new Thickness(8, 6), Content = tall };
        var window = Open(Row(short1, short2, high));
        try
        {
            Assert.That(short1.Bounds.Height, Is.EqualTo(high.Bounds.Height), "The row stretches every cell");
            Assert.That(short1.Bounds.Height, Is.GreaterThan(caption.Bounds.Height + short1.Padding.Top + short1.Padding.Bottom + 4));
            AssertCentered(caption, short1, "caption");
            AssertCentered(title, short2, "title");
            AssertCentered(tall, high, "tall block");
            var top = new GamePanel { VerticalContentAlignment = VerticalAlignment.Top, Content = new GameLabel { Text = "top" } };
            window.Content = Row(top, new GamePanel { Height = 120 });
            window.UpdateLayout();
            Assert.That(BoundsIn((Control)top.Content!, top).Top, Is.EqualTo(top.Padding.Top).Within(.5), "An explicit alignment still wins");
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public async Task CounterRollStaysInsideItsLineBoxInAMixedRow()
    {
        var counter = new ResourceCounter { Label = "CREDITS" };
        GameUi.SetTheme(counter, GameTheme.NightCoast with { Motion = new GameMotion(Counter: .3) });
        var wave = new GamePanel { Content = new StackPanel { Children = { new GameLabel { Role = GameTextRole.Caption, Text = "WAVE" }, new GameLabel { Role = GameTextRole.Display, Text = "26" } } } };
        var window = Open(Row(counter, wave));
        try
        {
            var labels = counter.GetVisualDescendants().OfType<GameLabel>().ToArray();
            var caption = labels.Single(l => l.Role == GameTextRole.Caption);
            var number = labels.Single(l => l.Role == GameTextRole.Title);
            var settledNumber = BoundsIn(number, counter);
            var settledCaption = BoundsIn(caption, counter);
            AssertCentered((Control)counter.Content!, counter, "counter block");
            counter.Value = 16300;
            int samples = 0;
            while (counter.HasActiveMotion && samples < 40)
            {
                await Task.Delay(10); window.UpdateLayout(); samples++;
                var visual = number.RenderTransform?.Value is { } m ? settledNumber.TransformToAABB(m) : settledNumber;
                Assert.That(BoundsIn(number, counter), Is.EqualTo(settledNumber), "The number keeps its layout slot");
                Assert.That(visual, Is.EqualTo(settledNumber), "The number is not drawn outside its line box");
                Assert.That(visual.Top, Is.GreaterThanOrEqualTo(settledCaption.Bottom), "The number never overlaps the caption");
            }
            Assert.That(samples, Is.GreaterThan(2), "The roll was sampled while it ran");
            await Task.Delay(400);
            Assert.That(counter.HasActiveMotion, Is.False);
            Assert.That(number.Opacity, Is.EqualTo(1));
            Assert.That(counter.DisplayText, Is.EqualTo("16.3K"));
        }
        finally { window.Close(); }
    }
}
