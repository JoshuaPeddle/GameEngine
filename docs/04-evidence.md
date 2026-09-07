# Evidence

Reproduction harness for the findings marked **confirmed** in
[`02-findings.md`](02-findings.md), plus its measured output.

Nothing here is inferred from reading source. Each claim was executed against
`GameEngine.Core` at commit `df48b9f`.

---

## Environment

| | |
|---|---|
| Date | 2026-07-31 |
| Commit | `df48b9f` |
| OS | macOS (Darwin 25.5.0), Apple Silicon |
| SDK | .NET 10 |
| Build | Behavioural probes `Debug`; timing probes `Release` |

> Timing figures are machine-specific. What matters is the **shape** of the
> curve (quadratic) and the ratio to a frame budget — re-run locally before and
> after `GE-11` rather than comparing to these absolute numbers.

---

## Running it

Create a scratch console project outside the repo:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>disable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="../../GameEngine/GameEngine.Core/GameEngine.Core.csproj" />
  </ItemGroup>
</Project>
```

Then `dotnet run` (probes A–D want `-c Release`).

---

## Probe 1 — behavioural

### Source

```csharp
using GameEngine.Core;
using GameEngine.Core.Components;
using GameEngine.Core.Systems;
using GameEngine.Core.Utils;

// ── 1. Entity ID collisions after removal (GE-01) ───────────────────────
{
    var em = new EntityManager();
    var a = em.CreateEntity("a");
    var b = em.CreateEntity("b");
    var c = em.CreateEntity("c");
    em.Update();
    Console.WriteLine($"[1] ids after create: {a.Id},{b.Id},{c.Id}");

    b.Active = false;
    em.Update();

    var d = em.CreateEntity("d");
    em.Update();
    Console.WriteLine($"[1] after removing b, new entity 'd' got id {d.Id} " +
                      $"(collides with '{(a.Id == d.Id ? a.Tag : c.Id == d.Id ? c.Tag : "none")}')");

    var got = em.GetEntity(c.Id);
    Console.WriteLine($"[1] GetEntity({c.Id}) returned tag '{got.Tag}' — expected 'c'");
}

// ── 2. Shared cached list aliasing (GE-02) ──────────────────────────────
{
    var em = new EntityManager();
    for (int i = 0; i < 3; i++)
    {
        var e = em.CreateEntity("e" + i);
        e.AddComponent(new CTransform(new Vec2(i, i)));
    }
    em.Update();

    var first = em.GetEntitiesWith<CTransform>();
    Console.WriteLine($"[2] first call count = {first.Count}");
    try
    {
        foreach (var e in first)
            _ = em.GetEntitiesWith<CTransform>();   // wipes the list being iterated
        Console.WriteLine("[2] nested query: no throw");
    }
    catch (Exception ex) { Console.WriteLine($"[2] nested query threw {ex.GetType().Name}: {ex.Message}"); }

    var second = em.GetEntitiesWith<CTransform>();
    Console.WriteLine($"[2] same instance handed to two callers? {ReferenceEquals(first, second)}");
}

// ── 4. LevelBuilder → save → load round trip (GE-04) ────────────────────
{
    var path = Path.Combine(Path.GetTempPath(), "probe_level.json");
    new LevelBuilder("Probe")
        .AddEntity("player", p => p.AddTransform(10, 20).AddBoundingBox(5, 5).AddInput())
        .SaveToFile(path);
    Console.WriteLine("[4] written json:");
    Console.WriteLine(File.ReadAllText(path));
    try
    {
        var reloaded = LevelFile.LoadFromFile(path);
        Console.WriteLine($"[4] reloaded ok, {reloaded.Entities.Count} entities");
    }
    catch (Exception ex) { Console.WriteLine($"[4] reload threw {ex.GetType().Name}: {ex.Message}"); }
}

// ── 5. Gravity / movement unit mismatch (GE-05) ─────────────────────────
{
    var em = new EntityManager();
    var e = em.CreateEntity("faller");
    var t = new CTransform(Vec2.Zero);
    e.AddComponent(t);
    e.AddComponent(new CGravity());          // "0.2 virtual pixels per second^2"
    e.AddComponent(new CMovement(0, 1e9));   // no input accel, no effective speed cap
    em.Update();

    var physics = new PhysicsSystem();
    var movement = new MovementSystem();

    for (int i = 0; i < 60; i++)             // one simulated second at 60fps
    {
        physics.Update(em, 1000.0 / 60.0);
        movement.Update(em, 1000.0 / 60.0);
    }
    Console.WriteLine($"[5] after 1 s of gravity: velocity.Y = {t.Velocity.Y:0.###} px/s, " +
                      $"position.Y = {t.Position.Y:0.###} px");
    Console.WriteLine($"[5] expected for 0.2 px/s^2: velocity.Y ≈ 0.2, position.Y ≈ 0.1");
}

