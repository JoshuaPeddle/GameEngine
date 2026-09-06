using System;
using System.Collections.Generic;
using System.Linq;
using GameEngine.Core;

namespace GameEngine.Demo;

public enum BastionPhase { Planning, Combat, Victory, Defeat }
public enum BastionTowerKind { Pulse, Rail, Cryo, Mortar }
public enum BastionEnemyKind { Drone, Runner, Armor, Shield, Warden }
public enum BastionTarget { First, Strongest, Nearest }

public sealed class BastionTower
{
    public BastionTowerKind Kind { get; init; }
    public int Level { get; internal set; } = 1;
    public int Investment { get; internal set; }
    public double Cooldown { get; internal set; }
    public double Rotation { get; internal set; }
    public BastionTarget Target { get; internal set; }
    public double Range => BastionBattle.Range(Kind) + (Level - 1) * 18;
    public int UpgradeCost => Level >= 3 ? 0 : BastionBattle.Cost(Kind) * Level * 3 / 4;
    public int Refund => Investment * 7 / 10;
}

public sealed class BastionEnemy
{
    public BastionEnemyKind Kind { get; init; }
    public double Distance { get; internal set; }
    public double Hull { get; internal set; }
    public double MaxHull { get; init; }
    public double Shield { get; internal set; }
    public double SlowTime { get; internal set; }
    public double HitTime { get; internal set; }
    public Vec2 Position => BastionBattle.PointAt(Distance);
    public double Speed => Kind switch { BastionEnemyKind.Runner => 110, BastionEnemyKind.Armor => 44, BastionEnemyKind.Warden => 32, _ => 66 };
    public int Bounty => Kind switch { BastionEnemyKind.Warden => 90, BastionEnemyKind.Armor => 16, BastionEnemyKind.Shield => 13, _ => 9 };
}

public sealed class BastionEffect
{
    public Vec2 From { get; init; }
    public Vec2 To { get; init; }
    public BastionTowerKind Kind { get; init; }
    public double Life { get; internal set; }
    public double Duration { get; init; }
    public bool Impact { get; init; }
}

public sealed class BastionShell
{
    public Vec2 From { get; init; }
    public Vec2 To { get; init; }
    public double Remaining { get; internal set; } = 0.45;
    public double Damage { get; init; }
    public double Radius { get; init; }
}

public sealed class BastionBattle
{
    public const int WaveCount = 12;
    public const int MaxEnemies = 64;
    public const int MaxEffects = 96;
    public const int MaxShells = 48;
    private static readonly Vec2[] route = [new(40, 240), new(280, 240), new(280, 560), new(500, 560), new(500, 190), new(740, 190), new(740, 560), new(925, 560)];
    private static readonly Vec2[] pads = [new(140, 165), new(155, 335), new(185, 475), new(375, 335), new(390, 460), new(385, 625), new(590, 285), new(600, 450), new(600, 615), new(845, 145), new(845, 305), new(840, 445), new(840, 630), new(645, 365), new(390, 155)];
    public static IReadOnlyList<Vec2> Route { get; } = Array.AsReadOnly(route);
    public static IReadOnlyList<Vec2> Pads { get; } = Array.AsReadOnly(pads);
    public static double RouteLength { get; } = Enumerable.Range(1, route.Length - 1).Sum(i => route[i].DistanceTo(route[i - 1]));
    private readonly BastionTower?[] towers = new BastionTower?[pads.Length];
    private readonly List<BastionEnemy> enemies = [];
    private readonly List<BastionEffect> effects = [];
    private readonly List<BastionShell> shells = [];
    private readonly Queue<BastionEnemyKind> incoming = new();
    private double spawnTimer;
    private double accumulator;
    public IReadOnlyList<BastionTower?> Towers { get; }
    public IReadOnlyList<BastionEnemy> Enemies { get; }
    public IReadOnlyList<BastionEffect> Effects { get; }
    public IReadOnlyList<BastionShell> Shells { get; }
    public BastionPhase Phase { get; private set; }
    public int Credits { get; private set; } = 270;
    public int CoreHull { get; private set; } = 20;
    public int Wave { get; private set; }
    public int Kills { get; private set; }
    public int Incoming => incoming.Count;
    public bool Paused { get; set; }
    public int Speed { get; private set; } = 1;
    public double IonCooldown { get; private set; }
    public string Notice { get; private set; } = "Select a socket, build a tower, then launch wave 1.";
    public bool Finished => Phase is BastionPhase.Victory or BastionPhase.Defeat;

