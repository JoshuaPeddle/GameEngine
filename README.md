# GameEngine

A 2D entity–component–system game engine in C#, rendering through SkiaSharp, with
runners for desktop, mobile, and the browser, plus an editor that compiles and
previews scenes live.

Targets .NET 10.

## Start a game

```bash
dotnet new install GameEngine.Templates
dotnet new gameengine-game -n MyGame
cd MyGame
dotnet run --project src/MyGame.Desktop
```

That gives you a playable game with desktop, Android, iOS and browser heads, a headless
test project, an `AGENTS.md`, and a GitHub Actions workflow that publishes every target on
a tag push. Or add the engine to a project you already have:

```bash
dotnet add package GameEngine.Core            # the engine, UI-agnostic
dotnet add package GameEngine.Runner.Avalonia # the Avalonia GameView, if you want a host
```

---

## Layout

| Project | What it is |
|---|---|
| `GameEngine.Core` | The engine: ECS, systems, rendering, input, level format |
| `GameEngine.Demo` | Sample games — Emberbrook RPG, Void Bastion, Void Siege, Void Salvage, Tetris, Astral Relay, Pong, Brick Breaker, Snake, side-scrollers |
| `GameEngine.Runner.Avalonia` | Shared Avalonia host, with Desktop / Android / Browser / iOS heads |
| `GameEngine.Runner.Winforms` | WinForms host |
| `GameEngine.Editor` | Scene and level editor (Avalonia) |
| `GameEngine.Editor.Desktop` | Editor entry point |
| `GameEngine.Editor.ImageGen` | ComfyUI client for generating sprite art |
| `GameEngine.Core.Tests` | Unit and integration tests for the engine |
| `GameEngine.Demo.Tests` | Headless smoke tests that drive every demo scene |
| `GameEngine.Editor.Tests` | Headless view-model tests for the editor |
| `templates` | The `dotnet new gameengine-game` template pack |

## Running

```bash
dotnet run --project GameEngine.Runner.Avalonia/GameEngine.Runner.Avalonia.Desktop
dotnet run --project GameEngine.Runner.Winforms          # Windows only
dotnet run --project GameEngine.Editor.Desktop           # the editor
```

**Emberbrook RPG** is the first demo-menu entry. Choose New adventure or Continue.
Click to walk and interact; release the mouse and the action continues. **X** stops
an action; **M** repeats gathering. Backpack, Skills, and Journal tabs keep the
current activity running while you inspect your progress.

Complete four connected quests through the village, Sunken Halls, and Ashen Trail.
Gather ore, fish, and ash; use explicit recipe panels to craft equipment and batch
cook food. The **Broken Crossing** is a parallel project after Copper Promise:
pay Bram with materials or coins to open a shortcut and learn smoked trout.
Reinforced tools open a richer vein; an ash spear trades shield protection for
reach. Defeated bosses, a restored bell, a feast, and repaired crossings leave
persistent changes in the world.

The twelve-slot backpack stacks materials to five and smoked trout to two.
The bank supports all item types. **E** eats food; **T** switches the earned charm;
**S/L** save/load; **Q** returns to the menu. Safe village milestones autosave,
with a persistent status for failures. The Sound button mutes feedback cues.

This chapter uses version 6 saves and requires a fresh adventure for older saves.
Desktop saves live under local application data at
`GameEngine/Emberbrook/save.json`; browser disk saves are not supported yet.
See [the RPG play guide](GameEngine.Demo/Emberbrook/README.md) for recipes, quests,
controls, and verification, and [the design plan](GameEngine.Demo/Emberbrook/DESIGN.md)
for the chapter's rationale and remaining human playtest questions.

**Void Bastion** is available in the demo menu: a twelve-wave tower defence game
in the same neon sci-fi setting. Click a socket and a tower card to build. Pulse guns
handle light drones, Rail guns pierce armor and shields, Cryo slows groups, and Mortars
fire predictive splash shells. Every tower has three tiers and First / Strongest / Nearest
targeting; selling recovers 70% of its investment.

