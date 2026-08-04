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
        public static Func<string, Stream>? _fileFetcher;
        public static Func<Core.Scene>? StartupScene;
        public static Action? FirstFramePresented;

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (_fileFetcher != null)
                Core.Assets._fileFetcher = _fileFetcher;

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
