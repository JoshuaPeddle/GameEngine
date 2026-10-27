# Findings Register

Stable IDs for every finding from the [2026-07-31 review](01-review.md).
Reference these in commits: `fix(core): monotonic entity IDs (GE-01)`.

**Status values:** `open` · `in progress` · `fixed` · `wontfix` · `deferred`

**Severity:**

| | Meaning |
|---|---|
| **S1** | Wrong behaviour users will hit; data loss; broken CI |
| **S2** | Significant perf, architecture, or correctness risk |
| **S3** | Quality, maintainability, cleanup |

**Confirmed** = verified directly rather than inferred from reading — by the
harness in [`04-evidence.md`](04-evidence.md), by compiler/build output, or by
an exhaustive grep. Unmarked rows are code-reading judgements.

---

## Core — correctness

| ID | Sev | Finding | Location | Confirmed | Status |
|---|---|---|---|---|---|
| GE-01 | S1 | Entity IDs collide after removal; `GetEntity(id)` is a list index | `EntityManager.cs:66,78` | ✅ | **fixed** |
| GE-02 | S1 | `GetEntitiesWith<T>()` returns one shared mutable list to all callers | `EntityManager.cs:190` | ✅ | **fixed** |
| GE-03 | S1 | Draw order = `HashSet` slot order; unstable after respawn; makes collision resolution order-dependent | `EntityManager.cs:232`, `PhysicsSystem.cs:37` | ✅ | **fixed** |
| GE-04 | S1 | `LevelBuilder` output cannot be loaded back — no `type` field written | `LevelBuilder.cs:102`, `LevelFile.cs:105` | ✅ | **fixed** |
| GE-05 | S1 | Gravity is 1000× documented value; `deltaTime` is ms in physics, s in movement | `CGravity.cs:5`, `PhysicsSystem.cs:77` | ✅ | **fixed** |
| GE-06 | S1 | Audio entirely dead (`if (false==true)`); `_audioEnabled` never read; null passed to non-nullable param | `Engine.cs:19,112,137,280` | ✅ | **fixed** |
| GE-07 | S1 | Pointer callbacks execute scene logic on the UI thread | `InputManager.cs:88` | ✅ | **fixed** |
| GE-08 | S1 | `BindAction` before `AddAction` silently dropped, no diagnostic | `InputManager.cs:47` | ✅ | **fixed** |
| GE-09 | S2 | `HandlePointerEvent` throws if resolution not yet set | `InputManager.cs:93` | ✅ | **fixed** |
| GE-10 | S2 | `actionBindings` `List` values mutated across threads | `InputManager.cs:14` | — | **fixed** |
| GE-61 | S1 | Unequal-size collision resolution treats intersection size as penetration depth; a contained small collider redirects the larger body to the wrong axis | `PhysicsSystem.cs` | ✅ | **fixed** |

## Core — performance

| ID | Sev | Finding | Location | Confirmed | Status |
|---|---|---|---|---|---|
| GE-11 | S1 | O(n²) collision broad phase — 14.49 ms/frame at 3000 entities | `PhysicsSystem.cs:37` | ✅ | **fixed** |
| GE-12 | S2 | `FpsSmoothingSamples = 1000` — counter takes ~1000 frames to converge | `Engine.cs:135` | ✅ | **fixed** |
| GE-13 | S3 | FPS smoothing re-sums all samples every frame; not O(1) as commit `34b2ce3` claims | `RenderSystem.cs:50` | ✅ | **fixed** |
| GE-14 | S2 | `Engine.TargetFrameRate` is `static`; Avalonia desktop sets 14,400 → spin-burns a core | `Engine.cs:100` | — | **fixed** |
| GE-15 | S2 | Per-frame LINQ: `SystemContainer.Get<T>`, `GetEntityWithTag` | `SystemContainer.cs:23`, `EntityManager.cs:88` | — | **fixed** |
| GE-16 | S3 | `EntityManager.Update` removal is O(n·m) | `EntityManager.cs:49` | — | **fixed** |

## Core — architecture

