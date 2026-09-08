using GameEngine.Core;
using GameEngine.Core.Components;
using GameEngine.Core.Systems;
using SkiaSharp;
using System;
using System.Collections.Generic;

namespace GameEngine.Demo
{
    /// <summary>
    /// Tetris. The rules live in <see cref="TetrisBoard"/>; this draws whatever the board says
    /// and turns keys into moves. Blocks are drawn from a pool of entities that are repositioned
    /// and hidden rather than spawned and destroyed, so a hundred-line game creates no entities
    /// after the first frame.
    /// </summary>
    public class SceneTetris : Scene
    {
        private const int Cell = 32;
        private const int WellX = 240;
        private const int WellY = 64;
        private const int PreviewX = 620;
        private const int PreviewY = 160;

        // How long a held left or right waits before it starts repeating, and how fast it
        // repeats once it does — the difference between nudging a piece and sliding it.
        private const double MoveRepeatDelay = 0.17;
        private const double MoveRepeatInterval = 0.045;
        private const double SoftDropInterval = 0.04;

        private static readonly string[] BlockAnimations =
            ["TetrisI", "TetrisO", "TetrisT", "TetrisS", "TetrisZ", "TetrisJ", "TetrisL"];

        private readonly int _seed;

        private Assets? assets;
        private Assets Assets => assets
            ?? throw new InvalidOperationException("SceneTetris has not been initialized.");

        private TetrisBoard board = null!;
        private Action<Scene?>? resetScene;

        private readonly List<Entity> settledBlocks = [];
        private readonly List<Entity> pieceBlocks = [];
        private readonly List<Entity> ghostBlocks = [];
        private readonly List<Entity> previewBlocks = [];

        private Entity inputCarrier = null!;
        private CText scoreText = null!;
        private CText linesText = null!;
        private CText levelText = null!;
        private CText gameOverText = null!;

        private bool moveLeftHeld;
        private bool moveRightHeld;
        private bool softDropHeld;
        private double moveRepeatTimer;
        private double softDropTimer;
        private double fallTimer;

        public SceneTetris() : this(Environment.TickCount) { }

        public SceneTetris(int seed) => _seed = seed;

        public override int VirtualWidth => 800;

        public override int VirtualHeight => 800;

        /// <summary>The rules the scene is drawing, for tests and for a host that wants the score.</summary>
        public TetrisBoard Board => board;

        public override void Initialize(
            EntityManager entityManager,
            InputManager inputManager,
            AudioSystem? audioPlayer,
            Action<Scene?> ResetScene)
        {
            assets = LoadAssets("assets.json");
            board = new TetrisBoard(_seed);
            resetScene = ResetScene;

            moveLeftHeld = moveRightHeld = softDropHeld = false;
            moveRepeatTimer = softDropTimer = fallTimer = 0;

            BuildWell(entityManager);
            BuildBlockPools(entityManager);
            BuildLabels(entityManager);
            BindInput(inputManager);

            Redraw();
        }

        private void BuildWell(EntityManager entityManager)
        {
            var wall = Assets.GetAnimation("TetrisWall");

            for (int y = -1; y <= TetrisBoard.Rows; y++)
            {
                AddWallBlock(entityManager, wall, -1, y);
                AddWallBlock(entityManager, wall, TetrisBoard.Columns, y);
            }

            for (int x = 0; x < TetrisBoard.Columns; x++)
                AddWallBlock(entityManager, wall, x, TetrisBoard.Rows);
        }

        private static void AddWallBlock(EntityManager entityManager, Animation wall, int x, int y)
        {
            var block = entityManager.CreateEntity("wall");
            block.AddComponent(new CTransform(CellCentre(x, y)) { Layer = 0 });
            block.AddComponent(new CAnimation(wall));
        }

        private void BuildBlockPools(EntityManager entityManager)
        {
            for (int i = 0; i < TetrisBoard.Columns * TetrisBoard.Rows; i++)
                settledBlocks.Add(CreatePooledBlock(entityManager, "settled", layer: 2));

            for (int i = 0; i < 4; i++)
                ghostBlocks.Add(CreatePooledBlock(entityManager, "ghost", layer: 1));

            for (int i = 0; i < 4; i++)
                pieceBlocks.Add(CreatePooledBlock(entityManager, "piece", layer: 3));

            for (int i = 0; i < 4; i++)
                previewBlocks.Add(CreatePooledBlock(entityManager, "preview", layer: 2));
        }

