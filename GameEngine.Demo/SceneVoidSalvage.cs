using System;
using System.Collections.Generic;
using System.Linq;
using GameEngine.Core;
using GameEngine.Core.Components;
using GameEngine.Core.Systems;
using SkiaSharp;

namespace GameEngine.Demo;

public enum SalvagePhase { Ready, Flight, Upgrade, Victory, Defeat }

public sealed class SceneVoidSalvage : Scene
{
    private sealed class Raider : Component
    {
        public double Age;
        public double Warning = 1.2;
    }

    private static readonly Vec2 Dock = new(165, 400);
    private readonly List<Entity> cargo = [];
    private Assets assets = null!;
    private EntityManager entities = null!;
    private Entity player = null!;
    private Entity shield = null!;
    private CText status = null!;
    private CText message = null!;
    private CText detail = null!;
    private Action<Scene?> reset = null!;
    private AudioSystem? audio;
    private Vec2 direction = new(1, 0);
    private double spawnTimer;
    private double dashTime;
    private double grace;
    private int spawnIndex;
    private int engineUpgrades;
    private int capacity = 3;
    private int maxHull = 5;

    public override int VirtualWidth => 1280;
    public override int VirtualHeight => 800;
    public SalvagePhase Phase { get; private set; }
    public int Sortie { get; private set; }
    public int Delivered { get; private set; }
    public int Quota => 5 + Sortie;
    public int Cargo => cargo.Count;
    public int Capacity => capacity;
    public int Hull { get; private set; } = 5;
    public int Score { get; private set; }
    public double RemainingSeconds { get; private set; }
    public double DashCooldown { get; private set; }

    public override void Initialize(EntityManager entityManager, InputManager inputManager,
        AudioSystem? audioPlayer, Action<Scene?> ResetScene)
    {
        entities = entityManager;
        assets = new Assets("assets.json", AssetSource);
        reset = ResetScene;
        audio = audioPlayer;
        Sprite("salvageArena", "Siegearena", new Vec2(640, 400), 1280, 800, -100);
        Sprite("salvageDock", "Siegering", Dock, 150, 150, 1);
        Sprite("salvageCarrier", "Bastioncore", Dock, 64, 64, 2);
        Label("salvageDockLabel", "CARRIER / BANK CARGO", new Vec2(64, 505), 15, "#75FFF1");
        player = Sprite("salvagePlayer", "Siegepilot", Dock, 32, 32, 10);
        player.AddComponent<CInput>();
        player.AddComponent(new CBoundingBox(new Vec2(32, 32), false, false));
        player.GetComponent<CTransform>().Position -= new Vec2(16, 16);
        player.AddComponent(new CMovement(0, 1000));
        shield = Sprite("salvageShield", "Siegering", Dock, 65, 65, 11);
        Label("salvageTitle", "VOID / SALVAGE", new Vec2(48, 60), 30, "#75FFF1");
        Label("salvageSubtitle", "HEAVY CARGO. HOSTILE SPACE.", new Vec2(49, 84), 13, "#A3BBCD");
        status = Label("salvageStatus", "", new Vec2(390, 55), 19, "#E3F5FF");
        message = Label("salvageMessage", "", new Vec2(48, 733), 22, "#75FFF1");
        detail = Label("salvageDetail", "", new Vec2(48, 765), 15, "#A3BBCD");
        MoveBinding(inputManager, GeKeys.W, GeKeys.Up, "SalvageUp", (c, v) => c.Up = v);
        MoveBinding(inputManager, GeKeys.S, GeKeys.Down, "SalvageDown", (c, v) => c.Down = v);
        MoveBinding(inputManager, GeKeys.A, GeKeys.Left, "SalvageLeft", (c, v) => c.Left = v);
        MoveBinding(inputManager, GeKeys.D, GeKeys.Right, "SalvageRight", (c, v) => c.Right = v);
        Bind(inputManager, GeKeys.Space, "SalvageDash", () =>
        {
            if (Phase == SalvagePhase.Ready) Launch();
            else if (Phase == SalvagePhase.Flight && DashCooldown <= 0)
            {
                dashTime = 0.22;
                grace = Math.Max(grace, 0.3);
                DashCooldown = 2.4;
            }
        });
        Bind(inputManager, GeKeys.F, "SalvageHold", () => Upgrade(0));
        Bind(inputManager, GeKeys.G, "SalvageEngine", () => Upgrade(1));
        Bind(inputManager, GeKeys.H, "SalvageArmor", () => Upgrade(2));
        Bind(inputManager, GeKeys.R, "SalvageRestart", () => reset(new SceneVoidSalvage()));
        Bind(inputManager, GeKeys.Q, "SalvageMenu", () => reset(new SceneMenu()));
        UpdateHud();
    }

    private void MoveBinding(InputManager input, GeKeys key, GeKeys alternate, string name, Action<CInput, bool> action)
    {
        input.AddAction(key, name);
        input.AddAction(alternate, name);
        input.ActionMapper.MapActionToComponent(name, player, action);
    }