| ID | Sev | Finding | Location | Confirmed | Status |
|---|---|---|---|---|---|
| GE-17 | S2 | `Engine` has no `Stop()` / `IDisposable`; `while(true)` loop leaks a thread per instance | `Engine.cs:191` | — | **fixed** |
| GE-18 | S2 | `ApplySceneChange` replaces `EntityManager` and `InputManager`; cached refs go stale | `Engine.cs:272` | — | **fixed** |
| GE-19 | S2 | `Scene` has two `Update` overloads, both invoked every frame | `Scene.cs:11-12`, `Engine.cs:256` | — | **fixed** |
| GE-20 | S2 | Every runner has a compile-time dependency on `GameEngine.Demo` | all runners | — | **fixed** |
| GE-21 | S2 | Letterbox math duplicated 4×; input path ignores `ScalingStrategy` → clicks misplaced under Stretch/Crop | `RenderSystem.cs:70`, `InputManager.cs:99`, `EngineView.cs:47` | — | **fixed** |
| GE-22 | S3 | ~180 lines of legacy render path; only caller is orphaned `Runner.Avalonia.Old` (not in solution) | `RenderSystem.cs:118-428` | ✅ | **fixed** |
| GE-23 | S3 | `Entity.Components` is a per-entity `ConcurrentDictionary` for single-threaded access | `Entity.cs:12` | ✅ | **fixed** |
| GE-24 | S3 | `SystemContainer.Get<T>` exact type match — subclassed systems don't resolve | `SystemContainer.cs:23` | — | **fixed** |
| GE-25 | S3 | `LevelBuilderExamples` (maze gen, console validators) ships inside the engine library | `LevelBuilder.cs:122` | — | **fixed** |
| GE-26 | S3 | `Exceptions` is a non-static class used as a namespace container | `Exceptions.cs:3` | — | **fixed** |

## Level system

| ID | Sev | Finding | Location | Confirmed | Status |
|---|---|---|---|---|---|
| GE-27 | S2 | Three overlapping level paths; `EntityData.ToEntity` duplicates `LevelLoader.CreateEntity` | `LevelFile.cs:20,209` | — | **fixed** |
| GE-28 | S2 | `ComponentFactory` covers 5 of 8 components — `CText`/`CCamera`/`CGravity` unrepresentable | `ComponentFactory.cs:13` | — | **fixed** |
| GE-29 | S2 | `LevelLoader` hardcodes tag `player`, action names, and sound `Hit` | `LevelFile.cs:188,237` | — | **fixed** |
| GE-30 | S2 | `SaveToFile` ignores `_fileFetcher` while `LoadFromFile` honours it | `LevelFile.cs:117` | — | **deferred** — dormant; revisit only when something needs to write assets |
| GE-31 | S2 | `CTransform` rotation written by builder, dropped by factory | `ComponentFactory.cs:33` | — | **fixed** |

## Editor

| ID | Sev | Finding | Location | Confirmed | Status |
|---|---|---|---|---|---|
| GE-32 | S1 | `InitializeSystems()` after config → bounding boxes never draw in editor | `EngineView.cs:127-130` | — | **fixed** |
| GE-33 | S1 | Non-bbox hit test offset by a full sprite size | `EngineView.cs:74` | — | **fixed** |
| GE-34 | S2 | Design-time ctor mutates global `Assets._fileFetcher` and does file IO | `LevelEditorViewModel.cs:88` | ✅ | **fixed** |
| GE-35 | S3 | `LevelEditorViewModel` is 743 lines spanning four responsibilities | `LevelEditorViewModel.cs` | ✅ | **fixed** |
| GE-36 | S3 | `SceneCompiler.cs` is a dead prototype superseded by `SceneProjectCompiler` | `Magic/SceneCompiler.cs` | — | **fixed** |
| GE-37 | S2 | `_alc.Unload()` while a `Scene` from that ALC may still be running | `SceneProjectCompiler.cs:217` | — | **fixed** |
| GE-38 | S2 | Hardcoded LAN address `http://192.168.2.169:8000` in a public repo | `ImageGeneratorControl.axaml.cs:25`, `ComfyUiClient.cs:358` | — | **fixed** |
| GE-39 | S3 | Filename typo `AssetEditorViewMovel.cs` | `ViewModels/` | — | **fixed** |

## Runners

