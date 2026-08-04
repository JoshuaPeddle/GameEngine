namespace GameEngine.Core
{
    public sealed class FileAssetSource : IAssetSource
    {
        public const string DefaultContentRoot = "assets";

        private readonly string contentRoot;

        public FileAssetSource(string contentRoot = DefaultContentRoot)
        {
            this.contentRoot = contentRoot;
        }

        public Stream Open(string path)
        {
            ArgumentException.ThrowIfNullOrEmpty(path);

            if (Path.IsPathRooted(path) || File.Exists(path))
                return File.OpenRead(path);

            return File.OpenRead(Path.Combine(contentRoot, path));
        }
    }
}