    private void Bind(InputManager input, GeKeys key, string name, Action action)
    {
        input.AddAction(key, name);
        input.ActionMapper.MapActionToComponent<CInput>(name, player, (_, active) => { if (active) action(); }, oneShot: true);
    }

    private Entity Sprite(string tag, string image, Vec2 position, double width, double height, int layer)
    {
        var entity = entities.CreateEntity(tag);
        entity.AddComponent(new CTransform(position) { Layer = layer });
        entity.AddComponent(new CAnimation(assets.GetAnimation(image, new Vec2(width, height))));
        return entity;
    }

    private CText Label(string tag, string value, Vec2 position, int size, string color)
    {
        var entity = entities.CreateEntity(tag);
        entity.AddComponent(new CTransform(position) { Layer = 100 });
        var text = new CText(value, size) { TextAlign = SKTextAlign.Left };
        text.Paint.Color = SKColor.Parse(color);
        entity.AddComponent(text);
        return text;
    }

    private void Launch()
    {
        Sortie++;
        Delivered = 0;
        RemainingSeconds = 65;
        DashCooldown = dashTime = 0;
        grace = 2;
        spawnTimer = 2;
        spawnIndex = 0;
        Phase = SalvagePhase.Flight;
        player.GetComponent<CTransform>().Position = Dock - new Vec2(16, 16);
        for (int i = 0; i < Quota + 3; i++)
        {
            double x = 510 + i % 3 * 265;
            double y = 205 + i / 3 * 125;
            Sprite("salvageCore", "Siegescrap", new Vec2(x, y), 28, 28, 5);
        }
        UpdateHud();
    }

    public override void Update(EntityManager entityManager, SystemContainer systems, double deltaSeconds)
    {
        var transform = player.GetComponent<CTransform>();
        if (Phase != SalvagePhase.Flight)
        {
            transform.Velocity = Vec2.Zero;
            shield.GetComponent<CAnimation>().ShouldDraw = false;
            return;
        }
        RemainingSeconds = Math.Max(0, RemainingSeconds - deltaSeconds);
        DashCooldown = Math.Max(0, DashCooldown - deltaSeconds);
        grace = Math.Max(0, grace - deltaSeconds);
        var input = player.GetComponent<CInput>();
        var move = new Vec2((input.Right ? 1 : 0) - (input.Left ? 1 : 0), (input.Down ? 1 : 0) - (input.Up ? 1 : 0));
        if (move.LengthSquared() > 0) direction = move.Normalize();
        transform.Position = new Vec2(Math.Clamp(transform.Position.X, 50, 1198), Math.Clamp(transform.Position.Y, 130, 640));
        transform.Velocity = dashTime > 0 ? direction * 850 : move.Normalize() * (310 + engineUpgrades * 45) / (1 + Cargo * 0.19);
        transform.Rotation = direction.Angle * 180 / Math.PI + 90;
        shield.GetComponent<CTransform>().Position = Centre(player);
        shield.GetComponent<CAnimation>().ShouldDraw = grace > 0 || dashTime > 0;
        UpdateRaiders(deltaSeconds);
        foreach (var hit in systems.Get<PhysicsSystem>().CollisionEvents)
        {
            Entity? raider = hit.A == player && hit.B.Tag == "salvageRaider" ? hit.B
                : hit.B == player && hit.A.Tag == "salvageRaider" ? hit.A : null;
            if (raider == null || !raider.Active || raider.GetComponent<Raider>().Warning > 0) continue;
            if (dashTime > 0)
            {
                raider.Active = false;
                Score += 100;
                audio?.Play("Hit", SoundType.SoundEffect);
            }
            else if (grace <= 0)
            {
                raider.Active = false;
                Hull--;
                grace = 1.5;
                DropCargo();
                if (Hull <= 0) Finish(SalvagePhase.Defeat);
            }
        }
        dashTime = Math.Max(0, dashTime - deltaSeconds);
        if (Phase != SalvagePhase.Flight) { UpdateHud(); return; }
        foreach (var core in entities.GetEntitiesWithTag("salvageCore"))
        {
            if (!core.Active) continue;
            core.GetComponent<CTransform>().Rotation += deltaSeconds * 75;
            if (Cargo < capacity && grace < 1.3 && core.GetComponent<CTransform>().Position.DistanceTo(Centre(player)) < 52)
            {
                core.Tag = "salvageCargo";
                cargo.Add(core);
            }
        }
        for (int i = 0; i < cargo.Count; i++)
        {
            var tow = cargo[i].GetComponent<CTransform>();
            var target = Centre(player) - direction * (40 + i * 27);
            tow.Position += (target - tow.Position) * Math.Min(1, deltaSeconds * 12);
        }
        if (Centre(player).DistanceTo(Dock) < 72 && Cargo > 0)
        {
            Delivered += Cargo;
            Score += Cargo * 250;
            foreach (var core in cargo) core.Active = false;
            cargo.Clear();
            Hull = Math.Min(maxHull, Hull + 1);
            if (Delivered >= Quota)
            {
                Score += (int)RemainingSeconds * 20;
                Finish(Sortie == 3 ? SalvagePhase.Victory : SalvagePhase.Upgrade);
            }
        }
        if (Phase == SalvagePhase.Flight && RemainingSeconds <= 0) Finish(SalvagePhase.Defeat);
        UpdateHud();
    }

