using SkiaSharp;

namespace GameEngine.Core.UI;

public sealed class UiPanel(SKRect bounds, int layer = 120)
{
    public SKRect Bounds { get; set; } = bounds;
    public int Layer { get; } = layer;
    public bool Visible { get; set; } = true;
    public bool Modal { get; set; }
    public bool ConsumePointer { get; set; } = true;
}

public sealed class UiButton(SKRect bounds, string caption, Action action)
{
    public SKRect Bounds { get; set; } = bounds;
    public string Caption { get; set; } = caption;
    public Action Action { get; set; } = action;
    public bool Visible { get; set; } = true;
    public bool Enabled { get; set; } = true;
    public float FontSize { get; set; } = 12;
    public float Padding { get; set; } = 6;
    public SKColor Color { get; set; } = SKColors.White;
    public SKColor DisabledColor { get; set; } = SKColors.Gray;
}

public sealed class UiTextBlock(SKRect bounds, int lines)
{
    public SKRect Bounds { get; set; } = bounds;
    public int Lines { get; } = lines;
    public string Text { get; set; } = "";
    public bool Visible { get; set; } = true;
    public float FontSize { get; set; } = 12;
    public SKColor Color { get; set; } = SKColors.White;
}
