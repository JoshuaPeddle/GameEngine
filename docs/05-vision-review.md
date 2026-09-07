# Vision Review — 2026-08-30

Reviewed at `8f7c61f`, against the stated product vision rather than code
correctness (the [2026-07-31 review](01-review.md) covered that; 65 of its 66
findings are fixed).

**The vision:** the ultimate easy-to-use game editor that publishes to every
platform and embeds anywhere, without SDK wrangling; LLM-friendly at both the
level/config layer and the engine-code layer; a 2026 C#, friendly-licensed
engine.

New findings are registered as **GE-67 … GE-79** in
[`02-findings.md`](02-findings.md).

---

## 1. Where it actually stands

The honest baseline, because the roadmap only makes sense against it:

- **~15k lines of C#** across engine, demos, runners, editor, tests. This is a
  small, coherent 2D ECS engine — not a Unity competitor, and that is its
  advantage for every pillar of the vision. An LLM can hold the entire engine
  in context. Lean into that; never market or architect against it.
- **The core is in genuinely good shape.** The 2026-07-31 review's debt is
  paid: deterministic `Tick(deltaSeconds)`, real lifecycle, seconds-based
  units, uniform-grid broad phase, snapshot-isolated rendering, injected
  `IAssetSource`, zero-warning build with `TreatWarningsAsErrors`.
- **Five verified platforms** — Windows/Linux/macOS desktop (Avalonia +
  WinForms), Android (CI emulator smoke test), iOS (built, launched,
  play-tested), Browser (wasm, Dockerized). Verified, not just compiling —
  that discipline is rare and worth protecting.
- **222+ tests**, including a headless harness that drives every demo scene
  through a real engine. This harness is the single most LLM-valuable asset in
  the repo (§5).
- **The editor is an early prototype.** Scene discovery + Roslyn
  live-compilation into a collectible ALC is a strong foundation
  (`SceneProjectCompiler`), but the visible surface is a scene dropdown, a
  read-only inspector, and a form-based level-file editor.

Scorecard against the vision:

| Pillar | State |
|---|---|
| Easy-to-use editor | Prototype; opens an existing `.csproj` only; no create/place/publish flow |
| Publish every platform | True for **this repo's demos** via CI; nonexistent for a user's game |
| Embeddable anywhere | Core: yes. Runner: undermined by statics (GE-75) |
| No SDK wrangling | Not addressed; every mobile/web target needs local workloads |
| LLM-friendly levels | JSON format exists, but silently lossy and schema-less (GE-67/68/76) |
| LLM-friendly engine code | Strong: small, commentless-by-convention, well-tested |
| Friendly licensed | **No LICENSE file at all**; repo private (GE-72) |
| 2026 C# | .NET 10, central package management, current deps — yes |

---

## 2. The structural gap: there is no "game project"

Every other gap is downstream of this one. Today the only games that exist are
`GameEngine.Demo`, living inside the engine repo, with the platform heads
hardwired to its assets and scenes. A user cannot:

- create a game without cloning this repo and imitating `GameEngine.Demo`
  (no NuGet package — `GameEngine.Core.csproj` has no package identity, and
  nothing is published to any feed — GE-73);
- open the editor and say "New Game" (`ProjectEditorViewModel` only browses to
  an *existing* `.csproj`);
- export their game to any platform (`publish.yml` publishes the demos of
  this repo and pushes `joshuapeddle/gameengine-web` — a user's game has no
  path to an APK, a wasm bundle, or an exe — GE-74).

**The unlock is one deliverable: a `dotnet new` template pack.**
`dotnet new gamengine-game` emits a solution with the game project (scenes,
`assets.txt`, `levels/`) plus thin pre-wired platform heads referencing the
NuGet packages. Everything the vision wants stacks on top:

- the editor's "New Project" button runs the same template;
- publishing is `dotnet publish` of a head the user already owns;
- the LLM story becomes "here is a complete, small, conventional project" —
  the shape agents handle best;
