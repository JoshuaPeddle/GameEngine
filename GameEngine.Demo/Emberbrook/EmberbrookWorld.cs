using System;
using System.Collections.Generic;
using System.Linq;

namespace GameEngine.Demo.Emberbrook;

public readonly record struct Cell(int X, int Y)
{
    public int Distance(Cell other) => Math.Abs(X - other.X) + Math.Abs(Y - other.Y);
}

public enum Region { Village, Ruins, Forest }
public enum SiteKind { Elder, Ore, Forge, Bank, Merchant, Mossling, Entrance, Exit, Sentinel, Guardian, Relic, Fishing, Campfire, TrailGate, TrailExit, AshTree, Beacon, Stalker, Hart, RichOre, Nell, Orin, Landmark, LostWay, Jetty, ReturnGate }
public enum Item { Ore, Bar, Ration, Trout, Meal, Log, SmokedTrout }

public sealed class WorldSite(string id, SiteKind kind, Cell cell, Region region = Region.Village)
{
    public string Id { get; } = id;
    public SiteKind Kind { get; } = kind;
    public Cell Cell { get; set; } = cell;
    public Cell Home { get; } = cell;
    public Region Region { get; } = region;
    public int MaxHull => Kind == SiteKind.Hart ? 80 : Kind == SiteKind.Stalker ? 24 : Kind == SiteKind.Guardian ? 60 : Kind == SiteKind.Sentinel ? 18 : 10;
    public int Hull { get; set; } = kind == SiteKind.Hart ? 80 : kind == SiteKind.Stalker ? 24 : kind == SiteKind.Guardian ? 60 : kind == SiteKind.Sentinel ? 18 : 10;
    public double MoveClock { get; set; }
    public double AttackClock { get; set; }
    public bool IsEnemy => Kind is SiteKind.Mossling or SiteKind.Sentinel or SiteKind.Guardian or SiteKind.Stalker or SiteKind.Hart;
    public double RespawnSeconds { get; set; }
    public string Name => Kind switch
    {
        SiteKind.Elder => "Warden Elin", SiteKind.RichOre => "Rich copper vein", SiteKind.Ore => "Copper seam", SiteKind.Forge => "Village forge",
        SiteKind.Nell => "Nell", SiteKind.Orin => "Orin", SiteKind.Jetty => "Fishing jetty", SiteKind.ReturnGate => "Old return gate",
        SiteKind.Landmark or SiteKind.LostWay => EmberbrookWorld.Discoveries.Single(d => d.Id == Id).Name,
        SiteKind.Bank => "Supply chest", SiteKind.Merchant => "Bram / provisions",
        SiteKind.Entrance => "Sunken halls", SiteKind.Exit => "Return to Emberbrook",
        SiteKind.Sentinel => "Reed sentinel", SiteKind.Guardian => "Bell guardian",
        SiteKind.Relic => "Sunken bell", SiteKind.Fishing => "Trout shoal", SiteKind.Campfire => "Cooking fire", SiteKind.TrailGate => "Ashen Trail", SiteKind.TrailExit => "Village",
        SiteKind.AshTree => "Ash tree", SiteKind.Beacon => "Forest beacon", SiteKind.Stalker => "Ash wolf", SiteKind.Hart => "Briar Hart", _ => "Mossling"
    };
}

public sealed partial class EmberbrookWorld
{
    public const int Width = 24;
    public const int Height = 15;
    public const int PackCapacity = 12;
    private static readonly Cell[] Steps = [new(0, -1), new(-1, 0), new(1, 0), new(0, 1)];
    private readonly Queue<Cell> route = new();
    private readonly Dictionary<Item, int> inventory = new() { [Item.Ration] = 2 };
    private double walkClock;
    private double actionClock;
    private WorldSite? target;
    private double guardianClock = 2;
    public Region Region { get; private set; }
    public int RuinQuestStage { get; private set; }
    public bool HasShield { get; private set; }
    public bool GuardianDefeated { get; private set; }
    public bool RepeatGathering { get; private set; }
    public Cell? DangerCentre { get; private set; }
    public double DangerSeconds { get; private set; }
    public IEnumerable<WorldSite> ActiveSites => Sites.Where(s => s.Region == Region && SitePresent(s));
    public string QuestTitle => TrailQuestStage == 3 ? "A HOMECOMING" : QuestStage < 2 ? "THE COPPER PROMISE" : RuinQuestStage < 3 ? "THE SUNKEN BELL" : RiverQuestStage < 2 ? "RIVER'S BOUNTY" : "THE ASHEN TRAIL";

