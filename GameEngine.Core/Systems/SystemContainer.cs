namespace GameEngine.Core.Systems
{
    public class SystemContainer
    {
        public List<ISystem> Systems { get; } = new List<ISystem>();

        public SystemContainer() { }

        public void Add(ISystem system)
        {
            if (Systems.Any(s => s.GetType() == system.GetType()))
                throw new DuplicateSystemException();
            Systems.Add(system);
        }

        public T Get<T>() where T : ISystem
        {
            var system = Systems.FirstOrDefault(s => s.GetType() == typeof(T));
            return system != null ? (T)system : throw new MissingSystemException();
        }

        public bool Contains<T>() where T : ISystem
        {
            return Systems.Any(s => s.GetType() == typeof(T));
        }
    }
}
