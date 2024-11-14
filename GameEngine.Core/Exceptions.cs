namespace GameEngine.Core
{
    public class Exceptions
    {
        public class EntityNotFoundException(string message) : Exception(message) { }

        public class ComponentNotFoundException<T> : Exception where T : Component{
            public ComponentNotFoundException(Entity entity) : base(FormatMessage(entity)) { }
            static string FormatMessage(Entity entity) => $"Entity {entity.id}:{entity.Tag} does not have component of type {typeof(T)}.";
        }

        public class FailedToLoadTextureException(string message) : Exception(message) { }
    }
}
