using System;
using System.Collections.Generic;

namespace GameEngine.Demo
{
    /// <summary>
    /// The rules of Tetris, with no engine types in sight. The scene owns one of these and
    /// draws whatever it says; every rule in here is decidable without a frame, a texture or a
    /// key, which is what makes the game testable a move at a time.
    /// </summary>
    public sealed class TetrisBoard
    {
        public const int Columns = 10;
        public const int Rows = 20;

        // A row above the well for pieces to spawn into, so a piece that cannot enter is a
        // game over rather than an immediate overlap.
        private const int SpawnY = -2;

        private static readonly int[] LineScores = [0, 100, 300, 500, 800];

        private readonly int?[,] settled = new int?[Columns, Rows];
        private readonly List<TetrominoKind> bag = [];
        private readonly Random random;

        public TetrisBoard(int seed)
        {
            random = new Random(seed);
            Next = TakeFromBag();
            Spawn();
        }

        public TetrominoKind Current { get; private set; }

        public TetrominoKind Next { get; private set; }

        public int Rotation { get; private set; }

        public int PieceX { get; private set; }

        public int PieceY { get; private set; }

        public int Score { get; private set; }

        public int Lines { get; private set; }

        public int Level => Lines / 10;

        public bool IsGameOver { get; private set; }

        /// <summary>Seconds between gravity steps at the current level.</summary>
        public double FallInterval => Math.Max(0.07, 0.8 - Level * 0.07);

        /// <summary>The piece kind settled in a cell, or null when it is empty.</summary>
        public TetrominoKind? SettledAt(int x, int y) =>
            settled[x, y] is { } kind ? (TetrominoKind?)kind : null;

        public IEnumerable<(int X, int Y)> CurrentCells() => CellsAt(PieceX, PieceY, Rotation);

        /// <summary>Where the current piece would land if it dropped now — the ghost piece.</summary>
        public IEnumerable<(int X, int Y)> GhostCells()
        {
            int y = PieceY;
            while (Fits(PieceX, y + 1, Rotation))
                y++;

            return CellsAt(PieceX, y, Rotation);
        }

        public bool MoveLeft() => TryMove(-1, 0);

        public bool MoveRight() => TryMove(1, 0);

        /// <summary>One gravity step. Locks the piece when it has landed.</summary>
        public bool StepDown()
        {
            if (IsGameOver)
                return false;

            if (TryMove(0, 1))
                return true;

            Lock();
            return false;
        }

        /// <summary>A player-driven step down, which scores a point per row.</summary>
        public bool SoftDrop()
        {
            if (IsGameOver)
                return false;

            if (!TryMove(0, 1))
            {
                Lock();
                return false;
            }

            Score += 1;
            return true;
        }

        /// <summary>Drops the piece as far as it goes and locks it there.</summary>
        public void HardDrop()
        {
            if (IsGameOver)
                return;

            int dropped = 0;
            while (TryMove(0, 1))
                dropped++;

            Score += dropped * 2;
            Lock();
        }

        // Rotating against a wall or a stack would normally fail; nudging the piece a column or
        // two and trying again is what lets a player spin out of a tight gap.
        private static readonly int[] KickOffsets = [0, -1, 1, -2, 2];

        public bool Rotate()
        {
            if (IsGameOver)
                return false;

            int rotated = (Rotation + 1) % 4;

            foreach (int kick in KickOffsets)
            {
                if (!Fits(PieceX + kick, PieceY, rotated))
                    continue;

                PieceX += kick;
                Rotation = rotated;
                return true;
            }

            return false;
        }

        private bool TryMove(int dx, int dy)
        {
            if (IsGameOver || !Fits(PieceX + dx, PieceY + dy, Rotation))
                return false;

            PieceX += dx;
            PieceY += dy;
            return true;
        }

        private void Lock()
        {
            foreach (var (x, y) in CurrentCells())
            {
                if (y >= 0)
                    settled[x, y] = (int)Current;
            }

            int cleared = ClearFullRows();
            Lines += cleared;
            Score += LineScores[Math.Min(cleared, LineScores.Length - 1)] * (Level + 1);

            Spawn();
        }

        private int ClearFullRows()
        {
            int write = Rows - 1;

            for (int read = Rows - 1; read >= 0; read--)
            {
                if (IsRowFull(read))
                    continue;

                if (write != read)
                {
                    for (int x = 0; x < Columns; x++)
                        settled[x, write] = settled[x, read];
                }

                write--;
            }

            int cleared = write + 1;

            for (int y = write; y >= 0; y--)
            {
                for (int x = 0; x < Columns; x++)
                    settled[x, y] = null;
            }

            return cleared;
        }

        private bool IsRowFull(int y)
        {
            for (int x = 0; x < Columns; x++)
            {
                if (settled[x, y] == null)
                    return false;
            }

            return true;
        }

        private void Spawn()
        {
            Current = Next;
            Next = TakeFromBag();
            Rotation = 0;
            PieceX = (Columns - Tetromino.BoxSize(Current)) / 2;
            PieceY = SpawnY;

            // Drop the piece into view before deciding anything: a spawn fails only when the
            // stack has reached the top, not merely because the piece starts above the well.
            while (PieceY < 0 && Fits(PieceX, PieceY + 1, Rotation))
                PieceY++;

            if (PieceY < 0 || !Fits(PieceX, PieceY, Rotation))
                IsGameOver = true;
        }

        // A seven-bag: every piece appears once before any appears twice, which is what stops a
        // run of the same piece or a long drought of the one you need.
        private TetrominoKind TakeFromBag()
        {
            if (bag.Count == 0)
            {
                for (int kind = 0; kind < Tetromino.Kinds; kind++)
                    bag.Add((TetrominoKind)kind);

                for (int i = bag.Count - 1; i > 0; i--)
                {
                    int j = random.Next(i + 1);
                    (bag[i], bag[j]) = (bag[j], bag[i]);
                }
            }

            var taken = bag[^1];
            bag.RemoveAt(bag.Count - 1);
            return taken;
        }

        private IEnumerable<(int X, int Y)> CellsAt(int originX, int originY, int rotation)
        {
            foreach (var (x, y) in Tetromino.Cells(Current, rotation))
                yield return (originX + x, originY + y);
        }

        private bool Fits(int originX, int originY, int rotation)
        {
            foreach (var (x, y) in CellsAt(originX, originY, rotation))
            {
                if (x < 0 || x >= Columns || y >= Rows)
                    return false;

                // Above the well is open air: a piece may be part way in.
                if (y >= 0 && settled[x, y] != null)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Puts a block into a cell, or clears it. This is how a starting position is set up —
        /// by a test, or by a scene that wants the well to begin part full.
        /// </summary>
        public void SetCell(int x, int y, TetrominoKind? kind)
        {
            if (x < 0 || x >= Columns || y < 0 || y >= Rows)
                throw new ArgumentOutOfRangeException(nameof(x), $"({x}, {y}) is outside the well.");

            settled[x, y] = kind is { } present ? (int)present : null;
        }
    }
}
