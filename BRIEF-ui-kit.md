# Brief: a game UI kit for the engine

You are in `/Users/josh/Source/GameEngine-ui` (git worktree, branch `game-ui-kit`). Read `AGENTS.md`,
`docs/README.md` and `docs/12-render-effects.md` first and follow their conventions (no code comments
except short "why" notes, zero warnings, strict formats, tests through the headless harness). Commit on
`game-ui-kit` in small commits; do not push. Delete this brief in your last commit. No em dashes in
prose.

Read the first real consumer too, read only: `/Users/josh/Source/tower-idle/src/TowerIdle.Game/GamePage.cs`
and its captures in `/Users/josh/Source/tower-idle/.artifacts/checks/` (`map-*.png`, `sound-and-touch.png`
and the panel captures). The owner's verdict: in-game graphics 6.5/10, but the UI and menus "look like
Microsoft Paint": default system font, flat rectangles, plain buttons, no hierarchy, no motion.

## Goal
A reusable, themeable game UI kit that makes menus feel like a polished commercial mobile game, so every
future game built on this engine starts with good UI. `GameEngine.Core` stays free of any UI framework:
put the kit in a new project `GameEngine.UI.Avalonia` (published like the runner) or inside
`GameEngine.Runner.Avalonia`, whichever fits the existing packaging better; say why.

## What the kit provides
- **Theme:** one record of tokens (palette incl. surfaces, ink, accent, danger, rarity or tier colours;
  type scale; spacing; radii; stroke and glow; motion durations and easings). Games supply their own theme;
  ship two example themes (a dark "night coast" one fitting Tower Idle's Aurora look, and a bright one) to
  prove it is truly themeable.
- **Typography:** embedded OFL fonts loaded by the kit: one characterful display face for titles and
  numbers, one highly legible text face. Record licences in `THIRD-PARTY-NOTICES.md`. Tabular figures for
  counters.
- **Drawn surfaces, not flat boxes:** panels and frames rendered procedurally (Skia via the existing
  effect or composition paths): layered fills, inner light, subtle noise, bevel or rim highlights,
  optional corner ornaments, nine-slice style scaling, all cached. No raster art files required.
- **Components:** primary, secondary and icon buttons with press, hover and disabled states (scale and
  light response, haptic and sound hooks the game can wire); tabs; cards/list rows for shop and upgrade
  items (icon, title, description, cost with affordability state); resource counter with animated
  number roll and big-number formatting; progress and cooldown bars; badge; toggle and slider; modal
  sheet with enter and exit motion and scrim; toast; tooltip or info popover.
- **Icons:** a small procedural vector icon set (menu, pause, play, speed, settings, sound, close, back,
  upgrade, coin or salvage, lock, check, info, trophy, research, prestige) drawn from paths, themeable.
- **Accessibility and phones:** 48 dp minimum targets, respects text scaling, reduced-motion flag
  disables non-essential animation, safe-area insets, contrast checked for both example themes.
- **Performance:** the kit must not cost frame time while idle; animations only run while active. Report
  measurements from the existing benchmarks or a new focused one.

## Prove it
- A UI gallery scene in `GameEngine.Demo` that shows every component in both themes, with headless
  tests in `GameEngine.Demo.Tests` (render each component, check sizes and states, check reduced motion).
- Capture gallery PNGs to `docs/ui-kit/` at phone size (1080 x 2400) for both themes.
- Write `docs/13-ui-kit.md`: what the kit is, how a game themes and uses it, the measurements.

## Acceptance
- `dotnet build GameEngine.sln -c Release` zero warnings for the projects your workloads can build;
  `dotnet test` passes.
- Gallery captures exist and look like a premium mobile game UI, not default controls.
- Final message: packaging decision, component list, fonts and licences, performance numbers, test
  counts, and what a game must change to adopt the kit (Tower Idle is next).

## Status at handover (2026-11-12)
The first run hit the usage limit near the end. Done and committed: the `GameEngine.UI.Avalonia` project,
fonts and notices, components, gallery scene, 55 passing UI tests, captures in `docs/ui-kit/`,
`gallery-measurements.json`. Left to do:
1. Write `docs/13-ui-kit.md` (and link it from `docs/README.md`).
2. Measure and report idle cost (CPU or frames while nothing animates), not just repaint time.
3. Full `dotnet build GameEngine.sln -c Release` with zero warnings and a full `dotnet test`; fix
   anything that fails. Check the `dotnet pack` of the UI project and the workflow edits.
4. Tidy commit history if needed, delete this brief in the last commit, and give the final message the
   acceptance list asks for.
