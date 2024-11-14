using GameEngine.Core;
using GameEngine.Core.Components;
using SkiaSharp;

namespace GameEngine.Demo
{
    public class SceneBasic : Scene
    {
        private readonly Assets assets = new("assets.txt");

        private EntityManager entityManager;
        private InputManager inputManager;
        private ActionMapper actionMapper;
        private Entity? playerEntity;
        private Entity testEntity;

        public override void Initialize(EntityManager entityManager, InputManager inputManager, ActionMapper actionMapper)
        {
            this.entityManager = entityManager;
            this.inputManager = inputManager;
            this.actionMapper = actionMapper;


            inputManager.AddAction(Keys.W, "Up");
            inputManager.AddAction(Keys.S, "Down");
            inputManager.AddAction(Keys.A, "Left");
            inputManager.AddAction(Keys.D, "Right");

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
            Movement(deltaMs);
            Animations(deltaMs);
        }

        void Animations(float deltaMs)
        {
            entityManager.GetEntities().ForEach(entity =>
            {
                var animation = entity.GetComponent<CAnimation>();
                animation.Update(deltaMs);
            });
        }

        void Movement(float deltaMs)
        {
            var playerInput = playerEntity.GetComponent<CInput>();
            var playerTransform = playerEntity.GetComponent<CTransform>();

            float deltaSeconds = deltaMs / 1000f;
            float playerSpeed = 600f; 
            float playerSpeedTransform = playerSpeed * deltaSeconds;

            if (!playerInput.Any)
            {
                float decelerationFactor = 0.2f;
                playerTransform.Velocity *= MathF.Pow(decelerationFactor, deltaSeconds);
            }
            if (playerInput.Up)
            {
                playerTransform.Velocity.Y -= playerSpeedTransform;
            }
            if (playerInput.Down)
            {
                playerTransform.Velocity.Y += playerSpeedTransform;
            }
            if (playerInput.Left)
            {
                playerTransform.Velocity.X -= playerSpeedTransform;
            }
            if (playerInput.Right)
            {
                playerTransform.Velocity.X += playerSpeedTransform;
            }

            float maxSpeed = 250f;
            if (playerTransform.Velocity.Length() > maxSpeed)
            {
                playerTransform.Velocity = playerTransform.Velocity.Normalize() * maxSpeed;
            }

            playerTransform.Position += playerTransform.Velocity * deltaSeconds;
        }

        int i = 0;
        readonly SKPaint paint = new SKPaint() { Color = SKColors.Red, TextSize = 30 };
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