    public BastionBattle()
    {
        Towers = Array.AsReadOnly(towers);
        Enemies = enemies.AsReadOnly();
        Effects = effects.AsReadOnly();
        Shells = shells.AsReadOnly();
    }

    public static int Cost(BastionTowerKind kind) => kind switch { BastionTowerKind.Pulse => 70, BastionTowerKind.Rail => 115, BastionTowerKind.Cryo => 90, _ => 135 };
    public static double Range(BastionTowerKind kind) => kind switch { BastionTowerKind.Pulse => 165, BastionTowerKind.Rail => 245, BastionTowerKind.Cryo => 155, _ => 215 };
    public static double Damage(BastionTowerKind kind) => kind switch { BastionTowerKind.Pulse => 9, BastionTowerKind.Rail => 42, BastionTowerKind.Cryo => 5, _ => 34 };
    public static double Interval(BastionTowerKind kind) => kind switch { BastionTowerKind.Pulse => 0.35, BastionTowerKind.Rail => 1.25, BastionTowerKind.Cryo => 0.8, _ => 1.65 };

    public bool Build(int pad, BastionTowerKind kind)
    {
        if (Finished || !Enum.IsDefined(kind) || pad < 0 || pad >= towers.Length) return false;
        if (towers[pad] != null) { Notice = "Socket occupied. Upgrade it, or sell before rebuilding."; return false; }
        if (Credits < Cost(kind)) { Notice = $"Need {Cost(kind) - Credits} more credits to build {kind}."; return false; }
        Credits -= Cost(kind);
        towers[pad] = new BastionTower { Kind = kind, Investment = Cost(kind) };
        Notice = $"{kind} online at socket {pad + 1:00}.";
        return true;
    }

    public bool Upgrade(int pad)
    {
        if (Finished || pad < 0 || pad >= towers.Length || towers[pad] is not { } tower) return false;
        if (tower.Level == 3) { Notice = "Maximum tier reached."; return false; }
        if (Credits < tower.UpgradeCost) { Notice = $"Need {tower.UpgradeCost - Credits} more credits to upgrade."; return false; }
        Credits -= tower.UpgradeCost;
        tower.Investment += tower.UpgradeCost;
        tower.Level++;
        Notice = $"{tower.Kind} upgraded to tier {tower.Level}: more damage, range and fire rate.";
        return true;
    }

    public bool Sell(int pad)
    {
        if (Finished || pad < 0 || pad >= towers.Length || towers[pad] is not { } tower) return false;
        Credits += tower.Refund;
        towers[pad] = null;
        Notice = $"Tower recycled for {tower.Refund} credits.";
        return true;
    }

    public void CycleTarget(int pad)
    {
        if (!Finished && pad >= 0 && pad < towers.Length && towers[pad] is { } tower)
            tower.Target = (BastionTarget)(((int)tower.Target + 1) % 3);
    }

    public void ToggleSpeed() => Speed = Speed == 1 ? 2 : 1;

    public bool Launch()
    {
        if (Phase != BastionPhase.Planning) return false;
        if (towers.All(t => t == null)) { Notice = "Build at least one tower before launching."; return false; }
        Wave++;
        foreach (var kind in WaveRoster(Wave)) incoming.Enqueue(kind);
        spawnTimer = 0.5;
        accumulator = 0;
        IonCooldown = 0;
        Paused = false;
        Phase = BastionPhase.Combat;
        Notice = Wave % 4 == 0 ? "WARDEN DETECTED. Rail guns pierce its armor." : "Convoy inbound. Build and upgrade during the assault.";
        return true;
    }

