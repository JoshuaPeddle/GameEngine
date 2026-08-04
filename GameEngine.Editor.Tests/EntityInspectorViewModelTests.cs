using GameEngine.Core;
using GameEngine.Editor.ViewModels;

namespace GameEngine.Editor.Tests;

public class EntityInspectorViewModelTests
{
    private static EntitySnapshot Snapshot(params string[] componentTypes) =>
        new(7, "player", true, componentTypes);

    [Test]
    public void Select_ExposesTheSnapshotFields()
    {
        var inspector = new EntityInspectorViewModel();

        inspector.Select(Snapshot("CTransform", "CInput"));

        Assert.Multiple(() =>
        {
            Assert.That(inspector.HasEntitySelection, Is.True);
            Assert.That(inspector.SelectedEntityId, Is.EqualTo(7));
            Assert.That(inspector.SelectedEntityTag, Is.EqualTo("player"));
            Assert.That(inspector.SelectedEntityActive, Is.True);
            Assert.That(inspector.SelectedEntityComponents, Is.EqualTo(new[] { "CTransform", "CInput" }));
        });
    }

    [Test]
    public void SelectingNull_ClearsEverything()
    {
        var inspector = new EntityInspectorViewModel();
        inspector.Select(Snapshot("CTransform"));

        inspector.Select(null);

        Assert.Multiple(() =>
        {
            Assert.That(inspector.HasEntitySelection, Is.False);
            Assert.That(inspector.SelectedEntityId, Is.Null);
            Assert.That(inspector.SelectedEntityTag, Is.Null);
            Assert.That(inspector.SelectedEntityComponents, Is.Null);
        });
    }

    [Test]
    public void TheInspectorHoldsNoReferenceToALiveEntity()
    {
        var inspector = new EntityInspectorViewModel();
        var entities = new EntityManager();
        var entity = entities.CreateEntity("player");
        entities.Update();

        inspector.Select(entity.Capture());
        entity.Tag = "renamed by the engine thread";

        Assert.That(inspector.SelectedEntityTag, Is.EqualTo("player"));
    }
}
