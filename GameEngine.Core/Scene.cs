using SkiaSharp;

namespace GameEngine.Core
{
    public abstract class Scene
    {
        public abstract void HandleAction(Keys key, bool start);

        public abstract void Simulate(float deltaMs);

        public abstract void Render(SKCanvas canvas);
    }
}
