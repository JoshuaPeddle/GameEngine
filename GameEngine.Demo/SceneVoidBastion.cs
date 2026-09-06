using System;
using System.Linq;
using GameEngine.Core;
using GameEngine.Core.Components;
using GameEngine.Core.Systems;
using SkiaSharp;

namespace GameEngine.Demo;

public sealed class SceneVoidBastion : Scene
{
    private sealed record EnemyView(Entity Body, Entity Track, Entity Hull, Entity Shield, Entity Slow);
    private static readonly string[] colors = ["#75FFF1", "#FFBD75", "#BBA1FF", "#ADFF95"];
    private static readonly string[] towerHints = ["Rapid fire / best against light drones", "Pierces armor and shields / long range", "Area slow / pairs well with Mortar", "Predictive shells / area damage"];
    private Assets assets = null!;
    private EntityManager entities = null!;
    private AudioSystem? audio;
    private Entity inputCarrier = null!;
    private Entity range = null!;
    private Entity resultPanel = null!;
    private CText resultTitle = null!;
    private CText resultStats = null!;
    private CText resultKeys = null!;
    private Entity[] turrets = [];
    private CText[] padLabels = [];
    private EnemyView[] enemyViews = [];
    private Entity[] effectViews = [];
    private Entity[] shellViews = [];
    private Entity[] shellTargets = [];
    private CText status = null!;
    private CText preview = null!;
    private CText selected = null!;
    private CText stats = null!;
    private CText upgrade = null!;
    private CText targeting = null!;
    private CText sell = null!;
    private CText launch = null!;
    private CText notice = null!;
    private CText ability = null!;
    private CText pause = null!;
    private CText speed = null!;
    private CText[] cards = [];
    private int lastKills;
    private BastionTowerKind blueprint;
    public override int VirtualWidth => 1280;
    public override int VirtualHeight => 800;
    public BastionBattle Battle { get; private set; } = new();
    public int SelectedPad { get; private set; }

