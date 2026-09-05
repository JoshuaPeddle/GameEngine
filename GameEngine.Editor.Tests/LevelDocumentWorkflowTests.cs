using System.Reactive.Linq;
using GameEngine.Core;
using GameEngine.Core.Components;
using GameEngine.Core.Utils;
using GameEngine.Editor.Magic;
using GameEngine.Editor.ViewModels;
using ReactiveUI.Primitives;

namespace GameEngine.Editor.Tests;

// The workflow the level editor exists to support, end to end: open a level, place an entity,
// edit it, preview it, undo, redo, save, reopen, and get the same thing back.
public class LevelDocumentWorkflowTests
{
    private const string StartingLevel = """
        {
          "metadata": { "name": "Workflow", "version": "1.0", "description": "", "properties": {} },
          "entities": [
            {
              "tag": "player",
              "components": [
                { "type": "CTransform", "position": { "x": 10, "y": 20 } }
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
        _projectDirectory = Path.Combine(Path.GetTempPath(), "ge-workflow-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(Path.Combine(_projectDirectory, "levels"));
        _levelPath = Path.Combine(_projectDirectory, "levels", "level1.json");
        File.WriteAllText(_levelPath, StartingLevel);
    }

    [TearDown]
    public void RemoveProject()
    {
        if (Directory.Exists(_projectDirectory))
            Directory.Delete(_projectDirectory, recursive: true);
    }

    private static void Run(IObservable<RxVoid> command) => ObservableExtensions.Subscribe(command);

    private LevelEditorViewModel OpenLevel()
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

    private static LevelComponentViewModel TransformOf(LevelEntityViewModel entity) =>
        entity.Components.First(c => c.Type == "CTransform");

    [Test]
    public void ComponentTypesComeFromTheEngineVocabulary()
    {
        Assert.That(LevelDocumentViewModel.ComponentTypes, Is.EquivalentTo(ComponentSchemas.KnownTypes));
    }

    [Test]
    public void ALoadedDocumentStartsClean()
    {
        var editor = OpenLevel();

        Assert.Multiple(() =>
        {
            Assert.That(editor.Level.HasUnsavedChanges, Is.False);
            Assert.That(editor.Level.CanUndo, Is.False);
            Assert.That(editor.Level.CanRedo, Is.False);
        });
    }

    [Test]
    public void EachAuthoredEntityHasItsOwnDocumentIdentity()
    {
        var editor = OpenLevel();
        var placed = editor.Level.PlaceEntity("crate", new Vec2(100, 200));

        Assert.Multiple(() =>
        {
            Assert.That(placed.DocumentId, Is.Not.EqualTo(editor.Level.LevelEntities[0].DocumentId));
            Assert.That(editor.Level.FindByDocumentId(placed.DocumentId), Is.SameAs(placed));
            Assert.That(editor.Level.FindByDocumentId(Guid.NewGuid()), Is.Null);
        });
    }

    [Test]
    public void PlacingAnEntityRecordsItsPositionAndMarksTheDocumentDirty()
    {
        var editor = OpenLevel();

        var placed = editor.Level.PlaceEntity("crate", new Vec2(100, 200));

        Assert.Multiple(() =>
        {
            Assert.That(TransformOf(placed).PosX, Is.EqualTo(100));
            Assert.That(TransformOf(placed).PosY, Is.EqualTo(200));
            Assert.That(editor.Level.HasUnsavedChanges, Is.True);
            Assert.That(editor.Level.CanUndo, Is.True);
        });
    }

    [Test]
    public void UndoTakesBackAPlacementAndRedoPutsItBack()
    {
        var editor = OpenLevel();
        editor.Level.PlaceEntity("crate", new Vec2(100, 200));

        Run(editor.Level.UndoCommand.Execute());
        Assert.That(editor.Level.LevelEntities.Select(e => e.Tag), Is.EqualTo(new[] { "player" }));

        Run(editor.Level.RedoCommand.Execute());
        Assert.That(editor.Level.LevelEntities.Select(e => e.Tag), Is.EqualTo(new[] { "player", "crate" }));
    }

    [Test]
    public void UndoTakesBackAPropertyEdit()
    {
        var editor = OpenLevel();
        var transform = TransformOf(editor.Level.LevelEntities[0]);

        transform.PosX = 500;
        Assert.That(editor.Level.CanUndo, Is.True);

        Run(editor.Level.UndoCommand.Execute());

        Assert.Multiple(() =>
        {
            Assert.That(transform.PosX, Is.EqualTo(10));
            Assert.That(editor.Level.HasUnsavedChanges, Is.False);
        });
    }

    [Test]
    public void ChangingAComponentTypeIsOneUndoStep()
    {
        var editor = OpenLevel();
        var component = TransformOf(editor.Level.LevelEntities[0]);

        component.Type = "CBoundingBox";
        Run(editor.Level.UndoCommand.Execute());

        Assert.Multiple(() =>
        {
            Assert.That(component.Type, Is.EqualTo("CTransform"));
            Assert.That(component.PosX, Is.EqualTo(10));
            Assert.That(editor.Level.CanUndo, Is.False);
        });
    }

    [Test]
    public void UndoTakesBackAComponentRemoval()
    {
        var editor = OpenLevel();
        editor.Level.SelectedLevelEntity = editor.Level.LevelEntities[0];
        editor.Level.SelectedLevelComponent = TransformOf(editor.Level.LevelEntities[0]);

        Run(editor.Level.RemoveComponentCommand.Execute());
        Assert.That(editor.Level.LevelEntities[0].Components, Is.Empty);

        Run(editor.Level.UndoCommand.Execute());
        Assert.That(editor.Level.LevelEntities[0].Components, Has.Count.EqualTo(1));
    }

    [Test]
    public void SavingClearsTheUnsavedFlagAndKeepsTheUndoStack()
    {
        var editor = OpenLevel();
        editor.Level.PlaceEntity("crate", new Vec2(100, 200));

        Run(editor.Level.SaveLevelFileCommand.Execute());

        Assert.Multiple(() =>
        {
            Assert.That(editor.Status.Message, Is.EqualTo("Level saved."));
            Assert.That(editor.Level.HasUnsavedChanges, Is.False);
            Assert.That(editor.Level.CanUndo, Is.True);
        });
    }

    [Test]
    public void UndoingPastASaveMarksTheDocumentDirtyAgain()
    {
        var editor = OpenLevel();
        editor.Level.PlaceEntity("crate", new Vec2(100, 200));
        Run(editor.Level.SaveLevelFileCommand.Execute());

        Run(editor.Level.UndoCommand.Execute());

        Assert.That(editor.Level.HasUnsavedChanges, Is.True);
    }

    [Test]
    public void ThePreviewIsBuiltFromTheSameDocumentThatIsSaved()
    {
        var editor = OpenLevel();
        editor.Level.PlaceEntity("crate", new Vec2(100, 200));

        var (previewed, documentIds) = editor.Level.Project();
        Run(editor.Level.SaveLevelFileCommand.Execute());
        var saved = LevelFile.LoadFromJson(File.ReadAllText(_levelPath));

        Assert.Multiple(() =>
        {
            Assert.That(previewed.ToJson(), Is.EqualTo(saved.ToJson()));
            Assert.That(documentIds, Is.EqualTo(editor.Level.LevelEntities.Select(e => e.DocumentId)));
        });
    }

    [Test]
    public void ThePreviewSceneMapsRuntimeEntitiesBackToTheirDocumentRows()
    {
        var editor = OpenLevel();
        var crate = editor.Level.PlaceEntity("crate", new Vec2(100, 200));

        var (level, documentIds) = editor.Level.Project();
        var scene = new LevelDocumentScene(level, documentIds, () => null!);

        var entities = new EntityManager();
        scene.Initialize(entities, new InputManager(), null, _ => { });
        entities.Update();

        var runtimeCrate = entities.GetEntityWithTag("crate")!;
        var runtimePlayer = entities.GetEntityWithTag("player")!;

        Assert.Multiple(() =>
        {
            Assert.That(scene.DocumentIdOf(runtimeCrate.Id), Is.EqualTo(crate.DocumentId));
            Assert.That(scene.DocumentIdOf(runtimePlayer.Id),
                Is.EqualTo(editor.Level.LevelEntities[0].DocumentId));
            Assert.That(scene.DocumentIdOf(9999), Is.Null, "a runtime-only entity has no document row");
        });
    }

    [Test]
    public void SelectingAPreviewEntitySelectsItsDocumentRow()
    {
        var editor = OpenLevel();
        var crate = editor.Level.PlaceEntity("crate", new Vec2(100, 200));
        editor.Level.SelectedLevelEntity = editor.Level.LevelEntities[0];

        Assert.That(editor.Level.SelectByDocumentId(crate.DocumentId), Is.True);
        Assert.That(editor.Level.SelectedLevelEntity, Is.SameAs(crate));
    }

    [Test]
    public void TheWholeWorkflowRoundTrips()
    {
        var editor = OpenLevel();

        var crate = editor.Level.PlaceEntity("crate", new Vec2(100, 200));
        var transform = TransformOf(crate);
        transform.PosX = 140;

        editor.Level.SelectedLevelEntity = crate;
        Run(editor.Level.AddComponentCommand.Execute());
        editor.Level.SelectedLevelComponent!.Type = "CBoundingBox";
        editor.Level.SelectedLevelComponent.Width = 48;
        editor.Level.SelectedLevelComponent.Height = 24;

        var (previewed, _) = editor.Level.Project();
        Assert.DoesNotThrow(previewed.Validate);

        editor.Level.SelectedLevelComponent.Width = 999;
        Run(editor.Level.UndoCommand.Execute());
        Assert.That(editor.Level.SelectedLevelComponent.Width, Is.EqualTo(48));

        Run(editor.Level.RedoCommand.Execute());
        Assert.That(editor.Level.SelectedLevelComponent.Width, Is.EqualTo(999));
        Run(editor.Level.UndoCommand.Execute());

        Run(editor.Level.SaveLevelFileCommand.Execute());
        Assert.That(editor.Status.Message, Is.EqualTo("Level saved."));

        var reopened = OpenLevel();
        var reopenedCrate = reopened.Level.LevelEntities.Single(e => e.Tag == "crate");
        var reopenedTransform = TransformOf(reopenedCrate);
        var reopenedBox = reopenedCrate.Components.First(c => c.Type == "CBoundingBox");

        Assert.Multiple(() =>
        {
            Assert.That(reopenedTransform.PosX, Is.EqualTo(140));
            Assert.That(reopenedTransform.PosY, Is.EqualTo(200));
            Assert.That(reopenedBox.Width, Is.EqualTo(48));
            Assert.That(reopenedBox.Height, Is.EqualTo(24));
            Assert.That(reopened.Level.HasUnsavedChanges, Is.False);
        });
    }

    [Test]
    public void APreviewOfAnInvalidDocumentIsRefusedRatherThanLoaded()
    {
        var editor = OpenLevel();
        TransformOf(editor.Level.LevelEntities[0]).RawJson = """{ "type": "CText", "size": "big" }""";

        LevelDocumentScene? requested = null;
        editor.PreviewLevelRequested += scene => requested = scene;
        editor.PreviewLevel();

        Assert.Multiple(() =>
        {
            Assert.That(requested, Is.Null);
            Assert.That(editor.Status.Message, Does.StartWith("Level not previewed:"));
        });
    }
}
