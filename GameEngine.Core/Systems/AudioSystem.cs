namespace GameEngine.Core.Systems
{
    public enum SoundType { BGM, SoundEffect }

    /// <summary>
    /// The engine's audio service. It resolves a sound name to the manifest entry that
    /// declares it and hands the result to whatever backend the host registered. The service
    /// always exists: on a platform with no backend it reports itself unavailable and plays
    /// nothing, so a game runs and the reason is diagnosable rather than the system simply
    /// being absent.
    /// </summary>
    public class AudioSystem : ISystem, IDisposable
    {
        private readonly IAssetSource _assetSource;
        private readonly IAudioBackend _backend;
        private readonly bool _ownsBackend;
        private readonly Dictionary<string, string> _resolvedPaths = [];

        private Assets? _assets;
        private bool _disposed;

        public AudioSystem(IAssetSource assetSource)
            : this(assetSource, AudioBackends.Create(), ownsBackend: true)
        {
        }

        public AudioSystem(IAssetSource assetSource, IAudioBackend backend, bool ownsBackend = false)
        {
            ArgumentNullException.ThrowIfNull(assetSource);
            ArgumentNullException.ThrowIfNull(backend);

            _assetSource = assetSource;
            _backend = backend;
            _ownsBackend = ownsBackend;
        }

        public string BackendName => _backend.Name;

        public bool IsAvailable => !_disposed && _backend.IsAvailable;

        /// <summary>Why nothing will be heard, or null when audio is working.</summary>
        public string? UnavailableReason => _disposed
            ? "The audio system has been disposed."
            : _backend.UnavailableReason;

        /// <summary>
        /// Never returns null and never throws: an engine always has an audio service, and a
        /// platform without a backend gets one that reports why it is silent.
        /// </summary>
        public static AudioSystem Create(IAssetSource assetSource) => new(assetSource);

        public void Play(string assetName, SoundType soundType)
        {
            if (_disposed || !_backend.IsAvailable)
                return;

            if (!TryResolvePath(assetName, out var path))
                return;

            _backend.Play(assetName, path, soundType);
        }

        public void StopAll()
        {
            if (!_disposed)
                _backend.StopAll();
        }

        // The manifest lookup is cached rather than the decoded sound: reusing the decoded copy
        // is the backend's job, because only it knows what decoded means on this platform.
        private bool TryResolvePath(string assetName, out string path)
        {
            if (_resolvedPaths.TryGetValue(assetName, out path!))
                return true;

            try
            {
                _assets ??= new Assets("assets.json", _assetSource);
                path = _assets.GetSound(assetName).Path;
            }
            catch (Exception)
            {
                path = string.Empty;
                return false;
            }

            _resolvedPaths[assetName] = path;
            return true;
        }

        public void Update(EntityManager entityManager, double deltaSeconds) { }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;

            _backend.StopAll();
            if (_ownsBackend)
                _backend.Dispose();

            _resolvedPaths.Clear();
            GC.SuppressFinalize(this);
        }
    }
}
