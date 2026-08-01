using GameEngine.Core;
using GameEngine.Core.Components;
using GameEngine.Core.Systems;
using System;

namespace GameEngine.Demo
{
    public class SceneSideScroll2 : Scene
    {
        Assets? assets;
        public override int VirtualWidth => 4000;
        public override int VirtualHeight => 800;

        public override void Initialize(EntityManager entityManager, InputManager inputManager, AudioSystem? audioPlayer, Action<Scene> ResetScene)
        {
            assets ??= new Assets("assets.txt");

            // Input setup
            inputManager.AddAction(GeKeys.W, "Jump");
            inputManager.AddAction(GeKeys.S, "Down");
            inputManager.AddAction(GeKeys.A, "Left");
            inputManager.AddAction(GeKeys.D, "Right");
            inputManager.AddAction(GeKeys.Space, "Jump");

            // Create player
            var player = entityManager.CreateEntity("player");
            player.AddComponent(new CTransform(new Vec2(100, 600)));
            player.AddComponent(new CAnimation(assets.GetAnimation("Mario")));
            player.AddComponent(new CBoundingBox(new Vec2(35, 38), false, false));
            player.AddComponent(new CGravity { Acceleration = 700 });
            player.AddComponent(new CMovement(1200, 400));
            var playerInput = player.AddComponent<CInput>();
            player.AddComponent(new CPlayer());

            // Map inputs
            inputManager.ActionMapper.MapActionToComponent<CInput>("Left", player, (input, isActive) => input.Left = isActive);
            inputManager.ActionMapper.MapActionToComponent<CInput>("Right", player, (input, isActive) => input.Right = isActive);
            inputManager.ActionMapper.MapActionToComponent<CInput>("Jump", player, (input, isActive) => {
                if (isActive)
                {
                    var p = entityManager.GetEntityWithTag("player");
                    var transform = p.GetComponent<CTransform>();
                    var cPlayer = p.GetComponent<CPlayer>();

                    if (Math.Abs(transform.Velocity.Y) < 0.1 && cPlayer.CanJump)
                    {
                        cPlayer.CanJump = false;
                        transform.Velocity = new Vec2(transform.Velocity.X, -800);
                    }
                }
            });

            CreateLevel(entityManager);

            var camera = entityManager.CreateEntity("camera");
            var cameraComponent = new CCamera
            {
                Zoom = 1.5f,
                Position = new Vec2(200, 400),
                MinX = 400,
                MaxX = VirtualWidth - 400
            };
            camera.AddComponent(cameraComponent);

            var score = entityManager.CreateEntity("score");
            score.AddComponent(new CTransform(new Vec2(50, 50)));
            score.AddComponent(new CText("Coins: 0", 32));
        }

