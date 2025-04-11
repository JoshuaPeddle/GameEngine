using GameEngine.Core.Components;
using System.Collections.Concurrent;

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
        public ConcurrentBag<CollisionEvent> CollisionEvents { get; private set; } = [];

        private readonly List<(Entity, CBoundingBox, CTransform)> _validEntities = [];
        private readonly List<(Entity, CGravity, CTransform)> _entitiesWithGravity = [];

        public void Update(EntityManager entityManager, double deltaTime)
        {
            ResetEntityLists();
            PopulateEntityLists(entityManager);
            ProcessGravity(deltaTime);
            ProcessCollisions();
        }

        private void ProcessCollisions()
        {
            Parallel.ForEach(_validEntities, new ParallelOptions { MaxDegreeOfParallelism = 3 }, entityPair =>
            {
                var entity = entityPair.Item1;
                var boundingBox = entityPair.Item2;
                var transform = entityPair.Item3;

                foreach ((var entityToCheck, var boundingBoxToCheck, var transformToCheck) in _validEntities)
                {
                    if (entity.Id >= entityToCheck.Id || entity.Id == entityToCheck.Id) continue;

                    Vec2 overlap = Physics.GetOverlap(transform, transformToCheck, boundingBox, boundingBoxToCheck);
                    if (overlap.X > 0.0 && overlap.Y > 0.0)
                    {
                        CollisionEvents.Add(new CollisionEvent(entity, entityToCheck, overlap));

                        if (boundingBoxToCheck.BlockMovement)
                            ResolveCollision(transform, transformToCheck, overlap);
                    }
                }
            });
        }

        private void ProcessGravity(double deltaTime)
        {
            Parallel.ForEach(_entitiesWithGravity,  new ParallelOptions { MaxDegreeOfParallelism = 2 }, entity =>
            {
                var gravity = entity.Item2;
                var transform = entity.Item3;
                transform.Velocity += new Vec2(0, gravity.Acceleration * deltaTime);
            });
        }

        private void PopulateEntityLists(EntityManager entityManager)
        {
            _validEntities.AddRange(entityManager.GetEntitiesWithComponents<CBoundingBox, CTransform>());
            _entitiesWithGravity.AddRange(entityManager.GetEntitiesWithComponents<CGravity, CTransform>());
        }

        private void ResetEntityLists()
        {
            CollisionEvents.Clear();
            _validEntities.Clear();
            _entitiesWithGravity.Clear();
        }

        private void ResolveCollision(CTransform transform, CTransform transformToCheck, in Vec2 overlap)
        {
            if (overlap.X < overlap.Y)
                HandleXAxisCollision(transform, transformToCheck, overlap.X);
            else
                HandleYAxisCollision(transform, transformToCheck, overlap.Y);
        }

        private void HandleXAxisCollision(CTransform transform, CTransform transformToCheck, double overlapX)
        {
            double deltaX = transform.Position.X - transform.PreviousPosition.X;
            if (Math.Abs(deltaX) > epsilon)
                transform.Position += new Vec2((deltaX > 0) ? -overlapX : overlapX, 0);
            else
                transform.Position += new Vec2((transform.Position.X < transformToCheck.Position.X) ? -overlapX : overlapX, 0);
            transform.Velocity = new Vec2(0, transform.Velocity.Y);
        }

        private void HandleYAxisCollision(CTransform transform, CTransform transformToCheck, double overlapY)
        {
            double deltaY = transform.Position.Y - transform.PreviousPosition.Y;
            if (Math.Abs(deltaY) > epsilon)
                transform.Position += new Vec2(0, (deltaY > 0) ? -overlapY : overlapY);
            else
                transform.Position += new Vec2(0, (transform.Position.Y < transformToCheck.Position.Y) ? -overlapY : overlapY);
            transform.Velocity = new Vec2(transform.Velocity.X, 0);
        }
    }
}