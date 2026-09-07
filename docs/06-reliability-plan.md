# Reliability and editing workflow plan

Status: implemented. Items 1–9 are complete, each with the regressions named
under it; item 10 is half complete and says so. Checked boxes are done and
verified; the notes under each item record what landed and how it was checked.

Reviewed checkout: `1f4b4da`. Recheck affected code before implementation if the
checkout has moved.

This follows the September 2026 code review and supplements the historical
[action plan](03-action-plan.md). Keep Core small, UI-independent, and driven by
`Engine.Tick(deltaSeconds)`. Build on the existing test harnesses and component
schemas rather than introducing a new ECS architecture.

## Outcome and scope

The first milestone is an engine that preserves user content, applies supported
mutations consistently, and releases hosted instances when their views close.
The second is predictable simulation and a complete level-editing workflow.

Deliver small, separately reviewable changes. Before implementing each newly
identified defect, add it to [the findings register](02-findings.md), allocate an
unused `GE-nn`, and reference that ID in commits. Existing GE-77 and GE-79 remain
the tracking IDs for cross-platform audio and visual level editing.

Avoid an archetype ECS rewrite, a new CLI, new runners, or a broad feature push
in this plan. A public API change needs a migration note and updates to the demo,
template, editor, and runners that consume it.

## Review evidence

These are observations from the review, not acceptance results for future fixes.

| Area | Evidence | Verification strength |
|---|---|---|
| Level saving | Malformed component JSON produced “Level saved.” and a file with zero components | Reproduced through the editor view model |
| Component replacement | Movement continued using the old cached transform; replacement X stayed 100 instead of advancing to 110 | Reproduced |
| Tag mutation | A cached query for the previous tag still returned the renamed entity | Reproduced |
| Shared input action | Removing D also removed callbacks needed by Right for the same action | Reproduced |
| Spatial grid | Two moving boxes retained 20,006 buckets after 10,000 frames | Reproduced; not a frame-time benchmark |
| Sprite scale | A 10×10 sprite rendered 100 red pixels at both scale 1 and scale 2 | Reproduced |
| View lifetime | GameView starts owned engines on attachment and has no detach cleanup | Source inspection; needs a lifecycle regression test |

The existing Release suites passed: Core 199 passed / 3 skipped, Demo 66 passed,
Editor 34 passed. Editor testing required `-p:UsedAvaloniaProducts=` to bypass a
sandbox-blocked Avalonia telemetry write. The full solution build failed because
the Android SDK was unavailable and reported 37 warnings. These results do not
establish desktop interaction, mobile, browser, or audio correctness.

Review probes were temporary. Convert each reproduction into a repository test
before its fix; do not depend on files under `/tmp` for ongoing verification.

## Phase 1 — Preserve content and make mutation reliable

### 1. Transactional level saving

Primary files: `GameEngine.Editor/ViewModels/LevelDocumentViewModel.cs` and
`GameEngine.Core/Utils/LevelFile.cs`.

- [x] Add a regression that loads a valid file, corrupts one component's RawJson,
      saves, and asserts that the original file is byte-for-byte unchanged.
- [x] Build a separate candidate document. Parse every component and validate the
      complete candidate using the same vocabulary enforced during loading.
- [x] Reject malformed JSON, invalid types, missing required fields, and unknown
      properties with the entity/component location. Never skip a component.
- [x] Serialize before touching the destination; write to a sibling temporary
      file and replace the destination only after the write succeeds.
- [x] Commit the in-memory document and success status only after replacement.
      Preserve edits on failure and clean up temporary files.
- [x] Verify valid round trips, invalid input, and a failed write. Test the save
      command path, not just JSON parsing.

Acceptance: a failed save loses neither existing file contents nor the user's
unsaved edits; a successful save loads through the strict runtime loader.

