namespace GameEngine.Demo.Tests;

// The rules, exercised a move at a time. Nothing here needs a frame, a texture or a key.
public class TetrisBoardTests
{
    private const int Seed = 20260905;

    private static TetrisBoard NewBoard() => new(Seed);

    private static List<(int X, int Y)> Cells(IEnumerable<(int X, int Y)> cells) =>
        cells.OrderBy(c => c.Y).ThenBy(c => c.X).ToList();

    // Fills the bottom row except for the columns the current piece will land in, so a hard
    // drop completes the line whichever piece the bag happened to deal.
    private static void SetUpLineClear(TetrisBoard board)
    {
        int lowest = board.CurrentCells().Max(c => c.Y);
        var landingColumns = board.CurrentCells().Where(c => c.Y == lowest).Select(c => c.X).ToHashSet();

        for (int x = 0; x < TetrisBoard.Columns; x++)
            board.SetCell(x, TetrisBoard.Rows - 1, landingColumns.Contains(x) ? null : TetrominoKind.T);
    }

    private static void ClearWell(TetrisBoard board)
    {
        for (int y = 0; y < TetrisBoard.Rows; y++)
        {
            for (int x = 0; x < TetrisBoard.Columns; x++)
                board.SetCell(x, y, null);
        }
    }

    private static int SettledCount(TetrisBoard board)
    {
        int count = 0;
        for (int y = 0; y < TetrisBoard.Rows; y++)
        {
            for (int x = 0; x < TetrisBoard.Columns; x++)
            {
                if (board.SettledAt(x, y) != null)
                    count++;
            }
        }
        return count;
    }

    [Test]
    public void APieceSpawnsInsideTheWell()
    {
        var board = NewBoard();

        Assert.Multiple(() =>
        {
            Assert.That(board.CurrentCells().Count(), Is.EqualTo(4));
            foreach (var (x, y) in board.CurrentCells())
            {
                Assert.That(x, Is.InRange(0, TetrisBoard.Columns - 1));
                Assert.That(y, Is.InRange(0, TetrisBoard.Rows - 1));
            }
            Assert.That(board.IsGameOver, Is.False);
        });
    }

    [Test]
    public void APieceCannotBeMovedThroughTheWalls()
    {
        var board = NewBoard();

        for (int i = 0; i < TetrisBoard.Columns * 2; i++)
            board.MoveLeft();

        Assert.That(board.CurrentCells().Min(c => c.X), Is.EqualTo(0));

        for (int i = 0; i < TetrisBoard.Columns * 2; i++)
            board.MoveRight();

        Assert.That(board.CurrentCells().Max(c => c.X), Is.EqualTo(TetrisBoard.Columns - 1));
    }

    [Test]
    public void MovingSideToSideReturnsThePieceToWhereItStarted()
    {
        var board = NewBoard();
        var before = Cells(board.CurrentCells());

        Assert.That(board.MoveLeft(), Is.True);
        Assert.That(board.MoveRight(), Is.True);

        Assert.That(Cells(board.CurrentCells()), Is.EqualTo(before));
    }

    [Test]
    public void FourRotationsComeBackToTheStartingShape()
    {
        var board = NewBoard();
        var before = Cells(board.CurrentCells());

        for (int i = 0; i < 4; i++)
            board.Rotate();

        Assert.Multiple(() =>
        {
            Assert.That(board.Rotation, Is.EqualTo(0));
            Assert.That(Cells(board.CurrentCells()), Is.EqualTo(before));
        });
    }

    [TestCase(TetrominoKind.I)]
    [TestCase(TetrominoKind.O)]
    [TestCase(TetrominoKind.T)]
    [TestCase(TetrominoKind.S)]
    [TestCase(TetrominoKind.Z)]
    [TestCase(TetrominoKind.J)]
    [TestCase(TetrominoKind.L)]
    public void EveryRotationOfEveryPieceHasFourDistinctCells(TetrominoKind kind)
    {
        for (int rotation = 0; rotation < 4; rotation++)
        {
            var cells = Tetromino.Cells(kind, rotation);

            Assert.Multiple(() =>
            {
                Assert.That(cells, Has.Count.EqualTo(4), $"{kind} rotation {rotation}");
                Assert.That(cells.Distinct().Count(), Is.EqualTo(4), $"{kind} rotation {rotation} repeats a cell");
                foreach (var (x, y) in cells)
                {
                    Assert.That(x, Is.InRange(0, Tetromino.BoxSize(kind) - 1));
                    Assert.That(y, Is.InRange(0, Tetromino.BoxSize(kind) - 1));
                }
            });
        }
    }

