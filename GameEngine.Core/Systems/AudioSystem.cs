namespace GameEngine.Core.Systems;

public enum SoundType { BGM, SoundEffect }

public sealed class AudioSystem : ISystem, IDisposable
{
    public const int MaximumEffectVoices = 31;
    private const int MaximumPendingSounds = 128;
    private static readonly SoundPlaybackOptions DefaultOptions = new();
    private sealed record Voice(string Sound, int Priority, long Order);
    private sealed record Pending(string Path, int Count, int Priority);
    private readonly IAssetSource assetSource;
    private readonly IAudioBackend backend;
    private readonly bool ownsBackend;
    private readonly Dictionary<string, string> paths = [];
    private readonly Dictionary<string, SoundPlaybackOptions> options = [];
    private readonly Dictionary<string, Pending> pending = [];
    private readonly Dictionary<string, double> lastPlayed = [];
    private readonly Dictionary<int, Voice> voices = [];
    private readonly HashSet<(string, SoundType)> preloaded = [];
    private readonly int maxVoices;
    private bool manifestLoaded;
    private bool disposed;
    private double time;
    private long order;
    private float masterVolume = 1;
    private float musicVolume = 1;
    private float sfxVolume = 1;
    private bool muted;

    public AudioSystem(IAssetSource assetSource) : this(assetSource, AudioBackends.Create(), true) { }

    public AudioSystem(IAssetSource assetSource, IAudioBackend backend, bool ownsBackend = false, int maxVoices = 16)
    {
        ArgumentNullException.ThrowIfNull(assetSource);
        ArgumentNullException.ThrowIfNull(backend);
        if (maxVoices < 1 || maxVoices > MaximumEffectVoices)
            throw new ArgumentOutOfRangeException(nameof(maxVoices));
        this.assetSource = assetSource;
        this.backend = backend;
        this.ownsBackend = ownsBackend;
        this.maxVoices = maxVoices;
    }

    public string BackendName => backend.Name;
    public bool IsAvailable => !disposed && backend.IsAvailable;
    public string? UnavailableReason => disposed ? "The audio system has been disposed." : backend.UnavailableReason;
    public static AudioSystem Create(IAssetSource assetSource) => new(assetSource);

    public float MasterVolume { get => masterVolume; set { masterVolume = ValidateVolume(value); ApplyVolumes(); } }
    public float MusicVolume { get => musicVolume; set { musicVolume = ValidateVolume(value); ApplyVolumes(); } }
    public float SfxVolume { get => sfxVolume; set { sfxVolume = ValidateVolume(value); ApplyVolumes(); } }
    public bool Muted { get => muted; set { muted = value; ApplyVolumes(); } }

    public void ConfigureSound(string assetName, SoundPlaybackOptions playbackOptions)
    {
        ArgumentNullException.ThrowIfNull(playbackOptions);
        options[assetName] = playbackOptions;
        foreach (var (id, _) in voices.Where(pair => pair.Value.Sound == assetName)
            .OrderByDescending(pair => pair.Value.Priority).ThenByDescending(pair => pair.Value.Order)
            .Skip(playbackOptions.MaxConcurrentVoices).ToArray())
        {
            backend.StopVoice(id);
            voices.Remove(id);
        }
    }

    public void Preload(string assetName, SoundType soundType = SoundType.SoundEffect)
    {
        if (IsAvailable && TryResolvePath(assetName, out var path) && preloaded.Add((assetName, soundType)))
            backend.Preload(assetName, path, soundType);
    }

