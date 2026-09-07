# Action Plan

Sequenced work plan for the findings in [`02-findings.md`](02-findings.md).
Ordered by *unblocking value*, not just severity — cheap items that make later
work verifiable come first.

Tick items as they land and update the **Status** column in the register.

---

## Phase 0 — Unblock (~1 hour) ✅ COMPLETE

*Landed 2026-07-31. Build clean, 17/17 tests green, all three behavioural fixes verified.*

Small, isolated, no design decisions. Do these first so everything after is
verifiable in CI.

- [x] **GE-45** Set CI to `dotnet-version: 10.0.x` in `.github/workflows/dotnet.yml`
      *One line. Currently every push fails at restore with `NETSDK1045`.*
- [x] **GE-09** `HandlePointerEvent` no-ops instead of throwing when resolution is unset
- [x] **GE-12** `FpsSmoothingSamples` 1000 → 60 in `Engine.InitializeSystems`
- [x] **GE-13** Keep a running sum in `RenderSystem.Update` (add incoming, subtract evicted)
- [x] **GE-49** Delete empty `StarSim/` and stray `tool_test_report.txt`
- [x] **GE-39** Rename `AssetEditorViewMovel.cs` → `AssetEditorViewModel.cs`
- [x] **GE-36** Delete `Magic/SceneCompiler.cs` (dead prototype)

---

## Decisions

Recorded 2026-07-31. These close out the open questions from the review.

### D1 — Audio stays (`GE-06`, `GE-56`)

**SDL2 audio works on the platforms that matter, so it is kept.** `GE-06` is
therefore *revive*, not delete. That makes it real work rather than a one-line
deletion, so it moves into Phase 1:

- Wire `_audioEnabled` so it actually controls registration — replace
  `if (false==true)` with the flag it was always meant to read
- `Scene.Initialize`'s `AudioSystem audioPlayer` → `AudioSystem?` (clears the
  `CS8604` at `Engine.cs:280` and makes the null case honest)
- Gate registration on platform as well as flag — SDL2 audio cannot initialise
  on browser/iOS, and `AudioSystem`'s constructor **throws** on
  `SDL_Init` failure, which would take down engine construction
- **`GE-56` becomes mandatory, not optional:** `Play` currently calls
  `Mix_LoadWAV` on every invocation — file IO plus decode on the engine thread
  per sound effect. Cache decoded chunks in `Assets`, free them on dispose
- Keep `ppy.SDL2-CS` and the `SDL2_mixer.dll` copy step

*Open sub-question for later: the `SDL2_mixer.dll` copy is unconditional, so a
Windows-only native binary still ships in iOS and browser output. Worth
conditioning on RID, but it is cosmetic and not blocking.*

### D2 — Unit fix proceeds with demo re-tuning (`GE-05`)

Converting `deltaTime` to seconds engine-wide is approved, **including** the
consequent re-tuning of every demo's gravity and speed constants. Expect a
diff that touches most of `GameEngine.Demo`; that is the intended blast radius,
not scope creep.

---

## Phase 1 — Correctness (~1 day)

Per **D1**, audio revival lands here:

- [x] **GE-06** Make `_audioEnabled` actually control `AudioSystem` registration
      - [x] Replace `if (false==true)` with the flag
      - [x] Gate on platform too — `AudioSystem`'s ctor throws on `SDL_Init` failure
      - [x] `Scene.Initialize` param → `AudioSystem?`; clears `CS8604`
- [x] **GE-56** Cache decoded `Mix_LoadWAV` chunks; free on dispose
      - **Approach:** `AudioSystem.TryCreate()` returns null instead of throwing — unsupported
        platform, no device, or missing native library all degrade to silence rather than
        taking engine construction down. The constructor still throws for callers who want
        failure to be fatal. Chunks are cached for the system's lifetime, so `Play` is a
        pointer lookup; they are freed in `Dispose` after `Mix_HaltChannel(-1)` so nothing is
        reading them. BGM now loops with -1 rather than the old literal 100, closing that TODO.
      - **Cost:** ~0.5 ms added to `Engine` construction on a machine with no mixer (a caught
        `DllNotFoundException`). `InitializeSystems` runs on every scene change, so this
        repeats — judged not worth caching in static state, which is exactly the kind of
        global the rest of this work has been removing.

### ⚠ Audio only works on Windows today

Established while doing D1, and it gates any play-testing:

- `ppy.SDL2-CS` ships **SDL2** natives per platform (`libSDL2.dylib`, `SDL2.dll`) but does
  **not** ship `SDL2_mixer` for any platform.
- The only `SDL2_mixer` binary in the tree is a hand-placed **Windows x64** `SDL2_mixer.dll`
  in `GameEngine.Core/`, copied unconditionally into every build output — including macOS,
  Linux, iOS and browser, where it is useless.
- So on macOS and Linux `TryCreate()` returns null (verified: `DllNotFoundException`) and the
  game runs silently. That is the intended graceful degradation, but it is not "audio works".

To actually get audio off Windows, `SDL2_mixer` natives need to arrive per-RID — either a
NuGet package that ships them, or a build step that fetches them. Filed as a follow-up rather
than fixed here, since it is packaging work rather than engine work.


Each item gets a regression test *first* — these all live in untested code
(`GE-48`), which is exactly why they survived this long.

- [x] **GE-01** Monotonic entity IDs + `Dictionary<int, Entity>` lookup
      - [x] Test: create → remove → create; assert IDs never collide
      - [x] Test: `GetEntity(id)` returns the right entity after removals
- [x] **GE-02** Fix query-cache aliasing
      - [x] Test: nested `GetEntitiesWith<T>()` inside a `foreach` doesn't throw
      - [x] Test: two callers don't receive the same instance
      - **Approach taken:** versioned cache. A `structuralVersion` counter is bumped
        on entity add/remove and component add/remove; a stale cache entry is
        replaced with a *new* list rather than cleared in place, so a result already
        handed to a caller is never mutated underneath them.
      - **Measured tradeoff:** steady state is still **0 bytes/frame** (verified over
        100 physics frames on a static 3000-entity scene), and a cache hit costs
        0.36 µs. A scene that spawns and despawns every frame now allocates
        ~13.4 KB/frame rebuilding query lists, where the old code allocated nothing
        but was unsafe. Gen0-cheap and acceptable; if profiling ever shows it
        mattering, the next step is the struct enumerator over the backing `HashSet`.
