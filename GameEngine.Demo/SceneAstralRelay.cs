using GameEngine.Core;
using GameEngine.Core.Components;
using GameEngine.Core.Systems;
using SkiaSharp;
using System;

namespace GameEngine.Demo;

/// <summary>
/// A compact top-down action level that demonstrates dynamic objectives, seeking AI,
/// cooldown abilities, timed hazards, layered rendering, audio cues, and scene flow.
/// </summary>
public sealed class SceneAstralRelay : Scene
{
    private const int RelayCount = 3;
    private const int StartingHull = 3;
    private const double DashCooldownSeconds = 1.15;
    private const double PulseCooldownSeconds = 3.5;
    private const double PlayerInvulnerabilitySeconds = 1.35;
    private const double ArenaMargin = 54;

    private static readonly Vec2 PlayerSpawn = new(82, 330);
    private static readonly Vec2[] RelayPositions =
    [
        new(183, 137),
        new(558, 309),
        new(188, 538)
    ];

    private static readonly Vec2[] HunterSpawns =
    [
        new(710, 90),
        new(670, 570),
        new(420, 610),
        new(825, 155),
        new(450, 95)
    ];

    private Assets? assets;
    private Action<Scene?>? resetScene;
    private AudioSystem? audioSystem;
    private int activatedRelays;
    private double elapsedSeconds;
    private double messageSeconds;
    private double endStateSeconds;
    private bool extractionReady;
    private bool levelComplete;
    private bool signalLost;

    public override int VirtualWidth => 960;
    public override int VirtualHeight => 720;

    private Assets Assets => assets
        ?? throw new InvalidOperationException("SceneAstralRelay has not been initialized.");

    public override void Initialize(
        EntityManager entityManager,
        InputManager inputManager,
        AudioSystem? audioPlayer,
        Action<Scene?> ResetScene)
    {
        assets ??= new Assets("assets.txt");
        resetScene = ResetScene;
        audioSystem = audioPlayer;

        CreateBackdrop(entityManager);
        CreateHud(entityManager);
        CreateEnergyLanes(entityManager);

        var player = CreatePlayer(entityManager);
        ConfigureInput(inputManager, player);

        for (int i = 0; i < RelayPositions.Length; i++)
            CreateRelay(entityManager, i, RelayPositions[i]);

        CreateExtractionGate(entityManager);
        CreateHunter(entityManager, HunterSpawns[0], 118);
        CreateHunter(entityManager, HunterSpawns[1], 126);
        SetMessage(entityManager, "CHARGE ALL THREE RELAYS // THEN REACH THE VIOLET GATE", 5);
    }

    public override void Update(EntityManager entityManager, SystemContainer systems, double deltaSeconds)
    {
        elapsedSeconds += deltaSeconds;
        UpdateTimers(entityManager, deltaSeconds);

        var player = entityManager.GetEntityWithTag("astralPlayer");
        if (player == null)
            return;

        var pilot = player.GetComponent<CAstralPilot>();
        var playerTransform = player.GetComponent<CTransform>();

        if (pilot.RestartRequested)
        {
            resetScene?.Invoke(new SceneAstralRelay());
            return;
        }

        if (pilot.MenuRequested)
        {
            resetScene?.Invoke(new SceneMenu());
            return;
        }

        if (levelComplete || signalLost)
        {
            FreezeActors(entityManager, player);
            endStateSeconds -= deltaSeconds;
            if (signalLost && endStateSeconds <= 0)
                resetScene?.Invoke(new SceneAstralRelay());
            return;
        }

        UpdatePlayerAbilities(entityManager, player, pilot, playerTransform, deltaSeconds);
        UpdateHunters(entityManager, playerTransform, pilot, deltaSeconds);
        UpdateEnergyLanes(entityManager);
        HandleCollisions(entityManager, systems.Get<PhysicsSystem>(), player, pilot, playerTransform);
        ClampToArena(playerTransform, player.GetComponent<CBoundingBox>().Size);
        UpdateHud(entityManager, pilot);
    }

