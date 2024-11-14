using GameEngine.Core;
using GameEngine.Core.Components;
using SkiaSharp;

namespace GameEngine.Demo
{
    public class SceneBasic : Scene
    {
        private readonly EntityManager entityManager;
        private readonly InputManager inputManager;
        private readonly ActionMapper actionMapper;
        private readonly Entity testEntity;
        private readonly Assets assets = new("assets.txt");

        private Entity? playerEntity;

        public SceneBasic()
        {
            inputManager = new InputManager();
            actionMapper = new ActionMapper(inputManager);

            inputManager.AddAction(Keys.W, "Up");
            inputManager.AddAction(Keys.S, "Down");
            inputManager.AddAction(Keys.A, "Left");
            inputManager.AddAction(Keys.D, "Right");

            entityManager = new EntityManager();
            CreatePlayer();

            testEntity = entityManager.CreateEntity("grenade");
            testEntity.AddComponent(new CAnimation(assets.GetAnimation("Grenade")));
            testEntity.AddComponent<CTransform>();
        }

        private void CreatePlayer()
        {
            playerEntity = entityManager.CreateEntity("player");
            playerEntity.AddComponent(new CAnimation(assets.GetAnimation("JeepBack")));
            playerEntity.AddComponent<CTransform>();
            var playerInput = playerEntity.AddComponent<CInput>();

            actionMapper.MapActionToComponent<CInput>("Up", playerEntity, (input, isActive) => input.Up = isActive);
            actionMapper.MapActionToComponent<CInput>("Down", playerEntity, (input, isActive) => input.Down = isActive);
            actionMapper.MapActionToComponent<CInput>("Left", playerEntity, (input, isActive) => input.Left = isActive);
            actionMapper.MapActionToComponent<CInput>("Right", playerEntity, (input, isActive) => input.Right = isActive);
                }

        public override void HandleAction(Keys key, bool start)
        {
                if (start)
                inputManager.HandleKeyPress(key);
                else
                inputManager.HandleKeyRelease(key);
        }

        public override void Simulate(float deltaMs)
        {
            Movement();
            Animations(deltaMs);
            entityManager.Update();
        }

        void Animations(float deltaMs)
        {
            entityManager.GetEntities().ForEach(entity =>
            {
                var animation = entity.GetComponent<CAnimation>();
                animation.Update(deltaMs);
            });
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

            DrawTestEntity(canvas);

            canvas.DrawText(i++.ToString(), 30, 50, paint);
        }

        private void DrawTestEntity(SKCanvas canvas)
        {
            var testTransform = testEntity.GetComponent<CTransform>();

            var animation = testEntity.GetComponent<CAnimation>();
            using var frame = animation.GetCurrentFrame();

            canvas.DrawImage(frame, new SKPoint((float)testTransform.Position.X, (float)testTransform.Position.Y));
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
