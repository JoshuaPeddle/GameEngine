using System;
using System.Linq;
using GameEngine.Core;
using GameEngine.Core.Components;
using GameEngine.Demo.Emberbrook;
using SkiaSharp;

namespace GameEngine.Demo;

public sealed partial class SceneEmberbrook
{
    private int journalSection;
    private int bookIndex;
    private int residentIndex;
    private string renderedVillage = "";
    private bool AtResident => World.Beside(SiteKind.Nell) || World.Beside(SiteKind.Orin);
    private SiteKind Resident => World.Beside(SiteKind.Nell) ? SiteKind.Nell : SiteKind.Orin;
    private VillageRequest ResidentRequest => EmberbrookWorld.Requests.Where(r => r.Resident == Resident).ToArray()[residentIndex % 2];
    private VillageRequest BookRequest => EmberbrookWorld.Requests[bookIndex % 4];
    private readonly System.Collections.Generic.List<Entity> celebrationEntities = [];
    private readonly System.Collections.Generic.List<CText> celebrationLabels = [];
    private TextBlock celebrationText = null!;
    private string BookHeading => journalSection switch { 1 => "VILLAGE REQUESTS", 2 => "FIELD BOOK", 3 => "CONSTRUCTION", _ => "ADVENTURE JOURNAL" };

    private bool VillageButtonVisible(string tag, int y, Func<bool> visible)
    {
        if (World.HomecomingOpen) return tag is "emberHomeReturn" or "emberHomeUnfinished" && visible();
        if (tag is "emberHomeReturn" or "emberHomeUnfinished") return false;
        if (y is >= 516 and < 620 && page == SidebarPage.Journal && journalSection > 0 && !tag.StartsWith("emberBook")) return false;
        return visible();
    }

    private void InitializeVillagePanels()
    {
        PanelButtonAt("emberJournalSection", () => BookHeading + " >", 1016, 473, 224,
            () => { journalSection = (journalSection + 1) % 4; bookIndex = 0; }, () => !welcome && page == SidebarPage.Journal);
        PanelButtonAt("emberBookPrevious", () => "< Previous", 1016, 516, 108, () => bookIndex = (bookIndex + BookCount - 1) % BookCount, BookVisible);
        PanelButtonAt("emberBookNext", () => "Next >", 1132, 516, 108, () => bookIndex = (bookIndex + 1) % BookCount, BookVisible);
        PanelButtonAt("emberBookAction", BookActionCaption, 1016, 550, 224, BookAction, BookVisible);
        PanelButtonAt("emberBookAbandon", () => "Abandon active delivery", 1016, 584, 224, World.AbandonRequest,
            () => BookVisible() && journalSection == 1 && World.AcceptedRequest >= 0);
        PanelButtonAt("emberResidentPrevious", () => "< Request", 1016, 516, 108, () => residentIndex = 1 - residentIndex, () => !welcome && AtResident);
        PanelButtonAt("emberResidentNext", () => "Request >", 1132, 516, 108, () => residentIndex = 1 - residentIndex, () => !welcome && AtResident);
        PanelButtonAt("emberResidentTalk", () => "Talk to " + Resident, 1016, 550, 108, () => World.TalkToResident(Resident), () => !welcome && AtResident);
        PanelButtonAt("emberResidentRequest", () => World.RequestComplete(ResidentRequest.Id) ? "Completed" : World.AcceptedRequest == ResidentRequest.Id ? "Hand in" : "Accept request",
            1132, 550, 108, () => World.ActOnRequest(ResidentRequest.Id), () => !welcome && AtResident);
        PanelButtonAt("emberResidentAbandon", () => "Abandon active delivery", 1016, 584, 224, World.AbandonRequest,
            () => !welcome && AtResident && World.AcceptedRequest >= 0);
        PanelButtonAt("emberProjects", () => "Construction projects", 1016, 439, 224,
            () => { page = SidebarPage.Journal; journalSection = 3; bookIndex = 0; }, () => !welcome && AtShop && page == SidebarPage.Backpack);
        PanelButtonAt("emberHomecomingOffer", () => World.HomecomingSeen ? "Replay homecoming" : "Join the homecoming", 1016, 584, 224,
            World.OpenHomecoming, () => !welcome && AtElder && World.TrailQuestStage == 3);
        var backdrop = Sprite("emberCelebration", "panel", new Vec2(504, 388), new Vec2(856, 450), 140);
        celebrationEntities.Add(backdrop);
        var title = Label("emberCelebrationTitle", "A HOMECOMING", new Vec2(504, 208), 30, "#e9d4a0", SKTextAlign.Center, 151);
        celebrationLabels.Add(title);
        title.ShouldDraw = false;
        var lines = new CText[11];
        for (int i = 0; i < lines.Length; i++)
        {
            lines[i] = Label("emberCelebrationLine" + i, "", new Vec2(112, 290 + i * 25), 17, "#e3e4cb", SKTextAlign.Left, 151);
            celebrationLabels.Add(lines[i]);
        }
        celebrationText = new TextBlock(lines);
        foreach (var (name, x) in new[] { ("elder", 354), ("merchant", 454), ("nell", 554), ("orin", 654) })
            celebrationEntities.Add(Sprite("emberCelebrationGuest", name, new Vec2(x, 246), new Vec2(40, 40), 145));
        PanelButtonAt("emberHomeReturn", () => "Skip / return to village", 124, 565, 320, World.CloseHomecoming, () => World.HomecomingOpen);
        PanelButtonAt("emberHomeUnfinished", () => "View unfinished work", 464, 565, 320,
            OpenUnfinishedWork, () => World.HomecomingOpen);
    }

