# Brief: demand-driven frame pacing so games stop heating phones

You are in `/Users/josh/Source/GameEngine-pacing` (git worktree, branch `frame-pacing`). Read `AGENTS.md`,
`docs/README.md`, `docs/08-batched-rendering.md`, `docs/09-mobile-platforms.md` and
`docs/12-render-effects.md` first and follow their conventions (no code comments except short "why"
notes, zero warnings, strict formats, tests through the headless harness). Commit on `frame-pacing` in
small commits; do not push. Delete this brief in your last commit. No em dashes in prose.

## The problem (measured)
Tower Idle (the first shipped consumer, read only: `/Users/josh/Source/tower-idle/src/TowerIdle.Game/`,
esp. `GameHost.cs` and `GamePage.cs`) runs with `TargetFrameRate = 60`. On a Galaxy Note10+ it used
100 to 125% CPU while showing a static "Welcome back" modal over the board with no wave running:
one thread about 67% (most likely the engine loop in `Engine.RunLoop` plus `GameView` render via
`CustomDrawOp`), the UI thread about 30%. Phones get hot during normal play. Backgrounding already works
(0% with the screen off). Root cause: `Engine.RunLoop` ticks and invalidates every frame at the target
rate whether or not anything changed, and the runner redraws the full scene every frame.

## Goal
The engine draws only when something needs drawing, at the lowest rate that looks right, with an API so
simple a game gets it by default. This engine is a core asset of the company: the result must be
polished, easy to use and well documented, not a hack for one game.

## Requirements
- **Frame policy in Core** (no UI framework): a clear, small API, for example a mode or tiers:
  *Active* (target rate, e.g. 60, while gameplay or a transition animates), *Ambient* (a lower cap, e.g.
  30 or less, for subtle background motion like water, aurora or idle particles), *Idle* (no ticks and
  no draws until something wakes the engine). Pick names and shape that fit the codebase; justify them.
- **Automatic where possible:** live particles, active tweens or effects, input, and systems that report
  activity keep the engine Active; when everything settles it steps down to Ambient then Idle after a
  short, configurable grace period. Games can also declare state explicitly (e.g. "board paused",
  "modal open") and request one-off frames. Input and `Post`ed work must wake an idle engine at once
  with no visible latency.
- **Correct simulation time:** fixed-timestep determinism and existing replay or test guarantees hold;
  no time jump or catch-up burst on wake; no lost input.
- **Caps:** a global max-rate cap a game can expose as a "battery saver" setting (e.g. 30).
- **Cheaper frames:** profile what a frame costs on the render path (`GameView`, `CustomDrawOp`,
  `RenderSystem.DrawEntitiesToCanvas`, snapshots, allocations per frame). Fix the clear wins, e.g.
  caching static layers so unchanged background is not re-rasterised every frame, avoiding per-frame
  allocations. Report before and after numbers.
- **Runner:** `GameView` only invalidates when the engine produced a frame; no busy loops or
  `Thread.Yield` spinning in any path a phone can hit. Check the browser loop (`Task.Delay(1)`) too.
- **Back compatible:** existing games and the Demo keep working; `TargetFrameRate` keeps its meaning as
  the Active rate. Update templates so new games get the policy by default.

## Prove it
- Headless tests: an idle scene produces zero ticks and zero draws over a long simulated period; activity
  raises it to the target rate; settling steps down; input and Post wake it; the cap is honoured;
  background lifecycle still stops everything; determinism tests unchanged.
- A focused benchmark in `GameEngine.Benchmarks` measuring CPU time per second of wall time for: idle
  scene, ambient scene, busy scene, before and after.
- Write `docs/14-frame-pacing.md`: the model, the API, how a game opts into states, the battery saver
  cap, measurements, and the exact steps for Tower Idle to adopt it (expected: declare paused, modal and
  between-wave states; expose a 30 fps saver toggle).

## Acceptance
- `dotnet build GameEngine.sln -c Release` zero warnings for the projects your workloads can build;
  `dotnet test` passes.
- Final message: API summary, how automatic detection works, measurements, test counts, risks, and the
  Tower Idle adoption steps.
