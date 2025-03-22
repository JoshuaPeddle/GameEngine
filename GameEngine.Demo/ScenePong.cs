using GameEngine.Core;
using GameEngine.Core.Components;
using GameEngine.Core.Systems;
using SkiaSharp;
using System;

namespace GameEngine.Demo
{
    public class ScenePong : Scene
    {
        public override int VirtualWidth => 800;
        public override int VirtualHeight => 600;


        public override void Initialize(EntityManager entityManager, InputManager inputManager, AudioSystem audioPlayer, Action<Scene?> ResetScene)
        {
            inputManager.AddAction(GeKeys.W, "Up");
            inputManager.AddAction(GeKeys.S, "Down");
            inputManager.AddAction(GeKeys.A, "Left");
            inputManager.AddAction(GeKeys.D, "Right");

            var paddle1 = entityManager.CreateEntity("paddle1");
            paddle1.AddComponent(new CTransform(new Vec2(25, 250)));
            paddle1.AddComponent(new CBoundingBox(new Vec2(20, 100), blockVision: true, blockMove: true));
            paddle1.AddComponent<CMovement>();
            paddle1.AddComponent<CInput>();
            inputManager.ActionMapper.MapActionToComponent<CInput>("Up", paddle1, (input, isActive) => input.Up = isActive);
            inputManager.ActionMapper.MapActionToComponent<CInput>("Down", paddle1, (input, isActive) => input.Down = isActive);

            var paddle2 = entityManager.CreateEntity("paddle2");
            paddle2.AddComponent(new CTransform(new Vec2(775, 250)));
            paddle2.AddComponent(new CBoundingBox(new Vec2(20, 100), true, true));
            paddle2.AddComponent(new CMovement(450));
            paddle2.AddComponent<CInput>();

            var wallTop = entityManager.CreateEntity("wallTop");
            wallTop.AddComponent(new CTransform(new Vec2(20, 0)));
            wallTop.AddComponent(new CBoundingBox(new Vec2(780, 20), true, true));

            var wallBottom = entityManager.CreateEntity("wallBottom");
            wallBottom.AddComponent(new CTransform(new Vec2(20, 580)));
            wallBottom.AddComponent(new CBoundingBox(new Vec2(780, 20), true, true));

            var wallLeft = entityManager.CreateEntity("wallLeft");
            wallLeft.AddComponent(new CTransform(new Vec2(0, 0)));
            wallLeft.AddComponent(new CBoundingBox(new Vec2(20, 600), true, true));

            var wallRight = entityManager.CreateEntity("wallRight");
            wallRight.AddComponent(new CTransform(new Vec2(800, 0)));
            wallRight.AddComponent(new CBoundingBox(new Vec2(20, 600), true, true));

            var ball = entityManager.CreateEntity("ball");
            ball.AddComponent(new CTransform(new Vec2(400, 300), new Vec2(-200, 15)));
            ball.AddComponent(new CBoundingBox(new Vec2(20, 20), true, false));
            ball.AddComponent(new CBall());
            ball.AddComponent(new CMovement());

            var score = entityManager.CreateEntity("score");
            score.AddComponent(new CTransform(new Vec2(400, 80)));
            score.AddComponent<CText>(new CScore());
        }

        public override void Update(EntityManager entityManager, PhysicsSystem physicsSystem, double deltaTime)
        {
            foreach (var collision in physicsSystem.CollisionEvents)
            {
                HandleBallPaddleCollision(collision);
                HandleBallWallCollision(collision, entityManager);
            }
            HandlePlayer2(entityManager);
        }

        private void HandlePlayer2(EntityManager entityManager)
        {
            var paddle2 = entityManager.GetEntityWithTag("paddle2");
            var paddle2Transform = paddle2.GetComponent<CTransform>();
            var paddle2Input = paddle2.GetComponent<CInput>();
            var paddle2BoundingBox = paddle2.GetComponent<CBoundingBox>();

            var ball = entityManager.GetEntityWithTag("ball");
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

                var cBall = ball.GetComponent<CBall>();

                if (cBall.LastHitPaddle != paddle.Tag)
                {
                    cBall.LastHitPaddle = paddle.Tag;
                    var ballTransform = ball.GetComponent<CTransform>();
                    var paddleTransform = paddle.GetComponent<CTransform>();

                    double paddleYVelocity = paddleTransform.Velocity.Y;

                    double yVelocityTransferFactor = 0.6;

                    double newBallYVelocity = ballTransform.Velocity.Y + (paddleYVelocity * yVelocityTransferFactor);

                    ballTransform.Velocity = new Vec2(-ballTransform.Velocity.X, newBallYVelocity);
                }
            }
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

                var cBall = ball.GetComponent<CBall>();

                if (cBall.LastHitWall != wall.Tag)
                {
                    cBall.LastHitWall = wall.Tag;

                    var ballTransform = ball.GetComponent<CTransform>();
                    if (wall.Tag == "wallLeft" || wall.Tag == "wallRight")
                    {
                        HandleScore(entityManager);
                    }
                    else if (wall.Tag == "wallTop" || wall.Tag == "wallBottom")
                        ballTransform.Velocity = new Vec2(ballTransform.Velocity.X, -ballTransform.Velocity.Y);
                }
            }
        }

        private static void HandleScore(EntityManager entityManager)
        {
            var ball = entityManager.GetEntityWithTag("ball");
            var cBall = ball.GetComponent<CBall>();
            var ballTransform = ball.GetComponent<CTransform>();
            var score = entityManager.GetEntityWithTag("score");
            var cScore = (CScore)score.GetComponent<CText>();
            if (ballTransform.Position.X < 400)
            {
                cScore.Player2++;
                ResetBall(ballTransform, cBall);
            }
            else if (ballTransform.Position.X > 400)
            {
                cScore.Player1++;
                ResetBall(ballTransform, cBall);
            }
        }

        private static void ResetBall(CTransform ballTransform, CBall cBall)
        {
            ballTransform.Position = new Vec2(400, 300);
            ballTransform.Velocity = new Vec2(-200, 15);
            cBall.LastHitPaddle = "";
            cBall.LastHitWall = "";
        }

        class CBall : Component
        {
            public string LastHitPaddle = "";
            public string LastHitWall = "";
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
                Paint.TextSize = 48;
                Paint.TextAlign = SKTextAlign.Center;
            }
        }
    }
}
