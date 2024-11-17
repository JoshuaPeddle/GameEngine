namespace GameEngine.Core
{
    public class EntityManager
    {
        private readonly List<Entity> entities = [];
        private readonly List<Entity> entitiesToAdd = [];
        private readonly Dictionary<Type, HashSet<Entity>> componentEntityMap = [];

        public EntityManager() { }

        public void Update()
        {
            foreach (var entity in entitiesToAdd)
            {
                entities.Add(entity);
                foreach (var componentType in entity.Components.Keys)
                {
                    if (!componentEntityMap.TryGetValue(componentType, out var entitySet))
                    {
                        entitySet = [];
                        componentEntityMap[componentType] = entitySet;
                    }
                    entitySet.Add(entity);
                }
            }
            entitiesToAdd.Clear();

            var inactiveEntities = entities.Where(e => !e.Active).ToList();
            foreach (var entity in inactiveEntities)
            {
                entities.Remove(entity);
                foreach (var componentType in entity.Components.Keys)
                {
                    if (componentEntityMap.TryGetValue(componentType, out var entitySet))
                    {
                        entitySet.Remove(entity);
                        if (entitySet.Count == 0)
                        {
                            componentEntityMap.Remove(componentType);
                        }
                    }
                }
            }
        }

        public Entity CreateEntity(string tag)
        {
            Entity entity = new(entities.Count + entitiesToAdd.Count, tag, this);
            entitiesToAdd.Add(entity);
            return entity;
        }

        public List<Entity> GetEntities()
        {
            return entities;
        }

        public Entity GetEntity(int id)
        {
            return entities[id];
        }

        public List<Entity> GetEntitiesWithComponent<T>() where T : Component
        {
            if (componentEntityMap.TryGetValue(typeof(T), out var entitySet))
            {
                return [.. entitySet];
            }
            return [];
        }

        internal void AddEntityToComponentMap(Type componentType, Entity entity)
        {
            if (!componentEntityMap.TryGetValue(componentType, out var entitySet))
            {
                entitySet = [];
                componentEntityMap[componentType] = entitySet;
            }
            entitySet.Add(entity);
        }

        internal void RemoveEntityFromComponentMap(Type componentType, Entity entity)
        {
            if (componentEntityMap.TryGetValue(componentType, out var entitySet))
            {
                entitySet.Remove(entity);
                if (entitySet.Count == 0)
                {
                    componentEntityMap.Remove(componentType);
                }
            }
        }
    }
}
