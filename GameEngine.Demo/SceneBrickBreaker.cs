using GameEngine.Core;
using GameEngine.Core.Components;
using GameEngine.Core.Systems;
using System;

namespace GameEngine.Demo
{
    // Flow of the game:
    // 1. Ball is at the bottom of the screen
    // 2. Player presses the screen
    // 3. Aimer starts at the ball's position and points towards the press position
    // 4. Player releases the screen
    // 5. Ball moves in the direction of the aimer

    class SceneBrickBreaker : Scene
    {
        private Assets? assets;
        public override int VirtualWidth => 600;
        public override int VirtualHeight => 1000;

        private const int _wallThickness = 10;

        private GameStates GameState { get; set; } = GameStates.Aiming;

        public override void Initialize(EntityManager entityManager, InputManager inputManager, AudioSystem audioPlayer, Action<Scene?> ResetScene)
        {
            assets ??= new("assets.txt");

            CreateBall(entityManager);

            var wallTop = entityManager.CreateEntity("wallTop");
            wallTop.AddComponent(new CAnimation(assets.GetAnimation("WallHorizontal").AsScaledAnimation(new Vec2(VirtualWidth - _wallThickness * 2, _wallThickness))));
            wallTop.AddComponent(new CTransform(new Vec2(_wallThickness, 0)));
            wallTop.AddComponent(new CBoundingBox(new Vec2(VirtualWidth - _wallThickness * 2, _wallThickness), false, false));

            var wallBottom = entityManager.CreateEntity("wallBottom");
            //wallBottom.AddComponent(new CAnimation(assets.GetAnimation("WallHorizontal").AsScaledAnimation(new Vec2(VirtualWidth - _wallThickness * 2, _wallThickness))));
            wallBottom.AddComponent(new CTransform(new Vec2(_wallThickness, VirtualHeight - _wallThickness)));
            wallBottom.AddComponent(new CBoundingBox(new Vec2(VirtualWidth - _wallThickness * 2, _wallThickness), false, false));

            var wallLeft = entityManager.CreateEntity("wallLeft");
            wallLeft.AddComponent(new CAnimation(assets.GetAnimation("WallVertical").AsScaledAnimation(new Vec2(_wallThickness, VirtualHeight))));
            wallLeft.AddComponent(new CTransform(new Vec2(0, 0)));
            wallLeft.AddComponent(new CBoundingBox(new Vec2(_wallThickness, VirtualHeight), false, false));

            var wallRight = entityManager.CreateEntity("wallRight");
            wallRight.AddComponent(new CAnimation(assets.GetAnimation("WallVertical").AsScaledAnimation(new Vec2(_wallThickness, VirtualHeight))));
            wallRight.AddComponent(new CTransform(new Vec2(VirtualWidth - _wallThickness, 0)));
            wallRight.AddComponent(new CBoundingBox(new Vec2(_wallThickness, VirtualHeight), false, false));

            var aimer = entityManager.CreateEntity("aimer");
            aimer.AddComponent(new CTransform(new Vec2(VirtualWidth / 2, VirtualHeight - 100)));
            aimer.AddComponent(new CAnimation(assets.GetAnimation("WallVertical").AsScaledAnimation(new Vec2(5, 160))));
            aimer.AddComponent(new CBoundingBox(new Vec2(20, 20), blockVision: false, blockMove: false));
            var cAimer = new CAimer();
            aimer.AddComponent(cAimer);
            inputManager.ActionMapper.MapPointerActionToComponent<CAimer>(Pointer.PointerEventType.Move, aimer, (cAimer, pointerEvent) =>
            {
                cAimer.MovePosition = pointerEvent.Position;
            });
            inputManager.ActionMapper.MapPointerActionToComponent<CAimer>(Pointer.PointerEventType.Press, aimer, (cAimer, pointerEvent) =>
            {
                cAimer.PressPosition = pointerEvent.Position;
            });
            inputManager.ActionMapper.MapPointerActionToComponent<CAimer>(Pointer.PointerEventType.Release, aimer, (cAimer, pointerEvent) =>
            {
                cAimer.ReleasePosition = pointerEvent.Position;
            });
            inputManager.VirtualResolution = new Vec2(VirtualWidth, VirtualHeight);

            CreateBlockRow(entityManager, 0);
        }
        private void CreateBall(EntityManager entityManager)
        {
            var ball1 = entityManager.CreateEntity("ball");
            ball1.AddComponent(new CAnimation(assets.GetAnimation("Ball")));
            ball1.AddComponent(new CTransform(new Vec2(VirtualWidth / 2, VirtualHeight - 60)));
            ball1.AddComponent(new CBoundingBox(new Vec2(20, 20), blockVision: false, blockMove: false));
            ball1.AddComponent(new CMovement(1300, 600));
            ball1.AddComponent(new CBall());
        }

