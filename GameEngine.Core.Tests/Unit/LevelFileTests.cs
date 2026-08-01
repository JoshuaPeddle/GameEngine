using System.Text.Json;
using GameEngine.Core.Components;
using GameEngine.Core.Utils;

namespace GameEngine.Core.Tests.Unit;

public class LevelFileTests
{
    private static LevelFile SampleLevel() =>
        new LevelBuilder("Round Trip")
            .SetMetadata("Round Trip", "2.0", "built in code")
            .AddProperty("difficulty", "easy")
            .AddEntity("player", player => player
                .AddTransform(10, 20, rotation: 45)
                .AddBoundingBox(5, 6, blockVision: true, blockMovement: true)
                .AddInput())
            .AddEntity("tile", tile => tile
                .AddTransform(100, 200))
            .Build();

    private static ComponentData ComponentOf(LevelFile level, string entityTag, string componentType) =>
        level.Entities.Single(e => e.Tag == entityTag).Components.Single(c => c.Type == componentType);

    [Test]
    public void BuilderOutput_CanBeLoadedBack()
    {
        var json = SampleLevel().ToJson();

        LevelFile? reloaded = null;
        Assert.DoesNotThrow(() => reloaded = LevelFile.LoadFromJson(json));
        Assert.That(reloaded!.Entities, Has.Count.EqualTo(2));
    }

    [Test]
    public void RoundTrip_PreservesTagsAndComponentTypes()
    {
        var reloaded = LevelFile.LoadFromJson(SampleLevel().ToJson());

        Assert.Multiple(() =>
        {
            Assert.That(reloaded.Entities.Select(e => e.Tag), Is.EqualTo(new[] { "player", "tile" }));
            Assert.That(reloaded.Entities[0].Components.Select(c => c.Type),
                Is.EqualTo(new[] { "CTransform", "CBoundingBox", "CInput" }));
            Assert.That(reloaded.Entities[1].Components.Select(c => c.Type),
                Is.EqualTo(new[] { "CTransform" }));
        });
    }

    [Test]
    public void RoundTrip_PreservesComponentValues()
    {
        var reloaded = LevelFile.LoadFromJson(SampleLevel().ToJson());

        var transform = ComponentOf(reloaded, "player", "CTransform").Data;
        var box = ComponentOf(reloaded, "player", "CBoundingBox").Data;

        Assert.Multiple(() =>
        {
            Assert.That(transform.GetProperty("position").GetProperty("x").GetDouble(), Is.EqualTo(10));
            Assert.That(transform.GetProperty("position").GetProperty("y").GetDouble(), Is.EqualTo(20));
            Assert.That(transform.GetProperty("rotation").GetDouble(), Is.EqualTo(45));
            Assert.That(box.GetProperty("size").GetProperty("x").GetDouble(), Is.EqualTo(5));
            Assert.That(box.GetProperty("blockMovement").GetBoolean(), Is.True);
        });
    }

    [Test]
    public void RoundTrip_PreservesMetadata()
    {
        var reloaded = LevelFile.LoadFromJson(SampleLevel().ToJson());

        Assert.Multiple(() =>
        {
            Assert.That(reloaded.Metadata.Name, Is.EqualTo("Round Trip"));
            Assert.That(reloaded.Metadata.Version, Is.EqualTo("2.0"));
            Assert.That(reloaded.Metadata.Description, Is.EqualTo("built in code"));
        });
    }

    [Test]
    public void RoundTrip_IsStableAcrossRepeatedSaves()
    {
        var once = LevelFile.LoadFromJson(SampleLevel().ToJson());
        var onceJson = once.ToJson();
        var twice = LevelFile.LoadFromJson(onceJson);

        Assert.That(twice.ToJson(), Is.EqualTo(onceJson), "serialisation must reach a fixed point");
    }

    [Test]
    public void SerialisedComponent_CarriesExactlyOneTypeProperty()
    {
        var json = LevelFile.LoadFromJson(SampleLevel().ToJson()).ToJson();

        using var document = JsonDocument.Parse(json);
        foreach (var entity in document.RootElement.GetProperty("entities").EnumerateArray())
        {
            foreach (var component in entity.GetProperty("components").EnumerateArray())
            {
                int typeProperties = component.EnumerateObject().Count(p => p.NameEquals("type"));
                Assert.That(typeProperties, Is.EqualTo(1),
                    $"component {component} should carry exactly one type property");
            }
        }
    }

    [Test]
    public void SaveAndLoadFromFile_RoundTrips()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ge04_{Guid.NewGuid():N}.json");
        try
        {
            SampleLevel().SaveToFile(path);
            var reloaded = LevelFile.LoadFromFile(path);
            Assert.That(reloaded.Entities, Has.Count.EqualTo(2));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Test]
    public void ComponentFactory_ReadsTransformRotation()
    {
        var data = ComponentOf(LevelFile.LoadFromJson(SampleLevel().ToJson()), "player", "CTransform").Data;

        var factory = new ComponentFactory(assets: null!);
        var transform = (CTransform)factory.CreateComponent("CTransform", data);

        Assert.Multiple(() =>
        {
            Assert.That(transform.Position.X, Is.EqualTo(10));
            Assert.That(transform.Position.Y, Is.EqualTo(20));
            Assert.That(transform.Rotation, Is.EqualTo(45));
        });
    }

    [Test]
    public void ComponentFactory_DefaultsRotationWhenAbsent()
    {
        const string json = """
            { "entities": [ { "tag": "tile", "components": [
                { "type": "CTransform", "position": { "x": 3, "y": 4 } } ] } ] }
            """;
        var data = ComponentOf(LevelFile.LoadFromJson(json), "tile", "CTransform").Data;

        var factory = new ComponentFactory(assets: null!);
        var transform = (CTransform)factory.CreateComponent("CTransform", data);

        Assert.Multiple(() =>
        {
            Assert.That(transform.Position.X, Is.EqualTo(3));
            Assert.That(transform.Rotation, Is.EqualTo(0));
        });
    }

    [Test]
    public void LoadFromJson_RejectsAComponentWithNoType()
    {
        const string json = """
            { "entities": [ { "tag": "x", "components": [ { "position": { "x": 1, "y": 2 } } ] } ] }
            """;

        Assert.Throws<InvalidDataException>(() => LevelFile.LoadFromJson(json));
    }
}
