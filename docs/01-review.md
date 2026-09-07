# GameEngine — Full Review

**Date:** 2026-07-31
**Baseline commit:** `df48b9f`
**Scope:** Core, runners (Avalonia / WinForms / MAUI / Browser / iOS / Android), Editor, level system, demos, tests, build, CI

Everything marked **confirmed** below was reproduced against `GameEngine.Core`
with a throwaway harness, not inferred from reading. Harness source and raw
output: [`04-evidence.md`](04-evidence.md).

Finding IDs (`GE-nn`) cross-reference [`02-findings.md`](02-findings.md).

---

## Contents

1. [What's genuinely good](#whats-genuinely-good)
2. [Confirmed bugs](#confirmed-bugs)
3. [Performance](#performance)
4. [Architecture](#architecture)
5. [Level system](#level-system)
6. [Editor](#editor)
7. [Runners](#runners)
8. [Tests, build, CI](#tests-build-ci)
9. [Code quality](#code-quality)
10. [On the pre-existing docs](#on-the-pre-existing-docs)

---

## What's genuinely good

These are real strengths, not padding — they're the parts worth preserving as
everything else gets reworked.

- **`RenderSnapshot` and the three-buffer pool are the best code in the repo.**
  Value-copied component data, `ReadOnlySpan<Entry>` exposure, reader-count
  tracking, the lock held only across reference swaps, and a pool sized with a
  written argument for *why three buffers suffice*. That is a correct solution to
  a genuinely hard problem, and the comments explain reasoning rather than
  restating code.
- **`PublishRenderSnapshot` fills outside the lock.** Easy to get wrong. It's
  right, and the safety argument is documented.
- **The scene-change handoff** — `Interlocked.Exchange` of `_pendingScene`,
  `_pauseUntilFirstPresent`, and resetting the delta baseline in
  `NotifyFirstPresent` — shows deliberate thought about the first-frame `dt`
  spike. Most hobby engines never address this.
- **Frame-rate-independent deceleration** in `MovementSystem` via
  `Math.Pow(DecelerationBase, deltaSeconds)`. Correct; the naive per-frame
  multiply is near-universal elsewhere.
- **Invalidation coalescing** via `Interlocked.Exchange(ref _invalidationsPending, 1)`,
  implemented consistently in every runner.
- Central package management, four real platform targets, and an editor with live
  Roslyn compilation into a collectible `AssemblyLoadContext`.

The instincts are good. Nearly every problem below sits in the layers *beneath*
the parts that have already been optimised.

---

## Confirmed bugs

### GE-01 — Entity IDs collide after any removal

`GameEngine.Core/EntityManager.cs:66`

```csharp
Entity entity = new(entities.Count + entitiesToAdd.Count, tag, this);
```

Count-derived IDs are reused as soon as anything despawns. Confirmed:

```
ids after create: 0,1,2          (a, b, c)
remove b, create d  ->  d got id 2      // collides with c
GetEntity(2) returned tag 'd'           // expected 'c'
```

`GetEntity(int id)` is `entities[id]` — a **list index**, not an ID lookup. After
a single removal it returns the wrong entity permanently. Any scene that despawns
(`SceneSnake`, `SceneBrickBreaker`) ends up with two live entities sharing an ID.

**Fix:** monotonic `_nextId++`, never reused, plus a `Dictionary<int, Entity>`
for lookup.

---

### GE-02 — `GetEntitiesWith<T>()` hands every caller the same mutable list

`GameEngine.Core/EntityManager.cs:190`

The cache is cleared and refilled on every call, then returned as
`IReadOnlyList` to *all* callers. Confirmed:

```
nested query threw InvalidOperationException: Collection was modified
same instance handed to two callers? True
```

Iterating `GetEntitiesWith<CTransform>()` while querying the same type inside the
loop throws. Two systems holding results across a call silently alias each other.
The natural thing to write in scene logic crashes.

> This is what `IMPROVEMENTS.md` §1.1 gets wrong (see `GE-57`). Wrapping in
> `AsReadOnly()` / `ReadOnlyCollection` changes nothing — the underlying list is
> still cleared on the next call.

**Fix:** per-call buffers, a versioned cache invalidated on structural change, or
a struct enumerator over the backing `HashSet`.

---

### GE-03 — Draw order scrambles when entities respawn

`GameEngine.Core/EntityManager.cs:232`

`componentEntityMap` is `Dictionary<Type, HashSet<Entity>>` and the snapshot is
built by walking that `HashSet`. Fresh inserts happen to enumerate in insertion
order, so this looks correct — until something despawns and new entities reuse
the freed slots. Confirmed:

```
initial      : old0,old1,old2,old3,old4,old5,old6,old7
after removes: old0,old1,old4,old6,old7
after respawn: old0,old1,new2,new1,old4,new0,old6,old7
```

Newly spawned entities jump *ahead* of older ones. Two consequences:

1. **Z-order is emergent and unstable.** There is no layer or sort key anywhere
   in the engine — draw order is hash-slot order. Sprites will pop in front of
   each other mid-game.
2. **Collision resolution becomes order-dependent.**
   `PhysicsSystem.ProcessCollisionsOptimized` only ever pushes `entityA` (the
   lower index), and only when `entityB.BlockMovement`. Which of a colliding pair
   moves therefore depends on `HashSet` slot order. In Pong the ball is
   `blockMovement: false` and paddles are `true`, so ball-vs-paddle resolution
   flips based on internal hashing state.

**Fix:** add an explicit `Layer` / `ZOrder` to the snapshot entry and sort on it;
make collision resolution symmetric (mass, or static/dynamic flags) rather than
index-dependent.

---

### GE-04 — `LevelBuilder` produces levels that cannot be loaded back

`GameEngine.Core/Utils/LevelBuilder.cs:102`, `LevelFile.cs:105`

`EntityBuilder.AddComponent` stores the payload **without a `type` field**, but
`LevelFile.LoadFromJson` requires `componentElement.GetProperty("type")`.
Confirmed round trip:

```
build -> SaveToFile -> LoadFromFile
=> KeyNotFoundException: The given key was not present in the dictionary
```

Written JSON contains bare `{"position":{...},"rotation":0}` and `{}` for
`AddInput()`. The entire programmatic level-authoring path is **write-only**, and
`LevelBuilderExamples.CloneLevel` / `ValidateAllLevelsInDirectory` are built on
top of it.

Related: `ComponentFactory.CreateCTransform` silently drops the `rotation` that
`AddTransform` writes (`GE-31`).

---

### GE-05 — Gravity is 1000× its documented value

`GameEngine.Core/Components/CGravity.cs:5`, `Systems/PhysicsSystem.cs:77`

`CGravity.Acceleration = 0.2` is commented *"virtual pixels per second^2"*.
`ProcessGravity` multiplies by `deltaTime`, which arrives in **milliseconds**,
while `MovementSystem` correctly converts (`deltaMs * 0.001`). Confirmed over one
simulated second at 60fps:

```
velocity.Y = 200 px/s, position.Y = 101.667 px
expected   =   0.2 px/s,           0.1 px
```

`deltaTime` means milliseconds in one system and seconds in another. The demos
have been tuned around the wrong number, so correcting the unit requires
re-tuning them.

**Fix:** make `ISystem.Update(EntityManager, double deltaSeconds)` seconds
everywhere, convert once in `Engine.CalculateDeltaTime`, and rename the parameter
so it is self-documenting.

---

### GE-06 — Audio is entirely dead code

`GameEngine.Core/Engine.cs:137`

```csharp
if (false==true)
    Systems.Add(new AudioSystem());
```

`_audioEnabled` is assigned in the constructor and **never read**. Four call
sites pass `audioEnabled: false` believing it does something. Consequences:

- `Systems.TryGet<AudioSystem>()` always returns null, so every
  `Scene.Initialize(..., AudioSystem audioPlayer, ...)` receives **null through a
  non-nullable parameter** — that is the `CS8604` warning at `Engine.cs:280`.
- `GameEngine.Core` still carries a hard `ppy.SDL2-CS` dependency and copies
  `SDL2_mixer.dll` — a Windows-only native binary — into every build, including
  iOS and browser, for a feature that cannot run.

Separately, while it *was* live, `AudioSystem.Play` called `Mix_LoadWAV` on
**every invocation** — file IO plus decode on the engine thread per sound effect
(`GE-56`) — and `_assets ??= new Assets("assets.txt")` hardcodes a path inside
the audio system.

**Resolved (2026-07-31, [D1](03-action-plan.md#decisions)):** SDL2 audio works on
the platforms that matter, so it is **kept and revived** behind the existing
`_audioEnabled` flag rather than deleted. Registration must also be gated on
platform — `AudioSystem`'s constructor throws on `SDL_Init` failure, which would
otherwise take down engine construction on browser/iOS. `GE-56` (per-call
`Mix_LoadWAV`) becomes a mandatory part of that work.

---

### GE-07 — Pointer input runs scene logic on the UI thread

`GameEngine.Core/InputManager.cs:88`

`HandlePointerEvent` invokes `action(remappedEvent)` **synchronously**. Runners
call it from UI event handlers, so pointer callbacks mutate live components while
the engine thread is inside `Update()`. `ScenePointer` does exactly this
(`c.PressPosition = pos`).

Keyboard is fine — `HandleKeyPress` only sets a `ConcurrentDictionary` flag, and
callbacks fire from `DoActions()` on the engine thread. Pointer bypasses that,
which partly undoes the isolation `RenderSnapshot` was built to provide.

**Fix:** queue pointer events and drain them in `InputSystem.Update`, exactly as
keys already are.

---

### GE-08 — `BindAction` before `AddAction` is silently dropped

`GameEngine.Core/InputManager.cs:47`

`BindAction` no-ops if the action name isn't registered yet. Confirmed:
bind-then-`AddAction` never fires, with no error. `LevelLoader.MapInputActions`
depends on the scene having called `AddAction` first — otherwise levels load with
dead controls and no diagnostic whatsoever.

---

### GE-09 — `HandlePointerEvent` throws before resolution is known

`GameEngine.Core/InputManager.cs:93`

Throws `InvalidOperationException` if resolution isn't set. Confirmed. A pointer
event arriving before the first `SizeChanged` takes down the UI thread. This
should no-op.

---

### GE-10 — `actionBindings` value lists are mutated across threads

`GameEngine.Core/InputManager.cs:14`

`ConcurrentDictionary<string, List<Action<bool>>>` — the *dictionary* is
concurrent, the `List` values are not. They're appended from the UI thread while
`DoActions` iterates them on the engine thread.

---

## Performance

All figures measured in **Release** on this machine. See [`04-evidence.md`](04-evidence.md).

### GE-11 — `PhysicsSystem` is O(n²) with no broad phase

`SceneBasic` creates exactly 3000 collidable entities:

| Entities | Physics ms/frame | Pair tests |
|---:|---:|---:|
| 500 | 0.26 | 124,750 |
| 1,000 | 1.13 | 499,500 |
| 2,000 | 5.57 | 1,999,000 |
| **3,000** | **14.49** | **4,498,500** |

At 3000 entities physics alone consumes 87% of a 60fps budget with nothing else
running. A uniform grid or spatial hash is **the single highest-leverage
performance change in the repo** — it turns 14.5ms into well under 1ms, and
matters far more than any micro-optimisation in recent commits.

### GE-12 — The FPS counter is misleading

`Engine.InitializeSystems` hardcodes `FpsSmoothingSamples = 1000`. Measured
response to a 240 → 30 fps cliff:

```
after    1 frames at 30 fps -> reads 239.8
after   60 frames at 30 fps -> reads 227.4
after  300 frames at 30 fps -> reads 177.0
after 1000 frames at 30 fps -> reads  30.0
```

A hitch visible to the eye takes ~1000 frames to register. Use ~30–60 samples,
or an exponential moving average.

### GE-13 — FPS smoothing is still O(n) per frame

Commit `34b2ce3` claims the circular buffer made this O(1). It didn't. The ring
removed the `RemoveAt(0)` shuffle, but the loop still re-sums all 1000 samples
every frame (measured 4.7µs/frame).

**Fix:** keep a running total — add the incoming sample, subtract the evicted one.

### GE-14 — `Engine.TargetFrameRate` is `static`

Global mutable state shared by every engine instance. The runners fight over it:

| Runner | Value | Frame budget |
|---|---:|---|
| Avalonia desktop | 14,400 | 0.069 ms |
| MAUI | 1,440 | 0.69 ms |
| WinForms | 1,000 | 1 ms |
| Editor | 120 | 8.3 ms |

At 14,400 the spin-wait in `RunLoop` never sleeps, so an `AboveNormal`-priority
thread pegs a core continuously. Whichever runner constructed last wins.

**Fix:** make it an instance property.

### GE-15 — Per-frame LINQ on hot paths

`SystemContainer.Get<T>()` is `FirstOrDefault` with a closure.
`Engine.Update` calls `Systems.Get<PhysicsSystem>()` every frame; every runner
calls `Systems.Get<InputSystem>()` on every input event.
`EntityManager.GetEntityWithTag` is also LINQ and is called several times per
frame by Pong.

**Fix:** cache a `Dictionary<Type, ISystem>`.

### GE-16 — `EntityManager.Update` removal is O(n·m)

Inactive entities are removed with `entities.Remove(entity)` in a loop. Use a
swap-remove or a single filtering pass.

---

## Architecture

### GE-17 — `Engine` cannot be stopped

`RunLoop` is `while (true)` with no exit; `SetRunning(false)` only makes it spin
on `Thread.Sleep(1)`. There is no `Stop()` and no `IDisposable`.
`IsBackground = true` saves you at process exit, but the editor creates engines
and never disposes them, so every session leaks a thread and its systems.

### GE-18 — `ApplySceneChange` replaces `EntityManager` *and* `InputManager`

Scene changes call `InitializeSystems()`, which allocates fresh instances. Any
code that cached `engine.InputManager` or `engine.EntityManager` is then talking
to a dead object. It works today only because everything re-resolves through
`engine.Systems` each time — a fragile invariant nobody has written down.

### GE-19 — `Scene` has two `Update` overloads and calls both every frame

```csharp
currentScene?.Update(EntityManager, physicsSystem, deltaTime);
currentScene?.Update(EntityManager, Systems, deltaTime);
```

Overload #1 is transitional; #2 is general. Keep the `SystemContainer` version,
delete the other.

### GE-20 — The engine cannot ship without the demos

Every runner does `using GameEngine.Demo;` and hardcodes `new SceneMenu()`. The
demo project is a *consumer* of the engine but is a compile-time dependency of
every host. Runners should take a startup scene via DI or configuration.

### GE-21 — Letterbox math is implemented four times

`RenderSystem.DrawEntitiesToCanvas`, `InputManager.HandlePointerEvent`,
`EngineView`'s hit test, and the legacy render path. **They have already
diverged:** `HandlePointerEvent` hardcodes `Math.Min` (Letterbox) and ignores
`RenderOptions.ScalingStrategy`, so with `Stretch` or `Crop` **clicks land in the
wrong place**.

**Fix:** extract one `ViewportTransform` type consumed by both render and input.

### GE-22 — ~180 lines of dead legacy render path

`RenderSystem` carries a duplicated `DrawEntitiesToCanvas(SKCanvas)` overload
plus its private helpers. The doc comment says the only remaining caller is
`Runner.Avalonia.Old` — which **is not in the solution**
(`grep -c Avalonia.Old GameEngine.sln` → `0`). Dead weight in the hot class.
Delete both the overload and the orphaned project.

### Smaller structural notes

- **`GE-23`** `Entity.Components` is a `ConcurrentDictionary` per entity — heavy
  allocation and an allocating `.Keys` enumeration — when access is
  single-threaded by design.
- **`GE-24`** `SystemContainer.Get<T>` uses exact `GetType() == typeof(T)`, so
  subclassed systems won't resolve.
- **`GE-25`** `LevelBuilderExamples` (maze generators, `Console.WriteLine`
  validators) ships inside the engine library.
- **`GE-26`** `Exceptions` is a non-static class used as a namespace container.

---

## Level system

There are **three** overlapping paths:

1. `LevelFile` / `LevelLoader` / `LevelManager` — JSON load. Works.
2. `LevelBuilder` / `EntityBuilder` — programmatic. **Write-only** (`GE-04`).
3. `EntityData.ToEntity` — a static duplicating `LevelLoader.CreateEntity`,
   unused except by the editor's design-time constructor.

Beyond the round-trip bug:

- **`GE-28`** `ComponentFactory` handles only 5 of 8 component types — no
  `CText`, `CCamera`, or `CGravity` — so those are unrepresentable in level files.
- **`GE-29`** `LevelLoader` hardcodes the tag `"player"`, the action names
  `Up`/`Down`/`Left`/`Right`, and the sound `"Hit"`. Game-specific policy baked
  into the engine.
- **`GE-30`** `SaveToFile` uses `File.WriteAllText` while `LoadFromFile` respects
  `Assets._fileFetcher` — save and load are asymmetric on mobile.
- **`GE-31`** `CTransform` rotation is dropped on load.

**Direction:** consolidate to one path. `LevelBuilder` should produce `LevelFile`
objects that serialise with `type` included, and both paths should share a single
serialiser.

---

## Editor

### GE-32 — `EngineView.OnSceneSelected` discards its own configuration

```csharp
_gameEngine.Systems.TryGet<RenderSystem>().options.DrawBoundingBoxes = true;
Engine.TargetFrameRate = 120;
InitializeAssetFileFetcher(...);
_gameEngine.InitializeSystems();   // <-- rebuilds everything, throwing the above away
```

The constructor already called `InitializeSystems()`. **Bounding boxes never draw
in the editor.** Delete that line.

### GE-33 — The non-bounding-box hit test is wrong

`boxPos = transform.Position - new Vec2(sprite.GetSourceRect().Size)` then tests
`boxPos .. boxPos + size`. The whole test rect is offset by a full sprite size, so
untagged sprites are selectable only in the wrong place.

The bbox branch is arithmetically correct but written so obliquely
(`boxPos.X + size.X/2` after `boxPos = Position - size/2`) that verifying it
reduces to `Position .. Position + size` takes real effort.

### GE-34 — The design-time constructor mutates global state

`LevelEditorViewModel()` sets `Assets._fileFetcher` to a hardcoded
`GameEngine.Demo` relative path and performs file IO. If that constructor ever
runs at runtime it corrupts asset loading process-wide.

Root cause: `Assets._fileFetcher` is a **public static mutable field**. Make it an
injected `IAssetSource`.

### Other editor findings

- **`GE-35`** `LevelEditorViewModel` is 743 lines covering scene compilation,
  level JSON editing, entity selection, and file dialogs. Split it.
- **`GE-36`** `SceneCompiler.cs` is a dead prototype with a hardcoded source
  string. `SceneProjectCompiler` supersedes it. Delete.
- **`GE-37`** `SceneProjectCompiler.EmitAndLoadAsync` calls `_alc.Unload()` while
  the engine may still be running a `Scene` from that ALC. It survives because
  unload is deferred, but the engine should be stopped and the scene reference
  cleared first.
- **`GE-38`** `http://192.168.2.169:8000` is hardcoded in
  `ImageGeneratorControl.axaml.cs:25` (and in a `ComfyUiClient` doc comment).
  A LAN address in a public repo — it makes the feature dead for everyone else
  and is needless disclosure. Move to config.
- **`GE-39`** `AssetEditorViewMovel.cs` — filename typo.

---

## Runners

### GE-40 — Swipe/tap logic is duplicated verbatim

The ~70-line swipe/tap → synthetic-key block is copy-pasted between
`Runner.Avalonia/GameView.cs` and `Runner.Winforms/MainView.cs`. MAUI lacks it
entirely. Extract a shared `GestureToKeyTranslator`.

### Other runner findings

- **`GE-41`** `public static Engine _gameEngine` in `GameView` — static, public,
  mutable, underscore-prefixed.
- **`GE-42`** `Task.Delay(100).ContinueWith(...)` fires synthetic key-ups on
  unbounded thread-pool continuations with no cancellation.
- **`GE-43`** WinForms `OnSizeChanged` passes the **Form's** `Bounds` rather than
  the canvas control's, so it includes title bar and borders and the letterbox
  math is off by that much.
- **`GE-44`** MAUI installs a `SimpleReactiveGlobalHook` that captures
  **system-wide** keystrokes even when the app is unfocused.

---

## Tests, build, CI

17 tests, all passing, covering `RenderSnapshot`, the snapshot handoff,
`InputManager`, `ActionMapper`, and movement. **The snapshot tests are genuinely
good** — they test the pooling invariants, which is the subtle part.

### GE-48 — Every confirmed bug lives in untested code

Nothing covers `EntityManager` (where `GE-01`–`GE-03` live), `LevelFile`
round-tripping (`GE-04`), `PhysicsSystem` collision resolution, `Engine`
lifecycle, or `Vec2`. That correlation is the actionable signal — not the
coverage number.

### GE-45 — CI is broken

`.github/workflows/dotnet.yml` pins `dotnet-version: 9.0.x` while every project
targets `net10.0`. Restore fails with `NETSDK1045`. One-line fix:

```yaml
dotnet-version: 10.0.x
```

### GE-46 — There is no README

For a project this size — four runners plus an editor — this is the largest
single gap for anyone (including future you) picking it up. Needs: what it is,
how to run each runner, how to write a scene, and the level JSON format.

### GE-47 / GE-49

~30 nullable warnings, including 3× `CS8618` on `Engine`'s public fields and
`CS0162` unreachable code (the dead audio line). Consider
`<TreatWarningsAsErrors>` on Core once cleared. `StarSim/` is an empty directory;
`tool_test_report.txt` is a stray artifact.

---

## Code quality

- **`GE-50`** `RenderSnapshot.Reset` doesn't clear entries past `_count`, so
  stale `SKBitmap`/`SKPaint` references stay reachable in the array tail after
  the entity count drops. (This is the *real* retention issue near what
  `IMPROVEMENTS.md` §1.2 mis-describes — see `GE-58`.)
- **`GE-51`** `Vec2`: `Normalize()` returns NaN on the zero vector and divides by
  a `float` `Magnitude()` while `Length()` returns `double`; `Clone()` on a
  `readonly struct` is a no-op; `operator >` / `<` compare by length, which is
  surprising.
- **`GE-52`** `CText.Text` is non-nullable but the default constructor leaves it
  null → NRE at `DrawText`.
- **`GE-53`** `Animation` supports horizontal strips only (`frameHeight` is the
  full texture height), has no non-looping mode, and divides by `delay` without
  guarding zero — `assets.txt` contains several `delay 0` entries.
- **`GE-54`** `Assets.ParseLine` has no error handling; a blank line throws
  `IndexOutOfRangeException`, and comments are unsupported.
- **`GE-55`** Static `SKPaint` instances are never disposed, and several
  SkiaSharp APIs in use (`TextSize`, `FilterQuality`, `SKFilterQuality`) are
  deprecated in SkiaSharp 3.x — relevant when that upgrade comes.
- **`GE-56`** `AudioSystem.Play` does file IO and decode per invocation.

---

## On the pre-existing docs

`IMPROVEMENTS.md` is broadly well-aimed — it independently flags entity ID reuse,
spatial partitioning, and the duplicate `Scene.Update`. Two corrections before
acting on it:

### GE-57 — §1.1's proposed fix doesn't work

It suggests `AsReadOnly()` / `ReadOnlyCollection`. Both wrap the *same list* that
gets cleared on the next call. See `GE-02` for what actually fixes it.

### GE-58 — §1.2 is incorrect

It claims `SKBitmap` references in `AnimationData` leak and should be disposed.
The snapshot does **not own** those bitmaps — `Assets` does, and they're shared
across every entity using that animation. Disposing them there would cause
use-after-free on the next frame. (It also cites
`GameEngine.Core/Rendering/RenderSnapshot.cs`, a path that doesn't exist.)

The real, much smaller retention issue nearby is `GE-50`.

---

## Closing assessment

For a first engine this is well above average. The snapshot pipeline is work I'd
expect from someone who had built one before.

The gap is one of *targeting*: recent effort went into optimising an already-fast
path (snapshot allocation) while the O(n²) broad phase and the ID / query-cache
bugs sat underneath, untested. The phase-1 and phase-2 items in
[`03-action-plan.md`](03-action-plan.md) are roughly a weekend of work and would
move this from "impressive but fragile" to genuinely solid.
