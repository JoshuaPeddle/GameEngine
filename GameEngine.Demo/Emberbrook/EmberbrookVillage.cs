using System;
using System.Linq;

namespace GameEngine.Demo.Emberbrook;

public sealed record VillageRequest(int Id, string Name, SiteKind Resident, int Bars, int Logs, int Meals, int Smoked, int Coins, string Story)
{
    public int Needs(Item item) => item switch { Item.Bar => Bars, Item.Log => Logs, Item.Meal => Meals, Item.SmokedTrout => Smoked, _ => 0 };
    public string Cost => string.Join(" + ", Enum.GetValues<Item>().Where(i => Needs(i) > 0).Select(i => $"{Needs(i)} {EmberbrookWorld.ItemName(i)}"));
}
public sealed record VillageDiscovery(string Id, string Name, string Hint, string Story, string Image);

public sealed partial class EmberbrookWorld
{
    public static readonly VillageRequest[] Requests =
    [
        new(0, "Supper for Two", SiteKind.Nell, 0, 0, 2, 0, 14, "Nell: Two trout, if you're fishing. Bram calls a ration supper. I disagree."),
        new(1, "A Proper Delivery", SiteKind.Orin, 2, 4, 0, 0, 16, "Orin: The wheel's sound. It's the axle that's given up. Two bars, four logs, and Bram can stop carrying everything on his back."),
        new(2, "Trail Lunches", SiteKind.Nell, 0, 0, 0, 3, 15, "Nell: Three smoked trout for the trail crews. A smaller bite, but two portions to a pocket."),
        new(3, "The Last Repairs", SiteKind.Orin, 3, 6, 0, 0, 24, "Orin: A new village sign deserves sound fittings. Three bars and six logs. I'll leave room for your name.")
    ];
    public static readonly VillageDiscovery[] Discoveries =
    [
        new("floodMark", "Flood Mark", "Look beside the northern crossing.", "The notch is above your head. The flood split two neighbourhoods overnight. Bram remembers the crossing before it washed away.", "floodmark"),
        new("oldFloat", "Nell's Old Float", "Something glints near the southern fishing bank.", "A painted wooden float, carved with an N. Nell by the western fire might remember it.", "float"),
        new("quarryMarks", "Quarry Marks", "Look around the rich copper outcrop.", "Old miners marked the deep seams: reinforced tools loosen three ore at a time. Orin knows the recipe: two bars and thirty coins.", "quarrymarks"),
        new("bellmaker", "The Bellmaker's Name", "An inscription waits in a side alcove of the halls.", "MARA, BELLMAKER. May its voice bring us home. Her name belongs on the restored village bell.", "tablet"),
        new("lostWay", "The Lost Way", "An old trunk blocks the western forest hedge.", "The trail keepers left this path for returning travellers. The fallen trunk is cleared; the shorter way stays open.", "fallenlog"),
        new("quietSpring", "The Quiet Spring", "Water glimmers beyond the Hart's thicket.", "Roots reach into a still pool. The trail keepers tended the forest, rather than owning it. After the Hart falls, lilies open here again.", "spring")
    ];
    public int AcceptedRequest { get; private set; } = -1;
    public int CompletedRequests { get; private set; }
    public int Discovered { get; private set; }
    public bool FloatShown { get; private set; }
    public bool KeeperAwarded { get; private set; }
    public bool JettyBuilt { get; private set; }
    public bool ReturnGateBuilt { get; private set; }
    public bool HomecomingSeen { get; private set; }
    public bool HomecomingOpen { get; private set; }
    public bool LostWayOpen => Found(4);
    public bool Found(int index) => (Discovered & (1 << index)) != 0;
    public bool RequestComplete(int id) => (CompletedRequests & (1 << id)) != 0;
    public bool RequestUnlocked(int id) => id switch { 0 => true, 1 => QuestStage == 2, 2 => RiverQuestStage == 2 && BridgeStage == 2, 3 => TrailQuestStage == 3, _ => false };
    public string RequestRequirement(int id) => id switch { 1 => "Finish Copper Promise.", 2 => "Finish the feast and crossing.", 3 => "Finish the Ashen Trail.", _ => "Available on arrival." };
    public bool SafeToTravel => Activity == "Idle" && DangerCentre == null && !ActiveSites.Any(s => s.IsEnemy && s.Hull > 0 && s.Cell.Distance(Player) <= 6);
    public string VillageProgress => $"{AcceptedRequest}:{CompletedRequests}:{Discovered}:{FloatShown}:{KeeperAwarded}:{JettyBuilt}:{ReturnGateBuilt}:{HomecomingSeen}";
    public int Reserved(Item item) => (item == Item.Meal && RiverQuestStage == 1 ? 3 : 0)
        + (BridgeStage == 1 ? item == Item.Log ? 4 : item == Item.Bar ? 1 : 0 : 0)
        + (AcceptedRequest >= 0 ? Requests[AcceptedRequest].Needs(item) : 0);
    private bool SitePresent(WorldSite site) => site.Kind != SiteKind.LostWay || !LostWayOpen;