| ID | Sev | Finding | Location | Confirmed | Status |
|---|---|---|---|---|---|
| GE-40 | S2 | ~70-line swipe/tap→key block duplicated verbatim; absent in MAUI | `GameView.cs:98`, `MainView.cs:77` | — | **fixed** |
| GE-41 | S3 | `public static Engine _gameEngine` | `GameView.cs:22` | — | **fixed** |
| GE-42 | S3 | Synthetic key-ups via uncancellable `Task.Delay().ContinueWith()` | `GameView.cs:114`, `MainView.cs:96` | — | **fixed** |
| GE-43 | S2 | WinForms passes Form `Bounds` (incl. chrome) instead of canvas bounds | `MainView.cs:59` | — | **fixed** |
| GE-44 | S2 | MAUI installs a system-wide global keyboard hook | `GameWindow.cs:52` | — | **fixed** (host removed) |
| GE-62 | S2 | Avalonia desktop installs the same system-wide keyboard hook; captures keystrokes while unfocused | `KeyboardHookHelper.cs:28` | ✅ | **fixed** |
| GE-63 | S3 | Assets resolved against the process working directory; launching from anywhere but the output folder died on the first asset load | `FileAssetSource.cs` | ✅ | **fixed** |
| GE-64 | S1 | iOS head does not compile: `.WithInterFont()` needs `Avalonia.Fonts.Inter`, which no project in the solution references | `AppDelegate.cs:22` | ✅ | **fixed** |
| GE-65 | S1 | iOS head packages no assets — no `BundleResource` items and no `App.AssetSource`, so the first scene to load a texture would have thrown | `GameEngine.Runner.Avalonia.iOS.csproj` | ✅ | **fixed** |

## Build, test, CI

| ID | Sev | Finding | Location | Confirmed | Status |
|---|---|---|---|---|---|
| GE-45 | S1 | CI pinned to SDK `9.0.x` while projects target `net10.0` — build fails | `.github/workflows/dotnet.yml:20` | — | **fixed** |
| GE-46 | S1 | No README | repo root | — | **fixed** |
| GE-47 | S3 | ~30 nullable warnings incl. 3× `CS8618` on `Engine` fields, `CS0162` | Core + Demo | ✅ | **fixed** |
| GE-48 | S1 | No tests for `EntityManager`, `LevelFile`, `PhysicsSystem`, `Engine`, `Vec2` — every confirmed bug is in untested code | `GameEngine.Core.Tests/` | — | **fixed** |
| GE-49 | S3 | Empty `StarSim/` dir; stray `tool_test_report.txt` | repo root | — | **fixed** |
| GE-59 | S1 | Linux cannot load `libSkiaSharp`: the test projects never referenced `SkiaSharp.NativeAssets.Linux`, so the test host crashes mid-run — CI runs on `ubuntu-latest` | `*.Tests.csproj`, `.github/workflows/dotnet.yml` | ✅ | **fixed** |
| GE-60 | S1 | Avalonia desktop resolves vulnerable `Tmds.DBus.Protocol` 0.21.2 (CVE-2026-39959, high severity) | `Directory.Packages.props`, Avalonia transitive graph | ✅ | **fixed** |
| GE-66 | S2 | `Basic_PlayerRespondsToInput` is flaky: it asserts input response inside `SceneBasic`, whose 3000 unseeded-random solid colliders sit directly in the player's path | `DemoSceneTests.cs:163`, `SceneBasic.cs:46` | ✅ | **fixed** |

## Code quality

| ID | Sev | Finding | Location | Confirmed | Status |
|---|---|---|---|---|---|
| GE-50 | S3 | `RenderSnapshot.Reset` leaves stale `SKBitmap`/`SKPaint` refs in the array tail | `RenderSnapshot.cs:130` | — | **fixed** |
| GE-51 | S3 | `Vec2`: NaN `Normalize()`, `float` vs `double` length pair, no-op `Clone()`, length-based `<`/`>` | `Vec2.cs` | — | **fixed** |
| GE-52 | S2 | `CText.Text` null by default → NRE at `DrawText` | `CText.cs:10` | — | **fixed** |
| GE-53 | S2 | `Animation`: horizontal strips only, no non-looping mode, divide-by-zero when `delay = 0` | `Animation.cs:39` | — | **fixed** |
| GE-54 | S3 | `Assets.ParseLine` has no error handling; blank line throws; no comment support | `Assets.cs:48` | — | **fixed** |
| GE-55 | S3 | Static `SKPaint`s never disposed; deprecated SkiaSharp 2.x APIs block a 3.x upgrade | `RenderSystem.cs:444` | ✅ | **fixed** |
| GE-56 | S2 | `AudioSystem.Play` does file IO + decode per invocation | `AudioSystem.cs:47` | — | **fixed** |

## Corrections to pre-existing docs

| ID | Sev | Finding | Location | Confirmed | Status |
|---|---|---|---|---|---|
| GE-57 | S2 | `IMPROVEMENTS.md` §1.1 fix is ineffective — `AsReadOnly()` wraps the same cleared list | `IMPROVEMENTS.md:11` | ✅ | **fixed** |
| GE-58 | S2 | `IMPROVEMENTS.md` §1.2 is wrong — disposing shared `SKBitmap`s would cause use-after-free; cites a non-existent path | `IMPROVEMENTS.md:52` | — | **fixed** |

---

## 2026-08-30 vision review

