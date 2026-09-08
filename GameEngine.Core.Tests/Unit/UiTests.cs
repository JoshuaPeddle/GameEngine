using GameEngine.Core.Components;
using GameEngine.Core.Systems;
using GameEngine.Core.UI;
using SkiaSharp;

namespace GameEngine.Core.Tests.Unit;

public class UiTests
{
    private static Animation Background(SKBitmap bitmap) => new(bitmap, 1, 0);

    [Test]
    public void RowsColumnsAndPaddingShareExactEdges()
    {
        var bounds = UiLayout.Inset(new SKRect(0, 0, 244, 82), 10);
        var rows = UiLayout.Column(bounds, 2, 6);
        var buttons = UiLayout.Row(rows[0], 2, 8);
        Assert.Multiple(() =>
        {
            Assert.That(rows[1], Is.EqualTo(new SKRect(10, 44, 234, 72)));
            Assert.That(buttons[0], Is.EqualTo(new SKRect(10, 10, 118, 38)));
            Assert.That(buttons[1], Is.EqualTo(new SKRect(126, 10, 234, 38)));
        });
    }

    [Test]
    public void HiddenAndDisabledButtonsHaveOneVisualAndInputState()
    {
        var entities = new EntityManager();
        var ui = new UiSystem(entities);
        var panel = ui.AddPanel(new SKRect(0, 0, 100, 100));
        panel.ConsumePointer = false;
        using var bitmap = new SKBitmap(2, 2);
        int clicked = 0;
        var button = ui.AddButton(panel, "button", new SKRect(10, 10, 90, 40), "Make", Background(bitmap), () => clicked++);
        ui.Update();
        Assert.That(ui.TryPress(new Vec2(20, 20)), Is.True);
        Assert.That(clicked, Is.EqualTo(1));
        button.Visible = false;
        ui.Update();
        entities.Update();
        Assert.Multiple(() =>
        {
            Assert.That(ui.TryPress(new Vec2(20, 20)), Is.False);
            Assert.That(entities.GetEntityWithTag("button")!.GetComponent<CAnimation>().ShouldDraw, Is.False);
            Assert.That(entities.GetEntityWithTag("buttonText")!.GetComponent<CText>().ShouldDraw, Is.False);
        });
        button.Visible = true;
        button.Enabled = false;
        ui.Update();
        Assert.That(ui.TryPress(new Vec2(20, 20)), Is.True);
        Assert.That(clicked, Is.EqualTo(1));
        Assert.That(entities.GetEntityWithTag("buttonText")!.GetComponent<CText>().Paint.Color, Is.EqualTo(button.DisabledColor));
    }

    [Test]
    public void ModalPanelBlocksUnderlyingButtonsAndWorldClicksOutsideItsBounds()
    {
        var entities = new EntityManager();
        var ui = new UiSystem(entities);
        using var bitmap = new SKBitmap(2, 2);
        var lower = ui.AddPanel(new SKRect(0, 0, 100, 100), 10);
        int clicked = 0;
        ui.AddButton(lower, "lower", new SKRect(10, 10, 90, 40), "Lower", Background(bitmap), () => clicked++);
        var modal = ui.AddPanel(new SKRect(40, 40, 80, 80), 20);
        modal.Modal = true;
        ui.Update();
        Assert.That(ui.TryPress(new Vec2(20, 20)), Is.True);
        Assert.That(ui.TryPress(new Vec2(150, 150)), Is.True);
        Assert.That(clicked, Is.Zero);
        modal.Visible = false;
        ui.Update();
        Assert.That(ui.TryPress(new Vec2(20, 20)), Is.True);
        Assert.That(clicked, Is.EqualTo(1));
        Assert.That(ui.TryPress(new Vec2(150, 150)), Is.False);
    }

    [Test]
    public void OverlappingButtonsUseTheSameOrderForDrawingAndInput()
    {
        var entities = new EntityManager();
        var ui = new UiSystem(entities);
        var panel = ui.AddPanel(new SKRect(0, 0, 100, 100));
        using var red = new SKBitmap(2, 2);
        using var blue = new SKBitmap(2, 2);
        red.Erase(SKColors.Red);
        blue.Erase(SKColors.Blue);
        string clicked = "";
        ui.AddButton(panel, "red", new SKRect(10, 10, 90, 50), "Lower", Background(red), () => clicked = "red");
        ui.AddButton(panel, "blue", new SKRect(10, 10, 90, 50), "", Background(blue), () => clicked = "blue");
        ui.Update();
        entities.Update();
        var snapshot = new RenderSnapshot();
        entities.BuildRenderSnapshot(snapshot);
        using var image = new SKBitmap(100, 100);
        using var canvas = new SKCanvas(image);
        new RenderSystem(new RenderOptions { VirtualWidth = 100, VirtualHeight = 100, DrawBoundingBoxes = false }).DrawEntitiesToCanvas(canvas, snapshot);
        Assert.That(image.GetPixel(50, 30), Is.EqualTo(SKColors.Blue));
        Assert.That(ui.TryPress(new Vec2(50, 30)), Is.True);
        Assert.That(clicked, Is.EqualTo("blue"));
    }

    [Test]
    public void MovingAButtonUpdatesItsVisualGeometryAndHitAreaTogether()
    {
        var entities = new EntityManager();
        var ui = new UiSystem(entities);
        var panel = ui.AddPanel(new SKRect(0, 0, 200, 100));
        panel.ConsumePointer = false;
        using var bitmap = new SKBitmap(2, 2);
        var button = ui.AddButton(panel, "moving", new SKRect(0, 0, 50, 30), "Move", Background(bitmap), () => { });
        ui.Update();
        button.Bounds = new SKRect(100, 0, 200, 40);
        ui.Update();
        entities.Update();
        Assert.That(ui.TryPress(new Vec2(25, 15)), Is.False);
        Assert.That(ui.TryPress(new Vec2(150, 20)), Is.True);
        var transform = entities.GetEntityWithTag("moving")!.GetComponent<CTransform>();
        Assert.That(transform.Position, Is.EqualTo(new Vec2(150, 20)));
        Assert.That(transform.Scale, Is.EqualTo(new Vec2(50, 20)));
    }

    [Test]
    public void TextWrapUsesGlyphWidthsAndEllipsizesOverflow()
    {
        using var font = new SKFont(SKTypeface.Default, 14);
        float width = font.MeasureText("iiiiiiii");
        var narrow = UiText.Wrap("iiiiiiii", 14, width, 3);
        var wide = UiText.Wrap("WWWWWWWW", 14, width, 3);
        Assert.That(narrow, Has.Count.EqualTo(1));
        Assert.That(wide, Has.Count.GreaterThan(1));
        Assert.That(wide[^1], Does.EndWith("…"));
        foreach (string line in wide) Assert.That(font.MeasureText(line), Is.LessThanOrEqualTo(width));
        Assert.That(UiText.Wrap("one\n\ntwo", 14, 100, 3), Is.EqualTo(new[] { "one", "", "two" }));
        Assert.That(UiText.FitLine("Long recipe description", 14, 60), Does.EndWith("…"));
    }
}
