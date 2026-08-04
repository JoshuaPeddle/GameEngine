using System.Text;
using GameEngine.Core.Systems;

namespace GameEngine.Core.Tests.Unit;

public class AssetSourceTests
{
    private sealed class SourceCapturingScene : Scene
    {
        public IAssetSource? Captured;

        public override void Initialize(EntityManager entityManager, InputManager inputManager,
            AudioSystem? audioPlayer, Action<Scene?> resetScene)
        {
            Captured = AssetSource;
        }
    }

    private sealed class RecordingAssetSource : IAssetSource
    {
        public List<string> Requested { get; } = new();

        public Dictionary<string, string> Contents { get; } = new();

        public Stream Open(string path)
        {
            Requested.Add(path);

            if (!Contents.TryGetValue(path, out var content))
                throw new FileNotFoundException(path);

            return new MemoryStream(Encoding.UTF8.GetBytes(content));
        }
    }

    private static string NewTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"ge34_{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }

    [Test]
    public void FileAssetSource_ResolvesManifestRelativePathsUnderTheContentRoot()
    {
        var directory = NewTempDirectory();
        var previous = Directory.GetCurrentDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(directory, "assets", "images"));
            File.WriteAllText(Path.Combine(directory, "assets", "images", "sprite.png"), "pixels");
            Directory.SetCurrentDirectory(directory);

            using var stream = new FileAssetSource().Open("images/sprite.png");

            Assert.That(new StreamReader(stream).ReadToEnd(), Is.EqualTo("pixels"));
        }
        finally
        {
            Directory.SetCurrentDirectory(previous);
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void FileAssetSource_PrefersAnExistingFileOverTheContentRoot()
    {
        var directory = NewTempDirectory();
        var previous = Directory.GetCurrentDirectory();
        try
        {
            File.WriteAllText(Path.Combine(directory, "assets.txt"), "manifest at the root");
            Directory.CreateDirectory(Path.Combine(directory, "assets"));
            File.WriteAllText(Path.Combine(directory, "assets", "assets.txt"), "manifest under assets");
            Directory.SetCurrentDirectory(directory);

            using var stream = new FileAssetSource().Open("assets.txt");

            Assert.That(new StreamReader(stream).ReadToEnd(), Is.EqualTo("manifest at the root"));
        }
        finally
        {
            Directory.SetCurrentDirectory(previous);
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void FileAssetSource_OpensRootedPathsAsGiven()
    {
        var directory = NewTempDirectory();
        try
        {
            var path = Path.Combine(directory, "level.json");
            File.WriteAllText(path, "{}");

            using var stream = new FileAssetSource().Open(path);

            Assert.That(new StreamReader(stream).ReadToEnd(), Is.EqualTo("{}"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void FileAssetSource_HonoursACustomContentRoot()
    {
        var directory = NewTempDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(directory, "content"));
            File.WriteAllText(Path.Combine(directory, "content", "sound.wav"), "riff");

            using var stream = new FileAssetSource(Path.Combine(directory, "content")).Open("sound.wav");

            Assert.That(new StreamReader(stream).ReadToEnd(), Is.EqualTo("riff"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void DelegateAssetSource_PassesThePathThroughUnchanged()
    {
        var seen = new List<string>();
        var source = new DelegateAssetSource(path =>
        {
            seen.Add(path);
            return new MemoryStream();
        });

        source.Open("images/sprite.png").Dispose();

        Assert.That(seen, Is.EqualTo(new[] { "images/sprite.png" }));
    }

    [Test]
    public void Assets_ReadsManifestAndTexturesFromTheInjectedSource()
    {
        var source = new RecordingAssetSource();
        source.Contents["assets.txt"] = "# nothing to load";

        var assets = new Assets("assets.txt", source);

        Assert.Multiple(() =>
        {
            Assert.That(source.Requested, Is.EqualTo(new[] { "assets.txt" }));
            Assert.That(assets.Source, Is.SameAs(source));
        });
    }

    [Test]
    public void Assets_OpenGoesThroughTheSource()
    {
        var source = new RecordingAssetSource();
        source.Contents["assets.txt"] = string.Empty;
        source.Contents["levels/level1.json"] = "{}";

        var assets = new Assets("assets.txt", source);
        using var stream = assets.Open("levels/level1.json");

        Assert.That(new StreamReader(stream).ReadToEnd(), Is.EqualTo("{}"));
    }

    [Test]
    public void Assets_RejectsAMissingSource()
    {
        Assert.Throws<ArgumentNullException>(() => new Assets("assets.txt", null!));
    }

    [Test]
    public void Scene_ReceivesTheEnginesAssetSource()
    {
        var injected = new RecordingAssetSource();
        using var engine = new Engine(audioEnabled: false, assetSource: injected);
        var scene = new SourceCapturingScene();

        engine.ChangeScene(scene);
        engine.Tick(1.0 / 60.0);

        Assert.That(scene.Captured, Is.SameAs(injected));
    }

    [Test]
    public void Scene_WithoutAnEngine_ReportsThatAssetsAreNotAvailableYet()
    {
        var scene = new SourceCapturingScene();

        Assert.Throws<InvalidOperationException>(
            () => scene.Initialize(new EntityManager(), new InputManager(), null, _ => { }));
    }

    [Test]
    public void Engine_DefaultsToTheFileSystemAndKeepsAnInjectedSource()
    {
        var injected = new RecordingAssetSource();

        using var defaulted = new Engine(audioEnabled: false);
        using var custom = new Engine(audioEnabled: false, assetSource: injected);

        Assert.Multiple(() =>
        {
            Assert.That(defaulted.AssetSource, Is.TypeOf<FileAssetSource>());
            Assert.That(custom.AssetSource, Is.SameAs(injected));
        });
    }
}
