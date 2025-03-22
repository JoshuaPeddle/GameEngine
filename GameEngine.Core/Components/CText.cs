using SkiaSharp;

namespace GameEngine.Core.Components
{
    public class CText : Component
    {
        public string Text { get; set; }
        public int Size { get; set; }

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
            Style = SKPaintStyle.Fill,
            TextAlign = SKTextAlign.Center,
            TextSize = 24
        };
    }
}
