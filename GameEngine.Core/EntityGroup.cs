using System.Collections.ObjectModel;

namespace GameEngine.Core;

public sealed class EntityGroup : IDisposable
{
    private readonly EntityManager manager;
    private readonly List<Entity> members = [];
    private bool disposed;
    public ReadOnlyCollection<Entity> Entities { get; }

    internal EntityGroup(EntityManager manager)
    {
        this.manager = manager;
        Entities = members.AsReadOnly();
    }

    public Entity CreateEntity(string tag)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var entity = manager.CreateEntity(tag);
        members.Add(entity);
        return entity;
    }

    public void Clear()
    {
        foreach (var entity in members) entity.Active = false;
        members.Clear();
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        Clear();
    }
}
