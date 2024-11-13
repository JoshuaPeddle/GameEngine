using SkiaSharp;

namespace GameEngine.Core
{
    public abstract class Scene
    {
        public Dictionary<Keys, string> actionMap = new();

        public Scene()
        {

        }

        public void AddAction(Keys key, string action)
        {
            actionMap.Add(key, action);
        }

        public void RemoveAction(Keys key)
        {
            actionMap.Remove(key);
        }

        public abstract void ExecuteAction(Keys key);

        public abstract void Simulate();

        public abstract void Render(SKCanvas canvas);
    }
}
