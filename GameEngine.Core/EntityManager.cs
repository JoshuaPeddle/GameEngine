namespace GameEngine.Core
{
    public class EntityManager
    {
        private readonly List<Entity> entities = new();
        private readonly List<Entity> entitiesToAdd = new();

        public EntityManager() { }

        public void Update()
        {
            entities.AddRange(entitiesToAdd);
            entitiesToAdd.Clear();
            entities.RemoveAll(entity => !entity.Active);
        }

        public Entity CreateEntity(string tag)
        {
            Entity entity = new Entity(entities.Count, tag);
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
            List<Entity> entitiesWithComponent = new();
            foreach (var entity in entities)
            {
                if (entity.HasComponent<T>())
                {
                    entitiesWithComponent.Add(entity);
                }
            }
            return entitiesWithComponent;
        }
    }
}