- the engine repo stops being the universe; `GameEngine.Demo` becomes just
  the first consumer of the template.

Order of operations: **license → NuGet packages → template → everything else.**

## 3. Publish everywhere, without SDK wrangling

Be honest about physics: an APK needs the Android workload + JDK, an IPA needs
Xcode on a Mac, wasm needs `wasm-tools`. "No manual SDK stuff" cannot mean "no
SDKs"; it can mean **the user never debugs an SDK themselves**:

1. **`gameengine` CLI** (a `dotnet tool`, thin wrappers):
   `new` / `run` / `publish --target android|ios|browser|win|linux|osx` /
   `doctor` (checks SDK/workload/Xcode state and prints the one command to fix
   it — the pain the action plan's own toolchain notes document, e.g. the
   workload feature-band trap, is exactly what `doctor` encodes).
2. **A reusable GitHub Actions workflow** shipped with the template, so a
   user's repo publishes all targets in the cloud on tag push. This is the
   realistic "publish everywhere with zero local SDKs" story — the template
   arrives with it already wired.
3. Fix the engine-side release hygiene while touching it (GE-71): `publish.yml`
   uses archived actions (`actions/create-release@v1`,
   `upload-release-asset@v1`), cuts a release `1.0.<run_number>` on *every*
   master push, has no macOS artifact despite macOS being the dev platform,
   and the browser build goes only to Docker Hub (a static `wwwroot` zip would
   serve GitHub Pages / itch.io directly).

Also under this pillar: **audio is Windows-only** (GE-77, already a known
limitation in the README). A "publish everywhere" engine currently ships
silent games on five of six platforms. Replace the hand-placed
`SDL2_mixer.dll` with a managed decode path (e.g. NLayer/NVorbis into an SDL
or platform audio sink), or adopt a maintained cross-platform audio lib.

## 4. Embeddable anywhere

The core embeds well *by design*: `Engine` is UI-agnostic, `Tick()` is
deterministic and thread-free, rendering hands out immutable snapshots, input
is queue-based, and the WinForms host proves there's no Avalonia coupling. The
test harness embeds the engine headlessly — embedding is literally tested.

The Avalonia runner — the piece an embedder would actually reach for — fights
it (GE-75):

- `App.AssetSource`, `App.StartupScene`, `App.FirstFramePresented` are static
  mutable fields on the Application subclass (`App.axaml.cs:15-17`);
- `GameView` keeps a `static Engine? Current` (`GameView.cs:22`) and always
  constructs its own engine — it cannot be handed one;
- net effect: one game per process, configured through globals, and only from
  code that runs before the control is constructed.

For "embeddable anywhere," `GameView` should be a plain control with
`Engine`/`Scene`/`AssetSource` as properties (settable in XAML or code), no
statics, N instances per window. The statics can remain as a convenience layer
for the app-template case. Same shape applies if a MonoGame-style raw window
host or a Unity-embed story ever matters; the core needs nothing.

Packaging is the other half: embeddable means `dotnet add package`, which is
GE-73 again.

## 5. LLM-friendly

### Levels (the config layer)

The JSON format is the right idea and the loader even allows comments and
case-insensitive keys. But it fails the property that matters most to an agent:
**feedback**. Everything unknown is silently ignored:

- The editor round-trips `velocity` on CTransform
  (`LevelComponentViewModel.cs:171,240`) but
  `ComponentFactory.CreateCTransform` never reads it — velocity set in the
  editor silently vanishes at load (GE-67). This is a live bug today, and it
  exists *because* nothing validates keys.
- The shipped `level1.json` itself carries dead keys — `speed`/`repeat` on
  `CAnimation`, `offset` on `CBoundingBox` — that no code has ever read
  (GE-68). An LLM (or human) copying the shipped levels as examples learns
  a vocabulary the engine doesn't speak, and gets zero signal.

