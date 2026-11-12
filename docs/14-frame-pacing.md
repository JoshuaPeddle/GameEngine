# Demand-driven frame pacing (GE-161, GE-162)

The live engine offers frames only while work needs presentation. A settled engine blocks until
input, posted work, a scene change, a resize or an explicit frame request wakes it. Core owns this
policy, so Avalonia, WinForms and the browser share the same simulation and pacing rules.

## Model and defaults

`FrameActivity` has three values. Active uses `TargetFrameRate`, Ambient uses the smaller of that
rate and `FramePolicy.AmbientFrameRate` (15 by default), and Idle performs no simulation updates,
snapshot copies or engine-requested draws. The names describe activity rather than device or visual
quality. A single nullable override handles game state; a separate cap handles the user's battery
preference. Games need neither platform checks nor their own timer loop.

Without an activity source, the engine remains Active for `ActiveGraceSeconds` (0.2), then Ambient
for `IdleGraceSeconds` (0.8), then sleeps. Persistent Ambient activity stays Ambient after the Active
grace. New activity raises the rate immediately. Requests reset the grace period. Set both grace
periods to zero for a strictly one-frame response to each request. A final dirty snapshot is offered
before sleeping, including when the grace periods are zero. Presentation always respects the cap;
initial scene publication can paint immediately so a new view never starts blank.

`TargetFrameRate` retains its role as the Active presentation rate. Null leaves the explicit target
unset; the engine offers at most one frame per simulation step because an offer needs fresh state.
An active unlimited engine therefore sleeps between simulation opportunities instead of spinning.
Mobile view defaults remain 60 Hz simulation and presentation. Borrowed engines keep their owner's
settings. A view can still repaint its last snapshot when its parent or the operating system asks
it to paint; Idle eliminates engine-generated requests, not exposure or layout paints.

## Automatic activity

The policy takes the highest activity reported by these sources:

- Mapped held keys, queued pointer input and live pointer contacts keep input Active. A mapped key
  press and release between frames reaches bound actions as a press followed by a release. Pointer
  press, release and cancel retain their existing ordered delivery.
- Moving `CMovement` bodies, nonzero gravity and visible multiframe sprite animations are Active.
  A single-frame sprite is static. Velocity must reach zero before a moving body can settle.
- Living particles and enabled continuous emitters with a positive rate are Active. Detection does
  not allocate the lazy particle store. Disabled emitters let their living particles expire first.
- Camera shake, zoom punch and hit-stop are Active until their timers expire. Starting a camera
  effect requests a frame. A render-effects pipeline defaults to Active because shaders and opaque
  draw callbacks can animate without any ECS motion. Set `RenderEffects.FrameActivity` to Ambient
  for continuous water or aurora, or Idle for a static pipeline.
- A custom `ISystem` can implement `IFrameActivitySource`. Return Active while a tween, timer or
  transition runs, Ambient for subtle perpetual motion, and Idle when settled. Unknown custom
  systems remain Active for compatibility.
- A scene without an overridden `Update` defaults to Idle. A custom scene update defaults to
  Active, preserving existing spawning, timers, game rules and saves even without visible motion.
  This override detection is cached per scene. Override `Scene.FrameActivity` to describe its work.

The engine cannot infer arbitrary scene fields, custom shader uniforms, raw sprite-batch span
writes, timers hidden in callbacks or mutations from another thread. Perform game mutations through
`Post` while running. From within a scene update, ordinary mutations are included in that update's
snapshot. There is no separate tween service in Core; a game's tween system reports through the same
small activity interface. Audio playback alone does not require a visual frame.

```csharp
public override FrameActivity FrameActivity =>
    waveRunning || transitionRunning ? FrameActivity.Active : FrameActivity.Idle;
```

This scene declaration still allows particles, camera response and systems to finish at Active.
For an intentionally frozen board behind a modal, use the engine override instead:

```csharp
engine.Post(e => e.FramePolicy.Activity = FrameActivity.Idle);
engine.Post(e => e.FramePolicy.Activity = null);
engine.RequestFrame();
```

`FramePolicy.Activity` replaces automatic detection, including particles and effects. Idle freezes
those effects after the grace period. Input and `Post` still wake the engine, execute work and request
presentation. Use `SetRunning(false)` for a game pause that should reject simulation entirely;
posted work still drains while paused, as before. Host background/inactive states additionally defer
posted work and input delivery until activation. Returning to the foreground preserves a game pause.

## Battery saver and custom hosts