    public IReadOnlyList<WorldSite> Sites { get; } = new WorldSite[]
    {
        new("nell", SiteKind.Nell, new(2, 6)), new("orin", SiteKind.Orin, new(7, 1)),
        new("floodMark", SiteKind.Landmark, new(10, 2)), new("oldFloat", SiteKind.Landmark, new(10, 9)),
        new("quarryMarks", SiteKind.Landmark, new(21, 4)), new("jetty", SiteKind.Jetty, new(12, 5)),
        new("bellmaker", SiteKind.Landmark, new(4, 3), Region.Ruins),
        new("lostWay", SiteKind.LostWay, new(7, 2), Region.Forest),
        new("quietSpring", SiteKind.Landmark, new(21, 10), Region.Forest),
        new("returnGate", SiteKind.ReturnGate, new(22, 12), Region.Forest),
        new("elder", SiteKind.Elder, new(4, 5)), new("forge", SiteKind.Forge, new(6, 3)),
        new("bank", SiteKind.Bank, new(3, 9)), new("merchant", SiteKind.Merchant, new(6, 9)),
        new("ore1", SiteKind.Ore, new(17, 3)), new("ore2", SiteKind.Ore, new(19, 3)),
        new("ore3", SiteKind.Ore, new(18, 5)), new("mossling1", SiteKind.Mossling, new(17, 10)),
        new("mossling2", SiteKind.Mossling, new(20, 10)), new("mossling3", SiteKind.Mossling, new(19, 12)),
        new("entrance", SiteKind.Entrance, new(23, 7)), new("exit", SiteKind.Exit, new(0, 7), Region.Ruins),
        new("sentinel1", SiteKind.Sentinel, new(11, 5), Region.Ruins),
        new("sentinel2", SiteKind.Sentinel, new(14, 10), Region.Ruins),
        new("guardian", SiteKind.Guardian, new(20, 7), Region.Ruins),
        new("relic", SiteKind.Relic, new(21, 3), Region.Ruins),
        new("fish1", SiteKind.Fishing, new(11, 3)), new("fish2", SiteKind.Fishing, new(12, 10)),
        new("campfire", SiteKind.Campfire, new(0, 4)), new("trailGate", SiteKind.TrailGate, new(13, 14)),
        new("trailExit", SiteKind.TrailExit, new(2, 0), Region.Forest),
        new("ash1", SiteKind.AshTree, new(4, 5), Region.Forest), new("ash2", SiteKind.AshTree, new(9, 10), Region.Forest),
        new("ash3", SiteKind.AshTree, new(16, 5), Region.Forest),
        new("beacon0", SiteKind.Beacon, new(5, 3), Region.Forest), new("beacon1", SiteKind.Beacon, new(18, 3), Region.Forest),
        new("beacon2", SiteKind.Beacon, new(17, 11), Region.Forest),
        new("wolf1", SiteKind.Stalker, new(10, 8), Region.Forest), new("wolf2", SiteKind.Stalker, new(16, 9), Region.Forest),
        new("richOre", SiteKind.RichOre, new(21, 2)),
        new("hart", SiteKind.Hart, new(20, 7), Region.Forest)
    };
    public Cell Player { get; private set; } = new(5, 7);
    public Cell? Destination => route.Count > 0 ? route.Last() : target?.Cell;
    public WorldSite? Target => target;
    public int Hull { get; private set; } = 20;
    public int Coins { get; private set; } = 8;
    public int MiningXp { get; private set; }
    public int CombatXp { get; private set; }
    public int MiningLevel => 1 + MiningXp / 30;
    public int CombatLevel => 1 + CombatXp / 30;
    public int QuestStage { get; private set; }
    public int Kills { get; private set; }
    public bool HasSword { get; private set; }
    public int BankedOre { get; private set; }
    public IReadOnlyDictionary<Item, int> Inventory => inventory;
    public int PackUsed => Inventory.Sum(p => Slots(p.Key, p.Value));
    public string Message { get; private set; } = "Welcome to Emberbrook. Click Warden Elin to begin.";
    public string Activity => BatchRecipe is { } recipe ? "Crafting " + Recipes.Single(r => r.Id == recipe).Name : route.Count > 0 ? "Walking" : target == null ? "Idle" : target.Kind switch
    {
        SiteKind.Ore or SiteKind.RichOre => target.RespawnSeconds > 0 ? "Waiting for ore" : "Mining",
        SiteKind.AshTree or SiteKind.LostWay => target.RespawnSeconds > 0 ? "Waiting for wood" : "Woodcutting",
        SiteKind.Fishing or SiteKind.Jetty => target.RespawnSeconds > 0 ? "Waiting for fish" : "Fishing",
        SiteKind.Campfire => "Cooking",
        SiteKind.Mossling or SiteKind.Sentinel or SiteKind.Guardian or SiteKind.Stalker or SiteKind.Hart => "Fighting", _ => "Interacting"
    };
    private double ActionInterval => target?.Kind switch
    {
        SiteKind.Ore or SiteKind.RichOre => Math.Max(0.6, 1.8 - (MiningLevel - 1) * 0.12),
        SiteKind.AshTree or SiteKind.LostWay => Math.Max(0.8, 2.4 - (WoodcuttingLevel - 1) * 0.15),
        SiteKind.Fishing or SiteKind.Jetty => Math.Max(0.8, 2.2 - (FishingLevel - 1) * 0.15),
        _ => 0.9
    };
    public double ActionProgress => BatchRecipe != null ? Math.Clamp(batchClock / 0.9, 0, 1) : target == null || route.Count > 0 || target.RespawnSeconds > 0
        ? 0 : Math.Clamp(actionClock / ActionInterval, 0, 1);
    public void CancelActivity() { Stop(); Message = "Stopped. Click a destination or choose another activity."; }