    private void UpdateRaiders(double dt)
    {
        spawnTimer -= dt;
        if (spawnTimer <= 0)
        {
            if (entities.GetEntitiesWithTag("salvageRaider").Count < 12)
            {
                double y = spawnIndex++ % 2 == 0 ? 150 : 625;
                double x = 490 + spawnIndex * 173 % 650;
                var raider = Sprite("salvageRaider", "Siegehunter", new Vec2(x, y), 34, 34, 8);
                raider.AddComponent(new CBoundingBox(new Vec2(34, 34), false, false));
                raider.AddComponent(new CMovement(0, 500));
                raider.AddComponent(new Raider());
            }
            spawnTimer = 3.2 - Sortie * 0.45;
        }
        foreach (var entity in entities.GetEntitiesWithTag("salvageRaider"))
        {
            var raider = entity.GetComponent<Raider>();
            var transform = entity.GetComponent<CTransform>();
            raider.Warning -= dt;
            raider.Age += dt;
            if (raider.Age > 18) { entity.Active = false; continue; }
            double scale = raider.Warning > 0 ? 1 + Math.Sin(raider.Warning * 24) * 0.3 : 1;
            transform.Scale = new Vec2(scale, scale);
            Vec2 aim = (Centre(player) - Centre(entity)).Normalize();
            bool docked = Centre(player).DistanceTo(Dock) < 95;
            transform.Velocity = raider.Warning > 0 ? Vec2.Zero : aim * (docked ? -110 : 105 + Sortie * 15);
            transform.Rotation = aim.Angle * 180 / Math.PI + 90;
        }
    }

    private void DropCargo()
    {
        for (int i = 0; i < cargo.Count; i++)
        {
            cargo[i].Tag = "salvageCore";
            double angle = i * Math.PI * 2 / cargo.Count;
            var position = Centre(player) + new Vec2(Math.Cos(angle), Math.Sin(angle)) * 65;
            cargo[i].GetComponent<CTransform>().Position = new Vec2(Math.Clamp(position.X, 300, 1200), Math.Clamp(position.Y, 155, 635));
        }
        cargo.Clear();
    }

    private void Finish(SalvagePhase phase)
    {
        Phase = phase;
        player.GetComponent<CTransform>().Velocity = Vec2.Zero;
        foreach (var entity in entities.GetEntitiesWithTag("salvageRaider")) entity.Active = false;
        if (phase == SalvagePhase.Upgrade)
            foreach (var core in entities.GetEntitiesWithTag("salvageCore")) core.Active = false;
    }

    private void Upgrade(int choice)
    {
        if (Phase != SalvagePhase.Upgrade) return;
        if (choice == 0) capacity++;
        if (choice == 1) engineUpgrades++;
        if (choice == 2) maxHull += 2;
        Hull = maxHull;
        Launch();
    }

    private void UpdateHud()
    {
        status.Text = $"HULL {Hull}/{maxHull}   SORTIE {Sortie}/3   BANKED {Delivered}/{Quota}   SCORE {Score:000000}";
        message.Text = Phase switch
        {
            SalvagePhase.Ready => "SPACE / LAUNCH    Collect green cores. Bring them back to the carrier.",
            SalvagePhase.Upgrade => "SORTIE CLEAR / CHOOSE:   F +CARGO HOLD    G +ENGINE SPEED    H +ARMOR",
            SalvagePhase.Victory => $"CARRIER SAVED / {Score:000000} POINTS    R PLAY AGAIN    Q MENU",
            SalvagePhase.Defeat => Hull <= 0 ? "TUG DESTROYED / R RETRY    Q MENU" : "JUMP WINDOW MISSED / R RETRY    Q MENU",
            _ => $"{RemainingSeconds:00.0}s TO JUMP    CARGO {Cargo}/{capacity}    DASH {(DashCooldown <= 0 ? "READY" : DashCooldown.ToString("0.0") + "s")}"
        };
        detail.Text = Phase == SalvagePhase.Upgrade ? "Every upgrade fully repairs your ship. Banked cores count toward this sortie only."
            : "WASD / ARROWS move    SPACE dash + ram    Tow cores on contact; cargo slows you. Dock repairs hull.    R restart    Q menu";
    }

    private static Vec2 Centre(Entity entity) => entity.GetComponent<CTransform>().Position + entity.GetComponent<CBoundingBox>().Size / 2;
}