Landed as GE-82. `LevelFile.Validate` now applies the loader's component
vocabulary through the new `ComponentValidator`, naming the entity and component
position on rejection, and `SaveToFile` validates, serialises, writes a sibling
temporary file and moves it over the destination. The editor builds a candidate
document, validates it, writes it, and only then replaces the in-memory
document. Covered by `GameEngine.Core.Tests/Unit/LevelSaveTests.cs` (7 tests)
and `GameEngine.Editor.Tests/LevelSaveTests.cs` (8 tests).

### 2. ECS mutation and query consistency

Primary files: `GameEngine.Core/Entity.cs` and `EntityManager.cs`.

- [x] Add regressions for replacement through every supported AddComponent
      overload, cached single/pair queries, and tag changes.
- [x] Invalidate cached component tuples when an existing component is replaced,
      even when component-set membership does not change.
- [x] Make entity identity immutable and route tag changes through an API that
      invalidates tag queries. Restrict direct writes to component collections
      and the manager's entity list.
- [x] Define visibility of pending additions, removals, and references retained
      after Clear. Prevent detached entities from re-entering component indexes.
- [x] Preserve the existing guarantee that previously returned query lists are
      not mutated underneath an active enumeration.
- [x] Audit ActionMapper's captured component references and explicitly decide
      whether bindings follow replacement or are invalidated with diagnostics.
- [x] Update consumers and document any API migration.

Acceptance: after supported mutations, fresh queries and systems use the current
components and tags; manager indexes cannot be bypassed by ordinary public APIs.

Landed as GE-83 and GE-84, covered by
`GameEngine.Core.Tests/Unit/EntityMutationTests.cs` (13 tests).

Migration notes for consumers of the public API:

- `Entity.Id` is now get-only, `Entity.Tag` and `Entity.Active` are properties,
  and `Entity.Components` is an `IReadOnlyDictionary`. Add and remove components
  through the `Entity` methods; nothing outside Core wrote to the dictionary.
- `EntityManager.GetEntities()` returns `IReadOnlyList<Entity>` rather than the
  manager's own list.
- An entity is invisible to every query — `GetEntities`, component queries and
  tag queries alike — until the next `EntityManager.Update()`. It used to appear
  in component queries one frame earlier than in the others. Two Core tests
  relied on that and now call `Update()` before running a system.
- `ActionMapper` bindings resolve their component from the entity per
  invocation, so a mapping follows a replaced component and does nothing after
  the component is removed. The component must still exist when the mapping is
  made, so a wrong type is reported at the call site as before.

### 3. Shared action binding removal

Primary file: `GameEngine.Core/InputManager.cs`.

- [x] Reproduce D/Right sharing an action, removing D, then pressing Right.
- [x] Separate removal of one key mapping from removal of action subscriptions.
      Define explicit whole-action removal if consumers need it.
- [x] Recompute action state from remaining held keys. Verify removal while held,
      removal of the last key, re-binding, and scene reset.

Acceptance: removing one mapping preserves the other mappings and callbacks;
action state cannot remain incorrectly held after its last mapping disappears.

Landed as GE-85, covered by
`GameEngine.Core.Tests/Unit/Systems/ActionRemovalTests.cs` (9 tests).
`RemoveAction(GeKeys)` now removes one key mapping and recomputes the action
state from the keys still held; the new `RemoveAction(string)` overload is the
whole-action removal. `AddAction` also recomputes rather than forcing the state
to false, so mapping a second key to a held action no longer releases it. The
Core test that asserted the old behaviour is now two tests, one per overload.

## Phase 2 — Complete host lifecycle and rendering behavior

### 4. Hosted engine lifetime

Primary files: Avalonia `GameView.cs`, editor `EngineView.cs`, and Core `Engine.cs`.

- [x] Add attach/detach/reattach tests for owned and supplied engines.
- [x] Stop and dispose owned engines when the view ends its ownership. Define how
      reattachment creates or resumes a usable instance.
- [x] Disconnect callbacks from supplied engines without stopping or disposing
      the caller's engine. Preserve any prior callback according to a documented
      attachment contract.
