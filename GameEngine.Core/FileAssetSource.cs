namespace GameEngine.Core
{
    public sealed class FileAssetSource : IAssetSource
    {
        public const string DefaultContentRoot = "assets";

        private readonly string baseDirectory;
        private readonly string contentRoot;

        public FileAssetSource(string? baseDirectory = null, string contentRoot = DefaultContentRoot)
        {
            this.baseDirectory = baseDirectory ?? AppContext.BaseDirectory;
            this.contentRoot = contentRoot;
        }

        public string BaseDirectory => baseDirectory;

        public Stream Open(string path)
        {
            ArgumentException.ThrowIfNullOrEmpty(path);

            if (Path.IsPathRooted(path))
                return File.OpenRead(path);

            var beside = Path.Combine(baseDirectory, path);
            if (File.Exists(beside))
                return File.OpenRead(beside);

            return File.OpenRead(Path.Combine(baseDirectory, contentRoot, path));
        }
    }
}