    public string Quest => TrackBridge ? BridgeQuest : MainQuest;
    public string MainQuest => QuestStage switch
    {
        0 => "Speak to Warden Elin in the village.",
        1 => $"Forge a copper sword. Defeat mosslings: {Math.Min(Kills, 3)}/3. Return to Elin.",
        _ => RuinQuestStage switch
        {
            0 => "Copper Promise complete. Speak to Elin for a new task.",
            1 => GuardianDefeated ? "Take the bell from its altar in the sunken halls." : "Enter the eastern halls. Defeat the Bell Guardian.",
            2 => "Return the sunken bell to Elin in Emberbrook.",
            _ => RiverQuest
        }
    };

    public bool IsWater(Cell cell) => Region == Region.Village ? VillageWater(cell) : Region == Region.Forest && ForestWater(cell);
    private bool VillageWater(Cell cell) => cell.X is 11 or 12 && cell.Y is not (6 or 7) && !(BridgeStage == 2 && cell.Y == 4);
    public bool IsTree(Cell cell) => Region == Region.Village ? VillageTree(cell) : Region == Region.Forest && ForestTree(cell);
    private static bool VillageTree(Cell cell) => cell is { X: 8, Y: >= 2 and <= 5 } or { X: 8, Y: >= 9 and <= 12 }
        || cell.Y == 1 && cell.X is >= 14 and <= 21 || cell is { X: 15, Y: >= 10 and <= 12 };
    public bool IsBuilding(Cell cell) => Region == Region.Village && VillageBuilding(cell);
    private static bool VillageBuilding(Cell cell) => cell is { X: >= 2 and <= 4, Y: >= 2 and <= 3 }
        or { X: >= 3 and <= 5, Y: >= 11 and <= 12 };
    public bool IsWall(Cell cell) => Region == Region.Ruins && RuinWall(cell);
    private static bool RuinWall(Cell cell) => cell.X == 8 && cell.Y != 7
        || cell.X == 16 && cell.Y is not (4 or 10);
    public bool IsWalkable(Cell cell) => IsWalkable(cell, Region);
    private bool IsWalkable(Cell cell, Region region, bool ignoreEnemies = false) => cell.X > 0 && cell.Y > 0 && cell.X < Width - 1 && cell.Y < Height - 1
        && !(region == Region.Village ? VillageWater(cell) || VillageTree(cell) || VillageBuilding(cell)
            : region == Region.Forest ? ForestWater(cell) || ForestTree(cell) || TrailQuestStage == 0 && cell.X >= 7 : RuinWall(cell))
        && !Sites.Any(s => SitePresent(s) && s.Region == region && s.Cell == cell && !(ignoreEnemies && s.IsEnemy));
    public int Count(Item item) => Inventory.GetValueOrDefault(item);

