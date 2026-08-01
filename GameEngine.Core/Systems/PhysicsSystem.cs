using GameEngine.Core.Components;

namespace GameEngine.Core.Systems
{
    public readonly struct CollisionEvent
    {
        public readonly Entity A;
        public readonly Entity B;
        public readonly Vec2 Overlap;

        /// <summary>
        /// <see cref="A"/>'s velocity at the moment of impact, before the collision was
        /// resolved. Resolution zeroes the velocity component along the separation axis, so a
        /// scene that wants to bounce — rather than stop — has to work from the impact value.
        /// Reading the live velocity after the fact sees the zero and loses both the speed and
        /// the direction it needs.
        /// </summary>
        public readonly Vec2 VelocityA;

        /// <summary><see cref="B"/>'s velocity at the moment of impact. See <see cref="VelocityA"/>.</summary>
        public readonly Vec2 VelocityB;

        public CollisionEvent(Entity a, Entity b, Vec2 overlap, Vec2 velocityA, Vec2 velocityB)
        {
            A = a;
            B = b;
            Overlap = overlap;
            VelocityA = velocityA;
            VelocityB = velocityB;
        }
    }

    public class PhysicsSystem : ISystem
    {
        private const double epsilon = 0.0001;

        // Use List instead of ConcurrentBag to avoid allocations
        public List<CollisionEvent> CollisionEvents { get; private set; } = new(100);

        private readonly List<(Entity entity, CBoundingBox bbox, CTransform transform)> _validEntities = new(1000);
        private readonly List<(Entity entity, CGravity gravity, CTransform transform)> _entitiesWithGravity = new(100);

        public void Update(EntityManager entityManager, double deltaSeconds)
        {
            ResetEntityLists();
            PopulateEntityLists(entityManager);
            ProcessGravity(deltaSeconds);
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
                        // Captured before resolving: resolution zeroes the velocity along the
                        // separation axis, and scenes that bounce need the impact value.
                        CollisionEvents.Add(new CollisionEvent(
                            entityA.entity, entityB.entity, overlap,
                            entityA.transform.Velocity, entityB.transform.Velocity));

                        ResolveCollision(
                            entityA.bbox, entityA.transform,
                            entityB.bbox, entityB.transform,
                            overlap);
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

        private void ProcessGravity(double deltaSeconds)
        {
            foreach ((Entity entity, CGravity gravity, CTransform transform) in _entitiesWithGravity)
            {
                transform.Velocity += new Vec2(0, gravity.Acceleration * deltaSeconds);
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

        /// <summary>
        /// Separate an overlapping pair along the shallower axis.
        /// <para>
        /// <see cref="CBoundingBox.BlockMovement"/> means "I am solid; things get pushed out of
        /// me", which is how every scene uses it — players and balls are false, platforms,
        /// paddles and walls are true. So a solid entity displaces a non-solid one entirely,
        /// two solids split the separation evenly, and two non-solids are a trigger pair that
        /// only raises the event for the scene to handle.
        /// </para>
        /// <para>
        /// This previously moved only whichever entity the pair loop happened to visit first,
        /// and only when the other was solid — so the outcome depended on hash-slot ordering.
        /// In Pong the ball was pushed out of a paddle or passed straight through it depending
        /// on which way round the pair came up.
        /// </para>
        /// </summary>
        private static void ResolveCollision(
            CBoundingBox boxA, CTransform transformA,
            CBoundingBox boxB, CTransform transformB,
            in Vec2 overlap)
        {
            // Two non-solid boxes are a trigger pair: report the event, move nothing.
            if (!boxA.BlockMovement && !boxB.BlockMovement)
                return;

            if (overlap.X < overlap.Y)
                SeparateOnX(transformA, boxA.BlockMovement, transformB, boxB.BlockMovement, overlap.X);
            else
                SeparateOnY(transformA, boxA.BlockMovement, transformB, boxB.BlockMovement, overlap.Y);
        }

        /// <summary>
        /// How much of the separation each entity absorbs. Stated symmetrically in A and B, so
        /// swapping the pair produces the same physical outcome — which is the whole point:
        /// the old code moved whichever entity the loop reached first.
        /// </summary>
        private static void GetSeparationShares(
            bool aSolid, bool bSolid, double deltaA, double deltaB,
            out double shareA, out double shareB)
        {
            // A solid entity displaces a non-solid one entirely.
            if (bSolid && !aSolid) { shareA = 1.0; shareB = 0.0; return; }
            if (aSolid && !bSolid) { shareA = 0.0; shareB = 1.0; return; }

            // Both solid. Whichever one moved into the other is the one that backs out, so
            // static geometry is never shoved aside by something running into it. Entities
            // without a CMovement never have PreviousPosition advanced, so they always read
            // as stationary here, which is exactly the behaviour walls and platforms want.
            bool movedA = Math.Abs(deltaA) > epsilon;
            bool movedB = Math.Abs(deltaB) > epsilon;

            if (movedA && !movedB) { shareA = 1.0; shareB = 0.0; return; }
            if (movedB && !movedA) { shareA = 0.0; shareB = 1.0; return; }

            // Both moving, or both still: split it.
            shareA = 0.5;
            shareB = 0.5;
        }

        private static void SeparateOnX(CTransform a, bool aSolid, CTransform b, bool bSolid, double overlapX)
        {
            double deltaA = a.Position.X - a.PreviousPosition.X;
            double deltaB = b.Position.X - b.PreviousPosition.X;

            GetSeparationShares(aSolid, bSolid, deltaA, deltaB, out double shareA, out double shareB);

            // Direction comes from relative motion so it is antisymmetric: swapping the pair
            // flips the sign and swaps the roles, landing on the same outcome.
            double relativeDelta = deltaA - deltaB;
            double directionA = Math.Abs(relativeDelta) > epsilon
                ? (relativeDelta > 0 ? -1.0 : 1.0)
                : (a.Position.X < b.Position.X ? -1.0 : 1.0);

            if (shareA > 0)
            {
                a.Position += new Vec2(directionA * overlapX * shareA, 0);
                a.Velocity = new Vec2(0, a.Velocity.Y);
            }

            if (shareB > 0)
            {
                b.Position += new Vec2(-directionA * overlapX * shareB, 0);
                b.Velocity = new Vec2(0, b.Velocity.Y);
            }
        }

        private static void SeparateOnY(CTransform a, bool aSolid, CTransform b, bool bSolid, double overlapY)
        {
            double deltaA = a.Position.Y - a.PreviousPosition.Y;
            double deltaB = b.Position.Y - b.PreviousPosition.Y;

            GetSeparationShares(aSolid, bSolid, deltaA, deltaB, out double shareA, out double shareB);

            double relativeDelta = deltaA - deltaB;
            double directionA = Math.Abs(relativeDelta) > epsilon
                ? (relativeDelta > 0 ? -1.0 : 1.0)
                : (a.Position.Y < b.Position.Y ? -1.0 : 1.0);

            if (shareA > 0)
            {
                a.Position += new Vec2(0, directionA * overlapY * shareA);
                a.Velocity = new Vec2(a.Velocity.X, 0);
            }

            if (shareB > 0)
            {
                b.Position += new Vec2(0, -directionA * overlapY * shareB);
                b.Velocity = new Vec2(b.Velocity.X, 0);
            }
        }
    }
}