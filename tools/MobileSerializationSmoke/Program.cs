using System.Text.Json;
using GameEngine.Core.Utils;
using GameEngine.Demo.Emberbrook;

if (JsonSerializer.IsReflectionEnabledByDefault) throw new InvalidOperationException("Reflection serialization must be disabled.");
var directory = Path.Combine(Path.GetTempPath(), "ge-aot-" + Guid.NewGuid().ToString("N"));
try
{
    var level = new LevelBuilder("AOT round trip")
        .AddProperty("nested", new Dictionary<string, object> { ["values"] = new object[] { 7, true, "saved", 1.5, null! } })
        .AddEntity("player", e => e.AddTransform(3, 4, 45, 2, -1, 2).AddBoundingBox(8, 9).AddInput()
            .AddMovement().AddGravity().AddAnimation("player"))
        .AddEntity("label", e => e.AddText("Saved", 16))
        .AddEntity("camera", e => e.AddCamera(1, 2, 1.5)).Build();
    level.SaveToFile(Path.Combine(directory, "level.json"));
    var restored = LevelFile.LoadFromJson(File.ReadAllText(Path.Combine(directory, "level.json")));
    restored.Validate();
    if (restored.Entities.Count != 3 || restored.Metadata.Name != level.Metadata.Name ||
        ((JsonElement)restored.Metadata.Properties["nested"]).GetProperty("values")[0].GetInt32() != 7)
        throw new InvalidOperationException("Level round trip failed.");
    restored.SaveToFile(Path.Combine(directory, "level.json"));
    var save = new EmberbrookWorld().Capture() with { Coins = 50, MiningXp = 60, Ore = 4, BankedOre = 7 };
    var store = new EmberbrookFileSaveStore(Path.Combine(directory, "save.json"));
    store.Write(save);
    if (store.Read() != save) throw new InvalidOperationException("Save round trip failed.");
    if (JsonSerializer.Deserialize(JsonSerializer.Serialize(save, EmberbrookSaveJson.Default.EmberbrookSave),
        EmberbrookSaveJson.Default.EmberbrookSave) != save) throw new InvalidOperationException("Browser save round trip failed.");
    Console.WriteLine("PASS: NativeAOT level, metadata, component builder and demo save round trips with JSON reflection disabled.");
}
finally
{
    if (Directory.Exists(directory)) Directory.Delete(directory, true);
}