    private void CreateBackdrop(EntityManager entityManager)
    {
        var backdrop = entityManager.CreateEntity("astralBackdrop");
        backdrop.AddComponent(new CAnimation(Assets.GetAnimation("AstralArena")));
        backdrop.AddComponent(new CTransform(new Vec2(VirtualWidth / 2, VirtualHeight / 2))
        {
            Layer = -100
        });
    }

    private Entity CreatePlayer(EntityManager entityManager)
    {
        var player = entityManager.CreateEntity("astralPlayer");
        player.AddComponent(new CAnimation(Assets.GetAnimation("AstralPlayer")));
        player.AddComponent(new CTransform(PlayerSpawn) { Layer = 20 });
        player.AddComponent(new CBoundingBox(new Vec2(72, 72), false, false));
        player.AddComponent(new CMovement(1150, 275));
        player.AddComponent<CInput>();
        player.AddComponent(new CAstralPilot());

        var pulse = CreateText(entityManager, "pulseVisual", "◎", 150,
            new Vec2(PlayerSpawn.X + 36, PlayerSpawn.Y + 55), SKColors.Cyan, 15);
        pulse.GetComponent<CText>().ShouldDraw = false;
        return player;
    }

    private void ConfigureInput(InputManager inputManager, Entity player)
    {
        inputManager.AddAction(GeKeys.W, "AstralUp");
        inputManager.AddAction(GeKeys.S, "AstralDown");
        inputManager.AddAction(GeKeys.A, "AstralLeft");
        inputManager.AddAction(GeKeys.D, "AstralRight");
        inputManager.AddAction(GeKeys.Space, "AstralDash");
        inputManager.AddAction(GeKeys.E, "AstralPulse");
        inputManager.AddAction(GeKeys.R, "AstralRestart");
        inputManager.AddAction(GeKeys.Q, "AstralMenu");

        inputManager.ActionMapper.MapActionToComponent<CInput>(
            "AstralUp", player, (input, active) => input.Up = active);
        inputManager.ActionMapper.MapActionToComponent<CInput>(
            "AstralDown", player, (input, active) => input.Down = active);
        inputManager.ActionMapper.MapActionToComponent<CInput>(
            "AstralLeft", player, (input, active) => input.Left = active);
        inputManager.ActionMapper.MapActionToComponent<CInput>(
            "AstralRight", player, (input, active) => input.Right = active);
        inputManager.ActionMapper.MapActionToComponent<CAstralPilot>(
            "AstralDash", player, (pilot, active) => pilot.DashRequested |= active, oneShot: true);
        inputManager.ActionMapper.MapActionToComponent<CAstralPilot>(
            "AstralPulse", player, (pilot, active) => pilot.PulseRequested |= active, oneShot: true);
        inputManager.ActionMapper.MapActionToComponent<CAstralPilot>(
            "AstralRestart", player, (pilot, active) => pilot.RestartRequested |= active, oneShot: true);
        inputManager.ActionMapper.MapActionToComponent<CAstralPilot>(
            "AstralMenu", player, (pilot, active) => pilot.MenuRequested |= active, oneShot: true);
    }

    private void CreateRelay(EntityManager entityManager, int index, Vec2 position)
    {
        var relay = entityManager.CreateEntity("astralRelay");
        relay.AddComponent(new CAnimation(Assets.GetAnimation("AstralCore")));
        relay.AddComponent(new CTransform(position) { Layer = 5 });
        relay.AddComponent(new CBoundingBox(new Vec2(46, 46), false, false));

        var indicator = CreateText(entityManager, $"relayLabel{index}", $"RELAY {index + 1}", 15,
            new Vec2(position.X + 23, position.Y + 68), SKColor.Parse("#FFBE45"), 6);
        relay.AddComponent(new CAstralRelay(index, indicator));
    }

    private void CreateExtractionGate(EntityManager entityManager)
    {
        var gate = entityManager.CreateEntity("astralGate");
        gate.AddComponent(new CTransform(new Vec2(862, 250)) { Layer = 4 });
        gate.AddComponent(new CBoundingBox(new Vec2(76, 220), false, false));

        var label = CreateText(entityManager, "gateLabel", "GATE LOCKED", 17,
            new Vec2(895, 242), SKColor.Parse("#E879F9"), 7);
        label.GetComponent<CText>().TextAlign = SKTextAlign.Center;
    }

