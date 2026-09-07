using System;
using System.Collections.Generic;
using System.Linq;
using GameEngine.Core;
using GameEngine.Core.Components;
using GameEngine.Demo.Emberbrook;
using SkiaSharp;

namespace GameEngine.Demo;

public sealed partial class SceneEmberbrook
{
    private enum SidebarPage { Backpack, Skills, Journal }
    private sealed record PanelButton(SKRect Bounds, Action Action, Func<bool> Visible, Entity Sprite, CText Text, Func<string> Caption);
    private readonly List<PanelButton> panelButtons = [];
    private readonly List<(Entity Icon, CText Quantity, Item? Item)> slots = [];
    private readonly List<Entity> welcomeEntities = [];
    private TextBlock detail = null!;
    private TextBlock pageText = null!;
    private CText serviceTitle = null!;
    private CText saveStatus = null!;
    private SidebarPage page;
    private Item selectedItem;
    private int forgeRecipeIndex;
    private int cookingRecipeIndex;
    private int quantity = 1;
    private bool welcome;
    private bool muted;
    private string saveMessage = "Not saved yet";
    private string savedMilestone = "";
    private GameEngine.Core.Systems.AudioSystem? audio;
    private double soundCooldown;
    private string facing = "down";
    private int renderedBridge;
    private int renderedTrail;
    private int renderedRuin;
    private int renderedFeast;

    private bool AtElder => World.Beside(SiteKind.Elder);
    private bool AtBank => World.Beside(SiteKind.Bank);
    private bool AtForge => World.Beside(SiteKind.Forge);
    private bool AtFire => World.Beside(SiteKind.Campfire);
    private bool AtShop => World.Beside(SiteKind.Merchant);
    private RecipeInfo SelectedRecipe => RecipeAt(AtFire ? SiteKind.Campfire : SiteKind.Forge);
    private RecipeInfo RecipeAt(SiteKind station)
    {
        var options = EmberbrookWorld.Recipes.Where(r => r.Station == station).ToArray();
        return options[(station == SiteKind.Campfire ? cookingRecipeIndex : forgeRecipeIndex) % options.Length];
    }
    private string CraftCaption => SelectedRecipe.Id switch
    {
        Recipe.Bar => $"Smelt {quantity} bar{(quantity == 1 ? "" : "s")}",
        Recipe.GrilledTrout => $"Cook {quantity} trout",
        Recipe.SmokedTrout => $"Smoke {quantity} trout",
        Recipe.Sword => "Forge sword", Recipe.Shield => "Forge shield",
        Recipe.Tool => "Forge tools", _ => "Forge spear"
    };

    private void SelectRecipe(int direction)
    {
        page = SidebarPage.Backpack;
        if (AtFire) cookingRecipeIndex = (cookingRecipeIndex + direction + 2) % 2;
        else forgeRecipeIndex = (forgeRecipeIndex + direction + 5) % 5;
    }

    private static string QuestStatus(int stage, int complete, bool unlocked) => stage == complete ? "[Done]" : stage > 0 ? "[Active]" : unlocked ? "[Ready]" : "[Locked]";
    private string Milestone => $"{World.QuestStage}:{World.RuinQuestStage}:{World.RiverQuestStage}:{World.TrailQuestStage}:{World.BridgeStage}:{World.VillageProgress}";

    private void PanelButtonAt(string tag, Func<string> caption, int x, int y, int width, Action action, Func<bool>? visible = null)
    {
        var sprite = Sprite(tag, "button", new Vec2(x + width / 2.0, y + 14), new Vec2(width, 28), 120);
        var text = Label(tag + "Text", "", new Vec2(x + width / 2.0, y + 19), 12, "#eee0b3", SKTextAlign.Center);
        panelButtons.Add(new(new SKRect(x, y, x + width, y + 28), action, () => VillageButtonVisible(tag, y, visible ?? (() => !welcome)), sprite, text, caption));
    }

