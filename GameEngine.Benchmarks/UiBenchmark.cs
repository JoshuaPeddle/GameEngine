using System.Diagnostics;
using System.Runtime.InteropServices;
using GameEngine.UI.Avalonia;
using SkiaSharp;

internal static class UiBenchmark
{
    public static void Run()
    {
        Console.WriteLine($"{RuntimeInformation.OSDescription}; {RuntimeInformation.ProcessArchitecture}; .NET {Environment.Version}");
        using var surface = SKSurface.Create(new SKImageInfo(1080, 2400));
        var canvas = surface.Canvas; canvas.Scale(3);
        SurfaceCache.Clear();
        foreach (var theme in new[] { GameTheme.NightCoast, GameTheme.Daybreak })
        {
            var panel = new SurfaceKey(theme, Ornaments: true);
            var button = new SurfaceKey(theme, SurfaceKind.Primary);
            var row = new SurfaceKey(theme, SurfaceKind.Raised);
            void Draw()
            {
                canvas.Clear(SKColors.Black);
                SurfaceCache.Draw(canvas, panel, new SKRect(20, 20, 340, 204));
                for (int i = 0; i < 3; i++)
                {
                    float y = 224 + i * 160;
                    SurfaceCache.Draw(canvas, row, new SKRect(20, y, 340, y + 144));
                    SurfaceCache.Draw(canvas, button, new SKRect(36, y + 80, 324, y + 128));
                }
                SurfaceCache.Draw(canvas, button, new SKRect(20, 728, 340, 780));
                canvas.Flush();
            }
            var cold = Stopwatch.StartNew(); Draw(); cold.Stop();
            for (int i = 0; i < 60; i++) Draw();
            var samples = new double[300];
            long allocated = GC.GetAllocatedBytesForCurrentThread();
            long generated = SurfaceCache.GeneratedCount;
            for (int i = 0; i < samples.Length; i++)
            {
                long start = Stopwatch.GetTimestamp(); Draw();
                samples[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            }
            long bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
            Array.Sort(samples);
            Console.WriteLine($"{theme.Name}: cold={cold.Elapsed.TotalMilliseconds:F3} ms; mean={samples.Average():F3} ms; p95={samples[284]:F3} ms; managed={bytes / samples.Length} B/frame; generated while warm={SurfaceCache.GeneratedCount - generated}");
        }
        var tween = new GameTween();
        for (int i = 0; i < 10000; i++) tween.Advance(1d / 60);
        long before = GC.GetAllocatedBytesForCurrentThread(); long stamp = Stopwatch.GetTimestamp();
        for (int i = 0; i < 1_000_000; i++) tween.Advance(1d / 60);
        double elapsed = Stopwatch.GetElapsedTime(stamp).TotalMilliseconds;
        long idleBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        Console.WriteLine($"Idle tween guard x1M={elapsed:F3} ms; managed={idleBytes} B; active animation timers={GameUiDiagnostics.ActiveAnimationTimers}; cache={SurfaceCache.Count}/{SurfaceCache.Capacity}");
        SurfaceCache.Clear();
    }
}