From the [2026-08-30 vision review](05-vision-review.md), which assessed the
repo against the product vision (editor UX, publish-everywhere, embeddability,
LLM-friendliness, licensing) at `8f7c61f`. Severity here reads as distance
from the stated vision for product gaps, and as usual for code defects.

| ID | Sev | Finding | Location | Confirmed | Status |
|---|---|---|---|---|---|
| GE-67 | S2 | Editor round-trips `velocity` on CTransform but `ComponentFactory.CreateCTransform` never reads it — velocity set in the editor is silently dropped when the level loads in-game | `LevelComponentViewModel.cs:171,240`, `ComponentFactory.cs:35` | ✅ | fixed |
| GE-68 | S3 | Shipped levels carry dead keys no loader reads (`speed`/`repeat` on CAnimation, `offset` on CBoundingBox); unknown properties are silently ignored everywhere, so examples teach a vocabulary the engine doesn't speak | `GameEngine.Demo/levels/level1.json`, `ComponentFactory.cs` | ✅ | fixed |
| GE-69 | S3 | Scene-compile diagnostics discarded: `catch (Exception)` → status text "Error loading scenes"; the Roslyn errors `SceneProjectCompiler` formats never reach the user | `SceneCatalogViewModel.cs:136` | ✅ | fixed |
| GE-70 | S3 | Dead, mutually contradictory `AvaloniaVersion` MSBuild properties (root 11.1.0, runner 11.2.3) while central package management pins Avalonia 12.x | `Directory.Build.props:4`, `GameEngine.Runner.Avalonia/Directory.Build.props:3` | ✅ | fixed |
| GE-71 | S3 | publish.yml uses archived actions (`create-release@v1`, `upload-release-asset@v1`), cuts release `1.0.<run>` on every master push, no macOS artifact, browser build reaches Docker Hub only | `.github/workflows/publish.yml` | ✅ | fixed |
| GE-72 | S1 | No LICENSE file (`licenseInfo: null`); no third-party notice for the checked-in `SDL2_mixer.dll`; demo asset licensing unstated — "friendly licensed" is currently unlicensed | repo root | ✅ | fixed |
| GE-73 | S1 | No consumption story: Core has no package identity and nothing publishes to a feed; the only way to build a game is inside this repo imitating `GameEngine.Demo` | `GameEngine.Core.csproj` | ✅ | fixed |
| GE-74 | S1 | No user-facing publish path: CI publishes this repo's demos; a user's game has no route to an APK/wasm/exe; platform heads are hardwired to Demo | `.github/workflows/publish.yml`, runner heads | — | in progress |
| GE-75 | S2 | Runner embeddability undermined by statics: `App.AssetSource`/`App.StartupScene`/`App.FirstFramePresented`, `GameView.Current`; `GameView` always constructs its own engine — one game per process, configured via globals | `App.axaml.cs:15-17`, `GameView.cs:22` | ✅ | fixed |
| GE-76 | S2 | Level/asset formats not agent-hardened: no JSON schema, no strict property validation, hand-written factory drifts from components and editor; `assets.txt` is positional and bespoke | `ComponentFactory.cs`, `Assets.cs` | ✅ | fixed |
| GE-77 | S2 | Audio is Windows-only (hand-placed `SDL2_mixer.dll`; ppy.SDL2-CS ships no mixer natives) — "publish everywhere" ships silent games on 5 of 6 platforms | `GameEngine.Core.csproj` | ✅ | **in progress**: AVFoundation, Android SoundPool/MediaPlayer and browser Web Audio backends implemented with shared mixer and voice policy; physical-device playback acceptance and Linux/macOS mixer distribution remain open (see docs/11-platform-audio.md) |
| GE-78 | S3 | No agent-facing docs: no AGENTS.md/CLAUDE.md, no machine-checkable component/level schema beyond the README table | repo root | ✅ | fixed |
| GE-79 | S3 | Editor's Runtime and Level File tabs share no document: inspecting shows live entities, editing edits JSON, neither sees the other; no visual placement or editable inspector | `LevelEditor.axaml` | ✅ | fixed |
| GE-80 | S3 | `GeKeys` had no arrow keys, so no runner could deliver them; the browser head's key map was a five-entry hand-written list that had already fallen behind | `GeKeys.cs`, browser/WinForms key maps | ✅ | fixed |
| GE-81 | S2 | `MultiLevelScene` listed `levels/level3.json`, which has never existed, and registered `NextLevel`/`PrevLevel` actions bound to nothing — the missing file was invisible because the scene could not leave level 1 | `SceneJson.cs:102` | ✅ | fixed |