    public void TalkToResident(SiteKind resident)
    {
        if (!Beside(resident)) { Message = "Walk beside the resident to talk."; return; }
        Stop();
        if (resident == SiteKind.Nell)
        {
            if (Found(1) && !FloatShown) { FloatShown = true; Message = "Nell: My old float! Bram made it before he learned to carve a straight line. I'll hang it here by the fire."; }
            else Message = TrailQuestStage == 3 ? "Nell: The trail crews are home. Tonight, we're cooking for pleasure."
                : BridgeStage == 2 ? "Nell: I crossed over to see Bram. He still calls a ration supper. Some things even a new bridge can't fix."
                : RequestComplete(0) ? "Nell: Bram is my brother. Those two bowls finally got him to sit down for supper."
                : Requests[0].Story;
        }
        else if (resident == SiteKind.Orin)
            Message = TrailQuestStage == 3 ? "Orin: A safe road, a bell, and people to use them. That's work worth putting your name to."
                : RuinQuestStage == 3 ? "Orin: I can hear the evening bell again. Reminds me to put the hammer down."
                : RequestComplete(1) ? "Orin: Bram's wheel turns true. Forge reinforced tools for the rich vein: two bars and thirty coins."
                : "Orin: Six ore, two bars, one sword. Then make something that keeps you safe. The forge uses the recipe you select.";
    }

    public void ActOnRequest(int id)
    {
        if (id < 0 || id >= Requests.Length) return;
        var request = Requests[id];
        if (!Beside(request.Resident)) { Message = "Bring the supplies to " + (request.Resident == SiteKind.Nell ? "Nell by the western fire." : "Orin north of the forge."); return; }
        if (RequestComplete(id)) { Message = "This request is complete. Your work is still here in the village."; return; }
        if (!RequestUnlocked(id)) { Message = RequestRequirement(id); return; }
        if (AcceptedRequest != id)
        {
            if (AcceptedRequest >= 0) { Message = "Finish or abandon your current delivery in the Journal first."; return; }
            Stop(); AcceptedRequest = id; Message = request.Story + " Bring " + request.Cost + $" for {request.Coins} coins."; return;
        }
        if (Enum.GetValues<Item>().Any(i => Count(i) < request.Needs(i))) { Message = "Bring the complete delivery: " + request.Cost + ". Nothing taken."; return; }
        Stop();
        foreach (var item in Enum.GetValues<Item>()) if (request.Needs(item) > 0) Take(item, request.Needs(item));
        Coins += request.Coins;
        CompletedRequests |= 1 << id;
        AcceptedRequest = -1;
        Message = request.Name + $" complete! +{request.Coins} coins. " + (id switch
        {
            0 => "Nell sets out two bowls. Bram is her brother; tonight he'll eat properly.",
            1 => "Bram's wheel is repaired. Orin recommends reinforced tools for the rich vein.",
            2 => "Lunches are packed beside the southern gate.", _ => "The village sign bears your name: Our adventurer, who brought the roads back."
        });
    }

