using GameEngine.Core;
using GameEngine.Core.Components;
using SkiaSharp;

namespace GameEngine.Demo
{
    public class SceneBasic : Scene
    {
        private readonly EntityManager entityManager = new EntityManager();
        private readonly Entity playerEntity;

        public SceneBasic()
        {
            AddAction(Keys.W, "moveUp");
            AddAction(Keys.S, "moveDown");
            AddAction(Keys.A, "moveLeft");
            AddAction(Keys.D, "moveRight");

            playerEntity = entityManager.CreateEntity("player");
            playerEntity.AddComponent<CTransform>();
        }

        public override void ExecuteAction(Keys key)
        {
            if (actionMap.ContainsKey(key))
            {
                string action = actionMap[key];

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

        public override void Render(SKCanvas canvas)
        {
            canvas.Clear(SKColors.White);
            DrawPlayer(canvas);
        }

        private void DrawPlayer(SKCanvas canvas)
        {
            var playerTransform = playerEntity.GetComponent<CTransform>();
            canvas.DrawRect(new SKRect((float)playerTransform.Position.X, (float)playerTransform.Position.Y, (float)playerTransform.Position.X + 10, (float)playerTransform.Position.Y + 10), new SKPaint() { Color = SKColors.Black });
        }
    }
}