    public override void Initialize(EntityManager entityManager, InputManager inputManager,
        AudioSystem? audioPlayer, Action<Scene?> ResetScene)
    {
        entities = entityManager;
        audio = audioPlayer;
        assets ??= new Assets("assets.json", AssetSource);
        Battle = new BastionBattle();
        SelectedPad = lastKills = 0;
        blueprint = BastionTowerKind.Pulse;
        Sprite("bastionMap", "map", new Vec2(640, 400), new Vec2(1280, 800), -100);
        Sprite("bastionCore", "core", BastionBattle.Route[^1], new Vec2(64, 64), 5);
        Label("bastionEntry", "INBOUND", new Vec2(45, 200), 12, "#FF839D");
        Label("bastionExit", "CORE", new Vec2(906, 611), 12, "#75FFF1");
        range = Sprite("bastionRange", "range", BastionBattle.Pads[0], new Vec2(512, 512), -5);
        turrets = new Entity[BastionBattle.Pads.Count];
        padLabels = new CText[turrets.Length];
        for (int i = 0; i < turrets.Length; i++)
        {
            var position = BastionBattle.Pads[i];
            Sprite("bastionSocket", "pad", position, new Vec2(56, 56), 0);
            turrets[i] = Sprite("bastionTower", "Pulse", position, new Vec2(52, 52), 5);
            padLabels[i] = Label("bastionSocketLabel", $"{i + 1:00}", position + new Vec2(-12, 43), 12, "#7F9CAF");
        }
        enemyViews = new EnemyView[BastionBattle.MaxEnemies];
        for (int i = 0; i < enemyViews.Length; i++)
            enemyViews[i] = new EnemyView(
                Sprite("bastionEnemy", "Drone", Vec2.Zero, new Vec2(32, 32), 10),
                Sprite("bastionHealthTrack", "trackBar", Vec2.Zero, new Vec2(32, 4), 16),
                Sprite("bastionHealth", "healthBar", Vec2.Zero, new Vec2(32, 4), 17),
                Sprite("bastionShield", "shieldBar", Vec2.Zero, new Vec2(32, 4), 17),
                Sprite("bastionSlow", "ring2", Vec2.Zero, new Vec2(40, 40), 9));
        effectViews = Enumerable.Range(0, BastionBattle.MaxEffects)
            .Select(_ => Sprite("bastionEffect", "beam0", Vec2.Zero, new Vec2(16, 6), 20)).ToArray();
        shellViews = Enumerable.Range(0, BastionBattle.MaxShells)
            .Select(_ => Sprite("bastionShell", "core", Vec2.Zero, new Vec2(12, 12), 23)).ToArray();
        shellTargets = Enumerable.Range(0, BastionBattle.MaxShells)
            .Select(_ => Sprite("bastionImpactTarget", "ring3", Vec2.Zero, new Vec2(48, 48), 3)).ToArray();
        Sprite("bastionHud", "hud", new Vec2(640, 400), new Vec2(1280, 800), 90);
        Label("bastionTitle", "VOID / BASTION", new Vec2(48, 61), 29, "#75FFF1");
        Label("bastionSubtitle", "HOLD THE LAST SIGNAL", new Vec2(49, 85), 13, "#7F9CAF");
        status = Label("bastionStatus", "", new Vec2(345, 52), 21, "#E3F5FF");
        preview = Label("bastionPreview", "", new Vec2(345, 83), 13, "#A6BBCF");
        pause = Label("bastionPause", "P / PAUSE", new Vec2(1100, 51), 13, "#A6BBCF");
        speed = Label("bastionSpeed", "B / 1X", new Vec2(1190, 51), 13, "#75FFF1");
        Label("bastionBuildTitle", "DEFENCE CONTROL", new Vec2(997, 142), 19, "#75FFF1");
        selected = Label("bastionSelection", "", new Vec2(997, 169), 16, "#FFFFFF");
        stats = Label("bastionStats", "", new Vec2(997, 190), 12, "#A6BBCF");
        cards = new CText[4];
        for (int i = 0; i < 4; i++)
        {
            var kind = (BastionTowerKind)i;
            string key = new[] { "A", "S", "D", "F" }[i];
            Sprite("bastionCardIcon", kind.ToString(), new Vec2(1017, 231 + i * 61), new Vec2(36, 36), 100);
            cards[i] = Label("bastionCard", $"{key}  {kind.ToString().ToUpperInvariant()}   {BastionBattle.Cost(kind)}c", new Vec2(1043, 227 + i * 61), 15, colors[i]);
            string hint = new[] { "Rapid / drones", "Piercing / armor", "Area slow / support", "Splash / clusters" }[i];
            Label("bastionCardHint", hint, new Vec2(1043, 247 + i * 61), 12, "#A6BBCF");
        }
        upgrade = Label("bastionUpgrade", "", new Vec2(1005, 499), 16, "#75FFF1");
        targeting = Label("bastionTargeting", "", new Vec2(1005, 554), 15, "#A6BBCF");
        sell = Label("bastionSell", "", new Vec2(1005, 606), 15, "#FFBD75");
        launch = Label("bastionLaunch", "", new Vec2(1005, 662), 16, "#75FFF1");
        notice = Label("bastionNotice", "", new Vec2(48, 727), 18, "#E1F6FF");
        ability = Label("bastionAbility", "", new Vec2(48, 752), 14, "#BBA1FF");
        Label("bastionControls", "CLICK socket + tower card   ARROWS select   A/S/D/F build   U upgrade   X sell   T target   SPACE wave   P pause   B speed   R restart   Q menu",
            new Vec2(48, 774), 12, "#8EAABE");
        resultPanel = Sprite("bastionResult", "result", new Vec2(480, 400), new Vec2(600, 180), 200);
        resultTitle = Label("bastionResultTitle", "", new Vec2(210, 370), 30, "#75FFF1", 210);
        resultStats = Label("bastionResultStats", "", new Vec2(210, 412), 20, "#E3F5FF", 210);
        resultKeys = Label("bastionResultKeys", "R / REBUILD       Q / RETURN TO MENU", new Vec2(210, 456), 17, "#A6BBCF", 210);
        inputCarrier = entities.CreateEntity("bastionInput");
        inputCarrier.AddComponent<CInput>();
        Bind(inputManager, GeKeys.Left, () => Select(SelectedPad - 1));
        Bind(inputManager, GeKeys.Right, () => Select(SelectedPad + 1));
        Bind(inputManager, GeKeys.Up, () => Select(SelectedPad - 3));
        Bind(inputManager, GeKeys.Down, () => Select(SelectedPad + 3));
        Bind(inputManager, GeKeys.A, () => Build(BastionTowerKind.Pulse));
        Bind(inputManager, GeKeys.S, () => Build(BastionTowerKind.Rail));
        Bind(inputManager, GeKeys.D, () => Build(BastionTowerKind.Cryo));
        Bind(inputManager, GeKeys.F, () => Build(BastionTowerKind.Mortar));
        Bind(inputManager, GeKeys.U, () => Battle.Upgrade(SelectedPad));
        Bind(inputManager, GeKeys.X, () => Battle.Sell(SelectedPad));
        Bind(inputManager, GeKeys.T, () => Battle.CycleTarget(SelectedPad));
        Bind(inputManager, GeKeys.Space, () => Battle.Launch());
        Bind(inputManager, GeKeys.E, () => Battle.IonStorm());
        Bind(inputManager, GeKeys.P, () => Battle.Paused = !Battle.Paused);
        Bind(inputManager, GeKeys.B, Battle.ToggleSpeed);
        Bind(inputManager, GeKeys.R, () => ResetScene(new SceneVoidBastion()));
        Bind(inputManager, GeKeys.Q, () => ResetScene(new SceneMenu()));
        inputManager.BindPointerAction(Pointer.PointerEventType.Press, e => Click(e.Position));
        inputManager.BindPointerAction(Pointer.PointerEventType.Move, e =>
        {
            if (e.Position.X >= 991 && e.Position.X <= 1244 && e.Position.Y >= 205 && e.Position.Y < 441)
                blueprint = (BastionTowerKind)Math.Clamp((int)((e.Position.Y - 205) / 61), 0, 3);
        });
        Redraw();
    }

