using GameEngine.Core.Components;
using SkiaSharp;

namespace GameEngine.Core.Tests.Unit;

public class AnimationTests
{
    private const int FrameWidth = 10;
    private const int FrameCount = 4;

    /// <summary>A 4-frame strip, each frame 10px wide, 250ms per frame — one second per loop.</summary>
    private static Animation FourFrameStrip(float delayMs = 250f)
        => new(new SKBitmap(FrameWidth * FrameCount, 16), FrameCount, delayMs);

    private static int FrameIndexOf(CAnimation animation)
        => (int)(animation.GetSourceRect().Left / FrameWidth);

    [Test]
    public void Animation_AdvancesOnSecondsElapsed()
    {
        // Arrange: GE-05 — CAnimation accumulates whatever unit the engine feeds it, and the
        // frame delay is authored in milliseconds in assets.txt. The two must agree.
        var animation = new CAnimation(FourFrameStrip());

        // Assert: 250ms per frame, driven with seconds.
        Assert.That(FrameIndexOf(animation), Is.EqualTo(0), "starts on the first frame");

        animation.Update(0.25);
        Assert.That(FrameIndexOf(animation), Is.EqualTo(1), "quarter of a second is one frame");

        animation.Update(0.25);
        Assert.That(FrameIndexOf(animation), Is.EqualTo(2));

        animation.Update(0.50);
        Assert.That(FrameIndexOf(animation), Is.EqualTo(0), "a full second wraps the loop");
    }

    [Test]
    public void Animation_HoldsFrameForItsFullDuration()
    {
        var animation = new CAnimation(FourFrameStrip());

        animation.Update(0.24);
        Assert.That(FrameIndexOf(animation), Is.EqualTo(0), "still inside the first frame");

        animation.Update(0.02);
        Assert.That(FrameIndexOf(animation), Is.EqualTo(1), "crossed into the second frame");
    }

    [Test]
    public void Animation_WithZeroDelay_StaysOnFirstFrameAndDoesNotThrow()
    {
        // Arrange: GE-53 — assets.txt authors several single-frame animations with a delay of
        // 0. Dividing elapsed time by that delay yields infinity, which casts to int.MinValue;
        // it only avoided an IndexOutOfRange because those animations have exactly one frame.
        var animation = new CAnimation(FourFrameStrip(delayMs: 0f));

        Assert.DoesNotThrow(() =>
        {
            animation.Update(1.0);
            _ = animation.GetSourceRect();
        });

        Assert.That(FrameIndexOf(animation), Is.EqualTo(0),
            "a zero delay means a static image, not an undefined frame");
    }

    [Test]
    public void Animation_SingleFrame_StaysOnFirstFrame()
    {
        var animation = new CAnimation(new Animation(new SKBitmap(10, 16), 1, 0f));

        animation.Update(5.0);

        Assert.That(animation.GetSourceRect().Left, Is.EqualTo(0));
    }
}
