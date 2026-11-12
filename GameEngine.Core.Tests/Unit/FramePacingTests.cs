using GameEngine.Core.Components;
using GameEngine.Core.Effects;
using GameEngine.Core.Systems;
using SkiaSharp;
using static GameEngine.Core.Pointer;

namespace GameEngine.Core.Tests.Unit;

public class FramePacingTests
{
    private sealed class QuietScene : Scene
    {
        public override FrameActivity FrameActivity => FrameActivity.Idle;
        public int Ticks;
        public double Seconds;
        public override void Initialize(EntityManager entities, InputManager input, AudioSystem? audio, Action<Scene?> reset) { }
        public override void Update(EntityManager entities, SystemContainer systems, double seconds) { Ticks++; Seconds += seconds; }
    }

    private sealed class ActivitySystem : ISystem, IFrameActivitySource
    {
        public FrameActivity FrameActivity { get; set; }
        public int Ticks;
        public void Update(EntityManager entities, double seconds) => Ticks++;
    }

    private sealed class FailingActivitySystem : ISystem, IFrameActivitySource
    {
        public FrameActivity FrameActivity => throw new InvalidOperationException("activity failed");
        public void Update(EntityManager entities, double seconds) { }
    }

    private sealed class StaticScene : Scene
    {
        public override void Initialize(EntityManager entities, InputManager input, AudioSystem? audio, Action<Scene?> reset) { }
    }

    private static Engine Create(Scene scene)
    {
        var engine = new Engine(audioEnabled: false) { TargetFrameRate = 60, FixedTimeStep = 1.0 / 120 };
        engine.ChangeScene(scene);
        engine.Tick(0);
        engine.NotifyFirstPresent();
        engine.Present(0);
        return engine;
    }

    private static int Run(Engine engine, double seconds)
    {
        int frames = 0;
        for (int i = 0; i < (int)Math.Round(seconds * 120); i++)
            if (engine.Present(1.0 / 120)) frames++;
        return frames;
    }

    [Test]
    public void SettledSceneProducesNoTicksOrDrawsForAnHour()
    {
        var scene = new QuietScene();
        using var engine = Create(scene);
        Run(engine, 2);
        int ticks = scene.Ticks;
        int draws = 0;
        engine.InvalidateAction = () => draws++;
        Assert.That(Run(engine, 3600), Is.Zero);
        Assert.That(scene.Ticks, Is.EqualTo(ticks));
        Assert.That(draws, Is.Zero);
        Assert.That(engine.NextFrameDelay, Is.EqualTo(Timeout.InfiniteTimeSpan));
    }

    [Test]
    public void SceneWithoutAnUpdateSettlesAutomatically()
    {
        using var engine = Create(new StaticScene());
        Run(engine, 2);
        Assert.That(engine.CurrentFrameActivity, Is.EqualTo(FrameActivity.Idle));
    }

    [Test]
    public void ReportedActivityWakesAtTargetAndSettlesThroughAmbient()
    {
        using var engine = Create(new QuietScene());
        var system = new ActivitySystem();
        engine.Systems.Add(system);
        Run(engine, 2);
        engine.Post(_ => system.FrameActivity = FrameActivity.Active);
        Assert.That(engine.Present(100), Is.True);
        Assert.That(Run(engine, 1), Is.EqualTo(60));
        engine.Post(_ => system.FrameActivity = FrameActivity.Idle);
        Run(engine, .1);
        Assert.That(engine.CurrentFrameActivity, Is.EqualTo(FrameActivity.Active));
        Run(engine, .2);
        Assert.That(engine.CurrentFrameActivity, Is.EqualTo(FrameActivity.Ambient));
        Run(engine, 1);
        Assert.That(engine.CurrentFrameActivity, Is.EqualTo(FrameActivity.Idle));
    }

