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
        private SKRect[]? _cachedFrames;

        public Animation(SKBitmap texture, int frames, float delayMs)
        {
            this.texture = texture;
            this.frames = frames;
            this.delay = delayMs;
            frameWidth = texture.Width / frames;
            frameHeight = texture.Height;
        }

        private SKRect[] InitializeFrames(int frames)
        {
            _cachedFrames = new SKRect[frames];
            for (int i = 0; i < frames; i++)
            {
                int x = i * frameWidth;
                _cachedFrames[i] = new SKRect(x, 0, x + frameWidth, frameHeight);
            }
            return _cachedFrames;
        }

        public SKBitmap Texture => texture;

        public SKRect GetSourceRect(double elapsedTime)
        {
            _cachedFrames ??= InitializeFrames(frames);
            int frameIndex = (int)(elapsedTime / delay) % frames;
            return _cachedFrames[frameIndex]; // No allocation!
        }
    }
}
