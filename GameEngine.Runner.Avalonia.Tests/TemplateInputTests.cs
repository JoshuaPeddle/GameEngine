using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using GameEngine.Core;
using GameEngine.Core.Components;

namespace GameEngine.Runner.Avalonia.Tests;

public class TemplateInputTests
{
    [AvaloniaTest]
    public void ATouchSwipeMovesTheStarterScenePlayer()
    {
        var scene = new GameTemplate.MainScene();
        using var engine = new Engine(audioEnabled: false,
            assetSource: new FileAssetSource(Path.Combine(AppContext.BaseDirectory, "Template")));
        engine.ChangeScene(scene);
        engine.Tick(0);
        engine.NotifyFirstPresent();
        engine.Tick(0);
        Assert.That(engine.Fault, Is.Null);
        var player = engine.EntityManager.GetEntityWithTag("player")!.GetComponent<CTransform>();
        double startX = player.Position.X;
        var view = new GameView { Engine = engine };
        var window = new Window { Width = 960, Height = 540, Content = view };
        window.Show();
        using var pointer = new global::Avalonia.Input.Pointer(200, PointerType.Touch, true);
        try
        {
            view.RaiseEvent(new PointerPressedEventArgs(view, pointer, window,
                new global::Avalonia.Point(400, 270), 0, new PointerPointProperties(), KeyModifiers.None));
            view.RaiseEvent(new PointerReleasedEventArgs(view, pointer, window,
                new global::Avalonia.Point(500, 270), 0, new PointerPointProperties(), KeyModifiers.None, MouseButton.Left));
            engine.Tick(1.0 / 60.0);

            Assert.Multiple(() =>
            {
                Assert.That(engine.Fault, Is.Null);
                Assert.That(player.Position.X, Is.GreaterThan(startX));
            });
        }
        finally { window.Content = null; window.Close(); }
    }
}