    [Test]
    public void AmbientReducesDrawsWithoutChangingTheSimulationStep()
    {
        var scene = new QuietScene();
        using var engine = Create(scene);
        engine.FramePolicy.Activity = FrameActivity.Ambient;
        Run(engine, 2);
        int ticks = scene.Ticks;
        Assert.That(Run(engine, 1), Is.EqualTo(15));
        Assert.That(scene.Ticks - ticks, Is.EqualTo(120));
    }

    [TestCase(1)]
    [TestCase(30)]
    [TestCase(20)]
    public void BatteryCapLimitsActiveFramesButPreservesSimulation(int cap)
    {
        var scene = new QuietScene();
        using var engine = Create(scene);
        engine.FramePolicy.Activity = FrameActivity.Active;
        engine.FramePolicy.MaxFrameRate = cap;
        Run(engine, 1);
        int ticks = scene.Ticks;
        Assert.That(Run(engine, 1), Is.EqualTo(cap));
        Assert.That(scene.Ticks - ticks, Is.EqualTo(120));
    }

    [Test]
    public void PostWakesWithoutCatchingUpIdleTime()
    {
        var scene = new QuietScene();
        using var engine = Create(scene);
        Run(engine, 2);
        int before = scene.Ticks;
        bool ran = false;
        engine.Post(_ => ran = true);
        Assert.That(engine.NextFrameDelay, Is.EqualTo(TimeSpan.Zero));
        Assert.That(engine.Present(3600), Is.True);
        Assert.That(ran, Is.True);
        Assert.That(scene.Ticks - before, Is.EqualTo(1));
        Assert.That(engine.DroppedSimulationSeconds, Is.Zero);
    }

    [Test]
    public void PointerPressAndReleaseWakeAndArriveInOrder()
    {
        using var engine = Create(new QuietScene());
        engine.SizeChanged(100, 100);
        Run(engine, 2);
        var observed = new List<PointerEventType>();
        engine.InputManager.BindPointerAction(PointerEventType.Press, _ => observed.Add(PointerEventType.Press));
        engine.InputManager.BindPointerAction(PointerEventType.Release, _ => observed.Add(PointerEventType.Release));
        engine.InputManager.HandlePointerEvent(PointerEventType.Press, new PointerEvent(new Vec2(20, 20)));
        engine.InputManager.HandlePointerEvent(PointerEventType.Release, new PointerEvent(new Vec2(20, 20)));
        Assert.That(engine.Present(100), Is.True);
        Assert.That(observed, Is.EqualTo(new[] { PointerEventType.Press, PointerEventType.Release }));
    }

    [Test]
    public void TapBetweenFramesIsDeliveredThenReleased()
    {
        using var engine = Create(new QuietScene());
        var observed = new List<bool>();
        engine.InputManager.AddAction(GeKeys.Space, "tap");
        engine.InputManager.BindAction("tap", observed.Add);
        Run(engine, 2);
        observed.Clear();
        engine.InputManager.HandleKeyPress(GeKeys.Space);
        engine.InputManager.HandleKeyRelease(GeKeys.Space);
        engine.Present(100);
        Run(engine, .05);
        Assert.That(observed[0], Is.True);
        Assert.That(observed[1], Is.False);
    }

    [Test]
    public void HeldInputPreventsSleepUntilRelease()
    {
        using var engine = Create(new QuietScene());
        engine.InputManager.AddAction(GeKeys.Space, "held");
        engine.InputManager.HandleKeyPress(GeKeys.Space);
        Run(engine, 3);
        Assert.That(engine.CurrentFrameActivity, Is.EqualTo(FrameActivity.Active));
        engine.InputManager.HandleKeyRelease(GeKeys.Space);
        Run(engine, 2);
        Assert.That(engine.CurrentFrameActivity, Is.EqualTo(FrameActivity.Idle));
    }

