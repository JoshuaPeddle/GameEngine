using System.Text.Json;
using GameEngine.Core.Components;
using GameEngine.Editor.ViewModels;

namespace GameEngine.Editor.Tests;

public class LevelComponentViewModelTests
{
    private static LevelComponentViewModel ComponentOfType(string type)
    {
        var component = new LevelComponentViewModel();
        component.ApplyTypeWithDefaults(type);
        return component;
    }

    [Test]
    public void RawJson_ParsesIntoTheTypedFields()
    {
        var component = ComponentOfType("CTransform");

        component.RawJson = """
        {
          "type": "CTransform",
          "position": { "x": 10, "y": 20 },
          "velocity": { "x": -1, "y": 2 },
          "scale": { "x": 3, "y": 4 },
          "rotation": 45
        }
        """;

        Assert.Multiple(() =>
        {
            Assert.That(component.PosX, Is.EqualTo(10));
            Assert.That(component.PosY, Is.EqualTo(20));
            Assert.That(component.VelX, Is.EqualTo(-1));
            Assert.That(component.VelY, Is.EqualTo(2));
            Assert.That(component.ScaleX, Is.EqualTo(3));
            Assert.That(component.ScaleY, Is.EqualTo(4));
            Assert.That(component.Rotation, Is.EqualTo(45));
        });
    }

    [Test]
    public void EditingATypedField_RewritesRawJson()
    {
        var component = ComponentOfType("CTransform");

        component.PosX = 128;

        using var document = JsonDocument.Parse(component.RawJson);
        var position = document.RootElement.GetProperty("position");

        Assert.That(position.GetProperty("x").GetDouble(), Is.EqualTo(128));
    }

    [Test]
    public void RawJson_CarriesTheTypeDiscriminatorForEveryEditableType()
    {
        foreach (var type in LevelDocumentViewModel.ComponentTypes)
        {
            var component = ComponentOfType(type);

            using var document = JsonDocument.Parse(component.RawJson);

            Assert.That(document.RootElement.GetProperty("type").GetString(), Is.EqualTo(type),
                $"{type} must write its own discriminator");
        }
    }

    [Test]
    public void ChangingType_AppliesThatTypesDefaults()
    {
        var component = ComponentOfType("CTransform");

        component.Type = "CBoundingBox";

        Assert.Multiple(() =>
        {
            Assert.That(component.Width, Is.EqualTo(32));
            Assert.That(component.Height, Is.EqualTo(32));
            Assert.That(component.BlockMovement, Is.True);
            Assert.That(component.BlockVision, Is.False);
            Assert.That(component.IsBoundingBox, Is.True);
            Assert.That(component.IsTransform, Is.False);
        });
    }

    [Test]
    public void MalformedRawJson_LeavesTheTypedFieldsAlone()
    {
        var component = ComponentOfType("CTransform");
        component.PosX = 7;

        Assert.DoesNotThrow(() => component.RawJson = "{ this is not json");

        Assert.That(component.PosX, Is.EqualTo(7));
    }

    [Test]
    public void EveryEditableType_ProducesJsonTheEngineCanLoad()
    {
        var factory = new ComponentFactory(assets: null!);

        foreach (var type in LevelDocumentViewModel.ComponentTypes)
        {
            if (type == "CAnimation")
                continue;

            var component = ComponentOfType(type);
            using var document = JsonDocument.Parse(component.RawJson);

            Assert.DoesNotThrow(
                () => factory.CreateComponent(type, document.RootElement.Clone()),
                $"the engine must be able to load the {type} the editor writes");
        }
    }

    [Test]
    public void BoundingBoxEditedInTheEditor_LoadsWithTheSameValues()
    {
        var component = ComponentOfType("CBoundingBox");
        component.Width = 48;
        component.Height = 96;
        component.BlockVision = true;

        using var document = JsonDocument.Parse(component.RawJson);
        var box = (CBoundingBox)new ComponentFactory(assets: null!)
            .CreateComponent("CBoundingBox", document.RootElement.Clone());

        Assert.Multiple(() =>
        {
            Assert.That(box.Size.X, Is.EqualTo(48));
            Assert.That(box.Size.Y, Is.EqualTo(96));
            Assert.That(box.BlockVision, Is.True);
            Assert.That(box.BlockMovement, Is.True);
        });
    }

    [Test]
    public void TransformEditedInTheEditor_LoadsWithTheSameValues()
    {
        var component = ComponentOfType("CTransform");
        component.PosX = 100;
        component.PosY = 200;
        component.Rotation = 30;
        component.ScaleX = 2;
        component.VelX = -15;
        component.VelY = 7;
        component.Layer = 3;

        using var document = JsonDocument.Parse(component.RawJson);
        var transform = (CTransform)new ComponentFactory(assets: null!)
            .CreateComponent("CTransform", document.RootElement.Clone());

        Assert.Multiple(() =>
        {
            Assert.That(transform.Position.X, Is.EqualTo(100));
            Assert.That(transform.Position.Y, Is.EqualTo(200));
            Assert.That(transform.Rotation, Is.EqualTo(30));
            Assert.That(transform.Scale.X, Is.EqualTo(2));
            Assert.That(transform.Velocity.X, Is.EqualTo(-15));
            Assert.That(transform.Velocity.Y, Is.EqualTo(7));
            Assert.That(transform.Layer, Is.EqualTo(3));
        });
    }

    [Test]
    public void EditingOneFieldKeepsTheOthers()
    {
        var component = ComponentOfType("CTransform");
        component.RawJson = """
        {
          "type": "CTransform",
          "position": { "x": 1, "y": 2 },
          "velocity": { "x": 3, "y": 4 },
          "layer": 5
        }
        """;

        component.PosX = 9;

        using var document = JsonDocument.Parse(component.RawJson);
        var transform = (CTransform)new ComponentFactory(assets: null!)
            .CreateComponent("CTransform", document.RootElement.Clone());

        Assert.Multiple(() =>
        {
            Assert.That(transform.Position.X, Is.EqualTo(9));
            Assert.That(transform.Velocity.X, Is.EqualTo(3));
            Assert.That(transform.Layer, Is.EqualTo(5), "editing the position must not drop the layer");
        });
    }
}
