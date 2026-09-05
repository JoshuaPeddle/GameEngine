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

        // A cell is stamped with the frame that last put anything in it, so a still world
        // re-uses its cells without touching the dictionary and a moving one clears only what
        // it actually occupies. Cells nothing occupied this frame are dropped once the map has
        // drifted well past current demand, which is what keeps a world that travels a long
        // way from accumulating a bucket for every cell it has ever crossed. Dropped cells go
        // back to a free list, so the retained storage is a bounded high-water pool rather
        // than a per-frame allocation.
        private sealed class Cell
        {
            public readonly List<int> Items = new(8);
            public int Frame = -1;
        }

        private const int PruneSlack = 64;

        private readonly Dictionary<long, Cell> _cells = new(1024);
        private readonly List<Cell> _occupied = new(1024);
        private readonly Stack<Cell> _freeCells = new();
        private readonly List<long> _pruneScratch = new(1024);
        private int _frame;

        private readonly List<int> _candidates = new(64);
        private int[] _lastPairedWith = [];
        private double _cellSize = MinimumCellSize;

        /// <summary>Cells the broad phase keeps mapped: bounded by demand plus a fixed slack.</summary>
        public int RetainedBroadPhaseCells => _cells.Count;

        /// <summary>Cells occupied by the colliders of the frame just built.</summary>
        public int OccupiedBroadPhaseCells => _occupied.Count;

        public void Update(EntityManager entityManager, double deltaSeconds)
        {
            ResetEntityLists();
            PopulateEntityLists(entityManager);
            ProcessGravity(deltaSeconds);
            ProcessCollisions();
            DropCellsNothingOccupies();
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
                            entityB.bbox, entityB.transform);
                    }
                }
            }
        }

        private void BuildSpatialGrid()
        {
            _cellSize = ChooseCellSize();

            for (int i = 0; i < _validEntities.Count; i++)
            {
                var (_, bbox, transform) = _validEntities[i];
                CellRange(transform.Position, bbox.Size, out int minX, out int minY, out int maxX, out int maxY);

                for (int cellY = minY; cellY <= maxY; cellY++)
                {
                    for (int cellX = minX; cellX <= maxX; cellX++)
                        BucketFor(CellKey(cellX, cellY)).Add(i);
                }
            }
        }

        private void ReleaseOccupiedCells()
        {
            _occupied.Clear();
            _frame++;
        }

        private List<int> BucketFor(long key)
        {
            if (!_cells.TryGetValue(key, out var cell))
            {
                cell = _freeCells.Count > 0 ? _freeCells.Pop() : new Cell();
                _cells[key] = cell;
            }

            if (cell.Frame != _frame)
            {
                cell.Items.Clear();
                cell.Frame = _frame;
                _occupied.Add(cell);
            }

            return cell.Items;
        }

        private void DropCellsNothingOccupies()
        {
            if (_cells.Count <= 2 * _occupied.Count + PruneSlack)
                return;

            foreach (var pair in _cells)
            {
                if (pair.Value.Frame != _frame)
                    _pruneScratch.Add(pair.Key);
            }

            foreach (var key in _pruneScratch)
            {
                if (_cells.Remove(key, out var cell))
                    _freeCells.Push(cell);
            }

            _pruneScratch.Clear();
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
                    if (!_cells.TryGetValue(CellKey(cellX, cellY), out var cell) || cell.Frame != _frame)
                        continue;

                    foreach (int other in cell.Items)
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
            ReleaseOccupiedCells();
            CollisionEvents.Clear();
            _validEntities.Clear();
            _entitiesWithGravity.Clear();
        }

        private static void ResolveCollision(
            CBoundingBox boxA, CTransform transformA,
            CBoundingBox boxB, CTransform transformB)
        {
            if (!boxA.BlockMovement && !boxB.BlockMovement)
                return;

            double deltaX = transformA.Position.X - transformA.PreviousPosition.X
                          - (transformB.Position.X - transformB.PreviousPosition.X);
            double deltaY = transformA.Position.Y - transformA.PreviousPosition.Y
                          - (transformB.Position.Y - transformB.PreviousPosition.Y);

            // Intersection width/height is not a penetration depth when one rectangle can
            // contain the other (for example SceneBasic's 50x80 Jeep and 20x20 grenades).
            // Compute the actual signed distance to each exit edge instead.
            double separationX = MinimumAxisSeparation(
                transformA.Position.X, boxA.Size.X,
                transformB.Position.X, boxB.Size.X,
                deltaX);
            double separationY = MinimumAxisSeparation(
                transformA.Position.Y, boxA.Size.Y,
                transformB.Position.Y, boxB.Size.Y,
                deltaY);

            double magnitudeX = Math.Abs(separationX);
            double magnitudeY = Math.Abs(separationY);
            bool separateOnX = magnitudeX < magnitudeY
                || (Math.Abs(magnitudeX - magnitudeY) <= epsilon
                    && Math.Abs(deltaX) > Math.Abs(deltaY));

            if (separateOnX)
                SeparateOnX(transformA, boxA.BlockMovement, transformB, boxB.BlockMovement, separationX);
            else
                SeparateOnY(transformA, boxA.BlockMovement, transformB, boxB.BlockMovement, separationY);
        }

        private static double MinimumAxisSeparation(
            double positionA, double sizeA,
            double positionB, double sizeB,
            double relativeDelta)
        {
            double towardNegative = positionB - (positionA + sizeA);
            double towardPositive = positionB + sizeB - positionA;
            double negativeMagnitude = Math.Abs(towardNegative);
            double positiveMagnitude = Math.Abs(towardPositive);

            if (negativeMagnitude < positiveMagnitude)
                return towardNegative;
            if (positiveMagnitude < negativeMagnitude)
                return towardPositive;

            // Equal-depth containment is ambiguous from the current rectangles alone.
            // Back out against relative motion; stationary coincident bodies use centers.
            if (relativeDelta > epsilon)
                return towardNegative;
            if (relativeDelta < -epsilon)
                return towardPositive;

            double centerA = positionA + sizeA * 0.5;
            double centerB = positionB + sizeB * 0.5;
            return centerA <= centerB ? towardNegative : towardPositive;
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

        private static void SeparateOnX(CTransform a, bool aSolid, CTransform b, bool bSolid, double separationA)
        {
            double deltaA = a.Position.X - a.PreviousPosition.X;
            double deltaB = b.Position.X - b.PreviousPosition.X;

            GetSeparationShares(aSolid, bSolid, deltaA, deltaB, out double shareA, out double shareB);

            if (shareA > 0)
            {
                a.Position += new Vec2(separationA * shareA, 0);
                a.Velocity = new Vec2(0, a.Velocity.Y);
            }

            if (shareB > 0)
            {
                b.Position += new Vec2(-separationA * shareB, 0);
                b.Velocity = new Vec2(0, b.Velocity.Y);
            }
        }

        private static void SeparateOnY(CTransform a, bool aSolid, CTransform b, bool bSolid, double separationA)
        {
            double deltaA = a.Position.Y - a.PreviousPosition.Y;
            double deltaB = b.Position.Y - b.PreviousPosition.Y;

            GetSeparationShares(aSolid, bSolid, deltaA, deltaB, out double shareA, out double shareB);

            if (shareA > 0)
            {
                a.Position += new Vec2(0, separationA * shareA);
                a.Velocity = new Vec2(a.Velocity.X, 0);
            }

            if (shareB > 0)
            {
                b.Position += new Vec2(0, -separationA * shareB);
                b.Velocity = new Vec2(b.Velocity.X, 0);
            }
        }
    }
}