    [Test]
    public void TheIPieceIsFourLongLyingDownAndStandingUp()
    {
        var flat = Tetromino.Cells(TetrominoKind.I, 0);
        var upright = Tetromino.Cells(TetrominoKind.I, 1);

        Assert.Multiple(() =>
        {
            Assert.That(flat.Select(c => c.Y).Distinct().Count(), Is.EqualTo(1), "flat I spans one row");
            Assert.That(flat.Select(c => c.X).Distinct().Count(), Is.EqualTo(4), "flat I spans four columns");
            Assert.That(upright.Select(c => c.X).Distinct().Count(), Is.EqualTo(1), "upright I spans one column");
            Assert.That(upright.Select(c => c.Y).Distinct().Count(), Is.EqualTo(4), "upright I spans four rows");
        });
    }

    [Test]
    public void TheOPieceNeverChangesShape()
    {
        var spawn = Cells(Tetromino.Cells(TetrominoKind.O, 0));

        for (int rotation = 1; rotation < 4; rotation++)
            Assert.That(Cells(Tetromino.Cells(TetrominoKind.O, rotation)), Is.EqualTo(spawn));
    }

    [Test]
    public void AHardDroppedPieceLandsOnTheFloorAndSettles()
    {
        var board = NewBoard();

        board.HardDrop();

        int lowest = 0;
        for (int y = 0; y < TetrisBoard.Rows; y++)
        {
            for (int x = 0; x < TetrisBoard.Columns; x++)
            {
                if (board.SettledAt(x, y) != null)
                    lowest = Math.Max(lowest, y);
            }
        }

        Assert.Multiple(() =>
        {
            Assert.That(SettledCount(board), Is.EqualTo(4), "the piece settles as four cells");
            Assert.That(lowest, Is.EqualTo(TetrisBoard.Rows - 1), "nothing was in the way, so it reached the floor");
        });
    }

    [Test]
    public void APieceStacksOnTopOfWhatIsAlreadyThere()
    {
        var board = NewBoard();
        board.HardDrop();
        board.HardDrop();

        Assert.That(SettledCount(board), Is.EqualTo(8), "two pieces settled without overlapping");
    }

    [Test]
    public void TheGhostShowsWhereAHardDropWouldLand()
    {
        var board = NewBoard();
        var ghost = Cells(board.GhostCells());

        board.HardDrop();

        var settled = new List<(int X, int Y)>();
        for (int y = 0; y < TetrisBoard.Rows; y++)
        {
            for (int x = 0; x < TetrisBoard.Columns; x++)
            {
                if (board.SettledAt(x, y) != null)
                    settled.Add((x, y));
            }
        }

        Assert.That(Cells(settled), Is.EqualTo(ghost));
    }

    [Test]
    public void AFullRowClearsAndTheRowsAboveComeDown()
    {
        var board = NewBoard();
        SetUpLineClear(board);

        // A marker well above the action, in a column the piece cannot reach, to prove the
        // stack comes down by exactly the rows cleared.
        board.SetCell(0, TetrisBoard.Rows - 3, TetrominoKind.Z);

        board.HardDrop();

        Assert.Multiple(() =>
        {
            Assert.That(board.Lines, Is.EqualTo(1));
            Assert.That(board.SettledAt(0, TetrisBoard.Rows - 3), Is.Null, "the marker left its old row");
            Assert.That(board.SettledAt(0, TetrisBoard.Rows - 2), Is.EqualTo(TetrominoKind.Z),
                "the marker came down one row");
        });
    }

    [Test]
    public void ClearingALineScoresAndCountsIt()
    {
        var board = NewBoard();
        SetUpLineClear(board);

        board.HardDrop();

        Assert.Multiple(() =>
        {
            Assert.That(board.Lines, Is.EqualTo(1));
            Assert.That(board.Score, Is.GreaterThanOrEqualTo(100));
        });
    }