        private Entity CreatePooledBlock(EntityManager entityManager, string tag, int layer)
        {
            var block = entityManager.CreateEntity(tag);
            block.AddComponent(new CTransform(Vec2.Zero) { Layer = layer });
            block.AddComponent(new CAnimation(Assets.GetAnimation(BlockAnimations[0])) { ShouldDraw = false });
            return block;
        }

        private void BuildLabels(EntityManager entityManager)
        {
            inputCarrier = AddLabelEntity(entityManager, "score", new Vec2(PreviewX, 380), "Score 0", 28);
            scoreText = inputCarrier.GetComponent<CText>();
            linesText = AddLabel(entityManager, "lines", new Vec2(PreviewX, 420), "Lines 0", 24);
            levelText = AddLabel(entityManager, "level", new Vec2(PreviewX, 456), "Level 0", 24);

            AddLabel(entityManager, "title", new Vec2(400, 40), "TETRIS", 32);
            AddLabel(entityManager, "nextLabel", new Vec2(PreviewX, 120), "NEXT", 24);
            AddLabel(entityManager, "hint", new Vec2(400, 776),
                "A/D move   W rotate   S drop   Space hard drop   R restart", 18);

            gameOverText = AddLabel(entityManager, "gameOver", new Vec2(400, 400), "GAME OVER — press R", 40);
            gameOverText.ShouldDraw = false;
            gameOverText.Paint.Color = SKColors.DarkRed;
        }

        private static CText AddLabel(
            EntityManager entityManager, string tag, Vec2 position, string text, int size) =>
            AddLabelEntity(entityManager, tag, position, text, size).GetComponent<CText>();

        private static Entity AddLabelEntity(
            EntityManager entityManager, string tag, Vec2 position, string text, int size)
        {
            var entity = entityManager.CreateEntity(tag);
            entity.AddComponent(new CTransform(position) { Layer = 4 });
            entity.AddComponent(new CText(text, size));
            return entity;
        }

        private void BindInput(InputManager inputManager)
        {
            inputManager.AddAction(GeKeys.A, "Left");
            inputManager.BindGestureAction(PointerGesture.Left, "Left");
            inputManager.AddAction(GeKeys.Left, "Left");
            inputManager.AddAction(GeKeys.D, "Right");
            inputManager.BindGestureAction(PointerGesture.Right, "Right");
            inputManager.AddAction(GeKeys.Right, "Right");
            inputManager.AddAction(GeKeys.S, "SoftDrop");
            inputManager.BindGestureAction(PointerGesture.Down, "SoftDrop");
            inputManager.AddAction(GeKeys.Down, "SoftDrop");
            inputManager.AddAction(GeKeys.W, "Rotate");
            inputManager.BindGestureAction(PointerGesture.Up, "Rotate");
            inputManager.AddAction(GeKeys.Up, "Rotate");
            inputManager.AddAction(GeKeys.Space, "HardDrop");
            inputManager.BindGestureAction(PointerGesture.Tap, "HardDrop");
            inputManager.AddAction(GeKeys.R, "Restart");

            // Held keys drive their own repeat in Update, so they read the flags rather than
            // acting per callback; the discrete ones fire once per press.
            inputManager.BindAction("Left", held =>
            {
                if (held && !moveLeftHeld)
                {
                    board.MoveLeft();
                    moveRepeatTimer = 0;
                }
                moveLeftHeld = held;
            });

            inputManager.BindAction("Right", held =>
            {
                if (held && !moveRightHeld)
                {
                    board.MoveRight();
                    moveRepeatTimer = 0;
                }
                moveRightHeld = held;
            });

            inputManager.BindAction("SoftDrop", held =>
            {
                if (held && !softDropHeld)
                {
                    board.SoftDrop();
                    softDropTimer = 0;
                    fallTimer = 0;
                }
                softDropHeld = held;
            });

            BindOneShot(inputManager, "Rotate", () => board.Rotate());
            BindOneShot(inputManager, "HardDrop", () =>
            {
                board.HardDrop();
                fallTimer = 0;
            });
            BindOneShot(inputManager, "Restart", () => resetScene?.Invoke(null));
        }

        // MapActionToComponent gives the one-shot edge detection. The entity it is mapped to is
        // held from when it was created rather than looked up, because entities a scene creates
        // are not visible to a tag query until the manager's next update.
        private void BindOneShot(InputManager inputManager, string action, Action onPress)
        {
            inputManager.ActionMapper.MapActionToComponent<CText>(action, inputCarrier, (_, isActive) =>
            {
                if (isActive)
                    onPress();
            }, oneShot: true);
        }

