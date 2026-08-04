namespace GameEngine.Core
{
    public sealed class DelegateAssetSource : IAssetSource
    {
        private readonly Func<string, Stream> open;

        public DelegateAssetSource(Func<string, Stream> open)
        {
            ArgumentNullException.ThrowIfNull(open);
            this.open = open;
        }

        public Stream Open(string path)
        {
            ArgumentException.ThrowIfNullOrEmpty(path);
            return open(path);
        }
    }
}
