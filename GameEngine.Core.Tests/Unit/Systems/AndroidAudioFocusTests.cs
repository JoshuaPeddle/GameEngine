using GameEngine.Audio.Android;

namespace GameEngine.Core.Tests.Unit.Systems;

public class AndroidAudioFocusTests
{
    [Test]
    public void DeniedMusicFocus_LaterEffectGrantResumesMusicWithoutGainCallback()
    {
        bool hasFocus = false;
        int resumes = 0;

        bool musicAccepted = AndroidAudioFocus.Request(ref hasFocus, false, false,
            () => false, () => resumes++);

        Assert.That(musicAccepted, Is.False);
        Assert.That(hasFocus, Is.False);
        Assert.That(resumes, Is.Zero);

        bool effectAccepted = AndroidAudioFocus.Request(ref hasFocus, false, false,
            () => true, () =>
            {
                Assert.That(hasFocus, Is.True);
                resumes++;
            });

        Assert.That(effectAccepted, Is.True);
        Assert.That(hasFocus, Is.True);
        Assert.That(resumes, Is.EqualTo(1));
    }

    [Test]
    public void OwnedFocus_ResumesPreparedMusicWithoutAnotherNativeRequest()
    {
        bool hasFocus = true;
        int requests = 0;
        int resumes = 0;

        bool accepted = AndroidAudioFocus.Request(ref hasFocus, false, false,
            () => { requests++; return false; }, () => resumes++);

        Assert.That(accepted, Is.True);
        Assert.That(requests, Is.Zero);
        Assert.That(resumes, Is.EqualTo(1));
    }

    [TestCase(true, false, false)]
    [TestCase(true, false, true)]
    [TestCase(false, true, false)]
    [TestCase(false, true, true)]
    public void BackgroundOrFocusPause_DoesNotRequestFocusOrResumeMusic(
        bool backgrounded, bool focusPaused, bool hasFocus)
    {
        int requests = 0;
        int resumes = 0;

        bool accepted = AndroidAudioFocus.Request(ref hasFocus, backgrounded, focusPaused,
            () => { requests++; return true; }, () => resumes++);

        Assert.That(accepted, Is.False);
        Assert.That(requests, Is.Zero);
        Assert.That(resumes, Is.Zero);
    }
}
