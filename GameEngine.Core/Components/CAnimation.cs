using SkiaSharp;

namespace GameEngine.Core.Components
{
    public class CAnimation : Component
    {
        private readonly Animation animation;
        private float elapsedTime;

        public CAnimation(Animation animation)
        {
            this.animation = animation;
            this.elapsedTime = 0f;
        }

        public SKImage GetCurrentFrame()
        {
            return animation.GetCurrentFrame(elapsedTime);
        }

        public void Update(float deltaTime)
        {
            elapsedTime += deltaTime;
        }
    }
}
