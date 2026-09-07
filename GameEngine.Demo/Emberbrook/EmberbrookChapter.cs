using System;
using System.Collections.Generic;
using System.Linq;

namespace GameEngine.Demo.Emberbrook;

public enum Recipe { Bar, Sword, Shield, Tool, Spear, GrilledTrout, SmokedTrout }
public sealed record RecipeInfo(Recipe Id, string Name, string Cost, SiteKind Station);

public sealed partial class EmberbrookWorld
{
    public static readonly RecipeInfo[] Recipes =
    [
        new(Recipe.Bar, "Copper bar", "3 ore", SiteKind.Forge),
        new(Recipe.Sword, "Copper sword", "2 bars", SiteKind.Forge),
        new(Recipe.Shield, "Copper shield", "2 bars + 20 coins", SiteKind.Forge),
        new(Recipe.Tool, "Reinforced tools", "2 bars + 30 coins", SiteKind.Forge),
        new(Recipe.Spear, "Ash spear", "4 logs + 2 bars", SiteKind.Forge),
        new(Recipe.GrilledTrout, "Grilled trout", "1 raw trout", SiteKind.Campfire),
        new(Recipe.SmokedTrout, "Smoked trout", "1 raw trout + 1 log", SiteKind.Campfire)
    ];
    public event Action? Crafted;
    public int BridgeStage { get; private set; }
    public bool HasTool { get; private set; }
    public bool HasSpear { get; private set; }
    public bool SpearEquipped { get; private set; }
    public bool TrackBridge { get; private set; }
    public int BatchRemaining { get; private set; }
    public Recipe? BatchRecipe { get; private set; }
    private double batchClock;
    private Recipe? pendingRecipe;
    private int pendingQuantity;
    public int CombatBonus => Math.Min(4, (CombatLevel - 1) / 2);
    public string BridgeQuest => BridgeStage switch
    {
        0 => QuestStage < 2 ? "Finish Copper Promise to help Bram." : "Ask Bram about the broken crossing.",
        1 => "Bring Bram 4 logs + 1 bar, or pay 45 coins. Repair the northern brook crossing.",
        _ => "Crossing repaired! Northern bridge open. Smoke trout at the cooking fire."
    };
    public static int StackSize(Item item) => item is Item.Ore or Item.Log or Item.Trout ? 5 : item == Item.SmokedTrout ? 2 : 1;
    public static int Slots(Item item, int count) => (count + StackSize(item) - 1) / StackSize(item);
    public bool CanCarry(Item item, int amount = 1) => PackUsed - Slots(item, Count(item)) + Slots(item, Count(item) + amount) <= PackCapacity;
    public int AttackRange => SpearEquipped ? 2 : 1;

    public void TalkToBram()
    {
        if (!Beside(SiteKind.Merchant)) { Message = "Meet Bram beside the village supply chest."; return; }
        if (QuestStage < 2) Message = "Bram: The northern crossing washed away. Help Elin first; then we can reopen it.";
        else if (BridgeStage == 0)
        {
            BridgeStage = 1;
            Message = "Bram: The broken crossing cut off my visits to my sister. Bring 4 ash logs and a copper bar, or pay 45 coins for supplies.";
        }
        else Message = BridgeStage == 1 ? "Bram: The southern gate now leads to safe ash trees. The deep forest is still sealed. Choose materials or coins here."
            : "Bram: I crossed over to see my sister this morning. Take my smoked trout recipe for the road!";
    }

    public void RepairBridge(bool useCoins)
    {
        if (!Beside(SiteKind.Merchant)) { Message = "Return to Bram to arrange the bridge repair."; return; }
        if (BridgeStage != 1) { Message = BridgeStage == 2 ? "The crossing is already repaired." : "Talk to Bram about the crossing first."; return; }
        if (useCoins ? Coins < 45 : Count(Item.Log) < 4 || Count(Item.Bar) < 1)
        { Message = useCoins ? "Repair needs 45 coins." : "Repair needs all 4 ash logs and 1 copper bar."; return; }
        Stop();
        if (useCoins) Coins -= 45;
        else { Take(Item.Log, 4); Take(Item.Bar, 1); }
        BridgeStage = 2;
        Message = "Crossing repaired! The northern bridge is open. Bram teaches smoked trout: 8 healing, two portions per slot.";
    }

    public void ToggleTrackedQuest() { TrackBridge = !TrackBridge; }

    public void EquipSpear()
    {
        if (!HasSpear) { Message = "Forge an ash spear from 4 logs and 2 bars."; return; }
        if (Activity != "Idle" || DangerCentre != null || ActiveSites.Any(s => s.IsEnemy && s.Hull > 0 && s.Cell.Distance(Player) <= 6))
        { Message = "Change weapons while idle and away from enemies."; return; }
        SpearEquipped = !SpearEquipped;
        Message = SpearEquipped ? "Ash spear equipped: two-tile reach; your shield is stowed." : "Sword equipped; shield protection restored.";
    }

    private bool InAttackRange(Cell from, WorldSite enemy)
    {
        int distance = from.Distance(enemy.Cell);
        if (distance == 1) return true;
        if (AttackRange != 2 || distance != 2 || from.X != enemy.Cell.X && from.Y != enemy.Cell.Y) return false;
        return IsWalkable(new Cell((from.X + enemy.Cell.X) / 2, (from.Y + enemy.Cell.Y) / 2));
    }

