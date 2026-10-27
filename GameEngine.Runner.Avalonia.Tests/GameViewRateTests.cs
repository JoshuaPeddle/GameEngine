using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using GameEngine.Core;

namespace GameEngine.Runner.Avalonia.Tests;

public class GameViewRateTests
{
    [AvaloniaTest]
    public void OwnedEngineUsesConfiguredPresentationAndSimulationRates()
    {
        var view = new GameView { AudioEnabled = false, TargetFrameRate = 30, FixedTimeStep = 1.0 / 50 };
        var window = new Window { Content = view };
        try
        {
            window.Show();
            var engine = GameView.Current!;
            Assert.That(engine.TargetFrameRate, Is.EqualTo(30));
            Assert.That(engine.FixedTimeStep, Is.EqualTo(1.0 / 50));
        }
        finally
        {
            window.Content = null;
            window.Close();
        }
    }

    [AvaloniaTest]
    public void BorrowedEngineKeepsItsOwnersRates()
    {
        using var engine = new Engine(audioEnabled: false) { TargetFrameRate = 77, FixedTimeStep = 0.02 };
        var view = new GameView { Engine = engine, TargetFrameRate = 30, FixedTimeStep = 0.1 };
        var window = new Window { Content = view };
        try
        {
            window.Show();
            view.TargetFrameRate = 20;
            view.FixedTimeStep = 0.2;
            engine.Tick(0);
            Assert.That(engine.TargetFrameRate, Is.EqualTo(77));
            Assert.That(engine.FixedTimeStep, Is.EqualTo(0.02));
        }
        finally
        {
            window.Content = null;
            window.Close();
        }
    }

    [AvaloniaTest]
    public void InvalidRatesAreRejected()
    {
        var view = new GameView();
        Assert.Throws<ArgumentException>(() => view.TargetFrameRate = 0);
        Assert.Throws<ArgumentException>(() => view.FixedTimeStep = double.NaN);
    }
}