    [Test]
    public void TheLevelRisesEveryTenLines()
    {
        var board = NewBoard();
        Assert.That(board.Level, Is.EqualTo(0));

        while (board.Lines < 10 && !board.IsGameOver)
        {
            SetUpLineClear(board);
            board.HardDrop();
        }

        Assert.Multiple(() =>
        {
            Assert.That(board.Lines, Is.GreaterThanOrEqualTo(10));
            Assert.That(board.Level, Is.GreaterThanOrEqualTo(1));
            Assert.That(board.FallInterval, Is.LessThan(0.8));
        });
    }

    [Test]
    public void SoftDroppingScoresARowAtATime()
    {
        var board = NewBoard();
        int before = board.Score;

        Assert.That(board.SoftDrop(), Is.True);
        Assert.That(board.Score, Is.EqualTo(before + 1));
    }

    [Test]
    public void GravityLocksThePieceWhenItLands()
    {
        var board = NewBoard();

        while (board.StepDown()) { }

        Assert.That(SettledCount(board), Is.EqualTo(4), "the piece that landed is now part of the stack");
    }

    [Test]
    public void FillingTheWellEndsTheGame()
    {
        var board = NewBoard();

        for (int drop = 0; drop < 200 && !board.IsGameOver; drop++)
            board.HardDrop();

        Assert.That(board.IsGameOver, Is.True, "dropping pieces in one place should eventually top out");
    }

    [Test]
    public void AFinishedGameIgnoresFurtherMoves()
    {
        var board = NewBoard();
        for (int drop = 0; drop < 200 && !board.IsGameOver; drop++)
            board.HardDrop();

        var before = Cells(board.CurrentCells());

        Assert.Multiple(() =>
        {
            Assert.That(board.MoveLeft(), Is.False);
            Assert.That(board.MoveRight(), Is.False);
            Assert.That(board.Rotate(), Is.False);
            Assert.That(board.StepDown(), Is.False);
            Assert.That(Cells(board.CurrentCells()), Is.EqualTo(before));
        });
    }

    [Test]
    public void EverySevenPiecesContainsEachKindOnce()
    {
        var board = NewBoard();
        var drawn = new List<TetrominoKind> { board.Current };

        // The well is emptied between pieces so the run never tops out and stops dealing.
        for (int i = 0; i < 20; i++)
        {
            drawn.Add(board.Next);
            ClearWell(board);
            board.HardDrop();
        }

        Assert.That(board.IsGameOver, Is.False);

        foreach (var bag in drawn.Take(14).Chunk(7))
            Assert.That(bag.Distinct().Count(), Is.EqualTo(7), "a seven-bag holds each piece exactly once");
    }

    [Test]
    public void TheSameSeedPlaysOutTheSameWay()
    {
        var first = new TetrisBoard(Seed);
        var second = new TetrisBoard(Seed);

        for (int i = 0; i < 20; i++)
        {
            first.HardDrop();
            second.HardDrop();
        }

        Assert.Multiple(() =>
        {
            Assert.That(second.Score, Is.EqualTo(first.Score));
            Assert.That(second.Lines, Is.EqualTo(first.Lines));
            Assert.That(second.Current, Is.EqualTo(first.Current));
            Assert.That(Cells(second.CurrentCells()), Is.EqualTo(Cells(first.CurrentCells())));
        });
    }

    [Test]
    public void RotatingAgainstTheWallKicksThePieceInsteadOfFailing()
    {
        var board = new TetrisBoard(Seed);

        while (board.Current != TetrominoKind.I && !board.IsGameOver)
            board.HardDrop();

        Assert.That(board.IsGameOver, Is.False, "the bag should produce an I piece well before the well fills");

        board.Rotate();
        while (board.MoveLeft()) { }

        Assert.Multiple(() =>
        {
            Assert.That(board.Rotate(), Is.True, "an upright I against the left wall should kick and turn");
            Assert.That(board.CurrentCells().Min(c => c.X), Is.GreaterThanOrEqualTo(0));
        });
    }
}
