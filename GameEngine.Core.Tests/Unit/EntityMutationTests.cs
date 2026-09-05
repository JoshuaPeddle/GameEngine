using GameEngine.Core.Components;
using GameEngine.Core.Systems;

namespace GameEngine.Core.Tests.Unit;

public class EntityMutationTests
{
    private static EntityManager LiveManager(out Entity entity, string tag = "player")
    {
        var manager = new EntityManager();
        entity = manager.CreateEntity(tag);
        entity.AddComponent(new CTransform(new Vec2(100, 0)));
        manager.Update();
        return manager;
    }

    [Test]
    public void ReplacingAComponent_IsVisibleToASingleComponentQuery()
    {
        var manager = LiveManager(out var entity);
        _ = manager.GetEntitiesWithComponents<CTransform>();

        entity.AddComponent(new CTransform(new Vec2(500, 0)));

        var (_, transform) = manager.GetEntitiesWithComponents<CTransform>().Single();
        Assert.That(transform.Position.X, Is.EqualTo(500));
    }

    [Test]
    public void ReplacingAComponent_IsVisibleToAPairQuery()
    {
        var manager = LiveManager(out var entity);
        entity.AddComponent(new CMovement(1, 1));
        manager.Update();
        _ = manager.GetEntitiesWithComponents<CTransform, CMovement>();

        entity.AddComponent(new CTransform(new Vec2(500, 0)));

        var (_, transform, _) = manager.GetEntitiesWithComponents<CTransform, CMovement>().Single();
        Assert.That(transform.Position.X, Is.EqualTo(500));
    }

    [Test]
    public void ReplacingAComponent_ThroughTheGenericOverload_IsVisibleToQueries()
    {
        var manager = LiveManager(out var entity);
        entity.AddComponent(new CInput { Right = true });
        manager.Update();
        _ = manager.GetEntitiesWithComponents<CInput>();

        entity.AddComponent<CInput>(new CInput { Right = false });

        Assert.That(manager.GetEntitiesWithComponents<CInput>().Single().Item2.Right, Is.False);
    }

    [Test]
    public void ReplacingAComponent_ThroughTheNewOverload_IsVisibleToQueries()
    {
        var manager = LiveManager(out var entity);
        entity.AddComponent(new CInput { Right = true });
        manager.Update();
        _ = manager.GetEntitiesWithComponents<CInput>();

        entity.AddComponent<CInput>();

        Assert.That(manager.GetEntitiesWithComponents<CInput>().Single().Item2.Right, Is.False);
    }

    [Test]
    public void MovementSystem_DrivesTheReplacementTransform()
    {
        var manager = LiveManager(out var entity);
        entity.AddComponent(new CMovement(0, 1000));
        manager.Update();

        var movement = new MovementSystem();
        movement.Update(manager, 1);

        var replacement = new CTransform(new Vec2(100, 0)) { Velocity = new Vec2(10, 0) };
        entity.AddComponent(replacement);
        movement.Update(manager, 1);

        Assert.That(replacement.Position.X, Is.EqualTo(110));
    }

    [Test]
    public void RenamingAnEntity_InvalidatesTheOldTagQuery()
    {
        var manager = LiveManager(out var entity);
        Assert.That(manager.GetEntitiesWithTag("player"), Has.Count.EqualTo(1));

        entity.Tag = "hero";

        Assert.Multiple(() =>
        {
            Assert.That(manager.GetEntitiesWithTag("player"), Is.Empty);
            Assert.That(manager.GetEntitiesWithTag("hero"), Has.Count.EqualTo(1));
            Assert.That(manager.GetEntityWithTag("hero"), Is.SameAs(entity));
            Assert.That(manager.GetEntityWithTag("player"), Is.Null);
        });
    }

    [Test]
    public void RemovingAComponent_IsVisibleToQueries()
    {
        var manager = LiveManager(out var entity);
        _ = manager.GetEntitiesWithComponents<CTransform>();

        entity.RemoveComponent<CTransform>();

        Assert.That(manager.GetEntitiesWithComponents<CTransform>(), Is.Empty);
    }

