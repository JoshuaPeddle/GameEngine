using GameEngine.Core.Components;
using System.Diagnostics.CodeAnalysis;
using static GameEngine.Core.Exceptions;

namespace GameEngine.Core
{
    public class EntityManager
    {
        /// <summary>Tag the renderer looks for when picking the active camera.</summary>
        private const string CameraTag = "camera";

        private readonly List<Entity> entities = [];
        private readonly List<Entity> entitiesToAdd = [];
        private readonly List<Entity> inactiveBuffer = [];
        private readonly Dictionary<Type, HashSet<Entity>> componentEntityMap = [];
        private readonly Dictionary<int, Entity> entitiesById = [];

        private int nextEntityId;

        private int structuralVersion;

        private sealed class CachedQuery<T>
        {
            public int Version = -1;
            public List<T> Items = [];
        }

        private readonly Dictionary<Type, object> cachedEntityLists = [];
        private readonly Dictionary<Type, object> cachedTupleLists = [];
        private readonly Dictionary<Type, object> cachedTuple2Lists = [];
        private readonly Dictionary<string, object> cachedTagLists = [];

        public EntityManager() { }

        public void Update()
        {
            if (entitiesToAdd.Count > 0)
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
                structuralVersion++;
            }

            inactiveBuffer.Clear();
            foreach (var e in entities)
            {
                if (!e.Active)
                    inactiveBuffer.Add(e);
            }

            if (inactiveBuffer.Count == 0)
                return;

            foreach (var entity in inactiveBuffer)
            {
                entitiesById.Remove(entity.Id);
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

            CompactActiveEntities();
            structuralVersion++;
        }

        private void CompactActiveEntities()
        {
            int write = 0;
            for (int read = 0; read < entities.Count; read++)
            {
                if (entities[read].Active)
                    entities[write++] = entities[read];
            }

            entities.RemoveRange(write, entities.Count - write);
        }

        public Entity CreateEntity(string tag)
        {
            Entity entity = new(nextEntityId++, tag, this);
            entitiesToAdd.Add(entity);
            entitiesById[entity.Id] = entity;
            return entity;
        }

        public List<Entity> GetEntities()
        {
            return entities;
        }

        public Entity GetEntity(int id)
        {
            if (entitiesById.TryGetValue(id, out var entity))
                return entity;

            throw new EntityNotFoundException($"No entity with id {id}.");
        }

        public bool TryGetEntity(int id, [NotNullWhen(true)] out Entity? entity)
        {
            return entitiesById.TryGetValue(id, out entity);
        }

        public IReadOnlyList<Entity> GetEntitiesWith<T>() where T : Component
        {
            return GetCachedEntityList<T>();
        }

        public Entity? GetEntityWithTag(string tag)
        {
            foreach (var entity in entities)
            {
                if (entity.Tag == tag)
                    return entity;
            }

            return null;
        }

        public IReadOnlyList<Entity> GetEntitiesWithTag(string tag)
        {
            if (!cachedTagLists.TryGetValue(tag, out var boxed))
            {
                boxed = new CachedQuery<Entity>();
                cachedTagLists[tag] = boxed;
            }

            var cache = (CachedQuery<Entity>)boxed;
            if (cache.Version == structuralVersion)
                return cache.Items;

            var rebuilt = new List<Entity>();
            foreach (var e in entities)
            {
                if (e.Tag == tag)
                    rebuilt.Add(e);
            }

            cache.Items = rebuilt;
            cache.Version = structuralVersion;
            return rebuilt;
        }

        public IReadOnlyList<Entity> GetEntitiesWithComponent<T>() where T : Component
        {
            return GetCachedEntityList<T>();
        }

        public IReadOnlyList<(Entity, T)> GetEntitiesWithComponents<T>() where T : Component
        {
            var type = typeof(T);
            if (!cachedTupleLists.TryGetValue(type, out var boxed))
            {
                boxed = new CachedQuery<(Entity, T)>();
                cachedTupleLists[type] = boxed;
            }

            var cache = (CachedQuery<(Entity, T)>)boxed;
            if (cache.Version == structuralVersion)
                return cache.Items;

            componentEntityMap.TryGetValue(type, out var entitySet);
            var rebuilt = new List<(Entity, T)>(entitySet?.Count ?? 0);
            if (entitySet != null)
            {
                foreach (var entity in entitySet)
                {
                    rebuilt.Add((entity, entity.GetComponent<T>()));
                }
            }

            cache.Items = rebuilt;
            cache.Version = structuralVersion;
            return rebuilt;
        }

