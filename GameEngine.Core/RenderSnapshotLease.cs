namespace GameEngine.Core;

public sealed class RenderSnapshotLease : IDisposable
{
    private Engine? owner;
    private readonly RenderSnapshot snapshot;

    internal RenderSnapshotLease(Engine owner, RenderSnapshot snapshot)
    {
        this.owner = owner;
        this.snapshot = snapshot;
    }

    public RenderSnapshot Snapshot
    {
        get
        {
            ObjectDisposedException.ThrowIf(owner == null, this);
            return snapshot;
        }
    }

    public void Dispose() => Interlocked.Exchange(ref owner, null)?.ReleaseSnapshot(snapshot);
}
