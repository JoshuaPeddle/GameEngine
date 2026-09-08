using System;
using System.Linq;
using GameEngine.Core;
using GameEngine.Core.Components;
using GameEngine.Core.Systems;
using SkiaSharp;

namespace GameEngine.Demo;

public enum SiegePhase { Ready, Combat, Upgrade, Victory, Defeat }
public enum SiegeKind { Hunter, Gunner, Splitter, Boss, Cover }

public sealed class CSiegeActor : Component
{
    public SiegeKind Kind;
    public int Hull;
    public int MaxHull;
    public double Cooldown;
    public double Age;
    public double Stun;
    public bool Small;
    public int Orbit;
}

public sealed class SceneVoidSiege : Scene
{
    private sealed class Shot : Component
    {
        public bool Hostile;
        public int Damage;
        public double Life = 4;
    }

    private sealed class Spark : Component
    {
        public double Life;
    }

    private Assets assets = null!;
    private EntityManager entities = null!;
    private Entity player = null!;
    private Entity pulse = null!;
    private Entity shield = null!;
    private Entity[] sparks = [];
    private CText status = null!;
    private CText message = null!;
    private CText detail = null!;
    private CText bossStatus = null!;
    private Action<Scene?> reset = null!;
    private AudioSystem? audio;
    private readonly Random random = new(7619);
    private Vec2 direction = new(0, -1);
    private double fireTimer;
    private double dashTime;
    private double invulnerability;
    private double pulseTime;
    private double waveAge;
    private int sparkIndex;
    private int queuedShots;
    private int volley = 1;
    private int haste;
    private int maxHull = 6;

    public override int VirtualWidth => 1280;
    public override int VirtualHeight => 800;
    public SiegePhase Phase { get; private set; }
    public int Wave { get; private set; }
    public int EnemiesRemaining { get; private set; }
    public int Hull { get; private set; } = 6;
    public int Score { get; private set; }
    public int Salvage { get; private set; }
    public double DashCooldown { get; private set; }
    public double PulseCooldown { get; private set; }

