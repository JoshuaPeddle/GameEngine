# GameEngine — map for agents

A 2D ECS game engine in C#, published as NuGet packages plus a `dotnet new` template.
~15k lines across the engine, four runners, an editor and the demos, which is small enough
to hold in one context window. Keep it that way.

## Verify a change

```bash
dotnet build GameEngine.sln -c Release          # zero warnings is the standard
dotnet test                                     # Core, Demo and Editor suites
dotnet test GameEngine.Core.Tests               # the fastest loop while changing the engine
dotnet run --project GameEngine.Runner.Avalonia/GameEngine.Runner.Avalonia.Desktop
dotnet run --project GameEngine.Editor.Desktop
```

`GameEngine.Demo.Tests` drives every demo scene through a real `Engine` headlessly with a
fixed timestep — no window, no display. That harness is the reason a scene or level change
can be verified without launching anything, so add to it rather than around it.

On Linux the test host needs `libfontconfig1`, or `libSkiaSharp` fails to load and the
process dies instead of reporting failures.

Android, iOS and browser heads need their workloads (`android`, `ios`, `wasm-tools`).
CI builds all of them; a local `dotnet build GameEngine.sln` will fail on any you lack.

## Layout

| Project | What it is |
|---|---|
| `GameEngine.Core` | The engine: ECS, systems, rendering, input, level and asset formats. Published as a NuGet package. No UI framework may enter it. |
| `GameEngine.Demo` | Sample games, and the first consumer of everything Core exposes |
| `GameEngine.Runner.Avalonia` | `GameView` plus the Desktop / Android / iOS / Browser heads. Published as a NuGet package. |
| `GameEngine.Runner.Winforms` | A second host, which exists to keep Core honest about UI independence |
| `GameEngine.Editor` | Scene and level editor: live Roslyn compile into a collectible ALC, snapshot picking |
| `templates/` | The `dotnet new gameengine-game` template pack. `templates/game/` is the project a user gets. |
| `docs/` | Review, findings register (`GE-nn`), action plan. Not published. |

## The formats

Both are strict: unknown keys, unknown types and wrong value kinds are rejected with a
message naming what was allowed. That is deliberate — silent drops are the one thing an
agent cannot recover from.

- **Levels** — `GameEngine.Core/levels/level.schema.json`, generated from
  `ComponentSchemas`. `LevelSchemaTests.PublishedSchemaMatchesTheComponentVocabulary`
  fails if the two drift, so regenerate the schema when you change the vocabulary.
- **Assets** — `GameEngine.Core/assets.schema.json`, parsed by `AssetManifest`. The old
  positional `assets.txt` is still read; nothing new should be written in it.

Adding a component means touching `ComponentSchemas`, the factory, the editor form in
`LevelComponentViewModel`, and the schema file — the tests will tell you which you missed.

## Conventions

- **No comments in code.** Names and structure carry the meaning; explanations belong in
  documents like this one. The exception is a short note about *why* something
  non-obvious is the way it is.
- **Time is in seconds** everywhere except animation frame delays, which are milliseconds
  because that is a file-format value.
- **Coordinates are virtual.** A scene declares `VirtualWidth`/`VirtualHeight`; the engine
  letterboxes and maps pointer input back through the same transform.
- **Components are data; logic lives in systems.** Rendering stays inside `RenderSystem`.
- **`CTransform.Position` is the top-left of the bounding box, or the sprite centre when
  there is no box.** A sprite is drawn centred on that point; rotation and scale are applied
  about that centre and a negative scale mirrors on its axis. `CBoundingBox.Size` is
  collision geometry and is deliberately unaffected by `CTransform.Scale`. `SpriteGeometry`
  is the single definition, shared by drawing, culling and the editor's picking.
- **`GeKeys` is the key vocabulary** (`A`–`Z`, `Space`, arrows). Every runner builds its
  map from those names, so adding a value reaches all platforms; a swipe is classified as
  `W`/`A`/`S`/`D`. Several keys may share one action.
- **Entity mutation goes through the entity.** `Id` is immutable, `Tag` is a property and
  `Components` is read-only from outside, because every one of those changes has to reach
  `EntityManager` to invalidate its cached queries — replacing a component included, even
  though the component set is unchanged. A new entity is invisible to every query until the
  next `EntityManager.Update()`, and a removed one can never re-enter an index however long
  a caller holds its reference. `ActionMapper` bindings resolve their component per
  invocation, so they follow a replacement and go inert after a removal.
- **`Engine.Tick(deltaSeconds)` is the single frame primitive.** Both run loops are built
  on it, which is what makes the headless harness exercise the real path. It advances by
  exactly what it is given and rejects anything that is not a finite, non-negative number of
  seconds. The live loops reach it through a `FixedStepAccumulator`: `FixedTimeStep` sets the
  simulation rate, `MaxCatchUpSteps` bounds one frame's catch-up, and time beyond that budget
  is dropped rather than queued. `TargetFrameRate` paces presentation and nothing else.
- **Fast bodies are swept, not just tested where they land.** `PhysicsSystem` sweeps each
  pair along the paths they took this step and resolves at the moment they met, so a body
  cannot cross a thin solid wall inside one step and a deep overlap is not pushed out the far
  side. `CTransform.PreviousPosition` is where the entity started the step; `MovementSystem`
  records it for every transform, so a scene that repositions an entity directly does not
  leave a stale path behind.
- **A view owns the engine it constructs and borrows the one it is handed.** `GameView`
  stops and disposes an engine it built when it leaves the visual tree, and reattaching
  builds a fresh one; an engine passed in through the `Engine` property only has the view's
  paint callback put on at attach and the owner's put back at detach. `Engine.Systems` is
  published as a whole on a scene change, so look systems up with `TryGet` from UI code —
  the container is empty between disposal and the last queued event that still refers to it.
- **A scene failure faults the engine; it does not kill the loop.** An exception from a
  system, a scene's `Update`, or a scene's `Initialize` is recorded as an `EngineFault`
  naming the scene, the operation and the system, handed to `Engine.FaultAction`, and stops
  the simulation. The last snapshot stays paintable and loading another scene clears it.
- **Assets outlive the engines that draw from them.** An `Assets` owns every texture it
  decodes and every scaled animation it derives; a plain `Animation` borrows its texture and
  releases nothing. Nothing is freed when an entity, an animation or a scene goes away,
  because a render snapshot the host is still painting holds those bitmaps. Dispose an
  `Assets` only after the engines using it. Use `GetAnimation(name, scaleSize)` rather than
  `AsScaledAnimation` — the cached one does not decode a new bitmap per scene load.
- **Zero warnings.** `TreatWarningsAsErrors` is on in Core and Demo.
- Reference finding IDs from `docs/02-findings.md` in commit messages: `fix(core): read
  velocity from level files (GE-67)`.