        public IReadOnlyList<(Entity, T1, T2)> GetEntitiesWithComponents<T1, T2>() where T1 : Component where T2 : Component
        {
            var key = typeof((T1, T2));
            if (!cachedTuple2Lists.TryGetValue(key, out var boxed))
            {
                boxed = new CachedQuery<(Entity, T1, T2)>();
                cachedTuple2Lists[key] = boxed;
            }

            var cache = (CachedQuery<(Entity, T1, T2)>)boxed;
            if (cache.Version == structuralVersion)
                return cache.Items;

            componentEntityMap.TryGetValue(typeof(T1), out var entitySet);
            var rebuilt = new List<(Entity, T1, T2)>(entitySet?.Count ?? 0);
            if (entitySet != null)
            {
                foreach (var entity in entitySet)
                {
                    if (entity.HasComponent<T2>())
                    {
                        rebuilt.Add((entity, entity.GetComponent<T1>(), entity.GetComponent<T2>()));
                    }
                }
            }

            cache.Items = rebuilt;
            cache.Version = structuralVersion;
            return rebuilt;
        }

        public void Clear()
        {
            entities.Clear();
            entitiesToAdd.Clear();
            entitiesById.Clear();
            componentEntityMap.Clear();
            ClearCaches();
            structuralVersion++;

        }

        internal void AddEntityToComponentMap(Type componentType, Entity entity)
        {
            if (!componentEntityMap.TryGetValue(componentType, out var entitySet))
            {
                entitySet = [];
                componentEntityMap[componentType] = entitySet;
            }

            if (entitySet.Add(entity))
                structuralVersion++;
        }

        internal void RemoveEntityFromComponentMap(Type componentType, Entity entity)
        {
            if (componentEntityMap.TryGetValue(componentType, out var entitySet))
            {
                if (entitySet.Remove(entity))
                    structuralVersion++;

                if (entitySet.Count == 0)
                {
                    componentEntityMap.Remove(componentType);
                }
            }
        }

        private IReadOnlyList<Entity> GetCachedEntityList<T>() where T : Component
        {
            var type = typeof(T);
            if (!cachedEntityLists.TryGetValue(type, out var boxed))
            {
                boxed = new CachedQuery<Entity>();
                cachedEntityLists[type] = boxed;
            }

            var cache = (CachedQuery<Entity>)boxed;
            if (cache.Version == structuralVersion)
                return cache.Items;

            componentEntityMap.TryGetValue(type, out var entitySet);
            var rebuilt = new List<Entity>(entitySet?.Count ?? 0);
            if (entitySet != null)
            {
                foreach (var entity in entitySet)
                {
                    rebuilt.Add(entity);
                }
            }

            cache.Items = rebuilt;
            cache.Version = structuralVersion;
            return rebuilt;
        }

        private void ClearCaches()
        {
            cachedEntityLists.Clear();
            cachedTupleLists.Clear();
            cachedTuple2Lists.Clear();
            cachedTagLists.Clear();
        }

        /// Fill <paramref name="buffer"/> with all renderable entity state.
        /// Called on the engine thread after Update() so all deferred adds/removes are applied.
        /// The buffer is reused frame to frame, so this allocates nothing once its backing
        /// array has grown to fit the scene.
        public void BuildRenderSnapshot(RenderSnapshot buffer)
        {
            buffer.Reset(entities.Count);

            // A camera entity is not required to carry a CTransform (none of the demo scenes
            // give it one), so it has to be found through its own component set rather than
            // while walking the transforms.
            if (componentEntityMap.TryGetValue(typeof(CCamera), out var cameraEntities))
            {
                CCamera? activeCamera = null;
                foreach (var entity in cameraEntities)
                {
                    if (!entity.TryGetComponent<CCamera>(out var cam))
                        continue;

                    // Match the legacy renderer: the entity tagged "camera" wins. Any other
                    // camera is a fallback so untagged setups still render through one.
                    if (entity.Tag == CameraTag)
                    {
                        activeCamera = cam;
                        break;
                    }
                    activeCamera ??= cam;
                }

                if (activeCamera != null)
                    buffer.SetCamera(new RenderSnapshot.CameraData(activeCamera));
            }

            bool anyNonDefaultLayer = false;

            foreach (var entity in entities)
            {
                if (!entity.TryGetComponent<CTransform>(out var transform))
                    continue;

                if (transform.Layer != 0)
                    anyNonDefaultLayer = true;

                RenderSnapshot.AnimationData? animData = null;
                if (entity.TryGetComponent<CAnimation>(out var anim))
                    animData = new RenderSnapshot.AnimationData(anim);

                RenderSnapshot.TextData? textData = null;
                if (entity.TryGetComponent<CText>(out var text))
                    textData = new RenderSnapshot.TextData(text);

                RenderSnapshot.BoundingBoxData? bboxData = null;
                if (entity.TryGetComponent<CBoundingBox>(out var bbox))
                    bboxData = new RenderSnapshot.BoundingBoxData(bbox);

                buffer.Add(new RenderSnapshot.Entry(
                    entity.Id,
                    entity.Tag,
                    transform.Layer,
                    new RenderSnapshot.TransformData(transform),
                    animData,
                    textData,
                    bboxData
                ));
            }

            if (anyNonDefaultLayer)
                buffer.SortByLayer();
        }
    }
}
