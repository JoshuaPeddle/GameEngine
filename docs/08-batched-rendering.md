# Batched rendering

Use sprite batches for large swarms, projectiles and other objects that share an atlas. Use particles for cosmetic effects that need motion and a lifetime but no ECS identity, collision or input. Ordinary entities remain useful for text, independently transformed sprites, editor picking and small populations. Batching is opt-in and does not alter their drawing order.

## Gameplay sprites

Create a render group on the simulation thread with `EntityManager.CreateSpriteBatch(image, capacity, layer, blend)`. The image is a shared `SKImage`, the blend is `SpriteBlend.Normal` or `SpriteBlend.Additive`, and the capacity is fixed. `Add` returns the instance index. Update an existing value through `batch.Instances[index]`, or call `Clear` and refill the group when membership changes. Adding beyond capacity throws, so gameplay objects are never silently discarded. Remove the group with `RemoveSpriteBatch`. Clearing the entity manager removes all groups.

A `SpriteInstance` holds a centre position in virtual coordinates, an atlas source rectangle, a uniform scale, a rotation in degrees and an optional tint. A missing tint means white. Negative uniform scale is supported. Unlike ordinary transforms, a batch does not infer its centre from a bounding box. When an ECS entity supplies the gameplay state, copy `SpriteGeometry.Center(transform.Position, boxSize)` and the current animation region into its instance during the scene update. The object can keep its collider and other components; omit its `CAnimation` if the batch is its visual, to avoid drawing it twice. Batches do not participate in editor entity picking or debug box drawing. Nonuniform scaling and independent axis mirroring continue to use ordinary sprites.

```csharp
var group = entities.CreateSpriteBatch(atlas, 1000, layer: 2);
int index = group.Add(new SpriteInstance(new Vec2(100, 200), new SKRect(0, 0, 32, 32)));
group.Instances[index] = group.Instances[index] with { Position = new Vec2(120, 200) };
```

## Particles

Every engine builds a `ParticleSystem` and updates it through the same fixed-step path as other systems. Its pooled store is allocated only when first used. Set the engine-wide maximum in the constructor, for example `new Engine(audioEnabled: false, particleCapacity: 5000)`. Standalone systems accept their own capacity. All emitters in one system share that limit. At capacity, spawning recycles the oldest living particle in constant time. `Store.Count`, `Spawned`, `Expired` and `Dropped` expose the live count and cumulative counters. Scene changes dispose the old store and reset these counters.

```csharp
var particles = engine.Systems.Get<ParticleSystem>();
var emitter = particles.CreateEmitter(new ParticleOptions
{
    Texture = atlas,
    Region = new SKRect(32, 0, 64, 32),
    Layer = 3,
    Blend = SpriteBlend.Additive,
    Shape = ParticleShape.Cone,
    Direction = -90,
    Spread = 45,
    MinSpeed = 80,
    MaxSpeed = 180,
    Life = 0.8f,
    Gravity = new Vec2(0, 120),
    Drag = 0.5f,
    StartColor = new SKColor(255, 140, 30),
    EndColor = new SKColor(255, 140, 30, 0),
    StartScale = 1,
    EndScale = 0,
    AngularVelocity = 90
}, seed: 42);
emitter.Position = new Vec2(400, 300);
emitter.Burst(100);
emitter.Rate = 250;
```

Time and rates use seconds. Direction, spread, rotation and angular velocity use degrees and degrees per second. Cone emission samples a velocity direction inside the spread. Radius emission also samples a uniform point inside a disk around the emitter. Gravity changes velocity before movement; drag multiplies velocity by `exp(-drag * seconds)`. Colour, alpha and scale interpolate linearly over life. Keep the ending RGB equal to the starting RGB when only alpha should fade. Options are immutable and shared by the particles born from that emitter. Separate emitters may select different atlas regions, materials and lifetimes.

Continuous emission retains fractional births between steps. Existing particles advance before new continuous births, which start at age zero at the end of the step. `Enabled = false` pauses emission while existing particles keep aging. `RemoveEmitter` stops automatic emission and lets existing particles finish. Burst calls explicitly emit even while automatic emission is disabled. A fixed seed plus identical options, emission commands and timestep sequence reproduce the store state on the same runtime. Cosmetic RNG is independent of gameplay RNG. No cross-architecture replay guarantee is implied.