        public override void Update(EntityManager entityManager, SystemContainer systems, double deltaTime)
        {
            if (GameState == GameStates.Aiming)
            {
                HandleAimer(entityManager);
            }
            else if (GameState == GameStates.BallInPlay)
            {
                HandleCollisions(entityManager, systems);
            }
            else if (GameState == GameStates.BallOutPlay)
            {
                MoveBlocksDown(entityManager);
                CreateBlockRow(entityManager, 0);
                CreateBall(entityManager); 
                GameState = GameStates.Aiming; 
            }
        }

        private void HandleCollisions(EntityManager entityManager, SystemContainer systems)
        {
            foreach (var collision in systems.Get<PhysicsSystem>().CollisionEvents)
            {
                var hitBlock = HandleBallBlockCollision(collision, entityManager);
                if (hitBlock) break;
                HandleBallWallCollision(collision, entityManager);
            }
        }

        private static bool HandleBallBlockCollision(CollisionEvent collision, EntityManager entityManager)
        {
            bool aIsBall = collision.A.Tag.StartsWith("ball");
            bool aIsBlock = collision.A.Tag.StartsWith("block");
            bool bIsBall = collision.B.Tag.StartsWith("ball");
            bool bIsBlock = collision.B.Tag.StartsWith("block");

            if ((aIsBall && bIsBlock) || (bIsBall && aIsBlock))
            {
                var ball = aIsBall ? collision.A : collision.B;
                var block = aIsBlock ? collision.A : collision.B;

                var cBall = ball.GetComponent<CBall>();
  
                cBall.LastHit = block.Tag;
                var ballTransform = ball.GetComponent<CTransform>();
                var blockTransform = block.GetComponent<CTransform>();

                var ballPos = ballTransform.Position;
                var blockPos = blockTransform.Position;
                var blockSize = block.GetComponent<CBoundingBox>().Size;

                var ballCenter = new Vec2(ballPos.X + 10, ballPos.Y + 10); 
                var blockCenter = new Vec2(blockPos.X + blockSize.X / 2, blockPos.Y + blockSize.Y / 2);

                var difference = ballCenter - blockCenter;

                if (Math.Abs(difference.X) > Math.Abs(difference.Y))
                    ballTransform.Velocity = new Vec2(-ballTransform.Velocity.X, ballTransform.Velocity.Y);
                else
                    ballTransform.Velocity = new Vec2(ballTransform.Velocity.X, -ballTransform.Velocity.Y);
              
                block.Active = false;
                return true; 
            }
            return false; 
        }

        private void HandleBallWallCollision(CollisionEvent collision, EntityManager entityManager)
        {
            bool aIsBall = collision.A.Tag.StartsWith("ball");
            bool aIsWall = collision.A.Tag.StartsWith("wall");
            bool bIsBall = collision.B.Tag.StartsWith("ball");
            bool bIsWall = collision.B.Tag.StartsWith("wall");

            if ((aIsBall && bIsWall) || (bIsBall && aIsWall))
            {
                var ball = aIsBall ? collision.A : collision.B;
                var wall = aIsWall ? collision.A : collision.B;

                var cBall = ball.GetComponent<CBall>();

                if (cBall.LastHit != wall.Tag)
                {
                    cBall.LastHit = wall.Tag;

                    var ballTransform = ball.GetComponent<CTransform>();
                    if (wall.Tag == "wallLeft" || wall.Tag == "wallRight")
                    {
                        ballTransform.Velocity = new Vec2(-ballTransform.Velocity.X, ballTransform.Velocity.Y);
                    }
                    else if (wall.Tag == "wallTop")
                    {
                        ballTransform.Velocity = new Vec2(ballTransform.Velocity.X, -ballTransform.Velocity.Y);
                    }
                    else if (wall.Tag == "wallBottom")
                    {
                        ball.Active = false;
                        cBall.LastHit = "";
                        GameState = GameStates.BallOutPlay; 
                    }
                }
            }
        }

