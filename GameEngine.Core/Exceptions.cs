namespace GameEngine.Core
{
    public class Exceptions
    {
        public class EntityNotFoundException(string message) : Exception(message) { }

        public class ComponentNotFoundException(string message) : Exception(message) { }

        public class FailedToLoadTextureException(string message) : Exception(message) { }
    }
}