// ── 6. Pointer events before resolution is known (GE-09) ────────────────
{
    var im = new InputManager();
    try
    {
        im.HandlePointerEvent(Pointer.PointerEventType.Press,
            new Pointer.PointerEvent(new Vec2(1, 1)));
        Console.WriteLine("[6] early pointer event: no throw");
    }
    catch (Exception ex) { Console.WriteLine($"[6] early pointer event threw {ex.GetType().Name}: {ex.Message}"); }
}

// ── 7. BindAction before AddAction is silently dropped (GE-08) ──────────
{
    var im = new InputManager();
    bool fired = false;
    im.BindAction("Jump", _ => fired = true);   // action not registered yet
    im.AddAction(GeKeys.Space, "Jump");
    im.HandleKeyPress(GeKeys.Space);
    im.DoActions();
    Console.WriteLine($"[7] binding registered before AddAction ever fires? {fired}");
}
```

### Output

```
[1] ids after create: 0,1,2
[1] after removing b, new entity 'd' got id 2 (collides with 'c')
[1] GetEntity(2) returned tag 'd' — expected 'c'

[2] first call count = 3
[2] nested query threw InvalidOperationException: Collection was modified; enumeration operation may not execute.
[2] same instance handed to two callers? True

[4] written json:
{
  "metadata": {
    "name": "Probe",
    "version": "1.0",
    "description": "",
    "properties": {}
  },
  "entities": [
    {
      "tag": "player",
      "components": [
        { "position": { "x": 10, "y": 20 }, "rotation": 0 },
        { "size": { "x": 5, "y": 5 }, "blockVision": false, "blockMovement": false },
        {}
      ]
    }
  ]
}
[4] reload threw KeyNotFoundException: The given key was not present in the dictionary.

[5] after 1 s of gravity: velocity.Y = 200 px/s, position.Y = 101.667 px
[5] expected for 0.2 px/s^2: velocity.Y ≈ 0.2, position.Y ≈ 0.1

[6] early pointer event threw InvalidOperationException: Real and Virtual dimensions must be set to handle pointer events.

[7] binding registered before AddAction ever fires? False
```

**Note on `[4]`:** no component object carries a `type` field, and `AddInput()`
serialises to `{}`. `LoadFromJson` requires `type` — hence the throw.

**Note on `[5]`:** 200 px/s versus a documented 0.2 px/s² — exactly 1000×, the
ms↔s factor.

---

## Probe 2 — ordering and performance

### Source

```csharp
using System.Diagnostics;
using GameEngine.Core;
using GameEngine.Core.Components;
using GameEngine.Core.Systems;

// ── A. Draw order after removal and respawn (GE-03) ─────────────────────
{
    var em = new EntityManager();
    var made = new List<Entity>();
    for (int i = 0; i < 8; i++)
    {
        var e = em.CreateEntity("old" + i);
        e.AddComponent(new CTransform(Vec2.Zero));
        made.Add(e);
    }
    em.Update();

    string Snap()
    {
        var buf = new RenderSnapshot();
        em.BuildRenderSnapshot(buf);
        var tags = new List<string>();
        foreach (var entry in buf.Entries) tags.Add(entry.Tag);
        return string.Join(",", tags);
    }
    Console.WriteLine($"[A] initial      : {Snap()}");

    made[2].Active = false;      // despawn, the way a game kills bullets/enemies
    made[3].Active = false;
    made[5].Active = false;
    em.Update();
    Console.WriteLine($"[A] after removes: {Snap()}");

    for (int i = 0; i < 3; i++)
    {
        var e = em.CreateEntity("new" + i);
        e.AddComponent(new CTransform(Vec2.Zero));
    }
    em.Update();
    Console.WriteLine($"[A] after respawn: {Snap()}");
}

// ── B. Physics broad-phase cost vs entity count (GE-11) ─────────────────
{
    foreach (int n in new[] { 500, 1000, 2000, 3000 })
    {
        var em = new EntityManager();
        var rng = new Random(1);
        for (int i = 0; i < n; i++)
        {
            var e = em.CreateEntity("e" + i);
            e.AddComponent(new CTransform(new Vec2(rng.Next(0, 4000), rng.Next(0, 4000))));
            e.AddComponent(new CBoundingBox(new Vec2(20, 20), true, true));
        }
        em.Update();

        var physics = new PhysicsSystem();
        physics.Update(em, 16.67);                       // warm

        var sw = Stopwatch.StartNew();
        const int frames = 30;
        for (int f = 0; f < frames; f++) physics.Update(em, 16.67);
        sw.Stop();

        double msPerFrame = sw.Elapsed.TotalMilliseconds / frames;
        Console.WriteLine($"[B] {n,5} entities: {msPerFrame,7:0.00} ms/frame " +
                          $"({(msPerFrame > 16.67 ? "OVER" : "under")} a 60fps budget of 16.67 ms) " +
                          $"— {(long)n * (n - 1) / 2:N0} pair tests");
    }
}