        private void CreateLevel(EntityManager entityManager)
        {
            // SECTION 1: Starting area with basic platforms
            CreateFloorSection(entityManager, 0, 10, 760);
            CreatePlatform(entityManager, 5, 680, 3);
            CreateCoin(entityManager, 240, 640);

            // Gap with platforms to jump across
            CreatePlatform(entityManager, 12, 700, 2);
            CreateCoin(entityManager, 520, 660);
            CreatePlatform(entityManager, 16, 640, 2);
            CreatePlatform(entityManager, 20, 580, 2);
            CreateCoin(entityManager, 840, 540);

            // SECTION 2: Floor with obstacles
            CreateFloorSection(entityManager, 24, 15, 760);
            CreateEnemy(entityManager, 1000, 720);
            CreateEnemy(entityManager, 1200, 720);

            // Staircase section
            for (int i = 0; i < 5; i++)
            {
                CreatePlatform(entityManager, 40 + i * 2, 720 - i * 40, 2);
                if (i % 2 == 0)
                    CreateCoin(entityManager, (40 + i * 2) * 40 + 20, 680 - i * 40);
            }

            // SECTION 3: Floating platforms challenge
            CreatePlatform(entityManager, 52, 500, 3);
            CreateCoin(entityManager, 2120, 460);
            CreatePlatform(entityManager, 57, 450, 2);
            CreatePlatform(entityManager, 61, 400, 2);
            CreatePlatform(entityManager, 65, 450, 2);
            CreateCoin(entityManager, 2640, 410);
            CreatePlatform(entityManager, 69, 500, 3);

            // SECTION 4: Floor with pits
            CreateFloorSection(entityManager, 74, 4, 760);
            // Pit
            CreateFloorSection(entityManager, 80, 4, 760);
            CreateEnemy(entityManager, 3240, 720);
            // Another pit
            CreateFloorSection(entityManager, 86, 6, 760);

            // SECTION 5: Vertical section
            CreatePlatform(entityManager, 93, 700, 2);
            CreatePlatform(entityManager, 92, 600, 2);
            CreatePlatform(entityManager, 93, 500, 2);
            CreatePlatform(entityManager, 92, 400, 2);
            CreatePlatform(entityManager, 93, 300, 2);
            CreateCoin(entityManager, 3720, 260);

            // Final section with goal
            CreateFloorSection(entityManager, 96, 4, 760);
            CreateGoal(entityManager, 3900, 680);

            // Add some decorative platforms
            CreatePlatform(entityManager, 30, 600, 4);
            CreateCoin(entityManager, 1240, 560);
            CreatePlatform(entityManager, 35, 500, 3);

            // Bottom kill floor (in case player falls)
            for (int i = 0; i < 100; i++)
            {
                var killFloor = entityManager.CreateEntity("killFloor");
                killFloor.AddComponent(new CTransform(new Vec2(40 * i, 850)));
                killFloor.AddComponent(new CBoundingBox(new Vec2(40, 40), false, true));
            }
        }

        private void CreateFloorSection(EntityManager entityManager, int startX, int length, int y)
        {
            for (int i = 0; i < length; i++)
            {
                var floor = entityManager.CreateEntity("floor");
                floor.AddComponent(new CTransform(new Vec2((startX + i) * 40, y)));
                floor.AddComponent(new CAnimation(assets.GetAnimation("BrickBlock")));
                floor.AddComponent(new CBoundingBox(new Vec2(40, 40), false, true));
            }
        }

        private void CreatePlatform(EntityManager entityManager, int x, int y, int length)
        {
            for (int i = 0; i < length; i++)
            {
                var platform = entityManager.CreateEntity("platform");
                platform.AddComponent(new CTransform(new Vec2((x + i) * 40, y)));
                platform.AddComponent(new CAnimation(assets.GetAnimation("BrickBlock")));
                platform.AddComponent(new CBoundingBox(new Vec2(40, 40), false, true));
            }
        }

        private void CreateCoin(EntityManager entityManager, int x, int y)
        {
            var coin = entityManager.CreateEntity("coin");
            coin.AddComponent(new CTransform(new Vec2(x, y)));
            coin.AddComponent(new CAnimation(assets.GetAnimation("Grenade")));
            coin.AddComponent(new CBoundingBox(new Vec2(30, 30), false, false));
            coin.AddComponent(new CCoin());
        }

        private void CreateEnemy(EntityManager entityManager, int x, int y)
        {
            var enemy = entityManager.CreateEntity("enemy");
            enemy.AddComponent(new CTransform(new Vec2(x, y), new Vec2(50, 0)));
            enemy.AddComponent(new CAnimation(assets.GetAnimation("SnakeHead"))); 
            enemy.AddComponent(new CBoundingBox(new Vec2(35, 35), false, false));
            enemy.AddComponent(new CMovement(50, 50));
            enemy.AddComponent(new CEnemy { PatrolDistance = 120 });
        }