```csharp
engine.Post(e => e.FramePolicy.MaxFrameRate = batterySaver ? 30 : null);
```

The cap applies to Active, Ambient and one-off requests. Keep `FixedTimeStep` unchanged for a
presentation-only saver. Lowering a presentation cap does not lower fixed simulation frequency.
When a presentation interval exceeds the catch-up budget, the live scheduler wakes between draws
to advance simulation within that budget. Real stalls still use `MaxCatchUpSteps` and discard excess
time under the existing accumulator contract.

`Engine.Tick(seconds)` remains the exact simulation primitive. It ignores pacing, never splits its
argument and retains headless replay behavior. Existing Demo harness tests continue through that
path. `Engine.Present(elapsedSeconds)` is the live presentation primitive and the headless pacing
harness: it pumps work, advances whole fixed steps, copies one snapshot when a presentation is due,
invokes `InvalidateAction` only for a produced frame, and returns whether it offered one.
`NextFrameDelay` gives a custom host its next blocking wait, including `Timeout.InfiniteTimeSpan`
while idle. Do not poll an idle engine. `RequestFrame` and `Post` are safe wake signals; configure
policy and game state before starting or on the engine thread through `Post` afterward.

Idle and host-suspended time never enters the accumulator. Waking runs one fixed step to deliver
input and apply pending changes, then resumes normal elapsed-time accounting. It never simulates
the hours spent idle. Games with offline production must use their own monotonic or persisted clock.

Native hosts block on a semaphore's wait handle and consume its signal without a timed spin. The
browser asynchronously awaits that semaphore with the same calculated interval. The old unbounded
`Thread.Yield` path and browser `Task.Delay(1)` polling are gone. Neither path depends on a display
callback to discover input or posted work. Browser scheduling remains timer based rather than
requestAnimationFrame based, so this is a cap on offers, not display synchronization.

## Cheaper frames

A live frame can execute several fixed steps but builds only its final snapshot. Direct `Tick`
continues publishing after each call. Existing three-buffer ownership and held-reader protection
are unchanged. No UI code reads live entity state.

`RenderEffects.CacheBackground = true` retains the composed `Background` and `BackgroundLayers`
as an image. This is opt-in because callbacks and shaders may use time or external mutable data.
The cache automatically rebuilds for virtual resolution, camera position, background scale and
GPU context changes. Call `InvalidateBackground()` after changing captured background data, then
`engine.RequestFrame()` if there is no running update. Disabling the cache restores the live path;
re-enabling rebuilds the cached image. The effects pipeline owns the
retained surface and image and releases them on disposal. It borrows shaders and follows the existing
paint-lock replacement and disposal contract. It costs roughly four bytes per virtual pixel plus
backend surface/image overhead. Static backgrounds can be cached while foreground layers animate.
Animated aurora, water and weather must keep their live path unless deliberately frozen.

`GameView` retains its first-presentation and dispatcher delegates. It queues a visual invalidation
only after the engine offers a frame and coalesces outstanding requests. `CustomDrawOp` remains a
fresh immutable operation for each offer because Avalonia can retain old operations for later paint.
Pooling or mutating one operation would break that lifetime boundary. Its scene draw still acquires
the existing Skia lease once and reads a held render snapshot.

## Reproduction and measurements

Measurements use .NET 10.0.10 Arm64, SDK 10.0.302, macOS 26.6.2 and SkiaSharp 4.151.1. The workload
has 500 ordinary immutable shared-texture sprites, 1000 static background circles and a 360 by 640
CPU raster surface. Active additionally moves the 500 sprites in its scene update. Ambient retains
subtle-motion demand with the same rendering load. Idle requests no ongoing work. Each case starts
a real `Engine.Start` loop, warms for 1.5 seconds and samples for three seconds. The callback paints
on the engine thread to include the full CPU raster path without a window. Process CPU milliseconds
divided by measured wall seconds includes engine, rendering, runtime, GC and scheduling overhead.
It is distinct from elapsed draw duration and is not a phone power or GPU measurement.

Run the current workload and the same workload on the original commit:

```sh
dotnet run -c Release --project GameEngine.Benchmarks -- --frame-pacing
python3 tools/benchmark-frame-pacing-baseline.py --ref 833cddd
```

The baseline script archives that commit to a temporary directory and installs the current workload,
removing only the new API annotations, cache setting and paced-frame microbenchmark. It does not
change either checkout. Both versions use the actual native loop, 60 fps target and 120 Hz fixed
step. The after version declares each scene's activity and enables the static cache. This compares
the complete change, not just a cap or a cache in isolation. Raw repeated runs live in
[`benchmarks/2026-11-12`](benchmarks/2026-11-12).

