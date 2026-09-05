using GameEngine.Core.Components;
using GameEngine.Core.Systems;

namespace GameEngine.Core.Tests.Unit;

public class FixedStepTests
{
    private const double Step = 1.0 / 120.0;

    private static FixedStepAccumulator NewAccumulator(int budget = 8) => new(Step, budget);

    [Test]
    public void ARunOfFramesProducesOneStepPerStepOfElapsedTime()
    {
        var accumulator = NewAccumulator();
        int steps = 0;

        for (int frame = 0; frame < 60; frame++)
            steps += accumulator.Advance(1.0 / 60.0);

        Assert.That(steps, Is.EqualTo(120));
    }

    // Both schedules cover exactly one second in 64 frames; only the shape of the frames
    // differs. The values are exact binary fractions, so a difference in the step count is
    // the accumulator's doing rather than the arithmetic's.
    private static readonly double[] SteadySchedule = [1.0 / 64];

    private static readonly double[] JitterySchedule =
        [1.0 / 256, 8.0 / 256, 3.0 / 256, 4.0 / 256];

    private const double ExactStep = 1.0 / 128;
    private const int ScheduleFrames = 64;

    [Test]
    public void TheSameElapsedTimeSplitDifferentlyProducesTheSameNumberOfSteps()
    {
        var steady = new FixedStepAccumulator(ExactStep, 16);
        var jittery = new FixedStepAccumulator(ExactStep, 16);

        int steadySteps = 0;
        int jitterySteps = 0;

        for (int frame = 0; frame < ScheduleFrames; frame++)
        {
            steadySteps += steady.Advance(SteadySchedule[frame % SteadySchedule.Length]);
            jitterySteps += jittery.Advance(JitterySchedule[frame % JitterySchedule.Length]);
        }

        Assert.Multiple(() =>
        {
            Assert.That(steadySteps, Is.EqualTo(128));
            Assert.That(jitterySteps, Is.EqualTo(128));
        });
    }

    [Test]
    public void AFrameNeverRunsMoreStepsThanTheCatchUpBudget()
    {
        var accumulator = NewAccumulator(budget: 4);

        int steps = accumulator.Advance(10.0);

        Assert.Multiple(() =>
        {
            Assert.That(steps, Is.EqualTo(4));
            Assert.That(accumulator.DroppedSeconds, Is.EqualTo(10.0 - 4 * Step).Within(1e-9));
            Assert.That(accumulator.Pending, Is.EqualTo(0));
        });
    }

    [Test]
    public void TimeBeyondTheBudgetIsDroppedRatherThanQueued()
    {
        var accumulator = NewAccumulator(budget: 2);

        accumulator.Advance(1.0);
        int next = accumulator.Advance(1.0 / 120.0);

        Assert.That(next, Is.EqualTo(1), "the frame after a stall runs a normal step, not a backlog");
    }

    [Test]
    public void AFrameShorterThanAStepAccumulatesRatherThanRounding()
    {
        var accumulator = NewAccumulator();

        Assert.Multiple(() =>
        {
            Assert.That(accumulator.Advance(Step / 3), Is.EqualTo(0));
            Assert.That(accumulator.Advance(Step / 3), Is.EqualTo(0));
            Assert.That(accumulator.Advance(Step / 3), Is.EqualTo(1));
        });
    }

    [Test]
    public void ResetDropsThePendingRemainder()
    {
        var accumulator = NewAccumulator();
        accumulator.Advance(Step * 0.9);

        accumulator.Reset();

        Assert.Multiple(() =>
        {
            Assert.That(accumulator.Pending, Is.EqualTo(0));
            Assert.That(accumulator.Advance(Step * 0.9), Is.EqualTo(0));
        });
    }

    [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)]
    [TestCase(-0.001)]
    public void InvalidElapsedTimeIsRejected(double elapsed)
    {
        var accumulator = NewAccumulator();

        Assert.Throws<ArgumentOutOfRangeException>(() => accumulator.Advance(elapsed));
    }

    [TestCase(0.0)]
    [TestCase(-1.0)]
    [TestCase(double.NaN)]
    public void AnInvalidStepIsRejected(double step)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FixedStepAccumulator(step, 4));
    }

    [TestCase(double.NaN)]
    [TestCase(double.NegativeInfinity)]
    [TestCase(-0.001)]
    public void TickRejectsInvalidElapsedTime(double elapsed)
    {
        using var engine = new Engine(audioEnabled: false);

        Assert.Throws<ArgumentOutOfRangeException>(() => engine.Tick(elapsed));
    }

    [Test]
    public void TickAdvancesByExactlyWhatItIsGiven()
    {
        using var engine = new Engine(audioEnabled: false);
        engine.ChangeScene(new MovingScene());
        engine.Tick(0);
        engine.NotifyFirstPresent();

        engine.Tick(0.5);

        var mover = engine.EntityManager.GetEntityWithTag("mover")!;
        Assert.That(mover.GetComponent<CTransform>().Position.X, Is.EqualTo(50).Within(1e-9));
    }

    [Test]
    public void TheEngineExposesItsSimulationRateSeparatelyFromItsPresentationRate()
    {
        using var engine = new Engine(audioEnabled: false) { TargetFrameRate = 240 };

        engine.FixedTimeStep = 1.0 / 50.0;
        engine.MaxCatchUpSteps = 3;

        Assert.Multiple(() =>
        {
            Assert.That(engine.FixedTimeStep, Is.EqualTo(1.0 / 50.0));
            Assert.That(engine.MaxCatchUpSteps, Is.EqualTo(3));
            Assert.That(engine.TargetFrameRate, Is.EqualTo(240));
        });
    }

    // Scripted input, two different host-frame schedules, one simulated second: the run loop
    // is not exercised here, but the step sequence it would produce is.
    [Test]
    public void EquivalentElapsedTimeProducesEquivalentSimulation()
    {
        var steady = RunSchedule(SteadySchedule);
        var jittery = RunSchedule(JitterySchedule);

        Assert.Multiple(() =>
        {
            Assert.That(steady.Steps, Is.EqualTo(128));
            Assert.That(jittery.Steps, Is.EqualTo(steady.Steps));
            Assert.That(jittery.X, Is.EqualTo(steady.X).Within(1e-6));
        });
    }

    private static (int Steps, double X) RunSchedule(double[] schedule)
    {
        using var engine = new Engine(audioEnabled: false);
        engine.ChangeScene(new MovingScene());
        engine.Tick(0);
        engine.NotifyFirstPresent();

        var accumulator = new FixedStepAccumulator(ExactStep, 16);
        var mover = engine.EntityManager.GetEntityWithTag("mover")!;

        int steps = 0;

        for (int frame = 0; frame < ScheduleFrames; frame++)
        {
            int due = accumulator.Advance(schedule[frame % schedule.Length]);
            for (int step = 0; step < due; step++)
            {
                engine.Tick(ExactStep);
                steps++;
            }
        }

        return (steps, mover.GetComponent<CTransform>().Position.X);
    }

    private sealed class MovingScene : Scene
    {
        public override void Initialize(EntityManager entityManager, InputManager inputManager,
            AudioSystem? audioPlayer, Action<Scene?> resetScene)
        {
            var mover = entityManager.CreateEntity("mover");
            mover.AddComponent(new CTransform(Vec2.Zero, new Vec2(100, 0)));
            mover.AddComponent(new CMovement(0, 1000));
        }
    }
}
