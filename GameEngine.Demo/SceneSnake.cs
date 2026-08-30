using GameEngine.Core;
using GameEngine.Core.Components;
using GameEngine.Core.Systems;
using System;
using System.Collections.Generic;

namespace GameEngine.Demo
{
    public class SceneSnake : Scene
    {
        private Assets? assets;
        private Assets Assets => assets
            ?? throw new InvalidOperationException("SceneSnake has not been initialized.");
        private Action<Scene>? _resetScene;
        private Action<Scene> ResetSceneCallback => _resetScene
            ?? throw new InvalidOperationException("SceneSnake has not been initialized.");

        int _width = 20;
        int _height = 20;

        public override int VirtualWidth => 800;
        public override int VirtualHeight => 800;

        public override void Initialize(EntityManager entityManager, InputManager inputManager, AudioSystem? audioPlayer, Action<Scene> ResetScene)
        {
            assets ??= new("assets.json", AssetSource);

            var snakeHead = entityManager.CreateEntity("SnakeHead");
            snakeHead.AddComponent(new CTransform(new Vec2(40, 80)));
            snakeHead.AddComponent<CInput>();
            snakeHead.AddComponent(new CBoundingBox(new Vec2(40, 40), false, false));
            snakeHead.AddComponent(new CAnimation(assets.GetAnimation("SnakeHead")));

            var cSnake = new CSnake();
            cSnake.Segments.Add(snakeHead);
            snakeHead.AddComponent(cSnake);

            inputManager.AddAction(GeKeys.W, "Up");
            inputManager.AddAction(GeKeys.S, "Down");
            inputManager.AddAction(GeKeys.A, "Left");
            inputManager.AddAction(GeKeys.D, "Right");

            inputManager.ActionMapper.MapActionToComponent<CSnake>("Up", snakeHead, (csnake, active) =>
            {
                if (active) csnake.Direction = new Vec2(0, -1);
            }, true);
            inputManager.ActionMapper.MapActionToComponent<CSnake>("Down", snakeHead, (csnake, active) =>
            {
                if (active) csnake.Direction = new Vec2(0, 1);
            }, true);
            inputManager.ActionMapper.MapActionToComponent<CSnake>("Left", snakeHead, (csnake, active) =>
            {
                if (active) csnake.Direction = new Vec2(-1, 0);
            }, true);
            inputManager.ActionMapper.MapActionToComponent<CSnake>("Right", snakeHead, (csnake, active) =>
            {
                if (active) csnake.Direction = new Vec2(1, 0);
            }, true);

            var score = entityManager.CreateEntity("Score");
            score.AddComponent(new CTransform(new Vec2(110, 80)));
            score.AddComponent(new CBoundingBox(new Vec2(200, 50), false, false));
            score.AddComponent(new CText("Score: 0", 32));

            AddFood(entityManager);
            CreateBoundingEntities(entityManager);
            _resetScene = ResetScene;
        }

        private void CreateBoundingEntities(EntityManager entityManager)
        {
            for (int x = 0; x < _width; x++)
            {
                var top = entityManager.CreateEntity("Wall");
                top.AddComponent(new CTransform(new Vec2(x * 40, 0)));
                top.AddComponent(new CBoundingBox(new Vec2(40, 40), false, false));
                top.AddComponent(new CAnimation(Assets.GetAnimation("BrickBlock")));
                var bottom = entityManager.CreateEntity("Wall");
                bottom.AddComponent(new CTransform(new Vec2(x * 40, (_height - 1) * 40)));
                bottom.AddComponent(new CBoundingBox(new Vec2(40, 40), false, false));
                bottom.AddComponent(new CAnimation(Assets.GetAnimation("BrickBlock")));
            }
            for (int y = 1; y < _height - 1; y++)
            {
                var left = entityManager.CreateEntity("Wall");
                left.AddComponent(new CTransform(new Vec2(0, y * 40)));
                left.AddComponent(new CBoundingBox(new Vec2(40, 40), false, false));
                left.AddComponent(new CAnimation(Assets.GetAnimation("BrickBlock")));
                var right = entityManager.CreateEntity("Wall");
                right.AddComponent(new CTransform(new Vec2((_width - 1) * 40, y * 40)));
                right.AddComponent(new CBoundingBox(new Vec2(40, 40), false, false));
                right.AddComponent(new CAnimation(Assets.GetAnimation("BrickBlock")));
            }
        }

