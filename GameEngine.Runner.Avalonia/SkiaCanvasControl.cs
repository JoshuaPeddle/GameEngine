using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using GameEngine.Core;
using GameEngine.Core.Components;
using GameEngine.Core.Systems;
using SkiaSharp;

namespace GameEngine.Runner.Avalonia;

public class SkiaCanvasControl : Control
{
    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var entityManager = new EntityManager();

        Assets assets = new("assets.txt");

        var entity = entityManager.CreateEntity("Player");
        entity.AddComponent(new CTransform(new Vec2(100, 100)));
        entity.AddComponent(new CBoundingBox(new Vec2(50, 80), true, true));
        entity.AddComponent(new CAnimation(assets.GetAnimation("JeepBack")));

        var entity1 = entityManager.CreateEntity("Player");
        entity1.AddComponent(new CTransform(new Vec2(200, 200)));
        entity1.AddComponent(new CBoundingBox(new Vec2(60, 60), true, true));
        entity1.AddComponent(new CAnimation(assets.GetAnimation("StoneBlock")));


        var renderSystem = new RenderSystem(entityManager, new Core.Systems.RenderOptions()
        {
            DrawAnimations = true,
            DrawBoundingBoxes = true,
            DrawEntityCenters = true
        });

        var size = this.Bounds.Size;

        var info = new SKImageInfo((int)size.Width * 2, (int)size.Height * 2);

        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;
        renderSystem.DrawEntitiesToCanvas(canvas);
        using var image = surface.Snapshot();
        // Convert the SKImage to a bitmap
        using var data = image.Encode();
        using var stream = data.AsStream();
        var bitmap = new Bitmap(stream);

        var rect = new Rect(0, 0, size.Width, size.Height);
        context.DrawImage(bitmap, rect);
    }
}