    public static IReadOnlyList<BastionEnemyKind> WaveRoster(int wave)
    {
        var roster = new List<BastionEnemyKind>();
        for (int i = 0; i < 7 + wave * 2; i++)
            roster.Add(wave >= 3 && i % 5 == 3 ? BastionEnemyKind.Shield : wave >= 2 && i % 4 == 2 ? BastionEnemyKind.Armor : i % 3 == 1 ? BastionEnemyKind.Runner : BastionEnemyKind.Drone);
        if (wave % 4 == 0) roster.Insert(roster.Count / 2, BastionEnemyKind.Warden);
        return roster;
    }

    public bool IonStorm()
    {
        if (Phase != BastionPhase.Combat || Paused || IonCooldown > 0 || enemies.Count == 0) return false;
        IonCooldown = 25;
        foreach (var enemy in enemies)
        {
            enemy.Shield = 0;
            enemy.SlowTime = 3;
            Hit(enemy, 18, true);
            Effect(enemy.Position, enemy.Position, BastionTowerKind.Cryo, true);
        }
        Notice = "ION STORM: shields stripped, convoy slowed for 3 seconds.";
        return true;
    }

    public void Update(double deltaSeconds)
    {
        if (!double.IsFinite(deltaSeconds) || deltaSeconds < 0) throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
        if (Paused || Finished) return;
        accumulator += deltaSeconds * Speed;
        while (accumulator + 1e-10 >= 1.0 / 60)
        {
            accumulator = Math.Max(0, accumulator - 1.0 / 60);
            Step(1.0 / 60);
            if (Phase != BastionPhase.Combat) { accumulator = 0; break; }
        }
    }

    private void Step(double dt)
    {
        foreach (var effect in effects) effect.Life -= dt;
        effects.RemoveAll(e => e.Life <= 0);
        if (Phase != BastionPhase.Combat) return;
        IonCooldown = Math.Max(0, IonCooldown - dt);
        spawnTimer -= dt;
        if (spawnTimer <= 0 && incoming.Count > 0 && enemies.Count < MaxEnemies)
        {
            var kind = incoming.Dequeue();
            double hull = kind switch { BastionEnemyKind.Runner => 24, BastionEnemyKind.Armor => 95, BastionEnemyKind.Shield => 52, BastionEnemyKind.Warden => 1100, _ => 42 };
            hull *= 1 + (Wave - 1) * 0.24 + Math.Pow(Math.Max(0, Wave - 4), 2) * 0.035;
            enemies.Add(new BastionEnemy { Kind = kind, Hull = hull, MaxHull = hull, Shield = kind == BastionEnemyKind.Shield ? 30 + Wave * 6 : 0 });
            spawnTimer += Math.Max(0.4, 0.9 - Wave * 0.025);
        }
        foreach (var enemy in enemies)
        {
            if (enemy.Hull <= 0) continue;
            enemy.HitTime = Math.Max(0, enemy.HitTime - dt);
            enemy.Distance += enemy.Speed * (enemy.SlowTime > 0 ? 0.48 : 1) * dt;
            enemy.SlowTime = Math.Max(0, enemy.SlowTime - dt);
            if (enemy.Distance < RouteLength) continue;
            CoreHull = Math.Max(0, CoreHull - (enemy.Kind == BastionEnemyKind.Warden ? 6 : 1));
            enemy.Hull = 0;
            Notice = "CORE BREACHED. Reinforce the exit corridor.";
            if (CoreHull == 0)
            {
                Phase = BastionPhase.Defeat;
                shells.Clear();
                effects.Clear();
                Notice = "BASTION LOST. Press R to rebuild or Q for the menu.";
                return;
            }
        }
        foreach (var shell in shells)
        {
            shell.Remaining -= dt;
            if (shell.Remaining > 0) continue;
            foreach (var enemy in enemies)
                if (enemy.Hull > 0 && enemy.Position.DistanceTo(shell.To) <= shell.Radius) Hit(enemy, shell.Damage, false);
            Effect(shell.To, shell.To, BastionTowerKind.Mortar, true);
        }
        shells.RemoveAll(s => s.Remaining <= 0);
        for (int i = 0; i < towers.Length; i++)
        {
            if (towers[i] is not { } tower) continue;
            tower.Cooldown = Math.Max(0, tower.Cooldown - dt);
            var target = TargetFor(i);
            if (target == null) continue;
            tower.Rotation = (target.Position - pads[i]).Angle * 180 / Math.PI + 90;
            if (tower.Cooldown > 0) continue;
            tower.Cooldown = Interval(tower.Kind) * Math.Pow(0.88, tower.Level - 1);
            Fire(i, tower, target);
        }
        enemies.RemoveAll(e => e.Hull <= 0);
        if (incoming.Count == 0 && enemies.Count == 0)
        {
            Phase = Wave == WaveCount ? BastionPhase.Victory : BastionPhase.Planning;
            Credits += 35 + Wave * 7;
            CoreHull = Math.Min(20, CoreHull + 1);
            shells.Clear();
            Notice = Phase == BastionPhase.Victory ? "BASTION SECURED. All twelve convoys defeated. R replay / Q menu." : $"Wave {Wave} cleared. +{35 + Wave * 7} credits, +1 core hull. Prepare the next defence.";
        }
    }

