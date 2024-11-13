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
        }

        public override void ExecuteAction(Keys key)
        {
            if (actionMap.TryGetValue(key, out string? value))
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

        public override void Simulate()
        {

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
