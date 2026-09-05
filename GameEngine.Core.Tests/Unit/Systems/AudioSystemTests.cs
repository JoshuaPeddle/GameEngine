using System.Text;
using GameEngine.Core.Systems;

namespace GameEngine.Core.Tests.Unit.Systems;

public class AudioSystemTests
{
    private const string Manifest = """
        {
          "textures": {},
          "sounds": { "Hit": "sounds/hit.wav", "Level1": "sounds/level1.wav" }
        }
        """;

    // The headless implementation: it records what it was asked to play instead of playing it,
    // which is how a scene's audio can be asserted without a device.
    private sealed class RecordingAudioBackend : IAudioBackend
    {
        public readonly List<(string Sound, string Path, SoundType Type)> Played = [];
        public int Decodes;
        public int StopAllCalls;
        public bool Disposed;

        private readonly HashSet<string> _decoded = [];

        public string Name => "recording";

        public bool IsAvailable { get; init; } = true;

        public string? UnavailableReason { get; init; }

        public void Play(string soundName, string path, SoundType soundType)
        {
            if (_decoded.Add(soundName))
                Decodes++;

            Played.Add((soundName, path, soundType));
        }

        public void StopAll() => StopAllCalls++;

        public void Dispose() => Disposed = true;
    }

    private static IAssetSource ManifestSource() =>
        new DelegateAssetSource(path => AssetManifest.IsManifestPath(path)
            ? new MemoryStream(Encoding.UTF8.GetBytes(Manifest))
            : throw new FileNotFoundException(path));

    [SetUp]
    [TearDown]
    public void ClearRegisteredBackend() => AudioBackends.Factory = null;

    [Test]
    public void WithNoRegisteredBackend_TheServiceExistsAndSaysWhyItIsSilent()
    {
        using var engine = new Engine(audioEnabled: true, assetSource: ManifestSource());
        var audio = engine.Systems.TryGet<AudioSystem>();

        Assert.Multiple(() =>
        {
            Assert.That(audio, Is.Not.Null, "an engine with audio enabled always has the service");
            Assert.That(audio!.IsAvailable, Is.False);
            Assert.That(audio.UnavailableReason, Does.Contain("AudioBackends.Factory"));
        });
    }

    [Test]
    public void EngineWithAudioDisabled_RegistersNoAudioSystem()
    {
        using var engine = new Engine(audioEnabled: false);

        Assert.That(engine.Systems.TryGet<AudioSystem>(), Is.Null);
    }

    [Test]
    public void ARegisteredBackendIsUsed()
    {
        var backend = new RecordingAudioBackend();
        AudioBackends.Factory = () => backend;

        using var engine = new Engine(audioEnabled: true, assetSource: ManifestSource());
        var audio = engine.Systems.Get<AudioSystem>();
        audio.Play("Hit", SoundType.SoundEffect);

        Assert.Multiple(() =>
        {
            Assert.That(audio.IsAvailable, Is.True);
            Assert.That(audio.BackendName, Is.EqualTo("recording"));
            Assert.That(backend.Played, Is.EqualTo(new[] { ("Hit", "sounds/hit.wav", SoundType.SoundEffect) }));
        });
    }

    [Test]
    public void ABackendThatThrowsWhileStartingLeavesTheServiceUnavailable()
    {
        AudioBackends.Factory = () => throw new InvalidOperationException("no device here");

        using var audio = new AudioSystem(ManifestSource());

        Assert.Multiple(() =>
        {
            Assert.That(audio.IsAvailable, Is.False);
            Assert.That(audio.UnavailableReason, Does.Contain("no device here"));
            Assert.DoesNotThrow(() => audio.Play("Hit", SoundType.SoundEffect));
        });
    }

    [Test]
    public void AnUnavailableBackendIsNeverAskedToPlay()
    {
        var backend = new RecordingAudioBackend { IsAvailable = false, UnavailableReason = "muted" };
        using var audio = new AudioSystem(ManifestSource(), backend);

        audio.Play("Hit", SoundType.SoundEffect);

        Assert.Multiple(() =>
        {
            Assert.That(backend.Played, Is.Empty);
            Assert.That(audio.UnavailableReason, Is.EqualTo("muted"));
        });
    }

    [Test]
    public void ASoundIsDecodedOnceAndReused()
    {
        var backend = new RecordingAudioBackend();
        using var audio = new AudioSystem(ManifestSource(), backend);

        audio.Play("Hit", SoundType.SoundEffect);
        audio.Play("Hit", SoundType.SoundEffect);
        audio.Play("Level1", SoundType.BGM);

        Assert.Multiple(() =>
        {
            Assert.That(backend.Decodes, Is.EqualTo(2));
            Assert.That(backend.Played, Has.Count.EqualTo(3));
        });
    }

    [Test]
    public void ASoundTheManifestDoesNotDeclareIsIgnoredRatherThanThrown()
    {
        var backend = new RecordingAudioBackend();
        using var audio = new AudioSystem(ManifestSource(), backend);

        Assert.DoesNotThrow(() => audio.Play("Nonexistent", SoundType.SoundEffect));
        Assert.That(backend.Played, Is.Empty);
    }

    [Test]
    public void TheServiceDisposesOnlyABackendItOwns()
    {
        var borrowed = new RecordingAudioBackend();
        new AudioSystem(ManifestSource(), borrowed).Dispose();

        var owned = new RecordingAudioBackend();
        new AudioSystem(ManifestSource(), owned, ownsBackend: true).Dispose();

        Assert.Multiple(() =>
        {
            Assert.That(borrowed.Disposed, Is.False, "a backend the host owns outlives the engine");
            Assert.That(borrowed.StopAllCalls, Is.EqualTo(1), "but its sounds are stopped");
            Assert.That(owned.Disposed, Is.True);
        });
    }

    [Test]
    public void Dispose_IsIdempotent()
    {
        var audio = new AudioSystem(ManifestSource(), new RecordingAudioBackend());

        Assert.DoesNotThrow(() =>
        {
            audio.Dispose();
            audio.Dispose();
        });
    }

    [Test]
    public void Play_AfterDispose_DoesNothing()
    {
        var backend = new RecordingAudioBackend();
        var audio = new AudioSystem(ManifestSource(), backend);
        audio.Dispose();

        audio.Play("Hit", SoundType.SoundEffect);

        Assert.Multiple(() =>
        {
            Assert.That(backend.Played, Is.Empty);
            Assert.That(audio.IsAvailable, Is.False);
            Assert.That(audio.UnavailableReason, Does.Contain("disposed"));
        });
    }

    [Test]
    public void TheAudioDeviceSurvivesASceneChange()
    {
        var backend = new RecordingAudioBackend();
        AudioBackends.Factory = () => backend;

        using var engine = new Engine(audioEnabled: true, assetSource: ManifestSource());
        var first = engine.Systems.Get<AudioSystem>();

        engine.InitializeSystems();
        var second = engine.Systems.Get<AudioSystem>();

        Assert.Multiple(() =>
        {
            Assert.That(second, Is.SameAs(first));
            Assert.That(backend.Disposed, Is.False);
        });
    }

    [Test]
    public void TheSilentBackendIsAvailableAndPlaysNothing()
    {
        using var backend = new SilentAudioBackend();
        using var audio = new AudioSystem(ManifestSource(), backend);

        Assert.Multiple(() =>
        {
            Assert.That(audio.IsAvailable, Is.True);
            Assert.That(audio.UnavailableReason, Is.Null);
            Assert.DoesNotThrow(() => audio.Play("Hit", SoundType.BGM));
        });
    }
}