---

## 2026-09-04 reliability review

From the [reliability and editing workflow plan](06-reliability-plan.md), which
reviewed the repository at `1f4b4da`. Every row here was reproduced before it
was fixed, and each carries a behavioural regression in the suite named in the
Location column.

| ID | Sev | Finding | Location | Confirmed | Status |
|---|---|---|---|---|---|
| GE-82 | S1 | Editor save silently skips components whose JSON is malformed, then reports "Level saved." over a file it has already truncated; the in-memory document is committed before the write is attempted | `LevelDocumentViewModel.cs`, `LevelFile.cs` | ✅ | **fixed** |
| GE-83 | S1 | Cached component tuples are not invalidated when an existing component is replaced, so systems keep driving the component the author replaced | `EntityManager.cs`, `Entity.cs` | ✅ | **fixed** |
| GE-84 | S2 | `Entity.Tag` and `Entity.Components` are public mutable fields; a tag change leaves tag-query caches stale and direct writes bypass the manager's component index | `Entity.cs` | ✅ | **fixed** |
| GE-85 | S1 | `InputManager.RemoveAction(key)` removes the action state and every callback bound to the action, breaking the other keys that share it | `InputManager.cs` | ✅ | **fixed** |
| GE-86 | S1 | `GameView` starts an owned engine on attach and never stops it on detach; the run loop and its callbacks outlive the view | `GameView.cs` | ✅ | **fixed** |
| GE-87 | S2 | `CTransform.Scale` is never applied when drawing, and culling tests a sprite's origin rather than its extent | `RenderSystem.cs` | ✅ | **fixed** |
| GE-88 | S2 | The physics broad phase keeps a bucket for every cell any collider has ever occupied, so retained storage grows with distance travelled | `PhysicsSystem.cs` | ✅ | **fixed** |
| GE-89 | S2 | Simulation steps follow host frame pacing, and fast bodies pass through thin solid walls in a single step | `Engine.cs`, `PhysicsSystem.cs` | ✅ | **fixed** |
| GE-90 | S2 | An exception from a system or scene stops the run loop with no faulted state, no host-visible diagnostic, and no defined asset ownership across scene changes | `Engine.cs` | ✅ | **fixed** |

---

## Totals

| Severity | Count |
|---|---:|
| S1 | 26 |
| S2 | 38 |
| S3 | 26 |
| **Total** | **90** |

Verified directly (not inferred): **49**.

**65 of 66 pre-vision findings fixed** as of 2026-08-11; one deferred
(`GE-30`), nothing `wontfix`. The 2026-08-30 vision review added GE-67…GE-79 —
see [`05-vision-review.md`](05-vision-review.md) §8 for their sequencing.

Of those, **GE-67, 68, 69, 70, 71, 72, 73, 75, 76 and 78 are fixed** and **GE-74
is in progress**: MIT licence and notices, package identities for
`GameEngine.Core` and `GameEngine.Runner.Avalonia` with a NuGet workflow, a
`dotnet new gameengine-game` template pack whose generated game builds and tests
in CI, a strict level format with a generated JSON Schema, a JSON asset manifest,
compile diagnostics surfaced in the editor, an injectable `GameView`, and an
`AGENTS.md` in both the repo and the template. What GE-74 still lacks is the
`gameengine` CLI wrapper (`new` / `run` / `publish` / `doctor`); the template's
own workflow covers publishing without it.

**GE-79** (one editable level document) is fixed, and **GE-77**
(cross-platform audio) is half fixed — see the 2026-09-04 section below.

**GE-80** and **GE-81** were found while building the template, not by the
review, and are fixed. Both had the same shape as GE-67: something declared in
one place that nothing downstream honoured, invisible because no test crossed
the boundary. Each now has a test that does — a generated game moves on both
`D` and `Right`, and `MultiLevelScene` is driven through every level it lists.

`GE-66` was found on 2026-08-11 by a red CI run on PR #71, whose diff touches
only the iOS head. The failure was in `GameEngine.Demo.Tests` and reproduced on
neither master nor 13 local runs — the register's first flake, and a reminder
that a red build on an unrelated diff is worth reading before it is re-run.

`GE-64` and `GE-65` were found on 2026-08-04, the first time the iOS head was
ever built. Both are S1 and both had been sitting in a project this register
previously recorded as "compiles clean" — see the note in
[`03-action-plan.md`](03-action-plan.md).

