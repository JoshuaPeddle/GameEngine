using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using GameEngine.Core;
using GameEngine.Core.Components;
using GameEngine.Core.Systems;
using GameEngine.Demo.Emberbrook;
using SkiaSharp;

namespace GameEngine.Demo;

public sealed partial class SceneEmberbrook : Scene
{
    private sealed class TextBlock(CText[] lines)
    {
        public string Text
        {
            set
            {
                string[] content = value.Split('\n');
                for (int i = 0; i < lines.Length; i++) lines[i].Text = i < content.Length ? content[i] : "";
            }
        }
    }

    public const int TileSize = 40;
    public static readonly Vec2 MapOrigin = new(24, 110);
    private readonly IEmberbrookSaveStore saveStore;
    private readonly Dictionary<string, Entity> siteSprites = [];
    private readonly Dictionary<string, CText> siteLabels = [];
    private EntityGroup mapEntities = null!;
    private readonly List<Entity> dangerTiles = [];
    private Region renderedRegion;
    private bool buildingMap;
    private CText regionTitle = null!;
    private CText questTitle = null!;
    private EntityManager entities = null!;
    private Assets assets = null!;
    private Entity player = null!;
    private Entity marker = null!;
    private TextBlock stats = null!;
    private TextBlock equipment = null!;
    private TextBlock quest = null!;
    private TextBlock message = null!;
    private CText activity = null!;
    private CText hover = null!;

    public static Func<IEmberbrookSaveStore> SaveStoreFactory { get; set; } = EmberbrookFileSaveStore.Default;
    public SceneEmberbrook() : this(SaveStoreFactory(), true) { }
    public SceneEmberbrook(IEmberbrookSaveStore saveStore, bool showWelcome = false) { this.saveStore = saveStore; welcome = showWelcome; }
    public EmberbrookWorld World { get; } = new();
    public override int VirtualWidth => 1280;
    public override int VirtualHeight => 800;

    public override void Unload()
    {
        mapEntities?.Dispose();
        workbenchUi?.Dispose();
    }

    public override void Initialize(EntityManager entityManager, InputManager inputManager,
        AudioSystem? audioPlayer, Action<Scene?> ResetScene)
    {
        audio = audioPlayer;
        World.Crafted += () => PlayCue("Craft");
        entities = entityManager;
        mapEntities = entities.CreateGroup();
        assets = LoadAssets("assets.json");
        Sprite("emberBackdrop", "panel", new Vec2(640, 400), new Vec2(1280, 800), -100);
        BuildMap();
        player = Sprite("emberPlayer", "player", Position(World.Player), new Vec2(40, 40), 20);
        player.AddComponent<CInput>();
        marker = Sprite("emberDestination", "target", Position(World.Player), new Vec2(40, 40), 19);
        Label("emberTitle", "EMBERBROOK", new Vec2(145, 44), 32, "#e9d4a0");
        Label("emberSubtitle", "A village, a bell, and the roads beyond", new Vec2(29, 70), 15, "#a3b18a");
        regionTitle = Label("emberRegions", "VILLAGE                              THE BROOK                              COPPER HILLS / MOSSLING GROVE",
            new Vec2(34, 100), 13, "#c6c2a0");
        Label("emberControls", "CLICK interact   X stop   E eat   M gather   S save   L load   Q menu", new Vec2(570, 43), 14, "#c6c2a0");
        hover = Label("emberHover", "Click Elin to start your quest", new Vec2(570, 20), 15, "#e9d4a0");
        Sprite("emberSidebar", "paper", new Vec2(1128, 410), new Vec2(256, 600), -10);
        Label("emberCharacterTitle", "ADVENTURER", new Vec2(1016, 142), 19, "#e9d4a0");
        InitializePanels();
        questTitle = Label("emberQuestTitle", "THE COPPER PROMISE", new Vec2(1016, 629), 16, "#e9d4a0");
        quest = Lines("emberQuest", 4, 15, new Vec2(1016, 649), 13, "#e3e4cb");
        activity = Label("emberActivity", "", new Vec2(29, 740), 15, "#d4b678");
        message = Lines("emberMessage", 2, 20, new Vec2(29, 764), 16, "#e3e4cb");
        inputManager.BindPointerAction(Pointer.PointerEventType.Press, e => Click(e.Position));
        inputManager.BindPointerAction(Pointer.PointerEventType.Move, e => Hover(e.Position));
        Bind(inputManager, GeKeys.X, "EmberStop", World.CancelActivity);
        Bind(inputManager, GeKeys.T, "EmberCharm", World.ToggleCharm);
        Bind(inputManager, GeKeys.B, "EmberSell", World.SellFish);
        Bind(inputManager, GeKeys.M, "EmberRepeat", World.ToggleRepeatGathering);
        Bind(inputManager, GeKeys.E, "EmberEat", World.Eat);
        Bind(inputManager, GeKeys.S, "EmberSave", Save);
        Bind(inputManager, GeKeys.L, "EmberLoad", Load);
        Bind(inputManager, GeKeys.Q, "EmberMenu", () => ResetScene(new SceneMenu()));
        for (int i = 0; i < 9; i++)
        {
            var danger = Sprite("emberDanger", "danger", Vec2.Zero, new Vec2(40, 40), -5);
            danger.GetComponent<CAnimation>().ShouldDraw = false;
            dangerTiles.Add(danger);
        }
        marker.GetComponent<CAnimation>().ShouldDraw = false;
        InitializeFeedback();
        InitializeEffects();
        UpdateHud();
    }