// ── C. FPS smoothing cost as Engine configures it (GE-13) ───────────────
{
    var em = new EntityManager();
    em.Update();
    var rs = new RenderSystem(em, new RenderOptions { DrawFps = true, FpsSmoothingSamples = 1000 });
    for (int i = 0; i < 2000; i++) rs.Update(em, 16.67);   // fill the ring

    var sw = Stopwatch.StartNew();
    const int frames = 20000;
    for (int i = 0; i < frames; i++) rs.Update(em, 16.67);
    sw.Stop();
    Console.WriteLine($"[C] FPS smoothing: {sw.Elapsed.TotalMilliseconds / frames * 1000:0.0} µs/frame");
}

// ── D. Smoothing responsiveness at 1000 samples (GE-12) ─────────────────
{
    var em = new EntityManager();
    em.Update();
    var rs = new RenderSystem(em, new RenderOptions { DrawFps = true, FpsSmoothingSamples = 1000 });
    for (int i = 0; i < 1000; i++) rs.Update(em, 1000.0 / 240);   // steady 240 fps

    Console.WriteLine("[D] steady at 240 fps, then a hard drop to 30 fps:");
    for (int i = 1; i <= 1000; i++)
    {
        rs.Update(em, 1000.0 / 30);
        if (i is 1 or 60 or 300 or 600 or 1000)
        {
            var f = typeof(RenderSystem).GetField("_fps",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            Console.WriteLine($"[D]   after {i,4} frames at 30 fps, counter reads {(double)f.GetValue(rs)!:0.0}");
        }
    }
}
```

### Output (Release)

```
[A] initial      : old0,old1,old2,old3,old4,old5,old6,old7
[A] after removes: old0,old1,old4,old6,old7
[A] after respawn: old0,old1,new2,new1,old4,new0,old6,old7

[B]   500 entities:    0.26 ms/frame (under a 60fps budget of 16.67 ms) —   124,750 pair tests
[B]  1000 entities:    1.13 ms/frame (under a 60fps budget of 16.67 ms) —   499,500 pair tests
[B]  2000 entities:    5.57 ms/frame (under a 60fps budget of 16.67 ms) — 1,999,000 pair tests
[B]  3000 entities:   14.49 ms/frame (under a 60fps budget of 16.67 ms) — 4,498,500 pair tests

[C] FPS smoothing: 4.7 µs/frame

[D] steady at 240 fps, then a hard drop to 30 fps:
[D]   after    1 frames at 30 fps, counter reads 239.8
[D]   after   60 frames at 30 fps, counter reads 227.4
[D]   after  300 frames at 30 fps, counter reads 177.0
[D]   after  600 frames at 30 fps, counter reads 114.0
[D]   after 1000 frames at 30 fps, counter reads 30.0
```

**Note on `[A]`:** fresh inserts *do* enumerate in insertion order, which is why
this looks correct until something despawns. After respawn, `new2`, `new1` and
`new0` land in the freed `HashSet` slots and are drawn ahead of older entities.

**Note on `[B]`:** `SceneBasic` creates exactly 3000 collidable entities, so the
bottom row is a real scene in this repo, not a synthetic extreme. 14.49 ms is 87%
of a 60fps budget spent purely on the broad phase.

---

## Static verification

Findings confirmed by build output or exhaustive grep rather than execution.

### GE-06 — audio is unconditionally dead

```
$ grep -rn --include="*.cs" "_audioEnabled" .
GameEngine.Core/Engine.cs:19:        private bool _audioEnabled;
GameEngine.Core/Engine.cs:112:        _audioEnabled = audioEnabled;
```

Assigned once, **never read**. `Engine.cs:137` reads `if (false==true)`, which
the compiler confirms:

```
GameEngine.Core/Engine.cs(138,17): warning CS0162: Unreachable code detected
GameEngine.Core/Engine.cs(280,66): warning CS8604: Possible null reference argument
    for parameter 'audioPlayer' in 'void Scene.Initialize(..., AudioSystem audioPlayer, ...)'
```

Four call sites pass `audioEnabled: false` believing it has an effect.

### GE-22 — the legacy render path's only caller is orphaned

```
$ grep -c "Avalonia.Old" GameEngine.sln
0

$ grep -rn --include="*.cs" "DrawEntitiesToCanvas(canvas)" .
GameEngine.Runner.Avalonia.Old/SkiaCanvasControl.cs:46
```

The single-argument overload plus its helpers (~180 lines of `RenderSystem`) is
reachable only from a project that is not in the solution.

### GE-45 — CI cannot build the solution

`.github/workflows/dotnet.yml:20` pins `dotnet-version: 9.0.x`; every project
targets `net10.0`. Restore fails with `NETSDK1045`.

### GE-47 — build warnings

~30 nullable warnings across Core and Demo, including 3× `CS8618` on `Engine`'s
public `Systems` / `EntityManager` / `InputManager` fields, plus the `CS0162`
above.

---

## Baseline to preserve

Captured at `df48b9f` so regressions during the action plan are visible:

```
$ dotnet test GameEngine.Core.Tests/GameEngine.Core.Tests.csproj
Passed!  - Failed: 0, Passed: 17, Skipped: 0, Total: 17, Duration: 2 s
```
