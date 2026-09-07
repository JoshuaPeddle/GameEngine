using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using GameEngine.Runner.Avalonia.ViewModels;
using GameEngine.Runner.Avalonia.Views;
using System.IO;
using System;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace GameEngine.Runner.Avalonia
{
    public partial class App : Application
    {
        // Convenience layer for the one-game-per-process app template: a head sets these before
        // the first GameView is constructed. Embedders hand the same things to GameView directly
        // and never touch these.
        public static Core.IAssetSource? AssetSource;
        public static Func<Core.Scene>? StartupScene;
        public static Action? FirstFramePresented;
        public static Func<Core.Vec2>? InputViewportSize;

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new MainWindow
                {
                    DataContext = new MainViewModel()
                };
            }
            else if (ApplicationLifetime is IActivityApplicationLifetime activityPlatform)
            {
                activityPlatform.MainViewFactory = CreateMainView;
            }
            else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
            {
                singleViewPlatform.MainView = CreateMainView();
                singleViewPlatform.MainView.AttachedToVisualTree += (_, _) =>
                TopLevel.GetTopLevel(singleViewPlatform.MainView)!.BackRequested += OnBackRequested;
            }
            base.OnFrameworkInitializationCompleted();
        }

        private static MainView CreateMainView()
        {
            return new MainView
            {
                DataContext = new MainViewModel()
            };
        }

        private void OnBackRequested(object? sender, RoutedEventArgs e)
        {
            // Reload the MainView with a new instance of the MainView
            if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
            {
                singleViewPlatform.MainView = CreateMainView();
                singleViewPlatform.MainView.AttachedToVisualTree += (_, _) =>
                TopLevel.GetTopLevel(singleViewPlatform.MainView)!.BackRequested += OnBackRequested;
            }
            e.Handled = true;
        }

    }
}