    public void Play(string assetName, SoundType soundType, int? priority = null)
    {
        if (!IsAvailable || !TryResolvePath(assetName, out var path))
            return;
        if (soundType == SoundType.BGM)
        {
            backend.TryPlay(0, assetName, path, soundType, Volume(soundType), priority ?? 0);
            return;
        }
        var settings = Settings(assetName);
        int requestedPriority = priority ?? settings.Priority;
        if (pending.TryGetValue(assetName, out var existing))
            pending[assetName] = existing with
            {
                Count = Math.Min(existing.Count + 1, settings.MaxEventsPerFrame),
                Priority = Math.Max(existing.Priority, requestedPriority)
            };
        else if (pending.Count < MaximumPendingSounds)
            pending.Add(assetName, new Pending(path, 1, requestedPriority));
        else
        {
            var lowest = pending.MinBy(pair => pair.Value.Priority);
            if (requestedPriority > lowest.Value.Priority)
            {
                pending.Remove(lowest.Key);
                pending.Add(assetName, new Pending(path, 1, requestedPriority));
            }
        }
    }

    public void Update(EntityManager entityManager, double deltaSeconds)
    {
        if (!double.IsFinite(deltaSeconds) || deltaSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
        if (disposed)
            return;
        time += deltaSeconds;
        foreach (int id in voices.Keys.ToArray())
            if (!backend.IsPlaying(id))
                voices.Remove(id);

        foreach (var (sound, request) in pending.OrderByDescending(pair => pair.Value.Priority))
        {
            var settings = Settings(sound);
            if (lastPlayed.TryGetValue(sound, out double last) && time - last < settings.MinimumRetriggerInterval)
                continue;
            bool played = false;
            for (int i = 0; i < Math.Min(request.Count, settings.MaxEventsPerFrame); i++)
            {
                int id = FindVoice(sound, settings.MaxConcurrentVoices, request.Priority);
                if (id < 0)
                    break;
                if (backend.TryPlay(id, sound, request.Path, SoundType.SoundEffect, Volume(SoundType.SoundEffect), request.Priority))
                {
                    voices[id] = new Voice(sound, request.Priority, order++);
                    played = true;
                }
            }
            if (played)
                lastPlayed[sound] = time;
        }
        pending.Clear();
    }

    private int FindVoice(string sound, int limit, int priority)
    {
        var same = voices.Where(pair => pair.Value.Sound == sound).ToArray();
        if (same.Length < limit)
            for (int id = 1; id <= maxVoices; id++)
                if (!voices.ContainsKey(id))
                    return id;
        var candidates = same.Length >= limit ? same : voices.ToArray();
        var victim = candidates.OrderBy(pair => pair.Value.Priority).ThenBy(pair => pair.Value.Order).FirstOrDefault();
        if (victim.Value == null || priority <= victim.Value.Priority)
            return -1;
        backend.StopVoice(victim.Key);
        voices.Remove(victim.Key);
        return victim.Key;
    }

    public void StopAll()
    {
        pending.Clear();
        voices.Clear();
        lastPlayed.Clear();
        if (!disposed)
            backend.StopAll();
    }

    private SoundPlaybackOptions Settings(string sound) => options.GetValueOrDefault(sound, DefaultOptions);
    private float Volume(SoundType type) => muted ? 0 : masterVolume * (type == SoundType.BGM ? musicVolume : sfxVolume);
    private static float ValidateVolume(float value) => float.IsFinite(value) && value >= 0 && value <= 1
        ? value : throw new ArgumentOutOfRangeException(nameof(value), "Volume must be finite and between zero and one.");

    private void ApplyVolumes()
    {
        if (disposed)
            return;
        backend.SetVolume(0, Volume(SoundType.BGM));
        foreach (int id in voices.Keys)
            backend.SetVolume(id, Volume(SoundType.SoundEffect));
    }

    private bool TryResolvePath(string assetName, out string path)
    {
        if (!manifestLoaded)
        {
            try
            {
                foreach (var sound in AssetManifest.Load(AssetManifest.DefaultFileName, assetSource).Sounds)
                    paths.Add(sound.Name, sound.Path);
                manifestLoaded = true;
            }
            catch (Exception)
            {
                paths.Clear();
            }
        }
        return paths.TryGetValue(assetName, out path!);
    }

    public void Dispose()
    {
        if (disposed)
            return;
        StopAll();
        disposed = true;
        if (ownsBackend)
            backend.Dispose();
        paths.Clear();
        GC.SuppressFinalize(this);
    }
}
