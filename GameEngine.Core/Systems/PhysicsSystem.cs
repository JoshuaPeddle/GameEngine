using GameEngine.Core.Components;

namespace GameEngine.Core.Systems
{
    public class PhysicsSystem : ISystem
    {
        private const double epsilon = 0.0001;

        public void Update(EntityManager entityManager, double deltaTime)
        {
            var entities = entityManager.GetEntitiesWithComponent<CTransform>();
            var validEntities = entities.Where(e => e.HasComponent<CBoundingBox>()).ToList();

            foreach (var entity in validEntities)
            {
                var transform = entity.GetComponent<CTransform>();
                if (transform.Position == transform.PreviousPosition)
                    continue;

                var boundingBox = entity.GetComponent<CBoundingBox>();

                var entitiesToCheck = validEntities.Where(e => e.Id != entity.Id).ToList();
                foreach (var entityToCheck in entitiesToCheck)
                {
                    var transformToCheck = entityToCheck.GetComponent<CTransform>();
                    var boundingBoxToCheck = entityToCheck.GetComponent<CBoundingBox>();
                    if (boundingBoxToCheck.BlockMovement != true)
                        continue;
                    Vec2 overlap = Physics.GetOverlap(transform, transformToCheck, boundingBox, boundingBoxToCheck);
                    if (overlap.X > 0.0 && overlap.Y > 0.0)
                    {
                        if (overlap.X < overlap.Y)
                        {
                            double deltaX = transform.Position.X - transform.PreviousPosition.X;
                            if (Math.Abs(deltaX) > epsilon)
                            {
                                if (deltaX > 0)
                                    transform.Position.X -= overlap.X;
                                else
                                    transform.Position.X += overlap.X;
                            }
                            else
                            {
                                if (transform.Position.X < transformToCheck.Position.X)
                                    transform.Position.X -= overlap.X;
                                else
                                    transform.Position.X += overlap.X;
                            }
                            transform.Velocity.X = 0;
                        }
                        else
                        {
                            double deltaY = transform.Position.Y - transform.PreviousPosition.Y;
                            if (Math.Abs(deltaY) > epsilon)
                            {
                                if (deltaY > 0)
                                    transform.Position.Y -= overlap.Y;
                                else
                                    transform.Position.Y += overlap.Y;
                            }
                            else
                            {
                                if (transform.Position.Y < transformToCheck.Position.Y)
                                    transform.Position.Y -= overlap.Y;
                                else
                                    transform.Position.Y += overlap.Y;
                            }
                            transform.Velocity.Y = 0;
                        }
                    }
                }
            }

        }
    }
}