    public BastionEnemy? TargetFor(int pad)
    {
        if (pad < 0 || pad >= towers.Length || towers[pad] is not { } tower) return null;
        BastionEnemy? best = null;
        double bestValue = double.NegativeInfinity;
        foreach (var enemy in enemies)
        {
            double distance = enemy.Position.DistanceTo(pads[pad]);
            if (enemy.Hull <= 0 || distance > tower.Range) continue;
            double value = tower.Target switch { BastionTarget.Strongest => enemy.Hull + enemy.Shield, BastionTarget.Nearest => -distance, _ => enemy.Distance };
            if (value <= bestValue) continue;
            bestValue = value;
            best = enemy;
        }
        return best;
    }

    private void Fire(int pad, BastionTower tower, BastionEnemy target)
    {
        double damage = Damage(tower.Kind) * (1 + (tower.Level - 1) * 0.7);
        if (tower.Kind == BastionTowerKind.Mortar)
        {
            if (shells.Count < MaxShells)
                shells.Add(new BastionShell { From = pads[pad], To = PointAt(target.Distance + target.Speed * (target.SlowTime > 0 ? 0.48 : 1) * 0.45), Damage = damage, Radius = 65 + tower.Level * 5 });
        }
        else if (tower.Kind == BastionTowerKind.Cryo)
        {
            foreach (var enemy in enemies)
            {
                if (enemy.Hull <= 0 || enemy.Position.DistanceTo(target.Position) > 58) continue;
                enemy.SlowTime = Math.Max(enemy.SlowTime, 1.4 + tower.Level * 0.25);
                Hit(enemy, damage, false);
            }
            Effect(target.Position, target.Position, tower.Kind, true);
        }
        else Hit(target, damage, tower.Kind == BastionTowerKind.Rail);
        Effect(pads[pad], target.Position, tower.Kind, false);
    }

    private void Hit(BastionEnemy enemy, double damage, bool pierce)
    {
        if (enemy.Hull <= 0) return;
        enemy.HitTime = 0.12;
        if (!pierce)
        {
            double absorbed = Math.Min(enemy.Shield, damage);
            enemy.Shield -= absorbed;
            damage -= absorbed;
            if (damage <= 0) return;
            if (enemy.Kind is BastionEnemyKind.Armor or BastionEnemyKind.Warden) damage = Math.Max(1, damage - 6);
        }
        enemy.Hull -= damage;
        if (enemy.Hull > 0) return;
        Credits += enemy.Bounty;
        Kills++;
        Effect(enemy.Position, enemy.Position, BastionTowerKind.Pulse, true);
    }

    private void Effect(Vec2 from, Vec2 to, BastionTowerKind kind, bool impact)
    {
        if (effects.Count >= MaxEffects) effects.RemoveAt(0);
        double duration = impact ? 0.32 : kind == BastionTowerKind.Rail ? 0.2 : 0.1;
        effects.Add(new BastionEffect { From = from, To = to, Kind = kind, Impact = impact, Life = duration, Duration = duration });
    }

    public static Vec2 PointAt(double distance)
    {
        distance = Math.Max(0, distance);
        for (int i = 1; i < route.Length; i++)
        {
            double length = route[i].DistanceTo(route[i - 1]);
            if (distance <= length) return route[i - 1] + (route[i] - route[i - 1]) * (distance / length);
            distance -= length;
        }
        return route[^1];
    }
}
