using SkiaSharp;

namespace GameEngine.Core.Components
{
    public class CText : Component
    {
        public string Text { get; set; }
        public float Size => Paint.TextSize;

        public CText() { }

        public CText(string text, int size)
        {
            Text = text;
            Paint.TextSize = size;
        }

        public SKPaint Paint { get; set; } = new SKPaint
        {
            Color = SKColors.Black,
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            TextAlign = SKTextAlign.Center,
            TextSize = 24
        };

        public bool ShouldDraw { get; set; } = true;
    }
}
