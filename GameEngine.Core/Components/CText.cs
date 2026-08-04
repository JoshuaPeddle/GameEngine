using SkiaSharp;

namespace GameEngine.Core.Components
{
    public class CText : Component
    {
        public string Text { get; set; } = string.Empty;
        public float Size { get; set; } = 24;
        public SKTextAlign TextAlign { get; set; } = SKTextAlign.Center;

        public CText() { }

        public CText(string text, int size)
        {
            Text = text;
            Size = size;
        }

        public SKPaint Paint { get; set; } = new SKPaint
        {
            Color = SKColors.Black,
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        public bool ShouldDraw { get; set; } = true;
    }
}