    private void BuildMap()
    {
        mapEntities.Clear();
        siteSprites.Clear();
        siteLabels.Clear();
        buildingMap = true;
        renderedRegion = World.Region;
        renderedBridge = World.BridgeStage; renderedTrail = World.TrailQuestStage; renderedRuin = World.RuinQuestStage; renderedFeast = World.RiverQuestStage;
        for (int y = 0; y < EmberbrookWorld.Height; y++)
        for (int x = 0; x < EmberbrookWorld.Width; x++)
        {
            var cell = new Cell(x, y);
            bool bridge = World.Region == Region.Village && x is 11 or 12 && (y is 6 or 7 || World.BridgeStage == 2 && y == 4);
            bool road = World.Region == Region.Forest
                ? x == 2 || y == 8 || x == 18 && y is >= 3 and <= 11
                : y is 6 or 7 || x is >= 3 and <= 6 && y is >= 3 and <= 10;
            string tile = World.Region == Region.Ruins ? "floor" : World.IsWater(cell) ? "water" : bridge ? "bridge" : road ? "path" : "grass" + (x * 3 + y * 7) % 3;
            Sprite("emberTile", tile, Position(cell), new Vec2(40, 40), -20);
            bool boundary = x == 0 || y == 0 || x == EmberbrookWorld.Width - 1 || y == EmberbrookWorld.Height - 1;
            if ((World.IsTree(cell) || World.IsWall(cell) || World.Region == Region.Forest && World.TrailQuestStage == 0 && x == 7 || boundary) && !World.ActiveSites.Any(site => site.Cell == cell))
                Sprite("emberTree", World.Region == Region.Ruins ? "wall" : "tree", Position(cell), new Vec2(40, 40), y);
        }
        if (World.Region == Region.Forest && World.TrailQuestStage == 0)
            Label("emberTrailSeal", "Deep trail sealed / ask Elin", Position(new Cell(7, 7)), 12, "#fff0c3", SKTextAlign.Center);
        if (World.Region == Region.Village)
        {
            if (World.BridgeStage < 2) Sprite("emberBrokenBridge", "brokenbridge", Position(new Cell(11, 4)) + new Vec2(20, 0), new Vec2(80, 40), 4);
            if (World.RuinQuestStage == 3) Sprite("emberRestoredBell", "relic", Position(new Cell(5, 2)), new Vec2(40, 40), 4);
            if (World.RiverQuestStage == 2) Sprite("emberFeast", "feast", Position(new Cell(5, 10)), new Vec2(40, 40), 11);
            Sprite("emberCottage", "cottage", Position(new Cell(3, 2)) + new Vec2(0, 20), new Vec2(120, 80), 3);
            Sprite("emberCottage", "cottage", Position(new Cell(4, 11)) + new Vec2(0, 20), new Vec2(120, 80), 12);
        }
        foreach (var site in World.ActiveSites)
        {
            string image = site.Kind switch
            {
                SiteKind.Elder => "elder", SiteKind.Ore or SiteKind.RichOre => "ore", SiteKind.Forge => "forge",
                SiteKind.Bank => "bank", SiteKind.Merchant => "merchant",
                SiteKind.Entrance or SiteKind.Exit => "gate", SiteKind.Sentinel => "sentinel",
                SiteKind.Guardian => "guardian", SiteKind.Relic => "relic",
                SiteKind.Fishing => "fishing", SiteKind.Campfire => "campfire",
                SiteKind.TrailGate or SiteKind.TrailExit => "gate", SiteKind.AshTree => "ash",
                SiteKind.Beacon => "beacon", SiteKind.Stalker => "wolf", SiteKind.Hart => "hart", _ => VillageSiteImage(site)
            };
            siteSprites[site.Id] = Sprite("emberSite:" + site.Id, image, Position(site.Cell), new Vec2(40, 40), site.Cell.Y + 1);
            siteLabels[site.Id] = Label("emberLabel:" + site.Id, "", Position(site.Cell) + new Vec2(0, -24), 12, "#fff0c3", SKTextAlign.Center);
        }
        BuildVillageChanges();
        BuildAmbience();
        buildingMap = false;
    }

