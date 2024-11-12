using SkiaSharp;

namespace GameEngine.WinForms
{
    public class SceneBasic : Scene
    {

        Point position = new Point(0, 0);


        public SceneBasic()
        {
            AddAction(Keys.W, "moveUp");
            AddAction(Keys.S, "moveDown");
            AddAction(Keys.A, "moveLeft");
            AddAction(Keys.D, "moveRight");
        }

        public override void ExecuteAction(Keys key)
        {
            if (actionMap.ContainsKey(key))
            {
                string action = actionMap[key];
                if (action == "moveUp")
                {
                    position.Y -= 1;

                }
                else if (action == "moveDown")
                {
                    position.Y += 1;
                }
                else if (action == "moveLeft")
                {
                    position.X -= 1;
                }
                else if (action == "moveRight")
                {
                    position.X += 1;
                }
            }
        }

        public override void Simulate()
        {
            
        }

        internal override void Render(SKCanvas canvas)
        {
            canvas.Clear(SKColors.White);
            canvas.DrawCircle(position.X, position.Y, 10, new SKPaint() { Color = SKColors.Black });
        }
    }
}