    private void Bind(InputManager input, GeKeys key, Action action)
    {
        string name = "Bastion" + key;
        input.AddAction(key, name);
        input.ActionMapper.MapActionToComponent<CInput>(name, inputCarrier, (_, active) => { if (active) action(); }, oneShot: true);
    }

    private void Select(int index) => SelectedPad = (index + turrets.Length) % turrets.Length;
    private void Build(BastionTowerKind kind) { blueprint = kind; Battle.Build(SelectedPad, kind); }

    private void Click(Vec2 point)
    {
        for (int i = 0; i < BastionBattle.Pads.Count; i++)
            if (BastionBattle.Pads[i].DistanceTo(point) <= 30) { Select(i); return; }
        if (point.X >= 1100 && point.Y >= 25 && point.Y <= 62)
        {
            if (point.X < 1185) Battle.Paused = !Battle.Paused;
            else if (point.X <= 1250) Battle.ToggleSpeed();
            return;
        }
        if (point.X >= 48 && point.X <= 430 && point.Y >= 735 && point.Y <= 757) { Battle.IonStorm(); return; }
        if (point.X < 991 || point.X > 1244) return;
        for (int i = 0; i < 4; i++)
            if (point.Y >= 205 + i * 61 && point.Y <= 258 + i * 61) { Build((BastionTowerKind)i); return; }
        if (point.Y >= 473 && point.Y <= 513) Battle.Upgrade(SelectedPad);
        else if (point.Y >= 528 && point.Y <= 568) Battle.CycleTarget(SelectedPad);
        else if (point.Y >= 580 && point.Y <= 620) Battle.Sell(SelectedPad);
        else if (point.Y >= 638 && point.Y <= 674) Battle.Launch();
    }

    public override void Update(EntityManager entityManager, SystemContainer systems, double deltaSeconds)
    {
        Battle.Update(deltaSeconds);
        if (Battle.Kills > lastKills) audio?.Play("Hit", SoundType.SoundEffect);
        lastKills = Battle.Kills;
        Redraw();
    }

