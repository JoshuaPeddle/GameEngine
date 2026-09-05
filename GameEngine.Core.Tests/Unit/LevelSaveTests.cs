using System.Text.Json;
using GameEngine.Core.Utils;

namespace GameEngine.Core.Tests.Unit;

public class LevelSaveTests
{
    private string _directory = string.Empty;
    private string _path = string.Empty;

    [SetUp]
    public void CreateDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), "ge-level-write-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(_directory);
        _path = Path.Combine(_directory, "level.json");
    }

    [TearDown]
    public void RemoveDirectory()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    private static LevelFile LevelWith(string entityTag, params (string Type, string Json)[] components)
    {
        var level = new LevelFile();
        var entity = new EntityData { Tag = entityTag };

        foreach (var (type, json) in components)
        {
            using var document = JsonDocument.Parse(json);
            entity.Components.Add(new ComponentData { Type = type, Data = document.RootElement.Clone() });
        }

        level.Entities.Add(entity);
        return level;
    }

    private static LevelFile ValidLevel() =>
        LevelWith("player", ("CTransform", """{ "type": "CTransform", "position": { "x": 1, "y": 2 } }"""));

    [Test]
    public void Validate_RejectsAnUnknownComponentProperty()
    {
        var level = LevelWith("player",
            ("CTransform", """{ "type": "CTransform", "position": { "x": 1, "y": 2 }, "nonsense": 3 }"""));

        var thrown = Assert.Throws<LevelSchemaException>(level.Validate);
        Assert.That(thrown!.Message, Does.Contain("Entity 'player' (#1), component #1"));
    }

    [Test]
    public void Validate_RejectsAMissingRequiredProperty()
    {
        var level = LevelWith("player", ("CBoundingBox", """{ "type": "CBoundingBox" }"""));

        var thrown = Assert.Throws<LevelSchemaException>(level.Validate);
        Assert.That(thrown!.Message, Does.Contain("missing required property"));
    }

    [Test]
    public void Validate_RejectsAnUnknownComponentType()
    {
        var level = LevelWith("player", ("CNonsense", """{ "type": "CNonsense" }"""));

        var thrown = Assert.Throws<LevelSchemaException>(level.Validate);
        Assert.That(thrown!.Message, Does.Contain("Unknown component type 'CNonsense'"));
    }

    [Test]
    public void SaveToFile_LeavesAnExistingFileByteForByteUnchangedWhenTheDocumentIsInvalid()
    {
        ValidLevel().SaveToFile(_path);
        var original = File.ReadAllBytes(_path);

        var invalid = LevelWith("player", ("CTransform", """{ "type": "CTransform" }"""));

        Assert.Multiple(() =>
        {
            Assert.Throws<LevelSchemaException>(() => invalid.SaveToFile(_path));
            Assert.That(File.ReadAllBytes(_path), Is.EqualTo(original));
            Assert.That(Directory.GetFiles(_directory, "*.tmp"), Is.Empty);
        });
    }

    [Test]
    public void SaveToFile_LeavesNoTemporaryFileWhenTheDestinationCannotBeReplaced()
    {
        Directory.CreateDirectory(_path);

        Assert.Multiple(() =>
        {
            Assert.Throws<IOException>(() => ValidLevel().SaveToFile(_path));
            Assert.That(Directory.GetFiles(_directory, "*.tmp"), Is.Empty);
        });
    }

    [Test]
    public void SaveToFile_ReplacesAnExistingFileWithTheNewDocument()
    {
        ValidLevel().SaveToFile(_path);

        LevelWith("enemy", ("CInput", """{ "type": "CInput" }""")).SaveToFile(_path);

        var reloaded = LevelFile.LoadFromFile(_path, new FileAssetSource());

        Assert.Multiple(() =>
        {
            Assert.That(reloaded.Entities, Has.Count.EqualTo(1));
            Assert.That(reloaded.Entities[0].Tag, Is.EqualTo("enemy"));
            Assert.That(Directory.GetFiles(_directory, "*.tmp"), Is.Empty);
        });
    }
}