    private void InitializePanels()
    {
        stats = Lines("emberStats", 2, 17, new Vec2(1016, 163), 14, "#e3e4cb");
        equipment = Lines("emberEquipment", 2, 16, new Vec2(1016, 193), 12, "#e9d4a0");
        foreach (var tab in Enum.GetValues<SidebarPage>())
        {
            var captured = tab;
            PanelButtonAt("emberTab" + tab, () => (page == captured ? "[" : "") + captured + (page == captured ? "]" : ""),
                1016 + (int)tab * 76, 228, 72, () => page = captured);
        }
        for (int i = 0; i < 12; i++)
        {
            int index = i, x = 1016 + i % 4 * 56, y = 264 + i / 4 * 38;
            PanelButtonAt("emberSlot" + i, () => "", x, y, 52, () => { if (slots[index].Item is { } item) selectedItem = item; }, () => !welcome && page == SidebarPage.Backpack);
            var icon = Sprite("emberItem" + i, "ore", new Vec2(x + 17, y + 14), new Vec2(24, 24), 125);
            var count = Label("emberItemCount" + i, "", new Vec2(x + 49, y + 20), 12, "#fff0c3", SKTextAlign.Right);
            slots.Add((icon, count, null));
        }
        detail = Lines("emberDetail", 7, 16, new Vec2(1016, 393), 12, "#e3e4cb");
        pageText = Lines("emberPage", 13, 17, new Vec2(1016, 279), 12, "#e3e4cb");
        serviceTitle = Label("emberServiceTitle", "", new Vec2(1016, 505), 14, "#e9d4a0");
        PanelButtonAt("emberPreviousRecipe", () => "< Recipe", 1016, 516, 108, () => SelectRecipe(-1), () => !welcome && (AtForge || AtFire));
        PanelButtonAt("emberNextRecipe", () => "Recipe >", 1132, 516, 108, () => SelectRecipe(1), () => !welcome && (AtForge || AtFire));
        PanelButtonAt("emberTalk", () => "Talk to Bram", 1016, 516, 108, World.TalkToBram, () => !welcome && AtShop);
        PanelButtonAt("emberBuy", () => $"Buy {quantity} / {quantity * 3}c", 1132, 516, 108, () => World.BuyRation(quantity), () => !welcome && AtShop);
        PanelButtonAt("emberDepositAll", () => "Deposit all", 1016, 516, 108, World.DepositAll, () => !welcome && AtBank);
        PanelButtonAt("emberBankQuantity", () => "Amount: " + quantity, 1132, 516, 108, CycleQuantity, () => !welcome && AtBank);
        PanelButtonAt("emberDeposit", () => "Deposit " + quantity, 1016, 550, 108, () => World.Deposit(selectedItem, quantity), () => !welcome && AtBank);
        PanelButtonAt("emberWithdraw", () => "Withdraw " + quantity, 1132, 550, 108, () => World.Withdraw(selectedItem, quantity), () => !welcome && AtBank);
        PanelButtonAt("emberCraftAmount", () => "Batch: " + quantity, 1016, 550, 108, CycleQuantity, () => !welcome && (AtForge || AtFire));
        PanelButtonAt("emberCraft", () => CraftCaption, 1132, 550, 108, () => { page = SidebarPage.Backpack; World.StartBatch(SelectedRecipe.Id, quantity); }, () => !welcome && (AtForge || AtFire));
        PanelButtonAt("emberRepairSupplies", () => "4 logs + 1 bar", 1016, 550, 108, () => World.RepairBridge(false), () => !welcome && AtShop && World.BridgeStage == 1);
        PanelButtonAt("emberRepairCoins", () => "Repair / 45c", 1132, 550, 108, () => World.RepairBridge(true), () => !welcome && AtShop && World.BridgeStage == 1);
        PanelButtonAt("emberShopQuantity", () => "Amount: " + quantity, 1016, 550, 224, CycleQuantity, () => !welcome && AtShop && World.BridgeStage != 1);
        PanelButtonAt("emberSell", () => $"Sell up to {quantity} / {EmberbrookWorld.SalePrice(selectedItem)}c each", 1016, 584, 224, () => World.SellItem(selectedItem, quantity), () => !welcome && AtShop);
        PanelButtonAt("emberDiscuss", () => "Discuss the village", 1016, 516, 224, () => World.TalkToElin(false), () => !welcome && AtElder);
        PanelButtonAt("emberQuestAction", () => "Accept / turn in quest", 1016, 550, 224, () => World.TalkToElin(true), () => !welcome && AtElder);
        PanelButtonAt("emberEquip", () => World.SpearEquipped ? "Equip sword + shield" : "Equip ash spear", 1016, 516, 224, World.EquipSpear, () => !welcome && !AtElder && !AtShop && !AtBank && !AtForge && !AtFire && !AtResident && page == SidebarPage.Backpack);
        PanelButtonAt("emberCharm", () => "Charm: " + World.Charm + " [T]", 1016, 550, 224, World.ToggleCharm, () => !welcome && !AtElder && !AtShop && !AtBank && !AtForge && !AtFire && !AtResident && page != SidebarPage.Journal);
        PanelButtonAt("emberTrack", () => World.TrackBridge ? "Track main adventure" : "Track Broken Crossing", 1016, 516, 224, World.ToggleTrackedQuest, () => !welcome && page == SidebarPage.Journal && !AtElder && !AtShop && !AtBank && !AtForge && !AtFire);
        PanelButtonAt("emberRepeat", () => "Repeat: " + (World.RepeatGathering ? "ON" : "OFF") + " [M]", 1016, 584, 224, World.ToggleRepeatGathering, () => !welcome && !AtShop && !AtResident && !(AtElder && World.TrailQuestStage == 3));
        PanelButtonAt("emberEat", () => "Eat [E]", 570, 52, 80, World.Eat);
        PanelButtonAt("emberStop", () => "Stop [X]", 656, 52, 80, World.CancelActivity);
        PanelButtonAt("emberSave", () => "Save [S]", 742, 52, 80, Save);
        PanelButtonAt("emberLoad", () => "Load [L]", 828, 52, 80, Load);
        PanelButtonAt("emberMute", () => muted ? "Sound: off" : "Sound: on", 914, 52, 96, () => { muted = !muted; if (muted) audio?.StopAll(); });
        InitializeVillagePanels();
        saveStatus = Label("emberSaveStatus", "", new Vec2(1020, 72), 11, "#c6c2a0");
        var backdrop = Sprite("emberWelcome", "panel", new Vec2(500, 380), new Vec2(500, 260), 140);
        welcomeEntities.Add(backdrop);
        var title = Label("emberWelcomeTitle", "WELCOME TO EMBERBROOK", new Vec2(500, 303), 24, "#e9d4a0", SKTextAlign.Center);
        var intro = Label("emberWelcomeIntro", "Prepare. Explore. Bring the village back to life.", new Vec2(500, 345), 17, "#e3e4cb", SKTextAlign.Center);
        welcomeTitle = title; welcomeIntro = intro;
        PanelButtonAt("emberNew", () => "New adventure", 290, 398, 190, () => { World.Restore(new EmberbrookWorld().Capture()); page = SidebarPage.Backpack; journalSection = bookIndex = residentIndex = 0; welcome = false; ResetFeedback(); savedMilestone = Milestone; saveMessage = "New adventure / not saved"; World.Notify("Welcome to Emberbrook. Meet Elin in the village square."); }, () => welcome);
        PanelButtonAt("emberContinue", () => "Continue", 510, 398, 190, Load, () => welcome);
    }
    private CText welcomeTitle = null!;
    private CText welcomeIntro = null!;
    private void CycleQuantity() => quantity = quantity == 1 ? 5 : quantity == 5 ? 10 : 1;
    private static string ItemImage(Item item) => item switch { Item.Ore => "itemore", Item.Bar => "itembar", Item.Ration => "itemration", Item.Trout => "itemtrout", Item.Meal => "itemmeal", Item.Log => "itemlog", _ => "itemsmoked" };