- [x] **GE-05** Unify `deltaTime` to **seconds** engine-wide
      - [x] Convert once in `Engine.CalculateDeltaTime`; rename params to `deltaSeconds`
      - [x] Fix `PhysicsSystem.ProcessGravity`
      - [x] Check `CAnimation.Update` / `Animation.delay` (currently ms — convert or rescale `assets.txt`)
      - [x] Re-tune demo gravity values; they were tuned against the wrong unit
      - **Applied:** every gravity value multiplied by 1000, which exactly cancels the
        old millisecond delta, so the demos feel identical. `CGravity.Acceleration`
        default 0.2 -> 200; `SceneSideScroll2` 0.7 -> 700. The unit comment on
        `CGravity` is now true rather than aspirational.
      - **Note:** `Engine.RunLoop`'s frame *pacing* still works in milliseconds against
        `Stopwatch.Elapsed.TotalMilliseconds`. That is internal and self-consistent —
        it never reaches a system — so it was deliberately left alone.
      - [x] Test: 1s of gravity at 0.2 px/s² yields ≈0.2 px/s
- [x] **GE-03** Deterministic draw order + symmetric collision resolution
      - [x] Add `Layer`/`ZOrder` to `CTransform` or the snapshot entry; sort in `BuildRenderSnapshot`
      - [x] Make `ResolveCollision` use static/dynamic flags or mass, not pair index
      - [x] Test: draw order stable across remove/respawn cycles
      - **Draw order:** `BuildRenderSnapshot` now walks the ordered entity list instead of the
        component `HashSet`, so spawn order is the baseline for free. `CTransform.Layer` (int,
        default 0) sorts on top, and the sort is skipped entirely when every entity is on the
        default layer. Ties break on entity id, which is monotonic after GE-01, so an unstable
        sort still yields stable spawn order.
      - **Collision:** `BlockMovement` reads as "I am solid; things get pushed out of me",
        matching how every demo uses it. Solid vs non-solid displaces the non-solid one fully;
        two solids let whichever one *moved* back out (so static geometry is never shoved
        aside); two non-solids are a trigger pair that only raises the event. The rule is
        stated symmetrically in A and B, so the outcome no longer depends on pair order.
      - **Measured:** snapshot build at 3000 entities is 0.156 ms/frame and 0 bytes when
        unlayered, 0.514 ms and 64 bytes when layers are in use.
      - **Note:** the first draft used a flat 50/50 split for two solids, which broke the
        existing `PhysicsAndMovement_EntityStopsAtWall` integration test by sliding the wall.
        The test was right; the rule gained the moved/stationary distinction.
      - **Follow-up (regression found in play-testing):** making resolution consistent meant
        Pong's ball/paddle pair got resolved for the first time — previously the ball came up
        as "B" and the old rule only resolved when *B* was solid, so it was skipped entirely.
        Resolution zeroes the velocity on the separation axis, and `ScenePong` read
        `Velocity.X` *after* the systems ran to choose a bounce direction; it saw 0, fired the
        ball back into the paddle, and the `LastHitPaddle` guard blocked a second bounce. Fixed
        by adding `VelocityA`/`VelocityB` (impact velocities, captured before resolution) to
        `CollisionEvent`, which is information any bouncing scene needs. `ScenePong` now
        reflects off those for both the paddle and the top/bottom wall — the wall path had the
        same latent bug, negating an already-zeroed Y.
- [x] **GE-04** `LevelBuilder` round-trip
      - [x] Write `type` into the component payload
      - [x] **GE-31** Read `rotation` in `ComponentFactory.CreateCTransform`
      - [x] Test: build → save → load → assert equality
      - **Approach:** `ComponentData.Type` is now the single source of truth. `ToJson` writes
        the discriminator itself and skips any `type` already inside `Data`, so it handles
        both sources — builder output (payload only) and loaded data (payload plus its own
        `type`) — without duplicating or dropping it. `LoadFromJson` now raises a named
        `InvalidDataException` instead of a bare `KeyNotFoundException`.
      - **Verified against the shipped levels:** `level1.json` and `level2.json` both load,
        validate, and re-serialise to a fixed point, so an editor save cannot corrupt them.
      - *Still deferred to Phase 4: `CTransform.Scale` and the new `Layer` are not serialised,
        and `ComponentFactory` still covers only 5 of 8 component types (GE-28).*
- [x] **GE-07** Queue pointer events; drain in `InputSystem.Update`
      - Pointer events now enqueue only; `InputSystem.Update` drains them on the engine
        thread, so scene callbacks no longer run on the UI thread. The queue is capped at 256
        so a paused engine cannot grow it without bound. Real→virtual mapping moved into one
        `TryMapToVirtual`, which also stops it being recomputed once per bound handler.
- [x] **GE-08** ~~throw or log~~ → **made order-independent**
      - *Deviation from the plan, deliberately.* Throwing would catch the ordering bug but
        force every caller to register actions before binding, which is the constraint that
        caused the defect. Bindings are now stored regardless of order and fire once the
        action exists, and an action nothing maps to reads as inactive instead of throwing
        `KeyNotFoundException`. Strictly better than the old silent drop.
      - *Still open:* a typo'd action name binds to nothing silently. Catching that needs an
        action registry rather than raw strings — not attempted here.
- [x] **GE-10** Guard `actionBindings` list mutation
      - Bindings are copy-on-write `IReadOnlyList`s now, so a dispatch pass iterates an
        immutable snapshot and a concurrent `BindAction` cannot mutate it underneath.
- [x] **GE-52** Default `CText.Text` to `string.Empty`
- [x] **GE-53** Guard `delay <= 0` in `Animation.GetSourceRect`

---

## Phase 2 — Performance (~half day)