        private void HandleAimer(EntityManager entityManager)
        {
            var aimer = entityManager.GetEntityWithTag("aimer");
            if (aimer == null) return;

            var aimerTransform = aimer.GetComponent<CTransform>();

            var ball = entityManager.GetEntityWithTag("ball");
            var ballTransform = ball.GetComponent<CTransform>();

            aimerTransform.Position = ballTransform.Position;

            var cAimer = aimer.GetComponent<CAimer>();

            if (!cAimer.PressPosition.Equals(Vec2.Zero))
            {
                Vec2 direction;
                if (cAimer.MovePosition > new Vec2(0, 0))
                    direction = cAimer.MovePosition - ballTransform.Position;
                else
                    direction = cAimer.PressPosition - ballTransform.Position;
                double angleDegrees = direction.Angle * (180 / Math.PI);


                if (angleDegrees >= -23 || angleDegrees <= -157) return;

                aimerTransform.Rotation = angleDegrees - 90;

                if (!cAimer.ReleasePosition.Equals(Vec2.Zero))
                {
                    var releaseAngle = cAimer.ReleasePosition - ballTransform.Position;
                    var releaseAngleDegrees = releaseAngle.Angle * (180 / Math.PI);

                    if (releaseAngleDegrees >= -23 || releaseAngleDegrees <= -157) return;

                    Vec2 moveDirection = (direction).Normalize();
                    var ballMovement = ball.GetComponent<CMovement>();
                    ballTransform.Velocity = moveDirection * ballMovement.Speed;

                    cAimer.PressPosition = Vec2.Zero;
                    cAimer.MovePosition = Vec2.Zero;
                    cAimer.ReleasePosition = Vec2.Zero;
                    GameState = GameStates.BallInPlay;
                }
            }
        }

        private void CreateBlockRow(EntityManager entityManager, int row)
        {
            var blockWidth = (VirtualWidth - _wallThickness * 2) / 8;
            var blockHeight = 40;
            var blockSpacing = 0;
            var topPadding = VirtualHeight / 20;

            // Create a Random instance for the 50/50 chance
            var random = new Random();

            for (int i = 0; i < 8; i++)
            {
                // 50/50 chance to create a block (random.Next(0, 2) returns 0 or 1)
                if (random.Next(0, 2) == 0)
                {
                    var block = entityManager.CreateEntity("block");
                    block.AddComponent(new CAnimation(assets.GetAnimation("BrickBlock").AsScaledAnimation(new Vec2(blockWidth, blockHeight))));
                    block.AddComponent(new CTransform(new Vec2(_wallThickness + (blockWidth + blockSpacing) * i, topPadding + _wallThickness + (blockHeight + blockSpacing) * row)));
                    block.AddComponent(new CBoundingBox(new Vec2(blockWidth, blockHeight), blockVision: true, blockMove: false));
                }
            }
        }

        private void MoveBlocksDown(EntityManager entityManager)
        {
            var blockWidth = (VirtualWidth - _wallThickness * 2) / 8;
            var blockHeight = 40;
            var blockSpacing = 0;
            var topPadding = VirtualHeight / 20;
            var blocks = entityManager.GetEntitiesWithTag("block");
            foreach (var block in blocks)
            {
                var blockTransform = block.GetComponent<CTransform>();
                blockTransform.Position = new Vec2(blockTransform.Position.X, blockTransform.Position.Y + blockHeight + blockSpacing);
            }

        }


        class CAimer : Component
        {
            public Vec2 PressPosition { get; set; }
            public Vec2 MovePosition { get; set; }
            public Vec2 ReleasePosition { get; set; }
        }

        class CBall : Component
        {
            public string LastHit = "";
        }

        enum GameStates
        {
            Aiming, 
            BallInPlay,
            BallOutPlay,
        }
    }
}
