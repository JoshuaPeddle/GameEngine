using GameEngine.Core.Systems;

namespace GameEngine.Core.Tests.Unit.Systems;

public class SystemContainerTests
{
    private class RecordingSystem : ISystem
    {
        public int Updates;
        public void Update(EntityManager entityManager, double deltaSeconds) => Updates++;
    }

    private sealed class DerivedRecordingSystem : RecordingSystem { }

    [Test]
    public void Get_FindsASystemByItsExactType()
    {
        var container = new SystemContainer();
        var system = new RecordingSystem();
        container.Add(system);

        Assert.That(container.Get<RecordingSystem>(), Is.SameAs(system));
    }

    [Test]
    public void Get_FindsASubclassThroughItsBaseType()
    {
        var container = new SystemContainer();
        var system = new DerivedRecordingSystem();
        container.Add(system);

        Assert.Multiple(() =>
        {
            Assert.That(container.Get<RecordingSystem>(), Is.SameAs(system));
            Assert.That(container.TryGet<RecordingSystem>(), Is.SameAs(system));
            Assert.That(container.Contains<RecordingSystem>(), Is.True);
        });
    }

    [Test]
    public void Get_ThrowsWhenAbsent()
    {
        var container = new SystemContainer();

        Assert.Throws<MissingSystemException>(() => container.Get<RecordingSystem>());
        Assert.That(container.TryGet<RecordingSystem>(), Is.Null);
        Assert.That(container.Contains<RecordingSystem>(), Is.False);
    }

    [Test]
    public void Add_RejectsADuplicateSystemType()
    {
        var container = new SystemContainer();
        container.Add(new RecordingSystem());

        Assert.Throws<DuplicateSystemException>(() => container.Add(new RecordingSystem()));
    }

    [Test]
    public void Systems_KeepInsertionOrder()
    {
        var container = new SystemContainer();
        var first = new RecordingSystem();
        var second = new DerivedRecordingSystem();
        container.Add(first);
        container.Add(second);

        Assert.That(container.Systems, Is.EqualTo(new ISystem[] { first, second }).AsCollection);
    }

    [Test]
    public void Dispose_ClearsTheContainer()
    {
        var container = new SystemContainer();
        container.Add(new RecordingSystem());

        container.Dispose();

        Assert.That(container.Systems, Is.Empty);
        Assert.That(container.TryGet<RecordingSystem>(), Is.Null);
    }
}