The fix package (GE-76):

1. **Strict mode in `ComponentFactory`**: unknown component type already
   throws; unknown *property* should too (or warn), with the known-property
   list in the message — "hint-style errors" are what make a format
   agent-editable.
2. **Publish a JSON Schema** for the level format; validate in the loader, in
   the editor on save, and in CI over `levels/**`. The schema is also the
   machine-readable doc an agent can be pointed at.
3. **Make the factory serializer- or reflection-driven** so components, the
   schema, the editor forms, and the docs cannot drift four ways (they already
   have: engine, factory, editor, shipped levels each disagree today).
4. **Replace `assets.txt`** (`Animation Grenade TexGrenade 4 250` — positional,
   bespoke, undocumented in-band) with a JSON manifest sharing the same
   validation story. Keep reading the old format for a release.

### Scenes and the engine (the code layer)

Already strong, mostly by accident of discipline:

- Scenes are plain C# with a tiny base class — the most LLM-editable game
  format there is, *if* the agent can verify. It can: the headless harness
  drives any scene deterministically. **Productize that**: a
  `gameengine test`/template test project that boots the user's scene N frames
  and asserts it survives — the agent's inner loop becomes write → run →
  read real errors.
- The no-comments/self-descriptive convention plus 222 tests plus small size
  is a better agent substrate than most engines will ever have.

What's missing is the map (GE-78): an `AGENTS.md` (or CLAUDE.md) in the
template and the repo stating the component vocabulary, the level schema
location, the verify command, and the conventions. Cheap, high leverage.

### Editor compile loop

`SceneCatalogViewModel.LoadScenesAsync` catches all exceptions into
`"Error loading scenes"` (`SceneCatalogViewModel.cs:136-139`) — while
`SceneProjectCompiler` already formats Roslyn diagnostics into the exception
it throws. The single most important feedback in a compile-and-preview editor
is discarded one frame before the user (GE-69). Surfacing diagnostics is also
a prerequisite for any future in-editor AI assistance.

## 6. The editor itself

Current surface (verified against the XAML/view models): scene dropdown +
run/pause + click-to-inspect (read-only) on one tab; a form-based level-file
editor (entity list, per-component forms, raw JSON) on another. Solid plumbing
underneath — live Roslyn compile, collectible ALC reload, snapshot-based
picking — but as a *product* it is not yet an editor someone chooses.

What "ultimate easy to use" implies, in dependency order:

1. **Diagnostics visible** (GE-69) — table stakes.
2. **New Project** — template-backed (§2).
3. **Visual manipulation**: drag the picked entity in the paused preview and
   write the transform back to the level document; then palette-drop to
   create entities. Today the Runtime tab and the Level File tab don't share a
   document at all (GE-79) — inspecting shows live entities, editing edits
   JSON, and neither sees the other.
4. **Inspector becomes editable** (component values, applied via
   `Engine.Post` — the primitive already exists).
5. **Publish button** — the CLI/CI story (§3) surfaced in-editor.
6. Later: AI panel that edits level JSON / scene code through the same
   validated formats — which is why §5's strictness comes first.

The ComfyUI sprite-gen integration is ahead of its time in this codebase —
it's a differentiator once a project system exists for the sprites to land in.

## 7. Licensing (GE-72)

"Friendly licensed" is currently **unlicensed** — no LICENSE file, GitHub
reports `licenseInfo: null`, and the repo is private. Unlicensed means
all-rights-reserved: nobody can legally use, embed, or contribute.

- Pick **MIT** (maximum-friendly, matches SkiaSharp/Avalonia) or **Apache-2.0**
  (adds an explicit patent grant — the safer choice if other companies embed
  it). Both satisfy the vision; deciding is the work of an hour and blocks
  nothing on being decided *now*, before external code or assets arrive.
