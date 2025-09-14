using SkiaSharp;

namespace GameEngine.Core.Components
{
    public class CText : Component, IDisposable
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

        public void Dispose()
        {
            Paint.Dispose();
            Paint = null;
            GC.SuppressFinalize(this);
        }
    }
}
