using System.Reactive.Linq;
using System.Text.Json;
using GameEngine.Core.Utils;
using GameEngine.Editor.ViewModels;
using ReactiveUI.Primitives;

namespace GameEngine.Editor.Tests;

public class LevelSaveTests
{
    private const string ValidLevel = """
        {
          "metadata": { "name": "Round Trip", "version": "1.0", "description": "", "properties": {} },
          "entities": [
            {
              "tag": "player",
              "components": [
                { "type": "CTransform", "position": { "x": 10, "y": 20 } },
                { "type": "CBoundingBox", "size": { "x": 32, "y": 32 }, "blockVision": false, "blockMovement": true }
              ]
            }
          ]
        }
        """;

    private string _projectDirectory = string.Empty;
    private string _levelPath = string.Empty;

    [SetUp]
    public void CreateProject()
    {
        _projectDirectory = Path.Combine(Path.GetTempPath(), "ge-level-save-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(Path.Combine(_projectDirectory, "levels"));
        _levelPath = Path.Combine(_projectDirectory, "levels", "level1.json");
        File.WriteAllText(_levelPath, ValidLevel);
    }

    [TearDown]
    public void RemoveProject()
    {
        if (Directory.Exists(_projectDirectory))
            Directory.Delete(_projectDirectory, recursive: true);
    }

    private static void Run(IObservable<RxVoid> command) => ObservableExtensions.Subscribe(command);

    private LevelEditorViewModel LoadedEditor()
    {
        var assets = new AssetEditorViewModel(new StubFilePicker());
        assets.ProjectEditor.ProjectFolderPath = Path.Combine(_projectDirectory, "Game.csproj");

        var editor = assets.LevelEditor;
        editor.Level.RefreshLevelFileOptions();
        editor.Level.SelectedLevelFile = _levelPath;
        Run(editor.Level.LoadLevelFileCommand.Execute());

        Assert.That(editor.Status.Message, Is.EqualTo("Level loaded."));
        return editor;
    }

    private string[] TemporaryFilesLeftBehind() =>
        Directory.GetFiles(Path.Combine(_projectDirectory, "levels"), "*.tmp");

    [Test]
    public void Save_LeavesTheFileUntouchedWhenAComponentIsMalformedJson()
    {
        var original = File.ReadAllBytes(_levelPath);
        var editor = LoadedEditor();
        editor.Level.LevelEntities[0].Components[0].RawJson = "{ this is not json";

        Run(editor.Level.SaveLevelFileCommand.Execute());

        Assert.Multiple(() =>
        {
            Assert.That(File.ReadAllBytes(_levelPath), Is.EqualTo(original));
            Assert.That(editor.Status.Message, Does.StartWith("Level not saved:"));
            Assert.That(editor.Status.Message, Does.Contain("Entity 'player' (#1), component #1"));
            Assert.That(TemporaryFilesLeftBehind(), Is.Empty);
        });
    }

    [Test]
    public void Save_RejectsAnUnknownComponentTypeAndNamesItsLocation()
    {
        var original = File.ReadAllBytes(_levelPath);
        var editor = LoadedEditor();
        editor.Level.LevelEntities[0].Components[1].RawJson = """{ "type": "CNonsense" }""";

        Run(editor.Level.SaveLevelFileCommand.Execute());

        Assert.Multiple(() =>
        {
            Assert.That(File.ReadAllBytes(_levelPath), Is.EqualTo(original));
            Assert.That(editor.Status.Message, Does.Contain("Entity 'player' (#1), component #2"));
            Assert.That(editor.Status.Message, Does.Contain("Unknown component type 'CNonsense'"));
        });
    }

    [Test]
    public void Save_RejectsAnUnknownPropertyRatherThanDroppingIt()
    {
        var original = File.ReadAllBytes(_levelPath);
        var editor = LoadedEditor();
        editor.Level.LevelEntities[0].Components[1].RawJson = """{ "type": "CText", "nonsense": 1 }""";

        Run(editor.Level.SaveLevelFileCommand.Execute());

        Assert.Multiple(() =>
        {
            Assert.That(editor.Status.Message, Does.Contain("has no property 'nonsense'"));
            Assert.That(File.ReadAllBytes(_levelPath), Is.EqualTo(original));
        });
    }

    [Test]
    public void Save_RejectsAPropertyOfTheWrongType()
    {
        var original = File.ReadAllBytes(_levelPath);
        var editor = LoadedEditor();
        editor.Level.LevelEntities[0].Components[1].RawJson = """{ "type": "CCamera", "zoom": "far" }""";

        Run(editor.Level.SaveLevelFileCommand.Execute());

        Assert.Multiple(() =>
        {
            Assert.That(editor.Status.Message, Does.Contain("Entity 'player' (#1), component #2"));
            Assert.That(editor.Status.Message, Does.Contain("must be a number"));
            Assert.That(File.ReadAllBytes(_levelPath), Is.EqualTo(original));
        });
    }

    [Test]
    public void Save_KeepsUnsavedEditsWhenTheDocumentIsRejected()
    {
        var editor = LoadedEditor();
        editor.Level.LevelEntities[0].Tag = "hero";
        editor.Level.LevelEntities[0].Components[0].RawJson = "{ broken";

        Run(editor.Level.SaveLevelFileCommand.Execute());
        editor.Level.LevelEntities[0].Components[0].RawJson =
            """{ "type": "CTransform", "position": { "x": 10, "y": 20 } }""";
        Run(editor.Level.SaveLevelFileCommand.Execute());

        var reloaded = LevelFile.LoadFromJson(File.ReadAllText(_levelPath));

        Assert.Multiple(() =>
        {
            Assert.That(editor.Status.Message, Is.EqualTo("Level saved."));
            Assert.That(reloaded.Entities[0].Tag, Is.EqualTo("hero"));
        });
    }

    [Test]
    public void Save_WritesADocumentTheStrictLoaderAccepts()
    {
        var editor = LoadedEditor();
        editor.Level.LevelEntities[0].Components[0].RawJson =
            """{ "type": "CTransform", "position": { "x": 110, "y": 20 } }""";

        Run(editor.Level.SaveLevelFileCommand.Execute());

        var reloaded = LevelFile.LoadFromJson(File.ReadAllText(_levelPath));
        var position = reloaded.Entities[0].Components[0].Data.GetProperty("position");

        Assert.Multiple(() =>
        {
            Assert.That(editor.Status.Message, Is.EqualTo("Level saved."));
            Assert.That(position.GetProperty("x").GetDouble(), Is.EqualTo(110));
            Assert.That(reloaded.Entities[0].Components, Has.Count.EqualTo(2));
            Assert.DoesNotThrow(reloaded.Validate);
            Assert.That(TemporaryFilesLeftBehind(), Is.Empty);
        });
    }

    [Test]
    public void Save_ReportsAFailedWriteAndLeavesNoTemporaryFile()
    {
        var editor = LoadedEditor();
        File.Delete(_levelPath);
        Directory.CreateDirectory(_levelPath);

        Run(editor.Level.SaveLevelFileCommand.Execute());

        Assert.Multiple(() =>
        {
            Assert.That(editor.Status.Message, Does.StartWith("Failed to save level:"));
            Assert.That(TemporaryFilesLeftBehind(), Is.Empty);
        });
    }

    [Test]
    public void Save_PreservesTheSchemaPointerAndMetadata()
    {
        File.WriteAllText(_levelPath, """
            {
              "$schema": "../../GameEngine.Core/levels/level.schema.json",
              "metadata": { "name": "Kept", "version": "3.0", "description": "d", "properties": {} },
              "entities": [ { "tag": "player", "components": [ { "type": "CInput" } ] } ]
            }
            """);

        var editor = LoadedEditor();
        Run(editor.Level.SaveLevelFileCommand.Execute());

        using var document = JsonDocument.Parse(File.ReadAllText(_levelPath));

        Assert.Multiple(() =>
        {
            Assert.That(document.RootElement.GetProperty("$schema").GetString(),
                Is.EqualTo("../../GameEngine.Core/levels/level.schema.json"));
            Assert.That(document.RootElement.GetProperty("metadata").GetProperty("name").GetString(),
                Is.EqualTo("Kept"));
        });
    }
}