    public override void Update(EntityManager entityManager, SystemContainer systems, double deltaSeconds)
    {
        if (welcome) { UpdateHud(); return; }
        actionSite = World.Target;
        Cell before = World.Player;
        soundCooldown = Math.Max(0, soundCooldown - deltaSeconds);
        World.Update(deltaSeconds);
        if (renderedRegion != World.Region || renderedBridge != World.BridgeStage || renderedTrail != World.TrailQuestStage || renderedRuin != World.RuinQuestStage || renderedFeast != World.RiverQuestStage || renderedVillage != World.VillageProgress)
        {
            BuildMap();
            hover.Text = World.Region == Region.Forest ? World.TrailQuestStage == 3 ? "Trail restored / explore the paths and quiet spring" : World.TrailQuestStage == 0 ? "Safe forest edge / cut ash for Bram’s crossing" : "Light three beacons / dodge the Hart’s roots"
                : World.Region == Region.Ruins ? World.RuinQuestStage == 3 ? "Bell restored / inspect the old halls" : "Find the bell / dodge the guardian’s shockwaves" : "Rest at the chest / speak to Elin";
        }
        var transform = player.GetComponent<CTransform>();
        Vec2 destination = Position(World.Player);
        transform.Position = before.Distance(World.Player) > 2 ? destination
            : transform.Position + (destination - transform.Position) * Math.Min(1, deltaSeconds * 24);
        transform.Layer = World.Player.Y + 2;
        marker.GetComponent<CAnimation>().ShouldDraw = World.Destination != null;
        if (World.Destination is { } cell) marker.GetComponent<CTransform>().Position = Position(cell);
        foreach (var site in World.ActiveSites)
        {
            var sprite = siteSprites[site.Id];
            var siteTransform = sprite.GetComponent<CTransform>();
            siteTransform.Position += (Position(site.Cell) - siteTransform.Position) * Math.Min(1, deltaSeconds * 18);
            siteTransform.Layer = site.Cell.Y + 1;
            if (site.Kind is SiteKind.Guardian or SiteKind.Hart)
                sprite.GetComponent<CAnimation>().Animation = Drawing((site.Kind == SiteKind.Hart ? "hart" : "guardian")
                    + (World.DangerCentre != null ? "cast" : ""), new Vec2(40, 40));
            else if (site.Kind is SiteKind.Stalker or SiteKind.Sentinel)
                sprite.GetComponent<CAnimation>().Animation = Drawing((site.Kind == SiteKind.Stalker ? "wolf" : "sentinel")
                    + (siteTransform.Position.DistanceTo(Position(site.Cell)) > 0.5 ? "walk" : ""), new Vec2(40, 40));
            var labelEntity = entities.GetEntityWithTag("emberLabel:" + site.Id);
            if (labelEntity != null) labelEntity.GetComponent<CTransform>().Position = siteTransform.Position + new Vec2(0, site.Cell.Y == 0 ? 29 : -24);
            if (site.Kind == SiteKind.Beacon)
                sprite.GetComponent<CAnimation>().Animation = Drawing(World.BeaconIsLit(site) ? "campfire" : "beacon", new Vec2(40, 40));
            double scale = site.RespawnSeconds > 0 ? 0.55 : site.Kind is SiteKind.Guardian or SiteKind.Hart ? 1.4 : 1;
            sprite.GetComponent<CTransform>().Scale = new Vec2(scale, scale);
            sprite.GetComponent<CAnimation>().ShouldDraw = !site.IsEnemy || site.Hull > 0;
            if (site.Kind == SiteKind.Relic && World.RuinQuestStage >= 2) sprite.GetComponent<CAnimation>().ShouldDraw = false;
            siteLabels[site.Id].Text = site.RespawnSeconds > 0 ? $"{Math.Ceiling(site.RespawnSeconds)}s"
                : site.IsEnemy ? site.Hull <= 0 ? "Cleared" : site.Kind == SiteKind.Hart && World.BeaconCount < 3 ? "ROOTBOUND" : $"{site.Hull}/{site.MaxHull} HP" : site.Kind == SiteKind.RichOre ? World.HasTool ? "Rich copper" : "Needs tools" : site.Kind == SiteKind.Ore ? "Copper"
                : site.Kind == SiteKind.Elder ? "Elin !" : site.Kind == SiteKind.Merchant ? "Bram" : site.Kind == SiteKind.Fishing ? "Trout" : site.Kind == SiteKind.Campfire ? "Cook"
                : site.Kind == SiteKind.Beacon ? World.BeaconIsLit(site) ? "Lit" : "Beacon (2 logs)" : site.Kind == SiteKind.AshTree ? "Ash tree"
                : site.Kind == SiteKind.TrailGate ? "Ashen Trail" : site.Kind == SiteKind.TrailExit ? "Village"
                : site.Kind == SiteKind.Entrance ? "Sunken halls" : site.Kind == SiteKind.Exit ? "Village"
                : site.Kind == SiteKind.Relic ? World.RuinQuestStage >= 2 ? "Empty altar" : "Sunken bell" : site.Kind == SiteKind.Jetty ? World.JettyBuilt ? "Jetty / Fishing 3" : "Build with Bram"
                : site.Kind == SiteKind.ReturnGate ? World.ReturnGateBuilt ? "Return home" : "Ruined gate"
                : site.Kind is SiteKind.Landmark or SiteKind.LostWay ? "Inspect" : site.Name;
        }
        for (int i = 0; i < dangerTiles.Count; i++)
        {
            var tile = dangerTiles[i];
            tile.GetComponent<CAnimation>().ShouldDraw = World.DangerCentre != null;
            if (World.DangerCentre is { } centre)
                tile.GetComponent<CTransform>().Position = Position(World.Region != Region.Forest ? new Cell(centre.X + i % 3 - 1, centre.Y + i / 3 - 1)
                    : i < 5 ? new Cell(centre.X, centre.Y + i - 2)
                    : new Cell(centre.X + (i < 7 ? i - 7 : i - 6), centre.Y));
        }
        AnimatePlayer(before, deltaSeconds);
        UpdateFeedback(deltaSeconds);
        UpdateEffects(deltaSeconds);
        UpdateAmbience(deltaSeconds);
        if (savedMilestone != Milestone && World.Region == Region.Village && World.DangerCentre == null)
        { savedMilestone = Milestone; SaveMilestone(); }
        UpdateHud();
    }