- [x] **GE-11** Uniform-grid broad phase in `PhysicsSystem`
      - [x] Benchmarked before/after
      - **Approach:** cell size is the largest bounding-box extent in the scene, so an entity
        spans at most 2x2 cells. Candidates are deduped with a `lastPairedWith` marker array
        and sorted, which makes the emitted `CollisionEvents` sequence byte-identical to the
        old exhaustive loop — the grid is a pure optimisation with no observable behaviour
        change. Verified by characterisation tests written against the brute-force version
        first, covering random scenes, entities stacked on one point, wildly mismatched sizes,
        and negative/fractional coordinates.
      - **Measured:** 3000 entities 13.89 -> 2.61 ms/frame (5.3x), 2000 5.57 -> 0.98 ms (5.7x),
        1000 1.13 -> 0.31 ms (3.7x). Still 0 bytes/frame. At constant density the cost is now
        roughly linear (~0.6-1.3 us per entity from 1k to 16k) where it was quadratic.
      - **Caveat:** below ~500 entities the grid overhead cancels the saving (0.26 -> 0.275 ms
        at 500). Not worth a brute-force fallback — both are far inside a frame budget there.
      - **Visible elsewhere:** the demo test suite dropped from 38 s to 6 s, almost entirely
        `SceneBasic`'s 3000-entity stress scene.
- [x] **GE-14** `TargetFrameRate` → instance property; sane per-runner defaults
      - Avalonia desktop 14,400 -> 240 and MAUI 1,440 -> 240; both were spin-burning a core
        for frame budgets no display can use. WinForms keeps 1,000, the editor 120.
      - `SceneMenu` changes the frame rate from inside a scene and had no engine reference, so
        `Scene.Engine` was added (set during `ApplySceneChange`). Small API growth, but the
        alternative was leaving a global that runners fight over.
- [x] **GE-15** Cache `Dictionary<Type, ISystem>` in `SystemContainer`; de-LINQ `GetEntityWithTag`
      - `Add`/`Get`/`TryGet`/`Contains` are dictionary lookups now instead of LINQ scans with
        closure allocations, and `Get<T>` runs every frame from `Engine.Update` plus once per
        input event in every runner.
- [x] **GE-16** Single compacting pass in `EntityManager.Update`
      - `entities.Remove` per dead entity was O(n·m). Compaction is stable, which the draw
        order from GE-03 depends on.
