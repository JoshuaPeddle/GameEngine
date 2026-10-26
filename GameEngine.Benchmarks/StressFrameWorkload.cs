using GameEngine.Core;
using GameEngine.Core.Components;
using GameEngine.Core.Systems;
using SkiaSharp;

public sealed class StressFrameWorkload : IDisposable
{
    private readonly SKBitmap _bitmap = new(8, 8);
    private readonly SKImage _image;
    private readonly SKSurface _surface = SKSurface.Create(new SKImageInfo(360, 640));
    private readonly Engine _engine;
    private readonly SparkScene _scene;
    private readonly RenderSystem _renderer;

    public StressFrameWorkload(int count, bool transientEmitters)
    {
        _bitmap.Erase(SKColors.Orange);
        _bitmap.SetImmutable();
        _image = SKImage.FromBitmap(_bitmap);
        _engine = new Engine(audioEnabled: false, particleCapacity: 50000);
        _engine.RenderOptions.DrawFps = false;
        _scene = new SparkScene(_bitmap, _image, count, transientEmitters);
        _engine.ChangeScene(_scene);
        _engine.Tick(0);
        _engine.NotifyFirstPresent();
        _renderer = _engine.Systems.Get<RenderSystem>();
    }

    public void Update() => _engine.Tick(1.0 / 60);
    public RenderSnapshot AcquireSnapshot() => _engine.GetRenderSnapshot();
    public void Draw() => _renderer.DrawEntitiesToCanvas(_surface.Canvas, AcquireSnapshot());
    public void Frame() { Update(); Draw(); }
    public int Count => _engine.Systems.Get<ParticleSystem>().Store.Count;
    public long UpdateAllocationEnd => _scene.UpdateAllocationEnd;
    public long Spawned => _engine.Systems.Get<ParticleSystem>().Store.Spawned;
    public EngineFault? Fault => _engine.Fault;

    public void Dispose()
    {
        _engine.Dispose();
        _surface.Dispose();
        _image.Dispose();
        _bitmap.Dispose();
    }

    private sealed class SparkScene(SKBitmap bitmap, SKImage image, int count, bool transientEmitters) : Scene
    {
        private struct Spark
        {
            public Vec2 Position, Velocity;
            public float Age, Life;
        }

        private readonly Random _random = new(0x5EED);
        private readonly Spark[] _sparks = new Spark[count];
        private readonly ParticleOptions _options = new()
        {
            Texture = image, Region = new SKRect(0, 0, 8, 8), Layer = 2,
            EndScale = 0.2f, StartColor = SKColors.White, EndColor = SKColors.White.WithAlpha(0)
        };
        private ParticleSystem _particles = null!;
        private SpriteBatch _actors = null!;
        private double _time;
        public long UpdateAllocationEnd { get; private set; }

        public override int VirtualWidth => 720;
        public override int VirtualHeight => 1280;

        public override void Initialize(EntityManager entities, InputManager input, AudioSystem? audio, Action<Scene?> reset)
        {
            _particles = Engine!.Systems.Get<ParticleSystem>();
            var hidden = entities.CreateEntity("hidden");
            hidden.AddComponent(new CTransform(Vec2.Zero));
            hidden.AddComponent(new CMovement());
            hidden.AddComponent(new CAnimation(new Animation(bitmap, 1, 0)) { ShouldDraw = false });
            _actors = entities.CreateSpriteBatch(image, 1000, layer: 1);
            for (int i = 0; i < 1000; i++)
                _actors.Add(new SpriteInstance(new Vec2(360, 640), _options.Region));
            for (int i = 0; i < count; i++) Respawn(ref _sparks[i]);
        }

        private void Respawn(ref Spark spark)
        {
            float phase = _random.NextSingle() * MathF.Tau;
            float angle = _random.NextSingle() * MathF.Tau;
            float speed = 70 + _random.NextSingle() * 240;
            spark = new Spark
            {
                Position = new Vec2(360 + MathF.Sin(phase * 2) * 230, 640 + MathF.Cos(phase) * 430),
                Velocity = new Vec2(MathF.Cos(angle) * speed, MathF.Sin(angle) * speed),
                Life = 0.25f + _random.NextSingle() * 0.65f
            };
            if (transientEmitters)
            {
                var emitter = _particles.CreateEmitter(_options with
                {
                    Life = spark.Life, Direction = angle * 180 / MathF.PI,
                    Spread = 0, MinSpeed = speed, MaxSpeed = speed
                }, seed: 0x5EED);
                emitter.Position = spark.Position;
                emitter.Burst(1);
                _particles.RemoveEmitter(emitter);
            }
            else
            {
                _particles.Emit(_options, new ParticleSpawn(spark.Position, spark.Velocity, spark.Life));
            }
        }

        public override void Update(EntityManager entities, SystemContainer systems, double dt)
        {
            _time += dt;
            for (int i = 0; i < _actors.Count; i++)
            {
                double angle = _time * 0.24 + i * Math.Tau / 1000;
                _actors.Instances[i] = _actors.Instances[i] with
                {
                    Position = new Vec2(360 + Math.Sin(angle * 2) * 230, 640 + Math.Cos(angle) * 430),
                    Rotation = (float)(angle * 180 / Math.PI)
                };
            }
            for (int i = 0; i < count; i++)
            {
                _sparks[i].Age += (float)dt;
                if (_sparks[i].Age >= _sparks[i].Life) Respawn(ref _sparks[i]);
            }
            UpdateAllocationEnd = GC.GetAllocatedBytesForCurrentThread();
        }
    }
}

public static class StressAllocations
{
    public static void Run()
    {
        Console.WriteLine("particles,transient_emitters,update_B,snapshot_B,draw_B,frame_B");
        foreach (int count in new[] { 750, 3000, 8000 })
        foreach (bool transient in new[] { true, false })
        {
            using var workload = new StressFrameWorkload(count, transient);
            for (int i = 0; i < 1000; i++) workload.Frame();
            long update = 0, snapshot = 0, draw = 0;
            for (int i = 0; i < 600; i++)
            {
                long before = GC.GetAllocatedBytesForCurrentThread();
                workload.Update();
                long after = GC.GetAllocatedBytesForCurrentThread();
                workload.Draw();
                update += workload.UpdateAllocationEnd - before;
                snapshot += after - workload.UpdateAllocationEnd;
                draw += GC.GetAllocatedBytesForCurrentThread() - after;
            }
            if (workload.Fault != null || workload.Count != count)
                throw new InvalidOperationException($"Invalid workload: {workload.Fault}, count={workload.Count}");
            Console.WriteLine(FormattableString.Invariant($"{count},{transient},{update / 600.0:F2},{snapshot / 600.0:F2},{draw / 600.0:F2},{(update + snapshot + draw) / 600.0:F2}"));
        }
    }
}