Press Space to send the next convoy. E triggers an ion storm that strips shields and slows
enemies; P pauses while you plan or build, and B toggles double speed. Arrow keys select
sockets, A/S/D/F build, U upgrades, X sells, T changes targeting, R restarts, and Q returns
to the menu. Clear rewards fund reinforcements and repair one point of core hull. Wardens
arrive on waves 4, 8, and 12 and deal six core damage if they escape.

`BastionBattle` owns deterministic combat and economy rules; `SceneVoidBastion` maps them
onto pooled engine entities and mouse/keyboard input. Tests play the campaign through
`Engine.Tick`, including victory, defeat, letterboxing, pause, speed, and economy checks.

**Void Siege** is also available in the demo menu. Press Space to start a five-sector
arena campaign: automatic targeting, pursuing drones, strafing gunners, splitting enemies,
destructible cover, salvage repairs, upgrade choices, and a two-phase bullet-pattern boss.
Move with WASD or arrows, dash with Space, and use E for an EMP that clears nearby enemy
shots and stuns drones. Between sectors, choose F for more projectiles, G for firing and
movement speed, or H for armor and repairs. R restarts; Q returns to the menu.

The game runs through Core's movement and swept collision systems. It caps projectiles at
220 and reuses 128 particle entities. `SceneVoidSiegeTests` exercises combat, cover, abilities,
wave transitions, splitting, the boss, victory, and defeat through the headless engine.

**Void Salvage** is a three-sortie cargo extraction game using the Void fleet artwork.
Press Space to launch, move with WASD or arrows, and collect green cores on contact.
Cargo trails behind your tug and slows it down; return to the carrier ring to bank it
and repair one hull. Meet each sortie's quota before the 65-second jump window closes.
Space dashes and destroys raiders on contact. Enemy hits scatter recoverable cargo;
the carrier repels nearby raiders. Between sorties choose F for cargo capacity, G for
engine speed, or H for armor; every upgrade fully repairs the tug. R restarts, Q opens
the menu. Headless tests cover towing, banking, damage, swept ramming, upgrades,
the complete campaign, deadline failure, and scene navigation.

Android, iOS and Browser heads need their platform workloads and SDKs installed.

```bash
dotnet test                                              # both test suites
docker build -f tests.Dockerfile .                       # both suites on Linux
```

On Linux the test projects need `SkiaSharp.NativeAssets.Linux` (already referenced)
and the system package `libfontconfig1`. Without the latter, `libSkiaSharp` fails to
load and the test host crashes rather than reporting failures.

## Writing a scene

A scene builds entities and reacts to collisions. `Initialize` runs once when the
scene is loaded; `Update` runs every frame, after the systems.

```csharp
public class MyScene : Scene
{
    public override int VirtualWidth => 800;
    public override int VirtualHeight => 600;

    private Assets? assets;

    public override void Initialize(EntityManager entities, InputManager input,
        AudioSystem? audio, Action<Scene?> resetScene)
    {
        assets ??= new Assets("assets.json", AssetSource);

        input.AddAction(GeKeys.W, "Up");
        input.AddAction(GeKeys.Up, "Up");
        input.AddAction(GeKeys.S, "Down");
        input.AddAction(GeKeys.Down, "Down");

        var player = entities.CreateEntity("player");
        player.AddComponent(new CTransform(new Vec2(100, 100)));
        player.AddComponent(new CAnimation(assets.GetAnimation("PlayerIdle")));
        player.AddComponent(new CBoundingBox(new Vec2(32, 32), blockVision: false, blockMove: false));
        player.AddComponent(new CMovement(600, 250));
        player.AddComponent<CInput>();

        input.ActionMapper.MapActionToComponent<CInput>("Up", player, (c, held) => c.Up = held);
        input.ActionMapper.MapActionToComponent<CInput>("Down", player, (c, held) => c.Down = held);
    }

    public override void Update(EntityManager entities, SystemContainer systems, double deltaSeconds)
    {
        foreach (var collision in systems.Get<PhysicsSystem>().CollisionEvents)
        {
            // collision.A, collision.B, collision.Overlap,
            // and collision.VelocityA / VelocityB as they were on impact
        }
    }
}
```

