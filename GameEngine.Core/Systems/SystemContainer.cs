namespace GameEngine.Core.Systems
{
    public class SystemContainer : IDisposable
    {
        private readonly List<ISystem> _systems = [];
        private readonly Dictionary<Type, ISystem> _byType = [];

        /// <summary>
        /// Systems in insertion (execution) order.
        /// </summary>
        public IReadOnlyList<ISystem> Systems => _systems;

        public SystemContainer() { }

        public void Add(ISystem system)
        {
            if (!_byType.TryAdd(system.GetType(), system))
                throw new DuplicateSystemException();
            _systems.Add(system);
        }

        public T Get<T>() where T : ISystem
        {
            return TryFind<T>(out var system) ? system : throw new MissingSystemException();
        }

        public T? TryGet<T>() where T : class, ISystem
        {
            return TryFind<T>(out var system) ? system : null;
        }

        public bool Contains<T>() where T : ISystem
        {
            return TryFind<T>(out _);
        }

        private bool TryFind<T>(out T found) where T : ISystem
        {
            if (_byType.TryGetValue(typeof(T), out var exact))
            {
                found = (T)exact;
                return true;
            }

            foreach (var system in _systems)
            {
                if (system is T assignable)
                {
                    found = assignable;
                    return true;
                }
            }

            found = default!;
            return false;
        }

        public void Dispose()
        {
            foreach (var system in _systems)
            {
                if (system is IDisposable disposable)
                    disposable.Dispose();
            }
            _systems.Clear();
            _byType.Clear();
            GC.SuppressFinalize(this);
        }
    }
}
