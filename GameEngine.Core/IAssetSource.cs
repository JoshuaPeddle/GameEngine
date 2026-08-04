namespace GameEngine.Core
{
    public interface IAssetSource
    {
        Stream Open(string path);
    }
}
