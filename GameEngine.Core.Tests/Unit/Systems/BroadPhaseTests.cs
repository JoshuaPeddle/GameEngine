using GameEngine.Core.Components;
using GameEngine.Core.Systems;

namespace GameEngine.Core.Tests.Unit.Systems;

public class BroadPhaseTests
{
    private static EntityManager RandomScene(int seed, int count, int spread, int maxSize, bool solid)
    {
        var manager = new EntityManager();
        var random = new Random(seed);

        for (int i = 0; i < count; i++)
        {
            var entity = manager.CreateEntity("e" + i);
            entity.AddComponent(new CTransform(new Vec2(random.Next(0, spread), random.Next(0, spread))));
            entity.AddComponent(new CBoundingBox(
                new Vec2(random.Next(4, maxSize), random.Next(4, maxSize)), false, solid));
        }

        manager.Update();
        return manager;
    }

    private static List<(int, int)> ExhaustivePairs(EntityManager manager)
    {
        var entities = manager.GetEntitiesWithComponents<CBoundingBox, CTransform>().ToList();
        var pairs = new List<(int, int)>();

        for (int i = 0; i < entities.Count - 1; i++)
        {
            for (int j = i + 1; j < entities.Count; j++)
            {
                var overlap = Physics.GetOverlap(
                    entities[i].Item3, entities[j].Item3,
                    entities[i].Item2, entities[j].Item2);

                if (overlap.X > 0 && overlap.Y > 0)
                    pairs.Add((entities[i].Item1.Id, entities[j].Item1.Id));
            }
        }

        return pairs;
    }

    private static List<(int, int)> ReportedPairs(PhysicsSystem physics) =>
        physics.CollisionEvents.Select(c => (c.A.Id, c.B.Id)).ToList();

    [TestCase(1, 200, 400, 40)]
    [TestCase(2, 500, 2000, 30)]
    [TestCase(3, 300, 300, 80)]
    [TestCase(4, 50, 100, 60)]
    [TestCase(5, 400, 5000, 20)]
    public void BroadPhase_FindsExactlyThePairsExhaustiveTestingFinds(int seed, int count, int spread, int maxSize)
    {
        var manager = RandomScene(seed, count, spread, maxSize, solid: false);
        var expected = ExhaustivePairs(manager);

        var physics = new PhysicsSystem();
        physics.Update(manager, 1.0 / 60.0);

        Assert.That(ReportedPairs(physics), Is.EqualTo(expected).AsCollection);
    }

    [Test]
    public void BroadPhase_HandlesEntitiesStackedAtOnePoint()
    {
        var manager = new EntityManager();
        for (int i = 0; i < 40; i++)
        {
            var entity = manager.CreateEntity("e" + i);
            entity.AddComponent(new CTransform(Vec2.Zero));
            entity.AddComponent(new CBoundingBox(new Vec2(10, 10), false, false));
        }
        manager.Update();

        var expected = ExhaustivePairs(manager);

        var physics = new PhysicsSystem();
        physics.Update(manager, 1.0 / 60.0);

        Assert.That(ReportedPairs(physics), Is.EqualTo(expected).AsCollection);
        Assert.That(expected, Has.Count.EqualTo(40 * 39 / 2));
    }

    [Test]
    public void BroadPhase_HandlesWildlyDifferentSizes()
    {
        var manager = new EntityManager();

        var huge = manager.CreateEntity("huge");
        huge.AddComponent(new CTransform(new Vec2(-5000, -5000)));
        huge.AddComponent(new CBoundingBox(new Vec2(10000, 10000), false, false));

        var random = new Random(9);
        for (int i = 0; i < 100; i++)
        {
            var entity = manager.CreateEntity("small" + i);
            entity.AddComponent(new CTransform(new Vec2(random.Next(-400, 400), random.Next(-400, 400))));
            entity.AddComponent(new CBoundingBox(new Vec2(6, 6), false, false));
        }
        manager.Update();

        var expected = ExhaustivePairs(manager);

        var physics = new PhysicsSystem();
        physics.Update(manager, 1.0 / 60.0);

        Assert.That(ReportedPairs(physics), Is.EqualTo(expected).AsCollection);
        Assert.That(expected, Has.Count.GreaterThanOrEqualTo(100));
    }

    [Test]
    public void BroadPhase_HandlesNegativeAndFractionalCoordinates()
    {
        var manager = new EntityManager();
        var random = new Random(11);

        for (int i = 0; i < 150; i++)
        {
            var entity = manager.CreateEntity("e" + i);
            entity.AddComponent(new CTransform(new Vec2(
                random.NextDouble() * 600 - 300,
                random.NextDouble() * 600 - 300)));
            entity.AddComponent(new CBoundingBox(new Vec2(17.5, 23.25), false, false));
        }
        manager.Update();

        var expected = ExhaustivePairs(manager);

        var physics = new PhysicsSystem();
        physics.Update(manager, 1.0 / 60.0);

        Assert.That(ReportedPairs(physics), Is.EqualTo(expected).AsCollection);
    }

    [Test]
    public void BroadPhase_IsStableAcrossRepeatedFrames()
    {
        var manager = RandomScene(7, 250, 600, 40, solid: false);
        var physics = new PhysicsSystem();

        physics.Update(manager, 1.0 / 60.0);
        var first = ReportedPairs(physics);

        physics.Update(manager, 1.0 / 60.0);
        var second = ReportedPairs(physics);

        Assert.That(second, Is.EqualTo(first).AsCollection);
    }

    [Test]
    public void BroadPhase_StillResolvesSolidCollisions()
    {
        var manager = RandomScene(13, 120, 300, 40, solid: true);
        var physics = new PhysicsSystem();

        physics.Update(manager, 1.0 / 60.0);

        Assert.That(physics.CollisionEvents, Is.Not.Empty);
        foreach (var entity in manager.GetEntities())
        {
            var position = entity.GetComponent<CTransform>().Position;
            Assert.That(double.IsFinite(position.X) && double.IsFinite(position.Y), Is.True);
        }
    }

    [Test]
    public void BroadPhase_HandlesFewerThanTwoEntities()
    {
        var manager = new EntityManager();
        var only = manager.CreateEntity("only");
        only.AddComponent(new CTransform(Vec2.Zero));
        only.AddComponent(new CBoundingBox(new Vec2(10, 10), false, false));
        manager.Update();

        var physics = new PhysicsSystem();

        Assert.DoesNotThrow(() => physics.Update(manager, 1.0 / 60.0));
        Assert.That(physics.CollisionEvents, Is.Empty);
    }
}
