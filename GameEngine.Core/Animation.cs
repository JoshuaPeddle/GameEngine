using SkiaSharp;


namespace GameEngine.Core
{
    public class Animation
    {
        private string name;
        private SKBitmap texture;
        private int frames;
        private float delay; // Delay between frames in milliseconds
        private float elapsedTime; // Time elapsed since the last frame change

        public Animation(string name, SKBitmap texture, int frames, float delayMs)
        {
            this.name = name;
            this.texture = texture;
            this.frames = frames;
            this.delay = delayMs; 
            this.elapsedTime = 0f;
        }

        public void Update(float deltaTime)
        {
            elapsedTime += deltaTime;
        }

        public SKImage GetCurrentFrame()
        {
            using SKBitmap bitmap = CreateBitmap();

            return SKImage.FromBitmap(bitmap);
        }

        private SKBitmap CreateBitmap()
        {
            int frameWidth = texture.Width / frames;
            int frameHeight = texture.Height;

            int animationFrame = (int)(elapsedTime / delay) % frames;

            int x = animationFrame * frameWidth;
            int y = 0;
            var bitmap = new SKBitmap(frameWidth, frameHeight);
            using (var canvas = new SKCanvas(bitmap))
            {
                SKRect sourceRect = new SKRect(x, y, x + frameWidth, y + frameHeight);
                SKRect destRect = new SKRect(0, 0, frameWidth, frameHeight);
                canvas.DrawBitmap(texture, sourceRect, destRect);
            }

            return bitmap;
        }
    }
}
