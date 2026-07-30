using GameEngine.Core.Components;

namespace GameEngine.Core.Tests.Unit;

public class RenderSnapshotTests
{
    private static Entity AddCamera(EntityManager entityManager, string tag, float zoom, Vec2 position)
    {
        var entity = entityManager.CreateEntity(tag);
        entity.AddComponent(new CCamera { Zoom = zoom, Position = position });
        return entity;
    }

    [Test]
    public void BuildRenderSnapshot_FindsCamera_WhenCameraEntityHasNoTransform()
    {
        // Arrange: the demo scenes give the camera entity a CCamera and nothing else,
        // so it is not reachable by walking the entities that have a CTransform.
        var entityManager = new EntityManager();
        var player = entityManager.CreateEntity("player");
        player.AddComponent(new CTransform(new Vec2(100, 200)));
        AddCamera(entityManager, "camera", 2.0f, new Vec2(0, 500));
        entityManager.Update();

        // Act
        var snapshot = new RenderSnapshot();
        entityManager.BuildRenderSnapshot(snapshot);

        // Assert
        Assert.That(snapshot.ActiveCamera, Is.Not.Null, "camera without a CTransform must still be found");
        Assert.Multiple(() =>
        {
            Assert.That(snapshot.ActiveCamera!.Value.Zoom, Is.EqualTo(2.0f));
            Assert.That(snapshot.ActiveCamera!.Value.Position.Y, Is.EqualTo(500));
            // The camera has no CTransform, so it is not something to draw.
            Assert.That(snapshot.Entries.Length, Is.EqualTo(1));
            Assert.That(snapshot.Entries[0].Tag, Is.EqualTo("player"));
        });
    }

    [Test]
    public void BuildRenderSnapshot_PrefersEntityTaggedCamera_WhenSeveralCamerasExist()
    {
        // Arrange
        var entityManager = new EntityManager();
        AddCamera(entityManager, "other", 9.0f, new Vec2(0, 0));
        AddCamera(entityManager, "camera", 2.0f, new Vec2(0, 500));
        AddCamera(entityManager, "another", 7.0f, new Vec2(0, 0));
        entityManager.Update();

        // Act
        var snapshot = new RenderSnapshot();
        entityManager.BuildRenderSnapshot(snapshot);

        // Assert: deterministic pick, matching the tag the renderer has always used
        Assert.That(snapshot.ActiveCamera!.Value.Zoom, Is.EqualTo(2.0f));
    }

    [Test]
    public void BuildRenderSnapshot_FallsBackToAnyCamera_WhenNoneIsTagged()
    {
        // Arrange
        var entityManager = new EntityManager();
        AddCamera(entityManager, "untagged", 3.0f, new Vec2(0, 0));
        entityManager.Update();

        // Act
        var snapshot = new RenderSnapshot();
        entityManager.BuildRenderSnapshot(snapshot);

        // Assert
        Assert.That(snapshot.ActiveCamera!.Value.Zoom, Is.EqualTo(3.0f));
    }

    [Test]
    public void BuildRenderSnapshot_HasNoCamera_WhenSceneHasNone()
    {
        // Arrange
        var entityManager = new EntityManager();
        entityManager.CreateEntity("player").AddComponent(new CTransform(new Vec2(1, 2)));
        entityManager.Update();

        // Act
        var snapshot = new RenderSnapshot();
        entityManager.BuildRenderSnapshot(snapshot);

        // Assert
        Assert.That(snapshot.ActiveCamera, Is.Null);
    }

    [Test]
    public void BuildRenderSnapshot_CopiesComponentStateByValue()
    {
        // Arrange
        var entityManager = new EntityManager();
        var entity = entityManager.CreateEntity("player");
        var transform = new CTransform(new Vec2(10, 20));
        transform.Rotation = 45;
        entity.AddComponent(transform);
        entity.AddComponent(new CBoundingBox(new Vec2(40, 60), false, true));
        entityManager.Update();

        var snapshot = new RenderSnapshot();
        entityManager.BuildRenderSnapshot(snapshot);

        // Act: mutating the live component after the snapshot must not affect it
        transform.Position = new Vec2(999, 999);
        transform.Rotation = 180;

        // Assert
        var entry = snapshot.Entries[0];
        Assert.Multiple(() =>
        {
            Assert.That(entry.Transform.Position.X, Is.EqualTo(10));
            Assert.That(entry.Transform.Position.Y, Is.EqualTo(20));
            Assert.That(entry.Transform.Rotation, Is.EqualTo(45));
            Assert.That(entry.BoundingBox!.Value.Width, Is.EqualTo(40));
            Assert.That(entry.BoundingBox!.Value.Height, Is.EqualTo(60));
        });
    }

    [Test]
    public void BuildRenderSnapshot_ReusesItsBuffer_AndAllocatesNothingPerFrame()
    {
        // Arrange: a scene the size of SceneSideScroll's floor
        var entityManager = new EntityManager();
        for (int i = 0; i < 1000; i++)
        {
            var entity = entityManager.CreateEntity("floor");
            entity.AddComponent(new CTransform(new Vec2(40 * i, 760)));
            entity.AddComponent(new CBoundingBox(new Vec2(40, 40), false, true));
        }
        AddCamera(entityManager, "camera", 2.0f, new Vec2(0, 0));
        entityManager.Update();

        var snapshot = new RenderSnapshot();

        // Warm up past tiered JIT and let the backing array reach its final size.
        for (int i = 0; i < 1000; i++)
            entityManager.BuildRenderSnapshot(snapshot);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // Act
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int frame = 0; frame < 2000; frame++)
            entityManager.BuildRenderSnapshot(snapshot);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        // Assert
        Assert.That(snapshot.Entries.Length, Is.EqualTo(1000));
        Assert.That(allocated, Is.Zero,
            $"rendering must not allocate per frame, but 2000 frames allocated {allocated} bytes");
    }

    [Test]
    public void BuildRenderSnapshot_ShrinksToFewerEntities_WhenEntitiesAreRemoved()
    {
        // Arrange
        var entityManager = new EntityManager();
        var entities = new List<Entity>();
        for (int i = 0; i < 10; i++)
        {
            var entity = entityManager.CreateEntity("floor");
            entity.AddComponent(new CTransform(new Vec2(i, i)));
            entities.Add(entity);
        }
        entityManager.Update();

        var snapshot = new RenderSnapshot();
        entityManager.BuildRenderSnapshot(snapshot);
        Assert.That(snapshot.Entries.Length, Is.EqualTo(10));

        // Act: the reused buffer must not report stale entries from the larger frame
        for (int i = 0; i < 6; i++)
            entities[i].Active = false;
        entityManager.Update();
        entityManager.BuildRenderSnapshot(snapshot);

        // Assert
        Assert.That(snapshot.Entries.Length, Is.EqualTo(4));
    }
}
