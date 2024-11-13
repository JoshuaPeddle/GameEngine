using SkiaSharp;


namespace GameEngine.Core
{
    public class Animation
    {

        private string name;
        private SKBitmap texture;

        public Animation(string name, SKBitmap texture) 
        {
            this.name = name;
            this.texture = texture;
        }

        public void Update()
        {
        }

        public SKImage GetCurrentFrame()
        {
            return SKImage.FromBitmap(texture);
        }
    }
}
