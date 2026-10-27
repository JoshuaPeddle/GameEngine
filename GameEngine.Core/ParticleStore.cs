using System.Buffers;
using SkiaSharp;

namespace GameEngine.Core;

public readonly record struct ParticleState(Vec2 Position, Vec2 Velocity, float Age, float Life,
    float Rotation, float Scale, SKColor Color, SKRect Region);

public sealed class ParticleStore : IDisposable
{
    private readonly Vec2[] _position, _velocity, _gravity;
    private readonly float[] _age, _life, _rotation, _angularVelocity, _drag, _startScale, _endScale;
    private readonly SKColor[] _startColor, _endColor;
    private readonly SKRect[] _region;
    private readonly ParticleOptions?[] _options;
    private readonly int[] _next, _previous;
    private int _first = -1, _last = -1, _free;
    private bool _disposed;

    public ParticleStore(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        Capacity = capacity;
        _position = ArrayPool<Vec2>.Shared.Rent(capacity);
        _velocity = ArrayPool<Vec2>.Shared.Rent(capacity);
        _gravity = ArrayPool<Vec2>.Shared.Rent(capacity);
        _life = ArrayPool<float>.Shared.Rent(capacity);
        _angularVelocity = ArrayPool<float>.Shared.Rent(capacity);
        _drag = ArrayPool<float>.Shared.Rent(capacity);
        _startScale = ArrayPool<float>.Shared.Rent(capacity);
        _endScale = ArrayPool<float>.Shared.Rent(capacity);
        _startColor = ArrayPool<SKColor>.Shared.Rent(capacity);
        _endColor = ArrayPool<SKColor>.Shared.Rent(capacity);
        _region = ArrayPool<SKRect>.Shared.Rent(capacity);
        _age = ArrayPool<float>.Shared.Rent(capacity);
        _rotation = ArrayPool<float>.Shared.Rent(capacity);
        _options = ArrayPool<ParticleOptions?>.Shared.Rent(capacity);
        _next = ArrayPool<int>.Shared.Rent(capacity);
        _previous = ArrayPool<int>.Shared.Rent(capacity);
        for (int i = 0; i < capacity; i++) _next[i] = i + 1 < capacity ? i + 1 : -1;
    }

    public int Capacity { get; }
    public int Count { get; private set; }
    public long Spawned { get; private set; }
    public long Dropped { get; private set; }
    public long Expired { get; private set; }

    internal void Spawn(ParticleOptions options, Vec2 position, Vec2 velocity)
        => Spawn(options, new ParticleSpawn(position, velocity, options.Life,
            options.StartScale, options.StartColor, options.Rotation));

    internal void Spawn(ParticleOptions options, in ParticleSpawn particle)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (Count == Capacity)
        {
            Remove(_first);
            Dropped++;
        }
        int slot = _free;
        _free = _next[slot];
        _position[slot] = particle.Position;
        _velocity[slot] = particle.Velocity;
        _age[slot] = 0;
        _rotation[slot] = particle.Rotation;
        _life[slot] = particle.Life;
        _gravity[slot] = options.Gravity;
        _angularVelocity[slot] = options.AngularVelocity;
        _drag[slot] = options.Drag;
        _startScale[slot] = particle.Scale;
        _endScale[slot] = options.EndScale;
        _startColor[slot] = particle.Color ?? options.StartColor;
        _endColor[slot] = options.EndColor;
        _region[slot] = options.Region;
        _options[slot] = options;
        _previous[slot] = _last;
        _next[slot] = -1;
        if (_last >= 0) _next[_last] = slot;
        else _first = slot;
        _last = slot;
        Count++;
        Spawned++;
    }

    private void Remove(int slot)
    {
        int before = _previous[slot], after = _next[slot];
        if (before >= 0) _next[before] = after;
        else _first = after;
        if (after >= 0) _previous[after] = before;
        else _last = before;
        _options[slot] = null;
        _next[slot] = _free;
        _free = slot;
        Count--;
    }

    public void Update(double deltaSeconds)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!double.IsFinite(deltaSeconds) || deltaSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
        for (int slot = _first; slot >= 0;)
        {
            int next = _next[slot];
            _age[slot] += (float)deltaSeconds;
            if (_age[slot] >= _life[slot])
            {
                Remove(slot);
                Expired++;
            }
            else
            {
                _velocity[slot] = (_velocity[slot] + _gravity[slot] * deltaSeconds)
                    * Math.Exp(-_drag[slot] * deltaSeconds);
                _position[slot] += _velocity[slot] * deltaSeconds;
                _rotation[slot] += _angularVelocity[slot] * (float)deltaSeconds;
            }
            slot = next;
        }
    }

    public int CopyTo(Span<ParticleState> destination)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (destination.Length < Count) throw new ArgumentException("Destination is too small.", nameof(destination));
        int index = 0;
        for (int slot = _first; slot >= 0; slot = _next[slot]) destination[index++] = StateAt(slot);
        return index;
    }

    private ParticleState StateAt(int slot)
    {
        float progress = _age[slot] / _life[slot];
        var start = _startColor[slot];
        var end = _endColor[slot];
        return new ParticleState(_position[slot], _velocity[slot], _age[slot], _life[slot],
            _rotation[slot], _startScale[slot] + (_endScale[slot] - _startScale[slot]) * progress,
            new SKColor(Lerp(start.Red, end.Red, progress), Lerp(start.Green, end.Green, progress),
                Lerp(start.Blue, end.Blue, progress), Lerp(start.Alpha, end.Alpha, progress)), _region[slot]);
    }

    private static byte Lerp(byte start, byte end, float progress) => (byte)(start + (end - start) * progress);

    internal void CopyTo(RenderSnapshot snapshot)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        for (int slot = _first; slot >= 0; slot = _next[slot])
        {
            var options = _options[slot]!;
            var state = StateAt(slot);
            snapshot.BeginAtlasBatch(options.Texture, options.Blend, options.Layer).Add(
                new SpriteInstance(state.Position, state.Region, state.Scale, state.Rotation, state.Color));
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        ArrayPool<Vec2>.Shared.Return(_position);
        ArrayPool<Vec2>.Shared.Return(_velocity);
        ArrayPool<Vec2>.Shared.Return(_gravity);
        ArrayPool<float>.Shared.Return(_life);
        ArrayPool<float>.Shared.Return(_angularVelocity);
        ArrayPool<float>.Shared.Return(_drag);
        ArrayPool<float>.Shared.Return(_startScale);
        ArrayPool<float>.Shared.Return(_endScale);
        ArrayPool<SKColor>.Shared.Return(_startColor);
        ArrayPool<SKColor>.Shared.Return(_endColor);
        ArrayPool<SKRect>.Shared.Return(_region);
        ArrayPool<float>.Shared.Return(_age);
        ArrayPool<float>.Shared.Return(_rotation);
        ArrayPool<ParticleOptions?>.Shared.Return(_options, clearArray: true);
        ArrayPool<int>.Shared.Return(_next);
        ArrayPool<int>.Shared.Return(_previous);
        Count = 0;
    }
}