        public override void Update(EntityManager entityManager, SystemContainer systems, double deltaSeconds)
        {
            var physicsSystem = systems.Get<PhysicsSystem>();

            var snakeHead = entityManager.GetEntitiesWithComponent<CSnake>()[0];
            var cSnake = snakeHead.GetComponent<CSnake>();

            cSnake.TimeSinceLastMove += (float)deltaSeconds;

            if (cSnake.TimeSinceLastMove >= cSnake.MoveInterval)
            {
                cSnake.TimeSinceLastMove -= cSnake.MoveInterval;

                var collisions = physicsSystem.CollisionEvents;
                foreach (var collision in collisions)
                {
                    var tagA = collision.A.Tag;
                    var tagB = collision.B.Tag;
                    if ((tagA == "SnakeHead" && tagB == "Food") ||
                        (tagB == "SnakeHead" && tagA == "Food"))
                    {
                        HandleSnakeHeadFoodCollision(collision, entityManager);
                    }
                    else if ((tagA == "SnakeHead" && tagB == "Wall") ||
                             (tagB == "SnakeHead" && tagA == "Wall"))
                    {
                        ResetSceneCallback(this);
                    }
                    else if ((tagA == "SnakeHead" && tagB == "SnakeBody") ||
                             (tagB == "SnakeHead" && tagA == "SnakeBody"))
                    {
                        ResetSceneCallback(this);
                    }
                }

                var oldPositions = new List<Vec2>();
                foreach (var segment in cSnake.Segments)
                {
                    oldPositions.Add(segment.GetComponent<CTransform>().Position);
                }

                var headTransform = snakeHead.GetComponent<CTransform>();
                headTransform.PreviousPosition = headTransform.Position;
                headTransform.Position += cSnake.Direction * cSnake.TileSize;
                UpdateSnakeHeadRotation(cSnake.Direction, headTransform);

                for (int i = 1; i < cSnake.Segments.Count; i++)
                {
                    var segmentTransform = cSnake.Segments[i].GetComponent<CTransform>();
                    segmentTransform.PreviousPosition = segmentTransform.Position;
                    segmentTransform.Position = oldPositions[i - 1];
                }

            }
        }

        private void HandleSnakeHeadFoodCollision(CollisionEvent collision, EntityManager entityManager)
        {
            var snakeHead = collision.A.Tag == "SnakeHead" ? collision.A : collision.B;
            var cSnake = snakeHead.GetComponent<CSnake>();
            
            cSnake.Length++;
            cSnake.Score++;
            var theFood = collision.A.Tag == "Food" ? collision.A : collision.B;

            theFood.Active = false;
            GrowSnake(entityManager, cSnake);

            var score = entityManager.GetEntityWithTag("Score");
            if (score != null && score.TryGetComponent<CText>(out var scoreText))
            {
                scoreText.Text = $"Score: {cSnake.Score}";
            }

            AddFood(entityManager);
        }
  
        private void GrowSnake(EntityManager entityManager, CSnake cSnake)
        {
            var lastSegment = cSnake.Segments[^1];
            var lastTransform = lastSegment.GetComponent<CTransform>();

            var bodySegment = entityManager.CreateEntity("SnakeBody");
            bodySegment.AddComponent(new CTransform(lastTransform.Position));
            bodySegment.AddComponent(new CBoundingBox(new Vec2(40, 40), false, false));
            bodySegment.AddComponent(new CAnimation(Assets.GetAnimation("SnakeBody")));

            cSnake.Segments.Add(bodySegment);
        }

        private void AddFood(EntityManager entityManager)
        {
            var random = new Random();
            var x = random.Next(1, _width-1) * 40;
            var y = random.Next(1, _height-1) * 40;

            var food = entityManager.CreateEntity("Food");
            food.AddComponent(new CTransform(new Vec2(x, y)));
            food.AddComponent(new CBoundingBox(new Vec2(40, 40), false, false));
            food.AddComponent(new CAnimation(Assets.GetAnimation("SnakeFood")));
        }

        private static void UpdateSnakeHeadRotation(Vec2 direction, CTransform headTransform)
        {
            if (direction.X == 1)
                headTransform.Rotation = 270;
            else if (direction.X == -1)
                headTransform.Rotation = 90;
            else if (direction.Y == 1)
                headTransform.Rotation = 0;
            else if (direction.Y == -1)
                headTransform.Rotation = 180;
        }
    }

    internal class CSnake : Component
    {
        public List<Entity> Segments { get; } = new();

        public Vec2 Direction = new Vec2(1, 0);

        public int Length = 1;

        public int Score = 0;

        public int TileSize = 40;

        public float MoveInterval = 0.20f;

        public float TimeSinceLastMove = 0f;
    }
}