    private void Click(Vec2 point)
    {
        if (workbenchUi.TryPress(point)) { UpdatePanels(); return; }
        foreach (var button in panelButtons)
            if (button.Visible() && button.Bounds.Contains((float)point.X, (float)point.Y)) { button.Action(); PlayCue("Select"); return; }
        if (welcome || World.HomecomingOpen) return;
        if (TryCell(point, out var cell))
        {
            var station = World.ActiveSites.FirstOrDefault(site => site.Cell == cell && site.Kind is SiteKind.Forge or SiteKind.Campfire);
            if (station != null)
            {
                page = SidebarPage.Backpack;
                World.UseWorkstation(cell, RecipeAt(station.Kind).Id, quantity);
            }
            else World.Select(cell);
            PlayCue("Select");
        }
    }

    private void Hover(Vec2 point)
    {
        if (!TryCell(point, out var cell)) { hover.Text = "E eat  /  M repeat gathering  /  X stop"; return; }
        var site = World.ActiveSites.FirstOrDefault(s => s.Cell == cell);
        hover.Text = site == null ? World.IsWalkable(cell) ? "Walk here" : "Impassable terrain" : site.Kind switch
        {
            SiteKind.Elder => "Talk to " + site.Name, SiteKind.RichOre => "Rich vein / reinforced tools / 3 ore", SiteKind.Ore => "Mine copper / replenishes in 4s",
            SiteKind.Forge => "Click to make " + RecipeAt(SiteKind.Forge).Name + " / " + RecipeAt(SiteKind.Forge).Cost, SiteKind.Bank => "Rest / bank any item using the sidebar",
            SiteKind.Nell or SiteKind.Orin => "Talk to " + site.Name + " / village requests",
            SiteKind.Landmark => "Inspect " + site.Name + " / fieldbook",
            SiteKind.LostWay => "Clear the Lost Way / Woodcutting 3 / deep trail",
            SiteKind.Jetty => World.JettyBuilt ? "Fish at the jetty / Fishing 3 / two trout" : "Ruined jetty / ask Bram about construction",
            SiteKind.ReturnGate => World.ReturnGateBuilt ? "Return to Emberbrook / idle and safe" : "Restore with Bram after the Ashen Trail",
            SiteKind.Merchant => "Bram: trade / talk / construction projects", SiteKind.Entrance => "Explore the Sunken Halls",
            SiteKind.Exit => "Return to Emberbrook", SiteKind.Relic => "Recover the sunken bell",
            SiteKind.TrailGate => "Explore the Ashen Trail", SiteKind.TrailExit => "Return to the village",
            SiteKind.AshTree => "Cut ash logs / M repeats gathering", SiteKind.Beacon => "Light beacon / 2 ash logs",
            SiteKind.Fishing => "Catch trout / M repeats gathering", SiteKind.Campfire => "Click to make " + RecipeAt(SiteKind.Campfire).Name + " / " + RecipeAt(SiteKind.Campfire).Cost,
            _ => "Attack " + site.Name + " / click away to retreat"
        };
    }

