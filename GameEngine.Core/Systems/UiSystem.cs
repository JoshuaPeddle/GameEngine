using GameEngine.Core.Components;
using GameEngine.Core.UI;
using SkiaSharp;

namespace GameEngine.Core.Systems;

public sealed class UiSystem
{
    private readonly record struct ButtonState(SKRect Bounds, string Caption, float Size, float Padding, SKColor Color, bool Visible);
    private readonly record struct TextState(SKRect Bounds, string Text, float Size, SKColor Color, bool Visible);
    private sealed record ButtonView(UiPanel Panel, UiButton Button, Entity Sprite, Entity Label, Animation Background)
    {
        public ButtonState? State;
    }
    private sealed record TextView(UiPanel Panel, UiTextBlock Block, Entity[] Labels)
    {
        public TextState? State;
    }
    private readonly List<UiPanel> panels = [];
    private readonly List<ButtonView> buttons = [];
    private readonly List<TextView> texts = [];
    private readonly EntityManager entities;

    public UiSystem(EntityManager entities) => this.entities = entities;

    public UiPanel AddPanel(SKRect bounds, int layer = 120)
    {
        UiLayout.Validate(bounds);
        if (panels.Any(p => p.Layer == layer)) throw new ArgumentException("Each UI panel needs a distinct layer.", nameof(layer));
        var panel = new UiPanel(bounds, layer);
        panels.Add(panel);
        return panel;
    }

    public UiButton AddButton(UiPanel panel, string tag, SKRect bounds, string caption, Animation background, Action action)
    {
        RequirePanel(panel);
        var sprite = entities.CreateEntity(tag);
        sprite.AddComponent(new CTransform(Vec2.Zero));
        sprite.AddComponent(new CAnimation(background));
        var label = Label(tag + "Text");
        var button = new UiButton(bounds, caption, action);
        buttons.Add(new ButtonView(panel, button, sprite, label, background));
        return button;
    }

    public UiTextBlock AddText(UiPanel panel, string tag, SKRect bounds, int lines)
    {
        RequirePanel(panel);
        if (lines <= 0) throw new ArgumentOutOfRangeException(nameof(lines));
        var block = new UiTextBlock(bounds, lines);
        texts.Add(new TextView(panel, block, Enumerable.Range(0, lines).Select(i => Label(tag + i)).ToArray()));
        return block;
    }

    private void RequirePanel(UiPanel panel)
    {
        if (!panels.Contains(panel)) throw new ArgumentException("The panel belongs to a different UI system.", nameof(panel));
    }

    private Entity Label(string tag)
    {
        var entity = entities.CreateEntity(tag);
        entity.AddComponent(new CTransform(Vec2.Zero));
        entity.AddComponent(new CText());
        return entity;
    }

    private static void ValidateChild(UiPanel panel, SKRect bounds, float fontSize)
    {
        UiLayout.Validate(panel.Bounds);
        UiLayout.Validate(bounds);
        if (!panel.Bounds.Contains(bounds)) throw new ArgumentException("UI controls must fit inside their panel.", nameof(bounds));
        if (!float.IsFinite(fontSize) || fontSize <= 0) throw new ArgumentOutOfRangeException(nameof(fontSize));
    }

    public bool TryPress(Vec2 point)
    {
        Update();
        foreach (var panel in panels.Select((panel, index) => (panel, index)).OrderByDescending(p => p.panel.Layer).ThenByDescending(p => p.index).Select(p => p.panel))
        {
            if (!panel.Visible) continue;
            if (panel.Bounds.Contains((float)point.X, (float)point.Y))
            {
                foreach (var view in buttons.Where(b => b.Panel == panel).Reverse())
                {
                    var button = view.Button;
                    if (!button.Visible || !button.Bounds.Contains((float)point.X, (float)point.Y)) continue;
                    if (button.Enabled) button.Action();
                    return true;
                }
                if (panel.ConsumePointer) return true;
            }
            if (panel.Modal) return true;
        }
        return false;
    }

    public void Update()
    {
        foreach (var view in buttons)
        {
            var button = view.Button;
            var bounds = button.Bounds;
            var state = new ButtonState(bounds, button.Caption, button.FontSize, button.Padding,
                button.Enabled ? button.Color : button.DisabledColor, view.Panel.Visible && button.Visible);
            ValidateChild(view.Panel, bounds, button.FontSize);
            if (state == view.State) continue;
            if (!float.IsFinite(button.Padding) || button.Padding < 0) throw new ArgumentOutOfRangeException(nameof(button.Padding));
            var sprite = view.Sprite.GetComponent<CAnimation>();
            var label = view.Label.GetComponent<CText>();
            sprite.ShouldDraw = label.ShouldDraw = view.Panel.Visible && button.Visible;
            var transform = view.Sprite.GetComponent<CTransform>();
            transform.Position = new Vec2(bounds.MidX, bounds.MidY);
            transform.Scale = new Vec2(bounds.Width / view.Background.DrawFrameSize.X, bounds.Height / view.Background.DrawFrameSize.Y);
            transform.Layer = view.Panel.Layer;
            label.Text = UiText.FitLine(button.Caption, button.FontSize, bounds.Width - button.Padding * 2);
            label.Size = button.FontSize;
            label.Paint.Color = button.Enabled ? button.Color : button.DisabledColor;
            label.TextAlign = SKTextAlign.Center;
            using var font = new SKFont(SKTypeface.Default, button.FontSize);
            var textTransform = view.Label.GetComponent<CTransform>();
            textTransform.Position = new Vec2(bounds.MidX, bounds.MidY - (font.Metrics.Ascent + font.Metrics.Descent) / 2);
            textTransform.Layer = view.Panel.Layer;
            view.State = state;
        }
        foreach (var view in texts)
        {
            var block = view.Block;
            var state = new TextState(block.Bounds, block.Text, block.FontSize, block.Color, view.Panel.Visible && block.Visible);
            ValidateChild(view.Panel, block.Bounds, block.FontSize);
            if (state == view.State) continue;
            var lines = UiText.Wrap(block.Text, block.FontSize, block.Bounds.Width, block.Lines);
            using var font = new SKFont(SKTypeface.Default, block.FontSize);
            float spacing = block.Bounds.Height / block.Lines;
            if (font.Metrics.Descent - font.Metrics.Ascent > spacing)
                throw new ArgumentException("The text block needs enough height for its font and line count.");
            for (int i = 0; i < view.Labels.Length; i++)
            {
                var label = view.Labels[i].GetComponent<CText>();
                label.ShouldDraw = view.Panel.Visible && block.Visible;
                label.Text = i < lines.Count ? lines[i] : "";
                label.Size = block.FontSize;
                label.TextAlign = SKTextAlign.Left;
                label.Paint.Color = block.Color;
                var transform = view.Labels[i].GetComponent<CTransform>();
                transform.Position = new Vec2(block.Bounds.Left, block.Bounds.Top - font.Metrics.Ascent + i * spacing);
                transform.Layer = view.Panel.Layer;
            }
            view.State = state;
        }
    }
}
