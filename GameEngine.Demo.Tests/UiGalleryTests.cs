using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using GameEngine.UI.Avalonia;
using SkiaSharp;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace GameEngine.Demo.Tests;

public sealed class UiGalleryTests
{
    private static Window Open(Control content, double width = 360, double height = 800)
    {
        var window = new Window { Width = width, Height = height, Content = content };
        window.Show(); window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); return window;
    }

    private static byte[] Render(Control control, int width = 1080, int height = 2400)
    {
        using var bitmap = new RenderTargetBitmap(new PixelSize(width, height), new Vector(288, 288));
        bitmap.Render(control);
        using var stream = new MemoryStream(); bitmap.Save(stream, new PngBitmapEncoderOptions()); return stream.ToArray();
    }

    [AvaloniaTest]
    public void GalleryRendersEveryPageInBothThemes()
    {
        string? directory = Environment.GetEnvironmentVariable("UI_KIT_CAPTURE_DIR");
        foreach (bool bright in new[] { false, true })
        {
            var harness = SceneHarness.Load(new SceneUiGallery());
            using var engine = harness.Engine;
            var gallery = new UiGalleryView((SceneUiGallery)harness.Scene, bright);
            GameUi.SetReducedMotion(gallery, true);
            var window = Open(gallery);
            try
            {
                for (int page = 0; page < 5; page++)
                {
                    gallery.Sheet.Close();
                    gallery.Navigation.SelectedIndex = page < 3 ? page : page == 3 ? 0 : 2;
                    if (page == 3) gallery.Sheet.Open();
                    window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
                    if (page == 4) { gallery.ScrollToEnd(); gallery.Toast.Show("Tidal memory installed"); window.UpdateLayout(); }
                    byte[] png = Render(window);
                    using var decoded = SKBitmap.Decode(png);
                    Assert.That(decoded.Width, Is.EqualTo(1080)); Assert.That(decoded.Height, Is.EqualTo(2400));
                    Assert.That(png.Length, Is.GreaterThan(30_000));
                    if (directory is not null)
                    {
                        Directory.CreateDirectory(directory);
                        File.WriteAllBytes(Path.Combine(directory, $"{(bright ? "daybreak" : "night-coast")}-{new[] { "workshop", "settings", "library", "sheet", "details" }[page]}.png"), png);
                    }
                }
                Assert.That(engine.IsFaulted, Is.False);
            }
            finally { window.Close(); }
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ThemeTextMeetsContrast(bool bright)
    {
        var p = (bright ? GameTheme.Daybreak : GameTheme.NightCoast).Palette;
        foreach (var foreground in new[] { p.Ink, p.Muted, p.Accent, p.Danger, p.Rare, p.Epic, p.Legendary })
            foreach (var background in new[] { p.Background, p.Surface, p.Raised })
                Assert.That(GameTheme.Contrast(foreground, background), Is.GreaterThanOrEqualTo(4.5), $"{foreground} on {background}");
        Assert.That(GameTheme.Contrast(p.OnAccent, p.Accent), Is.GreaterThanOrEqualTo(4.5));
    }

    [AvaloniaTest]
    public void PurchaseUsesTheRealEngineAndUpdatesTheCounter()
    {
        var harness = SceneHarness.Load(new SceneUiGallery()); using var engine = harness.Engine;
        var gallery = new UiGalleryView((SceneUiGallery)harness.Scene); GameUi.SetReducedMotion(gallery, true);
        var window = Open(gallery);
        try
        {
            gallery.UpgradeCard.ActionButton.Focus(); window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null); window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            harness.Run(1); Dispatcher.UIThread.RunJobs();
            Assert.That(((SceneUiGallery)harness.Scene).State.Salvage, Is.EqualTo(10440));
            Assert.That(gallery.Wallet.DisplayText, Is.EqualTo("10.4K"));
            Assert.That(gallery.Wallet.HasActiveMotion, Is.False);
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public async Task SettledGallerySchedulesNoAnimationTicks()
    {
        var results = new List<object>();
        using var process = Process.GetCurrentProcess();
        foreach (bool bright in new[] { false, true })
        {
            var harness = SceneHarness.Load(new SceneUiGallery()); using var engine = harness.Engine;
            var gallery = new UiGalleryView((SceneUiGallery)harness.Scene, bright);
            var window = Open(gallery);
            try
            {
                for (int page = 0; page < 4; page++)
                {
                    gallery.Sheet.Close();
                    gallery.Navigation.SelectedIndex = page < 3 ? page : 0;
                    if (page == 3) gallery.Sheet.Open();
                    window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
                    await Task.Delay(700);
                    Assert.That(GameUiDiagnostics.ActiveAnimationTimers, Is.Zero);
                    long ticks = GameUiDiagnostics.AnimationTicks;
                    var cpu = process.TotalProcessorTime;
                    long stamp = Stopwatch.GetTimestamp();
                    await Task.Delay(1000);
                    double wallMs = Stopwatch.GetElapsedTime(stamp).TotalMilliseconds;
                    double cpuMs = (process.TotalProcessorTime - cpu).TotalMilliseconds;
                    long idleTicks = GameUiDiagnostics.AnimationTicks - ticks;
                    Assert.That(idleTicks, Is.Zero);
                    results.Add(new { Theme = bright ? "Daybreak" : "Night coast", Page = new[] { "workshop", "settings", "library", "sheet" }[page],
                        WallMs = wallMs, ProcessCpuMs = cpuMs, OneCorePercent = cpuMs / wallMs * 100,
                        ActiveAnimationTimers = GameUiDiagnostics.ActiveAnimationTimers, AnimationTicks = idleTicks });
                }
            }
            finally { window.Close(); }
        }
        string json = JsonSerializer.Serialize(new { OS = RuntimeInformation.OSDescription, Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            Runtime = Environment.Version.ToString(), Results = results }, new JsonSerializerOptions { WriteIndented = true });
        TestContext.Out.WriteLine(json);
        if (Environment.GetEnvironmentVariable("UI_KIT_CAPTURE_DIR") is { } directory)
        {
            Directory.CreateDirectory(directory); File.WriteAllText(Path.Combine(directory, "idle-measurements.json"), json + Environment.NewLine);
        }
    }

    [AvaloniaTest]
    public void WarmGalleryRepaintsReuseSurfaces()
    {
        var results = new List<object>();
        foreach (bool bright in new[] { false, true })
        {
            var harness = SceneHarness.Load(new SceneUiGallery()); using var engine = harness.Engine;
            var gallery = new UiGalleryView((SceneUiGallery)harness.Scene, bright); GameUi.SetReducedMotion(gallery, true);
            var window = Open(gallery);
            try
            {
                using var bitmap = new RenderTargetBitmap(new PixelSize(1080, 2400), new Vector(288, 288));
                for (int i = 0; i < 20; i++) bitmap.Render(window);
                long surfaces = SurfaceCache.GeneratedCount;
                var samples = new double[120];
                long allocated = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < samples.Length; i++)
                {
                    long stamp = Stopwatch.GetTimestamp(); bitmap.Render(window);
                    samples[i] = Stopwatch.GetElapsedTime(stamp).TotalMilliseconds;
                }
                long bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
                Array.Sort(samples);
                Assert.That(SurfaceCache.GeneratedCount, Is.EqualTo(surfaces));
                Assert.That(GameUiDiagnostics.ActiveAnimationTimers, Is.Zero);
                results.Add(new { Theme = bright ? "Daybreak" : "Night coast", MeanMs = samples.Average(), P95Ms = samples[113], ManagedBytesPerRepaint = bytes / samples.Length });
            }
            finally { window.Close(); }
        }
        string json = JsonSerializer.Serialize(new { OS = RuntimeInformation.OSDescription, Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            Runtime = Environment.Version.ToString(), Width = 1080, Height = 2400, Samples = 120, Warmup = 20, Results = results }, new JsonSerializerOptions { WriteIndented = true });
        TestContext.Out.WriteLine(json);
        if (Environment.GetEnvironmentVariable("UI_KIT_CAPTURE_DIR") is { } directory)
        {
            Directory.CreateDirectory(directory); File.WriteAllText(Path.Combine(directory, "gallery-measurements.json"), json + Environment.NewLine);
        }
    }
}
