using System;
using System.Collections.Generic;
using System.Linq;

namespace GameEngine.Demo.Emberbrook;

public sealed partial class EmberbrookWorld
{
    private readonly Dictionary<Item, int> bank = [];
    public int BankCount(Item item) => item == Item.Ore ? BankedOre : bank.GetValueOrDefault(item);
    public int BankTotal => BankedOre + bank.Values.Sum();
    public bool Beside(SiteKind kind) => ActiveSites.Any(s => s.Kind == kind && s.Cell.Distance(Player) == 1);

    public void Deposit(Item item, int quantity = 60)
    {
        if (!Beside(SiteKind.Bank)) { Message = "Walk to the supply chest to deposit items."; return; }
        int count = Math.Min(Count(item), Math.Max(0, quantity));
        Store(item, count);
        Take(item, count);
        Message = $"Banked {count} {ItemName(item)}. Stored: {BankCount(item)}.";
    }

    public void Withdraw(Item item, int quantity = 60)
    {
        if (!Beside(SiteKind.Bank)) { Message = "Walk to the supply chest to withdraw items."; return; }
        int count = Math.Min(BankCount(item), Math.Clamp(quantity, 0, 60));
        while (count > 0 && !CanCarry(item, count)) count--;
        Store(item, -count);
        if (count > 0) inventory[item] = Count(item) + count;
        Message = $"Withdrew {count} {ItemName(item)}. {BankCount(item)} remain in the chest.";
    }

    public void DepositAll()
    {
        if (!Beside(SiteKind.Bank)) { Message = "Walk to the supply chest to deposit items."; return; }
        int count = PackUsed;
        foreach (var item in Enum.GetValues<Item>())
        {
            Store(item, Count(item));
            inventory.Remove(item);
        }
        Message = $"Banked {count} items. Choose an item in the sidebar to withdraw it.";
    }

    private void Store(Item item, int amount)
    {
        if (item == Item.Ore) { BankedOre += amount; return; }
        int count = BankCount(item) + amount;
        if (count > 0) bank[item] = count;
        else bank.Remove(item);
    }

    public static string ItemName(Item item) => item switch
    {
        Item.Ore => "ore", Item.Bar => "bars", Item.Ration => "rations", Item.Trout => "raw trout",
        Item.SmokedTrout => "smoked trout", Item.Meal => "grilled trout", Item.Log => "ash logs", _ => throw new ArgumentOutOfRangeException(nameof(item))
    };
}
