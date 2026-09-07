using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Nodes;

namespace GameEngine.Demo.Emberbrook;

public sealed record EmberbrookSave
{
    [JsonRequired]
    public int Version { get; init; } = 6;
    [JsonRequired]
    public int BridgeStage { get; init; }
    [JsonRequired]
    public bool HasTool { get; init; }
    [JsonRequired]
    public bool HasSpear { get; init; }
    [JsonRequired]
    public bool SpearEquipped { get; init; }
    [JsonRequired]
    public int SmokedTrout { get; init; }
    [JsonRequired]
    public int BankSmokedTrout { get; init; }
    [JsonRequired]
    public int WoodcuttingXp { get; init; }
    [JsonRequired]
    public int Logs { get; init; }
    [JsonRequired]
    public int TrailQuestStage { get; init; }
    [JsonRequired]
    public int LitBeacons { get; init; }
    [JsonRequired]
    public int BankBars { get; init; }
    [JsonRequired]
    public int BankRations { get; init; }
    [JsonRequired]
    public int BankTrout { get; init; }
    [JsonRequired]
    public int BankMeals { get; init; }
    [JsonRequired]
    public int BankLogs { get; init; }
    [JsonRequired]
    public int FishingXp { get; init; }
    [JsonRequired]
    public int CookingXp { get; init; }
    [JsonRequired]
    public int RiverQuestStage { get; init; }
    [JsonRequired]
    public bool HasPearl { get; init; }
    [JsonRequired]
    public RiverCharm Charm { get; init; }
    [JsonRequired]
    public int Trout { get; init; }
    [JsonRequired]
    public int Meals { get; init; }
    [JsonRequired]
    public Region Region { get; init; }
    [JsonRequired]
    public int RuinQuestStage { get; init; }
    [JsonRequired]
    public bool HasShield { get; init; }
    [JsonRequired]
    public bool GuardianDefeated { get; init; }
    [JsonRequired]
    public int X { get; init; }
    [JsonRequired]
    public int Y { get; init; }
    [JsonRequired]
    public int Hull { get; init; }
    [JsonRequired]
    public int Coins { get; init; }
    [JsonRequired]
    public int MiningXp { get; init; }
    [JsonRequired]
    public int CombatXp { get; init; }
    [JsonRequired]
    public int QuestStage { get; init; }
    [JsonRequired]
    public int Kills { get; init; }
    [JsonRequired]
    public bool HasSword { get; init; }
    [JsonRequired]
    public int BankedOre { get; init; }
    [JsonRequired]
    public int Ore { get; init; }
    [JsonRequired]
    public int Bars { get; init; }
    [JsonRequired]
    public int Rations { get; init; }

    [JsonRequired]
    public int AcceptedRequest { get; init; } = -1;
    [JsonRequired]
    public int CompletedRequests { get; init; }
    [JsonRequired]
    public int Discovered { get; init; }
    [JsonRequired]
    public bool FloatShown { get; init; }
    [JsonRequired]
    public bool KeeperAwarded { get; init; }
    [JsonRequired]
    public bool JettyBuilt { get; init; }
    [JsonRequired]
    public bool ReturnGateBuilt { get; init; }
    [JsonRequired]
    public bool HomecomingSeen { get; init; }

