using SkiaSharp;

namespace GameEngine.Core
{
    public abstract class Scene
    {
        public Dictionary<Keys, string> ActionMap = [];

        public void AddAction(Keys key, string action)
        {
            ActionMap.Add(key, action);
        }

        public void RemoveAction(Keys key)
        {
            ActionMap.Remove(key);
        }

        public abstract void HandleAction(Keys key, bool start);

        public abstract void Simulate();

        public abstract void Render(SKCanvas canvas);
    }
}
