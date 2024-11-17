using static GameEngine.Core.Exceptions;

namespace GameEngine.Core
{
    public class Entity
    {
        public int Id = 0;
        public bool Active = true;
        public string Tag = "default";
        public Dictionary<Type, Component> Components = [];

        private readonly EntityManager entityManager;

        internal Entity(int id, string tag, EntityManager manager)
        {
            this.Id = id;
            Tag = tag;
            this.entityManager = manager;
        }

        public Component AddComponent(Component component)
        {
            Components[component.GetType()] = component;
            entityManager.AddEntityToComponentMap(component.GetType(), this);
            return component;
        }

        public T AddComponent<T>() where T : Component, new()
        {
            T component = new();
            Components[typeof(T)] = component;
            entityManager.AddEntityToComponentMap(typeof(T), this);
            return component;
        }

        public bool HasComponent<T>() where T : Component
        {
            return Components.ContainsKey(typeof(T));
        }

        public bool TryGetComponent<T>(out T? component) where T : Component
        {
            if (Components.TryGetValue(typeof(T), out var comp))
            {
                component = (T)comp;
                return true;
            }
            component = null;
            return false;
        }

        public T GetComponent<T>() where T : Component
        {
            if (Components.TryGetValue(typeof(T), out var comp))
            {
                return (T)comp;
            }
            throw new ComponentNotFoundException<T>(this);
        }

        public void RemoveComponent<T>() where T : Component
        {
            if (Components.Remove(typeof(T)))
            {
                entityManager.RemoveEntityFromComponentMap(typeof(T), this);
            }
        }

        public void RemoveComponent(Component component)
        {
            if (Components.Remove(component.GetType()))
            {
                entityManager.RemoveEntityFromComponentMap(component.GetType(), this);
            }
        }
    }
}