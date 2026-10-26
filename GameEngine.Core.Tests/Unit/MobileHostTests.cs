using GameEngine.Core.Systems;
using static GameEngine.Core.Pointer;

namespace GameEngine.Core.Tests.Unit;

public class MobileHostTests
{
    [Test]
    public void LifecycleIsOrderedIdempotentAndDoesNotResumeAGamePause()
    {
        using var engine = new Engine(audioEnabled: false);
        var events = new List<LifecycleEvent>();
        engine.LifecycleChanged += events.Add;
        engine.SetRunning(false);
        engine.SetHostState(HostState.Inactive);
        engine.SetHostState(HostState.Background);
        engine.SetHostState(HostState.Background);
        Assert.That(engine.Tick(10), Is.False);
        engine.SetHostState(HostState.Inactive);
        engine.SetHostState(HostState.Active);
        engine.SetHostState(HostState.Active);
        Assert.Multiple(() =>
        {
            Assert.That(events, Is.EqualTo(new[] { LifecycleEvent.Pause, LifecycleEvent.Background, LifecycleEvent.Foreground, LifecycleEvent.Resume }));
            Assert.That(engine.IsRunning, Is.False);
            Assert.That(engine.Tick(0), Is.False);
        });
    }

    [Test]
    public void BackgroundCheckpointWaitsForTheCurrentSimulationStep()
    {
        using var engine = new Engine(audioEnabled: false);
        using var entered = new ManualResetEventSlim();
        using var finish = new ManualResetEventSlim();
        var scene = new BlockingScene(entered, finish);
        engine.ChangeScene(scene);
        engine.Tick(0);
        engine.NotifyFirstPresent();
        var tick = Task.Run(() => engine.Tick(0.01));
        Assert.That(entered.Wait(TimeSpan.FromSeconds(5)), Is.True);
        bool checkpoint = false;
        engine.LifecycleChanged += e => { if (e == LifecycleEvent.Background) checkpoint = scene.Finished; };
        var background = Task.Run(() => engine.SetHostState(HostState.Background));
        try { Assert.That(background.Wait(50), Is.False); }
        finally { finish.Set(); }
        Assert.That(Task.WaitAll([tick, background], TimeSpan.FromSeconds(5)), Is.True);
        Assert.That(checkpoint, Is.True);
    }

    private sealed class BlockingScene(ManualResetEventSlim entered, ManualResetEventSlim finish) : Scene
    {
        public bool Finished;
        public override void Initialize(EntityManager entities, InputManager input, AudioSystem? audio, Action<Scene?> reset) { }
        public override void Update(EntityManager entities, SystemContainer systems, double deltaSeconds)
        {
            entered.Set();
            finish.Wait(TimeSpan.FromSeconds(5));
            Finished = true;
        }
    }

    [Test]
    public void BackgroundCancelsEveryContactAndReleasesKeysBeforeCheckpoint()
    {
        using var engine = new Engine(audioEnabled: false);
        var input = engine.InputManager;
        input.RealResolution = input.VirtualResolution = new Vec2(100, 100);
        input.AddAction(GeKeys.D, "move");
        input.HandleKeyPress(GeKeys.D);
        var cancelled = new List<long>();
        input.BindPointerAction(PointerEventType.Cancel, e => cancelled.Add(e.ContactId));
        input.HandlePointerEvent(PointerEventType.Press, new PointerEvent(new Vec2(20, 20), 1));
        input.HandlePointerEvent(PointerEventType.Press, new PointerEvent(new Vec2(30, 30), 2));
        engine.SetHostState(HostState.Background);
        Assert.Multiple(() =>
        {
            Assert.That(cancelled, Is.EqualTo(new long[] { 1, 2 }));
            Assert.That(input.Contacts, Is.Empty);
            Assert.That(input.IsActionActive("move"), Is.False);
        });
    }

    [Test]
    public void SleepingLoopWakesForWorkResumeAndStop()
    {
        var engine = new Engine(audioEnabled: false);
        engine.SetRunning(false);
        engine.Start();
        using var ran = new ManualResetEventSlim();
        engine.Post(_ => ran.Set());
        Assert.That(ran.Wait(TimeSpan.FromSeconds(5)), Is.True);
        engine.SetHostState(HostState.Background);
        engine.SetHostState(HostState.Active);
        engine.SetRunning(true);
        var stop = Task.Run(engine.Stop);
        Assert.That(stop.Wait(TimeSpan.FromSeconds(5)), Is.True);
        engine.Dispose();
    }

