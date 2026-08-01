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

        /// <summary>
        /// Ids are handed out monotonically and never reused. They used to be derived from the
        /// entity count, so despawning freed an id for the next spawn and two live entities
        /// could end up sharing one.
        /// </summary>
        private int nextEntityId;

        /// <summary>
        /// Bumped on every structural change: an entity added or removed, or a component added
        /// to or removed from one. Cached query results record the version they were built at
        /// and are rebuilt only when stale.
        /// </summary>
        private int structuralVersion;

        /// <summary>
        /// A query result plus the <see cref="structuralVersion"/> it was built at.
        /// <para>
        /// A stale entry is replaced with a <em>new</em> list rather than cleared and refilled
        /// in place. Callers hold onto these lists — systems iterate them, and
        /// <see cref="Systems.PhysicsSystem"/> copies out of them — so mutating a list that has
        /// already been handed out is what made the previous shared-buffer cache unsafe: a
        /// nested query for the same component type wiped the list the outer loop was walking.
        /// </para>
        /// <para>
        /// Steady state still allocates nothing. A rebuild only happens after a structural
        /// change, which is far rarer than querying.
        /// </para>
        /// </summary>
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
                entities.Remove(entity);
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
            structuralVersion++;
        }

        /// <summary>
        /// Create an entity. It becomes visible to <see cref="GetEntities"/> on the next
        /// <see cref="Update"/>, but is addressable by id — and by component queries, once
        /// components are attached — straight away.
        /// </summary>
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

        /// <summary>
        /// Look an entity up by its id. This used to index the backing list, so any removal
        /// shifted every later entity and returned the wrong one.
        /// </summary>
        /// <exception cref="EntityNotFoundException">No live entity carries that id.</exception>
        public Entity GetEntity(int id)
        {
            if (entitiesById.TryGetValue(id, out var entity))
                return entity;

            throw new EntityNotFoundException($"No entity with id {id}.");
        }

        /// <summary>Non-throwing counterpart to <see cref="GetEntity"/>.</summary>
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
            return entities.FirstOrDefault(e => e.Tag == tag);
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

            // nextEntityId deliberately keeps counting. Restarting it would let a reference
            // held across a scene reset silently match a freshly created entity.
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

        /// <summary>
        /// Fill <paramref name="buffer"/> with all renderable entity state.
        /// Called on the engine thread after Update() so all deferred adds/removes are applied.
        /// The buffer is reused frame to frame, so this allocates nothing once its backing
        /// array has grown to fit the scene.
        /// </summary>
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

            // Walk the entity list rather than the component set: the list is in spawn order,
            // whereas the set enumerates in hash-slot order, which reshuffles as soon as a
            // despawn frees a slot for a later entity to reuse. Draw order has to be stable.
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

            // Entries are already in spawn order, so sorting is only needed when a scene
            // actually uses layers.
            if (anyNonDefaultLayer)
                buffer.SortByLayer();
        }
    }
}