    private void CreateHunter(EntityManager entityManager, Vec2 position, double speed)
    {
        var hunter = entityManager.CreateEntity("astralHunter");
        hunter.AddComponent(new CAnimation(Assets.GetAnimation("AstralHunter")));
        hunter.AddComponent(new CTransform(position) { Layer = 18 });
        hunter.AddComponent(new CBoundingBox(new Vec2(78, 78), false, false));
        hunter.AddComponent(new CMovement(0, speed + 90));
        hunter.AddComponent(new CAstralHunter(speed));
    }

    private void CreateEnergyLanes(EntityManager entityManager)
    {
        CreateEnergyLane(entityManager, new Vec2(285, 225), 470, 0);
        CreateEnergyLane(entityManager, new Vec2(215, 475), 505, Math.PI);
    }

    private static void CreateEnergyLane(
        EntityManager entityManager,
        Vec2 position,
        double width,
        double phase)
    {
        string line = new('━', (int)(width / 23));
        var visual = CreateText(entityManager, "astralBeamVisual", line, 30,
            position, SKColor.Parse("#154B66"), -2, SKTextAlign.Left);
        var lane = entityManager.CreateEntity("astralBeam");
        lane.AddComponent(new CTransform(position + new Vec2(0, -18)) { Layer = -2 });
        lane.AddComponent(new CBoundingBox(Vec2.Zero, false, false));
        lane.AddComponent(new CAstralBeam(width, phase, visual));
    }

    private static void CreateHud(EntityManager entityManager)
    {
        CreateText(entityManager, "astralTitle", "ASTRAL RELAY", 24,
            new Vec2(26, 35), SKColor.Parse("#8BE9FD"), 100, SKTextAlign.Left);
        CreateText(entityManager, "astralStatus", string.Empty, 17,
            new Vec2(26, 62), SKColors.White, 100, SKTextAlign.Left);
        CreateText(entityManager, "astralControls",
            "WASD MOVE   SPACE DASH   E EMP PULSE   R RESTART   Q MENU", 14,
            new Vec2(26, 700), SKColor.Parse("#A5B4C8"), 100, SKTextAlign.Left);
        CreateText(entityManager, "astralMessage", string.Empty, 19,
            new Vec2(480, 94), SKColor.Parse("#FFDD88"), 100);
    }

    private static Entity CreateText(
        EntityManager entityManager,
        string tag,
        string value,
        int size,
        Vec2 position,
        SKColor color,
        int layer,
        SKTextAlign align = SKTextAlign.Center)
    {
        var entity = entityManager.CreateEntity(tag);
        entity.AddComponent(new CTransform(position) { Layer = layer });
        var text = new CText(value, size) { TextAlign = align };
        text.Paint.Color = color;
        entity.AddComponent(text);
        return entity;
    }

    private void UpdateTimers(EntityManager entityManager, double deltaSeconds)
    {
        if (messageSeconds <= 0)
            return;

        messageSeconds -= deltaSeconds;
        if (messageSeconds <= 0)
        {
            var message = entityManager.GetEntityWithTag("astralMessage")?.TryGetComponent<CText>();
            if (message != null)
                message.Text = string.Empty;
        }
    }