    public override void Initialize(EntityManager entityManager, InputManager inputManager,
        AudioSystem? audioPlayer, Action<Scene?> ResetScene)
    {
        entities = entityManager;
        assets = new Assets("assets.json", AssetSource);
        audio = audioPlayer;
        reset = ResetScene;
        Sprite("siegeArena", "arena", new Vec2(640, 400), new Vec2(1280, 800), -100);
        player = Body("siegePlayer", "pilot", new Vec2(640, 440), 32, 10, true);
        player.AddComponent<CInput>();
        player.AddComponent(new CMovement(0, 900));
        pulse = Sprite("siegePulse", "ring", Centre(player), new Vec2(128, 128), 15);
        pulse.GetComponent<CAnimation>().ShouldDraw = false;
        shield = Sprite("siegeShield", "ring", Centre(player), new Vec2(48, 48), 9);
        shield.GetComponent<CAnimation>().ShouldDraw = false;
        sparks = new Entity[128];
        for (int i = 0; i < sparks.Length; i++)
        {
            sparks[i] = Sprite("siegeSpark", "spark", Vec2.Zero, new Vec2(6, 6), 20);
            sparks[i].AddComponent(new Spark());
            sparks[i].AddComponent(new CMovement(0, 500));
            sparks[i].GetComponent<CAnimation>().ShouldDraw = false;
        }
        Label("siegeTitle", "VOID / SIEGE", new Vec2(48, 63), 30, "#75FFF1");
        Label("siegeSubtitle", "SURVIVE THE BREACH", new Vec2(49, 85), 13, "#7F9CAF");
        status = Label("siegeStatus", "", new Vec2(355, 53), 21, "#E3F5FF");
        bossStatus = Label("siegeBossStatus", "", new Vec2(355, 80), 15, "#FF839D");
        message = Label("siegeMessage", "SPACE  /  INITIATE BREACH", new Vec2(48, 734), 23, "#75FFF1");
        detail = Label("siegeDetail", "WASD / ARROWS move    SPACE dash    E EMP    Auto-fire targets nearest enemy    R restart    Q menu",
            new Vec2(48, 763), 15, "#A3BBCD");
        foreach (var centre in new[] { new Vec2(360, 280), new Vec2(920, 280), new Vec2(360, 520), new Vec2(920, 520) })
        {
            var cover = Body("siegeCover", "cover", centre, 58, 2, true);
            cover.AddComponent(new CSiegeActor { Kind = SiegeKind.Cover, Hull = 8, MaxHull = 8 });
        }
        inputManager.BindGestureAction(PointerGesture.Up, "SiegeUp");
        inputManager.BindGestureAction(PointerGesture.Down, "SiegeDown");
        inputManager.BindGestureAction(PointerGesture.Left, "SiegeLeft");
        inputManager.BindGestureAction(PointerGesture.Right, "SiegeRight");
        inputManager.BindGestureAction(PointerGesture.Tap, "SiegeDash");
        BindMove(inputManager, GeKeys.W, GeKeys.Up, "SiegeUp", (c, v) => c.Up = v);
        BindMove(inputManager, GeKeys.S, GeKeys.Down, "SiegeDown", (c, v) => c.Down = v);
        BindMove(inputManager, GeKeys.A, GeKeys.Left, "SiegeLeft", (c, v) => c.Left = v);
        BindMove(inputManager, GeKeys.D, GeKeys.Right, "SiegeRight", (c, v) => c.Right = v);
        Bind(inputManager, GeKeys.Space, "SiegeDash", () =>
        {
            if (Phase == SiegePhase.Ready) StartWave();
            else if (Phase == SiegePhase.Combat && DashCooldown <= 0)
            {
                DashCooldown = 1.8;
                dashTime = 0.19;
                invulnerability = Math.Max(invulnerability, 0.25);
                Burst(Centre(player), 10);
            }
        });
        Bind(inputManager, GeKeys.E, "SiegePulse", Emp);
        Bind(inputManager, GeKeys.F, "SiegeVolley", () => Upgrade(0));
        Bind(inputManager, GeKeys.G, "SiegeHaste", () => Upgrade(1));
        Bind(inputManager, GeKeys.H, "SiegeHull", () => Upgrade(2));
        Bind(inputManager, GeKeys.R, "SiegeRestart", () => reset(new SceneVoidSiege()));
        Bind(inputManager, GeKeys.Q, "SiegeMenu", () => reset(new SceneMenu()));
        UpdateHud();
    }

    private void BindMove(InputManager input, GeKeys key, GeKeys alternate, string action, Action<CInput, bool> apply)
    {
        input.AddAction(key, action);
        input.AddAction(alternate, action);
        input.ActionMapper.MapActionToComponent(action, player, apply);
    }

    private void Bind(InputManager input, GeKeys key, string action, Action apply)
    {
        input.AddAction(key, action);
        input.ActionMapper.MapActionToComponent<CInput>(action, player, (_, active) => { if (active) apply(); }, oneShot: true);
    }

    private Entity Sprite(string tag, string image, Vec2 centre, Vec2 size, int layer)
    {
        var entity = entities.CreateEntity(tag);
        entity.AddComponent(new CTransform(centre) { Layer = layer });
        entity.AddComponent(new CAnimation(assets.GetAnimation("Siege" + image, size)));
        return entity;
    }

