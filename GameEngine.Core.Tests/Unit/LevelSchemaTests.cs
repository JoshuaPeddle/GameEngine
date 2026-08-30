using System.Text.Json;
using GameEngine.Core.Components;
using GameEngine.Core.Utils;

namespace GameEngine.Core.Tests.Unit;

public class LevelSchemaTests
{
    private static readonly ComponentFactory Factory = new(assets: null!);

    private static Component Create(string type, string json) =>
        Factory.CreateComponent(type, JsonDocument.Parse(json).RootElement);

    [Test]
    public void CTransform_ReadsVelocity()
    {
        var transform = (CTransform)Create("CTransform", """
            { "type": "CTransform", "position": { "x": 1, "y": 2 }, "velocity": { "x": 30, "y": -40 } }
            """);

        Assert.Multiple(() =>
        {
            Assert.That(transform.Velocity.X, Is.EqualTo(30));
            Assert.That(transform.Velocity.Y, Is.EqualTo(-40));
        });
    }

    [Test]
    public void CTransform_DefaultsVelocityToZero()
    {
        var transform = (CTransform)Create("CTransform", """
            { "type": "CTransform", "position": { "x": 1, "y": 2 } }
            """);

        Assert.That(transform.Velocity, Is.EqualTo(new Vec2(0, 0)));
    }

    [Test]
    public void UnknownProperty_IsRejectedWithTheKnownList()
    {
        var thrown = Assert.Throws<LevelSchemaException>(() => Create("CAnimation", """
            { "type": "CAnimation", "animationName": "Ball", "repeat": true }
            """));

        Assert.That(thrown!.Message, Does.Contain("'repeat'").And.Contain("animationName"));
    }

    [Test]
    public void MisspelledProperty_IsSuggested()
    {
        var thrown = Assert.Throws<LevelSchemaException>(() => Create("CTransform", """
            { "type": "CTransform", "position": { "x": 0, "y": 0 }, "rotaton": 90 }
            """));

        Assert.That(thrown!.Message, Does.Contain("Did you mean 'rotation'?"));
    }

    [Test]
    public void UnknownComponentType_IsRejectedWithTheKnownList()
    {
        var thrown = Assert.Throws<LevelSchemaException>(() => Create("CTransfrom", """
            { "type": "CTransfrom" }
            """));

        Assert.That(thrown!.Message, Does.Contain("Did you mean 'CTransform'?"));
    }

    [Test]
    public void MissingRequiredProperty_NamesIt()
    {
        var thrown = Assert.Throws<LevelSchemaException>(() => Create("CBoundingBox", """
            { "type": "CBoundingBox", "size": { "x": 1, "y": 2 }, "blockVision": false }
            """));

        Assert.That(thrown!.Message, Does.Contain("blockMovement"));
    }

    [Test]
    public void WrongValueKind_SaysWhatWasExpected()
    {
        var thrown = Assert.Throws<LevelSchemaException>(() => Create("CMovement", """
            { "type": "CMovement", "speed": "fast", "maxSpeed": 10 }
            """));

        Assert.That(thrown!.Message, Does.Contain("must be a number").And.Contain("was a string"));
    }

    [Test]
    public void PropertyNamesStayCaseInsensitive()
    {
        var transform = (CTransform)Create("CTransform", """
            { "type": "CTransform", "Position": { "x": 5, "y": 6 } }
            """);

        Assert.That(transform.Position, Is.EqualTo(new Vec2(5, 6)));
    }

    [Test]
    public void UnknownEntityKey_IsRejected()
    {
        const string json = """
            { "entities": [ { "tag": "x", "name": "x", "components": [] } ] }
            """;

        Assert.That(() => LevelFile.LoadFromJson(json),
            Throws.TypeOf<InvalidDataException>().With.Message.Contains("'name'"));
    }

    [Test]
    public void SchemaPointerSurvivesARoundTrip()
    {
        var json = $$"""
            { "$schema": "{{LevelSchema.Url}}", "entities": [] }
            """;

        Assert.That(LevelFile.LoadFromJson(LevelFile.LoadFromJson(json).ToJson()).Schema,
            Is.EqualTo(LevelSchema.Url));
    }

    [Test]
    public void PublishedSchemaMatchesTheComponentVocabulary()
    {
        var published = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "levels", "level.schema.json"));

        Assert.That(Normalize(published), Is.EqualTo(Normalize(LevelSchema.Generate())),
            "GameEngine.Core/levels/level.schema.json is generated from ComponentSchemas; regenerate it.");
    }

    [Test]
    public void EverySchemaTypeHasAFactory()
    {
        foreach (var type in ComponentSchemas.KnownTypes)
        {
            try
            {
                Create(type, BuildMinimalComponent(type));
            }
            catch (LevelSchemaException ex) when (ex.Message.Contains("without assets"))
            {
                // CAnimation resolves its animation through Assets, which this factory has none of.
            }
            catch (Exception ex)
            {
                Assert.Fail($"{type} is in the vocabulary but the factory rejected its required properties: {ex.Message}");
            }
        }
    }

    private static string BuildMinimalComponent(string type)
    {
        var schema = ComponentSchemas.Find(type)!;
        var properties = schema.Required.Select(p => $"\"{p.Name}\": {SampleValue(p.Kind)}");
        return $"{{ \"type\": \"{type}\"{string.Concat(properties.Select(p => ", " + p))} }}";
    }

    private static string SampleValue(ComponentValueKind kind) => kind switch
    {
        ComponentValueKind.Vector => """{ "x": 1, "y": 2 }""",
        ComponentValueKind.Boolean => "true",
        ComponentValueKind.Text => "\"Ball\"",
        _ => "1"
    };

    private static string Normalize(string text) => text.ReplaceLineEndings("\n").TrimEnd();
}
