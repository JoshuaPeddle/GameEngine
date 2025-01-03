namespace GameEngine.Core.Systems
{
    [Serializable]
    internal class MissingSystemException : Exception
    {
        public MissingSystemException(){}

        public MissingSystemException(string? message) : base(message) { }

        public MissingSystemException(string? message, Exception? innerException) : base(message, innerException) { }
    }

    [Serializable]
    internal class DuplicateSystemException : Exception
    {
        public DuplicateSystemException() { }

        public DuplicateSystemException(string? message) : base(message) { }

        public DuplicateSystemException(string? message, Exception? innerException) : base(message, innerException) { }
    }
}
