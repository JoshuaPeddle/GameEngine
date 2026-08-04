using GameEngine.Core;
using GameEngine.Core.Components;
using GameEngine.Core.Systems;
using SkiaSharp;
using System;

namespace GameEngine.Demo
{
    public class ScenePong : Scene
    {
        private Assets? assets;
        private Assets Assets => assets
            ?? throw new InvalidOperationException("ScenePong has not been initialized.");
        public override int VirtualWidth => 800;
        public override int VirtualHeight => 600;

        private const int BallXSpeed = 600;
        private const double PaddleSpinTransfer = 0.6;
        private const double StallSpeedThreshold = 1.0;

        private int _wallThickness = 30;
        public override void Initialize(EntityManager entityManager, InputManager inputManager, AudioSystem? audioPlayer, Action<Scene?> ResetScene)
        {
            assets ??= new("assets.txt", AssetSource);

            inputManager.AddAction(GeKeys.W, "Up");
            inputManager.AddAction(GeKeys.S, "Down");
            inputManager.AddAction(GeKeys.A, "Left");
            inputManager.AddAction(GeKeys.D, "Right");

            var paddle1 = entityManager.CreateEntity("paddle1");
            paddle1.AddComponent(new CAnimation(assets.GetAnimation("Paddle")));
            paddle1.AddComponent(new CTransform(new Vec2(35, 250)));
            paddle1.AddComponent(new CBoundingBox(new Vec2(20, 100), blockVision: true, blockMove: true));
            paddle1.AddComponent(new CMovement(1300, 600));
            paddle1.AddComponent<CInput>();
            inputManager.ActionMapper.MapActionToComponent<CInput>("Up", paddle1, (input, isActive) => input.Up = isActive);
            inputManager.ActionMapper.MapActionToComponent<CInput>("Down", paddle1, (input, isActive) => input.Down = isActive);

            var paddle2 = entityManager.CreateEntity("paddle2");
            paddle2.AddComponent(new CAnimation(assets.GetAnimation("Paddle")));
            paddle2.AddComponent(new CTransform(new Vec2(745, 250)));
            paddle2.AddComponent(new CBoundingBox(new Vec2(20, 100), true, true));
            paddle2.AddComponent(new CMovement(450));
            paddle2.AddComponent<CInput>();

            var wallTop = entityManager.CreateEntity("wallTop");
            wallTop.AddComponent(new CAnimation(assets.GetAnimation("WallHorizontal")));
            wallTop.AddComponent(new CTransform(new Vec2(_wallThickness, 0)));
            wallTop.AddComponent(new CBoundingBox(new Vec2(800 - _wallThickness*2, _wallThickness), true, true));

            var wallBottom = entityManager.CreateEntity("wallBottom");
            wallBottom.AddComponent(new CAnimation(assets.GetAnimation("WallHorizontal")));
            wallBottom.AddComponent(new CTransform(new Vec2(_wallThickness, 600 - _wallThickness)));
            wallBottom.AddComponent(new CBoundingBox(new Vec2(800 - _wallThickness*2, _wallThickness), true, true));

            var wallLeft = entityManager.CreateEntity("wallLeft");
            wallLeft.AddComponent(new CAnimation(assets.GetAnimation("WallVertical")));
            wallLeft.AddComponent(new CTransform(new Vec2(0, 0)));
            wallLeft.AddComponent(new CBoundingBox(new Vec2(_wallThickness, 600), true, true));

            var wallRight = entityManager.CreateEntity("wallRight");
            wallRight.AddComponent(new CAnimation(assets.GetAnimation("WallVertical")));
            wallRight.AddComponent(new CTransform(new Vec2(800 - _wallThickness, 0)));
            wallRight.AddComponent(new CBoundingBox(new Vec2(_wallThickness, 600), true, true));

            var ball = entityManager.CreateEntity("ball");
            ball.AddComponent(new CAnimation(assets.GetAnimation("Ball")));
            ball.AddComponent(new CTransform(new Vec2(400, 300), new Vec2(-BallXSpeed, 0)));
            ball.AddComponent(new CBoundingBox(new Vec2(20, 20), true, false));
            ball.AddComponent(new CBall());
            ball.AddComponent(new CMovement(BallXSpeed, BallXSpeed));

            var score = entityManager.CreateEntity("score");
            score.AddComponent(new CTransform(new Vec2(400, 80)));
            score.AddComponent<CText>(new CScore());
        }