- [x] Cancel synthetic key releases, release held input, and clear static Current
      only when it refers to the departing engine.
- [x] Ensure pending invalidations and draw operations tolerate detachment.
- [x] Audit UI lookups of InputSystem and RenderSystem during scene replacement;
      use a stable handoff rather than reading a container while it is rebuilt.
- [x] Exercise two simultaneous views and repeated editor preview reloads.

Acceptance: removing a view leaves no owned run loop or callback retaining it;
borrowed engines remain usable; reattachment and scene changes do not cause
missing-system exceptions.

Landed as GE-86. The attachment contract is in `AGENTS.md`: a view owns the
engine it constructs and borrows the one it is handed. `GameView` stops and
disposes an owned engine on detach and builds a fresh one on reattach; a
supplied engine only gains the view's paint callback at attach and has the
owner's restored at detach. Detach also cancels pending synthetic key releases,
releases held keys, and clears `GameView.Current` only when it still refers to
the departing engine.

`Engine.Systems` is now published whole rather than emptied and refilled, so a
UI thread reading it during a scene change cannot see a half-built container.
The audio device moved to engine lifetime for the same reason: rebuilding it per
scene reopened a mixer that was already open. Hosts look systems up with
`TryGet`, because the container is empty between disposal and the last queued UI
event that still refers to it.

Covered by the new `GameEngine.Runner.Avalonia.Tests` project (7 headless
Avalonia tests, including two simultaneous views) and two engine reload
regressions in `GameEngine.Core.Tests/Unit/EngineLifecycleTests.cs`. The editor
preview engine is released on detach as well as on data-context change.

### 5. Rendering, bounds, and picking

Primary files: `RenderSystem.cs`, `RenderSnapshot.cs`, and editor `EngineView.cs`.

- [x] Add pixel-based scale tests and specify the sprite origin, pivot, negative
      scale behavior, and relationship between visual size and collision boxes.
- [x] Apply transform scale during sprite drawing. Keep physics bounds explicit
      rather than silently changing collision semantics with visual scale.
- [x] Compute conservative visual bounds for culling, including sprite extent,
      rotation, and scale; do not cull a visible sprite based only on its origin.
- [x] Share camera/world/screen conversion logic with picking. Account for camera
      position and zoom, sprite centering, rotation, scale, and layer order.
- [x] Pick the topmost visible matching entity when sprites overlap.

Acceptance: scale changes visible size; a visible transformed sprite remains
visible at viewport edges and can be selected where it is drawn.

Landed as GE-87, covered by
`GameEngine.Core.Tests/Unit/Systems/SpriteScaleAndPickingTests.cs` (12 tests,
five of them counting rendered pixels).

The geometry contract is now written down once, in `SpriteGeometry`, and stated
in `AGENTS.md`: `CTransform.Position` is the top-left of the bounding box when
there is one and the sprite centre otherwise; a sprite is drawn centred on that
point with rotation and scale applied about it; a negative scale mirrors on its
axis; and `CBoundingBox.Size` stays independent of `CTransform.Scale`, so making
a sprite bigger does not silently enlarge what it collides with.

`RenderSystem` applies the scale when drawing, culls against
`RenderSystem.VisualBounds` — the union of the box, the rotated and scaled
sprite extent, and a conservative text extent — and exposes `TryScreenToWorld`,
the exact inverse of the camera transform it draws through, plus `PickTopmost`.
The editor's picker calls both, so it can no longer disagree with the renderer
about where an entity is. No shipped level or scene set a scale other than 1, so
nothing in the demos changes appearance.

## Phase 3 — Bound performance and stabilize simulation

### 6. Spatial-grid lifetime and representative benchmarks

- [x] Replace retention of historical cells with active-cell tracking and pooled
      buckets. Keep memory proportional to current demand, allowing a documented
      bounded high-water pool.
- [x] Add a long-travel regression proving retained storage stabilizes for a
      fixed number of similarly sized moving colliders.
