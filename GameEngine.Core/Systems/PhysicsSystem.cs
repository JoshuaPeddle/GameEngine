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

        // Use List instead of ConcurrentBag to avoid allocations
        public List<CollisionEvent> CollisionEvents { get; private set; } = new(100);

        private readonly List<(Entity entity, CBoundingBox bbox, CTransform transform)> _validEntities = new(1000);
        private readonly List<(Entity entity, CGravity gravity, CTransform transform)> _entitiesWithGravity = new(100);

        public void Update(EntityManager entityManager, double deltaTime)
        {
            ResetEntityLists();
            PopulateEntityLists(entityManager);
            ProcessGravity(deltaTime);
            ProcessCollisionsOptimized();
        }

        private void ProcessCollisionsOptimized()
        {
            int count = _validEntities.Count;

            for (int i = 0; i < count - 1; i++)
            {
                (Entity entity, CBoundingBox bbox, CTransform transform) entityA = _validEntities[i];

                for (int j = i + 1; j < count; j++)
                {
                    (Entity entity, CBoundingBox bbox, CTransform transform) entityB = _validEntities[j];

                    if (!QuickAABBTest(
                        entityA.transform.Position, entityA.bbox.Size,
                        entityB.transform.Position, entityB.bbox.Size))
                        continue;

                    Vec2 overlap = Physics.GetOverlap(
                        entityA.transform, entityB.transform,
                        entityA.bbox, entityB.bbox);

                    if (overlap.X > 0.0 && overlap.Y > 0.0)
                    {
                        CollisionEvents.Add(new CollisionEvent(entityA.entity, entityB.entity, overlap));

                        if (entityB.bbox.BlockMovement)
                            ResolveCollision(entityA.transform, entityB.transform, overlap);
                    }
                }
            }
        }

        private static bool QuickAABBTest(Vec2 pos1, Vec2 size1, Vec2 pos2, Vec2 size2)
        {
            return pos1.X < pos2.X + size2.X &&
                   pos1.X + size1.X > pos2.X &&
                   pos1.Y < pos2.Y + size2.Y &&
                   pos1.Y + size1.Y > pos2.Y;
        }

        private void ProcessGravity(double deltaTime)
        {
            double gravityDelta = deltaTime;

            foreach ((Entity entity, CGravity gravity, CTransform transform) in _entitiesWithGravity)
            {
                transform.Velocity += new Vec2(0, gravity.Acceleration * gravityDelta);
            }
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