    private void OpenUnfinishedWork()
    {
        World.CloseHomecoming(); page = SidebarPage.Journal;
        bookIndex = Array.FindIndex(EmberbrookWorld.Requests, r => !World.RequestComplete(r.Id));
        if (bookIndex >= 0) { journalSection = 1; return; }
        if (!World.JettyBuilt || !World.ReturnGateBuilt) { journalSection = 3; bookIndex = World.JettyBuilt ? 1 : 0; return; }
        journalSection = 2;
        bookIndex = Enumerable.Range(0, 6).FirstOrDefault(i => !World.Found(i));
    }

    private bool BookVisible() => !welcome && page == SidebarPage.Journal && journalSection > 0;
    private int BookCount => journalSection == 1 ? 4 : journalSection == 2 ? 6 : 2;
    private string BookActionCaption() => journalSection switch
    {
        1 => World.RequestComplete(BookRequest.Id) ? "Completed" : World.AcceptedRequest == BookRequest.Id ? "Hand in to " + BookRequest.Resident : "Accept at " + BookRequest.Resident,
        2 => World.KeeperAwarded ? "Keeper of the Paths" : "Show fieldbook to Elin",
        _ => (bookIndex % 2 == 0 ? World.JettyBuilt : World.ReturnGateBuilt) ? "Built" : "Build with Bram"
    };
    private void BookAction()
    {
        if (journalSection == 1) World.ActOnRequest(BookRequest.Id);
        else if (journalSection == 2) World.ClaimFieldbook();
        else World.BuildProject(bookIndex % 2 != 0);
    }

    private string RequestText(VillageRequest r) => $"{r.Name}\n{(World.RequestComplete(r.Id) ? "COMPLETE" : World.AcceptedRequest == r.Id ? "ACTIVE DELIVERY" : World.RequestUnlocked(r.Id) ? "AVAILABLE" : "LOCKED")} / {r.Resident}\n"
        + Wrap("Bring: " + r.Cost, 31) + $"\nReward: {r.Coins} coins\n"
        + (World.RequestUnlocked(r.Id) ? string.Join("\n", Enum.GetValues<Item>().Where(i => r.Needs(i) > 0).Select(i => $"{EmberbrookWorld.ItemName(i)}: {World.Count(i)}/{r.Needs(i)}")) : World.RequestRequirement(r.Id));

    private string VillageJournal()
    {
        if (journalSection == 1) return $"REQUEST {bookIndex % 4 + 1}/4\n" + RequestText(BookRequest)
            + "\n\n" + (BookRequest.Resident == SiteKind.Nell ? "Nell: by the western fire." : "Orin: north of the forge.");
        if (journalSection == 2)
        {
            int index = bookIndex % 6;
            var discovery = EmberbrookWorld.Discoveries[index];
            string text = $"PLACES {Enumerable.Range(0, 6).Count(World.Found)}/6  /  PAGE {index + 1}\n";
            return text + (World.Found(index) ? discovery.Name + "\n\n" + Wrap(discovery.Story, 31) : "Undiscovered\n\n" + Wrap(discovery.Hint, 31))
                + (World.KeeperAwarded ? "\nKeeper of the Paths" : "");
        }
        bool gate = bookIndex % 2 != 0;
        return gate ? $"THE OLD RETURN GATE\n{(World.ReturnGateBuilt ? "BUILT" : World.TrailQuestStage == 3 ? "AVAILABLE" : "Finish the Ashen Trail")}\n\n2 bars + 120 coins\nOwned: {World.Count(Item.Bar)} bars / {World.Coins}c\n\nOne-way forest travel home.\nUse while idle and safe.\nNear the Hart's clearing.\n\nBuild at Bram's stall."
            : $"NELL'S FISHING JETTY\n{(World.JettyBuilt ? "BUILT" : World.BridgeStage == 2 ? "AVAILABLE" : "Repair the crossing first")}\n\n4 logs + 1 bar + 25 coins\nOwned: {World.Count(Item.Log)} logs / {World.Count(Item.Bar)} bars\nCoins: {World.Coins}\n\nFishing 3: two trout/catch.\nYour Fishing level: {World.FishingLevel}\n{(World.FishingLevel < 3 ? "Train at ordinary shoals." : "You can fish here once built.")}\nBuild at Bram's stall.";
    }

