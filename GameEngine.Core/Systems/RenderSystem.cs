using GameEngine.Core.Components;
using SkiaSharp;

namespace GameEngine.Core.Systems
{
    public class RenderSystem : ISystem
    {
        public readonly RenderOptions options;

        private double[] _fpsSamples = [];
        private int _fpsSampleIndex;
        private int _fpsSampleCount;
        private double _fpsSampleSum;
        private double _fps;

        public RenderSystem(RenderOptions options)
        {
            this.options = options;
        }

        public double Fps => _fps;

        public void Update(EntityManager entityManager, double deltaSeconds)
        {
            if (deltaSeconds <= 0)
                return;

            if (options.DrawFps)
            {
                double currentFps = 1.0 / deltaSeconds;

                if (options.FpsSmoothingSamples <= 1)
                {
                    _fps = currentFps;
                    return;
                }

                // Lazily allocate or resize if the option changed
                if (_fpsSamples.Length != options.FpsSmoothingSamples)
                {
                    _fpsSamples = new double[options.FpsSmoothingSamples];
                    _fpsSampleIndex = 0;
                    _fpsSampleCount = 0;
                    _fpsSampleSum = 0;
                }

                _fpsSampleSum -= _fpsSamples[_fpsSampleIndex];
                _fpsSamples[_fpsSampleIndex] = currentFps;
                _fpsSampleSum += currentFps;

                _fpsSampleIndex = (_fpsSampleIndex + 1) % _fpsSamples.Length;
                if (_fpsSampleCount < _fpsSamples.Length)
                    _fpsSampleCount++;

                _fps = _fpsSampleSum / _fpsSampleCount;
            }
        }

        /// <summary>
        /// Thread-safe overload: renders from an immutable <see cref="RenderSnapshot"/>
        /// that was built on the engine thread. No EntityManager access occurs.
        /// </summary>
        public void DrawEntitiesToCanvas(SKCanvas canvas, RenderSnapshot snapshot)
        {
            canvas.Clear(SKColors.White);

            var canvasBounds = canvas.LocalClipBounds;
            var viewport = ViewportFor(new Vec2(canvasBounds.Width, canvasBounds.Height));

            canvas.Save();
            canvas.Translate((float)viewport.OffsetX, (float)viewport.OffsetY);
            canvas.Scale((float)viewport.ScaleX, (float)viewport.ScaleY);

            if (snapshot.ActiveCamera is { } camera)
            {
                canvas.Translate(options.VirtualWidth / 2, options.VirtualHeight / 2);
                canvas.Scale(camera.Zoom, camera.Zoom);
                canvas.Translate(-((float)camera.Position.X), -((float)camera.Position.Y));

                DrawSnapshotEntries(canvas, snapshot, camera);
            }
            else
            {
                DrawSnapshotEntries(canvas, snapshot, null);
            }

            canvas.Restore();

            if (options.DrawFps)
            {
                DrawFpsCounter(canvas, _fps);
            }
        }

        /// <summary>
        /// Legacy overload that reads directly from EntityManager.
        /// ⚠️ Not thread-safe — only safe when the caller and the engine share a thread.
        /// Every runner has moved to the <see cref="RenderSnapshot"/> overload above;
        /// the only remaining caller is Runner.Avalonia.Old's SkiaCanvasControl.
        /// </summary>
        // ── Snapshot-based rendering ────────────────────────────────────────

