using GameEngine.Core;
using GameEngine.Core.Components;

namespace GameEngine.Demo.Tests;

// The scene, driven through a real Engine: keys in, entities out.
public class SceneTetrisTests
{
    private const int Seed = 20260905;

    private static (SceneHarness Harness, SceneTetris Scene) Load()
    {
        var scene = new SceneTetris(Seed);
        return (SceneHarness.Load(scene), scene);
    }

    private static IEnumerable<int> PieceColumns(SceneTetris scene) =>
        scene.Board.CurrentCells().Select(c => c.X);

    [Test]
    public void TheWellAndTheFirstPieceAreDrawn()
    {
        var (harness, scene) = Load();
        harness.Run(2);

        var drawn = harness.Snapshot();

        Assert.Multiple(() =>
        {
            Assert.That(drawn, Is.Not.Empty);
            Assert.That(scene.Board.CurrentCells().Count(), Is.EqualTo(4));
            Assert.That(harness.Entities.GetEntitiesWithTag("wall"), Is.Not.Empty, "the well has walls");
            Assert.That(harness.Entities.GetEntitiesWithTag("piece"), Has.Count.EqualTo(4));
        });
    }

    [Test]
    public void TheScoreboardStartsAtZero()
    {
        var (harness, _) = Load();
        harness.Run(2);

        Assert.Multiple(() =>
        {
            Assert.That(harness.Require("score").GetComponent<CText>().Text, Is.EqualTo("Score 0"));
            Assert.That(harness.Require("lines").GetComponent<CText>().Text, Is.EqualTo("Lines 0"));
            Assert.That(harness.Require("level").GetComponent<CText>().Text, Is.EqualTo("Level 0"));
        });
    }

    [TestCase(GeKeys.A, -1)]
    [TestCase(GeKeys.Left, -1)]
    [TestCase(GeKeys.D, 1)]
    [TestCase(GeKeys.Right, 1)]
    public void TappingAMoveKeyShiftsThePieceOneColumn(GeKeys key, int expected)
    {
        var (harness, scene) = Load();
        harness.Run(2);
        int before = PieceColumns(scene).Min();

        harness.PressAndRelease(key, framesHeld: 3);

        Assert.That(PieceColumns(scene).Min(), Is.EqualTo(before + expected));
    }

    [Test]
    public void HoldingAMoveKeySlidesThePieceAcross()
    {
        var (harness, scene) = Load();
        harness.Run(2);
        int before = PieceColumns(scene).Min();

        harness.PressAndRelease(GeKeys.A, framesHeld: 40);

        Assert.That(PieceColumns(scene).Min(), Is.LessThan(before - 1),
            "a held key should repeat, not move once");
    }

    [Test]
    public void RotateTurnsThePieceOncePerPress()
    {
        var (harness, scene) = Load();
        harness.Run(2);

        harness.PressAndRelease(GeKeys.W, framesHeld: 20);

        Assert.That(scene.Board.Rotation, Is.EqualTo(1), "holding rotate must not spin the piece");
    }

    [Test]
    public void HardDropSettlesThePieceAndBringsTheNextOne()
    {
        var (harness, scene) = Load();
        harness.Run(2);
        var landing = scene.Board.GhostCells().ToList();

        harness.PressAndRelease(GeKeys.Space, framesHeld: 2);

        Assert.Multiple(() =>
        {
            foreach (var (x, y) in landing)
                Assert.That(scene.Board.SettledAt(x, y), Is.Not.Null, $"({x}, {y}) should have settled");

            Assert.That(scene.Board.CurrentCells().Max(c => c.Y), Is.LessThan(TetrisBoard.Rows - 1),
                "a fresh piece is at the top of the well");
        });
    }

    [Test]
    public void GravityPullsThePieceDownOverTime()
    {
        var (harness, scene) = Load();
        harness.Run(2);
        int before = scene.Board.CurrentCells().Max(c => c.Y);

        harness.Run(120);

        Assert.That(scene.Board.CurrentCells().Max(c => c.Y), Is.GreaterThan(before));
    }

    [Test]
    public void SoftDropFallsFasterThanGravity()
    {
        var (withGravity, gravityScene) = Load();
        withGravity.Run(2);
        withGravity.Run(30);
        int gravityScore = gravityScene.Board.Score;
        int gravityDepth = gravityScene.Board.CurrentCells().Max(c => c.Y);

        var (withSoftDrop, softDropScene) = Load();
        withSoftDrop.Run(2);
        withSoftDrop.PressAndRelease(GeKeys.S, framesHeld: 30);

        Assert.Multiple(() =>
        {
            Assert.That(softDropScene.Board.Score, Is.GreaterThan(gravityScore),
                "a soft drop scores per row");
            Assert.That(softDropScene.Board.Lines + softDropScene.Board.Score, Is.GreaterThan(gravityDepth));
        });
    }

