using SkiaSharp;

namespace GameEngine.Core
{
    public class Animation
    {
        public SKBitmap texture;
        public int frames;
        public float delay;
        public int frameWidth;
        public int frameHeight;
        public SKRect[]? _cachedFrames;

        private readonly double frameDurationSeconds;

        public Animation(SKBitmap texture, int frames, float delayMs)
        {
            this.texture = texture;
            this.frames = frames;
            this.delay = delayMs;
            frameDurationSeconds = delayMs / 1000.0;
            frameWidth = texture.Width / frames;
            frameHeight = texture.Height;
        }

        public SKRect[] InitializeFrames(int frames)
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

        public SKRect GetSourceRect(double elapsedSeconds)
        {
            _cachedFrames ??= InitializeFrames(frames);

            if (frameDurationSeconds <= 0)
                return _cachedFrames[0];

            int frameIndex = (int)(elapsedSeconds / frameDurationSeconds) % frames;
            return _cachedFrames[frameIndex]; // No allocation!
        }

        public ScaledAnimation AsScaledAnimation(Vec2 scaleSize)
        {
            return new ScaledAnimation(texture, frames, delay, scaleSize);
        }
    }

    public class ScaledAnimation : Animation
    {
        private readonly Vec2 _scaleSize;

        public ScaledAnimation(SKBitmap texture, int frames, float delayMs, Vec2 scaleSize)
            : base(texture, frames, delayMs)
        {
            _scaleSize = scaleSize;
            RescaleTexture();
        }
        private void RescaleTexture()
        {
            var scaledBitmap = texture.Resize(new SKImageInfo((int)_scaleSize.X, (int)_scaleSize.Y), SKFilterQuality.High);
            texture = scaledBitmap;
            frameWidth = texture.Width / frames;
            frameHeight = texture.Height;
        }
    }
}
