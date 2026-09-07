using System;
using System.Linq;

namespace GameEngine.Demo.Emberbrook;

public sealed partial class EmberbrookWorld
{
    public int WoodcuttingXp { get; private set; }
    public int WoodcuttingLevel => 1 + WoodcuttingXp / 30;
    public int TrailQuestStage { get; private set; }
    public int LitBeacons { get; private set; }
    public int BeaconCount => ((LitBeacons & 1) != 0 ? 1 : 0) + ((LitBeacons & 2) != 0 ? 1 : 0) + ((LitBeacons & 4) != 0 ? 1 : 0);
    public bool HartDefeated => TrailQuestStage >= 2;
    public bool BeaconIsLit(WorldSite site) => (LitBeacons & (1 << int.Parse(site.Id[^1..]))) != 0;
    private string TrailQuest => TrailQuestStage switch
    {
        0 => "Feast complete. Speak to Elin about the Ashen Trail.",
        1 => $"Light the forest beacons ({BeaconCount}/3), then defeat the Briar Hart.",
        2 => "The trail is safe. Return to Elin for your reward.",
        _ => "Emberbrook is safe. Join Elin for the homecoming, or explore unfinished village work."
    };

    private void InteractTrailQuest()
    {
        if (TrailQuestStage == 0)
        {
            TrailQuestStage = 1;
            Message = "Elin: Light three forest beacons with 2 ash logs each, then defeat the Briar Hart. The southern trail is open.";
        }
        else if (TrailQuestStage == 2)
        {
            TrailQuestStage = 3;
            Coins += 120;
            CombatXp += 60;
            Message = "The Ashen Trail complete! +120 coins / +60 Combat XP. The forest roads are safe again.";
        }
        else Message = TrailQuestStage == 3 ? "Elin: You have restored our village, bell, feast, and forest. Thank you!"
            : "Elin: Cut six ash logs and light three beacons. The Hart loses its protection when all three burn.";
    }

    private void CutWood(WorldSite site)
    {
        if (!Give(Item.Log)) { Message = "Your pack is full. Use ash logs at beacons or bank them in the village."; Stop(); return; }
        int level = WoodcuttingLevel;
        WoodcuttingXp += 10;
        site.RespawnSeconds = 4;
        Message = $"+1 ash log / +10 Woodcutting XP.{(WoodcuttingLevel > level ? $" Woodcutting level {WoodcuttingLevel}!" : "")}";
        if (!RepeatGathering) Stop();
    }

    private void LightBeacon(WorldSite site)
    {
        if (TrailQuestStage == 0) { Message = "Elin has not asked you to light the beacons yet. Keep these logs for the crossing."; Stop(); return; }
        if (BeaconIsLit(site)) Message = "This beacon already burns brightly.";
        else if (Count(Item.Log) < 2) Message = "This beacon needs two ash logs. Click an ash tree to cut wood.";
        else
        {
            Take(Item.Log, 2);
            LitBeacons |= 1 << int.Parse(site.Id[^1..]);
            Message = BeaconCount == 3 ? "All three beacons are lit! The Briar Hart's roots weaken."
                : $"Beacon lit / {BeaconCount}/3. Its flame will persist when you leave the forest.";
        }
        Stop();
    }

    private static bool ForestWater(Cell cell) => cell.X is 9 or 10 && cell.Y is >= 2 and <= 5;
    private bool ForestTree(Cell cell) => (cell.X == 7 && cell.Y is >= 1 and <= 13 && cell.Y != 8 && !(cell.Y == 2 && LostWayOpen))
        || cell.X == 12 && cell.Y is >= 2 and <= 12 && cell.Y is not (4 or 8 or 11)
        || cell.Y == 12 && cell.X is >= 3 and <= 10;

    private void UpdateHart(double dt)
    {
        if (HartDefeated || BeaconCount < 3) return;
        var hart = Sites.Single(s => s.Kind == SiteKind.Hart);
        if (DangerCentre != null)
        {
            DangerSeconds = Math.Max(0, DangerSeconds - dt);
            if (DangerSeconds > 0) return;
            bool hit = IsDangerous(Player);
            DangerCentre = null;
            if (hit) { Message = "Briar roots strike! Move diagonally away from the marked cross."; Hurt(9); }
            else Message = "You evaded the roots. The Hart is exposed!";
            guardianClock = hart.Hull <= 40 ? 1.3 : 2.2;
        }
        else if (hart.Cell.Distance(Player) <= 7)
        {
            guardianClock -= dt;
            if (guardianClock <= 0)
            {
                DangerCentre = Player;
                DangerSeconds = 1.5;
                Message = "Roots gather beneath you! Leave the marked row AND column before they erupt.";
            }
        }
    }
}
