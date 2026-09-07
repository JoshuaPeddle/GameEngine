using System;
using System.Collections.Generic;
using GameEngine.Core;
using GameEngine.Core.Components;
using GameEngine.Demo.Emberbrook;

namespace GameEngine.Demo;

public sealed partial class SceneEmberbrook
{
    private readonly List<(Entity Entity, Vec2 Anchor, double Phase)> drifters = [];
    private double ambienceClock;

    private void BuildAmbience()
    {
        drifters.Clear();
        if (World.Region == Region.Village)
        {
            Sprite("emberSmoke", "smoke", Position(new Cell(3, 2)) + new Vec2(32, -30), new Vec2(40, 64), 18);
            Sprite("emberSmoke", "smoke", Position(new Cell(4, 11)) + new Vec2(32, -30), new Vec2(40, 64), 18);
            foreach (var cell in new[] { new Cell(6, 5), new Cell(14, 3), new Cell(16, 12) })
                AddDrifter("butterfly", cell);
        }
        else if (World.Region == Region.Forest)
        {
            foreach (var cell in new[] { new Cell(3, 3), new Cell(5, 10), new Cell(14, 5), new Cell(18, 12) })
                AddDrifter("mote", cell);
        }
        else
        {
            foreach (var cell in new[] { new Cell(8, 6), new Cell(16, 3), new Cell(16, 9), new Cell(23, 2) })
                Sprite("emberTorch", "torch", Position(cell), new Vec2(32, 40), 18);
        }
    }

    private void AddDrifter(string image, Cell cell)
    {
        var anchor = Position(cell);
        var sprite = Sprite("emberAmbient", image, anchor, new Vec2(32, 32), 18);
        drifters.Add((sprite, anchor, cell.X * 0.7 + cell.Y * 0.3));
    }

    private void UpdateAmbience(double dt)
    {
        ambienceClock = (ambienceClock + dt) % (Math.PI * 20);
        foreach (var (entity, anchor, phase) in drifters)
            entity.GetComponent<CTransform>().Position = anchor
                + new Vec2(Math.Sin(ambienceClock * 0.7 + phase) * 22, Math.Sin(ambienceClock * 1.1 + phase) * 10);
    }
}