    [Test]
    public void BackCanBeHandledOtherwiseItPausesThenAllowsPlatformNavigation()
    {
        using var engine = new Engine(audioEnabled: false);
        EventHandler<BackRequestEventArgs> closePanel = (_, e) => e.Handled = true;
        engine.BackRequested += closePanel;
        Assert.That(engine.RequestBack(), Is.True);
        Assert.That(engine.IsRunning, Is.True);
        engine.BackRequested -= closePanel;
        Assert.That(engine.RequestBack(), Is.True);
        Assert.That(engine.IsRunning, Is.False);
        Assert.That(engine.RequestBack(), Is.False);
    }

    [Test]
    public void ACheckpointFailureFaultsTheEngineAndStillReleasesForegroundServices()
    {
        var backend = new Backend();
        using var engine = new Engine(null, false, null, new HostServices(backend, backend));
        engine.HostServices.KeepAwakeEnabled = true;
        engine.LifecycleChanged += _ => throw new IOException("Save failed.");
        Assert.DoesNotThrow(() => engine.SetHostState(HostState.Background));
        Assert.That(engine.IsFaulted, Is.True);
        Assert.That(engine.Fault!.Operation, Is.EqualTo("host lifecycle"));
        Assert.That(backend.IsActive, Is.False);
    }

    private sealed class Backend : IKeepAwakeBackend, IHapticsBackend
    {
        public bool IsSupported => true;
        public bool IsActive { get; private set; }
        public string? UnavailableReason => null;
        public int Cues;
        public bool Enabled { get; set; }
        public void SetForeground(bool foreground) { }
        public void SetActive(bool active) => IsActive = active;
        public bool Play(HapticCue cue) { Cues++; return true; }
    }

    [Test]
    public void OptionalServicesHonorSettingsPauseBackgroundDetachAndDisposal()
    {
        var backend = new Backend();
        var services = new HostServices(backend, backend);
        using var engine = new Engine(null, false, null, services);
        Assert.That(backend.IsActive, Is.False);
        Assert.That(services.PlayHaptic(HapticCue.Placement), Is.False);
        services.KeepAwakeEnabled = services.HapticsEnabled = true;
        Assert.That(services.PlayHaptic(HapticCue.Reward), Is.True);
        Assert.That(backend.IsActive, Is.True);
        engine.SetHostState(HostState.Background);
        Assert.That(backend.IsActive, Is.False);
        Assert.That(services.PlayHaptic(HapticCue.Rejection), Is.False);
        engine.SetHostState(HostState.Active);
        Assert.That(backend.IsActive, Is.True);
        engine.SetRunning(false);
        Assert.That(backend.IsActive, Is.False);
        engine.SetRunning(true);
        engine.SetHostAttached(false);
        Assert.That(backend.IsActive, Is.False);
        engine.SetHostAttached(true);
        Assert.That(backend.IsActive, Is.True);
        engine.Stop();
        engine.SetRunning(true);
        engine.SetHostAttached(true);
        Assert.That(backend.IsActive, Is.False);
        engine.Dispose();
        Assert.That(backend.IsActive, Is.False);
        Assert.That(services.PlayHaptic(HapticCue.Reward), Is.False);
        Assert.That(backend.Cues, Is.EqualTo(1));
    }

    [Test]
    public void UnsupportedServicesAreDiagnosable()
    {
        using var services = new HostServices();
        services.HapticsEnabled = services.KeepAwakeEnabled = true;
        Assert.Multiple(() =>
        {
            Assert.That(services.HapticsSupported, Is.False);
            Assert.That(services.HapticsUnavailableReason, Is.Not.Empty);
            Assert.That(services.KeepAwakeSupported, Is.False);
            Assert.That(services.KeepAwakeUnavailableReason, Is.Not.Empty);
            Assert.That(services.PlayHaptic(HapticCue.Placement), Is.False);
        });
    }
}
