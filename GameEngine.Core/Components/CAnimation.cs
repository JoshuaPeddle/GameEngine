using SkiaSharp;

namespace GameEngine.Core.Components
{
    public class CAnimation : Component
    {
        private Animation animation;
        private double elapsedSeconds;

        public CAnimation(Animation animation)
        {
            this.animation = animation;
            this.elapsedSeconds = 0;
        }

        // Settable so an entity can change what it is playing without its component being
        // replaced. Replacing it would work, but every replacement invalidates the manager's
        // query caches, and a scene that swaps sprites per frame would rebuild them per frame.
        public Animation Animation
        {
            get => animation;
            set
            {
                ArgumentNullException.ThrowIfNull(value);

                if (ReferenceEquals(animation, value))
                    return;

                animation = value;
                elapsedSeconds = 0;
            }
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