    private static bool TryCell(Vec2 point, out Cell cell)
    {
        cell = new Cell((int)Math.Floor((point.X - MapOrigin.X) / TileSize), (int)Math.Floor((point.Y - MapOrigin.Y) / TileSize));
        return cell.X >= 0 && cell.Y >= 0 && cell.X < EmberbrookWorld.Width && cell.Y < EmberbrookWorld.Height;
    }

    private void Save()
    {
        try { saveStore.Write(World.Capture()); saveMessage = "Saved successfully"; World.Notify("Progress saved. Press L to resume this save, including after restarting the game."); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or NotSupportedException)
        { saveMessage = "SAVE FAILED / press S to retry"; World.Notify("Could not save: " + ex.Message); }
    }

    private void Load()
    {
        try
        {
            World.Restore(saveStore.Read());
            player.GetComponent<CTransform>().Position = Position(World.Player);
            ResetFeedback();
            welcome = false; savedMilestone = Milestone; saveMessage = "Adventure loaded";
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or NotSupportedException)
        { saveMessage = "Load failed / start new or retry"; World.Notify("Could not load: " + ex.Message); }
    }

    private void Bind(InputManager input, GeKeys key, string name, Action action)
    {
        input.AddAction(key, name);
        input.ActionMapper.MapActionToComponent<CInput>(name, player, (_, active) => { if (active && !World.HomecomingOpen && (!welcome || key is GeKeys.L or GeKeys.Q)) action(); }, oneShot: true);
    }

