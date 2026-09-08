# Engine review after Emberbrook

September 7, 2026. This is a usage-driven review of the current code, not a new
performance benchmark. Emberbrook's 601-test baseline and both boss playthroughs
show that the simulation foundation works. Most friction is now in game-authoring
APIs and host integration. Implementation progress is recorded below; remaining proposals are not yet implemented.

## What to preserve

`Engine.Tick` makes real games testable without a window. Virtual coordinates,
immutable render snapshots, strict asset/level formats, and the separation of
native audio from Core are worth keeping. The RPG's inventory, quests, combat,
and navigation can be tested independently of their presentation. Keep those game
rules in the demo; a generic RPG framework would make this engine harder to learn.

## Findings, ordered by value

| ID | Priority | Concrete friction | Proposed improvement |
|---|---|---|---|
| GE-91 | First | Avalonia and WinForms classify every pointer release into a keyboard gesture, including Space for a click. Emberbrook had to leave Space unbound to avoid cancelling actions. Avalonia also forwards pointer movement only while pressed, so ordinary hover feedback cannot follow the mouse. | Separate raw pointer events from opt-in gesture-to-action mapping. Move swipe bindings into game configuration and forward unpressed hover. |
| GE-92 | First | `Assets.GetAnimation(name, size)` resizes the entire sheet. A four-frame clip requested at 40×40 produces ten-pixel-wide frames. `SceneEmberbrook.Drawing` compensates by multiplying width by frame count. | A frame-size API with explicit sampling, defaulting pixel art to nearest-neighbour; retain clearly named sheet-size compatibility. Prefer draw-time destination sizing over decoded copies where practical. |
| GE-93 | First | Buttons need separate sprite/text entities, independent visibility, manual rectangles, and special modal exclusions. Text wrapping counts characters instead of measuring glyphs. The fieldbook heading overlapped a button during this implementation. | A small optional game-UI layer: measured text blocks, horizontal/vertical layout, padding, buttons with one visibility/hit-test state, and input-consuming panels. Keep it UI-framework independent. |
| GE-94 | Next | New entities cannot be found by query until the next manager update. Looking up celebration labels immediately after creating them caused a scene initialization fault. Region changes manually retain and retire lists of entities. | Preserve deferred query semantics, but return typed handles from builders and offer an entity group/scope that can retire its children. Do not make queries sometimes include pending entities. |
| GE-95 | Next | `Scene` has Initialize/Update but no release hook. Each new Emberbrook scene creates an `Assets`; Engine deliberately does not dispose those assets. The lifetime contract requires an owner that the scene API does not supply. | Engine/session-owned asset cache plus explicit leases. Release only after snapshots that borrow textures are finished. Add a lifecycle test covering repeated menu/game transitions and balanced allocation/disposal. This is a code-level ownership gap, not a measured memory-growth claim. |
| GE-96 | Next | The game abstracts save storage, but the default is a filesystem store that rejects browsers. Audio has a capability boundary; saves require a separate game-specific solution in every host. | A small host-service context for storage and capability reporting. Keep save schema/validation in the game. Support browser persistence and explicit unavailable states, not browser checks spread across scene code. |
| GE-97 | First | `SceneHarness.Run` catches thrown exceptions and checks finite transforms, but `Engine.Tick` intentionally captures scene faults. A frozen faulted scene can therefore pass a test that does not inspect the fault or require changed state. | Assert `Engine.Fault == null` after every ordinary tick, including initial load. Add an explicit expected-fault mode for tests exercising recovery. Include complete pointer press/move/release and host gesture policy in the harness. |
| GE-98 | Later | Each tile is a sprite entity; every content milestone rebuilds the region, even accepting a delivery that changes no map art. Definition names, sprite keys, labels and hover behavior are repeated in scene switches. | First separate visual revision from save revision and centralize site presentation definitions in the demo. Profile before adding a Core tilemap/static-layer renderer. No current measurement establishes a frame-rate problem. |