    private void UpdatePlayerAbilities(
        EntityManager entityManager,
        Entity player,
        CAstralPilot pilot,
        CTransform transform,
        double deltaSeconds)
    {
        pilot.DashCooldown = Math.Max(0, pilot.DashCooldown - deltaSeconds);
        pilot.PulseCooldown = Math.Max(0, pilot.PulseCooldown - deltaSeconds);
        pilot.Invulnerability = Math.Max(0, pilot.Invulnerability - deltaSeconds);
        pilot.DashTime = Math.Max(0, pilot.DashTime - deltaSeconds);
        pilot.PulseVisualTime = Math.Max(0, pilot.PulseVisualTime - deltaSeconds);
        player.GetComponent<CAnimation>().ShouldDraw = pilot.Invulnerability <= 0
            || (int)(elapsedSeconds * 12) % 2 == 0;

        var input = player.GetComponent<CInput>();
        Vec2 inputDirection = new(
            (input.Right ? 1 : 0) - (input.Left ? 1 : 0),
            (input.Down ? 1 : 0) - (input.Up ? 1 : 0));
        if (inputDirection != Vec2.Zero)
            pilot.Facing = inputDirection.Normalize();

        var movement = player.GetComponent<CMovement>();
        movement.MaxSpeed = pilot.DashTime > 0 ? 850 : 275;

        if (pilot.DashRequested && pilot.DashCooldown <= 0)
        {
            pilot.DashRequested = false;
            pilot.DashCooldown = DashCooldownSeconds;
            pilot.DashTime = 0.18;
            transform.Velocity = pilot.Facing * 850;
            movement.MaxSpeed = 850;
        }
        else
        {
            pilot.DashRequested = false;
        }

        if (pilot.PulseRequested && pilot.PulseCooldown <= 0)
        {
            pilot.PulseRequested = false;
            pilot.PulseCooldown = PulseCooldownSeconds;
            pilot.PulseVisualTime = 0.32;
            StunNearbyHunters(entityManager, CentreOf(player), 205);
            audioSystem?.Play("Hit", SoundType.SoundEffect);
        }
        else
        {
            pilot.PulseRequested = false;
        }

        var pulse = entityManager.GetEntityWithTag("pulseVisual");
        if (pulse != null)
        {
            pulse.GetComponent<CTransform>().Position = CentreOf(player) + new Vec2(0, 24);
            pulse.GetComponent<CText>().ShouldDraw = pilot.PulseVisualTime > 0;
        }

        if (transform.Velocity.LengthSquared() > 16)
            transform.Rotation = transform.Velocity.Angle * (180 / Math.PI) + 90;
    }

    private static void StunNearbyHunters(EntityManager entityManager, Vec2 origin, double radius)
    {
        foreach (var hunter in entityManager.GetEntitiesWithTag("astralHunter"))
        {
            if (CentreOf(hunter).DistanceTo(origin) <= radius)
                hunter.GetComponent<CAstralHunter>().StunTime = 2.4;
        }
    }

    private static void UpdateHunters(
        EntityManager entityManager,
        CTransform playerTransform,
        CAstralPilot pilot,
        double deltaSeconds)
    {
        Vec2 playerCentre = playerTransform.Position + new Vec2(36, 36);
        int hunterNumber = 0;

        foreach (var hunter in entityManager.GetEntitiesWithTag("astralHunter"))
        {
            var state = hunter.GetComponent<CAstralHunter>();
            var transform = hunter.GetComponent<CTransform>();
            state.StunTime = Math.Max(0, state.StunTime - deltaSeconds);

            if (state.StunTime > 0)
            {
                transform.Velocity = Vec2.Zero;
                transform.Rotation += 540 * deltaSeconds;
                continue;
            }

            Vec2 hunterCentre = transform.Position + new Vec2(39, 39);
            Vec2 toPlayer = (playerCentre - hunterCentre).Normalize();
            double orbitSign = hunterNumber++ % 2 == 0 ? 1 : -1;
            Vec2 tangent = new(-toPlayer.Y * orbitSign, toPlayer.X * orbitSign);
            double aggression = 1 + (pilot.Hull < StartingHull ? 0.08 : 0);
            Vec2 pursuit = (toPlayer + tangent * 0.18).Normalize();
            transform.Velocity = pursuit * state.Speed * aggression;
            transform.Rotation = pursuit.Angle * (180 / Math.PI) + 90;
            ClampToArena(transform, hunter.GetComponent<CBoundingBox>().Size);
        }
    }