        public override void Update(EntityManager entityManager, SystemContainer systems, double deltaSeconds)
        {
            var physicsSystem = systems.Get<PhysicsSystem>();

            foreach (var collision in physicsSystem.CollisionEvents)
            {
                HandleBallPaddleCollision(collision);
                HandleBallWallCollision(collision, entityManager);
            }
            KeepBallMoving(entityManager);
            HandlePlayer2(entityManager);
        }

        private static void KeepBallMoving(EntityManager entityManager)
        {
            var ball = entityManager.GetEntityWithTag("ball");
            if (ball == null) return;

            var ballTransform = ball.GetComponent<CTransform>();
            if (ballTransform.Velocity.Length() > StallSpeedThreshold)
                return;

            double awayFromNearestWall = ballTransform.Position.X < 400 ? BallXSpeed : -BallXSpeed;
            ballTransform.Velocity = new Vec2(awayFromNearestWall, ballTransform.Velocity.Y);
        }

        private void HandlePlayer2(EntityManager entityManager)
        {
            var paddle2 = entityManager.GetEntityWithTag("paddle2");
            if (paddle2 == null) return;
            var paddle2Transform = paddle2.GetComponent<CTransform>();
            var paddle2Input = paddle2.GetComponent<CInput>();
            var paddle2BoundingBox = paddle2.GetComponent<CBoundingBox>();

            var ball = entityManager.GetEntityWithTag("ball");
            if (ball == null) return;
            var ballTransform = ball.GetComponent<CTransform>();
            var ballBoundingBox = ball.GetComponent<CBoundingBox>();

            double paddle2CenterY = paddle2Transform.Position.Y + (paddle2BoundingBox.Size.Y / 2);
            double ballCenterY = ballTransform.Position.Y + (ballBoundingBox.Size.Y / 2);
            double screenCenterY = VirtualHeight / 2; 
            double deadZone = 10;
            double middleDeadZone = 160;

            bool ballMovingLeft = ballTransform.Velocity.X < 0;

            if (ballMovingLeft)
            {
                if (Math.Abs(paddle2CenterY - screenCenterY) > middleDeadZone)
                {
                    if (paddle2CenterY > screenCenterY + middleDeadZone)
                    {
                        paddle2Input.Up = true;
                        paddle2Input.Down = false;
                    }
                    else if (paddle2CenterY < screenCenterY - middleDeadZone)
                    {
                        paddle2Input.Down = true;
                        paddle2Input.Up = false;
                    }
                }
                else
                {
                    paddle2Input.Up = false;
                    paddle2Input.Down = false;
                }
            }
            else
            {
                if (ballCenterY < paddle2CenterY - deadZone)
                {
                    paddle2Input.Up = true;
                    paddle2Input.Down = false;
                }
                else if (ballCenterY > paddle2CenterY + deadZone)
                {
                    paddle2Input.Down = true;
                    paddle2Input.Up = false;
                }
                else
                {
                    paddle2Input.Up = false;
                    paddle2Input.Down = false;
                }
            }
        }

