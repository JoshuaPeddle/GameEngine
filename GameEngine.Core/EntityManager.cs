namespace GameEngine.Core
{
    public class EntityManager
    {
        private readonly List<Entity> entities = [];
        private readonly List<Entity> entitiesToAdd = [];
        private readonly List<Entity> inactiveBuffer = [];
        private readonly Dictionary<Type, HashSet<Entity>> componentEntityMap = [];

        // Pre-allocated caches to avoid per-frame allocations
        private readonly Dictionary<Type, List<Entity>> cachedEntityLists = [];
        private readonly Dictionary<Type, object> cachedTupleLists = [];
        private readonly Dictionary<Type, object> cachedTuple2Lists = [];
        private readonly Dictionary<string, List<Entity>> cachedTagLists = [];

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

            inactiveBuffer.Clear();
            foreach (var e in entities)
            {
                if (!e.Active)
                    inactiveBuffer.Add(e);
            }

            foreach (var entity in inactiveBuffer)
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

        public IReadOnlyList<Entity> GetEntitiesWith<T>() where T : Component
        {
            return GetCachedEntityList<T>();
        }

        public Entity? GetEntityWithTag(string tag)
        {
            return entities.FirstOrDefault(e => e.Tag == tag);
        }

        public IReadOnlyList<Entity> GetEntitiesWithTag(string tag)
        {
            if (!cachedTagLists.TryGetValue(tag, out var cached))
            {
                cached = [];
                cachedTagLists[tag] = cached;
            }

            cached.Clear();
            foreach (var e in entities)
            {
                if (e.Tag == tag)
                    cached.Add(e);
            }
            return cached;
        }

        public IReadOnlyList<Entity> GetEntitiesWithComponent<T>() where T : Component
        {
            return GetCachedEntityList<T>();
        }

        public IReadOnlyList<(Entity, T)> GetEntitiesWithComponents<T>() where T : Component
        {
            var type = typeof(T);
            if (!cachedTupleLists.TryGetValue(type, out var cached))
            {
                cached = new List<(Entity, T)>();
                cachedTupleLists[type] = cached;
            }

            var result = (List<(Entity, T)>)cached;
            result.Clear();

            if (componentEntityMap.TryGetValue(type, out var entitySet))
            {
                foreach (var entity in entitySet)
                {
                    result.Add((entity, entity.GetComponent<T>()));
                }
            }
            return result;
        }

        public IReadOnlyList<(Entity, T1, T2)> GetEntitiesWithComponents<T1, T2>() where T1 : Component where T2 : Component
        {
            var key = typeof((T1, T2));
            if (!cachedTuple2Lists.TryGetValue(key, out var cached))
            {
                cached = new List<(Entity, T1, T2)>();
                cachedTuple2Lists[key] = cached;
            }

            var result = (List<(Entity, T1, T2)>)cached;
            result.Clear();

            if (componentEntityMap.TryGetValue(typeof(T1), out var entitySet))
            {
                foreach (var entity in entitySet)
                {
                    if (entity.HasComponent<T2>())
                    {
                        result.Add((entity, entity.GetComponent<T1>(), entity.GetComponent<T2>()));
                    }
                }
            }
            return result;
        }

        public void Clear()
        {
            entities.Clear();
            entitiesToAdd.Clear();
            componentEntityMap.Clear();
            ClearCaches();
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

        private IReadOnlyList<Entity> GetCachedEntityList<T>() where T : Component
        {
            var type = typeof(T);
            if (!cachedEntityLists.TryGetValue(type, out var cached))
            {
                cached = [];
                cachedEntityLists[type] = cached;
            }

            cached.Clear();
            if (componentEntityMap.TryGetValue(type, out var entitySet))
            {
                foreach (var entity in entitySet)
                {
                    cached.Add(entity);
                }
            }
            return cached;
        }

        private void ClearCaches()
        {
            foreach (var list in cachedEntityLists.Values) list.Clear();
            foreach (var obj in cachedTupleLists.Values)
            {
                if (obj is System.Collections.IList l) l.Clear();
            }
            foreach (var obj in cachedTuple2Lists.Values)
            {
                if (obj is System.Collections.IList l) l.Clear();
            }
            foreach (var list in cachedTagLists.Values) list.Clear();
        }
    }
}
