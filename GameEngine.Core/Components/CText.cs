using SkiaSharp;

namespace GameEngine.Core.Components
{
    public class CText : Component
    {
        public CText(string text, int size)
        {
            Text = text;
            Size = size;
        }

        public string Text { get; }
        public int Size { get; }
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
