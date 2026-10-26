# Opt-in rendering effects (GE-120 through GE-125)

`GameEngine.Core.Effects` supplies runtime shaders, reduced-resolution bloom, camera response, geometric effects and deterministic PNG capture. With `RenderOptions.Effects` left null, rendering keeps its existing batched path. Camera response starts at identity and simulation time remains unchanged until a game requests hit-stop.

## Shaders and composition

Construct a `RuntimeShader` once, outside the frame loop. It compiles SkSL through `SKRuntimeEffect.CreateShader`. `Error` contains the compiler diagnostic and an optional reporting callback receives the same message. An invalid background/overlay paints its supplied fallback colour. An invalid layer or full-screen post-process preserves the unfiltered scene.

Shaders may declare these uniforms. Undeclared uniforms need no dummy values:

| Uniform | Meaning |
| --- | --- |
| `uniform float time;` | Unscaled engine presentation seconds |
| `uniform float2 resolution;` | Virtual dimensions for backgrounds, overlays and custom layers; output pixels for full-screen post-processing |
| `uniform float2 camera;` | Active camera position, or zero without a camera |
| `uniform shader content;` | Source image for a layer shader or full-screen post-process |

```csharp
using var sea = new RuntimeShader(source, SKColors.Navy, Console.Error.WriteLine);
using var grade = new RuntimeShader(gradeSource, SKColors.Transparent);
using var effects = new RenderEffects
{
    Background = sea,
    PostProcess = grade,
    Bloom = BloomQuality.Low,
    Layers = [new EffectLayer(DrawWeather), new EffectLayer(DrawEnergy, Bloom: true)]
};
engine.RenderOptions.Effects = effects;
```

The order is background, normal scene snapshots, custom layers, bloom, overlay, then full-screen post-process. Custom `EffectLayer` callbacks draw in virtual screen coordinates and may each supply a `RuntimeShader`. These are additional composition layers, independent of numeric ECS sprite layers. Use `content.eval(p)` in a layer shader to sample its rendered image. Backgrounds and overlays can animate procedurally without a source image. An overlay should return premultiplied colour and alpha. Full-screen post-processing also includes the letterbox and optional FPS counter.

Callbacks run on the paint thread. Publish immutable data or synchronize a bounded presentation buffer; do not read the live entity manager from a callback. A bloom-marked callback can run twice, so drawing must not advance state or consume RNG. The renderer restores the host canvas even if a callback throws.

Assign effects before starting, or use `RenderSystem.SetEffects` to replace/detach a running pipeline under its paint lock. The caller owns the pipeline and shaders. Detach before disposing them. `RenderSystem` owns its full-screen intermediate surface. `RenderEffects` owns its layer and bloom surfaces, paints and blur filter; disposing it does not dispose shaders that may be shared by other pipelines.

## Bloom and cost controls

Bloom collects additive snapshot batches, additive particles and custom layers marked `Bloom`. Ordinary opaque sprites do not require a luminance threshold pass. The bright buffer is transparent, blurred with Skia's image filter and composited with `SKBlendMode.Plus`. `BloomStrength` controls the contribution from zero to one.

| Quality | Bright and blur dimensions | At 720 by 1000 virtual |
| --- | --- | --- |
| Off | No bloom rendering or blur | No per-frame bloom work |
| Low | Quarter width and height, sigma 3 | 180 by 250 |
| High | Half width and height, sigma 5 | 360 by 500 |

Surfaces are retained across frames and rebuilt when dimensions or GPU context change. A GPU canvas uses the same `GRRecordingContext` for its intermediates, avoiding readback and upload. A raster canvas uses raster surfaces. Off retains already allocated buffers for cheap re-enabling. Dispose the pipeline to release them. Quality is read once per frame so a settings change cannot switch allocation policy halfway through drawing.

## Camera and geometry

`engine.CameraEffects.Shake(amplitude, seconds)` applies deterministic oscillation with linear decay. Amplitude is in virtual pixels. `ZoomPunch(amount, seconds)` adds a zoom that decays quadratically. `HitStop(seconds, timeScale)` scales system and scene delta time, with a default scale of zero. Its timer and shader time advance in real supplied tick seconds, so a complete freeze expires. Values must be finite and non-negative; hit-stop scale is bounded to zero through one. Effects alter neither saved entity positions nor physics shapes. Screen-to-world conversion applies the inverse shake and zoom.

`EffectGeometry.Trail` draws a tapered polyline, `Beam` draws a gradient streak, `Ring` draws an eased shockwave, and `Lightning` returns a bounded seeded polyline with exact endpoints. Cache lightning points at birth. `Shatter` emits bounded radial fragments using the caller's sprite region or shaped fragment texture through a normal pooled `ParticleEmitter`. `ParticleEmitter.Spawn` accepts a validated direct birth with position, velocity, scale, rotation and life. Cosmetic particles retain normal pool recycling semantics and never become ECS bodies.

## Capture

`SceneCapture.Render(engine, width, height, seconds, step)` initializes/presents a queued scene, advances that supplied engine by the requested duration using fixed steps plus a final remainder, checks engine faults and returns PNG bytes. Pass a fresh identically seeded engine/scene for each reproduction. The engine is borrowed, and capture advances its state. It does not seek an already advanced scene backwards. External clocks, assets and cosmetic seeds must also be fixed by the caller.

The callback overload `SceneCapture.Render(width, height, time, draw)` supplies an `EffectFrame` and a transparent headless canvas. It can compose a captured battlefield and a headless Avalonia UI into a phone screenshot. It does not itself advance simulation. No display server or GPU is required.

## Verification and measurements

`EffectsTests` covers compiler diagnostics and fallback pixels, all three uniforms, content grading and fallback, full-screen grading, bloom spread at three qualities and resizing, hit-stop expiry, shake/zoom decay, seeded geometry, pool saturation, canvas restoration, capture faults and repeated real-engine PNG equality. The integrated Release solution builds with zero warnings. All 694 engine tests pass: 373 Core, 249 Demo, 57 Editor and 15 Runner.

Run the reproducible desktop measurement with:

```sh
dotnet run --project GameEngine.Benchmarks -c Release -- --effects
```

`--effects --metal` additionally creates a macOS Metal surface and synchronizes each submit. This automation session could not create a Metal device, so no GPU result is claimed. Raster timings and the expected phone budget are recorded in the consuming game's `art/style-frames/verification.md`. Raster screenshot throughput must not be presented as a GPU frame-rate measurement. Both native desktop launch attempts returned Avalonia RenderTimer error `-6661`; headless rendering passes. Physical-device GPU, thermal and complete-frame measurements remain necessary to verify a 12 ms phone budget.