Point a runner at it by setting the startup scene in that host's entry point —
for Avalonia, `App.StartupScene = () => new MyScene();`.

## Input

`GeKeys` is the engine's key vocabulary: `A`–`Z`, `Space`, and `Up`/`Down`/`Left`/`Right`.
Each runner builds its own map from those names — Avalonia and WinForms parse them against
their own key enums, the browser head maps them to DOM key names — so a value added to
`GeKeys` reaches every platform without touching a runner.

Keys are bound to named actions, and **several keys can share one action**: an action stays
active until the last key holding it is released, so binding both `D` and `Right` to
`"Right"` behaves the way a player expects.

Pointer press, move (including hover), and release never synthesize keyboard input.
Games opt into gestures explicitly, for example
`inputManager.BindGestureAction(PointerGesture.Up, "Jump")` or
`inputManager.BindGestureAction(PointerGesture.Tap, "Jump")`. Gesture actions last
0.1 seconds of simulation time by default; the optional third argument sets that
duration. They share named action callbacks with keys without releasing a held key.
Gesture distances use virtual coordinates; presses outside the viewport are ignored.
Bindings and active gestures are cleared on scene changes.

## Sprite frame sizes

`assets.GetAnimationForFrame("PlayerIdle", new Vec2(40, 40))` draws every frame at
40×40 virtual pixels, regardless of the number of frames in the sheet. It shares
the original texture and caches the animation variant; no resized bitmap is allocated.
Nearest-neighbour sampling is the default. Pass `SKFilterMode.Linear` as the third
argument for smooth scaling. Transform scale and rotation still apply, and drawing,
culling, and editor picking agree on the requested size.

`GetAnimationForSheet(name, size)` names the older whole-sheet resizing behavior
explicitly. The existing `GetAnimation(name, size)` overload retains that behavior
for compatibility.

## Small game UI

`GameEngine.Core.UI` provides panel/button/text data and `UiLayout.Row`, `Column`,
and `Inset` helpers in virtual coordinates. `GameEngine.Core.Systems.UiSystem`
creates and retains the sprite/text entities, measures text with the renderer's
font, and updates them together. Rendering still goes through `RenderSystem`.

```csharp
var ui = new UiSystem(entityManager);
var panel = ui.AddPanel(new SKRect(20, 20, 244, 90), layer: 120);
var row = UiLayout.Row(UiLayout.Inset(panel.Bounds, 8), 2, gap: 8);
var make = ui.AddButton(panel, "make", row[0], "Make bar",
    assets.GetAnimationForFrame("Button", new Vec2(100, 28)), MakeBar);
make.Enabled = canCraft;
ui.Update();
```

Keep the UI owner with the scene. Update the models and call `ui.Update()` after
gameplay updates, including initialization. Before handling a world click, call
`ui.TryPress(position)` and return if it consumes the press. A disabled button
consumes its click without invoking its action. Hidden controls neither draw nor
activate. A visible modal panel consumes clicks outside its bounds too, blocking
panels below it. Give panels distinct layers; buttons added later in one panel
appear above earlier buttons and take input first.

Panel bounds define the input region, not a background image. Child bounds are
absolute virtual coordinates and must fit inside their panel. Button captions
ellipsize to the available width; text blocks wrap by glyph width with an ellipsis
when their line budget is exhausted. Unchanged controls reuse their measured
layout. Background animations are borrowed and follow the normal asset lifetime.
Emberbrook's shared forge/cooking workbench is the first consumer.

## Embedding

