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

    private sealed class RecordingAudioBackend : IAudioBackend
    {
        public readonly List<(string Sound, string Path, SoundType Type)> Played = [];
        public int Decodes;
        public int StopAllCalls;
        public bool Disposed;
        public readonly Dictionary<int, float> Volumes = [];
        public readonly HashSet<int> Active = [];
        public readonly List<int> Stopped = [];
        public readonly List<string> Preloaded = [];
        public bool AcceptPlayback = true;

        private readonly HashSet<string> _decoded = [];

        public string Name => "recording";

        public bool IsAvailable { get; init; } = true;

        public string? UnavailableReason { get; init; }

        public void Preload(string soundName, string path, SoundType soundType)
        {
            Preloaded.Add(soundName);
            if (_decoded.Add(soundName)) Decodes++;
        }

        public bool TryPlay(int voice, string soundName, string path, SoundType soundType, float volume, int priority)
        {
            if (!AcceptPlayback) return false;
            if (_decoded.Add(soundName)) Decodes++;
            Played.Add((soundName, path, soundType));
            Active.Add(voice);
            Volumes[voice] = volume;
            return true;
        }

        public bool IsPlaying(int voice) => Active.Contains(voice);
        public void SetVolume(int voice, float volume) => Volumes[voice] = volume;
        public void StopVoice(int voice) { Stopped.Add(voice); Active.Remove(voice); }
        public void StopAll() { StopAllCalls++; Active.Clear(); }

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
        audio.Update(new EntityManager(), 0);

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
        audio.Update(new EntityManager(), 0);
        audio.Play("Hit", SoundType.SoundEffect);
        audio.Update(new EntityManager(), 0.1);
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
    [Test]
    public void IdenticalEventsCoalesceWithinAFrame()
    {
        var backend = new RecordingAudioBackend();
        using var audio = new AudioSystem(ManifestSource(), backend);
        for (int i = 0; i < 1000; i++) audio.Play("Hit", SoundType.SoundEffect);
        Assert.That(backend.Played, Is.Empty);
        audio.Update(new EntityManager(), 0.01);
        Assert.That(backend.Played, Has.Count.EqualTo(1));
    }

    [Test]
    public void ConfiguredFrameAndConcurrentLimitsBoundABurst()
    {
        var backend = new RecordingAudioBackend();
        using var audio = new AudioSystem(ManifestSource(), backend);
        audio.ConfigureSound("Hit", new SoundPlaybackOptions(3, 0, maxEventsPerFrame: 2));
        for (int frame = 0; frame < 10; frame++)
        {
            for (int i = 0; i < 20; i++) audio.Play("Hit", SoundType.SoundEffect);
            audio.Update(new EntityManager(), 0.01);
            Assert.That(backend.Active.Count, Is.LessThanOrEqualTo(3));
            if (frame == 0) Assert.That(backend.Played, Has.Count.EqualTo(2));
        }
        Assert.That(backend.Played, Has.Count.EqualTo(3));
    }

    [Test]
    public void LoweringASoundCapStopsSurplusVoicesImmediately()
    {
        var backend = new RecordingAudioBackend();
        using var audio = new AudioSystem(ManifestSource(), backend);
        audio.ConfigureSound("Hit", new SoundPlaybackOptions(3, 0, maxEventsPerFrame: 3));
        for (int i = 0; i < 3; i++) audio.Play("Hit", SoundType.SoundEffect);
        audio.Update(new EntityManager(), 0);
        audio.ConfigureSound("Hit", new SoundPlaybackOptions(1, 0));
        Assert.That(backend.Active, Has.Count.EqualTo(1));
        Assert.That(backend.Stopped, Has.Count.EqualTo(2));
    }

    [Test]
    public void RetriggerIntervalUsesSimulationSecondsAndCompletedVoicesBecomeFree()
    {
        var backend = new RecordingAudioBackend();
        using var audio = new AudioSystem(ManifestSource(), backend);
        audio.ConfigureSound("Hit", new SoundPlaybackOptions(1, 0.1));
        audio.Play("Hit", SoundType.SoundEffect);
        audio.Update(new EntityManager(), 0);
        backend.Active.Clear();
        audio.Play("Hit", SoundType.SoundEffect);
        audio.Update(new EntityManager(), 0.05);
        Assert.That(backend.Played, Has.Count.EqualTo(1));
        audio.Play("Hit", SoundType.SoundEffect);
        audio.Update(new EntityManager(), 0.051);
        Assert.That(backend.Played, Has.Count.EqualTo(2));
    }

    [Test]
    public void GlobalCapProtectsEqualPriorityAndHigherPriorityStealsOldestVoice()
    {
        var backend = new RecordingAudioBackend();
        using var audio = new AudioSystem(ManifestSource(), backend, maxVoices: 1);
        audio.ConfigureSound("Hit", new SoundPlaybackOptions(2, 0));
        audio.Play("Hit", SoundType.SoundEffect);
        audio.Update(new EntityManager(), 0);
        audio.Play("Level1", SoundType.SoundEffect);
        audio.Update(new EntityManager(), 0);
        Assert.That(backend.Played, Has.Count.EqualTo(1));
        audio.Play("Level1", SoundType.SoundEffect, priority: 10);
        audio.Update(new EntityManager(), 0);
        Assert.Multiple(() =>
        {
            Assert.That(backend.Played[^1].Sound, Is.EqualTo("Level1"));
            Assert.That(backend.Stopped, Is.EqualTo(new[] { 1 }));
            Assert.That(backend.Active, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void PerSoundLimitCanStealOnlyForAHigherPriorityEvent()
    {
        var backend = new RecordingAudioBackend();
        using var audio = new AudioSystem(ManifestSource(), backend);
        audio.ConfigureSound("Hit", new SoundPlaybackOptions(1, 0));
        audio.Play("Hit", SoundType.SoundEffect);
        audio.Update(new EntityManager(), 0);
        audio.Play("Hit", SoundType.SoundEffect, 2);
        audio.Update(new EntityManager(), 0);
        Assert.That(backend.Stopped, Is.EqualTo(new[] { 1 }));
        Assert.That(backend.Active, Has.Count.EqualTo(1));
    }

    [Test]
    public void PendingEventsAreRankedBeforeAllocatingVoices()
    {
        var backend = new RecordingAudioBackend();
        using var audio = new AudioSystem(ManifestSource(), backend, maxVoices: 1);
        audio.Play("Hit", SoundType.SoundEffect);
        audio.Play("Level1", SoundType.SoundEffect, 4);
        audio.Update(new EntityManager(), 0);
        Assert.That(backend.Played.Select(play => play.Sound), Is.EqualTo(new[] { "Level1" }));
        Assert.That(backend.Stopped, Is.Empty);
    }

    [Test]
    public void VolumesAndMuteReachActiveMusicAndEffectsAndFuturePlayback()
    {
        var backend = new RecordingAudioBackend();
        using var audio = new AudioSystem(ManifestSource(), backend);
        audio.MasterVolume = 0.5f;
        audio.MusicVolume = 0.4f;
        audio.SfxVolume = 0.6f;
        audio.Play("Level1", SoundType.BGM);
        audio.Play("Hit", SoundType.SoundEffect);
        audio.Update(new EntityManager(), 0);
        Assert.Multiple(() =>
        {
            Assert.That(backend.Volumes[0], Is.EqualTo(0.2f));
            Assert.That(backend.Volumes[1], Is.EqualTo(0.3f));
        });
        audio.Muted = true;
        Assert.That(backend.Volumes.Values, Is.All.Zero);
        audio.Play("Hit", SoundType.SoundEffect);
        audio.Update(new EntityManager(), 0.1);
        Assert.That(backend.Volumes[2], Is.Zero);
        audio.SfxVolume = 0.2f;
        audio.Muted = false;
        Assert.That(backend.Volumes[1], Is.EqualTo(0.1f));
        Assert.That(backend.Volumes[2], Is.EqualTo(0.1f));
        Assert.That(backend.Volumes[0], Is.EqualTo(0.2f));
    }

    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    [TestCase(-0.1f)]
    [TestCase(1.1f)]
    public void InvalidVolumesAreRejected(float value)
    {
        using var audio = new AudioSystem(ManifestSource(), new RecordingAudioBackend());
        Assert.Throws<ArgumentOutOfRangeException>(() => audio.MasterVolume = value);
        Assert.Throws<ArgumentOutOfRangeException>(() => audio.MusicVolume = value);
        Assert.Throws<ArgumentOutOfRangeException>(() => audio.SfxVolume = value);
    }

    [Test]
    public void PreloadIsIdempotentAndDoesNotPlayOrDecodeTextures()
    {
        var backend = new RecordingAudioBackend();
        using var audio = new AudioSystem(ManifestSource(), backend);
        audio.Preload("Hit");
        audio.Preload("Hit");
        audio.Preload("Missing");
        audio.Play("Hit", SoundType.SoundEffect);
        audio.Update(new EntityManager(), 0);
        Assert.That(backend.Preloaded, Is.EqualTo(new[] { "Hit" }));
        Assert.That(backend.Decodes, Is.EqualTo(1));
    }

    [Test]
    public void StopAllClearsQueuedEffectsAndRetriggerHistory()
    {
        var backend = new RecordingAudioBackend();
        using var audio = new AudioSystem(ManifestSource(), backend);
        audio.Play("Hit", SoundType.SoundEffect);
        audio.StopAll();
        audio.Update(new EntityManager(), 0);
        Assert.That(backend.Played, Is.Empty);
        audio.Play("Hit", SoundType.SoundEffect);
        audio.Update(new EntityManager(), 0);
        audio.StopAll();
        audio.Play("Hit", SoundType.SoundEffect);
        audio.Update(new EntityManager(), 0);
        Assert.That(backend.Played, Has.Count.EqualTo(2));
    }

    [Test]
    public void FailedPlaybackDoesNotConsumeVoiceOrRetriggerBudget()
    {
        var backend = new RecordingAudioBackend { AcceptPlayback = false };
        using var audio = new AudioSystem(ManifestSource(), backend, maxVoices: 1);
        audio.Play("Hit", SoundType.SoundEffect);
        audio.Update(new EntityManager(), 0);
        backend.AcceptPlayback = true;
        audio.Play("Hit", SoundType.SoundEffect);
        audio.Update(new EntityManager(), 0);
        Assert.That(backend.Played, Has.Count.EqualTo(1));
    }

    [Test]
    public void UnavailableBackendKeepsItsDiagnosticNameThroughTheInterface()
    {
        IAudioBackend backend = new UnavailableAudioBackend("device", "missing");
        Assert.That(backend.Name, Is.EqualTo("device"));
    }

}
