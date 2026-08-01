using GameEngine.Core.Components;
using SkiaSharp;

namespace GameEngine.Core;

/// <summary>
/// A reusable buffer holding the entity state needed to render one frame.
/// Filled on the engine thread by <see cref="EntityManager.BuildRenderSnapshot"/>,
/// read on the UI thread. All mutable component data is copied by value so the
/// renderer never reads a live component.
/// <para>
/// Instances are pooled and recycled by <see cref="Engine"/>. The buffer returned by
/// <see cref="Engine.GetRenderSnapshot"/> stays valid until that reader asks for the next
/// one; the engine never fills a buffer a reader still holds. Steady-state rendering
/// allocates nothing — <see cref="Entries"/>' backing array only grows when the entity
/// count exceeds its capacity.
/// </para>
/// </summary>
public sealed class RenderSnapshot
{
    public readonly struct TransformData
    {
        public readonly Vec2 Position;
        public readonly Vec2 Scale;
        public readonly double Rotation;

        public TransformData(CTransform t)
        {
            Position = t.Position;
            Scale = t.Scale;
            Rotation = t.Rotation;
        }
    }

    public readonly struct AnimationData
    {
        public readonly SKBitmap Texture;
        public readonly SKRect SourceRect;
        public readonly bool ShouldDraw;

        public AnimationData(CAnimation a)
        {
            Texture = a.Texture;
            SourceRect = a.GetSourceRect();
            ShouldDraw = a.ShouldDraw;
        }
    }

    public readonly struct TextData
    {
        public readonly string Text;
        public readonly SKPaint Paint;
        public readonly bool ShouldDraw;

        public TextData(CText t)
        {
            Text = t.Text;
            Paint = t.Paint; // SKPaint is only read during render; safe to share
            ShouldDraw = t.ShouldDraw;
        }
    }

    public readonly struct BoundingBoxData
    {
        public readonly double Width;
        public readonly double Height;

        public BoundingBoxData(CBoundingBox b)
        {
            Width = b.Width;
            Height = b.Height;
        }
    }

    public readonly struct CameraData
    {
        public readonly Vec2 Position;
        public readonly float Zoom;

        public CameraData(CCamera c)
        {
            Position = c.Position;
            Zoom = c.Zoom;
        }
    }

    public readonly struct Entry
    {
        public readonly int EntityId;
        public readonly string Tag;
        public readonly int Layer;
        public readonly TransformData Transform;
        public readonly AnimationData? Animation;
        public readonly TextData? Text;
        public readonly BoundingBoxData? BoundingBox;

        public Entry(int entityId, string tag, int layer, TransformData transform,
            AnimationData? animation, TextData? text, BoundingBoxData? boundingBox)
        {
            EntityId = entityId;
            Tag = tag;
            Layer = layer;
            Transform = transform;
            Animation = animation;
            Text = text;
            BoundingBox = boundingBox;
        }
    }

    private sealed class LayerComparer : IComparer<Entry>
    {
        public static readonly LayerComparer Instance = new();

        public int Compare(Entry x, Entry y)
        {
            int byLayer = x.Layer.CompareTo(y.Layer);
            return byLayer != 0 ? byLayer : x.EntityId.CompareTo(y.EntityId);
        }
    }

    private Entry[] _entries = [];
    private int _count;

    /// How many readers currently hold this buffer. Only ever touched under the engine's
    /// snapshot lock; a buffer with readers is never handed back out to be filled.
    internal int Readers;

    public ReadOnlySpan<Entry> Entries => _entries.AsSpan(0, _count);

    /// <summary>The active camera, or null when the scene has none.</summary>
    public CameraData? ActiveCamera { get; private set; }

    /// <summary>Snapshot returned before the first frame is built. Never pooled or filled.</summary>
    public static readonly RenderSnapshot Empty = new();

    /// Engine thread: clear the buffer and pre-size it for <paramref name="expectedCount"/>
    /// entries, reusing the existing array whenever it is already big enough.
    internal void Reset(int expectedCount)
    {
        if (_entries.Length < expectedCount)
            Array.Resize(ref _entries, Math.Max(expectedCount, _entries.Length * 2));
        else if (_count > 0)
            Array.Clear(_entries, 0, _count);

        _count = 0;
        ActiveCamera = null;
    }

    /// <summary>Engine thread: append one entry, growing the backing array if needed.</summary>
    internal void Add(in Entry entry)
    {
        if (_count == _entries.Length)
            Array.Resize(ref _entries, _entries.Length == 0 ? 16 : _entries.Length * 2);

        _entries[_count++] = entry;
    }

    /// <summary>Engine thread: record the camera to render through.</summary>
    internal void SetCamera(in CameraData camera) => ActiveCamera = camera;

    internal void SortByLayer()
    {
        if (_count > 1)
            Array.Sort(_entries, 0, _count, LayerComparer.Instance);
    }
}
