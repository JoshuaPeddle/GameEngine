using SkiaSharp;

namespace GameEngine.Core.Components
{
    public class CAnimation : Component
    {
        public Animation animation;

        public CAnimation(Animation animation)
        {
            this.animation = animation;
        }

        public SKImage GetCurrentFrame()
        {
            return animation.GetCurrentFrame();
        }

        public void Update(float deltaTime)
        {
            animation.Update(deltaTime);
        }
    }
}