- [x] **GE-23** Consider plain `Dictionary` for `Entity.Components` — **settled 2026-08-04**, landed as `e7fc359` (#70)
      - *Was wontfix:* the editor read entity components from the UI thread while the engine
        thread mutated them, which `ConcurrentDictionary` tolerates and a plain `Dictionary`
        does not.
      - **That reader is gone.** Entity picking now runs on the engine thread through
        `Engine.Post`, and the editor holds an immutable `EntitySnapshot` instead of a live
        `Entity`. An audit of `GameEngine.Editor` finds no remaining off-thread access: the only
        live-entity reads are inside `PickEntity` (engine thread) and the design-time
        constructor (its own `EntityManager`, no engine running).
      - **Still a decision, not a foregone conclusion.** The swap should be justified by
        measurement rather than by the race being gone — `Entity` construction cost and
        per-entity allocation are what the original finding claimed. Worth a benchmark before
        changing a type that every component access goes through.
      - **Measured (`ComponentStorageBenchmarks`, Apple M4 / .NET 10), and it is not one-sided:**

        | Pattern | `ConcurrentDictionary` | `Dictionary` |
        |---|---:|---:|
        | Create 1000 stores | 162.4 us / 1,104,000 B | 34.6 us / 376,000 B |
        | 4000 lookups | 13.97 us | 17.41 us |
        | Enumerate keys | 73.1 us / 104,000 B | 3.86 us / 0 B |

        Creation and key enumeration favour the plain dictionary heavily — 728 bytes per
        entity saved, and `ConcurrentDictionary.Keys` materialises a snapshot list on every
        access, which `EntityManager.Update` and `Entity.Capture()` both hit on live paths.
        But **lookups are ~20% faster on the concurrent type**, re-run with the default job
        and tight error bars, so that is a real result rather than short-run noise.
      - **So it depends on the scene.** Static scenes do far more lookups than creations
        (~3.4 us per frame at 1000 entities in favour of keeping it — negligible); spawn-heavy
        scenes pay per entity created and destroyed. Settling it needs the swap measured end
        to end against an entity-churn benchmark, which is the next step rather than a
        conclusion this microbenchmark can reach on its own.
- [x] **GE-50** Clear the array tail in `RenderSnapshot.Reset`

---

## Phase 3 — Architecture (~2 days)

- [x] **GE-21** Extract one `ViewportTransform` consumed by render **and** input
      - [x] Removes 4 copies of letterbox math
      - [x] Fixes clicks landing wrong under `Stretch` / `Crop`
      - [x] Editor hit test (`GE-33`) consumes it too
      - `InputManager` gained a `ScalingStrategy` that `Engine` syncs from `RenderOptions` on
        every scene change, so input and rendering cannot disagree about the mapping.
      - `Stretch` now genuinely scales each axis independently. The old renderer applied the
        horizontal factor to both, which was scale-to-width, not stretch. Nothing in the repo
        selects anything but `Letterbox`, so no behaviour anyone relies on changed.
- [x] **GE-22** Delete the legacy render path and the orphaned `Runner.Avalonia.Old`
      - ~180 lines of duplicated rendering plus the whole orphaned project. `RenderSystem` no
        longer takes an `EntityManager` at all, which also removes a reference that went stale
        on every scene change.
- [x] **GE-17** `Engine.Stop()` + `IDisposable`; exit `RunLoop` cleanly
      - `Stop()` is distinct from `SetRunning(false)`: pausing is resumable, stopping is not.
        Both loops now exit on it and `Dispose` stops then releases the systems, so the editor
        no longer leaks a thread per engine it creates.
- [x] **GE-18** Stop replacing `EntityManager` / `InputManager` on scene change
      *(or document the invariant explicitly if the rebuild is intentional)*
- [x] **GE-19** Collapse `Scene.Update` to the `SystemContainer` overload
      - Both overloads were invoked every frame. Five demo scenes moved to the surviving one
        and pull `PhysicsSystem` off the container themselves.
- [x] **GE-20** Break the runner → `GameEngine.Demo` dependency; inject the startup scene
      - Scoped to the shared `GameEngine.Runner.Avalonia` *library*, which four heads consume;
        that is where the coupling actually hurt. `App.StartupScene` is a `Func<Scene>` the
        heads set, following the `App._fileFetcher` pattern already there — `GameView` comes
        from XAML so constructor injection was not available.
      - Android and Browser previously referenced only Demo's *assets* and got the code
        transitively through the shared library, so both needed explicit project references
        once that was cut.
      - WinForms and MAUI still reference Demo, correctly: those projects *are* the app head.
- [x] **GE-40** Extract shared swipe/tap classification
      - `SwipeGesture.Classify` in Core: pure geometry, no UI types, so both runners and the
        tests can use it. Replaced two verbatim ~35-line copies.
      - MAUI still has no gesture handling; wiring it in is a feature, not a duplication fix,
        so it was left out.
- [x] **GE-24** `SystemContainer` should match assignable types, not exact
      - Exact-key lookup first, then an assignability scan, so a subclassed system resolves
        through its base type without giving up the dictionary fast path.
      - `MissingSystemException` and `DuplicateSystemException` were `internal` while being
        thrown from `public` methods, so callers could not catch them by type. Both are public
        now.
- [x] **GE-34** Replace static `Assets._fileFetcher` with an injected `IAssetSource`
      - Merged as `805a07c` (PR #62). CI green on both jobs: 222 tests on Linux, and the
        Android emulator reported *"Android app presented an engine frame and remained
        alive"* — the packaged asset path verified on the platform it exists for.
      - **Shape:** `IAssetSource.Open(string path)` is the single read primitive. `Engine` takes
        one (defaulting to `FileAssetSource`) and exposes it; `Scene.AssetSource` reads it back,
        so a scene's `new Assets("assets.txt", AssetSource)` needs no global. `Assets` carries
        its source and exposes `Open`, which is how `LevelFile.LoadFromFile(path, source)` and
        `Scene2` reach packaged level JSON.
      - **Platform behaviour is unchanged by construction.** Every head's existing path-mapping
        lambda was moved verbatim into a `DelegateAssetSource` — Android's `game/` prefix,
        browser's `avares://` mapping, MAUI's `Assets/` vs `Assets/game/` split, the editor's
        project-relative resolution. Nothing about *where* bytes come from was rewritten; only
        *how* the mapping reaches the engine.
      - **The one behavioural decision** is `FileAssetSource`: it opens rooted or
        already-existing paths as given and resolves everything else under `assets/`. That
        reproduces the old split between `File.ReadAllLines("assets.txt")` and
        `SKBitmap.Decode("assets/" + path)` without needing to know which kind of path it holds.
        A level file that exists only under `assets/` now resolves where it previously threw —
        a superset, not a change to a working path.
      - **Also fixed the original register complaint:** `LevelEditorViewModel`'s design-time
        constructor built a fetcher pointing at `GameEngine.Demo/` and left it installed
        globally, so the editor's *runtime* preview inherited a design-time asset root. It now
        builds a local source, and a missing demo tree degrades to no preview entity instead of
        taking the designer down.
      - **Verified:** 219 tests pass (9 new, covering root resolution, existing-file precedence,
        rooted paths, custom roots, delegate pass-through, `Assets` wiring, and that a scene
        receives the engine's source). The demo suite exercises the real `FileAssetSource` path
        for every shipped scene, so texture loading is covered end to end. Android, iOS and MAUI
        still cannot be built on this machine; the Android emulator smoke job in CI is what
        confirms the packaged path.
      - `AudioSystem` takes the source too, though `Mix_LoadWAV` still needs a filesystem path,
        so audio remains desktop-only regardless.
- [x] **GE-25** Move `LevelBuilderExamples` out of the engine library
      - Deleted rather than moved: 99 lines of maze generation and `Console.WriteLine`
        validators with no callers anywhere in the solution.

---

## Phase 4 — Level system consolidation (~1 day)

Depends on `GE-04` landing first.

- [x] **GE-27** Collapse three level paths into one; delete `EntityData.ToEntity`
      - The static duplicated `LevelLoader.CreateEntity` and was used only by the editor's
        design-time constructor, which now goes through `LevelLoader` like everything else.
- [x] **GE-28** Register `CText`, `CCamera`, `CGravity` in `ComponentFactory`
      - All eight component types are now expressible in a level file. `CTransform` also
        gained `scale` and the `layer` introduced by GE-03, both optional so existing levels
        still load. `EntityBuilder` gained matching `AddText`/`AddCamera`/`AddGravity`/`AddMovement`.
- [x] **GE-29** Move hardcoded `player` / action names / `Hit` sound out of `LevelLoader`
      - `LevelLoader` ships with no entity handlers at all now; wiring a tag called "player"
        to WASD and a sound called "Hit" is game policy. `SceneJson` registers it itself
        through the `RegisterEntityHandler` hook that already existed.
- [ ] ~~**GE-30** `SaveToFile` honours the asset source~~ — **deferred**
      - `Assets.OpenAsset` is a *read* abstraction for packaged assets, and the platforms that
        install a fetcher (browser, Android, iOS) have read-only bundles. Saving is a desktop
        and editor operation working on absolute paths from a file picker. Making this
        symmetric needs a separate write abstraction that nothing currently wants.
- [x] Round-trip test covering every registered component type

---

## Phase 5 — Editor (~1 day)

- [x] **GE-32** Delete the stray `InitializeSystems()` — bounding boxes will draw again
      - Deeper than it looked: `ApplySceneChange` also rebuilds systems, so the option was
        discarded on *every* scene change, not just at startup. `RenderOptions` is owned by
        `Engine` now and reused across rebuilds, so editor settings persist.
- [x] **GE-33** Fix the non-bbox hit test (consume `ViewportTransform` from `GE-21`)
      - The sprite branch was offset by a full sprite size. Both branches now test
        `Position .. Position + size` against one shared viewport mapping.
- [x] **GE-38** Move `192.168.2.169:8000` to configuration
      - `ComfyUiClient` resolves from `GAMEENGINE_COMFYUI_URL`, falling back to
        `http://localhost:8000`. Also removed a 59-line commented-out usage block that carried
        the same address.
- [x] **GE-37** Stop the engine and clear the scene reference before `_alc.Unload()`
      - `Engine.Stop()` now joins its desktop loop thread, and `Dispose()` clears the current
        and pending scenes plus input/entity state after the loop has exited. The editor raises
        a synchronous reload handoff that disposes the preview engine and drops its cached
        runtime `Type` list before `SceneProjectCompiler` unloads the collectible context.
      - A blocking-system regression test proves `Dispose()` does not return while scene/system
        code is still executing. The two render-handoff tests now dispose their engines instead
        of leaving paused background threads alive in the test process.
- [x] **GE-35** Split `LevelEditorViewModel` (743 lines → compilation / level editing / selection / dialogs)
      - **Done 2026-08-04.** 755 lines became a 113-line composition root plus four focused types:
        `SceneCatalogViewModel` (146) owns scene discovery, compilation and the code-editor
        dialog; `LevelDocumentViewModel` (256) owns level-file load/save and entity/component
        editing; `LevelComponentViewModel` (287) holds the two per-item view models;
        `EntityInspectorViewModel` (40) owns the live-entity selection; `EditorStatus` (14) is
        the status line the first two both write to.
      - Children take `Func<string?> projectPath` and the shared status rather than reaching
        back through the parent, so each is constructible on its own — which is what made the
        existing tests re-point at `LevelDocumentViewModel` directly.
      - **`EngineView` needed no changes at all.** The root still exposes `SceneSelected`,
        `ScenesReloading`, `IsEngineRunning`, `EntitySelectedCommand` and `AssetEditorViewModel`,
        forwarding to the children. That kept the one untested consumer out of the blast radius.
      - Removed `ZoomLevel` in passing — a property with no binding and no reader anywhere.
      - **How the XAML half is verified:** the project sets `AvaloniaUseCompiledBindingsByDefault`
        and the view declares `x:DataType`, so all 48 bindings are resolved at compile time.
        Confirmed rather than assumed: pointing one binding at a non-existent property fails the
        build with `AVLN2000 ... on type LevelDocumentViewModel`.
      - **Not covered by any of this:** the scene-compilation path and the dialog it opens have
        no tests, and the editor cannot be driven headlessly here. The editor was launched and
        came up clean with no project loaded, which exercises construction and XAML load but not
        compile-and-preview.
      - *Superseded note, kept for history:* `GameEngine.Editor.Tests` now exists — 15 headless tests over the
        level-editing surface (entity/component add/remove and selection behaviour, the
        `RawJson` ↔ typed-property sync, and cross-checks that `ComponentFactory` loads what the
        editor writes with the same values). That is the behaviour a split has to preserve.
      - Two things the test host needs, both discovered by writing it: ReactiveUI 24 must be
        initialised explicitly (`RxAppBuilder.CreateReactiveUIBuilder().WithCoreServices().BuildApp()`)
        because the app only gets it through Avalonia's `UseReactiveUI()`, and `RxSchedulers`
        must be pointed at `ImmediateSequencer` so commands execute inline. `RxApp` is gone in
        this version, renamed to `RxSchedulers`.
      - `AssetEditorViewModel(IFilePickerService)` already had the seam that makes this
        constructible headlessly; a `StubFilePicker` is all the test host adds.

---

## Found later — Linux / CI

- [x] **GE-59** Linux could not load `libSkiaSharp`
      - Found by running the suites on the remote Docker host. `SkiaSharp.NativeAssets.Linux`
        was referenced only by the Avalonia Desktop head, so neither test project pulled the
        native library in. The host process crashed part-way, reporting "4 failed, 10 total"
        instead of running all 196 tests.
      - **This blocked CI**, which runs on `ubuntu-latest` — fixing the SDK version in GE-45
        was necessary but not sufficient.
      - Both test projects now reference the package, conditioned on Linux so Android, iOS and
        browser output is untouched. The workflow installs `libfontconfig1`, without which the
        native library still fails to load.
      - Verified: 140 + 3 skipped and 53 pass in a `mcr.microsoft.com/dotnet/sdk:10.0`
        container. `tests.Dockerfile` reproduces it.

- [x] **GE-60** Pin `Tmds.DBus.Protocol` to the patched 0.21.3 backport
      - The desktop runner's Avalonia 11.3.12 graph resolved 0.21.2, which NuGet flags for
        CVE-2026-39959 (high severity). Central transitive pinning keeps Avalonia on its current
        compatible dependency line while selecting the upstream 0.21.3 security backport.
      - Verified with `dotnet nuget why`, `dotnet list package --vulnerable --include-transitive`,
        and a clean desktop runner build.

## Found later — unequal collision boxes

- [x] **GE-61** Resolve unequal-size overlaps with a true minimum translation
      - `Physics.GetOverlap` measures intersection width/height. The resolver incorrectly used
        those values as penetration depths; once a 20x20 grenade was contained by SceneBasic's
        50x80 Jeep, both values became 20 and the tie selected Y, even for a side impact.
      - Resolution now computes the signed distance to each of the four exit edges, chooses the
        shortest axis, and uses relative movement to break equal-depth containment ties.
      - Four regression cases cover the Jeep approaching a grenade from left, right, top and
        bottom. All collision-resolution, core and demo tests pass.

---

## Phase 6 — Polish

- [x] **GE-46** README: what it is, running each runner, writing a scene, level JSON format
      - The scene example is extracted and compiled as part of writing it, so it cannot rot
        into something that no longer builds.
- [x] **GE-47** Clear nullable warnings; `<TreatWarningsAsErrors>` on Core and Demo
      - Core is at zero warnings and now fails the build on new ones. `InitializeSystems` is
        annotated `[MemberNotNull]`, which is the honest fix — it really does assign those
        three fields — rather than silencing with `null!`.
      - Follow-up adversarial pass cleared the remaining 21 Demo warnings without suppressions
        and enabled `<TreatWarningsAsErrors>` there too. Missing runtime entities now return
        safely, while helper methods use explicit initialized-asset invariants.
- [x] **GE-51** `Vec2` cleanup
      - Zero-vector `Normalize()` returned NaN and now returns zero. `Magnitude()` returned
        `float` while `Length()` returned `double`, so normalising silently lost precision;
        they agree now. Removed no-op `Clone()` and the length-comparing `<`/`>` operators —
        the one caller was using `> Vec2.Zero` to mean "is set", which now says so directly.
      - Added `LengthSquared`, `DistanceTo`, `Lerp`.
- [x] **GE-54** Error handling in `Assets.ParseLine`; skip blanks, support `#` comments
      - Also reports unknown directives and short argument lists instead of throwing
        `IndexOutOfRangeException` from inside the parser.
- [x] **GE-55** Dispose static `SKPaint`s; plan the SkiaSharp 3.x migration
      - Closed by the 2026-08-04 upgrade wave, which went past 3.x to SkiaSharp **4.151.0**.
        The 2.x APIs this finding was really about are gone: text draws through an `SKFont`
        (`RenderSystem._textFont`, `_fpsFont`), and `FilterQuality` became
        `SKSamplingOptions`. `CText` keeps a paint for colour and drops `TextSize`/`TextAlign`
        into its own fields.
      - The remaining literal half — disposing process-lifetime static paints — stays as
        judged: moot for singletons that live as long as the renderer.
- [x] **GE-26** Flatten the `Exceptions` container class
- [x] **GE-43** WinForms reported the Form's `Bounds`, chrome included, as the canvas size
      - Now takes `skglControl1`'s own size and listens to that control's `SizeChanged`, so the
        viewport mapping matches what is actually drawn.
- [x] **GE-41 / GE-42** Runner cleanups (static engine field, uncancellable synthetic key-ups)
- [x] **GE-44** MAUI system-wide keyboard hook — **resolved by removing the host (2026-08-04)**
      - Previously deferred: replacing the hook needed MAUI's own keyboard handling, a feature
        rather than a fix, and MAUI could not be built on this machine.
      - The host was deleted instead. Its platform coverage was a strict subset of the Avalonia
        heads (`net10.0-android` plus a Windows-only `net9.0-windows` target; iOS, MacCatalyst
        and Tizen were commented out), it was ~94 lines of real code that hardcoded `ScenePong`
        and had no gesture handling at all — so the Android app it produced had no touch input
        on a touch platform — and every commit touching it since creation was maintenance.
      - Removing it also took `SharpHook` out of the repo entirely (the Avalonia runner's copy
        went with GE-62), dropped the only project still on .NET 9, and left `GameEngine.Runner.Winforms`
        as the non-Avalonia consumer that proves the engine isn't coupled to one UI framework.
- [x] **GE-57 / GE-58** Correct `IMPROVEMENTS.md` §1.1 and §1.2 so they aren't actioned as written
      - Both now carry an inline correction, and the document is marked superseded.

---

## Test backlog (GE-48)

Cross-cutting; grows through phases 1–4. Target coverage for the currently-bare
areas:

- [x] `EntityManager` — IDs, lifecycle, query caching, snapshot construction
      — `Unit/EntityManagerTests.cs`, `Unit/EntitySnapshotTests.cs`
- [x] `LevelFile` / `LevelBuilder` — round trip, every component type, malformed input
      — `Unit/LevelFileTests.cs`
- [x] `PhysicsSystem` — overlap math, resolution symmetry, gravity units
      — `Unit/Systems/PhysicsSystemTests.cs`, `Unit/Systems/CollisionResolutionTests.cs`,
        `Unit/Systems/BroadPhaseTests.cs`, `Integration/Systems/PhysicsAndMovementSystemTests.cs`
- [x] `Engine` — scene change, start/stop, delta clamping
      — `Unit/EngineLifecycleTests.cs`, `Unit/EngineThreadWorkTests.cs`
- [x] `Vec2` — edge cases, especially zero-vector normalise
      — `Unit/Vec2Tests.cs`

---

## Suggested order of attack

**Session 1:** Phase 0 end-to-end, plus the `GE-06` decision. Everything is small
and independent; CI goes green, which makes the rest measurable.

**Session 2:** `GE-01` and `GE-02` with tests first. These are the two that most
undermine trust in the ECS, and both are contained within `EntityManager`.

**Session 3:** `GE-05` (units) then `GE-11` (broad phase). Units first — the
broad-phase benchmark is easier to trust once gravity isn't 1000× off.

Everything after that is comfortably incremental.


---

## Closing state — 2026-08-01

**55 of 61 fixed.** Five findings are deferred and one remains wontfix.

**GE-18** was the last open architectural item and is now fixed: `ApplySceneChange` reuses
the existing `EntityManager` and `InputManager` instead of allocating new ones, so a cached
reference cannot go stale. `InputManager.Reset()` clears actions, bindings and the pointer
queue, which also removed the manual save-and-restore of `RealResolution` that the old code
needed. **GE-48** is closed too — the suites went from 17 tests to 207, covering every area
the original review called out as untested.

### Deferred deliberately

- **GE-34** — *no longer deferred; fixed on 2026-08-04 once Android became testable in CI.*
- **GE-35** — *no longer deferred; done on 2026-08-04 once the editor had tests.*
- **GE-55** — static `SKPaint`s are never disposed, which is moot for process-lifetime
  singletons. The real content of this finding is that SkiaSharp 2.x APIs in use
  (`TextSize`, `TextAlign`, `FilterQuality`, the `DrawText` overload) are gone in 3.x. That
  migration is an upgrade project with real rendering risk, not a cleanup.
- **GE-23** — `wontfix` while the editor still reads components off the UI thread.
- **GE-30** — needs a write abstraction nothing currently wants.
- **GE-44** — *no longer deferred; the MAUI host was removed on 2026-08-04, which resolves it.*

### Verification reach

| Platform | Status |
|---|---|
| macOS | Both suites, all locally-buildable projects |
| Linux | Both suites in Docker, matching macOS exactly (GE-59) |
| Windows | Not verified — audio in particular needs a human |
| Android / iOS | Compile clean; SDKs unavailable here — *iOS half of this claim was false; see GE-64* |

*Superseded for Android by the 2026-08-04 section below.*

---

## Since the closing state — 2026-08-04

Four days of work landed on `master` outside the review branch. None of it was driven by
this register, but it changes what is left in it.

### What landed

| Commit | Effect on this plan |
|---|---|
| `08d04a3` Upgrade rendering dependencies and benchmarks | **Closes GE-55.** Avalonia 11.3.12 → 12.1.1, SkiaSharp 2.x → 4.151.0, NUnit 4.6.1, Test SDK 18.8.1. Adds a `GameEngine.Benchmarks` runner with its own README. |
| `af55f07` / `e07cad4` Mobile API fixes, raise Android min SDK | Fallout from the Avalonia 12 upgrade. |
| `3d1c1f5` / `4405b65` Publish workflow on .NET 10, browser disk usage | CI publish path caught up to the SDK bump from GE-45. |
| `ac993aa` Fix Android startup and add emulator smoke test | **Unblocks GE-34.** Android now boots on `IActivityApplicationLifetime.MainViewFactory`, SharpHook is kept off mobile, and a `FirstFramePresented` hook is asserted by an emulator job in CI. |
| `fb7b334` Add Astral Relay demo level | A 628-line demo scene plus assets. Covered automatically by the enumerated `DemoSceneTests`. |

`Tmds.DBus.Protocol` is now pinned at **0.94.2** rather than the 0.21.3 recorded under
GE-60 — Avalonia 12.1 moved to the 0.94 line, and 0.94.2 is its security-patched release.
The finding stands as fixed; only the version in its note is historical.

### Current state

- **57 of 62 fixed.** Deferred: GE-30, GE-35, GE-44. Open: GE-62. `wontfix`: GE-23.
  *(Superseded — GE-62, GE-35, GE-44 and GE-23 all closed later the same day; see
  the iOS section below for the current count of 64 of 65.)*
- 219 tests pass, 3 skipped (the audio suite, which needs a mixer), on macOS.
- Android is now verified per-push by an emulator smoke test, not merely compiled.
- **GE-34 landed on the strength of that job** — it was deferred only because the packaged
  asset path could not be verified from this machine.

### Newly observed — not yet in the register

- ~~**GE-62 (S2)**~~ — **fixed 2026-08-04.** The *Avalonia desktop* runner installed a
  system-wide `ReactiveGlobalHook`, the same OS-level key capture GE-44 flags in MAUI;
  GE-44 was written against `GameWindow.cs` only, so the desktop instance was never tracked.
  `GameView` now uses Avalonia's own focused key events — `Focusable`, focus on attach and
  on pointer press, `OnKeyDown`/`OnKeyUp` into `InputSystem`, and held keys released on
  `LostFocus` so alt-tabbing mid-movement cannot leave a key stuck down. `KeyboardHookHelper`
  and the `SharpHook.Reactive` reference are gone from the Avalonia runner.
  - **Also a real input fix:** the hook mapped only W/A/S/D/Space, while demo scenes bind
    Q, E, R, P and N — those keys did nothing on desktop. The map is now derived from
    `GeKeys`, so every letter and Space works.
  - **Verified by play-testing.** Screen capture is unavailable on this machine, so the
    keyboard path was confirmed by a human playing a full round of Astral Relay to a win.
- **Comment drift (S3)** — ~18 explanatory comments and one XML doc block arrived with the
  August work, against the convention that the code carries no comments and prose lives in
  these files.
- ~~**GE-63 (S3)**~~ — **fixed 2026-08-04.** Assets resolved against the process working
  directory, so `dotnet run --project ...Desktop` from the repo root died with an unhandled
  `DirectoryNotFoundException` on the engine thread the first time a scene loaded assets; the
  binary only worked when launched from its own output folder. Found while play-testing
  GE-62, and it predates the asset-source work — `File.ReadAllLines("assets.txt")` failed the
  same way, just with a different exception type.
  - **Fix:** `FileAssetSource` now resolves relative paths against a base directory that
    defaults to `AppContext.BaseDirectory` — where the assets are actually staged — rather
    than wherever the process happens to be running. Rooted paths are still opened as given,
    and the content root stays configurable, so the editor and the tests can point it wherever
    they need.
  - **Chosen over `Directory.SetCurrentDirectory`** (what `MauiProgram` does): mutating
    process-wide state at startup is the same class of global the asset-source work has been
    removing, and it would fix only the heads that remember to call it.
  - **The demo suite is now the regression test.** Its harness used to pin the working
    directory at the staged assets, which hid this bug; it now deliberately runs from a
    directory with no assets in it, so all 56 scenes prove their assets resolve regardless of
    cwd. Confirmed separately by launching the desktop head from the repo root — the exact
    command that crashed — and watching it come up clean.

### Where next

1. ~~**GE-34**~~ — done; see Phase 3. The Android smoke job is the remaining evidence, so
   the first CI run on this change is worth watching.
2. **GE-62** — decide whether desktop key capture should be a focused input path rather
   than a global hook. It is the only remaining finding that is a user-facing privacy
   question rather than a code-quality one.
3. **GE-35** — split `LevelEditorViewModel`, now 755 lines and still untested. The
   risk-to-benefit that deferred it has not changed; it needs editor test coverage first.
4. **GE-44** — still blocked on MAUI keyboard handling, not on buildability.
5. **GE-30 / GE-23** — unchanged; both wait on a design shift that nothing wants yet.

---

## Editor inspector snapshot — 2026-08-04

The editor read live engine state from the UI thread. Not just the inspector: `SelectEntityAt`
walked `EntityManager.GetEntities()` and probed components on the UI thread while the engine
thread mutated them, then handed the `Entity` itself to the view model, which kept reading it
for as long as it stayed selected.

**Engine gained one primitive:** `Engine.Post(Action<Engine>)` queues work for the engine
thread, drained at the top of `Tick` *before* the paused early-return — the editor pauses the
preview and still expects picking to work. Bounded at 256 like the pointer queue, so a stalled
engine cannot grow it without limit; `Post` returns false when it refuses.

**`Entity.Capture()` returns an `EntitySnapshot`** — id, tag, active flag and sorted component
type names, all immutable. That is exactly what the inspector displayed, so nothing was lost by
copying instead of referencing.

**The pick moved wholesale onto the engine thread.** `EngineView` now posts the hit test; the
engine thread runs it, captures a snapshot, and marshals only that back through
`Dispatcher.UIThread.Post`. Clicking empty space still leaves the previous selection alone.

**Measured cost:** none worth reporting. A pick is one queued delegate per click, and the
snapshot is a handful of strings.

**Tests:** 11 new in Core (posted work runs on the next tick and not on the posting thread,
runs while paused, runs in order, sees engine-owned entities, is bounded, is refused after
dispose, and frees capacity when drained; snapshots record identity and are unaffected by later
mutation) and 3 in the editor (the inspector exposes snapshot fields, clears on null, and holds
no live entity reference).

**Not covered:** the click path end to end. Driving a real pointer press through the editor
needs a UI harness that does not exist here, so `SelectEntityAt` itself is verified only by the
editor launching clean.

---

## GE-23 settled — 2026-08-04

The microbenchmark said the answer depended on the workload. The end-to-end A/B says it does
not: measured through the real `EntityManager` and `Entity` accessors, with both variants run
in one session on the same machine.

| Benchmark | `ConcurrentDictionary` | `Dictionary` | Change |
|---|---:|---:|---:|
| Spawn 1000 entities | 401.2 us / 1,651,580 B | 208.5 us / 843,559 B | -48% time, -49% bytes |
| Spawn then despawn 1000 | 535.1 us / 1,772,151 B | 254.9 us / 860,127 B | -52% time, -51% bytes |
| Steady-state lookups (4000) | 12.69 us | 12.09 us | -5% |
| BuildRenderSnapshot | 159.7 us | 153.0 us | -4% |
| PhysicsUpdate | 75.65 us | 71.62 us | -5% |

**Every path improves, including lookups** — the one result that had argued for keeping the
concurrent type. Its read advantage in isolation does not survive going through
`TryGetComponent<T>()` on real entities, where the working set differs and `typeof(T)` is a
constant. Had the microbenchmark been the whole story, the wrong call would have been obvious
and confident.

`Entity.Components` is a plain `Dictionary` now. The type change is one line; every API in use
(`indexer`, `ContainsKey`, `TryGetValue`, `Remove(key, out value)`, `Keys`) exists on both.

**Safe because the readers are gone, not because it was always safe.** The engine thread owns
entities; the editor picks through `Engine.Post` and holds `EntitySnapshot`; runners reach input
through the pointer queue and rendering through `RenderSnapshot`.

*Measurement note recorded in the benchmarks README:* the older dependency-upgrade numbers in
that file are not comparable with these — the same `BuildRenderSnapshot` reads 300 us there and
160 us here on unchanged rendering code. Both sides of a comparison have to be measured in one
sitting.

---

## iOS head verified — 2026-08-04

The last unverified platform. Xcode was installed on this machine for the first time, which
turned "iOS compiles clean" from an assumption into something testable — and it was wrong.

### Two S1 defects the register had never seen

- **GE-64** — the head **did not compile**. `AppDelegate.CustomizeAppBuilder` called
  `.WithInterFont()`, which lives in `Avalonia.Fonts.Inter`. No project in the solution
  references that package, and no other head calls it. Template boilerplate that had never
  been through a compiler.
- **GE-65** — the head **packaged no assets at all**. No `BundleResource` items and no
  `App.AssetSource`. Android declares `AndroidAsset` items and installs a `DelegateAssetSource`
  in `MainActivity.cs:31`; Browser does the equivalent with `AvaloniaResource` plus an
  `avares://` mapping. iOS did neither, so the first scene to load a texture would have thrown
  the same class of failure as GE-63.

Both were invisible because "compiles clean" had been inferred from the project restoring, not
from a build. The lesson worth keeping: a platform with no toolchain on the machine is
*unverified*, not *passing*.

### The fix is small because GE-34 and GE-63 did the work already

`FileAssetSource` takes an optional base directory (GE-34) and resolves relative paths against
it rather than the working directory (GE-63). On iOS the app bundle root *is* the content root,
so:

```csharp
App.AssetSource = new Core.FileAssetSource(NSBundle.MainBundle.BundlePath);
```

**iOS needs no `DelegateAssetSource`** — unlike Android and Browser, whose bundles impose a
prefix that has to be mapped. Three `BundleResource` items reproduce the desktop output layout
verbatim (`assets.txt` at the bundle root, `assets/**`, `levels/**`), and because that layout is
what `FileAssetSource` already expects, the existing engine code works untouched.
`NSBundle.MainBundle.BundlePath` is used in preference to `AppContext.BaseDirectory` so the
content root is stated rather than assumed to coincide with the bundle.

Total diff: 2 files, +9/-10. The three unused usings and the template comment block went with it.

### Verified

Built for `iossimulator-arm64`, installed and launched on an iPhone 17 simulator (iOS 26.5).
`SceneMenu` renders at 78.5 FPS. Because that scene is text-only and proves nothing about
assets, `StartupScene` was temporarily pointed at `SceneJson` — which loads textures *and* level
JSON — and confirmed rendering sprites from `assets/images/` with geometry from
`levels/level*.json`. That exercises `assets.txt` parsing, texture decode and level loading
together. Reverted to `SceneMenu` and rebuilt clean.

**Touch input confirmed by play-testing.** `GameView.cs:174` maps gestures to keys through
`SwipeGesture.Classify`, so a tap reaches `SceneMenu`'s `Space`/"Go" binding and a swipe drives
the `W`/`S` selection. Verified working across the demos by a human on the simulator.

It could not be verified from here: macOS refused synthetic clicks (accessibility error
`-25204`) and `simctl` has no tap primitive, so there is no way to drive a pointer through this
head programmatically. Automated coverage of the touch path would need a UI-automation harness
that does not exist in this repo — the same gap that leaves `SelectEntityAt` in the editor
verified only by launching clean. Worth knowing before anyone assumes an iOS CI job could
smoke-test input rather than just startup.

**The platform is now fully verified: build, startup, asset loading and input.**

### Toolchain requirements, recorded because they are not obvious

- .NET for iOS pins an exact Xcode version. Xcode **26.6** requires .NET for iOS
  **26.5.10315**, which ships only in workload set **10.0.302.1** — a 10.0.3xx band set. SDK
  10.0.100 cannot reach it no matter how much `dotnet workload update` runs; the SDK itself has
  to move feature band.
- `dotnet workload update --version <set>` updates *manifests* but does not install workload
  packs for the new band. `dotnet workload install ios android wasm-tools` is the second step.
  `maui-android` is deliberately not installed — the MAUI host went away with GE-44.
- Deleting `sdk-manifests/<band>` before running `dotnet workload clean` makes the GC throw
  `Could not find a part of the path` partway through and abandon collection. Recreate the
  directories the installation records point at, then re-run clean.