The 2026-09-04 reliability review added **GE-82…GE-90**, all nine of them fixed,
and closed **GE-79**. Every one carries a behavioural regression; the plan they
came from, [`06-reliability-plan.md`](06-reliability-plan.md), records what
landed and how each was checked. **GE-77** moved from open to in progress: Core
no longer references a native audio library and a missing backend is now
diagnosable rather than silently absent, but no sound has been proved end to end
on any platform and only Windows has a backend at all.

## Emberbrook usage review — September 7, 2026

See [07-emberbrook-engine-review.md](07-emberbrook-engine-review.md) for evidence,
proposed API boundaries, and acceptance checks. These are new open findings;
no performance regression or measured leak is claimed by this review.

| ID | Sev | Finding | Location | Confirmed | Status |
|---|---|---|---|---|---|
| GE-91 | S1 | Pointer releases synthesize keys without scene opt-in; Avalonia hover requires a pressed pointer | GameView / MainView | code + RPG regression history | fixed by GE-107 |
| GE-92 | S2 | Animation size means entire sheet, forcing frame-count compensation in games | Assets / Animation | code + RPG drawing helper | open |
| GE-93 | S2 | Game UI has independent visual/hit-test state and manual text layout | SceneEmberbrook panels | observed layout correction | open |
| GE-94 | S3 | Deferred entity queries lack a convenient construction/group ownership API | EntityManager / scene builders | observed initialization fault | open |
| GE-95 | S2 | Scene-created assets have no lifecycle hook or supplied owner satisfying snapshot lifetimes | Scene / Assets / Engine | code review | open |
| GE-96 | S2 | Host persistence capabilities lack an engine-level service boundary | browser host / RPG save store | code review | open |
| GE-97 | S2 | Headless harness does not reject faults captured internally by Engine.Tick | SceneHarness.Run | code review | open |
| GE-98 | S3 | Save milestones rebuild unrelated map art; content presentation definitions repeat | SceneEmberbrook / VillageProgress | code review | open |
| GE-99 | S2 | Trimmed browser publish uses reflection for level metadata and view lookup | LevelFile / ViewLocator | publish warnings | fixed |
| GE-100 | S1 | Browser high-DPI layout dimensions differ from CSS pointer coordinates; clicks miss buttons | browser / GameView | Chromium pointer and reload checks | fixed |


## Batched rendering review, October 26, 2026

The Note10+ stress harness demonstrated draw submission pressure at the intended mobile density. See [batched rendering](08-batched-rendering.md) for the engine API, ordering contract, measurements and verification limits.

| ID | Sev | Finding | Location | Confirmed | Status |
|---|---|---|---|---|---|
| GE-101 | S1 | Many gameplay sprites require individual canvas transforms and bitmap submissions | RenderSystem / EntityManager / RenderSnapshot | Note10+ stress results and raster benchmarks | fixed: opt-in sprite groups with atlas submissions and layer-aware snapshot copies |
| GE-102 | S1 | Cosmetic particles require entity churn or game-local pools with no shared bounded emitter API | ParticleSystem / ParticleStore / ParticleEmitter | stress harness and Core lifecycle, capacity and seed tests | fixed: pooled arrays, reusable snapshots, bounded oldest-first recycling and counters |
| GE-103 | S2 | Mobile GameView offers 240 presentation frames per second and does not expose host simulation rates | GameView | code and host rate tests | fixed: configurable rates, Android/iOS 60/60 defaults, preserved desktop and borrowed-engine rates |
| GE-104 | S2 | Shared sprite textures allocate a managed image wrapper per instance and layer sorting allocates a comparer adapter per snapshot | RenderSystem / RenderSnapshot | before and after MemoryDiagnoser results | fixed: frame-scoped shared images and span sorting |
| GE-105 | S2 | The engine-batched stress path creates options, an emitter and a seeded RNG per spark respawn; previous allocation benchmarks reuse emitters and omit this cost, while ordinary movement and animation queries box enumerators | ParticleSystem / ParticleStore / MovementSystem / AnimationSystem / StressFrameWorkload | actual GameStress source reproduction and per-thread full-frame allocation tests at 750, 3000 and 8000 particles | fixed: value-type direct births with shared options, indexed queries, lifetime-managed immutable image caching, 0 B/frame batched regression, triple-buffer reuse coverage and external harness migration patch |

## 2026-10-26: GE-77 mobile and browser audio

Added AVFoundation and Android framework backends, registered in their Avalonia heads,
and Web Audio interop in the browser head. Core now coalesces sound events, limits voices,
steals by priority, preloads effects and controls master/music/effect gain and mute. SDL
uses the same voice contract. Void Bastion preloads and plays music and kill effects.
No new third-party native audio library was added.