    public bool Select(Cell cell)
    {
        var site = ActiveSites.FirstOrDefault(s => s.Cell == cell);
        if (site?.Kind == SiteKind.ReturnGate && (!ReturnGateBuilt || !SafeToTravel))
        { Message = ReturnGateBuilt ? "The gate needs safety and no active work. Nothing queued." : "Ask Bram to restore the gate after the Ashen Trail."; return false; }
        if (site?.RespawnSeconds > 0 && !(site.Kind is SiteKind.Ore or SiteKind.RichOre or SiteKind.Fishing or SiteKind.Jetty or SiteKind.AshTree && RepeatGathering))
        {
            Message = $"{site.Name} returns in {Math.Ceiling(site.RespawnSeconds)}s.";
            return false;
        }
        var path = FindPath(Player, c => site == null ? c == cell : site.IsEnemy ? InAttackRange(c, site) : c.Distance(cell) == 1);
        if (path == null)
        {
            Message = "No route there. Choose open ground or a passage through the trees and walls.";
            return false;
        }
        Stop();
        foreach (var step in path) route.Enqueue(step);
        walkClock = actionClock = 0;
        target = site;
        Message = site == null ? "Walking to your destination." : $"Approaching {site.Name}.";
        return true;
    }

    private List<Cell>? FindPath(Cell start, Func<Cell, bool> arrived)
    {
        var frontier = new Queue<Cell>();
        var parents = new Dictionary<Cell, Cell> { [start] = start };
        frontier.Enqueue(start);
        while (frontier.TryDequeue(out var current))
        {
            if (arrived(current))
            {
                var path = new List<Cell>();
                while (current != start) { path.Add(current); current = parents[current]; }
                path.Reverse();
                return path;
            }
            foreach (var step in Steps)
            {
                var next = new Cell(current.X + step.X, current.Y + step.Y);
                if (!IsWalkable(next) || parents.ContainsKey(next)) continue;
                parents[next] = current;
                frontier.Enqueue(next);
            }
        }
        return null;
    }

    public void Update(double dt)
    {
        if (!double.IsFinite(dt) || dt < 0) throw new ArgumentOutOfRangeException(nameof(dt));
        if (HomecomingOpen) return;
        foreach (var site in Sites)
        {
            if (site.Kind == SiteKind.Guardian && GuardianDefeated || site.Kind == SiteKind.Hart && HartDefeated) continue;
            site.RespawnSeconds = Math.Max(0, site.RespawnSeconds - dt);
            if (site.RespawnSeconds == 0 && site.Hull <= 0) { site.Hull = site.MaxHull; site.Cell = site.Home; }
        }
        UpdateEnemies(dt);
        if (route.Count > 0)
        {
            walkClock += dt;
            while (walkClock >= 0.15 && route.TryDequeue(out var step))
            {
                if (!IsWalkable(step))
                {
                    var destination = target?.Cell ?? (route.Count > 0 ? route.Last() : step);
                    var queuedRecipe = pendingRecipe;
                    int queuedQuantity = pendingQuantity;
                    route.Clear();
                    if (!Select(destination)) Stop();
                    else { pendingRecipe = queuedRecipe; pendingQuantity = queuedQuantity; }
                    return;
                }
                Player = step;
                walkClock -= 0.15;
            }
            return;
        }
        if (pendingRecipe is { } recipe) StartBatch(recipe, pendingQuantity);
        if (BatchRecipe != null) { UpdateBatch(dt); return; }
        if (target == null) return;
        if (target.RespawnSeconds > 0)
        {
            if (target.Kind is not (SiteKind.Ore or SiteKind.RichOre or SiteKind.Fishing or SiteKind.Jetty or SiteKind.AshTree) || !RepeatGathering) Stop();
            return;
        }
        if (target.Hull <= 0 && target.IsEnemy) { Stop(); return; }
        if (target.IsEnemy ? !InAttackRange(Player, target) : Player.Distance(target.Cell) != 1) { Select(target.Cell); return; }
        actionClock += dt;
        double interval = ActionInterval;
        while (target != null && target.RespawnSeconds <= 0 && actionClock >= interval)
        {
            actionClock -= interval;
            Interact(target);
        }
    }

