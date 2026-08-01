namespace GameEngine.Core
{
    public class EntityNotFoundException(string message) : Exception(message) { }

    public class ComponentNotFoundException<T> : Exception where T : Component
    {
        public ComponentNotFoundException(Entity entity) : base(FormatMessage(entity)) { }

        private static string FormatMessage(Entity entity) =>
            $"Entity {entity.Id}:{entity.Tag} does not have component of type {typeof(T)}.";
    }

    public class FailedToLoadTextureException(string message) : Exception(message) { }

    public class FailedToLoadSoundException(string message) : Exception(message) { }
}
