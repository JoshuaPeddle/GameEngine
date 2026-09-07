using System;
using System.Collections.Generic;
using GameEngine.Core;
using GameEngine.Core.Components;
using GameEngine.Demo.Emberbrook;

namespace GameEngine.Demo;

public sealed partial class SceneEmberbrook
{
    private sealed class Effect(Entity entity)
    {
        public Entity Entity { get; } = entity;
        public double Remaining { get; set; }
    }
    private readonly List<Effect> effects = [];
    private WorldSite? actionSite;
    private int nextEffect;
    private Region effectRegion;

    private void InitializeEffects()
    {
        for (int i = 0; i < 8; i++)
        {
            var sprite = Sprite("emberEffect" + i, "spark", Vec2.Zero, new Vec2(40, 40), 40);
            sprite.GetComponent<CAnimation>().ShouldDraw = false;
            effects.Add(new Effect(sprite));
        }
    }

    private void Burst(string image, Vec2 position)
    {
        var effect = effects[nextEffect];
        nextEffect = (nextEffect + 1) % effects.Count;
        effect.Remaining = 0.32;
        effect.Entity.GetComponent<CTransform>().Position = position;
        var animation = effect.Entity.GetComponent<CAnimation>();
        animation.Animation = assets.GetAnimation("Emberpanel");
        animation.Animation = Drawing(image, new Vec2(40, 40));
        animation.ShouldDraw = true;
    }

    private void UpdateEffects(double dt)
    {
        foreach (var effect in effects)
        {
            effect.Remaining = effectRegion != World.Region ? 0 : Math.Max(0, effect.Remaining - dt);
            effect.Entity.GetComponent<CAnimation>().ShouldDraw = effect.Remaining > 0;
        }
        effectRegion = World.Region;
    }
}
