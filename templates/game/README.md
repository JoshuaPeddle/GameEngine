# GameTemplate

A 2D game built on [GameEngine](https://github.com/JoshuaPeddle/GameEngine).

## Run it

```bash
dotnet run --project src/GameTemplate.Desktop
```

WASD or the arrow keys move the player (swipe on a touch screen); collect the gold pickups.

## Test it

```bash
dotnet test tests/GameTemplate.Tests
```

The tests boot every scene through a real engine headlessly — no window, no display. This
is the fastest way to find out whether a change to a scene or a level works.

## Edit it

- **The game** is `src/GameTemplate/MainScene.cs`. Scenes are plain C#.
- **The level** is `src/GameTemplate/levels/level1.json`. It points at
  `level.schema.json`, so an editor that understands `$schema` will complete and validate
  it as you type. The loader rejects anything it does not recognise and tells you what it
  did expect.
- **The assets** are listed in `src/GameTemplate/assets.json`.

## Publish it

`.github/workflows/publish.yml` builds every target in CI on a tag push, so you never have
to install a mobile or web SDK locally:

```bash
git tag v0.1.0 && git push --tags
```

Locally, each head needs its own toolchain:

| Head | Needs |
|---|---|
| `src/GameTemplate.Desktop` | nothing beyond the .NET SDK |
| `src/GameTemplate.Browser` | `dotnet workload install wasm-tools` |
| `src/GameTemplate.Android` | `dotnet workload install android`, plus a JDK |
| `src/GameTemplate.iOS` | `dotnet workload install ios`, plus Xcode on macOS |

`dotnet build` at the root builds every head and will fail on any workload you have not
installed. Build the head you want instead, or install the workloads you need.
