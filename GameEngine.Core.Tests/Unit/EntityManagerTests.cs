using GameEngine.Core.Components;

namespace GameEngine.Core.Tests.Unit;

public class EntityManagerTests
{
    private static Entity AddTransform(EntityManager manager, string tag)
    {
        var entity = manager.CreateEntity(tag);
        entity.AddComponent(new CTransform(Vec2.Zero));
        return entity;
    }

    [Test]
    public void CreateEntity_AfterRemoval_DoesNotReuseIds()
    {
        var manager = new EntityManager();
        var a = manager.CreateEntity("a");
        var b = manager.CreateEntity("b");
        var c = manager.CreateEntity("c");
        manager.Update();

        b.Active = false;
        manager.Update();
        var d = manager.CreateEntity("d");
        manager.Update();

        Assert.That(d.Id, Is.Not.EqualTo(a.Id));
        Assert.That(d.Id, Is.Not.EqualTo(c.Id), "a new entity must not inherit a live entity's id");
        Assert.That(d.Id, Is.Not.EqualTo(b.Id), "ids of removed entities must not be recycled");
    }

    [Test]
    public void CreateEntity_AssignsUniqueIds_AcrossRepeatedChurn()
    {
        var manager = new EntityManager();
        var seen = new HashSet<int>();

        for (int cycle = 0; cycle < 25; cycle++)
        {
            var batch = new List<Entity>();
            for (int i = 0; i < 4; i++)
                batch.Add(manager.CreateEntity($"e{cycle}_{i}"));
            manager.Update();

            foreach (var entity in batch)
                Assert.That(seen.Add(entity.Id), Is.True, $"id {entity.Id} was issued twice");

            foreach (var entity in batch)
                entity.Active = false;
            manager.Update();
        }

        Assert.That(seen, Has.Count.EqualTo(100));
    }

    [Test]
    public void GetEntity_AfterRemoval_ReturnsTheRequestedEntity()
    {
        var manager = new EntityManager();
        var a = manager.CreateEntity("a");
        var b = manager.CreateEntity("b");
        var c = manager.CreateEntity("c");
        manager.Update();

        b.Active = false;
        manager.Update();

        Assert.That(manager.GetEntity(a.Id), Is.SameAs(a));
        Assert.That(manager.GetEntity(c.Id), Is.SameAs(c), "lookup must be by id, not list position");
    }

    [Test]
    public void GetEntity_UnknownId_ThrowsEntityNotFound()
    {
        var manager = new EntityManager();
        manager.Update();

        Assert.Throws<EntityNotFoundException>(() => manager.GetEntity(4242));
    }

    [Test]
    public void GetEntity_RemovedEntity_ThrowsEntityNotFound()
    {
        var manager = new EntityManager();
        var a = manager.CreateEntity("a");
        manager.Update();
        a.Active = false;
        manager.Update();

        Assert.Throws<EntityNotFoundException>(() => manager.GetEntity(a.Id));
    }

    [Test]
    public void TryGetEntity_ReportsPresenceWithoutThrowing()
    {
        var manager = new EntityManager();
        var a = manager.CreateEntity("a");
        manager.Update();

        Assert.Multiple(() =>
        {
            Assert.That(manager.TryGetEntity(a.Id, out var found), Is.True);
            Assert.That(found, Is.SameAs(a));
            Assert.That(manager.TryGetEntity(9999, out var missing), Is.False);
            Assert.That(missing, Is.Null);
        });
    }

    [Test]
    public void GetEntitiesWith_NestedQueryOfSameType_DoesNotInvalidateOuterIteration()
    {
        var manager = new EntityManager();
        for (int i = 0; i < 3; i++) AddTransform(manager, $"e{i}");
        manager.Update();

        Assert.DoesNotThrow(() =>
        {
            foreach (var outer in manager.GetEntitiesWith<CTransform>())
            {
                Assert.That(outer, Is.Not.Null);
                _ = manager.GetEntitiesWith<CTransform>();
            }
        });
    }

    [Test]
    public void GetEntitiesWith_EarlierResult_IsNotMutatedByLaterStructuralChange()
    {
        var manager = new EntityManager();
        AddTransform(manager, "a");
        manager.Update();
        var firstResult = manager.GetEntitiesWith<CTransform>();
        Assert.That(firstResult, Has.Count.EqualTo(1));

        AddTransform(manager, "b");
        manager.Update();
        var secondResult = manager.GetEntitiesWith<CTransform>();

        Assert.Multiple(() =>
        {
            Assert.That(secondResult, Has.Count.EqualTo(2), "a fresh query must see the new entity");
            Assert.That(firstResult, Has.Count.EqualTo(1),
                "a result already handed to a caller must not change underneath them");
        });
    }

