namespace GameEngine.Core
{
    public sealed record EntitySnapshot(
        int Id,
        string Tag,
        bool Active,
        IReadOnlyList<string> ComponentTypes);
}
