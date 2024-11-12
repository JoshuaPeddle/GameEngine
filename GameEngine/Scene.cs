using SkiaSharp;
using SkiaSharp.Views.Desktop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameEngine.WinForms
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
        public void RemoveAction(Keys key) {
            actionMap.Remove(key);
        }

        public abstract void ExecuteAction(Keys key);
        public abstract void Simulate();

        internal abstract void Render(SKCanvas canvas);
    }
}
