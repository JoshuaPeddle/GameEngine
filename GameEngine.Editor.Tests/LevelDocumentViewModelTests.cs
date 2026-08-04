using System.Reactive.Linq;
using GameEngine.Editor.ViewModels;
using ReactiveUI.Primitives;

namespace GameEngine.Editor.Tests;

public class LevelDocumentViewModelTests
{
    private static LevelDocumentViewModel NewLevelEditor() =>
        new AssetEditorViewModel(new StubFilePicker()).LevelEditor.Level;

    private static void Run(IObservable<RxVoid> command) => ObservableExtensions.Subscribe(command);

    [Test]
    public void AddEntity_NamesEachEntityUniquelyAndSelectsIt()
    {
        var editor = NewLevelEditor();

        Run(editor.AddEntityCommand.Execute());
        Run(editor.AddEntityCommand.Execute());

        Assert.Multiple(() =>
        {
            Assert.That(editor.LevelEntities.Select(e => e.Tag), Is.EqualTo(new[] { "entity1", "entity2" }));
            Assert.That(editor.SelectedLevelEntity!.Tag, Is.EqualTo("entity2"));
        });
    }

    [Test]
    public void AddEntity_SkipsTagsAlreadyInUse()
    {
        var editor = NewLevelEditor();
        editor.LevelEntities.Add(new LevelEntityViewModel { Tag = "entity1" });

        Run(editor.AddEntityCommand.Execute());

        Assert.That(editor.LevelEntities.Select(e => e.Tag), Is.EqualTo(new[] { "entity1", "entity2" }));
    }

    [Test]
    public void RemoveEntity_SelectsThePrecedingEntity()
    {
        var editor = NewLevelEditor();
        Run(editor.AddEntityCommand.Execute());
        Run(editor.AddEntityCommand.Execute());
        Run(editor.AddEntityCommand.Execute());
        editor.SelectedLevelEntity = editor.LevelEntities[2];

        Run(editor.RemoveEntityCommand.Execute());

        Assert.Multiple(() =>
        {
            Assert.That(editor.LevelEntities, Has.Count.EqualTo(2));
            Assert.That(editor.SelectedLevelEntity!.Tag, Is.EqualTo("entity2"));
        });
    }

    [Test]
    public void RemoveEntity_ClearsTheSelectionWhenTheLastOneGoes()
    {
        var editor = NewLevelEditor();
        Run(editor.AddEntityCommand.Execute());

        Run(editor.RemoveEntityCommand.Execute());

        Assert.Multiple(() =>
        {
            Assert.That(editor.LevelEntities, Is.Empty);
            Assert.That(editor.SelectedLevelEntity, Is.Null);
        });
    }

    [Test]
    public void AddComponent_DefaultsToATransformAndSelectsIt()
    {
        var editor = NewLevelEditor();
        Run(editor.AddEntityCommand.Execute());

        Run(editor.AddComponentCommand.Execute());

        Assert.Multiple(() =>
        {
            Assert.That(editor.SelectedLevelEntity!.Components, Has.Count.EqualTo(1));
            Assert.That(editor.SelectedLevelComponent!.Type, Is.EqualTo("CTransform"));
            Assert.That(editor.SelectedLevelComponent.ScaleX, Is.EqualTo(1));
        });
    }

    [Test]
    public void RemoveComponent_SelectsThePrecedingComponent()
    {
        var editor = NewLevelEditor();
        Run(editor.AddEntityCommand.Execute());
        Run(editor.AddComponentCommand.Execute());
        Run(editor.AddComponentCommand.Execute());
        editor.SelectedLevelComponent = editor.SelectedLevelEntity!.Components[1];

        Run(editor.RemoveComponentCommand.Execute());

        Assert.Multiple(() =>
        {
            Assert.That(editor.SelectedLevelEntity!.Components, Has.Count.EqualTo(1));
            Assert.That(editor.SelectedLevelComponent, Is.SameAs(editor.SelectedLevelEntity.Components[0]));
        });
    }

    [Test]
    public void ComponentTypes_CoverEveryTypeTheLoaderRegisters()
    {
        Assert.That(LevelDocumentViewModel.ComponentTypes, Is.Not.Empty);
    }
}
