using GameEngine.Core.Components;

namespace GameEngine.Core.Tests.Unit;

public class DrawOrderTests
{
    private static Entity Spawn(EntityManager manager, string tag, int layer = 0)
    {
        var entity = manager.CreateEntity(tag);
        entity.AddComponent(new CTransform(Vec2.Zero) { Layer = layer });
        return entity;
    }

    private static string DrawOrderOf(EntityManager manager)
    {
        var snapshot = new RenderSnapshot();
        manager.BuildRenderSnapshot(snapshot);
        var tags = new List<string>();
        foreach (var entry in snapshot.Entries) tags.Add(entry.Tag);
        return string.Join(",", tags);
    }

    [Test]
    public void DrawOrder_FollowsSpawnOrder()
    {
        var manager = new EntityManager();
        for (int i = 0; i < 6; i++) Spawn(manager, "e" + i);
        manager.Update();

        Assert.That(DrawOrderOf(manager), Is.EqualTo("e0,e1,e2,e3,e4,e5"));
    }

    [Test]
    public void DrawOrder_IsStableAcrossRemoveAndRespawn()
    {
        var manager = new EntityManager();
        var spawned = new List<Entity>();
        for (int i = 0; i < 8; i++) spawned.Add(Spawn(manager, "old" + i));
        manager.Update();

        spawned[2].Active = false;
        spawned[3].Active = false;
        spawned[5].Active = false;
        manager.Update();
        Assert.That(DrawOrderOf(manager), Is.EqualTo("old0,old1,old4,old6,old7"));

        for (int i = 0; i < 3; i++) Spawn(manager, "new" + i);
        manager.Update();

        Assert.That(DrawOrderOf(manager),
            Is.EqualTo("old0,old1,old4,old6,old7,new0,new1,new2"));
    }

    [Test]
    public void DrawOrder_SortsByLayerFirst()
    {
        var manager = new EntityManager();
        Spawn(manager, "midground", layer: 1);
        Spawn(manager, "foreground", layer: 2);
        Spawn(manager, "background", layer: 0);
        manager.Update();

        Assert.That(DrawOrderOf(manager), Is.EqualTo("background,midground,foreground"));
    }

    [Test]
    public void DrawOrder_WithinALayer_FollowsSpawnOrder()
    {
        var manager = new EntityManager();
        Spawn(manager, "hud1", layer: 10);
        Spawn(manager, "world1", layer: 0);
        Spawn(manager, "hud2", layer: 10);
        Spawn(manager, "world2", layer: 0);
        manager.Update();

        Assert.That(DrawOrderOf(manager), Is.EqualTo("world1,world2,hud1,hud2"));
    }

    [Test]
    public void DrawOrder_SupportsNegativeLayers()
    {
        var manager = new EntityManager();
        Spawn(manager, "normal", layer: 0);
        Spawn(manager, "behind", layer: -5);
        manager.Update();

        Assert.That(DrawOrderOf(manager), Is.EqualTo("behind,normal"));
    }

    [Test]
    public void DrawOrder_IsUnchangedByRepeatedSnapshots()
    {
        var manager = new EntityManager();
        Spawn(manager, "a", layer: 2);
        Spawn(manager, "b", layer: 1);
        manager.Update();

        var first = DrawOrderOf(manager);
        var second = DrawOrderOf(manager);

        Assert.That(second, Is.EqualTo(first));
        Assert.That(first, Is.EqualTo("b,a"));
    }
}