        private static void HandleBallPaddleCollision(CollisionEvent collision)
        {
            bool aIsBall = collision.A.Tag.StartsWith("ball");
            bool aIsPaddle = collision.A.Tag.StartsWith("paddle");
            bool bIsBall = collision.B.Tag.StartsWith("ball");
            bool bIsPaddle = collision.B.Tag.StartsWith("paddle");

            if ((aIsBall && bIsPaddle) || (bIsBall && aIsPaddle))
            {
                var ball = aIsBall ? collision.A : collision.B;
                var paddle = aIsPaddle ? collision.A : collision.B;

                var ballImpactVelocity = aIsBall ? collision.VelocityA : collision.VelocityB;
                var paddleImpactVelocity = aIsPaddle ? collision.VelocityA : collision.VelocityB;

                var ballTransform = ball.GetComponent<CTransform>();
                var paddleTransform = paddle.GetComponent<CTransform>();

                double awayFromPaddle = CentreOf(ball).X < CentreOf(paddle).X ? -BallXSpeed : BallXSpeed;
                double transferredSpin = paddleImpactVelocity.Y * PaddleSpinTransfer;

                ballTransform.Velocity = new Vec2(
                    awayFromPaddle,
                    ballImpactVelocity.Y + transferredSpin);
            }
        }

        private static Vec2 CentreOf(Entity entity)
        {
            var transform = entity.GetComponent<CTransform>();
            var size = entity.GetComponent<CBoundingBox>().Size;
            return new Vec2(transform.Position.X + (size.X / 2), transform.Position.Y + (size.Y / 2));
        }

        private static void HandleBallWallCollision(CollisionEvent collision, EntityManager entityManager)
        {
            bool aIsBall = collision.A.Tag.StartsWith("ball");
            bool aIsWall = collision.A.Tag.StartsWith("wall");
            bool bIsBall = collision.B.Tag.StartsWith("ball");
            bool bIsWall = collision.B.Tag.StartsWith("wall");

            if ((aIsBall && bIsWall) || (bIsBall && aIsWall))
            {
                var ball = aIsBall ? collision.A : collision.B;
                var wall = aIsWall ? collision.A : collision.B;

                var ballImpactVelocity = aIsBall ? collision.VelocityA : collision.VelocityB;
                var ballTransform = ball.GetComponent<CTransform>();

                if (wall.Tag == "wallLeft" || wall.Tag == "wallRight")
                {
                    HandleScore(entityManager);
                }
                else
                {
                    double awayFromWall = CentreOf(ball).Y < CentreOf(wall).Y
                        ? -Math.Abs(ballImpactVelocity.Y)
                        : Math.Abs(ballImpactVelocity.Y);

                    ballTransform.Velocity = new Vec2(ballTransform.Velocity.X, awayFromWall);
                }
            }
        }

        private static void HandleScore(EntityManager entityManager)
        {
            var ball = entityManager.GetEntityWithTag("ball");
            if (ball == null) return;
            var ballTransform = ball.GetComponent<CTransform>();
            var score = entityManager.GetEntityWithTag("score");
            if (score == null) return;
            var cScore = (CScore)score.GetComponent<CText>();
            if (ballTransform.Position.X < 400)
            {
                cScore.Player2++;
                ResetBall(ballTransform);
            }
            else if (ballTransform.Position.X > 400)
            {
                cScore.Player1++;
                ResetBall(ballTransform);
            }
        }

        private static void ResetBall(CTransform ballTransform)
        {
            ballTransform.Position = new Vec2(400, 300);

            var random = new Random();
            if (random.Next(0, 2) == 0)
                ballTransform.Velocity = new Vec2(BallXSpeed, 15);
            else
                ballTransform.Velocity = new Vec2(-BallXSpeed, 15);
        }

        class CBall : Component
        {
        }
        class CScore : CText
        {
            private int _player1 = 0;
            private int _player2 = 0;

            public int Player1
            {
                get => _player1;
                set
                {
                    if (_player1 != value)
                    {
                        _player1 = value;
                        UpdateScoreText();
                    }
                }
            }

            public int Player2
            {
                get => _player2;
                set
                {
                    if (_player2 != value)
                    {
                        _player2 = value;
                        UpdateScoreText();
                    }
                }
            }

            private void UpdateScoreText()
            {
                Text = $"{_player1}   {_player2}";
            }

            public CScore() : base("0   0", 24)
            {
                Paint.Color = SKColors.Black;
                Size = 48;
                TextAlign = SKTextAlign.Center;
            }
        }
    }
}