- [x] Benchmark steady state, moving worlds, spawn/despawn churn, dense overlaps,
      and long walls mixed with small colliders in the existing benchmark project.
- [x] Preserve collision coverage and ordering. Test solid contacts where
      resolution changes positions after the broad phase is built.
- [x] Change cell-size policy or separate oversized colliders only if mixed-size
      measurements justify it. Record before/after results in one environment.

Acceptance: travel duration does not continually increase memory or per-frame
grid clearing work; collision regressions pass; benchmark results state scene
size, distribution, allocations, and timing.

Landed as GE-88, covered by
`GameEngine.Core.Tests/Unit/Systems/BroadPhaseLifetimeTests.cs` (6 tests) and by
`BroadPhaseBenchmarks` in `GameEngine.Benchmarks`.

Each cell now carries the frame that last put something in it, so a still world
re-uses its cells without touching the dictionary and a moving one clears only
what it occupies. Cells nothing occupied are dropped once the map has drifted
past `2 × occupied + 64`, and the dropped cells go back to a free list — the
bounded high-water pool. The two travelling boxes that used to retain 20,006
buckets after 10,000 frames now peak at 80 retained cells and occupy 8.

Cell-size policy is unchanged: the mixed-size benchmark (four 8000×24 walls
among 1,000 12×12 colliders) is within noise before and after, so nothing
justified separating oversized colliders.

### Benchmark results

BenchmarkDotNet ShortRun (3 warmup, 3 iterations), .NET 10, Release, Apple
silicon macOS 25.6.0, one environment, same session. 1,000 colliders per scene
except where stated; every scene is built from a fixed seed.

| Scene | Before | After | Allocated before | Allocated after |
|---|---:|---:|---:|---:|
| Steady world, 1,000 colliders spread over 8000×8000, sizes 12–48 | 75.0 µs | 79.2 µs | 0 B | 0 B |
| Moving world, the same scene translated (11, 7) each frame | 1,864.4 µs | 255.4 µs | 5,246 B | 0 B |
| Spawn, collide and despawn 1,000 colliders | 600.1 µs | 521.0 µs | 1,377,352 B | 1,639,446 B |
| Dense overlaps, 1,000 32×32 colliders inside 200×200 | 2,608.5 µs | 2,573.9 µs | 0 B | 0 B |
| Four 8000×24 walls among 1,000 12×12 colliders | 4,768.8 µs | 4,797.4 µs | 0 B | 0 B |

The moving world is where the old grid's per-frame clear of every historical
bucket showed up: 7.3× faster and no longer allocating. The steady world pays
about 4 µs for the frame stamp, and the churn benchmark's extra allocation is
the free list filling on the frame the scene empties. Dense overlaps and mixed
sizes are unchanged, as expected — neither touches cell retention.

### 7. Fixed simulation steps and fast collisions

- [x] Specify a fixed-step accumulator for live runners, a maximum catch-up budget,
      and explicit behavior for pauses and elapsed time beyond that budget.
- [x] Preserve Tick as the single simulation primitive. Reject invalid elapsed
      values at its boundary and document its contract for headless consumers.
- [x] Separate simulation rate from presentation pacing; respect browser pacing
      and TargetFrameRate semantics. Add interpolation only if visually needed.
- [x] Verify that equivalent elapsed time split across different host-frame
      schedules produces equivalent simulation steps with scripted input.
- [x] Add thin-wall/high-speed regressions to Core and a real bouncing scene to
      Demo.Tests. Implement swept AABB or a documented bounded-substep strategy;
      fixed steps alone do not prevent tunneling.
- [x] Preserve impact velocities and verify simultaneous contacts, starting
      overlaps, and moving bodies.

Acceptance: host frame jitter within the catch-up budget does not alter the
simulation step sequence; supported fast bodies cannot cross tested solid walls.
Do not claim full deterministic replay without controlling input order and RNG.

