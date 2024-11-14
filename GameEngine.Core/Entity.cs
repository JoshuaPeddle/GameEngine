using static GameEngine.Core.Exceptions;

namespace GameEngine.Core
{
    public class Entity
    {
        public int id = 0;
        public bool Active = true;
        public string Tag = "default";
        public List<Component> Components = new();

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
            T component = new T();
            Components.Add(component);
            return component;
        }

        public T GetComponent<T>() where T : Component
        {
            foreach (var component in Components)
            {
                if (component is T)
                {
                    return component as T;
                }
            }
            throw new ComponentNotFoundException($"Entity {id}:{Tag} does not have component of type {typeof(T)}.");
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
