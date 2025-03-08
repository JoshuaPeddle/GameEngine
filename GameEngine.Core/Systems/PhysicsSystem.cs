using GameEngine.Core.Components;

namespace GameEngine.Core.Systems
{
    public readonly struct CollisionEvent
    {
        public readonly Entity A;
        public readonly Entity B;
        public readonly Vec2 Overlap;

        public CollisionEvent(Entity a, Entity b, Vec2 overlap)
        {
            A = a;
            B = b;
            Overlap = overlap;
        }
    }

    public class PhysicsSystem : ISystem
    {
        private const double epsilon = 0.0001;
        public List<CollisionEvent> CollisionEvents { get; private set; } = [];

        private readonly List<Entity> _validEntities = [];
        private readonly List<Entity> _entitiesWithGravity = [];
        private readonly List<Entity> _entitiesToCheck = [];

        public void Update(EntityManager entityManager, double deltaTime)
        {
            ResetEntityLists();
            PopulateEntityLists(entityManager);
            ProcessGravity(deltaTime);
            ProcessCollisions();
        }

        private void ProcessCollisions()
        {
            foreach (var entity in _validEntities)
            {
                var transform = entity.GetComponent<CTransform>();
                if (transform.Position == transform.PreviousPosition)
                    continue;

                var boundingBox = entity.GetComponent<CBoundingBox>();
                _entitiesToCheck.Clear();

                foreach (var potentialEntity in _validEntities)
                {
                    if (entity.Id >= potentialEntity.Id || entity.Id == potentialEntity.Id) continue;
                    _entitiesToCheck.Add(potentialEntity);
                }

                foreach (var entityToCheck in _entitiesToCheck)
                {
                    var transformToCheck = entityToCheck.GetComponent<CTransform>();
                    var boundingBoxToCheck = entityToCheck.GetComponent<CBoundingBox>();

                    Vec2 overlap = Physics.GetOverlap(transform, transformToCheck, boundingBox, boundingBoxToCheck);
                    if (overlap.X > 0.0 && overlap.Y > 0.0)
                    {
                        CollisionEvents.Add(new CollisionEvent(entity, entityToCheck, overlap));

                        if (boundingBoxToCheck.BlockMovement)
                        {
                            ResolveCollision(transform, transformToCheck, overlap);
                        }
                    }
                }
            }
        }

        private void ProcessGravity(double deltaTime)
        {
            Parallel.ForEach(_entitiesWithGravity, entity =>
            {
                var transform = entity.GetComponent<CTransform>();
                var gravity = entity.GetComponent<CGravity>();
                transform.Velocity.Y += gravity.Acceleration * deltaTime;
            });
        }

        private void PopulateEntityLists(EntityManager entityManager)
        {
            var entities = entityManager.GetEntitiesWithComponent<CTransform>();
            foreach (var entity in entities)
            {
                if (entity.HasComponent<CBoundingBox>())
                {
                    _validEntities.Add(entity);
                }
                if (entity.HasComponent<CGravity>())
                {
                    _entitiesWithGravity.Add(entity);
                }
            }
        }

        private void ResetEntityLists()
        {
            CollisionEvents.Clear();
            _validEntities.Clear();
            _entitiesWithGravity.Clear();
        }

        private void ResolveCollision(CTransform transform, CTransform transformToCheck, Vec2 overlap)
        {
            if (overlap.X < overlap.Y)
            {
                HandleXAxisCollision(transform, transformToCheck, overlap.X);
            }
            else
            {
                HandleYAxisCollision(transform, transformToCheck, overlap.Y);
            }
        }

        private void HandleXAxisCollision(CTransform transform, CTransform transformToCheck, double overlapX)
        {
            double deltaX = transform.Position.X - transform.PreviousPosition.X;
            if (Math.Abs(deltaX) > epsilon)
            {
                transform.Position.X += (deltaX > 0) ? -overlapX : overlapX;
            }
            else
            {
                transform.Position.X += (transform.Position.X < transformToCheck.Position.X) ? -overlapX : overlapX;
            }
            transform.Velocity.X = 0;
        }

        private void HandleYAxisCollision(CTransform transform, CTransform transformToCheck, double overlapY)
        {
            double deltaY = transform.Position.Y - transform.PreviousPosition.Y;
            if (Math.Abs(deltaY) > epsilon)
            {
                transform.Position.Y += (deltaY > 0) ? -overlapY : overlapY;
            }
            else
            {
                transform.Position.Y += (transform.Position.Y < transformToCheck.Position.Y) ? -overlapY : overlapY;
            }
            transform.Velocity.Y = 0;
        }
    }
}