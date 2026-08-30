using GameEngine.Editor.Services;

namespace GameEngine.Editor.Tests;

public class GameProjectCreatorTests
{
    [TestCase("MyGame")]
    [TestCase("_Game")]
    [TestCase("Space.Jelly")]
    public void AcceptsANameThatCanBecomeANamespace(string name)
    {
        Assert.That(GameProjectCreator.Validate(name), Is.Null);
    }

    [TestCase("", "Give the game a name")]
    [TestCase("   ", "Give the game a name")]
    [TestCase("my/game", "path separators")]
    [TestCase("9Lives", "start with a letter")]
    [TestCase("my game", "letters, digits")]
    public void RejectsANameWithAReason(string name, string expected)
    {
        Assert.That(GameProjectCreator.Validate(name), Does.Contain(expected));
    }

    [Test]
    public async Task RefusesToWriteIntoAFolderThatIsNotEmpty()
    {
        var parent = Directory.CreateTempSubdirectory("ge-new-project").FullName;
        try
        {
            Directory.CreateDirectory(Path.Combine(parent, "Taken"));
            File.WriteAllText(Path.Combine(parent, "Taken", "something.txt"), "in the way");

            var result = await GameProjectCreator.CreateAsync(parent, "Taken");

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.False);
                Assert.That(result.Message, Does.Contain("already exists"));
            });
        }
        finally
        {
            Directory.Delete(parent, recursive: true);
        }
    }

    [Test]
    public async Task ReportsAMissingParentFolder()
    {
        var result = await GameProjectCreator.CreateAsync(
            Path.Combine(Path.GetTempPath(), "ge-does-not-exist-" + Guid.NewGuid().ToString("N")), "MyGame");

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("does not exist"));
        });
    }

    [Test]
    public void TheCreatedProjectIsWhereTheEditorLooksForIt()
    {
        Assert.That(GameProjectCreator.ProjectPathFor("/games", "SpaceJelly"),
            Is.EqualTo(Path.Combine("/games", "SpaceJelly", "src", "SpaceJelly", "SpaceJelly.csproj")));
    }
}