    private void Interact(WorldSite site)
    {
        switch (site.Kind)
        {
            case SiteKind.Nell:
            case SiteKind.Orin:
                Message = site.Name + ": Choose Talk or a request in the sidebar."; Stop(); break;
            case SiteKind.Landmark:
            case SiteKind.LostWay:
                Discover(site); break;
            case SiteKind.ReturnGate:
                Stop(); UseReturnGate(); break;
            case SiteKind.Jetty:
                if (!JettyBuilt || FishingLevel < 3) { Message = !JettyBuilt ? "Ask Bram to build Nell's jetty after the crossing. Needs Fishing level 3 to use." : "This jetty needs Fishing level 3. Train at an ordinary shoal."; Stop(); }
                else Fish(site);
                break;
            case SiteKind.Elder:
                Message = "Elin: Shall we discuss the village or your next task? Choose a dialogue option.";
                Stop();
                break;
            case SiteKind.TrailGate:
                if (QuestStage < 2) { Message = "Complete Copper Promise to explore the safe forest edge."; Stop(); }
                else Travel(Region.Forest, new Cell(2, 2));
                break;
            case SiteKind.TrailExit:
                Travel(Region.Village, new Cell(13, 13));
                break;
            case SiteKind.AshTree:
                CutWood(site);
                break;
            case SiteKind.Beacon:
                LightBeacon(site);
                break;
            case SiteKind.Fishing:
                Fish(site);
                break;
            case SiteKind.Campfire:
                Message = "Use Recipe arrows in the sidebar, then press Cook trout. Grilled trout needs 1 raw trout."; Stop();
                break;
            case SiteKind.RichOre:
                if (!HasTool) { Message = "This rich vein needs reinforced tools: forge them with 2 bars and 30 coins."; Stop(); break; }
                if (!CanCarry(Item.Ore, 3)) { Message = "Make room for three ore from this rich vein."; Stop(); break; }
                Give(Item.Ore); Give(Item.Ore); Give(Item.Ore); MiningXp += 20; site.RespawnSeconds = 6;
                Message = "+3 ore / +20 Mining XP. Reinforced tools break the rich vein.";
                if (!RepeatGathering) Stop();
                break;
            case SiteKind.Ore:
                if (!Give(Item.Ore)) { Message = "Your pack is full. Bank ore at the supply chest or visit the forge."; Stop(); break; }
                MiningXp += 10;
                site.RespawnSeconds = 4;
                Message = "+1 copper ore / +10 Mining XP. The seam will replenish.";
                if (!RepeatGathering) Stop();
                break;
            case SiteKind.Forge:
                Message = "Use Recipe arrows in the sidebar, then press Smelt or Forge. One bar needs 3 ore."; Stop(); break;
            case SiteKind.Bank:
                Hull = 20; Message = "Fully rested. Select an item to deposit or withdraw at the bank."; Stop(); break;
            case SiteKind.Merchant:
                Message = "Bram: Browse provisions, sell supplies, or ask about the crossing."; Stop(); break;
            case SiteKind.Entrance:
                if (RuinQuestStage == 0) { Message = "The halls are sealed. Complete the Copper Promise and speak to Elin again."; Stop(); }
                else Travel(Region.Ruins, new Cell(2, 7));
                break;
            case SiteKind.Exit:
                Travel(Region.Village, new Cell(22, 7));
                break;
            case SiteKind.Relic:
                if (!GuardianDefeated) Message = "The bell is bound to its guardian. Defeat it first.";
                else if (RuinQuestStage == 1) { RuinQuestStage = 2; Message = "Recovered the Sunken Bell! Return it to Elin. It takes no backpack space."; }
                else Message = "The altar is empty. The bell is safe.";
                Stop();
                break;
            case SiteKind.Mossling:
            case SiteKind.Sentinel:
            case SiteKind.Guardian:
            case SiteKind.Stalker:
            case SiteKind.Hart:
                if (site.Kind == SiteKind.Hart && BeaconCount < 3)
                { Message = "The Hart is protected by roots. Light all three beacons first."; Stop(); break; }
                int damage = AttackDamage;
                site.Hull -= damage;
                if (site.Hull <= 0)
                {
                    site.RespawnSeconds = site.Kind is SiteKind.Guardian or SiteKind.Hart ? 0 : site.Kind == SiteKind.Mossling ? 12 : 20;
                    if (site.Kind == SiteKind.Mossling) Kills++;
                    if (site.Kind == SiteKind.Hart) TrailQuestStage = 2;
                    if (site.Kind is SiteKind.Guardian or SiteKind.Hart)
                    {
                        if (site.Kind == SiteKind.Guardian) GuardianDefeated = true;
                        DangerCentre = null;
                        DangerSeconds = 0;
                    }
                    CombatXp += 15;
                    Coins += 5;
                    Message = $"{site.Name} defeated! +5 coins / +15 Combat XP.";
                    Stop();
                }
                else if (site.Kind == SiteKind.Mossling && Player.Distance(site.Cell) == 1)
                {
                    Message = $"You hit {damage}. Mossling strikes back. Click away to retreat; E eats a ration.";
                    Hurt(3);
                }
                else Message = $"You hit {site.Name} for {damage}. Keep an eye on the glowing tiles!";
                break;
        }
    }

