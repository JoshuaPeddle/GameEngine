using GameEngine.Core.Components;
using GameEngine.Core.Systems;

namespace GameEngine.Core;

public partial class Engine
{
    public FramePolicy FramePolicy { get; }
    public FrameActivity CurrentFrameActivity { get; private set; } = FrameActivity.Active;
    private int _frameRequested = 1;
    private double _quietSeconds;
    private double _presentationElapsed;
    private bool _suspendedPresentation = true;
    private bool _presenting;
    private bool _wakeStepPending;
    private bool _hasPresented;
    private bool _snapshotDirty;

    public void RequestFrame()
    {
        Interlocked.Exchange(ref _frameRequested, 1);
        WakeLoop();
    }

    public TimeSpan NextFrameDelay
    {
        get
        {
            lock (_frameLock)
            {
                if (_stopped || _hostState != HostState.Active || !_isRunning || _pauseUntilFirstPresent || IsFaulted)
                    return Timeout.InfiniteTimeSpan;
                if (Volatile.Read(ref _frameRequested) != 0) return TimeSpan.Zero;
                if (CurrentFrameActivity == FrameActivity.Idle && !_wakeStepPending && !_snapshotDirty) return Timeout.InfiniteTimeSpan;
                return TimeSpan.FromSeconds(Math.Max(.001, Math.Min(FrameInterval - _presentationElapsed, FixedTimeStep * MaxCatchUpSteps / 2)));
            }
        }
    }

    private double FrameInterval
    {
        get
        {
            double interval = TargetFrameRate is { } rate ? 1.0 / rate : FixedTimeStep;
            if (CurrentFrameActivity == FrameActivity.Ambient) interval = Math.Max(interval, 1.0 / FramePolicy.AmbientFrameRate);
            if (FramePolicy.MaxFrameRate is { } cap) interval = Math.Max(interval, 1.0 / cap);
            return interval;
        }
    }

    public bool Present(double elapsedSeconds)
    {
        if (!double.IsFinite(elapsedSeconds) || elapsedSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        lock (_frameLock)
        {
            if (_stopped || _hostState != HostState.Active)
            {
                _suspendedPresentation = true;
                return false;
            }
            bool requested = Interlocked.Exchange(ref _frameRequested, 0) != 0;
            bool wasIdle = CurrentFrameActivity == FrameActivity.Idle;
            bool waking = _suspendedPresentation || (wasIdle && requested);
            _presentationElapsed += elapsedSeconds;
            if ((_suspendedPresentation || wasIdle) && !requested && !_wakeStepPending && !_snapshotDirty && Volatile.Read(ref _queuedWorkCount) == 0) return false;
            PumpPendingWork();
            if (!_isRunning || _pauseUntilFirstPresent || IsFaulted)
            {
                _suspendedPresentation = true;
                return false;
            }
            double simulationSeconds = elapsedSeconds;
            if (waking && !_wakeStepPending)
            {
                elapsedSeconds = 0;
                _accumulator.Reset();
                simulationSeconds = FixedTimeStep;
                _wakeStepPending = true;
                _presentationElapsed = _hasPresented ? Math.Min(_presentationElapsed, FrameInterval) : FrameInterval;
            }
            _wakeStepPending |= requested;
            _suspendedPresentation = false;
            _quietSeconds = requested ? 0 : _quietSeconds + elapsedSeconds;
            FrameActivity activity;
            try { activity = FramePolicy.Activity ?? DetectActivity(); }
            catch (Exception exception)
            {
                Fail("frame activity", null, exception);
                return false;
            }
            if (activity == FrameActivity.Active) _quietSeconds = 0;
            CurrentFrameActivity = activity == FrameActivity.Active || _quietSeconds < FramePolicy.ActiveGraceSeconds
                ? FrameActivity.Active
                : activity == FrameActivity.Ambient || _quietSeconds < FramePolicy.ActiveGraceSeconds + FramePolicy.IdleGraceSeconds
                    ? FrameActivity.Ambient : FrameActivity.Idle;
            if (CurrentFrameActivity == FrameActivity.Idle && !requested && !_wakeStepPending)
            {
                _accumulator.Reset();
                return OfferFrame();
            }
            int steps = _accumulator.Advance(simulationSeconds);
            if (steps > 0) _wakeStepPending = false;
            _presenting = true;
            try
            {
                for (int step = 0; step < steps; step++)
                    if (!Tick(FixedTimeStep) || IsFaulted) break;
            }
            finally { _presenting = false; }
            _snapshotDirty |= steps > 0;
            return OfferFrame();
        }
    }

    private bool OfferFrame()
    {
        if (!_snapshotDirty || IsFaulted || _presentationElapsed + 1e-9 < FrameInterval) return false;
        _presentationElapsed = Math.Clamp(_presentationElapsed - FrameInterval, 0, FrameInterval * .99);
        _hasPresented = true;
        _snapshotDirty = false;
        PublishRenderSnapshot();
        InvalidateAction?.Invoke();
        return true;
    }

    private FrameActivity DetectActivity()
    {
        if (InputManager.HasActivity || CameraEffects.IsActive) return FrameActivity.Active;
        FrameActivity activity = currentScene?.FrameActivity ?? FrameActivity.Idle;
        if (activity == FrameActivity.Active) return activity;
        var systems = Systems.Systems;
        for (int i = 0; i < systems.Count; i++)
        {
            if (systems[i] is IFrameActivitySource source)
                activity = (FrameActivity)Math.Max((int)activity, (int)source.FrameActivity);
            else if (systems[i] is not (InputSystem or MovementSystem or PhysicsSystem or AnimationSystem or RenderSystem or AudioSystem))
                return FrameActivity.Active;
        }
        if (RenderOptions.Effects is { } effects)
            activity = (FrameActivity)Math.Max((int)activity, (int)effects.FrameActivity);
        if (activity == FrameActivity.Active) return activity;
        var movers = EntityManager.GetEntitiesWithComponents<CMovement, CTransform>();
        for (int i = 0; i < movers.Count; i++)
            if (movers[i].Item3.Velocity.LengthSquared() > 0) return FrameActivity.Active;
        var gravity = EntityManager.GetEntitiesWithComponents<CGravity, CTransform>();
        for (int i = 0; i < gravity.Count; i++)
            if (gravity[i].Item2.Acceleration != 0) return FrameActivity.Active;
        var animations = EntityManager.GetEntitiesWithComponent<CAnimation>();
        for (int i = 0; i < animations.Count; i++)
        {
            var animation = animations[i].GetComponent<CAnimation>();
            if (animation.ShouldDraw && animation.Animation.frames > 1 && animation.Animation.delay > 0)
                return FrameActivity.Active;
        }
        return activity;
    }
}
