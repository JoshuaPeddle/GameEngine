using SkiaSharp;

namespace GameEngine.Core;

public sealed partial class RenderSnapshot
{
    public sealed class AtlasBatch
    {
        internal SKRect[] Sources = [];
        internal SKRotationScaleMatrix[] Matrices = [];
        internal SKColor[] Tints = [];
        internal int Order;

        public SKImage Texture { get; internal set; } = null!;
        public SpriteBlend Blend { get; internal set; }
        public int Layer { get; internal set; }
        public int Count { get; private set; }
        public int SubmittedCount => Sources.Length;
        public ReadOnlySpan<SKRect> Regions => Sources.AsSpan(0, Count);
        public ReadOnlySpan<SKRotationScaleMatrix> Transforms => Matrices.AsSpan(0, Count);
        public ReadOnlySpan<SKColor> Colors => Tints.AsSpan(0, Count);

        internal void Reset()
        {
            Array.Clear(Sources, 0, Count);
            Array.Clear(Matrices, 0, Count);
            Array.Clear(Tints, 0, Count);
            Texture = null!;
            Count = 0;
        }

        internal void Add(in SpriteInstance instance)
        {
            if (Math.Abs(instance.Scale) < 0.0001f || instance.Color is { Alpha: 0 }) return;
            if (Count == Sources.Length)
            {
                int capacity = Math.Max(16, Count * 2);
                Array.Resize(ref Sources, capacity);
                Array.Resize(ref Matrices, capacity);
                Array.Resize(ref Tints, capacity);
            }
            Sources[Count] = instance.Region;
            Matrices[Count] = instance.Matrix;
            Tints[Count] = instance.Color ?? SKColors.White;
            Count++;
        }
    }

    private sealed class AtlasLayerComparer : IComparer<AtlasBatch>
    {
        public static readonly AtlasLayerComparer Instance = new();
        public int Compare(AtlasBatch? x, AtlasBatch? y)
        {
            int layer = x!.Layer.CompareTo(y!.Layer);
            return layer != 0 ? layer : x.Order.CompareTo(y.Order);
        }
    }

    private static readonly Comparison<AtlasBatch> CompareAtlasLayers = AtlasLayerComparer.Instance.Compare;

    private AtlasBatch[] _atlasBatches = [];
    private int _atlasCount;
    public ReadOnlySpan<AtlasBatch> AtlasBatches => _atlasBatches.AsSpan(0, _atlasCount);

    private void ResetAtlasBatches()
    {
        for (int i = 0; i < _atlasCount; i++) _atlasBatches[i].Reset();
        _atlasCount = 0;
    }

    internal AtlasBatch BeginAtlasBatch(SKImage texture, SpriteBlend blend, int layer)
    {
        if (_atlasCount > 0)
        {
            var last = _atlasBatches[_atlasCount - 1];
            if (ReferenceEquals(last.Texture, texture) && last.Blend == blend && last.Layer == layer)
                return last;
        }
        if (_atlasCount == _atlasBatches.Length)
        {
            int before = _atlasBatches.Length;
            Array.Resize(ref _atlasBatches, Math.Max(4, before * 2));
            for (int i = before; i < _atlasBatches.Length; i++) _atlasBatches[i] = new AtlasBatch();
        }
        var batch = _atlasBatches[_atlasCount];
        batch.Texture = texture;
        batch.Blend = blend;
        batch.Layer = layer;
        batch.Order = _atlasCount++;
        return batch;
    }

    internal void SortAtlasBatches() => _atlasBatches.AsSpan(0, _atlasCount).Sort(CompareAtlasLayers);
}
