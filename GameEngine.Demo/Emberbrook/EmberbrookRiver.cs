using System;

namespace GameEngine.Demo.Emberbrook;

public enum RiverCharm { None, Might, Shelter }

public sealed partial class EmberbrookWorld
{
    public int FishingXp { get; private set; }
    public int CookingXp { get; private set; }
    public int FishingLevel => 1 + FishingXp / 30;
    public int CookingLevel => 1 + CookingXp / 24;
    public int RiverQuestStage { get; private set; }
    public bool HasPearl { get; private set; }
    public RiverCharm Charm { get; private set; }
    public int MealHealing => 12 + Math.Min(4, (CookingLevel - 1) * 2);
    public int AttackDamage => (SpearEquipped ? 5 : HasSword ? 5 : 2) + CombatBonus + (Charm == RiverCharm.Might ? 2 : 0);
    public int Protection => (HasShield && !SpearEquipped ? 2 : 0) + (Charm == RiverCharm.Shelter ? 1 : 0);
    private string RiverQuest => RiverQuestStage switch
    {
        0 => "Sunken Bell complete. Speak to Elin for River's Bounty.",
        1 => $"Bring Elin 3 grilled trout ({Math.Min(3, Count(Item.Meal))}/3) and a river pearl ({(HasPearl ? "found" : "catch 5 fish")}).",
        _ => TrailQuest
    };

    private void InteractRiverQuest()
    {
        if (RiverQuestStage == 0)
        {
            RiverQuestStage = 1;
            Message = "Elin: Bring 3 grilled trout and a river pearl for the village feast. Fish in the brook; cook at the western fire.";
        }
        else if (RiverQuestStage == 1 && Count(Item.Meal) >= 3 && HasPearl)
        {
            Take(Item.Meal, 3);
            HasPearl = false;
            RiverQuestStage = 2;
            Charm = RiverCharm.Might;
            Coins += 60;
            Message = "River's Bounty complete! +60 coins and a River Charm. T chooses Might (+2 attack) or Shelter (+1 protection).";
        }
        else Message = RiverQuestStage == 2 ? "Elin: The village feast was wonderful. May your river charm serve you well."
            : "Elin: Cook 3 trout at the western campfire. Your fifth successful catch reveals a pearl; it uses no pack space.";
    }

    private void Fish(WorldSite site)
    {
        int yield = site.Kind == SiteKind.Jetty ? 2 : 1;
        if (!CanCarry(Item.Trout, yield)) { Message = "Your pack is full. Cook your trout, eat a meal, or sell fish to Bram [B]."; Stop(); return; }
        for (int i = 0; i < yield; i++) Give(Item.Trout);
        int level = FishingLevel;
        FishingXp += yield * 10;
        site.RespawnSeconds = site.Kind == SiteKind.Jetty ? 5 : 3;
        bool pearlFound = !HasPearl && FishingXp >= 50 && RiverQuestStage < 2;
        if (pearlFound) HasPearl = true;
        Message = pearlFound ? $"You found a river pearl! Keep it for Elin's feast. +{yield} raw trout / +{yield * 10} Fishing XP."
            : $"Caught {yield} trout! +{yield * 10} Fishing XP.{(FishingLevel > level ? $" Fishing level {FishingLevel}!" : "")}";
        if (!RepeatGathering) Stop();
    }

    public void ToggleCharm()
    {
        if (RiverQuestStage != 2) { Message = "Complete River's Bounty to earn a River Charm."; return; }
        if (target != null || route.Count > 0 || DangerCentre != null)
        { Message = "Finish your current activity before changing your charm."; return; }
        Charm = Charm == RiverCharm.Might ? RiverCharm.Shelter : RiverCharm.Might;
        Message = Charm == RiverCharm.Might ? "River Charm / Might: +2 attack damage."
            : "River Charm / Shelter: incoming hits deal 1 less damage (minimum 1).";
    }

    public void SellFish()
    {
        var merchant = System.Linq.Enumerable.Single(Sites, s => s.Kind == SiteKind.Merchant);
        if (Region != Region.Village || Player.Distance(merchant.Cell) != 1)
        { Message = "Walk beside Bram to sell fish [B]. Click him to buy a ration."; return; }
        int raw = Count(Item.Trout), cooked = Math.Max(0, Count(Item.Meal) - Reserved(Item.Meal));
        if (raw + cooked == 0) { Message = $"Bram buys raw trout for 2 coins and grilled trout for 5. {Reserved(Item.Meal)} meals reserved for accepted work."; return; }
        int value = raw * 2 + cooked * 5;
        inventory.Remove(Item.Trout);
        if (cooked > 0) Take(Item.Meal, cooked);
        Coins += value;
        Message = $"Sold {raw} raw and {cooked} grilled trout for {value} coins. {Reserved(Item.Meal)} meals reserved for accepted work.";
    }
}