Medians of three separate process runs:

| Scene | Before CPU ms/wall s | After CPU ms/wall s | Before offers/s | After offers/s | After ticks/s |
|---|---:|---:|---:|---:|---:|
| Idle | 281.524 | 0.326 | 55.232 | 0.000 | 0.000 |
| Ambient | 254.207 | 36.063 | 55.148 | 14.972 | 119.595 |
| Active | 306.987 | 176.193 | 55.477 | 60.207 | 119.748 |

For this workload, 1000 CPU ms/wall s represents one fully occupied logical core. The runtime
background overhead in the settled idle measurement is not a simulation tick or a draw. Ambient
retains the 120 Hz simulation while reducing raster work. Active retains its target presentation
rate and benefits from the static background cache.

| Stage | Before mean us/op | After mean us/op | Before / after managed B/op |
|---|---:|---:|---:|
| tick_including_snapshot | 41.292 | 40.286 | 0 / 0 |
| snapshot | 21.702 | 20.832 | 0 / 0 |
| draw_including_acquire | 1208.308 | 149.352 | 0 / 0 |
| two_steps_and_draw | 1530.443 | 452.987 | 0 / 0 |
| paced_frame, final snapshot only | unavailable | 491.051 | unavailable / 0 |

The Core stage profile uses 200 warmup operations and 1000 measured operations, with a per-thread
managed allocation counter. Snapshot, shared-texture drawing and complete frames allocate zero
managed bytes after warmup in both versions. Reduced copying and cached raster work are time wins,
not claimed allocation reductions. New scenes and larger high-water snapshot capacities still
allocate during warmup.

The separate `GameView` offer-construction profile uses a headless offscreen Avalonia drawing
context, 100 warmups and 1000 offers. It measured 328 to 264 B/offer and 0.141 to 0.128 us/offer.
These numbers include command recording, exclude actual scene raster and UI dispatcher costs, and
are not a display frame budget. The cached first-presentation delegate removes 64 B/offer; the
cached dispatcher delegate also removes its former 64-byte delegate per queued invalidation.
The retained operation and framework command storage remain. Run it explicitly with:

```sh
dotnet test GameEngine.Runner.Avalonia.Tests -c Release --filter ProfileFrameOfferConstruction --logger 'console;verbosity=normal'
```

## Tower Idle adoption

Tower Idle was inspected read-only, specifically `GameHost.cs`, `GamePage.cs`, `IslandScene.cs`,
`GameRuntime.cs` and `SailingTransition.cs`. No consuming-game files were modified. Its existing
`TargetFrameRate = 60` and `FixedTimeStep = 1.0 / 60` remain appropriate.

1. Add an engine-thread-owned modal flag to `GameHost`. From `GamePage.Open`, `Close`, and the
   first `Refresh` that opens a return/tower panel, post changes to that flag only when its value
   changes. A modal does not itself pause Tower Idle combat today. Freeze only if combat is already
   paused or no wave is running; preserve Active during a running wave unless the game deliberately
   changes its rules to pause on modal opening.
2. Centralize a `RefreshFramePolicy` method on the engine thread. Call it after `Send`, `Publish`,
   resume, modal changes, and sailing completion. Choose Active while `Sailing.IsActive` or
   `Status.Active && !Status.Paused` or a visible battle transition is finishing. Choose Idle for
   a paused board or static return/modal with no combat running. Between waves, choose Ambient
   when aurora/weather is wanted, and Idle when atmosphere is disabled or the board is obscured.
   Assign this to `Engine.FramePolicy.Activity`; use the game override because opaque effects
   otherwise default Active. `Publish` runs only every 0.15 simulation seconds, so do not rely on
   a future publish to leave Idle. The posted game command must update policy itself.
3. Preserve the policy flag across `StatusChanged` callbacks. `IslandScene.Update` currently sets
   `beaconAura.Rate = 3` whenever the hull is alive, and the pipeline has aurora, weather and sailing
   callbacks. Automatic detection alone intentionally keeps those effects Active. The explicit
   board state overrides them while sleeping. For automatic scene-level control instead, disable
   decorative emission when settled and declare decorative effects Ambient; live particles still
   demand Active until they expire. Custom battle-effect tails and sailing must report Active
   explicitly because Core cannot inspect their private timers.
