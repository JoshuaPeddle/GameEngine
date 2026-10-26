namespace GameEngine.Core.Systems;

public sealed class ParticleSystem : ISystem, IDisposable
{
    private readonly List<ParticleEmitter> _emitters = [];
    private ParticleStore? _store;
    private bool _disposed;

    public ParticleSystem(int capacity = 10000)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        Capacity = capacity;
    }

    public int Capacity { get; }
    public ParticleStore Store => _store ??= CreateStore();

    private ParticleStore CreateStore()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return new ParticleStore(Capacity);
    }

    public ParticleEmitter CreateEmitter(ParticleOptions options, int seed = 0)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(options);
        var emitter = new ParticleEmitter(Store, options, seed);
        _emitters.Add(emitter);
        return emitter;
    }

    public void RemoveEmitter(ParticleEmitter emitter) => _emitters.Remove(emitter);

    public void Update(EntityManager entityManager, double deltaSeconds)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!double.IsFinite(deltaSeconds) || deltaSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
        _store?.Update(deltaSeconds);
        foreach (var emitter in _emitters) emitter.Update(deltaSeconds);
    }

    internal void CopyTo(RenderSnapshot snapshot) => _store?.CopyTo(snapshot);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _emitters.Clear();
        _store?.Dispose();
    }
}
