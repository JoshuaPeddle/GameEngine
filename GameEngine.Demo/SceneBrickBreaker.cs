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

        private const int _wallThickness = 30;

        public override void Initialize(EntityManager entityManager, InputManager inputManager, AudioSystem audioPlayer, Action<Scene?> ResetScene)
        {
            assets ??= new("assets.txt");

            var ball1 = entityManager.CreateEntity("ball");
            ball1.AddComponent(new CAnimation(assets.GetAnimation("Ball")));
            ball1.AddComponent(new CTransform(new Vec2(VirtualWidth / 2, VirtualHeight - 40)));
            ball1.AddComponent(new CBoundingBox(new Vec2(20, 20), blockVision: true, blockMove: true));
            ball1.AddComponent(new CMovement(1300, 600));


            var wallTop = entityManager.CreateEntity("wallTop");
            wallTop.AddComponent(new CAnimation(assets.GetAnimation("WallHorizontal").AsScaledAnimation(new Vec2(VirtualWidth - _wallThickness * 2, _wallThickness))));
            wallTop.AddComponent(new CTransform(new Vec2(_wallThickness, 0)));
            wallTop.AddComponent(new CBoundingBox(new Vec2(VirtualWidth - _wallThickness * 2, _wallThickness), true, true));

            var wallBottom = entityManager.CreateEntity("wallBottom");
            wallBottom.AddComponent(new CAnimation(assets.GetAnimation("WallHorizontal").AsScaledAnimation(new Vec2(VirtualWidth - _wallThickness * 2, _wallThickness))));
            wallBottom.AddComponent(new CTransform(new Vec2(_wallThickness, VirtualHeight - _wallThickness)));
            wallBottom.AddComponent(new CBoundingBox(new Vec2(VirtualWidth - _wallThickness * 2, _wallThickness), true, true));

            var wallLeft = entityManager.CreateEntity("wallLeft");
            wallLeft.AddComponent(new CAnimation(assets.GetAnimation("WallVertical").AsScaledAnimation(new Vec2(_wallThickness, VirtualHeight))));
            wallLeft.AddComponent(new CTransform(new Vec2(0, 0)));
            wallLeft.AddComponent(new CBoundingBox(new Vec2(_wallThickness, VirtualHeight), true, true));

            var wallRight = entityManager.CreateEntity("wallRight");
            wallRight.AddComponent(new CAnimation(assets.GetAnimation("WallVertical").AsScaledAnimation(new Vec2(_wallThickness, VirtualHeight))));
            wallRight.AddComponent(new CTransform(new Vec2(VirtualWidth - _wallThickness, 0)));
            wallRight.AddComponent(new CBoundingBox(new Vec2(_wallThickness, VirtualHeight), true, true));

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
        }

        public override void Update(EntityManager entityManager, SystemContainer systems, double deltaTime)
        {
            var aimer = entityManager.GetEntityWithTag("aimer");
            var aimerTransform = aimer.GetComponent<CTransform>();

            var ball = entityManager.GetEntityWithTag("ball");
            var ballTransform = ball.GetComponent<CTransform>();

            aimerTransform.Position = ballTransform.Position;

            var cAimer = aimer.GetComponent<CAimer>();

            if (!cAimer.PressPosition.Equals(Vec2.Zero))
            {
                Vec2 direction;
                if (cAimer.MovePosition > new Vec2(0,0))
                    direction = cAimer.MovePosition - ballTransform.Position;
                else
                    direction = cAimer.PressPosition - ballTransform.Position;
                double angleDegrees = direction.Angle * (180 / Math.PI);

                aimerTransform.Rotation = angleDegrees - 90;

                if (!cAimer.ReleasePosition.Equals(Vec2.Zero))
                {
                    Vec2 moveDirection = (cAimer.ReleasePosition - ballTransform.Position).Normalize();
                    var ballMovement = ball.GetComponent<CMovement>();
                    ballTransform.Velocity = moveDirection * ballMovement.Speed;

                    cAimer.PressPosition = Vec2.Zero;
                    cAimer.MovePosition = Vec2.Zero;
                    cAimer.ReleasePosition = Vec2.Zero;
                }
            }
        }

        class CAimer : Component
        {
            public Vec2 PressPosition { get; set; }
            public Vec2 MovePosition { get; set; }
            public Vec2 ReleasePosition { get; set; }
        }
    }
}