    public void Validate()
    {
        if (AcceptedRequest is < -1 or > 3 || CompletedRequests is < 0 or > 15 || Discovered is < 0 or > 63
            || AcceptedRequest >= 0 && (CompletedRequests & (1 << AcceptedRequest)) != 0
            || (AcceptedRequest == 1 || (CompletedRequests & 2) != 0) && QuestStage != 2
            || (AcceptedRequest == 2 || (CompletedRequests & 4) != 0) && (RiverQuestStage != 2 || BridgeStage != 2)
            || (AcceptedRequest == 3 || (CompletedRequests & 8) != 0) && TrailQuestStage != 3
            || (Discovered & 8) != 0 && RuinQuestStage == 0
            || (Discovered & 48) != 0 && TrailQuestStage == 0
            || (Discovered & 16) != 0 && WoodcuttingXp < 60
            || FloatShown && (Discovered & 2) == 0 || KeeperAwarded && Discovered != 63
            || JettyBuilt && BridgeStage != 2 || (ReturnGateBuilt || HomecomingSeen) && TrailQuestStage != 3
            || Version != 6 || BridgeStage is < 0 or > 2 || BridgeStage > 0 && QuestStage < 2
            || SpearEquipped && !HasSpear || SmokedTrout is < 0 or > 24 || BankSmokedTrout is < 0 or > 1000000 || WoodcuttingXp is < 0 or > 1000000 || Logs is < 0 or > 60
            || TrailQuestStage is < 0 or > 3 || LitBeacons is < 0 or > 7
            || TrailQuestStage > 0 && RiverQuestStage != 2 || LitBeacons > 0 && TrailQuestStage == 0
            || TrailQuestStage >= 2 && LitBeacons != 7 || Region == Region.Forest && QuestStage < 2
            || BankBars is < 0 or > 1000000 || BankRations is < 0 or > 1000000 || BankTrout is < 0 or > 1000000
            || BankMeals is < 0 or > 1000000 || BankLogs is < 0 or > 1000000 || FishingXp is < 0 or > 1000000 || CookingXp is < 0 or > 1000000
            || RiverQuestStage is < 0 or > 2 || RiverQuestStage > 0 && RuinQuestStage != 3
            || HasPearl && (FishingXp < 50 || RiverQuestStage == 2)
            || Charm is not (RiverCharm.None or RiverCharm.Might or RiverCharm.Shelter)
            || (RiverQuestStage == 2) != (Charm != RiverCharm.None)
            || Trout is < 0 or > 60 || Meals is < 0 or > 12 || Region is not (Region.Village or Region.Ruins or Region.Forest) || RuinQuestStage is < 0 or > 3
            || RuinQuestStage > 0 && QuestStage != 2 || Region == Region.Ruins && RuinQuestStage == 0
            || RuinQuestStage >= 2 && !GuardianDefeated || GuardianDefeated && RuinQuestStage == 0
            || HasShield && !HasSword || Hull is < 1 or > 20 || QuestStage is < 0 or > 2
            || Coins is < 0 or > 1000000 || MiningXp is < 0 or > 1000000 || CombatXp is < 0 or > 1000000
            || Kills is < 0 or > 1000000 || BankedOre is < 0 or > 1000000
            || Ore is < 0 or > 60 || Bars is < 0 or > 12 || Rations is < 0 or > 12
            || EmberbrookWorld.Slots(Item.Ore, Ore) + Bars + Rations + EmberbrookWorld.Slots(Item.Trout, Trout) + Meals
                + EmberbrookWorld.Slots(Item.Log, Logs) + EmberbrookWorld.Slots(Item.SmokedTrout, SmokedTrout) > EmberbrookWorld.PackCapacity)
            throw new InvalidDataException("Save contains unsupported or out-of-range progress.");
    }
}

public interface IEmberbrookSaveStore
{
    void Write(EmberbrookSave save);
    EmberbrookSave Read();
}

public sealed class EmberbrookFileSaveStore(string path) : IEmberbrookSaveStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static IEmberbrookSaveStore Default() => new EmberbrookFileSaveStore(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GameEngine", "Emberbrook", "save.json"));

    public void Write(EmberbrookSave save)
    {
        if (OperatingSystem.IsBrowser()) throw new NotSupportedException("Disk saves are available in the desktop runner.");
        save.Validate();
        string fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        string temporary = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(save, Options));
            File.Move(temporary, fullPath, true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    public EmberbrookSave Read()
    {
        if (OperatingSystem.IsBrowser()) throw new NotSupportedException("Disk saves are available in the desktop runner.");
        var root = JsonNode.Parse(File.ReadAllText(path)) as JsonObject
            ?? throw new InvalidDataException("Save must be an object.");
        if (root["Version"] is not JsonValue version || !version.TryGetValue<int>(out int number) || number != 6)
            throw new InvalidDataException("This adventure uses an older or unsupported save. Start a new game.");
        var save = root.Deserialize<EmberbrookSave>(Options)
            ?? throw new InvalidDataException("Save is empty.");
        save.Validate();
        return save;
    }
}