    public void TalkToElin(bool advance)
    {
        if (!Beside(SiteKind.Elder)) { Message = "Meet Elin in the village square."; return; }
        Stop();
        if (!advance) { Message = TrailQuestStage == 3 ? "Elin: The roads are safe. Join our homecoming, or show me your completed fieldbook." : "Elin: " + MainQuest; return; }
                if (QuestStage == 0)
                {
                    QuestStage = 1;
                    Message = "Elin: Mine six copper ore, forge a sword, then defeat three mosslings. I'll pay 40 coins.";
                }
                else if (QuestStage == 1 && HasSword && Kills >= 3)
                {
                    QuestStage = 2;
                    Coins += 40;
                    Message = "Elin: The road is safe again! The Copper Promise complete. +40 coins.";
                }
                else if (QuestStage == 2 && RuinQuestStage == 0)
                {
                    RuinQuestStage = 1;
                    Message = "Elin: Recover the sunken bell from the eastern halls. Forge a shield from 2 bars and 20 coins; bring rations!";
                }
                else if (RuinQuestStage == 2)
                {
                    RuinQuestStage = 3;
                    Coins += 80;
                    CombatXp += 30;
                    Message = "Elin: Our bell rings again! The Sunken Bell complete. +80 coins / +30 Combat XP.";
                }
                else if (RiverQuestStage == 2) InteractTrailQuest();
                else if (RuinQuestStage == 3) InteractRiverQuest();
                else Message = QuestStage < 2 ? "Elin: Six ore make two bars; two bars make a sword. Clear three mosslings, then return."
                    : RuinQuestStage == 3 ? "Elin: You have given Emberbrook its voice back. Thank you, adventurer."
                    : "Elin: The halls lie east of the bridge. Dodge the guardian's glowing tiles, then take the bell.";
    }

    private void Travel(Region region, Cell position)
    {
        Stop();
        Region = region;
        Player = position;
        DangerCentre = null;
        DangerSeconds = 0;
        guardianClock = 2;
        foreach (var site in Sites) { site.MoveClock = site.AttackClock = 0; }
        Message = region == Region.Forest ? TrailQuestStage == 3 ? "The trail is restored. Explore the old paths, gather supplies, or visit the quiet spring." : "The Ashen Trail / chop ash trees, fuel three beacons, then face the Hart. Watch for wolves!" : region == Region.Ruins
            ? RuinQuestStage == 3 ? "The bell is home. The old halls still hold the bellmaker's story." : "The Sunken Halls / sentinels pursue nearby intruders. Leave glowing tiles before the guardian strikes!"
            : "Back in Emberbrook. Rest at the chest or speak to Elin.";
    }

    public bool IsDangerous(Cell cell) => DangerCentre is { } centre
        && (Region == Region.Forest
            ? cell.X == centre.X && Math.Abs(cell.Y - centre.Y) <= 2 || cell.Y == centre.Y && Math.Abs(cell.X - centre.X) <= 2
            : Math.Abs(cell.X - centre.X) <= 1 && Math.Abs(cell.Y - centre.Y) <= 1);

    private void UpdateEnemies(double dt)
    {
        if (Region == Region.Village) return;
        foreach (var enemy in ActiveSites.Where(s => s.Kind is SiteKind.Sentinel or SiteKind.Stalker && s.Hull > 0))
        {
            enemy.MoveClock += dt;
            enemy.AttackClock += dt;
            int distance = enemy.Cell.Distance(Player);
            if (distance == 1 && enemy.AttackClock >= 1.2)
            {
                enemy.AttackClock = 0;
                Message = $"{enemy.Name} strikes! E eats food; click away to retreat.";
                Hurt(enemy.Kind == SiteKind.Stalker ? 5 : 4);
                if (Region == Region.Village) return;
            }
            if (distance <= 1 || enemy.MoveClock < 0.45) continue;
            enemy.MoveClock = 0;
            bool chase = distance <= 6 && Player.Distance(enemy.Home) <= 7;
            var path = FindPath(enemy.Cell, c => chase ? c.Distance(Player) == 1 : c == enemy.Home);
            if (path is { Count: > 0 } && path[0] != Player) enemy.Cell = path[0];
        }
        if (Region == Region.Forest) { UpdateHart(dt); return; }
        var guardian = Sites.Single(s => s.Kind == SiteKind.Guardian);
        if (GuardianDefeated) return;
        if (DangerCentre != null)
        {
            DangerSeconds = Math.Max(0, DangerSeconds - dt);
            if (DangerSeconds > 0) return;
            bool hit = IsDangerous(Player);
            DangerCentre = null;
            if (hit) { Message = "The bell shockwave hits! Move out of the glowing tiles before they erupt."; Hurt(8); }
            else Message = "The shockwave misses. Strike while the guardian recovers!";
            guardianClock = guardian.Hull <= 30 ? 1.4 : 2.2;
        }
        else if (guardian.Cell.Distance(Player) <= 7)
        {
            guardianClock -= dt;
            if (guardianClock <= 0)
            {
                DangerCentre = Player;
                DangerSeconds = 1.4;
                Message = "The bell tolls! Move at least two tiles away from the glowing ground.";
            }
        }
    }