    [Test]
    public void APreviouslyReturnedQueryList_IsNotMutatedByALaterChange()
    {
        var manager = LiveManager(out var entity);
        var held = manager.GetEntitiesWithComponents<CTransform>();

        entity.AddComponent(new CTransform(new Vec2(500, 0)));
        var refreshed = manager.GetEntitiesWithComponents<CTransform>();

        Assert.Multiple(() =>
        {
            Assert.That(held[0].Item2.Position.X, Is.EqualTo(100));
            Assert.That(refreshed[0].Item2.Position.X, Is.EqualTo(500));
            Assert.That(refreshed, Is.Not.SameAs(held));
        });
    }

    [Test]
    public void PendingEntities_AreInvisibleToEveryQueryUntilUpdate()
    {
        var manager = new EntityManager();
        var entity = manager.CreateEntity("pending");
        entity.AddComponent(new CTransform(Vec2.Zero));

        Assert.Multiple(() =>
        {
            Assert.That(manager.GetEntities(), Is.Empty);
            Assert.That(manager.GetEntitiesWith<CTransform>(), Is.Empty);
            Assert.That(manager.GetEntitiesWithComponents<CTransform>(), Is.Empty);
            Assert.That(manager.GetEntitiesWithTag("pending"), Is.Empty);
        });

        manager.Update();

        Assert.Multiple(() =>
        {
            Assert.That(manager.GetEntities(), Has.Count.EqualTo(1));
            Assert.That(manager.GetEntitiesWithComponents<CTransform>(), Has.Count.EqualTo(1));
            Assert.That(manager.GetEntitiesWithTag("pending"), Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void ADetachedEntity_CannotReEnterTheComponentIndex()
    {
        var manager = LiveManager(out var entity);
        entity.Active = false;
        manager.Update();

        entity.Active = true;
        entity.AddComponent(new CMovement(1, 1));
        entity.Tag = "back from the dead";
        manager.Update();

        Assert.Multiple(() =>
        {
            Assert.That(manager.GetEntities(), Is.Empty);
            Assert.That(manager.GetEntitiesWithComponents<CMovement>(), Is.Empty);
            Assert.That(manager.GetEntitiesWithTag("back from the dead"), Is.Empty);
            Assert.That(manager.TryGetEntity(entity.Id, out _), Is.False);
        });
    }

    [Test]
    public void EntitiesRetainedAfterClear_CannotReEnterTheComponentIndex()
    {
        var manager = LiveManager(out var entity);
        var pending = manager.CreateEntity("pending");

        manager.Clear();
        entity.AddComponent(new CMovement(1, 1));
        pending.AddComponent(new CTransform(Vec2.Zero));
        manager.Update();

        Assert.Multiple(() =>
        {
            Assert.That(manager.GetEntities(), Is.Empty);
            Assert.That(manager.GetEntitiesWithComponents<CMovement>(), Is.Empty);
            Assert.That(manager.GetEntitiesWithComponents<CTransform>(), Is.Empty);
        });
    }

    [Test]
    public void ActionBindings_FollowAReplacedComponent()
    {
        var manager = LiveManager(out var entity);
        var input = new InputManager();
        entity.AddComponent(new CInput());
        manager.Update();

        input.AddAction(GeKeys.D, "Right");
        input.ActionMapper.MapActionToComponent<CInput>("Right", entity, (c, active) => c.Right = active);

        var replacement = entity.AddComponent(new CInput());
        input.HandleKeyPress(GeKeys.D);
        input.DoActions();

        Assert.That(((CInput)replacement).Right, Is.True);
    }

    [Test]
    public void ActionBindings_GoInertWhenTheComponentIsRemoved()
    {
        var manager = LiveManager(out var entity);
        var input = new InputManager();
        entity.AddComponent(new CInput());
        manager.Update();

        input.AddAction(GeKeys.D, "Right");
        input.ActionMapper.MapActionToComponent<CInput>("Right", entity, (c, active) => c.Right = active);
        entity.RemoveComponent<CInput>();

        input.HandleKeyPress(GeKeys.D);

        Assert.DoesNotThrow(input.DoActions);
    }
}
