namespace GameEngine.Core
{
    public class Exceptions
    {
        public class EntityNotFoundException : Exception
        {
            public EntityNotFoundException(string message) : base(message) { }
        }

        public class ComponentNotFoundException : Exception
        {
            public ComponentNotFoundException(string message) : base(message) { }
        }
    }
}