        private void CreateGoal(EntityManager entityManager, int x, int y)
        {
            var goal = entityManager.CreateEntity("goal");
            goal.AddComponent(new CTransform(new Vec2(x, y)));
            goal.AddComponent(new CAnimation(assets.GetAnimation("SnakeFood"))); 
            goal.AddComponent(new CBoundingBox(new Vec2(40, 60), false, false));
            goal.AddComponent(new CText("GOAL!", 24));
        }

        public override void Update(EntityManager entityManager, PhysicsSystem physicsSystem, double deltaSeconds)
        {
            var camera = entityManager.GetEntityWithTag("camera");
            var cameraComponent = camera?.GetComponent<CCamera>();
            var player = entityManager.GetEntityWithTag("player");
            var playerTransform = player?.GetComponent<CTransform>();

            if (cameraComponent != null && playerTransform != null)
            {
                double targetX = Math.Max(cameraComponent.MinX, Math.Min(cameraComponent.MaxX, playerTransform.Position.X));
                cameraComponent.Position = new Vec2(targetX, 400);
            }
            // Handle collisions
            var playerComponent = player?.GetComponent<CPlayer>();
            foreach (var collision in physicsSystem.CollisionEvents)
            {
                // Ground detection for jump reset
                if ((collision.A.Tag == "player" && (collision.B.Tag == "floor" || collision.B.Tag == "platform")) ||
                    (collision.B.Tag == "player" && (collision.A.Tag == "floor" || collision.A.Tag == "platform")))
                {
                    var cInput = player.GetComponent<CInput>();
                    if (playerComponent != null && playerTransform.Velocity.Y >= 0 && !playerComponent.CanJump)
                    {
                        playerComponent.CanJump = true;
                    }
                }

                if ((collision.A.Tag == "player" && collision.B.Tag == "coin") ||
                    (collision.B.Tag == "player" && collision.A.Tag == "coin"))
                {
                    var coin = collision.A.Tag == "coin" ? collision.A : collision.B;
                    if (coin.Active)
                    {
                        coin.Active = false;
                        playerComponent.CoinsCollected++;
                        UpdateScore(entityManager, playerComponent.CoinsCollected);
                    }
                }

                if ((collision.A.Tag == "player" && (collision.B.Tag == "enemy" || collision.B.Tag == "killFloor")) ||
                    (collision.B.Tag == "player" && (collision.A.Tag == "enemy" || collision.A.Tag == "killFloor")))
                {
                    playerTransform.Position = new Vec2(100, 600);
                    playerTransform.Velocity = Vec2.Zero;
                }

                if ((collision.A.Tag == "player" && collision.B.Tag == "goal") ||
                    (collision.B.Tag == "player" && collision.A.Tag == "goal"))
                {
                    UpdateScore(entityManager, -1);
                }
            }

            var enemies = entityManager.GetEntitiesWithTag("enemy");
            foreach (var enemy in enemies)
            {
                var enemyTransform = enemy.GetComponent<CTransform>();
                var enemyData = enemy.GetComponent<CEnemy>();

                enemyData.DistanceTraveled += Math.Abs(enemyTransform.Velocity.X * deltaSeconds);

                if (enemyData.DistanceTraveled >= enemyData.PatrolDistance)
                {
                    enemyTransform.Velocity = new Vec2(-enemyTransform.Velocity.X, 0);
                    enemyData.DistanceTraveled = 0;
                }
            }
        }

        private void UpdateScore(EntityManager entityManager, int coins)
        {
            var score = entityManager.GetEntityWithTag("score");
            var scoreText = score?.GetComponent<CText>();
            if (scoreText != null)
            {
                if (coins == -1)
                    scoreText.Text = "LEVEL COMPLETE!";
                else
                    scoreText.Text = $"Coins: {coins}";
            }
        }

        class CPlayer : Component
        {
            public bool CanJump = false;
            public int CoinsCollected = 0;
        }

        class CCoin : Component { }

        class CEnemy : Component
        {
            public double PatrolDistance = 100;
            public double DistanceTraveled = 0;
        }
    }
}