    private void UpdateEnergyLanes(EntityManager entityManager)
    {
        foreach (var lane in entityManager.GetEntitiesWithTag("astralBeam"))
        {
            var beam = lane.GetComponent<CAstralBeam>();
            bool active = Math.Sin(elapsedSeconds * 1.65 + beam.Phase) > 0.35;
            beam.Active = active;
            lane.GetComponent<CBoundingBox>().Size = active
                ? new Vec2(beam.Width, 20)
                : Vec2.Zero;
            beam.Visual.GetComponent<CText>().Paint.Color = active
                ? SKColor.Parse("#4DEBFF")
                : SKColor.Parse("#154B66");
        }
    }

    private void HandleCollisions(
        EntityManager entityManager,
        PhysicsSystem physics,
        Entity player,
        CAstralPilot pilot,
        CTransform playerTransform)
    {
        foreach (var collision in physics.CollisionEvents)
        {
            if (!TryOther(collision, player, out var other))
                continue;

            switch (other.Tag)
            {
                case "astralRelay":
                    ActivateRelay(entityManager, other);
                    break;
                case "astralHunter":
                    if (other.GetComponent<CAstralHunter>().StunTime <= 0)
                        DamagePlayer(entityManager, pilot, playerTransform, "HUNTER IMPACT");
                    break;
                case "astralBeam":
                    if (other.GetComponent<CAstralBeam>().Active)
                        DamagePlayer(entityManager, pilot, playerTransform, "ENERGY SURGE");
                    break;
                case "astralGate":
                    if (extractionReady)
                        CompleteLevel(entityManager, pilot, playerTransform);
                    else
                        SetMessage(entityManager, $"GATE REQUIRES {RelayCount - activatedRelays} MORE RELAY SIGNALS", 1.25);
                    break;
            }
        }
    }

    private void ActivateRelay(EntityManager entityManager, Entity relayEntity)
    {
        var relay = relayEntity.GetComponent<CAstralRelay>();
        if (relay.Activated)
            return;

        relay.Activated = true;
        relayEntity.GetComponent<CAnimation>().ShouldDraw = false;
        relayEntity.GetComponent<CBoundingBox>().Size = Vec2.Zero;
        var label = relay.Indicator.GetComponent<CText>();
        label.Text = $"RELAY {relay.Index + 1} ONLINE";
        label.Paint.Color = SKColor.Parse("#67F8FF");

        activatedRelays++;
        audioSystem?.Play("Hit", SoundType.SoundEffect);
        CreateHunter(entityManager, HunterSpawns[activatedRelays + 1], 132 + activatedRelays * 14);

        if (activatedRelays == RelayCount)
        {
            extractionReady = true;
            var gateLabel = entityManager.GetEntityWithTag("gateLabel")?.GetComponent<CText>();
            if (gateLabel != null)
            {
                gateLabel.Text = "EXTRACT";
                gateLabel.Paint.Color = SKColor.Parse("#F0ABFC");
            }
            SetMessage(entityManager, "ALL RELAYS ONLINE // EXTRACTION GATE OPEN", 4);
        }
        else
        {
            SetMessage(entityManager,
                $"RELAY {relay.Index + 1} CHARGED // PURSUIT LEVEL {activatedRelays + 1}", 2.25);
        }
    }

    private void DamagePlayer(
        EntityManager entityManager,
        CAstralPilot pilot,
        CTransform playerTransform,
        string source)
    {
        if (pilot.Invulnerability > 0 || pilot.DashTime > 0)
            return;

        pilot.Hull--;
        pilot.Invulnerability = PlayerInvulnerabilitySeconds;
        playerTransform.Position = PlayerSpawn;
        playerTransform.PreviousPosition = PlayerSpawn;
        playerTransform.Velocity = Vec2.Zero;
        audioSystem?.Play("Hit", SoundType.SoundEffect);

        if (pilot.Hull <= 0)
        {
            signalLost = true;
            endStateSeconds = 2.5;
            SetMessage(entityManager, "SIGNAL LOST // AUTOMATIC REBOOT", 3);
        }
        else
        {
            SetMessage(entityManager, $"{source} // HULL AT {pilot.Hull}", 1.5);
        }
    }

