using GameEngine.Editor.Magic;

namespace GameEngine.Editor.Tests;

public class CompileDiagnosticTests
{
    private static CompileDiagnostic Error(string message) =>
        new("CS0103", "error", message, "/project/SceneMenu.cs", 42, 17);

    [Test]
    public void ADiagnosticReadsLikeACompilerLine()
    {
        Assert.That(Error("The name 'foo' does not exist").ToString(),
            Is.EqualTo("SceneMenu.cs(42,17): error CS0103: The name 'foo' does not exist"));
    }

    [Test]
    public void ADiagnosticWithNoSourceLocationOmitsIt()
    {
        var diagnostic = new CompileDiagnostic("CS0006", "error", "Metadata file not found", "", 0, 0);

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Location, Is.Empty);
            Assert.That(diagnostic.ToString(), Is.EqualTo("error CS0006: Metadata file not found"));
        });
    }

    [Test]
    public void TheExceptionMessageCarriesEveryError()
    {
        var exception = new SceneCompilationException([Error("first"), Error("second")]);

        Assert.Multiple(() =>
        {
            Assert.That(exception.Message, Does.StartWith("2 compile errors:"));
            Assert.That(exception.Message, Does.Contain("first").And.Contain("second"));
        });
    }

    [Test]
    public void WarningsAreNotCountedAsErrors()
    {
        var warning = new CompileDiagnostic("CS0168", "warning", "unused variable", "/p/S.cs", 1, 1);

        Assert.Multiple(() =>
        {
            Assert.That(warning.IsError, Is.False);
            Assert.That(Error("boom").IsError, Is.True);
        });
    }
}