Position, velocity, age, lifetime, colours, scales, rotation, atlas region and forces live in separate pooled arrays. An additional column references the immutable emitter options for the shared image, blend and layer. Slots are recycled through a free list and linked birth order, so expiration and dropping the oldest never shift the remaining particles or allocate particle objects. `CopyTo(Span<ParticleState>)` permits diagnostics without allocating a result array. All mutations, including emitter configuration and render-group changes, belong on the engine thread. Hosts can schedule them through `Engine.Post`.

## Snapshots, ordering and assets

Snapshot construction copies all instance transforms, source rectangles and colours into reusable arrays. The existing buffer handoff protects those arrays while the UI holds a snapshot. Returning particle storage to its pool on a scene change cannot affect a held snapshot. The UI never reads the live store or render groups.

Layers ascend across ordinary entries and atlas groups. Within a layer, ordinary entries draw first in their existing entity order, then sprite groups in registration order, then particles in birth order. Give effects a separate layer when they must sit behind or between gameplay visuals. Adjacent groups with the same image object, blend and layer combine into one atlas submission. Groups separated by another material remain separate to preserve translucent compositing order. Particles with alternating materials can therefore produce many submissions; emit related effects together and share atlas images. Grouping never sorts transparent instances by texture across intervening content.

The renderer uses `SKBlendMode.Modulate` for tinting and a paint with `SKBlendMode.Plus` for additive destination blending. Both paths inherit the same virtual viewport, camera transform, zoom and canvas clip as ordinary sprites. Atlas groups rely on canvas clipping rather than per-instance camera culling. `DrawAnimations = false` hides both paths.

SkiaSharp 4.151.1 exposes array-based atlas calls without a live-count argument. Snapshot arrays grow geometrically and retain their high-water capacity. Unused slots contain zero rectangles, zero transforms and transparent colours, so they draw nothing. `AtlasBatch.Count` reports visible instances; `SubmittedCount` reports array slots sent to Skia. This avoids allocating exact-length arrays as particles expire, with submission overhead proportional to the retained capacity. Large count reductions retain that capacity until the snapshot is discarded. Fully transparent instances and scale magnitudes below 0.0001 are omitted. A nearly singular leading transform can suppress a whole atlas call on the raster backend; filtering also avoids useless work. Each nonempty group still uses one `DrawAtlas` call. A Skia submission is not a promise about the number of GPU hardware draws.

Images are borrowed. Keep them immutable and alive until all engines and readers using them have finished, just as with existing asset textures. Neither groups, particles nor scene transitions dispose images. The demo uses one small application-lifetime atlas shared across scene resets. The ordinary sprite renderer creates one temporary image per distinct bitmap per draw frame and releases it after drawing. This preserves mutable bitmap updates between frames and removes one image wrapper allocation per sprite when a texture is shared.

## Host rates and demo

`GameView.TargetFrameRate` and `GameView.FixedTimeStep` configure engines owned by the view. Android and iOS default to 60 presentation offers and 60 simulation steps per second. Desktop retains 240 presentation offers and 120 simulation steps; browser retains 60 presentation offers and 120 simulation steps. A battery setting can use 30 presentation offers while retaining 60 simulation steps. Changes after attachment are posted to the engine thread. A borrowed engine retains its owner's rates. Core's defaults and `Engine.Tick` behavior are unchanged. These are offer rates, not measurements of display presentation.

Choose **Batched rendering** from the demo menu. It shows 500 moving swarm instances, continuous additive sparks and a Space burst. The regular headless demo harness exercises the scene and its reset, and a dedicated test checks its bounded particle population.

## Performance evidence

The Note10+ stress harness motivated this work: its normal 500 enemies, 500 projectiles and 3000 particles rose from about 26 fps with individual sprites to about 60 fps using dense atlas particles, with no stalls above 50 ms in the recorded run. The engine integration here has not yet been remeasured on that device. CPU raster timings below cannot establish mobile GPU throughput, thermal behavior or battery use.