4. Add a persisted battery saver toggle to `GameHost`'s visual settings and `GamePage`'s settings
   panel. Its posted handler sets `FramePolicy.MaxFrameRate` to 30 or null. Keep the 60 Hz simulation
   timestep, battle accumulator, speed setting and virtual dimensions unchanged.
5. Keep `GameRuntime.Checkpoint` on commands, suspend and disposal. Its foreground settlement uses
   a monotonic clock, but its ten-second periodic checkpoint and `IslandScene` status publication
   currently depend on simulation updates. If the UI must refresh research/production while the
   board sleeps, use a coarse host timer to post `Game.Checkpoint()`, then publish `Game.Status()`.
   That one-off work should run only as frequently as the visible UI requires and stop in background.
   Never feed elapsed idle wall time into `Engine.Tick`. Avoid posting unchanged policy values from
   the existing 120 ms `GamePage.Refresh` timer, which would continuously wake the engine.
6. Cache only static captured environment/map art. Keep `CacheBackground` false for animated aurora
   or animated `DrawEnvironment`; pre-render static environment art into a shared image or deliberately
   freeze and invalidate it when the map, visual quality or captured art changes. `SetBloom` changes
   scale and the scale change invalidates the cache automatically, but a map-art change still needs
   explicit invalidation. Freeze time-driven foreground decoration with the Idle state.
7. Recheck Note10+ with the static Welcome back panel, a paused board, between-wave atmosphere,
   sailing, active combat, the saver toggle, immediate resume/tap, and home/lock transitions.
   Record process and UI-thread CPU, actual presentations and thermal behavior. The headless desktop
   measurements establish engine behavior and cost boundaries; they do not substitute for that run.

## Verification and limits

The final Release solution configuration built with zero warnings and zero errors. The regular
repository suites passed 732 tests: 409 Core, 249 Demo, 58 Editor and 16 Avalonia host tests. The
explicit offer profiler passed separately. A copied template passed all eight tests against the
current Core project; package placeholders were replaced only in the temporary verification copy.
Core also built with `EnableTrimAnalyzer=true` and zero warnings. A temporary self-contained trimmed
macOS executable verified that a static scene settles and an overridden scene update remains Active.
The `Scene` type preserves public method metadata so this detection survives trimming. This is a
trimmed managed smoke check, not a new NativeAOT or iOS device result.

Commands used the sandbox-compatible NuGet caches at `/tmp/ge-nuget` and `/tmp/ge-nuget-http`,
`-p:UsedAvaloniaProducts=` to disable the external telemetry write, the installed JDK 21 at
`/tmp/gameengine-ui-jdk21/Contents/Home`, Android SDK at `/Users/josh/Library/Android/sdk`, and a
temporary Android debug keystore. These are environment accommodations, not repository settings.

```sh
dotnet build GameEngine.sln -c Release -p:UsedAvaloniaProducts= -p:JavaSdkDirectory=/tmp/gameengine-ui-jdk21/Contents/Home -p:AndroidSdkDirectory=/Users/josh/Library/Android/sdk -p:AndroidDebugKeyStore=/tmp/ge-pacing-debug.keystore
dotnet test -c Release -p:UsedAvaloniaProducts= -p:JavaSdkDirectory=/tmp/gameengine-ui-jdk21/Contents/Home -p:AndroidSdkDirectory=/Users/josh/Library/Android/sdk -p:AndroidDebugKeyStore=/tmp/ge-pacing-debug.keystore
dotnet build GameEngine.Core -c Release -p:EnableTrimAnalyzer=true
```

The existing deterministic, seeded particle, capture, fixed-step, input, lifecycle and Demo tests
remain unchanged and pass. Added tests cover an hour of idle time with zero ticks and draws,
automatic static scenes, target-rate activity, Ambient settling, 30/20/1 fps caps without changing
simulation steps, capped one-off requests, pointer order, short key taps, held input, particle
lifetimes and continuous emitters, camera effects, background deferral, wake without catch-up,
final dirty publication, native posted-work wake, activity-callback fault isolation and equivalent fixed-step schedules. Cache tests
check pixels, transparency, explicit invalidation, resolution, scale and camera changes. The template
uses the existing scene harness to prove idle then immediate movement on input.

No physical-device, real browser presentation or GPU timing claim is made. Unknown custom logic
stays Active until it declares activity, an explicit Idle override freezes simulation-owned timers,
and static caching is an ownership contract for captured drawing data. These are deliberate limits
rather than attempts to infer arbitrary game semantics.