    private void Redraw()
    {
        for (int i = 0; i < turrets.Length; i++)
        {
            var tower = Battle.Towers[i];
            Show(turrets[i], tower != null);
            if (tower != null)
            {
                turrets[i].GetComponent<CAnimation>().Animation = assets.GetAnimation("Bastion" + tower.Kind, new Vec2(52, 52));
                var transform = turrets[i].GetComponent<CTransform>();
                transform.Rotation = tower.Rotation;
                double recoil = tower.Cooldown > BastionBattle.Interval(tower.Kind) * Math.Pow(0.88, tower.Level - 1) - 0.07 ? 0.9 : 1;
                transform.Scale = new Vec2(recoil, recoil);
            }
            padLabels[i].Text = $"{i + 1:00}" + (tower == null ? "" : " / " + new string('|', tower.Level));
            padLabels[i].Paint.Color = SKColor.Parse(i == SelectedPad ? "#75FFF1" : "#7F9CAF");
        }
        var selectedTower = Battle.Towers[SelectedPad];
        double radius = selectedTower?.Range ?? BastionBattle.Range(blueprint);
        range.GetComponent<CTransform>().Position = BastionBattle.Pads[SelectedPad];
        range.GetComponent<CTransform>().Scale = new Vec2(radius / 252, radius / 252);
        for (int i = 0; i < enemyViews.Length; i++)
        {
            var view = enemyViews[i];
            bool visible = i < Battle.Enemies.Count;
            Show(view.Body, visible); Show(view.Track, visible); Show(view.Hull, visible);
            Show(view.Shield, false); Show(view.Slow, false);
            if (!visible) continue;
            var enemy = Battle.Enemies[i];
            double size = enemy.Kind == BastionEnemyKind.Warden ? 56 : enemy.Kind == BastionEnemyKind.Runner ? 26 : 32;
            var transform = view.Body.GetComponent<CTransform>();
            transform.Position = enemy.Position;
            transform.Rotation = (BastionBattle.PointAt(enemy.Distance + 1) - enemy.Position).Angle * 180 / Math.PI + 90;
            double scale = enemy.HitTime > 0 ? 1.12 : 1;
            transform.Scale = new Vec2(scale, scale);
            view.Body.GetComponent<CAnimation>().Animation = assets.GetAnimation("Bastion" + enemy.Kind, new Vec2(size, size));
            Vec2 bar = enemy.Position + new Vec2(0, -size / 2 - 7);
            view.Track.GetComponent<CTransform>().Position = bar;
            double health = Math.Clamp(enemy.Hull / enemy.MaxHull, 0, 1);
            view.Hull.GetComponent<CTransform>().Position = bar - new Vec2(16 * (1 - health), 0);
            view.Hull.GetComponent<CTransform>().Scale = new Vec2(health, 1);
            Show(view.Shield, enemy.Shield > 0);
            view.Shield.GetComponent<CTransform>().Position = bar + new Vec2(0, -5);
            view.Shield.GetComponent<CTransform>().Scale = new Vec2(enemy.Shield / (30 + Battle.Wave * 6), 0.6);
            Show(view.Slow, enemy.SlowTime > 0);
            view.Slow.GetComponent<CTransform>().Position = enemy.Position;
        }
        for (int i = 0; i < effectViews.Length; i++)
        {
            var entity = effectViews[i];
            Show(entity, i < Battle.Effects.Count);
            if (i >= Battle.Effects.Count) continue;
            var effect = Battle.Effects[i];
            var transform = entity.GetComponent<CTransform>();
            if (effect.Impact)
            {
                entity.GetComponent<CAnimation>().Animation = assets.GetAnimation("Bastionring" + (int)effect.Kind);
                transform.Position = effect.To;
                double scale = (1 - effect.Life / effect.Duration) * 0.8 + 0.15;
                transform.Scale = new Vec2(scale, scale);
                transform.Rotation = 0;
            }
            else
            {
                entity.GetComponent<CAnimation>().Animation = assets.GetAnimation("Bastionbeam" + (int)effect.Kind);
                transform.Position = (effect.From + effect.To) / 2;
                transform.Rotation = (effect.To - effect.From).Angle * 180 / Math.PI;
                transform.Scale = new Vec2(effect.From.DistanceTo(effect.To) / 16, effect.Life / effect.Duration);
            }
        }
        for (int i = 0; i < shellViews.Length; i++)
        {
            bool visible = i < Battle.Shells.Count;
            Show(shellViews[i], visible); Show(shellTargets[i], visible);
            if (!visible) continue;
            var shell = Battle.Shells[i];
            double t = 1 - shell.Remaining / 0.45;
            shellViews[i].GetComponent<CTransform>().Position = shell.From + (shell.To - shell.From) * t - new Vec2(0, Math.Sin(t * Math.PI) * 65);
            shellTargets[i].GetComponent<CTransform>().Position = shell.To;
        }
        Show(resultPanel, Battle.Finished);
        resultTitle.ShouldDraw = resultStats.ShouldDraw = resultKeys.ShouldDraw = Battle.Finished;
        resultTitle.Text = Battle.Phase == BastionPhase.Victory ? "BASTION SECURED" : "SIGNAL LOST";
        resultStats.Text = $"WAVE {Battle.Wave}/12    CORE {Battle.CoreHull}/20    {Battle.Kills} HOSTILES DESTROYED";
        UpdateHud(selectedTower);
    }