The benchmark suite adds `AtlasBenchmarks` at 1000, 5000 and 10000 instances. Stock and batched sprite draws use the same shared texture, positions, rotations, scale, raster surface and render options. Particle draw uses additive blending, a radial distribution and the same texture and surface. Particle stepping includes continuous spawning and capacity recycling; snapshot and draw measurements are separate. Benchmarks use .NET 10.0.10 Arm64 on macOS 26.6.2 with a 1600 by 1600 CPU surface. The processor model is unavailable. BenchmarkDotNet ShortRun uses one launch, three warmups and three measured iterations, so timings are directional.

Run with `dotnet run -c Release --project GameEngine.Benchmarks -- --filter '*AtlasBenchmarks*' '*RenderBenchmarks*' '*EngineBenchmarks.BuildRenderSnapshot*' --job short`. The retained [CSV reports](benchmarks/2026-10-26) include mean, standard deviation, confidence intervals and managed allocation.


The existing benchmark before and after the change:

| Benchmark | Before mean | After mean | Before managed B/op | After managed B/op |
|---|---:|---:|---:|---:|
| BuildRenderSnapshot, 1000 entities and eight layers | 302.1 us | 305.1 us | 64 | 0 |
| DrawFrame, 1000 rotated sprites and debug boxes | 11.606 ms | 11.004 ms | 104000 | 104 |

Snapshot time is effectively unchanged within ShortRun uncertainty. The legacy frame removes 99.9 percent of managed allocation for this shared-texture workload, with a modest raster time reduction. It still submits sprites individually.

The new matched sprite comparison uses the optimized legacy renderer as its stock baseline. These are two paths in the final engine, not the historical phone measurements.

| Instances | Stock draw ms | Atlas draw ms | Atlas snapshot us | Particle step us | Particle snapshot us | Particle draw ms |
|---|---:|---:|---:|---:|---:|---:|
| 1000 | 3.431 | 1.245 | 12.07 | 9.15 | 21.28 | 0.782 |
| 5000 | 16.179 | 5.007 | 69.54 | 45.71 | 107.54 | 2.750 |
| 10000 | 31.721 | 9.751 | 119.67 | 91.75 | 212.01 | 5.262 |

All new atlas draw, particle step and snapshot cases measured 0 B/op after warmup. Stock draws measured 104 B/op at every count, one temporary image for the shared bitmap. At 10000 sprites the atlas draw is about 3.25 times faster on this CPU surface. Atlas arrays submit 1024, 8192 and 16384 slots respectively, including cleared unused capacity. The 5000-instance sprite snapshot case has an 8.44 us standard deviation; other snapshot and simulation cases are tighter. Particle draws use different positions, tint and blend from the matched sprite pair and should not be read as a direct particle-versus-sprite speedup.

## Verification

The full Release test run passes all 628 tests: 318 Core, 242 Demo, 57 Editor and 11 Avalonia host tests. Core tests cover birth, motion, life, fading, scale, gravity, drag, continuous-rate fractions, fixed seeds, recycling, oldest-first capacity dropping, grouping, ordering, camera placement, alpha/additive pixels, array reuse, concurrent reader holds, scene-reset ownership, partial initialization failure and warmed allocation counts. The demo harness runs and resets the new scene, and its dedicated regression checks visible spark pixels after 300 real engine ticks. A 960 by 640 raster capture was inspected visually.

The Release solution build succeeds across the available heads. It reports two pre-existing CS8602 nullable warnings in Android `MainActivity.cs` at lines 46 and 48. Changed Core, Demo and host code introduce no warnings. Desktop and editor launch attempts stop before scene initialization with Avalonia native RenderTimer error -6661 in this environment. A live window and the final engine path on the Note10+ and iPhone remain unverified.

The sandbox requires a writable NuGet cache and CLI state directory. Validation used `NUGET_PACKAGES=/private/tmp/gameengine-nuget` and `DOTNET_CLI_HOME=/private/tmp/gameengine-dotnet`. The build additionally uses `-p:UsedAvaloniaProducts=` to disable the build telemetry task that writes outside the workspace, `-p:JavaSdkDirectory=/opt/homebrew/opt/openjdk@17/libexec/openjdk.jdk/Contents/Home` for the installed JDK, and `-p:_ApkDebugKeyStore=/private/tmp/gameengine-debug.keystore` for the disposable Android debug key. These are command-line overrides; repository build configuration is unchanged.