        private void DrawSnapshotEntries(SKCanvas canvas, RenderSnapshot snapshot,
            RenderSnapshot.CameraData? camera)
        {
            ReadOnlySpan<RenderSnapshot.Entry> entries = snapshot.Entries;

            // Optional visibility culling when a camera is active
            bool cull = false;
            SKRect cameraBounds = default;
            if (camera is { } cam)
            {
                cull = true;
                float halfViewW = options.VirtualWidth / cam.Zoom / 2;
                float halfViewH = options.VirtualHeight / cam.Zoom / 2;

                // The camera position is the centre of the view, so the box extends half a
                // view height above it as well as below.
                cameraBounds = new SKRect(
                    (float)cam.Position.X - halfViewW,
                    (float)cam.Position.Y - halfViewH,
                    (float)cam.Position.X + halfViewW,
                    (float)cam.Position.Y + halfViewH);
                cameraBounds.Inflate(100, 100);
            }

            for (int i = 0; i < entries.Length; i++)
            {
                ref readonly var entry = ref entries[i];

                // Visibility culling
                if (cull)
                {
                    if (entry.BoundingBox.HasValue)
                    {
                        var bb = entry.BoundingBox.Value;
                        var entityRect = new SKRect(
                            (float)entry.Transform.Position.X,
                            (float)entry.Transform.Position.Y,
                            (float)(entry.Transform.Position.X + bb.Width),
                            (float)(entry.Transform.Position.Y + bb.Height));

                        if (!cameraBounds.IntersectsWith(entityRect))
                            continue;
                    }
                    else if (!cameraBounds.Contains(
                                 (float)entry.Transform.Position.X,
                                 (float)entry.Transform.Position.Y))
                    {
                        continue;
                    }
                }

                // Draw animation
                if (options.DrawAnimations && entry.Animation is { ShouldDraw: true } anim)
                {
                    DrawSnapshotAnimation(canvas, entry, anim);
                }

                // Draw text
                if (entry.Text is { ShouldDraw: true } text)
                {
                    _textFont.Size = text.Size;
                    canvas.DrawText(text.Text,
                        (float)entry.Transform.Position.X,
                        (float)entry.Transform.Position.Y,
                        text.TextAlign,
                        _textFont,
                        text.Paint);
                }

                // Draw bounding box (debug)
                if (options.DrawBoundingBoxes && entry.BoundingBox is { } bbox)
                {
                    var rect = new SKRect(
                        (float)entry.Transform.Position.X,
                        (float)entry.Transform.Position.Y,
                        (float)entry.Transform.Position.X + (float)bbox.Width,
                        (float)entry.Transform.Position.Y + (float)bbox.Height);
                    canvas.DrawRect(rect, _boundingBoxPaint);
                }

                // Draw entity center (debug)
                if (options.DrawEntityCenters)
                {
                    var center = FindEntryCenter(entry);
                    canvas.DrawPoint((float)center.X, (float)center.Y, _debugPointPaint);
                }
            }
        }

        private static void DrawSnapshotAnimation(SKCanvas canvas,
            in RenderSnapshot.Entry entry, in RenderSnapshot.AnimationData anim)
        {
            float frameWidth = anim.SourceRect.Width;
            float frameHeight = anim.SourceRect.Height;

            Vec2 center = FindEntryCenter(entry);

            canvas.Save();
            canvas.Translate((float)center.X, (float)center.Y);
            canvas.RotateDegrees((float)entry.Transform.Rotation);

            SKRect destRect = new SKRect(
                -(frameWidth / 2),
                -(frameHeight / 2),
                (frameWidth / 2),
                (frameHeight / 2));

            canvas.DrawBitmap(anim.Texture, anim.SourceRect, destRect, _animationSampling, _animationPaint);
            canvas.Restore();
        }

        private static Vec2 FindEntryCenter(in RenderSnapshot.Entry entry)
        {
            if (entry.BoundingBox is { } bb)
            {
                return new Vec2(
                    entry.Transform.Position.X + bb.Width / 2,
                    entry.Transform.Position.Y + bb.Height / 2);
            }
            return entry.Transform.Position;
        }

        private static void DrawFpsCounter(SKCanvas canvas, double fps)
        {
            string fpsText = $"FPS: {fps:0.0}";
            float margin = 10;
            canvas.DrawText(
                fpsText,
                margin,
                margin + _fpsFont.Size,
                SKTextAlign.Left,
                _fpsFont,
                _fpsPaint);

        }

        public ViewportTransform ViewportFor(Vec2 realResolution) =>
            ViewportTransform.Create(
                realResolution,
                new Vec2(options.VirtualWidth, options.VirtualHeight),
                options.ScalingStrategy);

        public void SetVirtualDimensions(float width, float height)
        {
            options.VirtualWidth = width;
            options.VirtualHeight = height;
        }

        private readonly SKFont _textFont = new(SKTypeface.Default, 24);

        private readonly static SKSamplingOptions _animationSampling =
            new(SKFilterMode.Linear, SKMipmapMode.None);

        private readonly static SKPaint _animationPaint = new SKPaint
        {
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
            IsAntialias = true
        };

        private readonly static SKFont _fpsFont = new(SKTypeface.Default, 24);
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