    private void CompleteLevel(
        EntityManager entityManager,
        CAstralPilot pilot,
        CTransform playerTransform)
    {
        levelComplete = true;
        endStateSeconds = double.PositiveInfinity;
        playerTransform.Velocity = Vec2.Zero;
        SetMessage(entityManager,
            $"EXTRACTION COMPLETE // {pilot.Hull} HULL // {elapsedSeconds:0.0} SECONDS", double.PositiveInfinity);

        var controls = entityManager.GetEntityWithTag("astralControls")?.GetComponent<CText>();
        if (controls != null)
            controls.Text = "MISSION COMPLETE   R REPLAY   Q RETURN TO MENU";
    }

    private static void FreezeActors(EntityManager entityManager, Entity player)
    {
        player.GetComponent<CInput>().Up = false;
        player.GetComponent<CInput>().Down = false;
        player.GetComponent<CInput>().Left = false;
        player.GetComponent<CInput>().Right = false;
        player.GetComponent<CTransform>().Velocity = Vec2.Zero;

        foreach (var hunter in entityManager.GetEntitiesWithTag("astralHunter"))
            hunter.GetComponent<CTransform>().Velocity = Vec2.Zero;
    }

    private void UpdateHud(EntityManager entityManager, CAstralPilot pilot)
    {
        var status = entityManager.GetEntityWithTag("astralStatus")?.GetComponent<CText>();
        if (status == null)
            return;

        string dash = pilot.DashCooldown <= 0 ? "READY" : $"{pilot.DashCooldown:0.0}s";
        string pulse = pilot.PulseCooldown <= 0 ? "READY" : $"{pilot.PulseCooldown:0.0}s";
        status.Text = $"RELAYS {activatedRelays}/{RelayCount}   HULL {pilot.Hull}/{StartingHull}   DASH {dash}   PULSE {pulse}";
    }

    private void SetMessage(EntityManager entityManager, string text, double seconds)
    {
        var message = entityManager.GetEntityWithTag("astralMessage")?.GetComponent<CText>();
        if (message != null)
            message.Text = text;
        messageSeconds = seconds;
    }

    private static bool TryOther(CollisionEvent collision, Entity player, out Entity other)
    {
        if (collision.A == player)
        {
            other = collision.B;
            return true;
        }

        if (collision.B == player)
        {
            other = collision.A;
            return true;
        }

        other = player;
        return false;
    }

    private static Vec2 CentreOf(Entity entity)
    {
        var transform = entity.GetComponent<CTransform>();
        var size = entity.GetComponent<CBoundingBox>().Size;
        return transform.Position + size / 2;
    }

    private static void ClampToArena(CTransform transform, Vec2 size)
    {
        double x = Math.Clamp(transform.Position.X, ArenaMargin, 960 - ArenaMargin - size.X);
        double y = Math.Clamp(transform.Position.Y, ArenaMargin + 18, 720 - ArenaMargin - size.Y);
        transform.Position = new Vec2(x, y);
    }

    private sealed class CAstralPilot : Component
    {
        public Vec2 Facing = new(1, 0);
        public int Hull = StartingHull;
        public double DashCooldown;
        public double DashTime;
        public double PulseCooldown;
        public double PulseVisualTime;
        public double Invulnerability;
        public bool DashRequested;
        public bool PulseRequested;
        public bool RestartRequested;
        public bool MenuRequested;
    }

    private sealed class CAstralRelay : Component
    {
        public CAstralRelay(int index, Entity indicator)
        {
            Index = index;
            Indicator = indicator;
        }

        public int Index { get; }
        public Entity Indicator { get; }
        public bool Activated { get; set; }
    }

    private sealed class CAstralHunter : Component
    {
        public CAstralHunter(double speed)
        {
            Speed = speed;
        }

        public double Speed { get; }
        public double StunTime { get; set; }
    }

    private sealed class CAstralBeam : Component
    {
        public CAstralBeam(double width, double phase, Entity visual)
        {
            Width = width;
            Phase = phase;
            Visual = visual;
        }

        public double Width { get; }
        public double Phase { get; }
        public Entity Visual { get; }
        public bool Active { get; set; }
    }
}
