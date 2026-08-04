using GameEngine.Core.Systems;

namespace GameEngine.Core.Tests.Unit.Systems;

public class AudioSystemTests
{
    [Test]
    public void TryCreate_NeverThrows()
    {
        AudioSystem? audioSystem = null;
        Assert.DoesNotThrow(() => audioSystem = AudioSystem.TryCreate(new FileAssetSource()));
        audioSystem?.Dispose();
    }

    [Test]
    public void TryCreate_ReturnsNullOnPlatformsWithoutAMixer()
    {
        if (AudioSystem.IsSupportedPlatform)
            Assert.Ignore("This platform supports SDL2_mixer; nothing to assert.");

        Assert.That(AudioSystem.TryCreate(new FileAssetSource()), Is.Null);
    }

    [Test]
    public void EngineWithAudioDisabled_RegistersNoAudioSystem()
    {
        var engine = new Engine(audioEnabled: false);

        Assert.That(engine.Systems.TryGet<AudioSystem>(), Is.Null);
    }

    [Test]
    public void EngineWithAudioEnabled_Constructs()
    {
        Engine? engine = null;
        Assert.DoesNotThrow(() => engine = new Engine(audioEnabled: true));

        var audioSystem = engine!.Systems.TryGet<AudioSystem>();
        if (!AudioSystem.IsSupportedPlatform)
            Assert.That(audioSystem, Is.Null, "unsupported platforms must not register a system");
    }

    [Test]
    public void Dispose_IsIdempotent()
    {
        var audioSystem = AudioSystem.TryCreate(new FileAssetSource());
        if (audioSystem is null)
            Assert.Ignore("No audio device on this machine.");

        Assert.DoesNotThrow(() =>
        {
            audioSystem!.Dispose();
            audioSystem.Dispose();
        });
    }

    [Test]
    public void Play_AfterDispose_DoesNothing()
    {
        var audioSystem = AudioSystem.TryCreate(new FileAssetSource());
        if (audioSystem is null)
            Assert.Ignore("No audio device on this machine.");

        audioSystem!.Dispose();

        Assert.DoesNotThrow(() => audioSystem.Play("anything", SoundType.SoundEffect));
    }
}