    [Test]
    public void BackgroundDefersWorkAndFramesAndResumeDiscardsElapsedTime()
    {
        var scene = new QuietScene();
        using var engine = Create(scene);
        engine.FramePolicy.Activity = FrameActivity.Active;
        engine.SetHostState(HostState.Background);
        bool ran = false;
        engine.Post(_ => ran = true);
        int ticks = scene.Ticks;
        Assert.That(Run(engine, 60), Is.Zero);
        Assert.That(ran, Is.False);
        Assert.That(scene.Ticks, Is.EqualTo(ticks));
        engine.SetHostState(HostState.Active);
        engine.Present(3600);
        Assert.That(ran, Is.True);
        Assert.That(scene.Ticks - ticks, Is.EqualTo(1));
    }

    [Test]
    public void ExplicitIdleFreezesEffectsAndOneOffFramesStillWork()
    {
        using var effects = new RenderEffects();
        using var engine = Create(new QuietScene());
        engine.RenderOptions.Effects = effects;
        engine.FramePolicy.Activity = FrameActivity.Idle;
        Run(engine, 2);
        Assert.That(engine.CurrentFrameActivity, Is.EqualTo(FrameActivity.Idle));
        engine.RequestFrame();
        Assert.That(engine.Present(100), Is.True);
    }

    [Test]
    public void LiveParticlesAndContinuousEmittersKeepAutomaticPolicyActive()
    {
        using var engine = Create(new QuietScene());
        using var bitmap = new SKBitmap(2, 2);
        using var image = SKImage.FromBitmap(bitmap);
        var particles = engine.Systems.Get<ParticleSystem>();
        var emitter = particles.CreateEmitter(new ParticleOptions { Texture = image, Region = new(0, 0, 2, 2), Life = 3 });
        emitter.Burst(1);
        Run(engine, 2);
        Assert.That(engine.CurrentFrameActivity, Is.EqualTo(FrameActivity.Active));
        Run(engine, 3);
        Assert.That(engine.CurrentFrameActivity, Is.EqualTo(FrameActivity.Idle));
        engine.Post(_ => emitter.Rate = 1);
        Run(engine, 2);
        Assert.That(engine.CurrentFrameActivity, Is.EqualTo(FrameActivity.Active));
    }

    [Test]
    public void CameraEffectsKeepAutomaticPolicyActiveUntilTheyExpire()
    {
        using var engine = Create(new QuietScene());
        engine.CameraEffects.Shake(5, 2);
        Run(engine, 1.5);
        Assert.That(engine.CurrentFrameActivity, Is.EqualTo(FrameActivity.Active));
        Run(engine, 3);
        Assert.That(engine.CurrentFrameActivity, Is.EqualTo(FrameActivity.Idle));
    }

    [Test]
    public void TickIgnoresPolicyForDeterministicHeadlessConsumers()
    {
        var scene = new QuietScene();
        using var engine = Create(scene);
        Run(engine, 2);
        double before = scene.Seconds;
        engine.Tick(.75);
        Assert.That(scene.Seconds - before, Is.EqualTo(.75).Within(1e-9));
    }

    [Test]
    public void RepeatedOneOffRequestsCannotBypassTheCap()
    {
        using var engine = Create(new QuietScene());
        engine.FramePolicy.ActiveGraceSeconds = 0;
        engine.FramePolicy.IdleGraceSeconds = 0;
        engine.FramePolicy.MaxFrameRate = 30;
        int frames = 0;
        for (int i = 0; i < 120; i++)
        {
            engine.RequestFrame();
            if (engine.Present(1.0 / 120)) frames++;
        }
        Assert.That(frames, Is.LessThanOrEqualTo(30));
    }

    [Test]
    public void MovingAndAnimatedEntitiesKeepAutomaticPolicyActive()
    {
        using var engine = Create(new QuietScene());
        using var bitmap = new SKBitmap(4, 2);
        var entity = engine.EntityManager.CreateEntity("moving");
        var transform = new CTransform(Vec2.Zero, new Vec2(1, 0));
        entity.AddComponent(transform);
        entity.AddComponent(new CMovement(0, 10));
        engine.EntityManager.Update();
        Run(engine, 2);
        Assert.That(engine.CurrentFrameActivity, Is.EqualTo(FrameActivity.Active));
        transform.Velocity = Vec2.Zero;
        entity.AddComponent(new CAnimation(new Animation(bitmap, 2, 100)));
        Run(engine, 2);
        Assert.That(engine.CurrentFrameActivity, Is.EqualTo(FrameActivity.Active));
    }

