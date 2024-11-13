using GameEngine.Core;
using GameEngine.Core.Components;
using SkiaSharp;

namespace GameEngine.Demo
{
    public class SceneBasic : Scene
    {
        private readonly EntityManager entityManager;
        private readonly Entity playerEntity;
        private readonly Assets assets = new("assets.txt");

        public SceneBasic()
        {
            AddAction(Keys.W, "moveUp");
            AddAction(Keys.S, "moveDown");
            AddAction(Keys.A, "moveLeft");
            AddAction(Keys.D, "moveRight");
            entityManager = new EntityManager();

            playerEntity = entityManager.CreateEntity("player");
            playerEntity.AddComponent(new CAnimation(assets.GetAnimation("JeepBack")));
            playerEntity.AddComponent<CTransform>();
            playerEntity.AddComponent<CInput>();
        }

        public  void ExecuteAction2(Keys key)
        {
            if (ActionMap.TryGetValue(key, out string? value))
            {
                string action = value;

                var playerTransform = playerEntity.GetComponent<CTransform>();
                
                if (action == "moveUp")
                {
                    playerTransform.PreviousPosition = playerTransform.Position;
                    playerTransform.Position.Y -= 1;
                }
                else if (action == "moveDown")
                {
                    playerTransform.PreviousPosition = playerTransform.Position;
                    playerTransform.Position.Y += 1;
                }
                else if (action == "moveLeft")
                {
                    playerTransform.PreviousPosition = playerTransform.Position;
                    playerTransform.Position.X -= 1;
                }
                else if (action == "moveRight")
                {
                    playerTransform.PreviousPosition = playerTransform.Position;
                    playerTransform.Position.X += 1;
                }
            }
        }

        public override void HandleAction(Keys key, bool start)
        {
            if (ActionMap.TryGetValue(key, out string? value))
            {
                string action = value;

                var playerInput = playerEntity.GetComponent<CInput>();

                if (start)
                {
                    if (action == "moveUp")
                    {
                        playerInput.Up = true;
                    }
                    else if (action == "moveDown")
                    {
                        playerInput.Down = true;
                    }
                    else if (action == "moveLeft")
                    {
                        playerInput.Left = true;
                    }
                    else if (action == "moveRight")
                    {
                        playerInput.Right = true;
                    }
                }
                else
                {
                    if (action == "moveUp")
                    {
                        playerInput.Up = false;
                    }
                    else if (action == "moveDown")
                    {
                        playerInput.Down = false;
                    }
                    else if (action == "moveLeft")
                    {
                        playerInput.Left = false;
                    }
                    else if (action == "moveRight")
                    {
                        playerInput.Right = false;
                    }
                }

            }
        }

        public override void Simulate()
        {
            Movement();
        }

        void Movement()
        {
            var playerInput = playerEntity.GetComponent<CInput>();
            var playerTransform = playerEntity.GetComponent<CTransform>();

            if (!playerInput.Any)
            {
                playerTransform.Velocity = playerTransform.Velocity * 0.9f;
            }
            if (playerInput.Up)
            {
                playerTransform.Velocity.Y += -1;
            }
            if (playerInput.Down)
            {
                playerTransform.Velocity.Y += 1;
            }
            if (playerInput.Left)
            {
                playerTransform.Velocity.X += -1;
            }
            if (playerInput.Right)
            {
                playerTransform.Velocity.X += 1;
            }
            
            if (playerTransform.Velocity.Length() > 5)
                playerTransform.Velocity = playerTransform.Velocity.Normalize() * 5;

            playerTransform.Position += playerTransform.Velocity;
        }



        int i = 0;
        SKPaint paint = new SKPaint() { Color = SKColors.Red, TextSize = 30 };
        public override void Render(SKCanvas canvas)
        {
            canvas.Clear(SKColors.White);
            DrawPlayer(canvas);

            canvas.DrawText(i++.ToString(), 30, 50, paint);
        }

        private void DrawPlayer(SKCanvas canvas)
        {
            var playerTransform = playerEntity.GetComponent<CTransform>();

            var animation = playerEntity.GetComponent<CAnimation>();
            using var frame = animation.GetCurrentFrame();

            canvas.DrawImage(frame, new SKPoint((float)playerTransform.Position.X, (float)playerTransform.Position.Y));
        }
    }
}