Landed as GE-89, covered by `GameEngine.Core.Tests/Unit/FixedStepTests.cs`
(18 cases), `GameEngine.Core.Tests/Unit/Systems/TunnellingTests.cs` (9 tests)
and `GameEngine.Demo.Tests/FastBallTests.cs` (4 tests, driving Pong and
BrickBreaker through the real engine).

**Stepping.** `FixedStepAccumulator` turns a host frame's elapsed time into whole
steps of `Engine.FixedTimeStep` (default 1/120 s), capped at
`Engine.MaxCatchUpSteps` (default 8). Time past that budget is dropped and
counted in `Engine.DroppedSimulationSeconds` rather than queued, because queueing
a shortfall makes the next frame longer and the one after that longer again. A
pause resets the accumulator so resuming does not spend the pause. Both run loops
still reach `Engine.Tick`, which advances by exactly what it is given and now
rejects anything that is not finite and non-negative — the contract a headless
consumer relies on when it owns its own pacing. `TargetFrameRate` paces
presentation only. No interpolation was added: with a 120 Hz simulation the
sub-step error is smaller than the pixel it would smooth.

The single clamp on a long frame is now the catch-up budget. The old
`MaxDeltaSeconds` clamp was removed rather than left to disagree with it.

**Tunnelling.** Fixed steps alone do not stop a fast body, so `PhysicsSystem`
sweeps. The broad phase inserts each collider over the cells its path crossed,
bounded to 64 cells per body — a longer jump is a teleport, not motion, and falls
back to the current footprint. The narrow phase sweeps every candidate pair
before resolving any of them, keeps only the earliest contacts (a body that
stopped at the first wall never reached the second), and backs the moving bodies
up to the moment of contact.

Sweeping also takes precedence over the old minimum-translation separation, which
fixed a second way through a wall: a body that ended the step deeply inside one
was pushed out whichever side was nearer, and for a fast body that is the far
side. `MovementSystem` now records `PreviousPosition` for every transform rather
than only the ones it moves, so the sweep reads a real path.

Contacts a sweep finds report the impact velocity, like any other contact, and a
non-solid pair still reports the contact without being separated — a fast body
passing through a trigger is no longer missed.

### Benchmark results

Same environment and method as the GE-88 table above, measured before and after
the sweep landed.

| Scene | Before sweep | After sweep |
|---|---:|---:|
| Steady world, 1,000 colliders | 79.2 µs | 88.1 µs |
| Moving world, 1,000 colliders travelling | 255.4 µs | 357.1 µs |
| Spawn, collide and despawn 1,000 colliders | 521.0 µs | 582.4 µs |
| Dense overlaps, 1,000 colliders in one pile | 2,573.9 µs | 2,723.6 µs |
| Four 8000×24 walls among 1,000 12×12 colliders | 4,797.4 µs | 5,066.8 µs |

The sweep costs 5–17% depending on scene shape; the moving world pays most
because its colliders genuinely span more cells. All five remain well below the
1,864 µs the moving world cost before GE-88.

## Phase 4 — Finish the authoring and platform story

### 8. Runtime failures and resource ownership

- [x] Define a faulted engine state and a host-visible error containing the scene,
      operation/system, and exception. Stop failed loops safely and surface the
      diagnostic in editor preview.
- [x] Specify scene exit versus reset, asset ownership, and release behavior.
- [x] Add explicit cleanup for owned native resources. Shared textures must stay
      alive until all render snapshots using them are retired; do not dispose
      textures when an individual animation or entity is removed.
- [x] Verify repeated scene changes, failed initialization, runtime exceptions,
      and disposal with a render reader still holding a snapshot.

Acceptance: a bad preview scene reports a useful failure and can be replaced;
reloading does not retain old scene resources or release textures still in use.

Landed as GE-90, covered by `GameEngine.Core.Tests/Unit/EngineFaultTests.cs`
(8 tests) and `GameEngine.Core.Tests/Unit/AssetOwnershipTests.cs` (7 tests).

