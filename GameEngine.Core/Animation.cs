using SkiaSharp;

namespace GameEngine.Core
{
    public class Animation : IDisposable
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

        // An animation shares the manifest's texture and does not own it: disposing one
        // because a single entity was removed would pull the bitmap out from under every
        // other entity drawn from it, and from any render snapshot still being painted. Only
        // a ScaledAnimation, which decodes a bitmap of its own, has anything to release.
        protected virtual void Dispose(bool disposing) { }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
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
            bool isDownscaling = _scaleSize.X < texture.Width || _scaleSize.Y < texture.Height;
            SKSamplingOptions sampling = isDownscaling
                ? new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear)
                : new SKSamplingOptions(SKCubicResampler.Mitchell);
            var scaledBitmap = texture.Resize(
                new SKImageInfo((int)_scaleSize.X, (int)_scaleSize.Y),
                sampling);
            texture = scaledBitmap;
            frameWidth = texture.Width / frames;
            frameHeight = texture.Height;
        }

        // This one owns its bitmap: RescaleTexture decoded it rather than borrowing it from
        // the manifest.
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                texture.Dispose();

            base.Dispose(disposing);
        }
    }
}
