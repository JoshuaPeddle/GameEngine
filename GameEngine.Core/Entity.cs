using System.Diagnostics.CodeAnalysis;

namespace GameEngine.Core
{
    // Where an entity sits in its manager's lifecycle. Queries only ever see Live entities:
    // a pending one has not been flushed by EntityManager.Update yet, and a detached one has
    // been removed and can never re-enter an index, however long a caller keeps hold of it.
    internal enum EntityState
    {
        Pending,
        Live,
        Detached
    }

    public class Entity
    {
        public int Id { get; }

        public bool Active { get; set; } = true;

        private string tag;
        private readonly Dictionary<Type, Component> components = [];
        private readonly EntityManager entityManager;

        internal EntityState State { get; set; } = EntityState.Pending;

        internal Entity(int id, string tag, EntityManager manager)
        {
            Id = id;
            this.tag = tag;
            entityManager = manager;
        }

        // Tag queries are cached, so renaming has to reach the manager rather than being a
        // plain field write that leaves the previous tag's cache answering.
        public string Tag
        {
            get => tag;
            set
            {
                if (tag == value)
                    return;

                tag = value;
                entityManager.TagChanged(this);
            }
        }

        public IReadOnlyDictionary<Type, Component> Components => components;

        internal IEnumerable<Type> ComponentTypes => components.Keys;

        public EntitySnapshot Capture() =>
            new(Id, Tag, Active, components.Keys.Select(t => t.Name).OrderBy(n => n).ToArray());

        public Component AddComponent(Component component)
        {
            ArgumentNullException.ThrowIfNull(component);
            return Set(component.GetType(), component);
        }

        public T AddComponent<T>() where T : Component, new() =>
            (T)Set(typeof(T), new T());

        public T AddComponent<T>(Component component) where T : Component, new()
        {
            ArgumentNullException.ThrowIfNull(component);
            return (T)Set(typeof(T), component);
        }

        private Component Set(Type componentType, Component component)
        {
            components[componentType] = component;
            entityManager.ComponentAdded(componentType, this);
            return component;
        }

        public bool HasComponent<T>() where T : Component
        {
            return components.ContainsKey(typeof(T));
        }

        public bool TryGetComponent<T>([NotNullWhen(true)] out T? component) where T : Component
        {
            if (components.TryGetValue(typeof(T), out var comp))
            {
                component = (T)comp;
                return true;
            }
            component = default;
            return false;
        }

        public T GetComponent<T>() where T : Component
        {
            if (components.TryGetValue(typeof(T), out var comp))
            {
                return (T)comp;
            }
            throw new ComponentNotFoundException<T>(this);
        }

        public T? TryGetComponent<T>() where T : Component
        {
            if (components.TryGetValue(typeof(T), out var comp))
            {
                return (T)comp;
            }
            return default;
        }

        public void RemoveComponent<T>() where T : Component
        {
            if (components.Remove(typeof(T), out _))
            {
                entityManager.ComponentRemoved(typeof(T), this);
            }
        }

        public void RemoveComponent(Component component)
        {
            ArgumentNullException.ThrowIfNull(component);

            if (components.Remove(component.GetType(), out _))
            {
                entityManager.ComponentRemoved(component.GetType(), this);
            }
        }
    }
}
