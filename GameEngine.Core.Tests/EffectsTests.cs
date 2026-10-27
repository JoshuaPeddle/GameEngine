using GameEngine.Core;
using GameEngine.Core.Components;
using GameEngine.Core.Effects;
using GameEngine.Core.Systems;
using SkiaSharp;

namespace GameEngine.Core.Tests;

public class EffectsTests
{
    [Test]
    public void ShaderReportsCompilerErrorAndPaintsFallback()
    {
        string? diagnostic = null;
        using var shader = new RuntimeShader("not sksl", SKColors.Coral, message => diagnostic = message);
        using var effects = new RenderEffects { Background = shader };
        using var bitmap = new SKBitmap(32, 32);
        using var canvas = new SKCanvas(bitmap);
        effects.DrawBackground(canvas, new(0, new(32, 32), default));
        Assert.Multiple(() =>
        {
            Assert.That(diagnostic, Does.Contain("SkSL shader compilation failed"));
            Assert.That(bitmap.GetPixel(16, 16), Is.EqualTo(SKColors.Coral));
        });
    }
    [Test]
    public void ShaderReceivesTimeResolutionAndCamera()
    {
        using var shader = new RuntimeShader("uniform float time; uniform float2 resolution; uniform float2 camera; half4 main(float2 p) { return half4(time, p.x/resolution.x, camera.x, 1); }", SKColors.Red);
        using var effects = new RenderEffects { Background = shader };
        using var bitmap = new SKBitmap(32, 32);
        using var canvas = new SKCanvas(bitmap);
        effects.DrawBackground(canvas, new(.25f, new(32, 32), new(.75f, 0)));
        var color = bitmap.GetPixel(16, 16);
        Assert.Multiple(() =>
        {
            Assert.That(shader.Error, Is.Null);
            Assert.That(color.Red, Is.InRange(62, 66));
            Assert.That(color.Green, Is.InRange(129, 133));
            Assert.That(color.Blue, Is.InRange(190, 194));
        });
    }
    [Test]
    public void ReducedBackgroundPreservesCoordinatesAndOffUsesFallback()
    {
        using var shader = new RuntimeShader("uniform float2 resolution; half4 main(float2 p) { return half4(p.x/resolution.x,0,0,1); }", SKColors.Navy);
        using var effects = new RenderEffects { Background = shader, BackgroundScale = .25f,
            BackgroundLayers = [new((canvas, _) => { using var paint = new SKPaint { Color = SKColors.Lime }; canvas.DrawRect(0, 0, 4, 4, paint); })] };
        foreach (int size in new[] { 64, 96, 64 })
        {
            using var bitmap = new SKBitmap(size, size);
            using var canvas = new SKCanvas(bitmap);
            effects.DrawBackground(canvas, new(0, new(size, size), default));
            Assert.That(bitmap.GetPixel(size / 2, size / 2).Red, Is.InRange(124, 135));
            Assert.That(bitmap.GetPixel(1, 1), Is.EqualTo(SKColors.Lime));
            effects.BackgroundScale = 0;
            effects.DrawBackground(canvas, new(0, new(size, size), default));
            Assert.That(bitmap.GetPixel(size / 2, size / 2), Is.EqualTo(SKColors.Navy));
            effects.BackgroundScale = .25f;
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => effects.BackgroundScale = float.NaN);
    }
    [Test]
    public void LayerShaderCanGradeItsContentAndFallbackPreservesIt()
    {
        using var shader = new RuntimeShader("uniform shader content; half4 main(float2 p) { half4 c = content.eval(p); return half4(c.g,c.r,c.b,c.a); }", SKColors.Red);
        using var bad = new RuntimeShader("invalid", SKColors.Red);
        using var bitmap = new SKBitmap(32, 32);
        using var canvas = new SKCanvas(bitmap);
        foreach (var candidate in new[] { shader, bad })
        {
            using var effects = new RenderEffects { Layers = [new((c, _) => c.Clear(SKColors.Green), Shader: candidate)] };
            effects.DrawLayers(canvas, new(0, new(32, 32), default));
            var color = bitmap.GetPixel(16, 16);
            Assert.That(candidate == shader ? color.Red : color.Green, Is.GreaterThan(100));
        }
    }
    [TestCase(BloomQuality.Off)]
    [TestCase(BloomQuality.Low)]
    [TestCase(BloomQuality.Medium)]
    [TestCase(BloomQuality.High)]
    public void BloomSpreadsOnlyOptedInLayersAndHandlesResize(BloomQuality quality)
    {
        using var effects = new RenderEffects { Bloom = quality, Layers = [new((c, _) => { using var p = new SKPaint { Color = SKColors.White }; c.DrawCircle(32, 32, 4, p); }, true)] };
        foreach (int size in new[] { 64, 96, 64 })
        {
            using var bitmap = new SKBitmap(size, size);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.Black);
            effects.DrawLayers(canvas, new(0, new(size, size), default));
            Assert.That(bitmap.GetPixel(42, 32).Red, quality == BloomQuality.Off ? Is.EqualTo(0) : Is.GreaterThan(0));
        }
    }
    [TestCase(BloomQuality.Low)]
    [TestCase(BloomQuality.Medium)]
    [TestCase(BloomQuality.High)]
    public void BloomLayersDrawOnceAndReuseTheirSurfaceWithShaderAppliedToGlow(BloomQuality quality)
    {
        using var shader = new RuntimeShader("uniform shader content; half4 main(float2 p) { half4 c=content.eval(p); return half4(0,c.r,0,c.a); }", SKColors.Red);
        var targets = new List<SKCanvas>();
        using var effects = new RenderEffects { Bloom = quality, Layers = [new((c, _) =>
        {
            targets.Add(c);
            using var paint = new SKPaint { Color = SKColors.Red };
            c.DrawCircle(32, 32, 4, paint);
        }, true, shader)] };
        foreach (int size in new[] { 64, 64, 96 })
        {
            using var bitmap = new SKBitmap(size, size);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.Black);
            effects.DrawLayers(canvas, new(0, new(size, size), default));
            Assert.That(bitmap.GetPixel(32, 32).Green, Is.GreaterThan(100));
            Assert.That(bitmap.GetPixel(42, 32).Green, Is.GreaterThan(0));
            Assert.That(bitmap.GetPixel(42, 32).Red, Is.EqualTo(0));
        }
        Assert.That(targets, Has.Count.EqualTo(3));
        Assert.That(targets[1], Is.SameAs(targets[0]));
        Assert.That(targets[2], Is.Not.SameAs(targets[0]));
    }
    [Test]
    public void EffectIntermediatesReuseTheirCanvasUntilDimensionsChange()
    {
        using var shader = new RuntimeShader("uniform shader content; half4 main(float2 p) { return content.eval(p); }", SKColors.Red);
        var targets = new List<SKCanvas>();
        using var effects = new RenderEffects { Layers = [new((c, _) => targets.Add(c), Shader: shader)] };
        using var bitmap = new SKBitmap(96, 96);
        using var canvas = new SKCanvas(bitmap);
        for (int i = 0; i < 3; i++) effects.DrawLayers(canvas, new(0, new(64, 64), default));
        Assert.That(targets[1], Is.SameAs(targets[0]));
        Assert.That(targets[2], Is.SameAs(targets[0]));
        effects.DrawLayers(canvas, new(0, new(96, 96), default));
        Assert.That(targets[3], Is.Not.SameAs(targets[0]));
    }
    [Test]
    public void SceneResolutionCapPreservesVirtualCoordinatesAndCanvasState()
    {
        using var shader = new RuntimeShader("uniform float2 resolution; half4 main(float2 p) { return half4(p.x/resolution.x,0,0,1); }", SKColors.Red);
        using var effects = new RenderEffects { Background = shader };
        var options = new RenderOptions { VirtualWidth = 64, VirtualHeight = 64, MaxRenderWidth = 32, Effects = effects };
        var measurements = new List<DrawMeasurement>();
        options.DrawMeasured = measurements.Add;
        using var renderer = new RenderSystem(options);
        foreach (int size in new[] { 64, 128, 16, 64 })
        {
            using var bitmap = new SKBitmap(size, size);
            using var canvas = new SKCanvas(bitmap);
            int saved = canvas.SaveCount;
            renderer.DrawEntitiesToCanvas(canvas, RenderSnapshot.Empty);
            Assert.That(bitmap.GetPixel(size / 2, size / 2).Red, Is.InRange(124, 140), $"Output size {size}");
            Assert.That(canvas.SaveCount, Is.EqualTo(saved));
            Assert.That(renderer.TryScreenToWorld(new(size / 2, size / 2), new(size, size), null, out var world), Is.True);
            Assert.That(world.X, Is.EqualTo(32));
        }
        using (var bitmap = new SKBitmap(128, 128))
        using (var canvas = new SKCanvas(bitmap))
        {
            int observed = 0;
            using var observedEffects = new RenderEffects { BackgroundLayers = [new((c, _) => observed = c.DeviceClipBounds.Width)] };
            using var scaledRenderer = new RenderSystem(new RenderOptions { VirtualWidth = 64, VirtualHeight = 64, MaxRenderWidth = 32, Effects = observedEffects });
            canvas.Scale(4);
            scaledRenderer.DrawEntitiesToCanvas(canvas, RenderSnapshot.Empty);
            Assert.That(observed, Is.EqualTo(32));
        }
        Assert.That(measurements, Has.Count.EqualTo(4));
        Assert.That(measurements.All(m => m.CpuSeconds >= 0 && !m.Accelerated), Is.True);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.MaxRenderWidth = -1);
    }
    [Test]
    public void FullScreenPostProcessGradesSceneAndFailureKeepsScene()
    {
        using var background = new RuntimeShader("half4 main(float2 p) { return half4(0, 1, 0, 1); }", SKColors.Blue);
        using var grade = new RuntimeShader("uniform shader content; half4 main(float2 p) { half4 c=content.eval(p); return half4(c.g,c.r,c.b,c.a); }", SKColors.Blue);
        using var failed = new RuntimeShader("invalid", SKColors.Blue);
        foreach (var shader in new[] { grade, failed })
        {
            using var effects = new RenderEffects { Background = background, PostProcess = shader };
            using var renderer = new RenderSystem(new RenderOptions { VirtualWidth = 32, VirtualHeight = 32, MaxRenderWidth = 16, Effects = effects });
            using var bitmap = new SKBitmap(32, 32);
            using var canvas = new SKCanvas(bitmap);
            renderer.DrawEntitiesToCanvas(canvas, RenderSnapshot.Empty);
            Assert.That(bitmap.GetPixel(16, 16), Is.EqualTo(shader == grade ? SKColors.Red : SKColors.Lime));
        }
    }
    [Test]
    public void ThrowingLayerRestoresHostCanvasAndCaptureRejectsFaults()
    {
        using var effects = new RenderEffects { Layers = [new((_, _) => throw new InvalidOperationException("layer"))] };
        using var renderer = new RenderSystem(new RenderOptions { Effects = effects });
        using var bitmap = new SKBitmap(32, 32);
        using var canvas = new SKCanvas(bitmap);
        int saved = canvas.SaveCount;
        Assert.Throws<InvalidOperationException>(() => renderer.DrawEntitiesToCanvas(canvas, RenderSnapshot.Empty));
        Assert.That(canvas.SaveCount, Is.EqualTo(saved));
        using var engine = new Engine(audioEnabled: false);
        engine.ChangeScene(new FaultScene());
        Assert.Throws<InvalidOperationException>(() => SceneCapture.Render(engine, 32, 32, .1));
    }
    [Test]
    public void CameraProjectsBrightBatchesButKeepsCustomLayersInScreenSpace()
    {
        using var texture = new SKBitmap(8, 8); texture.Erase(SKColors.White);
        using var image = SKImage.FromBitmap(texture);
        using var background = new RuntimeShader("half4 main(float2 p) { return half4(0,0,0,1); }", SKColors.Black);
        using var effects = new RenderEffects { Background = background, Bloom = BloomQuality.High, Layers = [new((canvas, _) => { using var p = new SKPaint { Color = SKColors.Red }; canvas.DrawCircle(8, 8, 3, p); })] };
        var entities = new EntityManager();
        entities.CreateEntity("camera").AddComponent(new CCamera { Position = new Vec2(200, 300), Zoom = 2 });
        entities.CreateSpriteBatch(image, 1, blend: SpriteBlend.Additive).Add(new(new Vec2(200, 300), new SKRect(0, 0, 8, 8)));
        entities.Update();
        var snapshot = new RenderSnapshot(); entities.BuildRenderSnapshot(snapshot);
        using var renderer = new RenderSystem(new RenderOptions { VirtualWidth = 64, VirtualHeight = 64, Effects = effects });
        using var bitmap = new SKBitmap(64, 64);
        using var canvas = new SKCanvas(bitmap);
        renderer.DrawEntitiesToCanvas(canvas, snapshot);
        Assert.That(bitmap.GetPixel(8, 8).Red, Is.GreaterThan(200));
        Assert.That(bitmap.GetPixel(45, 32).Green, Is.GreaterThan(0));
        renderer.AdvanceEffects(.1, new(new SKPoint(3, -2), 1.1f));
        Assert.That(renderer.TryScreenToWorld(new Vec2(35, 30), new Vec2(64, 64), snapshot.ActiveCamera, out var world), Is.True);
        Assert.That(world.X, Is.EqualTo(200).Within(1e-6));
        Assert.That(world.Y, Is.EqualTo(300).Within(1e-6));
    }
    [Test]
    public void CameraEffectsDecayAndHitStopUsesUnscaledTime()
    {
        var effects = new CameraEffects();
        effects.Shake(10, .5); effects.ZoomPunch(.1f, .5); effects.HitStop(.1, .25);
        Assert.That(effects.Advance(.04), Is.EqualTo(.01).Within(1e-9));
        Assert.That(effects.State.Zoom, Is.GreaterThan(1));
        Assert.That(effects.Advance(.1), Is.EqualTo(.055).Within(1e-9));
        effects.Advance(1);
        Assert.That(effects.State, Is.EqualTo(new CameraEffectState(default, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => effects.HitStop(.1, double.NaN));
    }
    [Test]
    public void CaptureRunsRealEngineDeterministicallyAndHitStopScalesScene()
    {
        byte[] Capture()
        {
            using var engine = new Engine(audioEnabled: false);
            engine.RenderOptions.DrawBoundingBoxes = true;
            var scene = new CaptureScene();
            engine.ChangeScene(scene);
            var png = SceneCapture.Render(engine, 96, 128, .5);
            Assert.That(scene.Time, Is.EqualTo(.5).Within(1e-9));
            engine.CameraEffects.HitStop(.1);
            engine.Tick(.05);
            Assert.That(scene.Time, Is.EqualTo(.5).Within(1e-9));
            engine.Tick(.1);
            Assert.That(scene.Time, Is.EqualTo(.55).Within(1e-9));
            return png;
        }
        var first = Capture();
        var second = Capture();
        Assert.That(first, Is.EqualTo(second));
        Assert.Throws<ArgumentOutOfRangeException>(() => SceneCapture.Render(0, 10, 0, (_, _) => { }));
    }
    [Test]
    public void LightningKeepsEndpointsAndSeedAndShatterUsesBoundedPool()
    {
        var points = EffectGeometry.Lightning(new(1, 2), new(80, 50), 42);
        Assert.That(points, Is.EqualTo(EffectGeometry.Lightning(new(1, 2), new(80, 50), 42)));
        Assert.That(points[0], Is.EqualTo(new SKPoint(1, 2)));
        Assert.That(points[^1], Is.EqualTo(new SKPoint(80, 50)));
        using var texture = SKImage.Create(new SKImageInfo(8, 8));
        using var particles = new ParticleSystem(5);
        var emitter = particles.CreateEmitter(new ParticleOptions { Texture = texture, Region = new(0, 0, 8, 8) });
        EffectGeometry.Shatter(emitter, new(32, 32), 12, 8, 42);
        Assert.That(particles.Store.Spawned, Is.EqualTo(8));
        Assert.That(particles.Store.Count, Is.EqualTo(5));
    }
    private sealed class FaultScene : Scene
    {
        public override void Initialize(EntityManager entityManager, InputManager inputManager, AudioSystem? audioPlayer, Action<Scene?> resetScene) => throw new InvalidOperationException("capture fault");
    }
    private sealed class CaptureScene : Scene
    {
        public double Time { get; private set; }
        private CTransform transform = null!;
        public override void Initialize(EntityManager entityManager, InputManager inputManager, AudioSystem? audioPlayer, Action<Scene?> resetScene)
        {
            var actor = entityManager.CreateEntity("capture");
            transform = new CTransform(new Vec2(200, 200));
            actor.AddComponent(transform);
            actor.AddComponent(new CBoundingBox(new Vec2(200, 200), false, false));
        }
        public override void Update(EntityManager entityManager, SystemContainer systems, double deltaSeconds) { Time += deltaSeconds; transform.Position = new Vec2(200 + Time * 100, 200); }
    }
}
