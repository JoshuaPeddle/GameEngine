using SkiaSharp;

namespace GameEngine.Core.Components
{
    public class CAnimation : Component
    {
        private readonly Animation animation;
        private double elapsedTime;

        public CAnimation(Animation animation)
        {
            this.animation = animation;
            this.elapsedTime = 0f;
        }

        public SKBitmap Texture => animation.Texture;
        public bool ShouldDraw { get; set; } = true;

        public SKRect GetSourceRect()
        {
            return animation.GetSourceRect(elapsedTime);
        }

        public void Update(double deltaTime)
        {
            elapsedTime += deltaTime;
        }
    }
}
