using System;
using System.IO;
using System.Text.Json;
using GameEngine.Core;
using GameEngine.Core.Components;
using GameEngine.Core.Systems;
using GameEngine.Demo.Emberbrook;

namespace GameEngine.Demo;

public sealed partial class SceneEmberbrook
{
    private bool previousDanger;
    private int previousGatherXp;
    private int previousCookingXp;
    private void PlayCue(string name)
    {
        if (muted || soundCooldown > 0) return;
        audio?.Play("Ember" + name, SoundType.SoundEffect);
        soundCooldown = 0.08;
    }

    private void AnimatePlayer(Cell before, double dt)
    {
        if (World.Player.X != before.X) facing = World.Player.X > before.X ? "right" : "left";
        else if (World.Player.Y != before.Y) facing = World.Player.Y > before.Y ? "down" : "up";
        else if (World.Target is { } target)
        {
            int x = target.Cell.X - World.Player.X, y = target.Cell.Y - World.Player.Y;
            facing = Math.Abs(x) > Math.Abs(y) ? x > 0 ? "right" : "left" : y > 0 ? "down" : "up";
        }
        string pose = World.Activity == "Walking" ? "walk" : World.BatchRecipe != null ?
            World.BatchRecipe is Recipe.GrilledTrout or Recipe.SmokedTrout ? "cook" : "mine"
            : World.Activity switch
            {
                "Mining" => "mine", "Woodcutting" => "chop", "Fishing" => "fish",
                "Fighting" => World.SpearEquipped ? "spear" : "attack", _ => "idle"
            };
        player.GetComponent<CAnimation>().Animation = Drawing("hero" + facing + pose, new Vec2(40, 40));
        if (World.DangerCentre != null && !previousDanger) PlayCue("Danger");
        previousDanger = World.DangerCentre != null;
        int gathered = World.MiningXp + World.FishingXp + World.WoodcuttingXp;
        if (gathered > previousGatherXp)
        {
            PlayCue("Gather");
            if (actionSite is { } site) Burst(site.Kind is SiteKind.Fishing or SiteKind.Jetty ? "splash" : "spark", Position(site.Cell));
        }
        if (World.CookingXp > previousCookingXp) PlayCue("Craft");
        previousGatherXp = gathered;
        previousCookingXp = World.CookingXp;
    }

    private void SaveMilestone()
    {
        try { saveStore.Write(World.Capture()); saveMessage = "Autosaved in village"; PlayCue("Complete"); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or NotSupportedException)
        { saveMessage = "SAVE FAILED / press S to retry"; World.Notify("Autosave failed: " + ex.Message); }
    }
}