The Release solution and both standalone backend targets build with zero warnings. The
Android head produces a signed Release APK. All 619 .NET tests and five JavaScript state
tests pass. Browser Release build and static publish pass. iOS head packaging is blocked
by the installed Xcode/workload version mismatch. Actual mobile and browser playback is
still unverified, and Linux/macOS mixer distribution is unchanged, so GE-77 remains in
progress. Formats, lifecycle policy, build details and the device acceptance checks are
in [platform audio](11-platform-audio.md).

## Mobile platform audit follow-up, October 26, 2026

These entries implement the scoped PG findings in the platform audit. GE-101 through
GE-104 are reserved for the batched-rendering branch already referenced by that audit.
See [mobile platform support](09-mobile-platforms.md) for API contracts, verification
and the remaining physical-device checks. Existing open findings outside this scope
retain their status.

| ID | Sev | Finding | Location | Confirmed | Status |
|---|---|---|---|---|---|
| GE-106 | S1 | PG-01 and PG-14: mobile/browser lifecycle does not reach games and paused loops poll every millisecond | Engine, HostLifecycle, native delegates, browser host; MobileHostTests | reproduced and regression tested | fixed; native device lifecycle and energy checks pending |
| GE-107 | S1 | PG-08: a second pointer overwrites the first contact and no capture/cancel identity survives dispatch | Pointer, InputManager, GameView; MultiTouchTests and three-contact headless host test | reproduced and regression tested | fixed |
| GE-108 | S1 | PG-09: pointer releases synthesize keys without opt-in, drop terminal events under queue pressure and share physical key state | InputManager, Avalonia and WinForms hosts, demo policies; MultiTouchTests, GameViewLifetimeTests, MobileInputPolicyTests | reproduced and regression tested | fixed; also closes GE-91 |
| GE-109 | S1 | PG-10: Android Back misses the activity lifetime and can replace the whole game view | GameView BackRequested and Android manifests; MobileHostTests | source inspected and policy tested | fixed; predictive gesture/device verification pending |
| GE-110 | S1 | PG-05: neither iOS head bundles an audited required-reason privacy manifest | demo/template PrivacyInfo.xcprivacy, check-ios-privacy.py | both built simulator app bundles inspected | fixed; final signed archive privacy review pending |
| GE-111 | S1 | PG-11: level writes, component builders and demo file saves use reflection serialization | generated Core and Emberbrook contexts; MobileSerializationSmoke | Release NativeAOT round trips with JSON reflection disabled | fixed |
| GE-112 | S2 | PG-33: games cannot request foreground-only screen keep-awake or diagnose denial | HostServices, native backends, BrowserHost; MobileHostTests and host-interop.test.mjs | capability and transition tests | fixed; physical idle-timer checks pending |
| GE-113 | S3 | PG-36: no optional haptics capability or user toggle | HostServices and native feedback backends; MobileHostTests | toggles, capability and foreground gates tested | fixed; physical feedback checks pending |
| GE-114 | S1 | PG-12: delivered Android native libraries have no ELF/ZIP 16 KB validation gate | check-android-alignment.py, build/release workflows, artifact report | signed Debug and Release APKs inspected, all native LOAD segments pass | fixed; 16 KB runtime check pending |

## Opt-in visual effects, October 26, 2026

IDs start at GE-120 to avoid clashes with the integrated work through GE-114.
See [render effects](12-render-effects.md) for contracts, ownership, benchmark commands and verification limits.

| ID | Sev | Finding | Location | Confirmed | Status |
|---|---|---|---|---|---|
| GE-120 | S2 | Games lack runtime SkSL backgrounds, custom composition-layer shaders and full-screen grading with diagnosable fallback | Effects/RuntimeShader, RenderEffects, RenderSystem | pixel tests for uniforms, compilation failure, source preservation and grading | fixed |
| GE-121 | S2 | Bright batches and particles lack reduced-resolution opt-in bloom and phone quality controls | RenderEffects, RenderSystem | Off/Low/High spread and resize tests; desktop raster benchmark | fixed; physical GPU cost pending |
| GE-122 | S3 | Games must implement shake, zoom response and expiring hit-stop independently | CameraEffects, Engine, RenderSystem | deterministic decay, unscaled expiry and real-engine time tests | fixed |
| GE-123 | S3 | Common trails, gradient beams, shockwave rings, seeded lightning and shaped pooled shatter have no reusable API | EffectGeometry, ParticleEmitter | seeded endpoints and bounded pool saturation tests | fixed |
| GE-124 | S3 | No reusable deterministic PNG capture path checks real-engine faults and supplied simulation time | SceneCapture | real-engine repeatability, timing, invalid input and fault tests; six consuming-game phone frames | fixed |
| GE-125 | S2 | Effect intermediates need explicit lifetime, resize and host-context ownership to avoid per-frame surfaces and GPU readbacks | RenderEffects, RenderSystem.SetEffects | resize, replacement and throwing callback checks; GPU-context allocation path inspected | fixed; physical GPU execution pending |

