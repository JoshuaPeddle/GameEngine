using SkiaSharp;

namespace GameEngine.Core
{
    public class Animation
    {
        private readonly SKBitmap texture;
        private readonly int frames;
        private readonly float delay; // Delay between frames in milliseconds
        private readonly int frameWidth;
        private readonly int frameHeight;

        public Animation(SKBitmap texture, int frames, float delayMs)
        {
            this.texture = texture;
            this.frames = frames;
            this.delay = delayMs;
            frameWidth = texture.Width / frames;
            frameHeight = texture.Height;
        }

        public SKBitmap Texture => texture;
        public SKRect GetSourceRect(double elapsedTime)
        {
            int animationFrame = (int)(elapsedTime / delay) % frames;
            int x = animationFrame * frameWidth;
            int y = 0;
            return new SKRect(x, y, x + frameWidth, y + frameHeight);
        }
    }
}