**Faults.** An exception from a system, a scene's `Update` or a scene's
`Initialize` becomes an `EngineFault` naming the scene, the operation and the
system, handed to `Engine.FaultAction` on the engine thread. The engine stops
simulating rather than throwing the same exception every frame, keeps its last
snapshot so the host can still paint, and clears the fault when another scene is
loaded — which is what lets the editor replace a preview scene that failed. A
failed initialization also clears the entities the half-built scene created. The
editor preview reports the fault through the status line.

**Scene exit versus reset.** Both go through `ChangeScene` on the engine thread.
Either way the entity manager is cleared, the input manager is reset and the
system container is rebuilt; `ResetScene(null)` re-initialises the scene already
loaded, `ResetScene(other)` and `ChangeScene(other)` leave it. What neither
touches is assets. The audio device is engine-lifetime, established in GE-86.

**Asset ownership.** An `Assets` owns every texture it decodes and every scaled
animation it derives; a plain `Animation` borrows its texture and releases
nothing, so disposing one because an entity was removed cannot pull a bitmap out
from under another entity or a snapshot still being painted. `Assets` is now
`IDisposable`, and the contract is that its owner disposes it after the engines
using it. The one leak this uncovered was `AsScaledAnimation`, which decodes a
bitmap nobody owned: every BrickBreaker load leaked one per scaled entity.
`Assets.GetAnimation(name, scaleSize)` caches and owns them instead, and the demo
scene uses it.

### 9. One editable level document (GE-79)

Depends on safe saving, mutation semantics, and correct picking.

- [x] Give authored entities stable document identities distinct from transient
      runtime IDs; define mapping into preview and behavior for runtime-only entities.
- [x] Drive selection, inspector edits, placement, and serialization from the same
      document. Route preview changes through the engine-thread work queue.
- [x] Introduce undoable edit commands, dirty state, and a clear unsaved-edit flow.
- [x] Deliver one complete workflow: open level, place entity, edit position and
      appearance, preview, undo/redo, save, reopen, and verify the same result.
- [x] Generate component choices from ComponentSchemas so the editor exposes the
      supported vocabulary without another hand-maintained list.

Acceptance: editing and saving reproduce the preview's authored state; play-mode
simulation does not silently overwrite the authored document.

Landed as GE-79, covered by
`GameEngine.Editor.Tests/LevelDocumentWorkflowTests.cs` (15 tests, one of them
the whole open-place-edit-preview-undo-redo-save-reopen round trip).

`LevelEntityViewModel.DocumentId` is the authored identity, stable for as long as
the document is open and unrelated to the runtime id the preview assigns.
`LevelDocumentViewModel.Project()` returns the document exactly as the engine
would read it, paired with those identities, and both the preview and the saved
file are built from that one projection — the test asserts the two are byte-equal
JSON. `LevelDocumentScene` loads that projection through the ordinary
`LevelLoader` (which now reports each entity with its document index) and keeps a
runtime-to-document map, so a click in the viewport selects the row that produced
what was clicked. An entity the simulation spawns later maps to nothing and is
shown as the read-only runtime inspector already showed it.

Every change goes through an `ILevelEdit` on an `EditHistory`: adding, removing
and renaming entities, adding and removing components, and every value typed into
the inspector, which each component reports as one edit however many setters the
gesture walked through. `HasUnsavedChanges` is history depth against the last
save, so undoing past a save marks the document dirty again. Shift-clicking the
viewport places an entity at that world position, using the same screen-to-world
conversion the renderer draws through (GE-87).

The preview follows the document while it is paused and stops following once it
is running, so play-mode simulation cannot write back over authored state. The
component type list is `ComponentSchemas.KnownTypes`; the four types with no
typed form yet are edited as JSON, which the inspector now says rather than
leaving blank.

### 10. Cross-platform audio (GE-77)

- [x] Inventory platform capabilities and native packaging requirements before
      choosing a backend. Keep platform-specific integration outside Core.
- [x] Define a small audio service with an explicit unavailable state, reusable
      decoded sounds, lifetime management, and a headless test implementation.
