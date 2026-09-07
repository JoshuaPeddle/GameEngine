using System;
using System.Collections.Generic;
using GameEngine.Core;
using GameEngine.Core.Components;
using GameEngine.Demo.Emberbrook;
using SkiaSharp;

namespace GameEngine.Demo;

public sealed partial class SceneEmberbrook
{
    private sealed class FloatingText(Entity entity, CText text)
    {
        public Entity Entity { get; } = entity;
        public CText Text { get; } = text;
        public double Remaining { get; set; }
    }

    private readonly List<FloatingText> floatingText = [];
    private readonly Dictionary<string, int> previousEnemyHealth = [];
    private Entity healthFill = null!;
    private Entity actionFill = null!;
    private int previousHealth;
    private int previousLevels;
    private Region previousRegion;
    private int nextFloatingText;

    private int TotalLevels => World.MiningLevel + World.CombatLevel + World.FishingLevel + World.CookingLevel + World.WoodcuttingLevel;

    private void InitializeFeedback()
    {
        Sprite("emberHealthTrack", "panel", new Vec2(1128, 219), new Vec2(224, 6), 50);
        healthFill = Sprite("emberHealthFill", "health", new Vec2(1128, 219), new Vec2(224, 6), 51);
        Sprite("emberActionTrack", "paper", new Vec2(504, 718), new Vec2(960, 4), 50);
        actionFill = Sprite("emberActionFill", "progress", new Vec2(504, 718), new Vec2(960, 4), 51);
        for (int i = 0; i < 8; i++)
        {
            string tag = "emberFloat" + i;
            var entity = entities.CreateEntity(tag);
            entity.AddComponent(new CTransform(Vec2.Zero) { Layer = 110 });
            var text = new CText("", 17) { TextAlign = SKTextAlign.Center };
            entity.AddComponent(text);
            floatingText.Add(new FloatingText(entity, text));
        }
        ResetFeedback();
        Fill(actionFill, 0, 24, 960, 718);
    }

    private void ResetFeedback()
    {
        previousHealth = World.Hull;
        previousGatherXp = World.MiningXp + World.FishingXp + World.WoodcuttingXp;
        previousCookingXp = World.CookingXp; previousDanger = World.DangerCentre != null;
        previousLevels = TotalLevels;
        previousRegion = World.Region;
        foreach (var site in World.Sites) previousEnemyHealth[site.Id] = site.Hull;
        foreach (var popup in floatingText) { popup.Remaining = 0; popup.Text.ShouldDraw = false; }
    }

    private void UpdateFeedback(double dt)
    {
        foreach (var popup in floatingText)
        {
            popup.Remaining = Math.Max(0, popup.Remaining - dt);
            popup.Text.ShouldDraw = popup.Remaining > 0;
            if (popup.Remaining > 0)
                popup.Entity.GetComponent<CTransform>().Position += new Vec2(0, -18 * dt);
        }
        if (previousRegion != World.Region) ResetFeedback();
        if (previousHealth != World.Hull)
        {
            int change = World.Hull - previousHealth;
            PlayCue(change > 0 ? "Craft" : "Damage");
            if (change < 0) Burst("strike", Position(World.Player));
            Float(change > 0 ? "+" + change : change.ToString(), Position(World.Player), change > 0 ? "#b8f5ad" : "#ffb0a3");
        }
        if (TotalLevels > previousLevels) PlayCue("Complete");
        if (TotalLevels > previousLevels) Float("Level up!", Position(World.Player) + new Vec2(0, -22), "#ffe39a");
        foreach (var site in World.ActiveSites)
        {
            if (site.IsEnemy && previousEnemyHealth.TryGetValue(site.Id, out int old) && site.Hull < old)
            {
                Float("−" + (old - Math.Max(0, site.Hull)), Position(site.Cell), "#fff0c3");
                PlayCue(site.Hull <= 0 ? "Complete" : "Damage");
                Burst("strike", Position(site.Cell));
                if (site.Hull <= 0 && site.Kind is SiteKind.Hart or SiteKind.Guardian)
                    Float(site.Name + " defeated!", Position(site.Cell) + new Vec2(0, -24), "#ffe39a");
            }
            previousEnemyHealth[site.Id] = site.Hull;
        }
        previousHealth = World.Hull;
        previousLevels = TotalLevels;
        Fill(healthFill, World.Hull / 20.0, 1016, 224, 219);
        healthFill.GetComponent<CAnimation>().Animation = assets.GetAnimation(World.Hull <= 6 ? "Emberwound" : "Emberhealth", new Vec2(224, 6));
        Fill(actionFill, World.ActionProgress, 24, 960, 718);
    }

    private void Float(string value, Vec2 position, string color)
    {
        var popup = floatingText[nextFloatingText++ % floatingText.Count];
        popup.Remaining = 1.1;
        popup.Text.Text = value;
        popup.Text.Paint.Color = SKColor.Parse(color);
        popup.Text.ShouldDraw = true;
        popup.Entity.GetComponent<CTransform>().Position = position + new Vec2(0, -36);
    }

    private static void Fill(Entity entity, double fraction, double left, double width, double y)
    {
        entity.GetComponent<CAnimation>().ShouldDraw = fraction > 0;
        var transform = entity.GetComponent<CTransform>();
        transform.Scale = new Vec2(fraction, 1);
        transform.Position = new Vec2(left + width * fraction / 2, y);
    }
}