    private Entity Body(string tag, string image, Vec2 centre, double size, int layer, bool solid = false)
    {
        var entity = Sprite(tag, image, centre - new Vec2(size / 2, size / 2), new Vec2(size, size), layer);
        entity.AddComponent(new CBoundingBox(new Vec2(size, size), false, solid));
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

    private void StartWave()
    {
        Wave++;
        waveAge = 0;
        fireTimer = 0;
        Phase = SiegePhase.Combat;
        invulnerability = 1.5;
        DashCooldown = PulseCooldown = 0;
        if (Wave == 5)
        {
            Spawn(SiegeKind.Boss, new Vec2(640, 220));
            for (int i = 0; i < 4; i++) Spawn(SiegeKind.Gunner, SpawnPoint(i, 4));
        }
        else
        {
            int count = 8 + Wave * 7;
            for (int i = 0; i < count; i++)
                Spawn(i % 5 == 4 && Wave >= 2 ? SiegeKind.Splitter : i % 3 == 2 ? SiegeKind.Gunner : SiegeKind.Hunter,
                    SpawnPoint(i, count));
        }
    }

    private static Vec2 SpawnPoint(int index, int count)
    {
        double angle = Math.PI * 2 * index / count;
        return new Vec2(640 + Math.Cos(angle) * 535, 400 + Math.Sin(angle) * 220);
    }

    private void Spawn(SiegeKind kind, Vec2 position, bool small = false)
    {
        EnemiesRemaining++;
        int hull = kind switch { SiegeKind.Boss => 360, SiegeKind.Splitter => 8, SiegeKind.Gunner => 5, _ => small ? 2 : 4 };
        double size = kind == SiegeKind.Boss ? 96 : small ? 20 : 36;
        var enemy = Body("siegeEnemy", kind.ToString().ToLowerInvariant(), position, size, 8);
        enemy.AddComponent(new CMovement(0, 220));
        enemy.AddComponent(new CSiegeActor
        {
            Kind = kind, Hull = hull, MaxHull = hull, Cooldown = 1.1 + random.NextDouble(),
            Small = small, Orbit = EnemiesRemaining % 2 == 0 ? 1 : -1
        });
        Burst(position, 5);
    }

    public override void Update(EntityManager entityManager, SystemContainer systems, double deltaSeconds)
    {
        queuedShots = 0;
        UpdateEffects(deltaSeconds);
        if (Phase != SiegePhase.Combat)
        {
            player.GetComponent<CTransform>().Velocity = Vec2.Zero;
            ClearShots();
            shield.GetComponent<CAnimation>().ShouldDraw = false;
            foreach (var enemy in entities.GetEntitiesWithTag("siegeEnemy")) enemy.GetComponent<CTransform>().Velocity = Vec2.Zero;
            return;
        }
        waveAge += deltaSeconds;
        DashCooldown = Math.Max(0, DashCooldown - deltaSeconds);
        PulseCooldown = Math.Max(0, PulseCooldown - deltaSeconds);
        dashTime = Math.Max(0, dashTime - deltaSeconds);
        invulnerability = Math.Max(0, invulnerability - deltaSeconds);
        MovePlayer();
        UpdateEnemies(deltaSeconds);
        foreach (var hit in systems.Get<PhysicsSystem>().CollisionEvents)
        {
            if (!hit.A.Active || !hit.B.Active) continue;
            Contact(hit.A, hit.B);
            if (hit.A.Active && hit.B.Active) Contact(hit.B, hit.A);
        }
        foreach (var (entity, shot) in entities.GetEntitiesWithComponents<Shot>())
        {
            shot.Life -= deltaSeconds;
            var p = Centre(entity);
            if (shot.Life <= 0 || p.X < 40 || p.X > 1240 || p.Y < 115 || p.Y > 685) entity.Active = false;
        }
        CollectSalvage(deltaSeconds);
        fireTimer -= deltaSeconds;
        if (Phase == SiegePhase.Combat && fireTimer <= 0)
        {
            var target = entities.GetEntitiesWithTag("siegeEnemy").Where(e => e.Active)
                .MinBy(e => Centre(e).DistanceTo(Centre(player)));
            if (target != null)
            {
                Vec2 aim = (Centre(target) - Centre(player)).Normalize();
                for (int i = 0; i < volley; i++) Fire(Centre(player), Rotate(aim, (i - (volley - 1) / 2.0) * 0.12), false, 1 + (volley - 1) / 3);
                fireTimer = 0.25 * Math.Pow(0.8, haste);
                player.GetComponent<CTransform>().Rotation = aim.Angle * 180 / Math.PI + 90;
            }
        }
        if (Phase == SiegePhase.Combat && waveAge > 1 && EnemiesRemaining == 0)
        {
            Phase = Wave == 5 ? SiegePhase.Victory : SiegePhase.Upgrade;
            ClearShots();
            player.GetComponent<CTransform>().Velocity = Vec2.Zero;
            player.GetComponent<CAnimation>().ShouldDraw = true;
            if (Phase == SiegePhase.Victory) Score += Hull * 500;
        }
        UpdateHud();
    }

    private void MovePlayer()
    {
        var input = player.GetComponent<CInput>();
        var move = new Vec2((input.Right ? 1 : 0) - (input.Left ? 1 : 0), (input.Down ? 1 : 0) - (input.Up ? 1 : 0));
        if (move.LengthSquared() > 0) direction = move.Normalize();
        var transform = player.GetComponent<CTransform>();
        transform.Velocity = dashTime > 0 ? direction * 850 : move.Normalize() * (260 + haste * 15);
        transform.Position = new Vec2(Math.Clamp(transform.Position.X, 50, 1198), Math.Clamp(transform.Position.Y, 128, 641));
        shield.GetComponent<CAnimation>().ShouldDraw = invulnerability > 0;
        shield.GetComponent<CTransform>().Position = Centre(player);
    }

    private void UpdateEnemies(double dt)
    {
        foreach (var enemy in entities.GetEntitiesWithTag("siegeEnemy"))
        {
            if (!enemy.Active) continue;
            var actor = enemy.GetComponent<CSiegeActor>();
            var transform = enemy.GetComponent<CTransform>();
            var toPlayer = Centre(player) - Centre(enemy);
            var aim = toPlayer.Normalize();
            actor.Stun = Math.Max(0, actor.Stun - dt);
            if (actor.Stun > 0)
            {
                transform.Velocity *= Math.Pow(0.02, dt);
                continue;
            }
            actor.Age += dt;
            actor.Cooldown -= dt;
            if (actor.Kind == SiegeKind.Boss)
            {
                var destination = new Vec2(640 + Math.Sin(actor.Age * 0.55) * 340, 240 + Math.Sin(actor.Age) * 60);
                transform.Velocity = (destination - Centre(enemy)) * 1.5;
                transform.Rotation += dt * 24;
                if (actor.Cooldown <= 0)
                {
                    bool enraged = actor.Hull <= actor.MaxHull / 2;
                    int count = enraged ? 36 : 24;
                    for (int i = 0; i < count; i++) Fire(Centre(enemy), Rotate(new Vec2(1, 0), i * Math.PI * 2 / count + actor.Age * 0.5), true);
                    if (enraged)
                        for (int i = -2; i <= 2; i++) Fire(Centre(enemy), Rotate(aim, i * 0.15), true);
                    actor.Cooldown = enraged ? 0.85 : 1.4;
                }
            }
            else
            {
                double speed = actor.Small ? 180 : actor.Kind == SiegeKind.Splitter ? 74 : 92 + Wave * 7;
                var tangent = new Vec2(-aim.Y, aim.X) * actor.Orbit;
                transform.Velocity = actor.Kind == SiegeKind.Gunner
                    ? (aim * (toPlayer.Length() > 310 ? 1 : toPlayer.Length() < 220 ? -1 : 0) + tangent * 0.55).Normalize() * 85
                    : (aim + tangent * 0.18).Normalize() * speed;
                transform.Rotation = aim.Angle * 180 / Math.PI + 90;
                transform.Position = new Vec2(Math.Clamp(transform.Position.X, 52, 1190), Math.Clamp(transform.Position.Y, 130, 637));
                if (actor.Kind == SiegeKind.Gunner && actor.Cooldown <= 0)
                {
                    for (int i = -1; i <= 1; i++) Fire(Centre(enemy), Rotate(aim, i * 0.18), true);
                    actor.Cooldown = 2.2;
                }
            }
            double warning = actor.Kind is SiegeKind.Gunner or SiegeKind.Boss && actor.Cooldown < 0.45 ? 1.15 : 1;
            transform.Scale = new Vec2(warning, warning);
        }
    }

    private void Fire(Vec2 origin, Vec2 aim, bool hostile, int damage = 1)
    {
        if (entities.GetEntitiesWithComponents<Shot>().Count + queuedShots >= 220) return;
        queuedShots++;
        var shot = Body("siegeShot", hostile ? "hostile" : "shot", hostile ? origin : origin + aim * 30, hostile ? 12 : 8, 12);
        shot.AddComponent(new Shot { Hostile = hostile, Damage = damage });
        shot.AddComponent(new CMovement(0, 1000));
        shot.GetComponent<CTransform>().Velocity = aim * (hostile ? 190 + Wave * 9 : 800);
    }

    private void Contact(Entity a, Entity b)
    {
        if (a.TryGetComponent<Shot>(out var shot))
        {
            if (b == player && shot.Hostile)
            {
                a.Active = false;
                HurtPlayer();
            }
            else if (b.TryGetComponent<CSiegeActor>(out var actor) && (actor.Kind == SiegeKind.Cover || !shot.Hostile))
            {
                a.Active = false;
                Damage(b, shot.Damage);
            }
        }
        else if (a == player && b.Tag == "siegeEnemy") HurtPlayer();
    }

    private void Damage(Entity entity, int amount)
    {
        if (!entity.Active) return;
        var actor = entity.GetComponent<CSiegeActor>();
        actor.Hull -= amount;
        Burst(Centre(entity), actor.Hull <= 0 ? 14 : 3);
        if (actor.Hull > 0) return;
        entity.Active = false;
        if (actor.Kind == SiegeKind.Cover) return;
        EnemiesRemaining--;
        Score += actor.Kind == SiegeKind.Boss ? 5000 : actor.Small ? 50 : 100;
        Sprite("siegeSalvage", "scrap", Centre(entity), new Vec2(14, 14), 3);
        if (actor.Kind == SiegeKind.Splitter)
        {
            Spawn(SiegeKind.Hunter, Centre(entity) + new Vec2(18, 0), true);
            Spawn(SiegeKind.Hunter, Centre(entity) - new Vec2(18, 0), true);
        }
    }

    private void HurtPlayer()
    {
        if (invulnerability > 0 || Phase != SiegePhase.Combat) return;
        Hull--;
        invulnerability = 1;
        Burst(Centre(player), 18);
        audio?.Play("Hit", SoundType.SoundEffect);
        if (Hull > 0) return;
        Phase = SiegePhase.Defeat;
        ClearShots();
        player.GetComponent<CTransform>().Velocity = Vec2.Zero;
        player.GetComponent<CAnimation>().ShouldDraw = true;
        foreach (var enemy in entities.GetEntitiesWithTag("siegeEnemy")) enemy.GetComponent<CTransform>().Velocity = Vec2.Zero;
    }

    private void Emp()
    {
        if (Phase != SiegePhase.Combat || PulseCooldown > 0) return;
        PulseCooldown = 7;
        pulseTime = 0.45;
        pulse.GetComponent<CTransform>().Position = Centre(player);
        foreach (var enemy in entities.GetEntitiesWithTag("siegeEnemy"))
        {
            if (Centre(enemy).DistanceTo(Centre(player)) >= 240) continue;
            var actor = enemy.GetComponent<CSiegeActor>();
            Damage(enemy, actor.Kind == SiegeKind.Boss ? 12 : 4);
            if (actor.Kind != SiegeKind.Boss)
            {
                actor.Stun = 0.6;
                enemy.GetComponent<CTransform>().Velocity = (Centre(enemy) - Centre(player)).Normalize() * 200;
            }
        }
        foreach (var (entity, shot) in entities.GetEntitiesWithComponents<Shot>())
            if (shot.Hostile && Centre(entity).DistanceTo(Centre(player)) < 270) entity.Active = false;
        audio?.Play("Hit", SoundType.SoundEffect);
    }

    private void Upgrade(int choice)
    {
        if (Phase != SiegePhase.Upgrade) return;
        if (choice == 0) volley++;
        if (choice == 1) haste++;
        if (choice == 2) maxHull += 2;
        Hull = Math.Min(maxHull, Hull + (choice == 2 ? 4 : 1));
        foreach (var scrap in entities.GetEntitiesWithTag("siegeSalvage"))
            if (scrap.Active) TakeSalvage(scrap);
        StartWave();
    }

    private void CollectSalvage(double dt)
    {
        foreach (var scrap in entities.GetEntitiesWithTag("siegeSalvage"))
        {
            if (!scrap.Active) continue;
            var transform = scrap.GetComponent<CTransform>();
            Vec2 delta = Centre(player) - transform.Position;
            if (delta.Length() < 24) TakeSalvage(scrap);
            else if (delta.Length() < 190) transform.Position += delta.Normalize() * Math.Min(delta.Length(), dt * 360);
            transform.Rotation += 120 * dt;
        }
    }

    private void TakeSalvage(Entity scrap)
    {
        scrap.Active = false;
        Salvage++;
        Score += 25;
        if (Salvage % 5 == 0) Hull = Math.Min(maxHull, Hull + 1);
    }

    private void ClearShots()
    {
        foreach (var (entity, _) in entities.GetEntitiesWithComponents<Shot>()) entity.Active = false;
    }

    private void Burst(Vec2 position, int count)
    {
        if (sparks.Length == 0) return;
        for (int i = 0; i < count; i++)
        {
            var spark = sparks[sparkIndex++ % sparks.Length];
            var transform = spark.GetComponent<CTransform>();
            transform.Position = position;
            transform.Velocity = Rotate(new Vec2(1, 0), random.NextDouble() * Math.PI * 2) * (60 + random.NextDouble() * 180);
            spark.GetComponent<Spark>().Life = 0.25 + random.NextDouble() * 0.3;
            spark.GetComponent<CAnimation>().ShouldDraw = true;
        }
    }

    private void UpdateEffects(double dt)
    {
        foreach (var spark in sparks)
        {
            var state = spark.GetComponent<Spark>();
            state.Life -= dt;
            spark.GetComponent<CAnimation>().ShouldDraw = state.Life > 0;
            var transform = spark.GetComponent<CTransform>();
            double scale = Math.Max(0, state.Life * 2);
            transform.Scale = new Vec2(scale, scale);
            if (state.Life <= 0) transform.Velocity = Vec2.Zero;
        }
        pulseTime = Math.Max(0, pulseTime - dt);
        pulse.GetComponent<CAnimation>().ShouldDraw = pulseTime > 0;
        double pulseScale = 0.5 + (0.45 - pulseTime) * 7.2;
        pulse.GetComponent<CTransform>().Scale = new Vec2(pulseScale, pulseScale);
    }

    private void UpdateHud()
    {
        status.Text = $"HULL {Hull}/{maxHull}     WAVE {Wave}/5     SCORE {Score:000000}     SALVAGE {Salvage}";
        var boss = entities.GetEntitiesWithTag("siegeEnemy").FirstOrDefault(e => e.Active && e.GetComponent<CSiegeActor>().Kind == SiegeKind.Boss);
        bossStatus.Text = boss != null
            ? $"THE WARDEN  /  {boss.GetComponent<CSiegeActor>().Hull}/360  /  {(boss.GetComponent<CSiegeActor>().Hull <= 180 ? "OVERDRIVE" : "RADIAL BARRAGE") }"
            : $"DASH {(DashCooldown <= 0 ? "READY" : DashCooldown.ToString("0.0") + "s")}     EMP {(PulseCooldown <= 0 ? "READY" : PulseCooldown.ToString("0.0") + "s")}     Every 5 salvage repairs 1 hull";
        message.Text = Phase switch
        {
            SiegePhase.Ready => "SPACE  /  INITIATE BREACH",
            SiegePhase.Upgrade => "SECTOR CLEAR  /  CHOOSE AN UPGRADE:   F +VOLLEY    G +FIRE RATE / SPEED    H +ARMOR / REPAIR",
            SiegePhase.Victory => $"BREACH SEALED  /  VICTORY     {Score:000000} POINTS     R PLAY AGAIN    Q MENU",
            SiegePhase.Defeat => "SIGNAL LOST  /  R RESTART    Q MENU",
            _ => Wave == 5 ? "FINAL SECTOR  /  DESTROY THE WARDEN AND ITS ESCORT" : $"SECTOR {Wave:00}  /  CLEAR ALL HOSTILES"
        };
        message.Size = Phase == SiegePhase.Upgrade ? 19 : 23;
        detail.Text = Phase == SiegePhase.Upgrade
            ? "Choose once to continue. All uncollected salvage is recovered. Every upgrade repairs hull."
            : "WASD / ARROWS move    SPACE dash    E EMP    Auto-fire targets nearest enemy    R restart    Q menu";
    }

    private static Vec2 Centre(Entity entity) => entity.GetComponent<CTransform>().Position + (entity.TryGetComponent<CBoundingBox>()?.Size ?? Vec2.Zero) / 2;
    private static Vec2 Rotate(Vec2 v, double angle) => new(v.X * Math.Cos(angle) - v.Y * Math.Sin(angle), v.X * Math.Sin(angle) + v.Y * Math.Cos(angle));
}
