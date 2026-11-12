using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using GameEngine.Core;
using GameEngine.Demo;

namespace GameEngine.Runner.Avalonia.Desktop;

public sealed class UiGalleryApp : Application
{
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var engine = new Engine(audioEnabled: false) { TargetFrameRate = 60 };
            var scene = new SceneUiGallery();
            engine.SizeChanged(360, 800); engine.ChangeScene(scene); engine.Tick(0); engine.NotifyFirstPresent();
            var window = new Window { Title = "Aurora | Game UI gallery", Width = 390, Height = 844,
                MinWidth = 320, MinHeight = 600, Content = new UiGalleryView(scene) };
            window.Opened += (_, _) => _ = engine.Start();
            window.Closed += (_, _) => engine.Dispose();
            desktop.MainWindow = window;
        }
        base.OnFrameworkInitializationCompleted();
    }
}