    private void UpdateHud(BastionTower? tower)
    {
        status.Text = $"CORE {Battle.CoreHull}/20    CREDITS {Battle.Credits}    WAVE {Battle.Wave}/12";
        pause.Text = Battle.Paused ? "P / RESUME" : "P / PAUSE";
        speed.Text = $"B / {Battle.Speed}X";
        int next = Math.Min(12, Battle.Wave + (Battle.Phase == BastionPhase.Planning ? 1 : 0));
        var roster = BastionBattle.WaveRoster(Math.Max(1, next)).GroupBy(e => e);
        preview.Text = Battle.Phase == BastionPhase.Combat
            ? $"LIVE {Battle.Enemies.Count}   INBOUND {Battle.Incoming}   KILLS {Battle.Kills}   {(Battle.Wave % 4 == 0 ? "WARDEN CONVOY" : "DEFEND THE CORE") }"
            : $"NEXT {next:00} / " + string.Join("   ", roster.Select(g => $"{g.Count()} {g.Key.ToString().ToUpperInvariant()}"));
        if (Battle.Finished)
            preview.Text = Battle.Phase == BastionPhase.Victory ? "ALL CONVOYS DEFEATED / THE SIGNAL SURVIVES" : "CORE DESTROYED / REBUILD YOUR DEFENCE";
        selected.Text = $"SOCKET {SelectedPad + 1:00} / " + (tower == null ? "EMPTY" : $"{tower.Kind.ToString().ToUpperInvariant()} {tower.Level}");
        stats.Text = tower == null ? $"Preview {blueprint} / range {BastionBattle.Range(blueprint)}" : $"DMG {BastionBattle.Damage(tower.Kind) * (1 + (tower.Level - 1) * 0.7):0} / RNG {tower.Range} / TIER {tower.Level}";
        upgrade.Text = tower == null ? "U / SELECT A TOWER" : tower.Level == 3 ? "MAXIMUM TIER" : $"U / UPGRADE   {tower.UpgradeCost}c";
        targeting.Text = "T / " + (tower == null ? "TARGET PRIORITY" : $"TARGET: {tower.Target.ToString().ToUpperInvariant()}");
        sell.Text = tower == null ? "X / SELL TOWER" : $"X / SELL   +{tower.Refund}c";
        launch.Text = Battle.Finished ? (Battle.Phase == BastionPhase.Victory ? "BASTION SECURED" : "BASTION LOST") : Battle.Phase == BastionPhase.Combat ? "WAVE IN PROGRESS" : $"SPACE / WAVE {Battle.Wave + 1:00}";
        notice.Text = Battle.Paused && !Battle.Finished ? "PAUSED / Plan, build or upgrade. Press P to resume." : Battle.Notice;
        ability.Text = $"E / ION {(Battle.IonCooldown <= 0 ? "READY" : Battle.IonCooldown.ToString("0.0") + "s")}  (strip shields + slow convoy)     " + towerHints[(int)(tower?.Kind ?? blueprint)];
        for (int i = 0; i < cards.Length; i++)
            cards[i].Paint.Color = SKColor.Parse(tower == null && Battle.Credits >= BastionBattle.Cost((BastionTowerKind)i) ? colors[i] : "#718799");
    }

    private Entity Sprite(string tag, string name, Vec2 position, Vec2 size, int layer)
    {
        var entity = entities.CreateEntity(tag);
        entity.AddComponent(new CTransform(position) { Layer = layer });
        entity.AddComponent(new CAnimation(assets.GetAnimation("Bastion" + name, size)));
        return entity;
    }

    private CText Label(string tag, string value, Vec2 position, int size, string color, int layer = 100)
    {
        var entity = entities.CreateEntity(tag);
        entity.AddComponent(new CTransform(position) { Layer = layer });
        var text = new CText(value, size) { TextAlign = SKTextAlign.Left };
        text.Paint.Color = SKColor.Parse(color);
        entity.AddComponent(text);
        return text;
    }

    private static void Show(Entity entity, bool visible) => entity.GetComponent<CAnimation>().ShouldDraw = visible;
}
