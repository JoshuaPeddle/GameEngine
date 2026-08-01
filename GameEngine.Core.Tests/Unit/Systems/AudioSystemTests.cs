using GameEngine.Core.Systems;

namespace GameEngine.Core.Tests.Unit.Systems;

/// <summary>
/// GE-06: audio registration was hard-wired off behind <c>if (false==true)</c>, so the
/// <c>audioEnabled</c> constructor argument four call sites were passing did nothing, and
/// every scene received null through a non-nullable parameter.
/// <para>
/// These cover the wiring and the failure modes. Whether sound is actually audible needs a
/// human on a machine with a sound device.
/// </para>
/// </summary>
public class AudioSystemTests
{
    [Test]
    public void TryCreate_NeverThrows()
    {
        // A machine with no audio device, or no native mixer library, must still run the game.
        AudioSystem? audioSystem = null;
        Assert.DoesNotThrow(() => audioSystem = AudioSystem.TryCreate());
        audioSystem?.Dispose();
    }

    [Test]
    public void TryCreate_ReturnsNullOnPlatformsWithoutAMixer()
    {
        if (AudioSystem.IsSupportedPlatform)
            Assert.Ignore("This platform supports SDL2_mixer; nothing to assert.");

        Assert.That(AudioSystem.TryCreate(), Is.Null);
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
        // The point of TryCreate: enabling audio must never be able to take engine
        // construction down, whatever the machine looks like.
        Engine? engine = null;
        Assert.DoesNotThrow(() => engine = new Engine(audioEnabled: true));

        // Registered exactly when the platform and device allowed it.
        var audioSystem = engine!.Systems.TryGet<AudioSystem>();
        if (!AudioSystem.IsSupportedPlatform)
            Assert.That(audioSystem, Is.Null, "unsupported platforms must not register a system");
    }

    [Test]
    public void Dispose_IsIdempotent()
    {
        var audioSystem = AudioSystem.TryCreate();
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
        var audioSystem = AudioSystem.TryCreate();
        if (audioSystem is null)
            Assert.Ignore("No audio device on this machine.");

        audioSystem!.Dispose();

        // Reaching into freed native memory would be far worse than staying silent.
        Assert.DoesNotThrow(() => audioSystem.Play("anything", SoundType.SoundEffect));
    }
}
