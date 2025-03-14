using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using GameEngine.Runner.Avalonia.ViewModels;
using GameEngine.Runner.Avalonia.Views;
using System.IO;
using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace GameEngine.Runner.Avalonia
{
    public partial class App : Application
    {
        public static Func<string, Stream>? _fileFetcher;

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
                // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
                // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
                DisableAvaloniaDataAnnotationValidation();
                desktop.MainWindow = new MainWindow
                {
                    DataContext = new MainViewModel()
                };
            }
            else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
            {
                singleViewPlatform.MainView = new MainView
                {
                    DataContext = new MainViewModel()
                };
                singleViewPlatform.MainView.AttachedToVisualTree += (_, _) =>
                TopLevel.GetTopLevel(singleViewPlatform.MainView)!.BackRequested += OnBackRequested;
            }
            base.OnFrameworkInitializationCompleted();
        }

        private void OnBackRequested(object? sender, RoutedEventArgs e)
        {
            // Reload the MainView with a new instance of the MainView
            if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
            {
                singleViewPlatform.MainView = new MainView
                {
                    DataContext = new MainViewModel()
                };
                singleViewPlatform.MainView.AttachedToVisualTree += (_, _) =>
                TopLevel.GetTopLevel(singleViewPlatform.MainView)!.BackRequested += OnBackRequested;
            }
            e.Handled = true;
        }

        private void DisableAvaloniaDataAnnotationValidation()
        {
            // Get an array of plugins to remove
            var dataValidationPluginsToRemove =
                BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

            // remove each entry found
            foreach (var plugin in dataValidationPluginsToRemove)
            {
                BindingPlugins.DataValidators.Remove(plugin);
            }
        }
    }
}