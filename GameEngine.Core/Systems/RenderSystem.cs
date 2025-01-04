using GameEngine.Core.Components;
using SkiaSharp;

namespace GameEngine.Core.Systems
{
    public class RenderSystem : ISystem
    {
        private readonly EntityManager entityManager;
        private readonly RenderOptions options;

        private readonly List<double> _fpsSamples = new();

        private double _fps;


        public RenderSystem(EntityManager entityManager, RenderOptions options)
        {
            this.entityManager = entityManager;
            this.options = options;
        }

        public void Update(EntityManager entityManager, double deltaTime)
        {
            if (deltaTime <= 0)
                return;

            double currentFps = 1000.0 / deltaTime;

            if (options.FpsSmoothingSamples <= 1)
            {
                _fps = currentFps;
                return;
            }

            _fpsSamples.Add(currentFps);

            if (_fpsSamples.Count > options.FpsSmoothingSamples)
                _fpsSamples.RemoveAt(0);

            _fps = _fpsSamples.Average();
        }

        public void DrawEntitiesToCanvas(SKCanvas canvas)
        {
            canvas.Clear(SKColors.White);

            var entities = entityManager.GetEntitiesWithComponent<CTransform>();
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
                    using var paint = new SKPaint
                    {
                        FilterQuality = SKFilterQuality.High,
                        IsAntialias = true
                    };
                    canvas.DrawBitmap(texture, sourceRect, destRect, paint);
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

            if (options.DrawFps)
            {
                DrawFpsCounter(canvas, _fps);
            }
        }

        private static Vec2 FindEntityCenter(Entity entity)
        {
            var transform = entity.GetComponent<CTransform>();
            var hasBoundingBox = entity.HasComponent<CBoundingBox>();

            if (hasBoundingBox)
            {
                var boundingBox = entity.GetComponent<CBoundingBox>();
                return new Vec2(transform.Position.X + (boundingBox.Width / 2),
                                transform.Position.Y + (boundingBox.Height / 2));
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

            var rect = new SKRect(
                (float)transform.Position.X,
                (float)transform.Position.Y,
                (float)transform.Position.X + (float)boundingBox.Width,
                (float)transform.Position.Y + (float)boundingBox.Height);

            canvas.DrawRect(rect, new SKPaint
            {
                Color = options.BoundingBoxColor,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2,
                IsAntialias = true
            });
        }

        private void DrawFpsCounter(SKCanvas canvas, double fps)
        {
            using var paint = new SKPaint
            {
                Color = SKColors.Black,
                TextSize = 24,
                IsAntialias = true
            };

            string fpsText = $"FPS: {fps:0.0}";
            float textWidth = paint.MeasureText(fpsText);
            float margin = 10;

            SKRect canvasBounds = canvas.DeviceClipBounds;

            float x = canvasBounds.Right - textWidth - margin;
            float y = margin + paint.TextSize;

            canvas.DrawText(fpsText, x/3, y, paint);
        }
    }

    public class RenderOptions
    {
        public bool DrawBoundingBoxes { get; set; } = true;
        public bool DrawAnimations { get; set; } = true;
        public bool DrawEntityCenters { get; set; } = false;
        public bool DrawFps { get; set; } = false;
        public SKColor BoundingBoxColor { get; set; } = SKColor.Parse("#FF0000");
        public int FpsSmoothingSamples { get; set; } = 1;
        public static RenderOptions Default => new();
    }
}
