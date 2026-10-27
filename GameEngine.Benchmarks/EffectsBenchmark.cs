using System.Diagnostics;
using System.Runtime.InteropServices;
using GameEngine.Core;
using GameEngine.Core.Effects;
using GameEngine.Core.Systems;
using SkiaSharp;

internal static class EffectsBenchmark
{
    public static void Run(bool metal)
    {
        using var gpu = metal ? new MetalBenchmarkContext() : null;
        Console.WriteLine($"{RuntimeInformation.OSDescription}; {RuntimeInformation.ProcessArchitecture}; {RuntimeInformation.FrameworkDescription}");
        Console.WriteLine($"{(metal ? "Metal GPU SKSurface with synchronized submit" : "Raster SKSurface")}, 1440x3040 output, 720x1000 virtual, 300 bright points, 12 warmup, 60 measured frames");
        Console.WriteLine("case,mean_ms,p95_ms,bytes_per_frame");
        const string source = "uniform float time; uniform float2 resolution; uniform float2 camera; half4 main(float2 p) { float2 uv=p/resolution; float w=sin(uv.x*21.+uv.y*34.-time*.7)+sin(uv.x*47.-uv.y*15.+time*.3); float a=exp(-abs(uv.y-.15-.04*sin(uv.x*5.+time*.13))*24.); return half4(float3(.02,.04,.1)+a*float3(.04,.3,.2)+pow(max(0.,w*.25+.46),12.)*.06,1); }";
        using var shader = new RuntimeShader(source, SKColors.Navy);
        foreach (var (name, background, quality) in new[] {
            ("baseline", false, BloomQuality.Off), ("shader", true, BloomQuality.Off),
            ("bloom-low", false, BloomQuality.Low), ("bloom-high", false, BloomQuality.High),
            ("shader+bloom-low", true, BloomQuality.Low), ("shader+bloom-high", true, BloomQuality.High) })
        {
            using var surface = gpu is null ? SKSurface.Create(new SKImageInfo(1440, 3040)) : SKSurface.Create(gpu.Context, true, new SKImageInfo(1440, 3040));
            using var paint = new SKPaint { Color = SKColor.Parse("#63FADA"), IsAntialias = true };
            using var effects = new RenderEffects { Background = background ? shader : null, Bloom = quality, Layers = [new((canvas, _) =>
            {
                for (int i = 0; i < 300; i++) canvas.DrawCircle(10 + i * 37 % 700, 10 + i * 79 % 980, 3, paint);
            }, true)] };
            using var renderer = new RenderSystem(new RenderOptions { VirtualWidth = 720, VirtualHeight = 1000, DrawBoundingBoxes = false, Effects = effects });
            for (int i = 0; i < 12; i++) renderer.DrawEntitiesToCanvas(surface.Canvas, RenderSnapshot.Empty);
            gpu?.Context.Flush(); gpu?.Context.Submit(true);
            var samples = new double[60];
            long allocated = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < samples.Length; i++)
            {
                long start = Stopwatch.GetTimestamp();
                renderer.AdvanceEffects(1.0 / 60, new(default, 1));
                renderer.DrawEntitiesToCanvas(surface.Canvas, RenderSnapshot.Empty);
                surface.Canvas.Flush();
                gpu?.Context.Flush(); gpu?.Context.Submit(true);
                samples[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            }
            long bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
            Array.Sort(samples);
            Console.WriteLine($"{name},{samples.Average():F3},{samples[(int)(samples.Length * .95)]:F3},{bytes / samples.Length}");
        }
    }
}

internal sealed class MetalBenchmarkContext : IDisposable
{
    private readonly IntPtr device;
    private readonly IntPtr queue;
    public GRContext Context { get; }
    public MetalBenchmarkContext()
    {
        if (!OperatingSystem.IsMacOS()) throw new PlatformNotSupportedException("The Metal benchmark requires macOS.");
        device = MTLCreateSystemDefaultDevice();
        queue = objc_msgSend(device, sel_registerName("newCommandQueue"));
        if (device == IntPtr.Zero || queue == IntPtr.Zero) throw new InvalidOperationException("Metal device unavailable.");
        Context = GRContext.CreateMetal(new GRMtlBackendContext { DeviceHandle = device, QueueHandle = queue }) ?? throw new InvalidOperationException("Skia Metal context unavailable.");
    }
    public void Dispose()
    {
        Context.Dispose();
        objc_msgSend(queue, sel_registerName("release"));
        objc_msgSend(device, sel_registerName("release"));
    }
    [DllImport("/System/Library/Frameworks/Metal.framework/Metal")]
    private static extern IntPtr MTLCreateSystemDefaultDevice();
    [DllImport("/usr/lib/libobjc.A.dylib")]
    private static extern IntPtr sel_registerName(string name);
    [DllImport("/usr/lib/libobjc.A.dylib")]
    private static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector);
}
