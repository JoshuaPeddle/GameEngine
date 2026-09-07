using Avalonia.Controls;
using Avalonia.Controls.Templates;
using GameEngine.Runner.Avalonia.ViewModels;
using GameEngine.Runner.Avalonia.Views;

namespace GameEngine.Runner.Avalonia
{
    public class ViewLocator : IDataTemplate
    {
        public Control? Build(object? param) => param switch
        {
            null => null,
            MainViewModel => new MainView(),
            _ => new TextBlock { Text = "Not Found: " + param.GetType().FullName }
        };

        public bool Match(object? data) => data is ViewModelBase;
    }
}