    public void AbandonRequest()
    {
        if (AcceptedRequest < 0) return;
        AcceptedRequest = -1;
        Message = "Delivery set aside. Your supplies are released for sale; you can accept it again later.";
    }

    private void Discover(WorldSite site)
    {
        int index = Array.FindIndex(Discoveries, d => d.Id == site.Id);
        if (index < 0) return;
        if (site.Kind == SiteKind.LostWay && (TrailQuestStage == 0 || WoodcuttingLevel < 3))
        { Message = "The Lost Way needs the deep trail open and Woodcutting level 3. The original path runs south through the hedge."; Stop(); return; }
        Discovered |= 1 << index;
        Message = Discoveries[index].Name + ": " + Discoveries[index].Story;
        Stop();
    }

    public void ClaimFieldbook()
    {
        if (!Beside(SiteKind.Elder)) { Message = "Show your fieldbook to Elin in the village square."; return; }
        if (Discovered != 63) { Message = "Discover all six places in your Journal's fieldbook."; return; }
        if (KeeperAwarded) { Message = "Elin: Keeper of the Paths, your banner has a home here."; return; }
        KeeperAwarded = true;
        Message = "Elin hangs your explorer's banner. Keeper of the Paths: you know this place, and it knows you.";
    }

    public string ProjectRequirement(bool gate) => gate ? "Finish the Ashen Trail." : "Repair the northern crossing.";
    public void BuildProject(bool gate)
    {
        if (!Beside(SiteKind.Merchant)) { Message = "Ask Bram beside the supply chest about construction."; return; }
        if (gate ? ReturnGateBuilt : JettyBuilt) { Message = "This project is already built."; return; }
        if (gate ? TrailQuestStage != 3 : BridgeStage != 2) { Message = ProjectRequirement(gate); return; }
        int bars = gate ? 2 : 1, logs = gate ? 0 : 4, coins = gate ? 120 : 25;
        if (Count(Item.Bar) < bars || Count(Item.Log) < logs || Coins < coins)
        { Message = gate ? "Return gate needs 2 bars and 120 coins. Nothing taken." : "Jetty needs 4 logs, 1 bar and 25 coins. Nothing taken."; return; }
        Stop(); Take(Item.Bar, bars); if (logs > 0) Take(Item.Log, logs); Coins -= coins;
        if (gate) ReturnGateBuilt = true; else JettyBuilt = true;
        Message = gate ? "The old return gate is restored beside the Hart's clearing. Use it in safety to travel home."
            : "Nell's fishing jetty is built! Fishing level 3: two trout per catch. " + (FishingLevel < 3 ? "Train at an ordinary shoal until level 3." : "Nell: Room to cast a proper line!");
    }

    public void UseReturnGate()
    {
        if (Region != Region.Forest || !ReturnGateBuilt) { Message = "Bram can restore this return gate after the Ashen Trail."; return; }
        if (!SafeToTravel) { Message = "The return gate needs safety: stop work, leave marked attacks, and move away from enemies. Nothing queued."; return; }
        UseGateRoute();
    }

    private void UseGateRoute()
    {
        var gate = Sites.Single(s => s.Kind == SiteKind.ReturnGate);
        if (Player.Distance(gate.Cell) == 1) { Travel(Region.Village, new Cell(5, 7)); return; }
        Select(gate.Cell);
    }

    public void OpenHomecoming()
    {
        if (!Beside(SiteKind.Elder) || TrailQuestStage != 3) { Message = "Finish the Ashen Trail and speak to Elin to join the homecoming."; return; }
        if (!SafeToTravel) { Message = "Finish your activity and meet Elin in safety first."; return; }
        HomecomingOpen = true;
    }

    public void CloseHomecoming()
    {
        if (!HomecomingOpen) return;
        HomecomingOpen = false; HomecomingSeen = true;
        Message = "Emberbrook is safe. This chapter is complete. Your unfinished work is still waiting in the Journal.";
    }
}