    [Test]
    public void GetEntitiesWith_ReflectsAddsAndRemoves()
    {
        var manager = new EntityManager();
        var a = AddTransform(manager, "a");
        var b = AddTransform(manager, "b");
        manager.Update();

        Assert.That(manager.GetEntitiesWith<CTransform>(), Has.Count.EqualTo(2));

        a.Active = false;
        manager.Update();

        var remaining = manager.GetEntitiesWith<CTransform>();
        Assert.Multiple(() =>
        {
            Assert.That(remaining, Has.Count.EqualTo(1));
            Assert.That(remaining, Does.Contain(b));
            Assert.That(remaining, Does.Not.Contain(a));
        });
    }

    [Test]
    public void GetEntitiesWith_RepeatedCallsWithoutChange_AreConsistent()
    {
        var manager = new EntityManager();
        for (int i = 0; i < 4; i++) AddTransform(manager, $"e{i}");
        manager.Update();

        var first = manager.GetEntitiesWith<CTransform>();
        var second = manager.GetEntitiesWith<CTransform>();

        Assert.That(second, Is.EqualTo(first).AsCollection);
    }

    [Test]
    public void GetEntitiesWithComponents_Single_EarlierResultIsNotMutated()
    {
        var manager = new EntityManager();
        AddTransform(manager, "a");
        manager.Update();
        var firstResult = manager.GetEntitiesWithComponents<CTransform>();
        Assert.That(firstResult, Has.Count.EqualTo(1));

        AddTransform(manager, "b");
        manager.Update();
        var secondResult = manager.GetEntitiesWithComponents<CTransform>();

        Assert.Multiple(() =>
        {
            Assert.That(secondResult, Has.Count.EqualTo(2));
            Assert.That(firstResult, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void GetEntitiesWithComponents_Pair_EarlierResultIsNotMutated()
    {
        var manager = new EntityManager();
        var a = AddTransform(manager, "a");
        a.AddComponent(new CBoundingBox(new Vec2(10, 10), false, false));
        manager.Update();

        var firstResult = manager.GetEntitiesWithComponents<CBoundingBox, CTransform>();
        Assert.That(firstResult, Has.Count.EqualTo(1));

        var b = AddTransform(manager, "b");
        b.AddComponent(new CBoundingBox(new Vec2(10, 10), false, false));
        manager.Update();
        var secondResult = manager.GetEntitiesWithComponents<CBoundingBox, CTransform>();

        Assert.Multiple(() =>
        {
            Assert.That(secondResult, Has.Count.EqualTo(2));
            Assert.That(firstResult, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void GetEntitiesWithTag_EarlierResultIsNotMutated()
    {
        var manager = new EntityManager();
        manager.CreateEntity("wall");
        manager.Update();
        var firstResult = manager.GetEntitiesWithTag("wall");
        Assert.That(firstResult, Has.Count.EqualTo(1));

        manager.CreateEntity("wall");
        manager.Update();
        var secondResult = manager.GetEntitiesWithTag("wall");

        Assert.Multiple(() =>
        {
            Assert.That(secondResult, Has.Count.EqualTo(2));
            Assert.That(firstResult, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void GetEntitiesWith_SeesComponentAddedAfterAnEarlierQuery()
    {
        var manager = new EntityManager();
        var a = manager.CreateEntity("a");
        manager.Update();
        Assert.That(manager.GetEntitiesWith<CTransform>(), Is.Empty);

        a.AddComponent(new CTransform(Vec2.Zero));

        Assert.That(manager.GetEntitiesWith<CTransform>(), Has.Count.EqualTo(1));
    }

    [Test]
    public void GetEntitiesWith_SeesComponentRemovedAfterAnEarlierQuery()
    {
        var manager = new EntityManager();
        var a = AddTransform(manager, "a");
        manager.Update();
        Assert.That(manager.GetEntitiesWith<CTransform>(), Has.Count.EqualTo(1));

        a.RemoveComponent<CTransform>();

        Assert.That(manager.GetEntitiesWith<CTransform>(), Is.Empty);
    }
}