`GameEngine.Core` has no UI dependency: `Engine.Tick(deltaSeconds)` advances one frame on
whatever thread you call it from, rendering hands out immutable snapshots, and input is a
queue. The headless test harness embeds it that way, so embedding is a tested path rather
than a claim.

For Avalonia, `GameView` is a plain control you can hand everything to, so several games
can run side by side in one process:

```xml
<local:GameView x:Name="Preview" AudioEnabled="False" />
```

```csharp
Preview.Engine = myEngine;       // or leave it and set Scene / AssetSource instead
Preview.Scene = new MyScene();
Preview.AssetSource = new FileAssetSource(contentRoot);
```

An engine you supply is driven by the view but not owned by it: the view attaches its
invalidation callback and leaves the run loop to you. Set nothing and the view builds its
own engine, falling back to `App.AssetSource` / `App.StartupScene` — the convenience layer
the single-game app template uses.

### Things worth knowing

- **Time is in seconds.** Every `Update` receives `deltaSeconds`, and component
  values like `CGravity.Acceleration` are in virtual pixels per second squared.
- **Coordinates are virtual.** A scene declares its own `VirtualWidth`/`VirtualHeight`
  and the engine letterboxes to the real window. Pointer input is mapped back
  through the same transform, so both agree.
- **Draw order** is spawn order, then `CTransform.Layer` if a scene sets one.
- **`CBoundingBox.BlockMovement` means "solid".** A solid entity pushes a non-solid
  one out; two solids separate, with a stationary one staying put; two non-solid
  boxes only raise a collision event and move nothing.
- **Scene logic runs after the systems**, so by the time `Update` sees a collision
  the positions are already resolved. Use `VelocityA`/`VelocityB` from the event
  if you need the velocity at impact.

## Components

| Component | Purpose |
|---|---|
| `CTransform` | Position, velocity, rotation, scale, layer |
| `CBoundingBox` | Size and solidity |
| `CAnimation` | Sprite strip playback |
| `CText` | Text drawing |
| `CInput` | Directional input flags |
| `CMovement` | Speed and maximum speed |
| `CGravity` | Downward acceleration |
| `CCamera` | View position and zoom; tag the entity `camera` to make it active |

## Assets

`assets.json` sits beside the executable and names everything a game can ask for.

```json
{
  "$schema": "./assets.schema.json",
  "textures": { "TexPlayer": "images/player.png" },
  "animations": { "PlayerIdle": { "texture": "TexPlayer", "frames": 4, "frameDelayMs": 250 } },
  "sounds": { "Hit": "sounds/hit.wav" }
}
```

Frames are read left to right across a single horizontal strip; `frameDelayMs` of `0`
means a static image. Unknown sections and unknown keys are rejected with a message
listing what was allowed. `GameEngine.Core/assets.schema.json` is the published schema —
point a manifest at it with `$schema` and an editor will complete and check it.

The old positional format (`Texture TexPlayer images/player.png` in `assets.txt`) is still
read so existing projects keep working, including its `Font` lines, which remain ignored.

### Where assets are read from

Every read goes through an `IAssetSource` that the host supplies when it builds
the engine:

```csharp
var engine = new Engine(invalidate, assetSource: new FileAssetSource());
```

`FileAssetSource` is the default: it opens rooted or already-existing paths as
given, and resolves everything else under `assets/`. Platforms whose content is
packaged rather than on disk — Android and browser — pass a
`DelegateAssetSource` wrapping their own loader. A scene reads the engine's
source through the inherited `AssetSource` property, so nothing needs a global.

## Level files

Levels are JSON and load through `LevelFile` / `LevelLoader`, or can be built in
code with `LevelBuilder`.

```json
{
  "$schema": "level.schema.json",
  "metadata": { "name": "Training Grounds", "version": "1.0" },
  "entities": [
    {
      "tag": "player",
      "components": [
        { "type": "CTransform", "position": { "x": 100, "y": 100 }, "rotation": 0 },
        { "type": "CBoundingBox", "size": { "x": 32, "y": 32 },
          "blockVision": false, "blockMovement": false },
        { "type": "CInput" }
      ]
    }
  ]
}
```

