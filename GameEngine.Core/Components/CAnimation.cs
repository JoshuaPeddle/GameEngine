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

        public SKImage GetCurrentFrame()
        {
            return animation.GetCurrentFrame(elapsedTime);
        }

        public void Update(double deltaTime)
        {
            elapsedTime += deltaTime;
        }
    }
}
