using GameEngine.Core.Components;

namespace GameEngine.Core.Tests.Unit;

public class EntityGroupTests
{
    [Test]
    public void ConstructionReturnsHandlesWithoutChangingDeferredQueries()
    {
        var manager = new EntityManager();
        using var group = manager.CreateGroup();
        var entity = group.CreateEntity("label");
        var transform = new CTransform(new Vec2(1, 2));
        entity.AddComponent(transform);
        Assert.That(group.Entities.Single(), Is.SameAs(entity));
        Assert.That(entity.GetComponent<CTransform>(), Is.SameAs(transform));
        Assert.That(manager.GetEntityWithTag("label"), Is.Null);
        manager.Update();
        Assert.That(manager.GetEntityWithTag("label"), Is.SameAs(entity));
    }

    [Test]
    public void ClearingRetiresLiveAndPendingMembersWithoutTouchingOtherGroups()
    {
        var manager = new EntityManager();
        using var map = manager.CreateGroup();
        using var hud = manager.CreateGroup();
        var old = map.CreateEntity("old");
        hud.CreateEntity("hud");
        manager.Update();
        var pending = map.CreateEntity("pending");
        map.Clear();
        var replacement = map.CreateEntity("new");
        manager.Update();
        Assert.That(manager.GetEntities().Select(e => e.Tag), Is.EquivalentTo(new[] { "hud", "new" }));
        Assert.That(map.Entities.Single(), Is.SameAs(replacement));
        old.Active = pending.Active = true;
        old.AddComponent(new CTransform(Vec2.Zero));
        manager.Update();
        Assert.That(manager.GetEntities().Select(e => e.Tag), Is.EquivalentTo(new[] { "hud", "new" }));
    }

    [Test]
    public void DisposingAGroupIsIdempotentAndPreventsFurtherConstruction()
    {
        var manager = new EntityManager();
        var group = manager.CreateGroup();
        group.CreateEntity("pending");
        group.Dispose();
        group.Dispose();
        manager.Update();
        Assert.That(manager.GetEntities(), Is.Empty);
        Assert.Throws<ObjectDisposedException>(() => group.CreateEntity("late"));
    }
}
