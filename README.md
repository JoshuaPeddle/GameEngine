# GameEngine

A 2D entity–component–system game engine in C#, rendering through SkiaSharp, with
runners for desktop, mobile, and the browser, plus an editor that compiles and
previews scenes live.

Targets .NET 10.

---

## Layout

| Project | What it is |
|---|---|
| `GameEngine.Core` | The engine: ECS, systems, rendering, input, level format |
| `GameEngine.Demo` | Sample games — Pong, Brick Breaker, Snake, side-scrollers |
| `GameEngine.Runner.Avalonia` | Shared Avalonia host, with Desktop / Android / Browser / iOS heads |
| `GameEngine.Runner.Winforms` | WinForms host |
| `GameEngine.Runner.Maui` | MAUI host |
| `GameEngine.Editor` | Scene and level editor (Avalonia) |
| `GameEngine.Editor.Desktop` | Editor entry point |
| `GameEngine.Editor.ImageGen` | ComfyUI client for generating sprite art |
| `GameEngine.Core.Tests` | Unit and integration tests for the engine |
| `GameEngine.Demo.Tests` | Headless smoke tests that drive every demo scene |

## Running

```bash
dotnet run --project GameEngine.Runner.Avalonia/GameEngine.Runner.Avalonia.Desktop
dotnet run --project GameEngine.Runner.Winforms          # Windows only
dotnet run --project GameEngine.Editor.Desktop           # the editor
```

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
        assets ??= new Assets("assets.txt");

        input.AddAction(GeKeys.W, "Up");
        input.AddAction(GeKeys.S, "Down");

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

`assets.txt` sits beside the executable and is read line by line.

```
Texture  TexPlayer   images/player.png
Animation PlayerIdle TexPlayer 4 250
Sound    Hit         sounds/hit.wav
```

`Animation` takes a texture name, a frame count, and a per-frame delay in
milliseconds. Frames are read left to right across a single horizontal strip. A
delay of `0` means a static image. `Font` is parsed but not yet implemented.

## Level files

Levels are JSON and load through `LevelFile` / `LevelLoader`, or can be built in
code with `LevelBuilder`.

```json
{
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
| `CTransform` | `position` | `rotation`, `layer`, `scale` |
| `CBoundingBox` | `size`, `blockVision`, `blockMovement` | |
| `CMovement` | `speed`, `maxSpeed` | |
| `CAnimation` | `animationName` | |
| `CText` | | `text`, `size` |
| `CCamera` | | `position`, `zoom` |
| `CGravity` | | `acceleration` |
| `CInput` | | |

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

- **Audio is Windows-only in practice.** `ppy.SDL2-CS` ships SDL2 natives per
  platform but no `SDL2_mixer`, and the only mixer binary in the tree is a
  Windows x64 DLL. Elsewhere `AudioSystem.TryCreate()` returns null and the game
  runs silently.
- `Font` entries in `assets.txt` are parsed but ignored.
- Animations support a single horizontal strip only — no grids, no per-frame
  timing, no non-looping playback.
- The MAUI runner installs a system-wide keyboard hook.