- Dependency licenses are compatible either way: Avalonia (MIT), SkiaSharp
  (MIT), ppy.SDL2-CS (MIT), SDL2/SDL2_mixer (zlib). The checked-in
  `SDL2_mixer.dll` should carry its zlib notice (a `THIRD-PARTY-NOTICES` file)
  — moot if §3 removes the binary.
- Demo/sprite assets need a stated license too (CC0 recommended), including
  provenance for AI-generated art.

## 8. Sequenced roadmap

Ordered by unblocking value, same rule as the action plan:

| # | Deliverable | Findings | Size |
|---|---|---|---|
| 1 | LICENSE + THIRD-PARTY-NOTICES + asset license | GE-72 | hours |
| 2 | NuGet identities + CI publish of Core / Runner.Avalonia packages | GE-73 | ~1 day |
| 3 | `dotnet new` template pack (game + heads + tests + reusable publish workflow + AGENTS.md) | GE-73/74/78 | ~1 week |
| 4 | Level-format hardening: strict mode, JSON Schema, serializer-driven factory, fix velocity, clean shipped levels | GE-67/68/76 | ~2 days |
| 5 | Editor: surface compile diagnostics; New Project | GE-69 | ~1 day |
| 6 | `gameengine` CLI: new/run/publish/doctor | GE-74 | ~3 days |
| 7 | De-static the Avalonia runner; injectable `GameView` | GE-75 | ~1 day |
| 8 | JSON asset manifest replacing assets.txt | GE-76 | ~1 day |
| 9 | Cross-platform audio | GE-77 | ~1 week, risky |
| 10 | publish.yml modernization (+ macOS, + wwwroot artifact) | GE-71 | hours |
| 11 | Editor visual manipulation + editable inspector | GE-79 | weeks, ongoing |

Items 1–5 are a coherent "v0.1: other people can build a game with this"
milestone. Everything in 6–11 gets easier to justify once a stranger (or an
agent) has done that.

### Status — 2026-08-30

Items **1, 2, 3, 4, 5, 7, 8 and 10 are done**; see the register in
[`02-findings.md`](02-findings.md) for the per-finding statuses.

`dotnet new gameengine-game` now emits a solution with the game, four platform
heads, a headless test project, an `AGENTS.md` and a publish workflow, and a CI
job creates a game from the template and runs its tests on every push — so the
template cannot rot silently. The level and asset formats are strict, schema-backed
and generated from one table in `ComponentSchemas`. `GameView` takes an engine,
scene and asset source as properties.

Not done, by choice:

- **Item 6, the `gameengine` CLI.** The template ships the publish workflow the
  CLI would have wrapped, so this is now convenience rather than capability.
- **Item 9, cross-platform audio (GE-77)** — the largest and riskiest item.
- **Item 11, editor visual manipulation (GE-79)** — open-ended by nature.

Two things surfaced while doing the work, were recorded as **GE-80** and
**GE-81**, and have since been fixed:

- `GeKeys` had no arrow keys, so no runner could deliver them. It now carries
  `Up`/`Down`/`Left`/`Right`; the WinForms and browser key maps are generated
  from the enum the way Avalonia's already was, so the browser's five-key
  hand-written list can no longer fall behind. `InputManager` learned that
  several keys may share one action, which binding WASD *and* the arrows
  requires. Swipes still classify as W/A/S/D.
- `MultiLevelScene` listed a `levels/level3.json` that never existed, and its
  `NextLevel`/`PrevLevel` actions were bound to nothing — which is why the
  missing file never surfaced. The scene now navigates, `level2.json` is a real
  level rather than a one-invisible-entity stub, `level3.json` exists, and a
  test walks the scene through every level it lists.

## 9. What this review did not cover

Engine feature breadth (tilemaps, particles, gamepads, non-strip animations,
scene transitions, save state, the ignored `Font` entries) is real but was
deliberately left out: breadth follows adoption, and adoption is blocked by
§2, not by missing features. The demos prove the current feature set already
makes shippable small games.
