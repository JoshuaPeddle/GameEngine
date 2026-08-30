namespace GameEngine.Core.Tests.Unit;

public class AssetManifestTests
{
    private const string Sample = """
        {
          "textures": { "TexBall": "images/ball.png" },
          "animations": { "Ball": { "texture": "TexBall", "frames": 4, "frameDelayMs": 250 } },
          "sounds": { "Hit": "sounds/hit.wav" }
        }
        """;

    [Test]
    public void ReadsEverySection()
    {
        var manifest = AssetManifest.ParseJson(Sample);

        Assert.Multiple(() =>
        {
            Assert.That(manifest.Textures.Single(), Is.EqualTo(new TextureEntry("TexBall", "images/ball.png")));
            Assert.That(manifest.Animations.Single(), Is.EqualTo(new AnimationEntry("Ball", "TexBall", 4, 250)));
            Assert.That(manifest.Sounds.Single(), Is.EqualTo(new SoundEntry("Hit", "sounds/hit.wav")));
        });
    }

    [Test]
    public void FrameDelayIsOptionalAndMeansStatic()
    {
        var manifest = AssetManifest.ParseJson("""
            { "animations": { "Block": { "texture": "TexBlock", "frames": 1 } } }
            """);

        Assert.That(manifest.Animations.Single().FrameDelayMs, Is.EqualTo(0));
    }

    [Test]
    public void UnknownSection_IsRejectedWithTheKnownList()
    {
        Assert.That(() => AssetManifest.ParseJson("""{ "fonts": {} }"""),
            Throws.TypeOf<InvalidDataException>().With.Message.Contains("textures, animations, sounds"));
    }

    [Test]
    public void UnknownAnimationKey_IsRejected()
    {
        Assert.That(() => AssetManifest.ParseJson("""
            { "animations": { "Ball": { "texture": "TexBall", "frames": 1, "repeat": true } } }
            """),
            Throws.TypeOf<InvalidDataException>().With.Message.Contains("'repeat'"));
    }

    [Test]
    public void MissingAnimationTexture_NamesTheAnimation()
    {
        Assert.That(() => AssetManifest.ParseJson("""
            { "animations": { "Ball": { "frames": 1 } } }
            """),
            Throws.TypeOf<InvalidDataException>().With.Message.Contains("animations.Ball"));
    }

    [Test]
    public void RoundTripsThroughJson()
    {
        var manifest = AssetManifest.ParseJson(Sample);
        manifest.Schema = "../GameEngine.Core/assets.schema.json";

        var reloaded = AssetManifest.ParseJson(manifest.ToJson());

        Assert.Multiple(() =>
        {
            Assert.That(reloaded.Schema, Is.EqualTo(manifest.Schema));
            Assert.That(reloaded.Textures, Is.EqualTo(manifest.Textures));
            Assert.That(reloaded.Animations, Is.EqualTo(manifest.Animations));
            Assert.That(reloaded.Sounds, Is.EqualTo(manifest.Sounds));
        });
    }

    [Test]
    public void TheLineFormatIsStillRead()
    {
        var manifest = AssetManifest.ParseLines("""
            # a comment
            Texture TexBall images/ball.png
            Font Title fonts/title.ttf
            Animation Ball TexBall 4 250
            Sound Hit sounds/hit.wav
            """);

        Assert.Multiple(() =>
        {
            Assert.That(manifest.Textures.Single().Name, Is.EqualTo("TexBall"));
            Assert.That(manifest.Animations.Single(), Is.EqualTo(new AnimationEntry("Ball", "TexBall", 4, 250)));
            Assert.That(manifest.Sounds.Single().Path, Is.EqualTo("sounds/hit.wav"));
        });
    }

    [Test]
    public void AnAnimationWithNoTexture_NamesTheTextureItWanted()
    {
        var source = new InMemoryAssetSource
        {
            [AssetManifest.DefaultFileName] = """
                { "animations": { "Ball": { "texture": "TexBall", "frames": 1 } } }
                """
        };

        Assert.That(() => new Assets(AssetManifest.DefaultFileName, source),
            Throws.TypeOf<InvalidDataException>().With.Message.Contains("TexBall"));
    }

    [Test]
    public void BothManifestNamesAreRecognised()
    {
        Assert.Multiple(() =>
        {
            Assert.That(AssetManifest.IsManifestPath("assets.json"), Is.True);
            Assert.That(AssetManifest.IsManifestPath("game/assets.txt"), Is.True);
            Assert.That(AssetManifest.IsManifestPath("levels/level1.json"), Is.False);
        });
    }
}

file sealed class InMemoryAssetSource : IAssetSource
{
    private readonly Dictionary<string, string> contents = new();

    public string this[string path] { set => contents[path] = value; }

    public Stream Open(string path) =>
        contents.TryGetValue(path, out var content)
            ? new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content))
            : throw new FileNotFoundException(path);
}