    [Test]
    public void PresentationSchedulesPreserveTheFixedStepSequence()
    {
        static double RunSchedule(double[] schedule)
        {
            var scene = new QuietScene();
            using var engine = Create(scene);
            engine.FramePolicy.Activity = FrameActivity.Active;
            engine.FixedTimeStep = 1.0 / 128;
            double before = scene.Seconds;
            for (int i = 0; i < 64; i++) engine.Present(schedule[i % schedule.Length]);
            return scene.Seconds - before;
        }
        Assert.That(RunSchedule([1.0 / 64]), Is.EqualTo(1));
        Assert.That(RunSchedule([1.0 / 256, 8.0 / 256, 3.0 / 256, 4.0 / 256]), Is.EqualTo(1));
    }

    [Test]
    public void NativeIdleLoopWakesImmediatelyForPostedWork()
    {
        using var engine = Create(new QuietScene());
        engine.FramePolicy.ActiveGraceSeconds = engine.FramePolicy.IdleGraceSeconds = 0;
        using var painted = new ManualResetEventSlim();
        engine.InvalidateAction = () => painted.Set();
        engine.Start();
        engine.RequestFrame();
        Assert.That(painted.Wait(TimeSpan.FromSeconds(2)), Is.True);
        using var ran = new ManualResetEventSlim();
        engine.Post(_ => ran.Set());
        Assert.That(ran.Wait(TimeSpan.FromSeconds(2)), Is.True);
    }

    [Test]
    public void SettlingPublishesTheLastChangedStateBeforeSleeping()
    {
        using var engine = Create(new QuietScene());
        engine.FramePolicy.ActiveGraceSeconds = engine.FramePolicy.IdleGraceSeconds = 0;
        Run(engine, 2);
        int frames = 0;
        engine.InvalidateAction = () => frames++;
        engine.RequestFrame();
        engine.Present(0);
        Run(engine, .1);
        Assert.That(frames, Is.EqualTo(1));
        Assert.That(engine.NextFrameDelay, Is.EqualTo(Timeout.InfiniteTimeSpan));
    }

    [Test]
    public void ActivityCallbackFailureFaultsTheEngineAndRetainsItsLastSnapshot()
    {
        using var engine = Create(new QuietScene());
        var snapshot = engine.GetRenderSnapshot();
        engine.Systems.Add(new FailingActivitySystem());
        Assert.That(engine.Present(1.0 / 60), Is.False);
        Assert.That(engine.IsFaulted, Is.True);
        Assert.That(engine.Fault!.Operation, Is.EqualTo("frame activity"));
        Assert.That(engine.GetRenderSnapshot(), Is.SameAs(snapshot));
        Assert.That(engine.NextFrameDelay, Is.EqualTo(Timeout.InfiniteTimeSpan));
    }

    [Test]
    public void RateAndGraceValidationRejectsInvalidValues()
    {
        using var engine = new Engine(audioEnabled: false);
        Assert.Throws<ArgumentOutOfRangeException>(() => engine.TargetFrameRate = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => engine.FramePolicy.MaxFrameRate = -1);
        Assert.Throws<ArgumentOutOfRangeException>(() => engine.FramePolicy.AmbientFrameRate = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => engine.FramePolicy.ActiveGraceSeconds = double.NaN);
        Assert.Throws<ArgumentOutOfRangeException>(() => engine.FramePolicy.IdleGraceSeconds = -1);
        Assert.Throws<ArgumentOutOfRangeException>(() => engine.FramePolicy.Activity = (FrameActivity)99);
        Assert.Throws<ArgumentOutOfRangeException>(() => engine.Present(double.PositiveInfinity));
    }
}
