using GameEngine.Core.Components;
using SkiaSharp.Views.Desktop;
using SkiaSharp;

namespace GameEngine.Core
{
    public interface ISystem
    {
        void Update(EntityManager entityManager, float deltaTime);
    }

    // MovementSystem.cs
    public class MovementSystem : ISystem
    {
        public void Update(EntityManager entityManager, float deltaTime)
        {
            var entities = entityManager.GetEntitiesWithComponent<CTransform>();
            foreach (var entity in entities)
            {
                var transform = entity.GetComponent<CTransform>();
                // Update transform based on velocity and input
                // Logic moved from SceneBasic.Movement
            }
        }
    }

    // AnimationSystem.cs
    public class AnimationSystem : ISystem
    {
        public void Update(EntityManager entityManager, float deltaTime)
        {
            var entities = entityManager.GetEntitiesWithComponent<CAnimation>();
            foreach (var entity in entities)
            {
                var animation = entity.GetComponent<CAnimation>();
                animation.Update(deltaTime);
            }
        }
    }

    // InputSystem.cs
    public class InputSystem : ISystem
    {
        private readonly InputManager inputManager;
        private readonly ActionMapper actionMapper;

        public InputSystem(InputManager inputManager, ActionMapper actionMapper)
        {
            this.inputManager = inputManager;
            this.actionMapper = actionMapper;
        }

        public void Update(EntityManager entityManager, float deltaTime)
        {
            // Process input and update components
        }
    }

    // RenderSystem.cs
    public class RenderSystem : ISystem
    {
        private readonly SKControl skControl;
        private readonly EntityManager entityManager;

        public RenderSystem(SKControl skControl, EntityManager entityManager)
        {
            this.skControl = skControl;
            skControl.PaintSurface += OnPaintSurface;
            this.entityManager = entityManager;
        }

        public void Update(EntityManager entityManager, float deltaTime)
        {
            skControl.Invalidate();
        }

        private void OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;
            canvas.Clear(SKColors.White);

            var entities = entityManager.GetEntitiesWithComponent<CTransform>();

            foreach (var entity in entities)
            {
                if (entity.HasComponent<CAnimation>())
                {
                    var transform = entity.GetComponent<CTransform>();
                    var animation = entity.GetComponent<CAnimation>();

                    var frame = animation.GetCurrentFrame();
                    canvas.DrawImage(frame, new SKPoint((float)transform.Position.X, (float)transform.Position.Y));
                }
            }
        }
    }
}
