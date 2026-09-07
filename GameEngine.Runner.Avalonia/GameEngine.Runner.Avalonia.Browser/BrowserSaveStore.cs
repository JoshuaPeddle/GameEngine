using System.IO;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Text.Json.Serialization;
using GameEngine.Demo.Emberbrook;

[SupportedOSPlatform("browser")]
internal sealed partial class BrowserSaveStore : IEmberbrookSaveStore
{
    private const string Key = "gameengine.emberbrook.save";

    [JSImport("globalThis.localStorage.getItem")]
    private static partial string? GetItem(string key);
    [JSImport("globalThis.localStorage.setItem")]
    private static partial void SetItem(string key, string value);

    public void Write(EmberbrookSave save)
    {
        save.Validate();
        try { SetItem(Key, JsonSerializer.Serialize(save, BrowserSaveJson.Default.EmberbrookSave)); }
        catch (JSException ex) { throw new IOException("Browser storage is unavailable or full. Allow site storage and retry.", ex); }
    }

    public EmberbrookSave Read()
    {
        string? json;
        try { json = GetItem(Key); }
        catch (JSException ex) { throw new IOException("Browser storage is unavailable. Allow site storage and retry.", ex); }
        if (json == null) throw new InvalidDataException("No adventure saved in this browser yet. Start a new adventure.");
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.TryGetProperty("Version", out var version) || version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out int number) || number != 6)
            throw new InvalidDataException("This adventure uses an older or unsupported save. Start a new game.");
        var save = JsonSerializer.Deserialize(json, BrowserSaveJson.Default.EmberbrookSave) ?? throw new InvalidDataException("Save is empty.");
        save.Validate();
        return save;
    }
}

[JsonSourceGenerationOptions(UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(EmberbrookSave))]
internal partial class BrowserSaveJson : JsonSerializerContext;
