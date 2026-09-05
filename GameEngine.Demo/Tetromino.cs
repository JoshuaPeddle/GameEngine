using System;
using System.Collections.Generic;

namespace GameEngine.Demo
{
    public enum TetrominoKind { I, O, T, S, Z, J, L }

    /// <summary>
    /// The seven pieces and the four rotations of each. Spawn shapes are the standard ones,
    /// laid out inside a square box; rotating turns the box rather than the cells, which is
    /// what keeps an I piece four long and an O piece still.
    /// </summary>
    public static class Tetromino
    {
        public const int Kinds = 7;

        private static readonly (int X, int Y)[][] Spawn =
        [
            [(0, 1), (1, 1), (2, 1), (3, 1)], // I
            [(0, 0), (1, 0), (0, 1), (1, 1)], // O
            [(1, 0), (0, 1), (1, 1), (2, 1)], // T
            [(1, 0), (2, 0), (0, 1), (1, 1)], // S
            [(0, 0), (1, 0), (1, 1), (2, 1)], // Z
            [(0, 0), (0, 1), (1, 1), (2, 1)], // J
            [(2, 0), (0, 1), (1, 1), (2, 1)]  // L
        ];

        // I needs a 4x4 box to stay centred as it turns; O never turns, so its box is its own
        // size; everything else turns inside 3x3.
        private static readonly int[] BoxSizes = [4, 2, 3, 3, 3, 3, 3];

        private static readonly (int X, int Y)[][][] Rotations = BuildRotations();

        public static int BoxSize(TetrominoKind kind) => BoxSizes[(int)kind];

        /// <summary>The four cells of <paramref name="kind"/> at <paramref name="rotation"/>.</summary>
        public static IReadOnlyList<(int X, int Y)> Cells(TetrominoKind kind, int rotation) =>
            Rotations[(int)kind][((rotation % 4) + 4) % 4];

        private static (int X, int Y)[][][] BuildRotations()
        {
            var all = new (int X, int Y)[Kinds][][];

            for (int kind = 0; kind < Kinds; kind++)
            {
                all[kind] = new (int X, int Y)[4][];
                all[kind][0] = Spawn[kind];

                for (int rotation = 1; rotation < 4; rotation++)
                    all[kind][rotation] = RotateClockwise(all[kind][rotation - 1], BoxSizes[kind]);
            }

            return all;
        }

        private static (int X, int Y)[] RotateClockwise((int X, int Y)[] cells, int boxSize)
        {
            var turned = new (int X, int Y)[cells.Length];
            for (int i = 0; i < cells.Length; i++)
                turned[i] = (boxSize - 1 - cells[i].Y, cells[i].X);

            Array.Sort(turned, static (a, b) => a.Y != b.Y ? a.Y - b.Y : a.X - b.X);
            return turned;
        }
    }
}