    private Animation Drawing(string name, Vec2 size)
    {
        return assets.GetAnimationForFrame("Ember" + name, size);
    }

    private Entity Sprite(string tag, string image, Vec2 position, Vec2 size, int layer)
    {
        var entity = buildingMap ? mapEntities.CreateEntity(tag) : entities.CreateEntity(tag);
        entity.AddComponent(new CTransform(position) { Layer = layer });
        var animation = new CAnimation(Drawing(image, size));
        if (buildingMap) animation.Update((Math.Abs(position.X * 7 + position.Y * 13) % 17) * 0.09);
        entity.AddComponent(animation);
        return entity;
    }

    private CText Label(string tag, string value, Vec2 position, int size, string color, SKTextAlign align = SKTextAlign.Left, int layer = 100)
    {
        var entity = buildingMap ? mapEntities.CreateEntity(tag) : entities.CreateEntity(tag);
        entity.AddComponent(new CTransform(position) { Layer = layer });
        var text = new CText(value, size) { TextAlign = align };
        text.Paint.Color = SKColor.Parse(color);
        entity.AddComponent(text);
        return text;
    }

    private TextBlock Lines(string tag, int count, int spacing, Vec2 position, int size, string color)
    {
        var lines = new CText[count];
        for (int i = 0; i < count; i++)
            lines[i] = Label(tag + i, "", position + new Vec2(0, i * spacing), size, color);
        return new TextBlock(lines);
    }

    private void UpdateHud()
    {
        stats.Text = $"Health {World.Hull}/20   Coins {World.Coins}";
        equipment.Text = (World.SpearEquipped ? "Ash spear / reach 2 / no shield" : World.HasSword ? "Copper sword" + (World.HasShield ? " + shield" : "") : "Worn knife")
            + $"\nATK {World.AttackDamage} DEF {World.Protection} / {World.Charm}";
        UpdatePanels();
        questTitle.Text = World.TrackBridge ? "THE BROKEN CROSSING" : World.QuestTitle;
        regionTitle.Text = World.Region == Region.Forest ? "THE ASHEN TRAIL  /  BEACON GROVE                     ASHWOOD                         THE HART'S THICKET" : World.Region == Region.Ruins ? "THE SUNKEN HALLS  /  ENTRY                     SENTINEL COURT                         THE BELL CHAMBER"
            : "VILLAGE                              THE BROOK                              COPPER HILLS / MOSSLING GROVE";
        quest.Text = Wrap(World.Quest, 32);
        activity.Paint.Color = SKColor.Parse(World.DangerCentre != null ? "#ffad7a" : World.Hull <= 6 ? "#ff9a91" : "#d4b678");
        activity.Text = World.DangerCentre != null ? $"{(World.Region == Region.Forest ? "ROOTS" : "SHOCKWAVE")} IN {World.DangerSeconds:0.0}s  /  LEAVE THE GLOWING TILES!"
            : $"{World.Activity.ToUpperInvariant()}{(World.Target is { } target ? " / " + target.Name : "")}  /  Repeat gathering {(World.RepeatGathering ? "ON" : "OFF")} [M]  /  Click to move or retreat. Save before leaving.";
        message.Text = Wrap(World.Message, 135);
    }

    private static string Wrap(string text, int width)
    {
        var lines = new List<string>();
        string line = "";
        foreach (var word in text.Split(' '))
        {
            if (line.Length + word.Length + 1 > width) { lines.Add(line); line = ""; }
            line += (line.Length == 0 ? "" : " ") + word;
        }
        lines.Add(line);
        return string.Join("\n", lines);
    }

    public static Vec2 Position(Cell cell) => MapOrigin + new Vec2(cell.X * TileSize + 20, cell.Y * TileSize + 20);
}
