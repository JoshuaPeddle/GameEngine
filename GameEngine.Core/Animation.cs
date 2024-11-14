using SkiaSharp;

namespace GameEngine.Core
{
      public class Animation
    {
        private readonly string name;
        private readonly SKBitmap texture;
        private readonly int frames;
        private readonly float delay; // Delay between frames in milliseconds

        public Animation(string name, SKBitmap texture, int frames, float delayMs)
        {
            this.name = name;
            this.texture = texture;
            this.frames = frames;
            this.delay = delayMs;
        }

        public SKImage GetCurrentFrame(float elapsedTime)
        {
            using SKBitmap bitmap = CreateBitmap(elapsedTime);
            return SKImage.FromBitmap(bitmap);
        }

        private SKBitmap CreateBitmap(float elapsedTime)
        {
            int animationFrame = (int)(elapsedTime / delay) % frames;

            int frameHeight = texture.Height;
            int frameWidth = texture.Width / frames;
            var bitmap = new SKBitmap(frameWidth, frameHeight);
            using (var canvas = new SKCanvas(bitmap))
            {
                int x = animationFrame * frameWidth;
                int y = 0;
                SKRect sourceRect = new(x, y, x + frameWidth, y + frameHeight);
                SKRect destRect = new(0, 0, frameWidth, frameHeight);
                canvas.DrawBitmap(texture, sourceRect, destRect);
            }
            return bitmap;
        }
    }
}