    private void Hurt(int damage)
    {
        Hull = Math.Max(0, Hull - Math.Max(1, damage - Protection));
        if (Hull > 0) return;
        Hull = 20;
        Coins = Math.Max(0, Coins - 5);
        foreach (var site in Sites.Where(s => s.IsEnemy && s.Hull > 0)) { site.Hull = site.MaxHull; site.Cell = site.Home; }
        Travel(Region.Village, new Cell(5, 7));
        Message = "Elin brought you home. Lost up to 5 coins; your items and skills are safe.";
    }

    public void ToggleRepeatGathering()
    {
        RepeatGathering = !RepeatGathering;
        Message = RepeatGathering ? "Repeat gathering ON. Click ore, a shoal, or an ash tree to gather until your pack fills. Click elsewhere to stop."
            : "Repeat gathering OFF. Each click gathers one ore, trout, or log.";
        if (!RepeatGathering && target?.Kind is SiteKind.Ore or SiteKind.RichOre or SiteKind.Fishing or SiteKind.Jetty or SiteKind.AshTree) Stop();
    }

    public void Eat()
    {
        if (Hull == 20) { Message = "You are already at full health."; return; }
        bool useMeal = Count(Item.Meal) > 0 && (20 - Hull > 8 || Count(Item.Ration) == 0);
        var food = Count(Item.SmokedTrout) > 0 && (20 - Hull <= 8 || Count(Item.Ration) + Count(Item.Meal) == 0) ? Item.SmokedTrout : useMeal ? Item.Meal : Item.Ration;
        if (Count(food) == 0) { Message = "No food. Buy rations from Bram or catch and grill trout."; return; }
        int healing = food == Item.Meal ? MealHealing : 8;
        Take(food, 1);
        int restored = Math.Min(20 - Hull, healing);
        Hull += restored;
        Message = $"{(food == Item.Meal ? "Grilled trout" : food == Item.SmokedTrout ? "Smoked trout" : "A warm ration")} restores {restored} health.";
    }

    public void WithdrawOre() => Withdraw(Item.Ore);

    private bool Give(Item item)
    {
        if (!CanCarry(item)) return false;
        inventory[item] = Count(item) + 1;
        return true;
    }

    private void Take(Item item, int count)
    {
        int remaining = Count(item) - count;
        if (remaining == 0) inventory.Remove(item);
        else inventory[item] = remaining;
    }

    public void Stop() { route.Clear(); target = null; walkClock = actionClock = batchClock = 0; BatchRecipe = null; BatchRemaining = 0; pendingRecipe = null; pendingQuantity = 0; }
    public void Notify(string message) => Message = message;