## Aurora mobile rendering

| ID | Sev | Finding | Location | Confirmed | Status |
|---|---|---|---|---|---|
| GE-130 | S2 | Atmospheric background shaders shade every output pixel even at Low bloom; background decoration cannot run before opaque scene sprites | `Effects/RenderEffects.cs` | ✅ | **fixed** |

GE-130 adds independent `BackgroundScale` and `BackgroundLayers`. Reduced shader surfaces retain virtual coordinates, reuse the host GPU context, and resize with the viewport. Zero uses the fallback colour without running the shader. Background layers always run before scene sprites and never enter bloom. Coordinate, fallback, resize and layer-order pixels are covered by `EffectsTests`.

| ID | Sev | Finding | Location | Confirmed | Status |
|---|---|---|---|---|---|
| GE-131 | S2 | Native-resolution scene blending leaves insufficient phone frame headroom beneath a separate native HUD | `Systems/RenderSystem.cs` | ✅ | **fixed** |

GE-131 adds opt-in `RenderOptions.MaxRenderWidth`, default zero for the existing direct path. A retained scene surface caps drawing resolution while preserving virtual geometry, pointer mapping and host HUD resolution. The final post-process still runs at output resolution. Tests cover resize, bypass, coordinates, canvas restoration and post-processing.

## Effect surface retention

| ID | Sev | Finding | Location | Confirmed | Status |
|---|---|---|---|---|---|
| GE-132 | S2 | Conservative local clip bounds invalidate retained effect surfaces every frame and distort reduced-buffer scaling | `Effects/RenderEffects.cs`, `Systems/RenderSystem.cs` | ✅ | **fixed** |

Surface allocation checks now use exact device clip dimensions. Viewport dimensions use inverse-mapped device bounds rather than Skia's conservative local clip expansion. A repeated-layer test verifies the same intermediate canvas is retained, followed by replacement on resize. The resolution-cap test also verifies scaled host canvases and input mapping.

## Opt-in draw measurement

| ID | Sev | Finding | Location | Confirmed | Status |
|---|---|---|---|---|---|
| GE-133 | S3 | Hosts cannot distinguish engine draw submission cost from render-thread pacing and GPU completion | `Systems/RenderSystem.cs` | ✅ | **fixed** |

`RenderOptions.DrawMeasured` optionally receives CPU draw seconds and whether the host canvas is GPU-backed. No timing work runs without a callback. This measures scene submission, not GPU completion or the host HUD. The callback runs on the paint thread after canvas restoration. The cap test verifies one measurement per draw and the raster flag.

## Aurora second polish

| ID | Sev | Finding | Location | Confirmed | Status |
|---|---|---|---|---|---|
| GE-140 | S3 | Effects expose no intermediate quality between quarter-size and half-size bloom buffers | `Effects/RenderEffects.cs` | bloom spread and resize tests at all four levels | fixed |
| GE-141 | S3 | Bloom callbacks redraw geometry and ignore layer grading in their glow contribution | `Effects/RenderEffects.cs` | single callback, shader colour, resize and reuse tests at Low, Medium and High | fixed |
| GE-142 | S3 | GPU instrumentation around an Avalonia scene needs extra graphics leases that disturb the measured path | `GameView.cs` | game Skia render check verifies paired callbacks inside the existing lease | fixed |

`BloomQuality.Medium` uses one-third-size retained bloom surfaces and a four-pixel blur sigma. Existing enum values remain Off=0, Low=1 and High=2; Medium=3 preserves numeric persistence compatibility. Consuming games choose their own scene width and atmospheric resolution. Phone measurements and default selection live in the consuming game's `art/style-frames/verification.md`.

Bloom layers now draw once into a retained transparent surface. The composed result supplies both the visible layer and its downsampled glow, including the layer shader. Low renders that source at half virtual dimensions, Medium at three quarters and High at full size. Non-bloom layers and additive sprite batches preserve their existing order and projection.

`GameView.DrawBoundary` optionally receives the borrowed canvas before and after the scene draw, inside the existing Skia lease. Hosts can issue asynchronous GPU timer queries without adding a second rendering control or acquiring more leases. The after callback runs in a finally block. A null observer preserves normal rendering.