    private void UpdateVillagePanels()
    {
        serviceTitle.ShouldDraw = !BookVisible() && !World.HomecomingOpen;
        if (AtResident && !BookVisible() && page == SidebarPage.Backpack) detail.Text = RequestText(ResidentRequest);
        if (page == SidebarPage.Journal && journalSection > 0) { pageText.Text = VillageJournal(); serviceTitle.Text = BookHeading; }
        else if (AtResident) serviceTitle.Text = Resident + " / VILLAGE REQUESTS";
        if (AtShop && page == SidebarPage.Backpack)
            detail.Text = $"{EmberbrookWorld.ItemName(selectedItem).ToUpperInvariant()}\nCarry {World.Count(selectedItem)} / reserve {World.Reserved(selectedItem)}\nSale price {EmberbrookWorld.SalePrice(selectedItem)}c each";
        foreach (var entity in celebrationEntities)
        {
            if (entity.TryGetComponent<CAnimation>(out var animation)) animation.ShouldDraw = World.HomecomingOpen;
            if (entity.TryGetComponent<CText>(out var text)) { text.ShouldDraw = World.HomecomingOpen; entity.GetComponent<CTransform>().Layer = 151; }
        }
        foreach (var label in celebrationLabels) label.ShouldDraw = World.HomecomingOpen;
        celebrationText.Text = World.HomecomingOpen
            ? "Elin: The bell rings. The trail is safe. Tonight, we celebrate.\nNell: Sit down. For once, supper isn't something to pack.\nOrin: Good work lasts. Look around you.\nBram: Tomorrow can wait until tomorrow.\n\n"
                + "Bell restored / feast prepared / three beacons burning.\n"
                + (World.BridgeStage == 2 ? "The crossing joins our neighbours again. " : "") + (World.JettyBuilt ? "Nell's jetty is open." : "") + "\n"
                + $"Village deliveries: {Enumerable.Range(0, 4).Count(World.RequestComplete)}/4. Places found: {Enumerable.Range(0, 6).Count(World.Found)}/6.\n"
                + (World.KeeperAwarded ? "Your explorer's banner hangs above the square.\n" : "\n")
                + "Emberbrook is safe. This chapter is complete."
            : "";
    }

    private string VillageSiteImage(WorldSite site) => site.Kind switch
    {
        SiteKind.Nell => "nell", SiteKind.Orin => "orin", SiteKind.Jetty => World.JettyBuilt ? "jetty" : "ruinedjetty",
        SiteKind.ReturnGate => World.ReturnGateBuilt ? "returngate" : "ruinedgate",
        SiteKind.LostWay => "fallenlog", SiteKind.Landmark => site.Id == "quietSpring" && World.HartDefeated ? "springbloom" : EmberbrookWorld.Discoveries.Single(d => d.Id == site.Id).Image,
        _ => "mossling"
    };

    private void BuildVillageChanges()
    {
        renderedVillage = World.VillageProgress;
        if (World.Region != Region.Village) return;
        if (World.RequestComplete(0)) Sprite("emberSupperBowls", "bowls", Position(new Cell(1, 6)), new Vec2(40, 40), 7);
        if (World.RequestComplete(1)) Sprite("emberRepairedWheel", "wheel", Position(new Cell(7, 10)), new Vec2(40, 40), 11);
        if (World.RequestComplete(2)) Sprite("emberTrailLunches", "lunches", Position(new Cell(12, 13)), new Vec2(40, 40), 14);
        if (World.RequestComplete(3)) { Sprite("emberDedication", "sign", Position(new Cell(6, 11)), new Vec2(40, 40), 12); Label("emberDedicationText", "Our adventurer", Position(new Cell(6, 11)) + new Vec2(0, -22), 11, "#fff0c3", SKTextAlign.Center); }
        if (World.FloatShown) Sprite("emberReturnedFloat", "float", Position(new Cell(1, 4)), new Vec2(24, 24), 5);
        if (World.Found(3) && World.RuinQuestStage == 3) Label("emberBellmakerName", "Mara, bellmaker", Position(new Cell(5, 2)) + new Vec2(0, -22), 11, "#fff0c3", SKTextAlign.Center);
        if (World.KeeperAwarded) Sprite("emberExplorerBanner", "banner", Position(new Cell(3, 5)), new Vec2(40, 40), 6);
    }
}
