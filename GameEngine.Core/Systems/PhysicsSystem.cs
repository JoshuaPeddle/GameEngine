using GameEngine.Core.Components;

namespace GameEngine.Core.Systems
{
    public class PhysicsSystem : ISystem
    {
        public void Update(EntityManager entityManager, double deltaTime)
        {
            var entities = entityManager.GetEntitiesWithComponent<CTransform>();
            var validEntities = entities.Where(e => e.HasComponent<CBoundingBox>()).ToList();

            foreach (var entity in validEntities)
            {
                var transform = entity.GetComponent<CTransform>();
                var boundingBox = entity.GetComponent<CBoundingBox>();

                var entitiesToCheck = validEntities.Where(e => e != entity).ToList();
                foreach (var entityToCheck in entitiesToCheck)
                {
                    var transformToCheck = entityToCheck.GetComponent<CTransform>();
                    var boundingBoxToCheck = entityToCheck.GetComponent<CBoundingBox>();
                    if (boundingBoxToCheck.BlockMovement != true)
                        continue;
                    Vec2 overlap = Physics.GetOverlap(entity, entityToCheck);
                    if (overlap.X > 0 && overlap.Y > 0)
                    {
                        Vec2 previousOverlap = Physics.GetPreviousOverlap(entity, entityToCheck);
                        if (overlap.X < overlap.Y)
                        {
                            if (previousOverlap.X > 0)
                                transform.Position.X += overlap.X;
                            else
                                transform.Position.X -= overlap.X;
                            transform.Velocity.X = 0;
                        }
                        else
                        {
                            if (previousOverlap.Y > 0)
                                transform.Position.Y += overlap.Y;
                            else
                                transform.Position.Y -= overlap.Y;
                            transform.Velocity.Y = 0;
                        }
                    }
                }
            }
        }
    }
}