        public override void Update(EntityManager entityManager, SystemContainer systems, double deltaSeconds)
        {
            if (board.IsGameOver)
            {
                gameOverText.ShouldDraw = true;
                Redraw();
                return;
            }

            ApplyHeldMovement(deltaSeconds);
            ApplyGravity(deltaSeconds);
            Redraw();
        }

        private void ApplyHeldMovement(double deltaSeconds)
        {
            if (moveLeftHeld == moveRightHeld)
            {
                moveRepeatTimer = 0;
                return;
            }

            moveRepeatTimer += deltaSeconds;
            if (moveRepeatTimer < MoveRepeatDelay)
                return;

            while (moveRepeatTimer >= MoveRepeatDelay + MoveRepeatInterval)
            {
                moveRepeatTimer -= MoveRepeatInterval;

                if (moveLeftHeld)
                    board.MoveLeft();
                else
                    board.MoveRight();
            }
        }

        private void ApplyGravity(double deltaSeconds)
        {
            if (softDropHeld)
            {
                softDropTimer += deltaSeconds;
                while (softDropTimer >= SoftDropInterval && !board.IsGameOver)
                {
                    softDropTimer -= SoftDropInterval;
                    board.SoftDrop();
                }

                fallTimer = 0;
                return;
            }

            softDropTimer = 0;
            fallTimer += deltaSeconds;

            while (fallTimer >= board.FallInterval && !board.IsGameOver)
            {
                fallTimer -= board.FallInterval;
                board.StepDown();
            }
        }

        private void Redraw()
        {
            DrawSettled();
            DrawCells(ghostBlocks, board.GhostCells(), board.Current, ghost: true);
            DrawCells(pieceBlocks, board.CurrentCells(), board.Current, ghost: false);
            DrawPreview();

            scoreText.Text = $"Score {board.Score}";
            linesText.Text = $"Lines {board.Lines}";
            levelText.Text = $"Level {board.Level}";
        }

        private void DrawSettled()
        {
            int used = 0;

            for (int y = 0; y < TetrisBoard.Rows; y++)
            {
                for (int x = 0; x < TetrisBoard.Columns; x++)
                {
                    if (board.SettledAt(x, y) is not { } kind)
                        continue;

                    Place(settledBlocks[used++], CellCentre(x, y), kind, ghost: false);
                }
            }

            Hide(settledBlocks, from: used);
        }

        private void DrawCells(
            List<Entity> pool, IEnumerable<(int X, int Y)> cells, TetrominoKind kind, bool ghost)
        {
            int used = 0;

            foreach (var (x, y) in cells)
            {
                // A piece entering the well is part way above it; those cells are simply not drawn.
                if (y < 0 || used >= pool.Count)
                    continue;

                Place(pool[used++], CellCentre(x, y), kind, ghost);
            }

            Hide(pool, from: used);
        }

        private void DrawPreview()
        {
            var cells = Tetromino.Cells(board.Next, 0);
            int box = Tetromino.BoxSize(board.Next);
            int used = 0;

            foreach (var (x, y) in cells)
            {
                var centre = new Vec2(
                    PreviewX + (x - box / 2.0 + 0.5) * Cell,
                    PreviewY + (y - box / 2.0 + 0.5) * Cell);

                Place(previewBlocks[used++], centre, board.Next, ghost: false);
            }

            Hide(previewBlocks, from: used);
        }

        private void Place(Entity block, Vec2 centre, TetrominoKind kind, bool ghost)
        {
            block.GetComponent<CTransform>().Position = centre;

            // A ghost is the same block drawn smaller, which reads as an outline without
            // needing a second set of textures.
            block.GetComponent<CTransform>().Scale = ghost ? new Vec2(0.45, 0.45) : new Vec2(1, 1);

            var animation = block.GetComponent<CAnimation>();
            animation.Animation = Assets.GetAnimation(BlockAnimations[(int)kind]);
            animation.ShouldDraw = true;
        }

        private static void Hide(List<Entity> pool, int from)
        {
            for (int i = from; i < pool.Count; i++)
                pool[i].GetComponent<CAnimation>().ShouldDraw = false;
        }

        private static Vec2 CellCentre(int x, int y) =>
            new(WellX + x * Cell + Cell / 2.0, WellY + y * Cell + Cell / 2.0);
    }
}
