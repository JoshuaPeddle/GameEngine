using GameEngine.Core.Components;
using SkiaSharp;
using SkiaSharp.Views.Desktop;

namespace GameEngine.Core.Systems
{
    public class RenderSystem : ISystem
    {
        private readonly SKControl skControl;
        private readonly EntityManager entityManager;
        private readonly RenderOptions options;

        public RenderSystem(SKControl skControl, EntityManager entityManager, RenderOptions options)
        {
            this.skControl = skControl;
            skControl.PaintSurface += OnPaintSurface;
            this.entityManager = entityManager;
            this.options = options;
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
                if (options.DrawAnimations && entity.HasComponent<CAnimation>())
                {
                    var transform = entity.GetComponent<CTransform>();
                    var animation = entity.GetComponent<CAnimation>();

                    using var frame = animation.GetCurrentFrame();
                    var aimationSize = new Vec2(frame.Width, frame.Height);

                    var entityCenter = FindEntityCenter(entity);

                    canvas.DrawImage(frame, new SKPoint((float)entityCenter.X - (aimationSize.X / 2),
                                                        (float)entityCenter.Y - (aimationSize.Y / 2)));

                }
                if (options.DrawBoundingBoxes && entity.HasComponent<CBoundingBox>())
                {
                    DrawBoundingBox(canvas, entity);
                }
                if (options.DrawEntityCenters)
                {
                    DrawEntityCenterDebugPoints(canvas, FindEntityCenter(entity));
                }
            }
        }

        private static Vec2 FindEntityCenter(Entity entity)
        {
            var transform = entity.GetComponent<CTransform>();
            var hasBoundingBox = entity.HasComponent<CBoundingBox>();

            // If it has a bounding box, we can find the center of the entity using that
            if (hasBoundingBox)
            {
                var boundingBox = entity.GetComponent<CBoundingBox>();
                return new Vec2(transform.Position.X + (boundingBox.Width / 2), transform.Position.Y + (boundingBox.Height / 2));
            }
            else if (entity.HasComponent<CAnimation>())
            {
                var animation = entity.GetComponent<CAnimation>();
                var frame = animation.GetCurrentFrame();
                var aimationSize = new Vec2(frame.Width, frame.Height);

                return new Vec2(transform.Position.X + (aimationSize.X / 2), transform.Position.Y + (aimationSize.Y / 2));
            }
            else
            {
                return new Vec2(transform.Position.X, transform.Position.Y);
            }
        }

        private static void DrawEntityCenterDebugPoints(SKCanvas canvas, Vec2 position)
        {
            canvas.DrawPoint(position.X, position.Y, new SKPaint
            {
                Color = SKColors.Red,
                StrokeWidth = 5
            });
        }

        private void DrawBoundingBox(SKCanvas canvas, Entity entity)
        {
            var transform = entity.GetComponent<CTransform>();
            var boundingBox = entity.GetComponent<CBoundingBox>();

            var rect = new SKRect((float)transform.Position.X, (float)transform.Position.Y, (float)transform.Position.X + boundingBox.Width, (float)transform.Position.Y + boundingBox.Height);
            canvas.DrawRect(rect, new SKPaint
            {
                Color = options.BoundingBoxColor,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2
            });
        }
    }

    public class RenderOptions
    {
        public bool DrawBoundingBoxes { get; set; } = true;
        public bool DrawAnimations { get; set; } = true;
        public bool DrawEntityCenters { get; set; } = false;
        public SKColor BoundingBoxColor { get; set; } = SKColor.Parse("#FF0000");
        public static RenderOptions Default => new();
    }
}
