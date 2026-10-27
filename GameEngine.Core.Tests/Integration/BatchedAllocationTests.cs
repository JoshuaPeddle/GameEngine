namespace GameEngine.Core.Tests.Integration;

public class BatchedAllocationTests
{
    [TestCase(750)]
    [TestCase(3000)]
    [TestCase(8000)]
    public void FullEngineFramesWithSparkRespawnsAllocateNothing(int count)
    {
        using var workload = new StressFrameWorkload(count, transientEmitters: false);
        for (int i = 0; i < 1000; i++)
        {
            workload.Update();
            workload.Frame();
        }
        long births = workload.Spawned;
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 120; i++)
        {
            workload.Update();
            workload.Frame();
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.That(workload.Fault, Is.Null);
        Assert.That(workload.Count, Is.EqualTo(count));
        Assert.That(workload.Spawned, Is.GreaterThan(births + count));
        Assert.That(allocated, Is.Zero);
    }

    [Test]
    public void AllThreeSnapshotBuffersRetainTheirAtlasGroupsAndCapacity()
    {
        using var workload = new StressFrameWorkload(750, transientEmitters: false);
        var buffers = new Dictionary<RenderSnapshot, (RenderSnapshot.AtlasBatch Group, int Capacity)>();
        for (int i = 0; i < 12; i++)
        {
            workload.Update();
            workload.Update();
            var snapshot = workload.AcquireSnapshot();
            var group = snapshot.AtlasBatches[1];
            if (buffers.TryGetValue(snapshot, out var previous))
            {
                Assert.That(group, Is.SameAs(previous.Group));
                Assert.That(group.SubmittedCount, Is.EqualTo(previous.Capacity));
            }
            else
            {
                buffers.Add(snapshot, (group, group.SubmittedCount));
            }
        }
        Assert.That(buffers, Has.Count.EqualTo(3));
        Assert.That(workload.Fault, Is.Null);
    }
}
