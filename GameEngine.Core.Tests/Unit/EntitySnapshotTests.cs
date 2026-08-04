using GameEngine.Core.Components;

namespace GameEngine.Core.Tests.Unit;

public class EntitySnapshotTests
{
    private static Entity NewEntity(string tag = "player")
    {
        var entities = new EntityManager();
        var entity = entities.CreateEntity(tag);
        entities.Update();
        return entity;
    }

    [Test]
    public void Capture_RecordsIdentityAndComponentNames()
    {
        var entity = NewEntity();
        entity.AddComponent(new CTransform(Vec2.Zero));
        entity.AddComponent(new CBoundingBox(new Vec2(4, 4), false, true));

        var snapshot = entity.Capture();

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.Id, Is.EqualTo(entity.Id));
            Assert.That(snapshot.Tag, Is.EqualTo("player"));
            Assert.That(snapshot.Active, Is.True);
            Assert.That(snapshot.ComponentTypes, Is.EqualTo(new[] { "CBoundingBox", "CTransform" }));
        });
    }

    [Test]
    public void Capture_IsUnaffectedByLaterMutation()
    {
        var entity = NewEntity();
        entity.AddComponent(new CTransform(Vec2.Zero));

        var snapshot = entity.Capture();

        entity.AddComponent<CInput>();
        entity.Tag = "renamed";
        entity.Active = false;

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.ComponentTypes, Is.EqualTo(new[] { "CTransform" }),
                "a snapshot handed to the UI thread must not change underneath it");
            Assert.That(snapshot.Tag, Is.EqualTo("player"));
            Assert.That(snapshot.Active, Is.True);
        });
    }

    [Test]
    public void Capture_OrdersComponentNames()
    {
        var entity = NewEntity();
        entity.AddComponent<CInput>();
        entity.AddComponent(new CTransform(Vec2.Zero));
        entity.AddComponent(new CGravity());

        Assert.That(entity.Capture().ComponentTypes, Is.Ordered);
    }

    [Test]
    public void Capture_OnAnEntityWithNoComponents_IsEmptyRatherThanNull()
    {
        Assert.That(NewEntity().Capture().ComponentTypes, Is.Empty);
    }
}
