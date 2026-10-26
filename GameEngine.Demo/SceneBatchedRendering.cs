using System;
using GameEngine.Core;
using GameEngine.Core.Components;
using GameEngine.Core.Systems;
using SkiaSharp;

namespace GameEngine.Demo;

public sealed class SceneBatchedRendering : Scene
{
    private static readonly Lazy<SKImage> Atlas = new(CreateAtlas);
    private SpriteBatch _swarm = null!;
    private ParticleEmitter _sparks = null!;
    private CText _status = null!;
    private double _time;

    public override int VirtualWidth => 960;
    public override int VirtualHeight => 640;

    private static SKImage CreateAtlas()
    {
        using var bitmap = new SKBitmap(32, 16);
        using var canvas = new SKCanvas(bitmap);
        using var paint = new SKPaint { IsAntialias = true, Color = SKColors.White };
        canvas.Clear(SKColors.Transparent);
        canvas.DrawCircle(8, 8, 7, paint);
        canvas.DrawRect(new SKRect(19, 5, 29, 11), paint);
        bitmap.SetImmutable();
        return SKImage.FromBitmap(bitmap);
    }

    public override void Initialize(EntityManager entityManager, InputManager inputManager,
        AudioSystem? audioPlayer, Action<Scene?> resetScene)
    {
        _time = 0;
        _swarm = entityManager.CreateSpriteBatch(Atlas.Value, 500);
        for (int i = 0; i < 500; i++)
            _swarm.Add(new SpriteInstance(new Vec2(480, 320), new SKRect(0, 0, 16, 16), 0.6f,
                Color: new SKColor(20, 90, 130)));
        _sparks = Engine!.Systems.Get<ParticleSystem>().CreateEmitter(new ParticleOptions
        {
            Texture = Atlas.Value, Region = new SKRect(16, 0, 32, 16), Blend = SpriteBlend.Additive,
            Layer = 1, Shape = ParticleShape.Radius, Radius = 12, Spread = 360,
            MinSpeed = 40, MaxSpeed = 180, Life = 1.2f, Gravity = new Vec2(0, 80), Drag = 0.4f,
            StartColor = new SKColor(255, 100, 10), EndColor = new SKColor(255, 100, 10, 0),
            StartScale = 1, EndScale = 0, AngularVelocity = 120
        }, 42);
        _sparks.Position = new Vec2(480, 320);
        _sparks.Rate = 1500;
        _sparks.Burst(200);
        var background = entityManager.CreateSpriteBatch(Atlas.Value, 1, -10);
        background.Add(new SpriteInstance(new Vec2(480, 320), new SKRect(22, 7, 26, 9), 480,
            Color: new SKColor(6, 12, 22)));
        var title = entityManager.CreateEntity("batch-title");
        title.AddComponent(new CTransform(new Vec2(480, 40)) { Layer = 10 });
        _status = new CText("Batched swarm and additive sparks", 22);
        _status.Paint.Color = SKColors.White;
        title.AddComponent(_status);
        var help = entityManager.CreateEntity("batch-help");
        help.AddComponent(new CTransform(new Vec2(480, 610)) { Layer = 10 });
        var text = new CText("Space: burst    Q: menu", 18);
        text.Paint.Color = SKColors.White;
        help.AddComponent(text);
        inputManager.AddAction(GeKeys.Space, "batch-burst");
        inputManager.BindAction("batch-burst", active => { if (active) _sparks.Burst(100); });
        inputManager.AddAction(GeKeys.Q, "batch-menu");
        inputManager.BindAction("batch-menu", active => { if (active) resetScene(new SceneMenu()); });
    }

    public override void Update(EntityManager entityManager, SystemContainer systems, double deltaSeconds)
    {
        _time += deltaSeconds;
        var instances = _swarm.Instances;
        for (int i = 0; i < instances.Length; i++)
        {
            double angle = _time * 0.5 + i * Math.Tau / instances.Length;
            instances[i] = instances[i] with
            {
                Position = new Vec2(480 + Math.Sin(angle * 2) * 340, 320 + Math.Cos(angle) * 200),
                Rotation = (float)(angle * 180 / Math.PI)
            };
        }
        _sparks.Position = new Vec2(480 + Math.Sin(_time) * 300, 320 + Math.Cos(_time * 1.7) * 150);
    }
}
