using GameEngine.Core.Components;

namespace GameEngine.Core.Systems
{
    public readonly struct CollisionEvent
    {
        public readonly Entity A;
        public readonly Entity B;
        public readonly Vec2 Overlap;
        public readonly Vec2 VelocityA;
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

        private const double MinimumCellSize = 1.0;

        private readonly List<(Entity entity, CBoundingBox bbox, CTransform transform)> _validEntities = new(1000);
        private readonly List<(Entity entity, CGravity gravity, CTransform transform)> _entitiesWithGravity = new(100);

        private readonly Dictionary<long, List<int>> _grid = new(1024);
        private readonly List<int> _candidates = new(64);
        private int[] _lastPairedWith = [];
        private double _cellSize = MinimumCellSize;

        public void Update(EntityManager entityManager, double deltaSeconds)
        {
            ResetEntityLists();
            PopulateEntityLists(entityManager);
            ProcessGravity(deltaSeconds);
            ProcessCollisions();
        }

        private void ProcessCollisions()
        {
            int count = _validEntities.Count;
            if (count < 2)
                return;

            BuildSpatialGrid();

            if (_lastPairedWith.Length < count)
                _lastPairedWith = new int[Math.Max(count, _lastPairedWith.Length * 2)];
            Array.Fill(_lastPairedWith, -1, 0, count);

            for (int i = 0; i < count; i++)
            {
                (Entity entity, CBoundingBox bbox, CTransform transform) entityA = _validEntities[i];

                CollectCandidates(i, entityA.transform.Position, entityA.bbox.Size);
                _candidates.Sort();

                foreach (int j in _candidates)
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

        private void BuildSpatialGrid()
        {
            foreach (var bucket in _grid.Values)
                bucket.Clear();

            _cellSize = ChooseCellSize();

            for (int i = 0; i < _validEntities.Count; i++)
            {
                var (_, bbox, transform) = _validEntities[i];
                CellRange(transform.Position, bbox.Size, out int minX, out int minY, out int maxX, out int maxY);

                for (int cellY = minY; cellY <= maxY; cellY++)
                {
                    for (int cellX = minX; cellX <= maxX; cellX++)
                    {
                        long key = CellKey(cellX, cellY);
                        if (!_grid.TryGetValue(key, out var bucket))
                        {
                            bucket = new List<int>(8);
                            _grid[key] = bucket;
                        }
                        bucket.Add(i);
                    }
                }
            }
        }

        private double ChooseCellSize()
        {
            double largestExtent = MinimumCellSize;

            foreach (var (_, bbox, _) in _validEntities)
            {
                if (bbox.Size.X > largestExtent) largestExtent = bbox.Size.X;
                if (bbox.Size.Y > largestExtent) largestExtent = bbox.Size.Y;
            }

            return largestExtent;
        }

        private void CollectCandidates(int index, Vec2 position, Vec2 size)
        {
            _candidates.Clear();
            CellRange(position, size, out int minX, out int minY, out int maxX, out int maxY);

            for (int cellY = minY; cellY <= maxY; cellY++)
            {
                for (int cellX = minX; cellX <= maxX; cellX++)
                {
                    if (!_grid.TryGetValue(CellKey(cellX, cellY), out var bucket))
                        continue;

                    foreach (int other in bucket)
                    {
                        if (other <= index || _lastPairedWith[other] == index)
                            continue;

                        _lastPairedWith[other] = index;
                        _candidates.Add(other);
                    }
                }
            }
        }

        private void CellRange(Vec2 position, Vec2 size, out int minX, out int minY, out int maxX, out int maxY)
        {
            minX = (int)Math.Floor(position.X / _cellSize);
            minY = (int)Math.Floor(position.Y / _cellSize);
            maxX = (int)Math.Floor((position.X + Math.Max(size.X, 0)) / _cellSize);
            maxY = (int)Math.Floor((position.Y + Math.Max(size.Y, 0)) / _cellSize);
        }

        private static long CellKey(int cellX, int cellY) =>
            ((long)cellX << 32) ^ (uint)cellY;

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

        private static void ResolveCollision(
            CBoundingBox boxA, CTransform transformA,
            CBoundingBox boxB, CTransform transformB,
            in Vec2 overlap)
        {
            if (!boxA.BlockMovement && !boxB.BlockMovement)
                return;

            if (overlap.X < overlap.Y)
                SeparateOnX(transformA, boxA.BlockMovement, transformB, boxB.BlockMovement, overlap.X);
            else
                SeparateOnY(transformA, boxA.BlockMovement, transformB, boxB.BlockMovement, overlap.Y);
        }

        private static void GetSeparationShares(
            bool aSolid, bool bSolid, double deltaA, double deltaB,
            out double shareA, out double shareB)
        {
            if (bSolid && !aSolid) { shareA = 1.0; shareB = 0.0; return; }
            if (aSolid && !bSolid) { shareA = 0.0; shareB = 1.0; return; }

            bool movedA = Math.Abs(deltaA) > epsilon;
            bool movedB = Math.Abs(deltaB) > epsilon;

            if (movedA && !movedB) { shareA = 1.0; shareB = 0.0; return; }
            if (movedB && !movedA) { shareA = 0.0; shareB = 1.0; return; }

            shareA = 0.5;
            shareB = 0.5;
        }

        private static void SeparateOnX(CTransform a, bool aSolid, CTransform b, bool bSolid, double overlapX)
        {
            double deltaA = a.Position.X - a.PreviousPosition.X;
            double deltaB = b.Position.X - b.PreviousPosition.X;

            GetSeparationShares(aSolid, bSolid, deltaA, deltaB, out double shareA, out double shareB);

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