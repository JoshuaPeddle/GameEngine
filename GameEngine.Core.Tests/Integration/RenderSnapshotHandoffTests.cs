using System.Diagnostics;
using GameEngine.Core.Components;
using GameEngine.Core.Systems;

namespace GameEngine.Core.Tests.Integration;

/// <summary>
/// Covers the handoff of render state from the engine thread to the UI thread:
/// the reader must never observe a buffer the engine is refilling, and a scene change
/// must not leave the first paint showing the previous scene.
/// </summary>
public class RenderSnapshotHandoffTests
{
    /// <summary>Drives every transform to the same position each frame, so any snapshot must be internally uniform.</summary>
    private sealed class LockstepSystem : ISystem
    {
        public int Frame;

        public void Update(EntityManager entityManager, double deltaSeconds)
        {
            Frame++;
            var position = new Vec2(Frame, Frame);
            foreach (var entity in entityManager.GetEntities())
            {
                if (entity.TryGetComponent<CTransform>(out var transform))
                    transform.Position = position;
            }
        }
    }

    /// <summary>Populates a scene whose entities are identifiable by tag, and signals when it is initialized.</summary>
    private sealed class TaggedScene(string tag, int count, Action? onInitialized = null) : Scene
    {
        public override void Initialize(EntityManager entityManager, InputManager inputManager,
            AudioSystem? audioPlayer, Action<Scene?> resetScene)
        {
            for (int i = 0; i < count; i++)
                entityManager.CreateEntity(tag).AddComponent(new CTransform(new Vec2(i, i)));

            onInitialized?.Invoke();
        }
    }

    [Test]
    public void GetRenderSnapshot_NeverReturnsBufferTheEngineIsRefilling()
    {
        // Arrange: engine running flat out, reader holding each buffer across a realistic paint
        var engine = new Engine(audioEnabled: false);
        var lockstep = new LockstepSystem();
        engine.Systems.Add(lockstep);

        for (int i = 0; i < 2000; i++)
        {
            var entity = engine.EntityManager.CreateEntity("floor");
            entity.AddComponent(new CTransform(new Vec2(0, 0)));
            entity.AddComponent(new CBoundingBox(new Vec2(40, 40), false, true));
        }
        engine.EntityManager.CreateEntity("camera").AddComponent(new CCamera { Zoom = 2.0f });

        engine.Start();

        int reads = 0, torn = 0, refilledWhileHeld = 0, missingCamera = 0;
        var stopwatch = Stopwatch.StartNew();

        // Act
        try
        {
            while (stopwatch.Elapsed < TimeSpan.FromSeconds(2))
            {
                var snapshot = engine.GetRenderSnapshot();
                if (snapshot.Entries.Length == 0)
                {
                    Thread.Sleep(1);
                    continue;
                }

                reads++;
                if (snapshot.ActiveCamera is null)
                    missingCamera++;

                int length = snapshot.Entries.Length;
                double first = snapshot.Entries[0].Transform.Position.X;

                var atAcquire = snapshot.Entries;
                for (int i = 1; i < atAcquire.Length; i++)
                {
                    if (atAcquire[i].Transform.Position.X != first)
                    {
                        torn++;
                        break;
                    }
                }

                // Hold the buffer while the engine completes many more frames.
                Thread.Sleep(16);

                var afterHold = snapshot.Entries;
                if (afterHold.Length != length || afterHold[0].Transform.Position.X != first)
                    refilledWhileHeld++;
            }
        }
        finally
        {
            engine.Dispose();
        }

        // Assert
        Assert.That(reads, Is.GreaterThan(10), "the engine should have published frames to read");
        Assert.Multiple(() =>
        {
            Assert.That(torn, Is.Zero, $"{torn} of {reads} snapshots were internally inconsistent");
            Assert.That(refilledWhileHeld, Is.Zero, $"{refilledWhileHeld} of {reads} held buffers were refilled");
            Assert.That(missingCamera, Is.Zero, $"{missingCamera} of {reads} snapshots lost the camera");
        });
    }

    [Test]
    public void ChangeScene_NeverPaintsThePreviousScene_AfterTheNewSceneIsInitialized()
    {
        // Arrange: the paint callback is the sole reader, as it is in every runner.
        // Everything below — Initialize, publish, and the paint request — runs on the engine
        // thread in that order, so once the new scene has initialized, every paint that
        // follows must already be showing it.
        Engine engine = null!;
        var newSceneInitialized = new ManualResetEventSlim(false);
        int paintsOfNewScene = 0;
        int stalePaints = 0;
        int paintsSeen = 0;

        engine = new Engine(() =>
        {
            var snapshot = engine.GetRenderSnapshot();
            string tag = snapshot.Entries.Length == 0 ? "<empty>" : snapshot.Entries[0].Tag;

            Interlocked.Increment(ref paintsSeen);
            if (newSceneInitialized.IsSet)
            {
                if (tag == "scene-B" && snapshot.Entries.Length == 9)
                    Interlocked.Increment(ref paintsOfNewScene);
                else
                    Interlocked.Increment(ref stalePaints);
            }

            engine.NotifyFirstPresent();
        }, audioEnabled: false);

        try
        {
            engine.ChangeScene(new TaggedScene("scene-A", 5));
            engine.Start();
            Thread.Sleep(300);
            Assert.That(Volatile.Read(ref paintsSeen), Is.GreaterThan(0), "the first scene should be painting");

            // Act: swap scenes. Updates are paused until the first present, so with no explicit
            // publish the paint requested by the scene change still shows scene-A.
            engine.ChangeScene(new TaggedScene("scene-B", 9, () => newSceneInitialized.Set()));

            Assert.That(newSceneInitialized.Wait(TimeSpan.FromSeconds(5)), Is.True,
                "the new scene was never initialized");

            var stopwatch = Stopwatch.StartNew();
            while (Volatile.Read(ref paintsOfNewScene) + Volatile.Read(ref stalePaints) < 5
                   && stopwatch.Elapsed < TimeSpan.FromSeconds(5))
            {
                Thread.Sleep(5);
            }

            // Assert
            Assert.That(Volatile.Read(ref paintsOfNewScene), Is.GreaterThan(0), "the new scene never painted");
            Assert.That(Volatile.Read(ref stalePaints), Is.Zero,
                $"{Volatile.Read(ref stalePaints)} paint(s) showed the previous scene after the swap");
        }
        finally
        {
            engine.Dispose();
        }
    }
}
