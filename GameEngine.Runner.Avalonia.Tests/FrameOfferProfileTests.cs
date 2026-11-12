using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia;
using Avalonia.Media.Imaging;
using GameEngine.Core;
using System.Diagnostics;

namespace GameEngine.Runner.Avalonia.Tests;

public class FrameOfferProfileTests
{
    [AvaloniaTest, Explicit("Opt-in construction profile; see docs/14-frame-pacing.md")]
    public void ProfileFrameOfferConstruction()
    {
        using var engine = new Engine(audioEnabled: false);
        var view = new GameView { Engine = engine };
        var window = new Window { Content = view };
        try
        {
            window.Show();
            using var bitmap = new RenderTargetBitmap(new PixelSize(32, 32));
            using var context = bitmap.CreateDrawingContext();
            for (int i = 0; i < 100; i++) view.Render(context);
            long allocated = GC.GetAllocatedBytesForCurrentThread();
            long start = Stopwatch.GetTimestamp();
            for (int i = 0; i < 1000; i++) view.Render(context);
            double micros = Stopwatch.GetElapsedTime(start).TotalMicroseconds / 1000;
            double bytes = (GC.GetAllocatedBytesForCurrentThread() - allocated) / 1000.0;
            TestContext.Out.WriteLine($"GameView.Render offer: {micros:F3} us, {bytes:F3} B/frame");
        }
        finally { window.Content = null; window.Close(); }
    }
}
