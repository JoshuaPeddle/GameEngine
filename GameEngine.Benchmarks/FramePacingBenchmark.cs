using System.Diagnostics;
using GameEngine.Core;
using GameEngine.Core.Components;
using GameEngine.Core.Effects;
using GameEngine.Core.Systems;
using SkiaSharp;

public static class FramePacingBenchmark
{
    public static void Run()
    {
        Console.WriteLine("scene,cpu_ms_per_wall_second,frames_per_second,ticks_per_second");
        foreach (var activity in Enum.GetValues<FrameActivity>())
        {
            using var scene = new Workload(activity);
            using var engine = Create(scene);
            using var surface = SKSurface.Create(new SKImageInfo(360, 640));
            int frames = 0;
            engine.InvalidateAction = () =>
            {
                engine.Systems.Get<RenderSystem>().DrawEntitiesToCanvas(surface.Canvas, engine.GetRenderSnapshot());
                Interlocked.Increment(ref frames);
                engine.NotifyFirstPresent();
            };
            engine.Start();
            Thread.Sleep(1500);
            using var process = Process.GetCurrentProcess();
            var cpu = process.TotalProcessorTime;
            int firstFrames = Volatile.Read(ref frames);
            int firstTicks = Volatile.Read(ref scene.Ticks);
            var wall = Stopwatch.StartNew();
            Thread.Sleep(3000);
            engine.Stop();
            double seconds = wall.Elapsed.TotalSeconds;
            Console.WriteLine(FormattableString.Invariant($"{activity},{(process.TotalProcessorTime - cpu).TotalMilliseconds / seconds:F3},{(frames - firstFrames) / seconds:F3},{(scene.Ticks - firstTicks) / seconds:F3}"));
        }
        Costs();
    }

    private static Engine Create(Workload scene)
    {
        var engine = new Engine(audioEnabled: false) { TargetFrameRate = 60, FixedTimeStep = 1.0 / 120 };
        engine.RenderOptions.DrawFps = false;
        engine.RenderOptions.Effects = scene.Effects;
        engine.ChangeScene(scene);
        return engine;
    }

    private static void Costs()
    {
        using var scene = new Workload(FrameActivity.Active);
        using var engine = Create(scene);
        engine.Tick(0);
        engine.NotifyFirstPresent();
        using var surface = SKSurface.Create(new SKImageInfo(360, 640));
        var renderer = engine.Systems.Get<RenderSystem>();
        var snapshot = new RenderSnapshot();
        Console.WriteLine("stage,mean_us,managed_bytes_per_operation");
        Measure("tick_including_snapshot", () => engine.Tick(1.0 / 120));
        Measure("snapshot", () => engine.EntityManager.BuildRenderSnapshot(snapshot));
        Measure("draw_including_acquire", () => renderer.DrawEntitiesToCanvas(surface.Canvas, engine.GetRenderSnapshot()));
        Measure("two_steps_and_draw", () =>
        {
            engine.Tick(1.0 / 120);
            engine.Tick(1.0 / 120);
            renderer.DrawEntitiesToCanvas(surface.Canvas, engine.GetRenderSnapshot());
        });
        engine.InvalidateAction = () => renderer.DrawEntitiesToCanvas(surface.Canvas, engine.GetRenderSnapshot());
        Measure("paced_frame", () => engine.Present(1.0 / 60));
    }

    private static void Measure(string name, Action action)
    {
        for (int i = 0; i < 200; i++) action();
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        long start = Stopwatch.GetTimestamp();
        for (int i = 0; i < 1000; i++) action();
        double microseconds = Stopwatch.GetElapsedTime(start).TotalMicroseconds / 1000;
        long bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
        Console.WriteLine(FormattableString.Invariant($"{name},{microseconds:F3},{bytes / 1000.0:F3}"));
    }

    private sealed class Workload(FrameActivity activity) : Scene, IDisposable
    {
        public override FrameActivity FrameActivity => activity;
        public override int VirtualWidth => 360;
        public override int VirtualHeight => 640;
        public int Ticks;
        private readonly SKBitmap bitmap = MakeBitmap();
        private readonly SKPaint paint = new() { Color = SKColors.Navy, IsAntialias = true };
        private RenderEffects? effects;
        public RenderEffects Effects => effects ??= new RenderEffects
        {
            FrameActivity = FrameActivity.Idle,
            CacheBackground = true,
            BackgroundLayers = [new((canvas, _) =>
            {
                for (int i = 0; i < 1000; i++)
                    canvas.DrawCircle(i * 17 % 360, i * 31 % 640, 12, paint);
            })]
        };

        private static SKBitmap MakeBitmap()
        {
            var bitmap = new SKBitmap(8, 8);
            bitmap.Erase(SKColors.Coral);
            bitmap.SetImmutable();
            return bitmap;
        }

        public override void Initialize(EntityManager entities, InputManager input, AudioSystem? audio, Action<Scene?> reset)
        {
            var animation = new Animation(bitmap, 1, 0);
            for (int i = 0; i < 500; i++)
            {
                var entity = entities.CreateEntity("sprite");
                entity.AddComponent(new CTransform(new Vec2(i * 19 % 360, i * 23 % 640)));
                entity.AddComponent(new CAnimation(animation));
            }
        }

        public override void Update(EntityManager entities, SystemContainer systems, double seconds)
        {
            Interlocked.Increment(ref Ticks);
            if (activity != FrameActivity.Active) return;
            var transforms = entities.GetEntitiesWithComponents<CTransform>();
            for (int i = 0; i < transforms.Count; i++)
            {
                var transform = transforms[i].Item2;
                transform.Position = new((transform.Position.X + seconds * 10) % 360, transform.Position.Y);
            }
        }

        public void Dispose() { effects?.Dispose(); bitmap.Dispose(); paint.Dispose(); }
    }
}