    public EmberbrookSave Capture() => new()
    {
        AcceptedRequest = AcceptedRequest, CompletedRequests = CompletedRequests, Discovered = Discovered, FloatShown = FloatShown,
        KeeperAwarded = KeeperAwarded, JettyBuilt = JettyBuilt, ReturnGateBuilt = ReturnGateBuilt, HomecomingSeen = HomecomingSeen,
        BridgeStage = BridgeStage, HasTool = HasTool, HasSpear = HasSpear, SpearEquipped = SpearEquipped,
        SmokedTrout = Count(Item.SmokedTrout), BankSmokedTrout = BankCount(Item.SmokedTrout),
        WoodcuttingXp = WoodcuttingXp, Logs = Count(Item.Log), TrailQuestStage = TrailQuestStage, LitBeacons = LitBeacons,
        BankBars = BankCount(Item.Bar), BankRations = BankCount(Item.Ration), BankTrout = BankCount(Item.Trout), BankMeals = BankCount(Item.Meal), BankLogs = BankCount(Item.Log),
        FishingXp = FishingXp, CookingXp = CookingXp, RiverQuestStage = RiverQuestStage, HasPearl = HasPearl,
        Charm = Charm, Trout = Count(Item.Trout), Meals = Count(Item.Meal),
        Region = Region, RuinQuestStage = RuinQuestStage, HasShield = HasShield, GuardianDefeated = GuardianDefeated,
        X = Player.X, Y = Player.Y, Hull = Hull, Coins = Coins, MiningXp = MiningXp,
        CombatXp = CombatXp, QuestStage = QuestStage, Kills = Kills, HasSword = HasSword,
        BankedOre = BankedOre, Ore = Count(Item.Ore), Bars = Count(Item.Bar), Rations = Count(Item.Ration)
    };

    public void Restore(EmberbrookSave save)
    {
        save.Validate();
        var position = new Cell(save.X, save.Y);
        var validator = new EmberbrookWorld { BridgeStage = save.BridgeStage, TrailQuestStage = save.TrailQuestStage, Discovered = save.Discovered };
        if (!validator.IsWalkable(position, save.Region, true)) throw new System.IO.InvalidDataException("Saved position is not walkable.");
        Stop(); HomecomingOpen = false;
        AcceptedRequest = save.AcceptedRequest; CompletedRequests = save.CompletedRequests; Discovered = save.Discovered;
        FloatShown = save.FloatShown; KeeperAwarded = save.KeeperAwarded; JettyBuilt = save.JettyBuilt;
        ReturnGateBuilt = save.ReturnGateBuilt; HomecomingSeen = save.HomecomingSeen;
        BridgeStage = save.BridgeStage; HasTool = save.HasTool; HasSpear = save.HasSpear; SpearEquipped = save.SpearEquipped;
        WoodcuttingXp = save.WoodcuttingXp;
        TrailQuestStage = save.TrailQuestStage;
        LitBeacons = save.LitBeacons;
        bank.Clear();
        Store(Item.SmokedTrout, save.BankSmokedTrout);
        Store(Item.Bar, save.BankBars);
        Store(Item.Ration, save.BankRations);
        Store(Item.Trout, save.BankTrout);
        Store(Item.Meal, save.BankMeals);
        Store(Item.Log, save.BankLogs);
        FishingXp = save.FishingXp;
        CookingXp = save.CookingXp;
        RiverQuestStage = save.RiverQuestStage;
        HasPearl = save.HasPearl;
        Charm = save.Charm;
        Region = save.Region;
        RuinQuestStage = save.RuinQuestStage;
        HasShield = save.HasShield;
        GuardianDefeated = save.GuardianDefeated;
        DangerCentre = null;
        DangerSeconds = 0;
        guardianClock = 2;
        Player = position;
        Hull = save.Hull;
        Coins = save.Coins;
        MiningXp = save.MiningXp;
        CombatXp = save.CombatXp;
        QuestStage = save.QuestStage;
        Kills = save.Kills;
        HasSword = save.HasSword;
        BankedOre = save.BankedOre;
        inventory.Clear();
        if (save.SmokedTrout > 0) inventory[Item.SmokedTrout] = save.SmokedTrout;
        if (save.Logs > 0) inventory[Item.Log] = save.Logs;
        if (save.Trout > 0) inventory[Item.Trout] = save.Trout;
        if (save.Meals > 0) inventory[Item.Meal] = save.Meals;
        if (save.Ore > 0) inventory[Item.Ore] = save.Ore;
        if (save.Bars > 0) inventory[Item.Bar] = save.Bars;
        if (save.Rations > 0) inventory[Item.Ration] = save.Rations;
        foreach (var site in Sites)
        {
            site.Hull = site.Kind == SiteKind.Guardian && GuardianDefeated || site.Kind == SiteKind.Hart && HartDefeated ? 0 : site.MaxHull;
            site.Cell = site.Home;
            if (SitePresent(site) && site.Region == Region && site.Cell == Player)
                site.Cell = Steps.Select(step => new Cell(Player.X + step.X, Player.Y + step.Y)).First(IsWalkable);
            site.RespawnSeconds = site.MoveClock = site.AttackClock = 0;
        }
        Message = "Progress loaded. Welcome back to Emberbrook.";
    }
}