    private void UpdatePanels()
    {
        foreach (var button in panelButtons)
        {
            bool visible = button.Visible();
            button.Sprite.GetComponent<CAnimation>().ShouldDraw = visible;
            button.Text.ShouldDraw = visible;
            button.Text.Text = button.Caption();
            var textEntity = entities.GetEntityWithTag(button.Sprite.Tag + "Text");
            if (textEntity != null) textEntity.GetComponent<CTransform>().Layer = 151;
            if (button.Sprite.Tag is "emberNew" or "emberContinue" or "emberHomeReturn" or "emberHomeUnfinished") button.Sprite.GetComponent<CTransform>().Layer = 150;
        }
        foreach (var entity in welcomeEntities) entity.GetComponent<CAnimation>().ShouldDraw = welcome;
        welcomeTitle.ShouldDraw = welcomeIntro.ShouldDraw = welcome;
        foreach (string tag in new[] { "emberWelcomeTitle", "emberWelcomeIntro" })
            if (entities.GetEntityWithTag(tag) is { } entity) entity.GetComponent<CTransform>().Layer = 151;
        saveStatus.Text = saveMessage;
        for (int i = 0; i < 12; i++)
            if (entities.GetEntityWithTag("emberItemCount" + i) is { } countEntity) countEntity.GetComponent<CTransform>().Layer = 130;
        var contents = new List<(Item, int)>();
        foreach (var item in Enum.GetValues<Item>())
        {
            if (AtBank) { contents.Add((item, World.BankCount(item))); continue; }
            for (int remaining = World.Count(item); remaining > 0; remaining -= EmberbrookWorld.StackSize(item))
                contents.Add((item, Math.Min(remaining, EmberbrookWorld.StackSize(item))));
        }
        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            bool visible = !welcome && page == SidebarPage.Backpack && i < contents.Count;
            slot.Icon.GetComponent<CAnimation>().ShouldDraw = visible;
            slot.Quantity.ShouldDraw = visible;
            if (i < contents.Count)
            {
                var (item, count) = contents[i];
                slot.Icon.GetComponent<CAnimation>().Animation = assets.GetAnimation("Ember" + ItemImage(item), new Vec2(24, 24));
                slot.Quantity.Text = count.ToString();
                slots[i] = (slot.Icon, slot.Quantity, item);
            }
            else slots[i] = (slot.Icon, slot.Quantity, null);
        }
        detail.Text = page != SidebarPage.Backpack || welcome ? "" : $"{EmberbrookWorld.ItemName(selectedItem).ToUpperInvariant()}\nCarry {World.Count(selectedItem)} / Bank {World.BankCount(selectedItem)}\nPack {World.PackUsed}/12 slots / stack {EmberbrookWorld.StackSize(selectedItem)}\n" +
            (AtForge || AtFire ? $"{SelectedRecipe.Name}\nNeeds: {SelectedRecipe.Cost}\nOre {World.Count(Item.Ore)} Bars {World.Count(Item.Bar)} Logs {World.Count(Item.Log)}\nTrout {World.Count(Item.Trout)} / coins {World.Coins}" : selectedItem is Item.Meal or Item.Ration or Item.SmokedTrout ? $"Heals {(selectedItem == Item.Meal ? World.MealHealing : 8)} health.\nUse Eat or E when wounded." : selectedItem switch
            {
                Item.Ore => "Smelt 3 ore into 1 copper bar.\nUse the village workbench.",
                Item.Bar => "Forge weapons, shield, or tools.\nA bridge repair uses one bar.",
                Item.Log => "Repair bridges, light beacons,\nforge spears, or smoke trout.",
                _ => "Cook at the western fire.\nGrill for healing or smoke\nfor compact expedition food."
            });
        pageText.Text = welcome || page == SidebarPage.Backpack ? "" : page == SidebarPage.Skills
            ? $"Mining Lv {World.MiningLevel} / {World.MiningXp} XP\n{(World.HasTool ? "Tools ready: rich vein / 3 ore" : "Forge tools to unlock rich vein")}\n\nCombat Lv {World.CombatLevel}\nBonus +{World.CombatBonus}/4 (every 2 levels)\n\nFishing Lv {World.FishingLevel}\nLv 3: use a built fishing jetty\nCooking Lv {World.CookingLevel}\nLv 2/3: meals heal 14/16\nWoodcutting Lv {World.WoodcuttingLevel}\nLv 3: clear the Lost Way.\nTool recipe: 2 bars + 30c"
            : $"{QuestStatus(World.QuestStage, 2, true)} Copper Promise\n{QuestStatus(World.RuinQuestStage, 3, World.QuestStage == 2)} Sunken Bell\n{QuestStatus(World.RiverQuestStage, 2, World.RuinQuestStage == 3)} River's Bounty\n{QuestStatus(World.TrailQuestStage, 3, World.RiverQuestStage == 2)} Ashen Trail\n{QuestStatus(World.BridgeStage, 2, World.QuestStage == 2)} Broken Crossing\n\n"
                + (World.TrailQuestStage == 3 ? "Join Elin for the homecoming.\n" : World.RuinQuestStage == 1 ? "Halls: weapon + several meals.\n" : "Elin: next adventure goal.\n")
                + (World.AcceptedRequest >= 0 ? EmberbrookWorld.Requests[World.AcceptedRequest].Name + " / delivery\n" : "Nell and Orin: village requests.\n")
                + (World.BridgeStage < 2 && World.QuestStage == 2 ? "Bram: repair the crossing.\n" : "Look for unusual landmarks.\n") + "Browse more work below.";
        serviceTitle.Text = AtElder ? "ELIN / VILLAGE WARDEN" : AtBank ? "BANK / select stored item" : AtForge ? "WORKBENCH" : AtFire ? "COOKING FIRE" : AtShop ? "BRAM / PROVISIONS" : page == SidebarPage.Journal ? "TRACK AN OBJECTIVE" : "PREPARE FOR THE ROAD";
        UpdateVillagePanels();
    }
}