    public void UseWorkstation(Cell cell, Recipe recipe, int quantity)
    {
        var info = Recipes.Single(r => r.Id == recipe);
        var station = ActiveSites.FirstOrDefault(s => s.Cell == cell && s.Kind == info.Station);
        if (station == null || !Select(cell)) return;
        if (Beside(info.Station)) { StartBatch(recipe, quantity); return; }
        pendingRecipe = recipe;
        pendingQuantity = quantity;
        Message = $"{info.Name} selected. Walking into range to craft. X cancels.";
    }

    public void StartBatch(Recipe recipe, int quantity)
    {
        var info = Recipes.Single(r => r.Id == recipe);
        if (!Beside(info.Station)) { Message = "Walk beside the " + (info.Station == SiteKind.Forge ? "forge." : "cooking fire."); return; }
        Stop();
        BatchRecipe = recipe;
        BatchRemaining = recipe is Recipe.Bar or Recipe.GrilledTrout or Recipe.SmokedTrout ? Math.Clamp(quantity, 1, 10) : 1;
        Message = $"Making {info.Name}: {BatchRemaining} queued. X cancels; ingredients are used per item.";
    }

    private void UpdateBatch(double dt)
    {
        if (BatchRecipe is not { } recipe) return;
        batchClock += dt;
        if (batchClock < 0.9) return;
        batchClock = 0;
        int remaining = BatchRemaining;
        if (!Craft(recipe)) { Stop(); return; }
        BatchRemaining = remaining - 1;
        if (BatchRemaining == 0) { BatchRecipe = null; return; }
        BatchRecipe = recipe;
    }

    public bool Craft(Recipe recipe)
    {
        var info = Recipes.Single(r => r.Id == recipe);
        if (!Beside(info.Station)) { Message = "Use this recipe beside its workstation."; return false; }
        int ore = recipe == Recipe.Bar ? 3 : 0;
        int bars = recipe is Recipe.Sword or Recipe.Shield or Recipe.Tool or Recipe.Spear ? 2 : 0;
        int logs = recipe == Recipe.Spear ? 4 : recipe == Recipe.SmokedTrout ? 1 : 0;
        int trout = recipe is Recipe.GrilledTrout or Recipe.SmokedTrout ? 1 : 0;
        int coins = recipe == Recipe.Shield ? 20 : recipe == Recipe.Tool ? 30 : 0;
        if (recipe == Recipe.SmokedTrout && BridgeStage < 2) { Message = "Repair Bram's crossing to learn smoked trout."; return false; }
        if (recipe == Recipe.Shield && !HasSword) { Message = "Forge a sword before a shield."; return false; }
        if (recipe == Recipe.Sword && HasSword || recipe == Recipe.Shield && HasShield || recipe == Recipe.Tool && HasTool || recipe == Recipe.Spear && HasSpear)
        { Message = "You already own this equipment."; return false; }
        if (Count(Item.Ore) < ore || Count(Item.Bar) < bars || Count(Item.Log) < logs || Count(Item.Trout) < trout || Coins < coins)
        { Message = "Missing supplies: " + info.Cost + "."; return false; }
        Item? output = recipe == Recipe.Bar ? Item.Bar : recipe == Recipe.GrilledTrout ? Item.Meal : recipe == Recipe.SmokedTrout ? Item.SmokedTrout : null;
        int slots = PackUsed;
        foreach (var pair in new[] { (Item.Ore, ore), (Item.Bar, bars), (Item.Log, logs), (Item.Trout, trout) })
            slots += Slots(pair.Item1, Count(pair.Item1) - pair.Item2) - Slots(pair.Item1, Count(pair.Item1));
        if (output is { } result) slots += Slots(result, Count(result) + 1) - Slots(result, Count(result));
        if (slots > PackCapacity) { Message = "Make room for the finished item before crafting."; return false; }
        Take(Item.Ore, ore); Take(Item.Bar, bars); Take(Item.Log, logs); Take(Item.Trout, trout);
        Coins -= coins;
        if (output is { } item) Give(item);
        if (recipe == Recipe.Sword) HasSword = true;
        if (recipe == Recipe.Shield) HasShield = true;
        if (recipe == Recipe.Tool) HasTool = true;
        if (recipe == Recipe.Spear) HasSpear = true;
        if (trout > 0) CookingXp += 8;
        Message = "Made " + info.Name + (trout > 0 ? " / +8 Cooking XP." : ".");
        Crafted?.Invoke();
        return true;
    }

    public void BuyRation(int quantity = 1)
    {
        if (!Beside(SiteKind.Merchant)) { Message = "Visit Bram to buy food."; return; }
        int bought = 0;
        for (int i = 0; i < Math.Clamp(quantity, 1, 10) && Coins >= 3 && CanCarry(Item.Ration); i++)
        { Give(Item.Ration); Coins -= 3; bought++; }
        Message = bought == 0 ? "Rations cost 3 coins each and need a free slot." : $"Bought {bought} rations / {bought * 3} coins.";
    }

    public void SellItem(Item item, int quantity)
    {
        if (!Beside(SiteKind.Merchant)) { Message = "Visit Bram to sell supplies."; return; }
        int reserve = Reserved(item);
        int amount = Math.Min(Math.Max(0, Count(item) - reserve), Math.Clamp(quantity, 1, 60));
        int price = SalePrice(item);
        if (price == 0 || amount == 0) { Message = "Nothing to sell. Active quest supplies are reserved."; return; }
        Take(item, amount); Coins += amount * price;
        Message = $"Sold {amount} {ItemName(item)} / +{amount * price} coins. Quest supplies reserved.";
    }
    public static int SalePrice(Item item) => item switch { Item.Ore or Item.Log => 1, Item.Bar => 3, Item.Trout => 2, Item.Meal => 5, Item.SmokedTrout => 4, _ => 0 };
}