Every component object needs a `type`. Required and optional fields:

| Type | Required | Optional |
|---|---|---|
| `CTransform` | `position` | `velocity`, `scale`, `rotation`, `layer` |
| `CBoundingBox` | `size`, `blockVision`, `blockMovement` | |
| `CMovement` | `speed`, `maxSpeed` | |
| `CAnimation` | `animationName` | |
| `CText` | | `text`, `size` |
| `CCamera` | | `position`, `zoom` |
| `CGravity` | | `acceleration` |
| `CInput` | | |

`position`, `velocity`, `scale` and `size` are `{ "x": <number>, "y": <number> }`.
Property names are case-insensitive, and `//` comments are allowed.

The loader is strict: an unknown component type, an unknown property or a value of the
wrong kind is rejected with a message naming what was allowed, and suggesting the nearest
known name when it looks like a typo. Nothing is silently ignored — a level that loads is
a level the engine understood in full.

`GameEngine.Core/levels/level.schema.json` is the published JSON Schema. It is generated
from `ComponentSchemas`, the same table the loader validates against, and a test fails if
the two ever disagree. Point a level at it with `$schema` and an editor will complete and
check it as you type.

The loader applies no game-specific behaviour. To react to a particular tag —
wiring an entity called `player` to movement keys, say — register a handler:

```csharp
var loader = LevelManager.CreateLoader(assets);
loader.RegisterEntityHandler("player", (entity, input, audio) => { /* ... */ });
```

## Testing

`GameEngine.Core.Tests` covers the engine directly. `GameEngine.Demo.Tests` runs
every shipped scene through a real `Engine` headlessly with a fixed timestep,
checking each one loads, survives several hundred frames without faulting or
producing non-finite transforms, publishes a renderable snapshot, and survives a
reset — plus per-scene checks on what each demo exists to show.

`Engine.Tick(deltaSeconds)` is what makes that possible: it advances one frame
deterministically without starting a thread, and both run loops are built on it,
so the harness exercises the same path the runners do.

## Known limitations

- **Audio is Windows-only in practice.** `GameEngine.Core` has no audio
  integration of its own: it defines `IAudioBackend`, and a host registers one
  with `AudioBackends.Factory` before constructing an engine. The only backend
  that ships is `GameEngine.Audio.Sdl`, and `ppy.SDL2-CS` ships SDL2 natives per
  platform but no `SDL2_mixer` — the only mixer binary in the tree is a Windows
  x64 DLL. Everywhere else, and on any head that registers no backend at all, the
  audio service reports itself unavailable through
  `AudioSystem.UnavailableReason` and the game runs silently rather than
  failing. Adding a platform means writing an `IAudioBackend`, not changing Core.
- `Font` entries in the old `assets.txt` format are parsed but ignored; `assets.json` has
  no fonts section at all.
- Animations support a single horizontal strip only — no grids, no per-frame
  timing, no non-looping playback.

## License

MIT — see [`LICENSE`](LICENSE). Dependency and redistributed-binary notices are in
[`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md). Demo art and audio are CC0
(`GameEngine.Demo/assets/LICENSE`).

## Web client

Play [Emberbrook on the web](https://joshuapeddle.com/GameEngine/).

The browser build opens Emberbrook directly; Q returns to the complete demo menu.
Progress is saved in this browser using local storage. The first visit downloads
the WebAssembly runtime and game assets. Browser audio is not yet connected.

`.github/workflows/pages.yml` publishes the static client after pushes to
`master` or `reliability-plan`. GitHub Pages uses GitHub Actions as its source.

The workflow uses the interpreted WebAssembly build for a shorter deployment
cycle. Release packaging retains its existing AOT configuration. All asset URLs
are relative, so the client works under a repository path as well as a domain root.