- [ ] Prove one sound end to end on desktop, then mobile and browser; account for
      browser user interaction requirements and app suspend/resume.
- [ ] Add platform startup/playback smoke checks where practical and document
      manual playback checks separately from build-only validation.

Acceptance: advertised audio platforms produce sound in a generated game; missing
audio support is diagnosable and does not prevent the game from running.

**Half of GE-77 is done.** The second half of the acceptance criterion — missing
audio is diagnosable and does not prevent the game from running — is met and
tested. The first half is not: no sound has been proved end to end on any
platform in this work, because that needs a machine with a device, a browser and
two mobile targets, none of which is verifiable from here. What has landed is the
inventory, the decision it points to, and the seam a backend plugs into.

### Inventory

| Platform | Today | What a backend needs |
|---|---|---|
| Windows x64 | Works, via the `SDL2_mixer.dll` checked into this repo | Nothing new |
| Linux, macOS | Silent | An `SDL2_mixer` native for the RID, which `ppy.SDL2-CS` does not ship |
| Android | Silent, excluded by platform check | A managed backend over `AudioTrack`/`SoundPool`, or an SDL2_mixer `.so` in the APK |
| iOS | Silent, excluded by platform check | `AVAudioPlayer`/`AudioToolbox` through a binding, or a static SDL2_mixer |
| Browser (wasm) | Silent, excluded by platform check | WebAudio through JS interop, plus the user-gesture rule below |

`ppy.SDL2-CS` ships SDL2 natives per RID but no mixer, which is why the only
mixer binary in the tree is a hand-placed Windows DLL. Two constraints shape any
choice: a browser will not start audio before a user gesture, so a backend there
has to queue what it is asked to play until one arrives; and mobile suspends the
audio session on background, so a backend has to reopen it on resume. Neither is
expressible in a single cross-platform native library, which is what the
inventory settles: **the backend is per-platform, so Core must not reference
one.**

### What landed

`GameEngine.Core` no longer references `ppy.SDL2-CS` and redistributes no native
audio binary. It defines `IAudioBackend` — name, availability, an unavailable
reason, play and stop — plus `SilentAudioBackend` (always available, plays
nothing, the headless implementation) and `UnavailableAudioBackend` (carries the
reason). A host registers one with `AudioBackends.Factory` before it constructs
an engine.

`AudioSystem` is now the service rather than the device driver. It always exists
when audio is enabled: it resolves a sound name against the asset manifest, hands
it to the backend, and reports `IsAvailable` and `UnavailableReason` when nothing
will be heard — where before the whole system was silently absent and a scene's
`audio?.Play(...)` did nothing with no way to ask why. Decoded sounds are reused
by the backend, which is the only layer that knows what decoded means; the
engine owns the audio device for its lifetime (GE-86) and disposes only a backend
it created.

`GameEngine.Audio.Sdl` is the SDL2_mixer backend, and the only project in the
repo that references a native audio library. It never throws on a machine with no
device or no mixer — it reports itself unavailable. The desktop Avalonia head,
the WinForms head and the editor register it; the NuGet workflow publishes it.

Covered by `GameEngine.Core.Tests/Unit/Systems/AudioSystemTests.cs` (12 tests
against a recording backend). The three tests that used to be skipped for want of
a device now run everywhere.

### What is left

- A backend each for Android, iOS and browser, plus a `SDL2_mixer` native for
  Linux and macOS or a managed replacement.
- Proving one sound end to end on each, which needs real devices.
- Startup smoke checks per platform, and a written manual playback check kept
  separate from build-only validation.
- The template still references only `GameEngine.Core`; a generated game opts
  into audio by adding `GameEngine.Audio.Sdl` and one line in its desktop head.
  That stays undocumented in the template until a sound has actually been heard
  from it.

## Verification and delivery

For each change, write the behavioral regression first, apply the smallest fix,
and run the relevant suite. Use Demo.Tests for scene behavior and Editor.Tests
for document/command behavior. Add runner lifecycle coverage where it is absent.

