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
| GE-77 | S2 | Audio is Windows-only (hand-placed `SDL2_mixer.dll`; ppy.SDL2-CS ships no mixer natives) — "publish everywhere" ships silent games on 5 of 6 platforms | `GameEngine.Core.csproj` | ✅ | **in progress** — Core no longer references a native audio library and missing audio is now diagnosable; no platform beyond Windows has a backend yet |
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
| GE-91 | S1 | Pointer releases synthesize keys without scene opt-in; Avalonia hover requires a pressed pointer | GameView / MainView | code + RPG regression history | fixed |
| GE-92 | S2 | Animation size means entire sheet, forcing frame-count compensation in games | Assets / Animation | code + RPG drawing helper | fixed |
| GE-93 | S2 | Game UI has independent visual/hit-test state and manual text layout | SceneEmberbrook panels | observed layout correction | fixed (workbench pilot) |
| GE-94 | S3 | Deferred entity queries lack a convenient construction/group ownership API | EntityManager / scene builders | observed initialization fault | open |
| GE-95 | S2 | Scene-created assets have no lifecycle hook or supplied owner satisfying snapshot lifetimes | Scene / Assets / Engine | code review | open |
| GE-96 | S2 | Host persistence capabilities lack an engine-level service boundary | browser host / RPG save store | code review | open |
| GE-97 | S2 | Headless harness does not reject faults captured internally by Engine.Tick | SceneHarness.Run | code review | fixed |
| GE-98 | S3 | Save milestones rebuild unrelated map art; content presentation definitions repeat | SceneEmberbrook / VillageProgress | code review | open |
| GE-99 | S2 | Trimmed browser publish uses reflection for level metadata and view lookup | LevelFile / ViewLocator | publish warnings | fixed |
| GE-100 | S1 | Browser high-DPI layout dimensions differ from CSS pointer coordinates; clicks miss buttons | browser / GameView | Chromium pointer and reload checks | fixed |
