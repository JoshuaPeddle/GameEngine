using GameEngine.Core.Components;
using SkiaSharp;
using SkiaSharp.Views.Desktop;

namespace GameEngine.Core.Systems
{
    public class RenderSystem : ISystem
    {
        private readonly SKGLControl skControl;
        private readonly EntityManager entityManager;
        private readonly RenderOptions options;

        public RenderSystem(SKGLControl skControl, EntityManager entityManager, RenderOptions options)
        {
            this.skControl = skControl;
            if (skControl != null)
                skControl.PaintSurface += OnPaintSurface;
            this.entityManager = entityManager;
            this.options = options;
        }

        public void Update(EntityManager entityManager, double deltaTime)
        {
            skControl.Invalidate();
        }

        private void OnPaintSurface(object? sender, SKPaintGLSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;
            canvas.Clear(SKColors.White);

            var entities = entityManager.GetEntitiesWithComponent<CTransform>();

            DrawEntitiesToCanvas(canvas, entities);
        }

        public void DrawEntitiesToCanvas(SKCanvas canvas, List<Entity> entities)
        {
            foreach (var entity in entities)
            {
                if (options.DrawAnimations && entity.HasComponent<CAnimation>())
                {
                    var animation = entity.GetComponent<CAnimation>();
                    SKBitmap texture = animation.Texture;
                    SKRect sourceRect = animation.GetSourceRect();

                    float frameWidth = sourceRect.Width;
                    float frameHeight = sourceRect.Height;
                    var animationSize = new Vec2(frameWidth, frameHeight);

                    var entityCenter = FindEntityCenter(entity);

                    var destRect = new SKRect(
                        (float)(entityCenter.X - (animationSize.X / 2)),
                        (float)(entityCenter.Y - (animationSize.Y / 2)),
                        (float)(entityCenter.X + (animationSize.X / 2)),
                        (float)(entityCenter.Y + (animationSize.Y / 2))
                    );

                    canvas.DrawBitmap(texture, sourceRect, destRect);
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

            else
            {
                return new Vec2(transform.Position.X, transform.Position.Y);
            }
        }

        private static void DrawEntityCenterDebugPoints(SKCanvas canvas, Vec2 position)
        {
            canvas.DrawPoint((float)position.X, (float)position.Y, new SKPaint
            {
                Color = SKColors.Red,
                StrokeWidth = 1
            });
        }

        private void DrawBoundingBox(SKCanvas canvas, Entity entity)
        {
            var transform = entity.GetComponent<CTransform>();
            var boundingBox = entity.GetComponent<CBoundingBox>();

            var rect = new SKRect((float)transform.Position.X, (float)transform.Position.Y, (float)transform.Position.X + (float)boundingBox.Width, (float)transform.Position.Y + (float)boundingBox.Height);
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
