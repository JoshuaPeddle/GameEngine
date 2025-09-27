using Avalonia;
using System;

namespace GameEngine.Runner.Avalonia.Desktop
{
    internal sealed class Program
    {
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args) => BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .With(new Win32PlatformOptions
                {
                    RenderingMode = [Win32RenderingMode.Wgl, Win32RenderingMode.Vulkan, Win32RenderingMode.AngleEgl]// , ,
                })
                .With(new X11PlatformOptions 
                { 
                    RenderingMode = [X11RenderingMode.Egl, X11RenderingMode.Vulkan] 
                })
                .UsePlatformDetect()
                .LogToTrace();
    }
}
