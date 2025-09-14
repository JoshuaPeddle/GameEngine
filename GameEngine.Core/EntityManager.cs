namespace GameEngine.Core
{
    public class EntityManager : IDisposable
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

            for (int i = entities.Count - 1; i >= 0; i--)
            {
                Entity? entity = entities[i];
                if (entity.Active) continue;

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
                entity.Dispose();
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

        public List<Entity> GetEntitiesWith<T>() where T : Component
        {
            if (componentEntityMap.TryGetValue(typeof(T), out var entitySet))
            {
                return [.. entitySet];
            }
            return [];
        }

        public Entity? GetEntityWithTag(string tag)
        {
            foreach (var entity in entities)
            {
                if (entity.Tag == tag)
                {
                    return entity;
                }
            }
            return null;
        }

        public List<Entity> GetEntitiesWithTag(string tag)
        {
            return entities.Where(e => e.Tag == tag).ToList();
        }

        public IReadOnlyList<Entity> GetEntitiesWithComponent<T>() where T : Component
        {
            if (componentEntityMap.TryGetValue(typeof(T), out var entitySet))
            {
                return [.. entitySet];
            }
            return [];
        }

        public List<(Entity, T)> GetEntitiesWithComponents<T>() where T : Component
        {
            if (componentEntityMap.TryGetValue(typeof(T), out var entitySet))
            {
                var result = new List<(Entity, T)>(entitySet.Count);
                foreach (var entity in entitySet)
                {
                    result.Add((entity, entity.GetComponent<T>()));
                }
                return result;
            }
            return [];
        }

        public IReadOnlyList<(Entity, T1, T2)> GetEntitiesWithComponentsUnsafe<T1, T2>() where T1 : Component where T2 : Component
        {
            if (componentEntityMap.TryGetValue(typeof(T1), out var entitySet))
            {
                var result = new List<(Entity, T1, T2)>(entitySet.Count);
                foreach (var entity in entitySet)
                {
                    result.Add((entity, entity.GetComponent<T1>(), entity.GetComponent<T2>()));
            }
            return result;
        }
            return [];
        }

        public IReadOnlyList<(Entity, T1, T2)> GetEntitiesWithComponents<T1, T2>() where T1 : Component where T2 : Component
        {
            if (!componentEntityMap.TryGetValue(typeof(T1), out var entitiesWithT1) ||
                !componentEntityMap.TryGetValue(typeof(T2), out var entitiesWithT2))
            {
                return Array.Empty<(Entity, T1, T2)>();
            }

            HashSet<Entity> smallerSet = entitiesWithT1.Count <= entitiesWithT2.Count ? entitiesWithT1 : entitiesWithT2;
            HashSet<Entity> largerSet = smallerSet == entitiesWithT1 ? entitiesWithT2 : entitiesWithT1;

            var result = new List<(Entity, T1, T2)>(smallerSet.Count);

            foreach (var entity in smallerSet)
            {
                if (largerSet.Contains(entity) &&
                    entity.TryGetComponent<T1>(out var component1) &&
                    entity.TryGetComponent<T2>(out var component2))
                {
                    result.Add((entity, component1, component2));
                }
            }

            return result;
        }

        public void Clear()
        {
            entities.Clear();
            entitiesToAdd.Clear();
            componentEntityMap.Clear();
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

        public void Dispose()
        {
            foreach (var entity in entities)
            {
                entity.Kill();
            }
            entities.Clear();
            entitiesToAdd.Clear();
            componentEntityMap.Clear();

        }
    }
}
