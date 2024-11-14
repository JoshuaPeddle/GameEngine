using GameEngine.Core.Components;
using SkiaSharp;
using SkiaSharp.Views.Desktop;

namespace GameEngine.Core.Systems
{
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

        private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
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

                    using var frame = animation.GetCurrentFrame();
                    canvas.DrawImage(frame, new SKPoint((float)transform.Position.X, (float)transform.Position.Y));
                }
            }
        }
    }
}