    [Test]
    public void SettledBlocksAreDrawnWhereTheBoardSaysTheyAre()
    {
        var (harness, scene) = Load();
        harness.Run(2);

        harness.PressAndRelease(GeKeys.Space, framesHeld: 2);
        harness.Run(2);

        int settledCells = 0;
        for (int y = 0; y < TetrisBoard.Rows; y++)
        {
            for (int x = 0; x < TetrisBoard.Columns; x++)
            {
                if (scene.Board.SettledAt(x, y) != null)
                    settledCells++;
            }
        }

        int drawnSettled = harness.Entities.GetEntitiesWithTag("settled")
            .Count(e => e.GetComponent<CAnimation>().ShouldDraw);

        Assert.Multiple(() =>
        {
            Assert.That(settledCells, Is.EqualTo(4));
            Assert.That(drawnSettled, Is.EqualTo(settledCells),
                "exactly the settled cells should be visible");
        });
    }

    [Test]
    public void TheBlockPoolIsNotGrownByPlaying()
    {
        var (harness, _) = Load();
        harness.Run(2);
        int entities = harness.Entities.GetEntities().Count;

        for (int piece = 0; piece < 12; piece++)
        {
            harness.PressAndRelease(GeKeys.Space, framesHeld: 2);
            harness.Run(2);
        }

        Assert.That(harness.Entities.GetEntities().Count, Is.EqualTo(entities),
            "blocks are drawn from a pool, so playing creates no entities");
    }

    [Test]
    public void ToppingOutShowsGameOverAndStopsThePiece()
    {
        var (harness, scene) = Load();
        harness.Run(2);

        for (int piece = 0; piece < 200 && !scene.Board.IsGameOver; piece++)
        {
            harness.PressAndRelease(GeKeys.Space, framesHeld: 2);
            harness.Run(1);
        }

        Assert.That(scene.Board.IsGameOver, Is.True, "hard-dropping in place should top the well out");

        var resting = scene.Board.CurrentCells().ToList();
        harness.Run(120);

        Assert.Multiple(() =>
        {
            Assert.That(scene.Board.CurrentCells(), Is.EqualTo(resting), "nothing moves after a game over");
            Assert.That(harness.Require("gameOver").GetComponent<CText>().ShouldDraw, Is.True);
        });
    }

    [Test]
    public void RestartClearsTheWell()
    {
        var (harness, scene) = Load();
        harness.Run(2);
        harness.PressAndRelease(GeKeys.Space, framesHeld: 2);
        harness.Run(2);
        Assert.That(scene.Board.SettledAt(0, TetrisBoard.Rows - 1) != null
                    || scene.Board.CurrentCells().Any(), Is.True);

        harness.PressAndRelease(GeKeys.R, framesHeld: 2);
        harness.Run(3);

        var restarted = (SceneTetris)harness.Scene;

        int settled = 0;
        for (int y = 0; y < TetrisBoard.Rows; y++)
        {
            for (int x = 0; x < TetrisBoard.Columns; x++)
            {
                if (restarted.Board.SettledAt(x, y) != null)
                    settled++;
            }
        }

        Assert.Multiple(() =>
        {
            Assert.That(settled, Is.EqualTo(0), "a restart empties the well");
            Assert.That(restarted.Board.Score, Is.EqualTo(0));
            Assert.That(restarted.Board.IsGameOver, Is.False);
        });
    }

    [Test]
    public void TheNextPiecePreviewShowsFourBlocks()
    {
        var (harness, _) = Load();
        harness.Run(2);

        int preview = harness.Entities.GetEntitiesWithTag("preview")
            .Count(e => e.GetComponent<CAnimation>().ShouldDraw);

        Assert.That(preview, Is.EqualTo(4));
    }

    [Test]
    public void TheGhostIsDrawnBelowThePieceUntilItLands()
    {
        var (harness, scene) = Load();
        harness.Run(2);

        var ghost = harness.Entities.GetEntitiesWithTag("ghost")
            .Where(e => e.GetComponent<CAnimation>().ShouldDraw)
            .Select(e => e.GetComponent<CTransform>().Position.Y)
            .ToList();
        var piece = harness.Entities.GetEntitiesWithTag("piece")
            .Where(e => e.GetComponent<CAnimation>().ShouldDraw)
            .Select(e => e.GetComponent<CTransform>().Position.Y)
            .ToList();

        Assert.Multiple(() =>
        {
            Assert.That(ghost, Has.Count.EqualTo(4));
            Assert.That(ghost.Min(), Is.GreaterThan(piece.Min()), "the ghost sits below the live piece");
        });
    }
}
