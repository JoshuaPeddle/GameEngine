using SkiaSharp;

namespace GameEngine.Core.Components
{
    public class CAnimation : Component
    {
        private readonly Animation animation;
        private double elapsedSeconds;

        public CAnimation(Animation animation)
        {
            this.animation = animation;
            this.elapsedSeconds = 0;
        }

        public SKBitmap Texture => animation.Texture;
        public bool ShouldDraw { get; set; } = true;

        public SKRect GetSourceRect()
        {
            return animation.GetSourceRect(elapsedSeconds);
        }

        public void Update(double deltaSeconds)
        {
            elapsedSeconds += deltaSeconds;
        }
    }
}
