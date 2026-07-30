namespace GameEngine.Core.Systems
{
    public class SystemContainer : IDisposable
    {
        private readonly List<ISystem> _systems = [];

        /// <summary>
        /// Systems in insertion (execution) order.
        /// </summary>
        public IReadOnlyList<ISystem> Systems => _systems;

        public SystemContainer() { }

        public void Add(ISystem system)
        {
            if (_systems.Any(s => s.GetType() == system.GetType()))
                throw new DuplicateSystemException();
            _systems.Add(system);
        }

        public T Get<T>() where T : ISystem
        {
            var system = _systems.FirstOrDefault(s => s.GetType() == typeof(T));
            return system != null ? (T)system : throw new MissingSystemException();
        }

        public T? TryGet<T>() where T : class, ISystem
        {
            return _systems.FirstOrDefault(s => s.GetType() == typeof(T)) as T;
        }

        public bool Contains<T>() where T : ISystem
        {
            return _systems.Any(s => s.GetType() == typeof(T));
        }

        public void Dispose()
        {
            foreach (var system in _systems)
            {
                if (system is IDisposable disposable)
                    disposable.Dispose();
            }
            _systems.Clear();
            GC.SuppressFinalize(this);
        }
    }
}
