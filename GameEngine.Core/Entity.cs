using static GameEngine.Core.Exceptions;

namespace GameEngine.Core
{
    public class Entity
    {
        public int id = 0;
        public bool Active = true;
        public string Tag = "default";
        public List<Component> Components = [];

        internal Entity(int id, string tag)
        {
            this.id = id;
            Tag = tag;
        }
        public Component AddComponent(Component component)
        {
            Components.Add(component);
            return component;
        }

        public T AddComponent<T>() where T : Component, new()
        {
            T component = new();
            Components.Add(component);
            return component;
        }

        public bool HasComponent<T>()
        {
            foreach (var component in Components)
            {
                if (component is T)
                {
                    return true;
                }
            }
            return false;
        }

        public bool TryGetComponent<T>(out T? component) where T : Component
        {
            foreach (var c in Components)
            {
                if (c is T)
                {
                    component = c as T;
                    return true;
                }
            }
            component = null;
            return false;
        }

        public T GetComponent<T>() where T : Component
        {
            foreach (var component in Components)
            {
                if (component is T typedComponent)
                {
                    return typedComponent;
                }
            }
            throw new ComponentNotFoundException<T>(this);
        }

        public void RemoveComponent<T>()
        {
            for (int i = 0; i < Components.Count; i++)
            {
                if (Components[i].GetType() == typeof(T))
                {
                    Components.RemoveAt(i);
                    return;
                }
            }
        }

        public void RemoveComponent(Component component)
        {
            Components.Remove(component);
        }
    }
}