Before merging a milestone:

```bash
dotnet build GameEngine.sln -c Release
dotnet test -c Release
```

Run builds and tests sequentially when they share output directories. Provision
required platform SDKs or use CI for those targets; report missing-platform checks
as unverified. Keep the existing generated-template smoke test passing.

### What was verified for this plan

Every project in the solution builds clean in Release except the Android head,
which fails with `XA5300` because no Android SDK is installed here. The iOS and
browser heads were not built either; all three are **unverified** and left to CI.

Suite totals after the work, all green:

| Suite | Result |
|---|---|
| `GameEngine.Core.Tests` | 299 passed, 0 skipped (was 199 passed / 3 skipped) |
| `GameEngine.Demo.Tests` | 70 passed (was 66) |
| `GameEngine.Editor.Tests` | 57 passed (was 34) |
| `GameEngine.Runner.Avalonia.Tests` | 7 passed (new project) |

The three previously skipped audio tests now run everywhere, because the service
no longer needs a device to be testable.

The generated-template smoke test was run locally the way CI runs it: pack Core,
the new audio backend, the Avalonia runner and the template pack into a local
feed, `dotnet new gameengine-game`, then build the desktop head and run the
generated tests. Build clean, 6 tests passed.

The Avalonia desktop runner and the editor were each launched and stayed up with
no exception logged — a startup check, not an interaction check. **Desktop
interaction, mobile, browser and audible playback remain unverified**, as they
were when the plan was written.

Benchmarks were run with BenchmarkDotNet ShortRun before and after items 6 and 7;
the tables are under those items.

Resolve current editor nullability/obsolete API warnings and WinForms package
compatibility warnings in focused changes, then extend warnings-as-errors to the
affected projects. Do not suppress warnings merely to meet the zero-warning goal.

Done. The 37 warnings the review counted are gone and none of them was
suppressed:

- Editor and ImageGen nullability (CS8618, CS8602, CS8603, CS8600, CS8601,
  CS8604, CS8629, CS8767): the models and view models that could not fill a
  field now do — a designer constructor builds the file picker it needs rather
  than leaving one null, `Texture.Bitmap` is nullable because it genuinely is
  until an image loads, and the two client methods that could return a null
  filename now say so.
- `Workspace.WorkspaceFailed` (CS0618) moved to `RegisterWorkspaceFailedHandler`.
- `TextBox.Watermark` (AVLN5001) moved to `PlaceholderText`.
- WinForms package compatibility (NU1701, 6 of them): the head targeted
  `net10.0-windows`, which is below the `net10.0-windows10.0.19041` group
  `SkiaSharp.Views.WindowsForms` publishes, so NuGet silently resolved its .NET
  Framework assets and their OpenTK 3.1.0. Raising the target framework resolves
  the modern assets. `SKGLControl.VSync` does not exist in OpenTK 4.x and was
  setting the default anyway, so the designer line went with it; the engine paces
  presentation through `TargetFrameRate`.

`TreatWarningsAsErrors` now covers every project that builds here: the editor,
the image generator, the editor desktop head, the Avalonia runner and its desktop
head, the WinForms head, the new audio backend, all four test projects and the
benchmarks, alongside Core and Demo where it already was. The Android, iOS and
browser heads and the template pack are excluded, because no build of them was
run in this environment and turning their warnings into errors unseen would only
move the failure into CI.

For lifecycle and editor milestones, also launch the Avalonia runner and editor
and exercise the relevant interaction. For performance changes, use reproducible
benchmarks; avoid brittle timing assertions in unit tests.

Recommended delivery order is items 1–6 as individual fixes, followed by simulation
and resource contracts (7–8), then the complete editor workflow (9). Audio (10)
can be scoped independently after resource ownership is defined. Update this plan
and the findings register with actual test evidence as each item lands.

That order was followed, one commit per item, each referencing its `GE-nn`.