Relevant code: `GameEngine.Runner.Avalonia/GameEngine.Runner.Avalonia/GameView.cs`,
`GameEngine.Runner.Winforms/MainView.cs`, `GameEngine.Core/Animation.cs`,
`GameEngine.Core/Assets.cs`, `GameEngine.Core/Scene.cs`,
`GameEngine.Demo/SceneEmberbrook.cs`, `SceneEmberbrook.Panels.cs`,
`SceneEmberbrook.Village.cs`, and `GameEngine.Demo.Tests/SceneHarness.cs`.
Paths without a project prefix in this paragraph are in `GameEngine.Demo`.

## Suggested implementation sequence

1. **Make failures and input honest (GE-97, GE-91).** A deliberately faulting
   scene must fail a normal harness run. A desktop click emits pointer events
   without a key unless the scene opted into that mapping. Mobile swipe demos
   continue working through explicit bindings. Hover works without dragging.
2. **Make sprites and UI straightforward (GE-92, GE-93).** The same 40×40 sprite
   request works for one-frame and four-frame assets. One button owns its visual
   and interaction state. A hidden or covered button cannot receive input; text
   fits by measured width. Migrate one workbench before migrating all demos.
3. **Make ownership explicit (GE-94, GE-95).** Construct a panel or map group,
   retain its handles, and retire it once. Repeated scene transitions do not
   accumulate decoded textures; snapshots remain safe during teardown.
4. **Make hosts interchangeable (GE-96).** Expose save availability, persistence,
   and failures through one contract. Validate browser reload/save/load and desktop
   atomic replacement independently. The Pages delivery adds a game-specific
   browser adapter first; that is not yet the general engine service design.
5. **Improve content authoring before optimizing (GE-98).** Reduce duplicated
   site definitions and needless rebuilds. Measure allocations, map rebuild time,
   and frame time before committing to a tilemap renderer or broader ECS changes.

## What should remain game code

Quest stages, item prices, recipe requirements, skill thresholds, enemy behavior,
and the RPG's four-neighbour pathfinding belong in Emberbrook. Shared helpers
should earn their place through a second game needing the same behavior. A quest
DSL, large animation graph editor, multiplayer layer, and procedural world builder
would add more surface area than this project currently needs.

The highest-return improvement is a compact UI/input layer backed by better
failure assertions. It addresses bugs players have actually encountered and
removes substantially more authoring work than another low-level ECS optimization.

## Findings from the web deployment

The trimmed publish exposed two reflection-dependent paths (GE-99): level metadata
JSON and the Avalonia view locator. They now use generated JSON metadata and an
explicit view mapping. Browser saves also use a generated JSON context.

A real Chromium session with a backing surface twice the CSS dimensions exposed a separate host input defect
(GE-100): the engine recorded a 2560×1600 input viewport while DOM pointer positions
were in a 1280×800 CSS viewport. Correct-looking rendering alone did not catch it.
The browser now supplies its CSS viewport dimensions to the view's input mapping;
desktop keeps its normal control bounds. Chromium checks cover starting an adventure,
walking, saving through the UI, and continuing after a reload. A headless host test
checks the backing-surface/CSS coordinate mapping.

## Implementation progress

GE-91 and GE-97 are implemented on the first engine improvement branch:

- Both hosts forward raw pointer events once, including unpressed hover. WinForms'
  duplicate event subscriptions and both hosts' synthetic key timers are removed.
- `InputManager.BindGestureAction` opts a scene into named tap/directional actions.
  Gestures use virtual coordinates and a configurable duration in simulation seconds,
  independent of physical key state. The legacy mobile demos opt in explicitly;
  Emberbrook and pointer-controlled demos retain raw pointer input.
- `SceneHarness` rejects captured faults after initial load and every normal tick.
  `ExpectFault` supports deliberate failure/recovery tests. Its pointer helper sends
  press, move, and release; Emberbrook's click tests now use that complete sequence.
- Regression coverage includes actual Avalonia mouse events, hover, gesture expiry,
  held keyboard keys, letterboxing, scene reset, and captured scene failures.

Next: frame-sized animations (GE-92), then a compact UI layer exercised by one
Emberbrook workbench (GE-93). Asset ownership and host services remain open.
