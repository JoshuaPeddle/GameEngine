using GameEngine.Core.Components;
using SkiaSharp;

namespace GameEngine.Core.Systems
{
    public class RenderSystem : ISystem
    {
        private readonly EntityManager entityManager;
        private readonly RenderOptions options;

        private readonly List<double> _fpsSamples = [];

        private double _fps;

        private readonly static SKPaint _animationPaint = new SKPaint
        {
            FilterQuality = SKFilterQuality.High,
            IsAntialias = true
        };

        private readonly static SKPaint _boundingBoxPaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2,
            IsAntialias = true
        };

        private readonly static SKPaint _debugPointPaint = new SKPaint
        {
            Color = SKColors.Red,
            StrokeWidth = 1
        };

        private readonly static SKPaint _fpsPaint = new SKPaint
        {
            Color = SKColors.Black,
            TextSize = 24,
            IsAntialias = true
        };


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

            // 1. Compute scale factors (width-based and height-based)
            var canvasBounds = canvas.LocalClipBounds; // e.g., (0,0, screenWidth, screenHeight)
            float screenWidth = canvasBounds.Width;
            float screenHeight = canvasBounds.Height;

            float scaleX = screenWidth / options.VirtualWidth;
            float scaleY = screenHeight / options.VirtualHeight;

            float finalScale = 1.0f;

            switch (options.ScalingStrategy)
            {
                case ScalingStrategy.Letterbox:
                    // Use the smaller scale so the entire game area is visible
                    finalScale = Math.Min(scaleX, scaleY);
                    break;
                case ScalingStrategy.Stretch:
                    // Use the entire screen, even if it distorts
                    finalScale = scaleX;
                    // or handle X and Y separately, if you *really* want full stretch
                    break;
                case ScalingStrategy.Crop:
                    // Possibly use the larger scale and let some of the game area go off screen
                    finalScale = Math.Max(scaleX, scaleY);
                    break;
            }

            // 2. If you want letterboxing, compute the leftover space
            float scaledWidth = options.VirtualWidth * finalScale;
            float scaledHeight = options.VirtualHeight * finalScale;

            float leftoverX = (screenWidth - scaledWidth) / 2f;
            float leftoverY = (screenHeight - scaledHeight) / 2f;

            // 3. Apply transformations so that (0,0) in *game space* ends up at
            // leftoverX, leftoverY in *screen space*, and everything is scaled by finalScale
            canvas.Save();
            canvas.Translate(leftoverX, leftoverY);
            canvas.Scale(finalScale, finalScale);

            // 4. Apply camera transformation if available.
            // We assume the camera entity has the tag "camera"
            var cameraEntity = entityManager.GetEntityWithTag("camera");
            if (cameraEntity != null && cameraEntity.HasComponent<CCamera>())
            {
                var camera = cameraEntity.GetComponent<CCamera>();

                // Here we translate the world so that the camera's position is at the center
                // of our virtual space and then scale by the camera's zoom factor.
                // The order of operations is:
                //   a) Translate so that camera.Position becomes the origin.
                //   b) Scale by Camera.Zoom (zoom in or out).
                //   c) Translate back to put the camera at the center of the virtual space.
                canvas.Translate(options.VirtualWidth / 2, options.VirtualHeight / 2);
                canvas.Scale(camera.Zoom, camera.Zoom);
                canvas.Translate(-((float)camera.Position.X), -((float)camera.Position.Y));
            }

            // 5. Draw the entities in world space.
            DrawEntities(canvas);

            // 5. Restore so that subsequent UI draws (like FPS counter) are in pixel space
            canvas.Restore();

            if (options.DrawFps)
            {
                DrawFpsCounter(canvas, _fps);
            }
        }

        private void DrawEntities(SKCanvas canvas)
        {
            var entities = entityManager.GetEntitiesWithComponent<CTransform>();
            foreach (var entity in entities)
            {
                // The existing logic is fine because from this point forward,
                // your (x, y) are in "virtual" coordinates, and Skia is scaling them.
                if (options.DrawAnimations && entity.HasComponent<CAnimation>())
                {
                    DrawAnimation(canvas, entity);
                }

                if (entity.HasComponent<CText>())
                {
                    var text = entity.GetComponent<CText>();
                    canvas.DrawText(text.Text, (float)entity.GetComponent<CTransform>().Position.X, (float)entity.GetComponent<CTransform>().Position.Y, text.Paint);
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

        private static void DrawAnimation(SKCanvas canvas, Entity entity)
        {
            var animation = entity.GetComponent<CAnimation>();
            SKBitmap texture = animation.Texture;
            SKRect sourceRect = animation.GetSourceRect();

            float frameWidth = sourceRect.Width;
            float frameHeight = sourceRect.Height;
            var animationSize = new Vec2(frameWidth, frameHeight);

            var entityCenter = FindEntityCenter(entity);

            float rotationAngle = entity.GetComponent<CTransform>().Rotation;

            canvas.Save();

            canvas.Translate((float)entityCenter.X, (float)entityCenter.Y);

            canvas.RotateDegrees(rotationAngle);

            SKRect destRect = new SKRect(
                -(float)(animationSize.X / 2),
                -(float)(animationSize.Y / 2),
                (float)(animationSize.X / 2),
                (float)(animationSize.Y / 2)
            );

            canvas.DrawBitmap(texture, sourceRect, destRect, _animationPaint);

            canvas.Restore();
        }

        private static Vec2 FindEntityCenter(Entity entity)
        {
            var transform = entity.GetComponent<CTransform>();
            if (entity.HasComponent<CBoundingBox>())
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
            canvas.DrawPoint((float)position.X, (float)position.Y, _debugPointPaint);
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

            canvas.DrawRect(rect, _boundingBoxPaint);
        }

        private static void DrawFpsCounter(SKCanvas canvas, double fps)
        {
            string fpsText = $"FPS: {fps:0.0}";
            float margin = 10;
            // Coordinates now are in *actual* pixels
            canvas.DrawText(fpsText, margin, margin + _fpsPaint.TextSize, _fpsPaint);

        }

        public void SetVirtualDimensions(float width, float height)
        {
            options.VirtualWidth = width;
            options.VirtualHeight = height;
        }
    }

    public class RenderOptions
    {
        public static RenderOptions Default => new();
        public float VirtualWidth { get; set; } = 1600; 
        public float VirtualHeight { get; set; } = 1600; 
        public bool DrawBoundingBoxes { get; set; } = true;
        public bool DrawAnimations { get; set; } = true;
        public bool DrawEntityCenters { get; set; } = false;
        public bool DrawFps { get; set; } = false;
        public SKColor BoundingBoxColor { get; set; } = SKColor.Parse("#FF0000");
        public int FpsSmoothingSamples { get; set; } = 1;
        public ScalingStrategy ScalingStrategy { get; set; } = ScalingStrategy.Letterbox;
    }

    public enum ScalingStrategy
    {
        Letterbox,
        Stretch,
        Crop
    }
}