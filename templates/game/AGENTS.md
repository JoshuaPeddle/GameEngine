# GameTemplate — map for agents

A 2D GameEngine game. Everything here is small and conventional on purpose: the whole
project fits in one context window, and every format rejects what it does not understand
rather than ignoring it.

## Verify a change

```bash
dotnet test tests/GameTemplate.Tests        # boots every scene headlessly, no window needed
dotnet build src/GameTemplate.Desktop       # the no-workload build
dotnet run --project src/GameTemplate.Desktop
```

`dotnet test` is the inner loop. `SceneHarness` runs a scene through a real engine with a
fixed timestep, so a broken level or scene fails with the engine's own error, not a blank
window. Mobile and browser heads need extra workloads (see README); the desktop head and
the tests need none.

## Layout

| Path | What it is |
|---|---|
| `src/GameTemplate/MainScene.cs` | The game. Scenes are plain C# with `Initialize` and `Update`. |
| `src/GameTemplate/levels/*.json` | Levels: entities and their components. |
| `src/GameTemplate/assets.json` | Names every texture, animation and sound. |
| `src/GameTemplate/level.schema.json` | JSON Schema for level files. Levels point at it with `$schema`. |
| `src/GameTemplate/assets.schema.json` | JSON Schema for the asset manifest. |
| `src/GameTemplate.Desktop` etc. | Platform heads. They set the startup scene and nothing else. |
| `tests/GameTemplate.Tests` | Headless scene tests. |

## Level format

Every component object needs a `type`. Unknown types, unknown properties and wrong value
kinds are rejected with a message naming what was allowed — read the error, it lists the
vocabulary. Property names are case-insensitive.

| Type | Required | Optional |
|---|---|---|
| `CTransform` | `position` | `velocity`, `scale`, `rotation`, `layer` |
| `CBoundingBox` | `size`, `blockVision`, `blockMovement` | |
| `CAnimation` | `animationName` | |
| `CMovement` | `speed`, `maxSpeed` | |
| `CText` | | `text`, `size` |
| `CCamera` | | `position`, `zoom` |
| `CGravity` | | `acceleration` |
| `CInput` | | |

`position`, `velocity`, `scale` and `size` are `{ "x": <number>, "y": <number> }`.
`animationName` must name an entry in `assets.json`.

## Conventions

- **No comments in code.** Names and structure carry the meaning; anything that needs
  explaining goes in a document like this one.
- **Time is in seconds.** `Update` receives `deltaSeconds`; `CGravity.Acceleration` and
  `CMovement.Speed` are per second.
- **Coordinates are virtual.** A scene declares `VirtualWidth`/`VirtualHeight` and the
  engine letterboxes to the window.
- **`blockMovement` means solid.** A solid entity pushes a non-solid one out.
- **Scene `Update` runs after the systems**, so collisions are already resolved.
- Add a test to `tests/GameTemplate.Tests` for anything you change in a scene or a level.
