using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using GameEngine.Editor.ViewModels;
using ReactiveUI;
using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace GameEngine.Editor;

public partial class CodeEditor : Window
{
    private readonly CompositeDisposable _disposables = new();

    public CodeEditor()
    {
        InitializeComponent();
        Unloaded += (_, _) => _disposables.Dispose();
        KeyDown += OnKeyDown;
    }

    public CodeEditor(string filePath) : this()
    {
        DataContext = new CodeEditorViewModel(filePath);
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not CodeEditorViewModel vm) return;

        vm.WhenAnyValue(v => v.SourceCode)
          .ObserveOn(RxApp.MainThreadScheduler)
          .Subscribe(text =>
          {
              if (Editor.Text != text)
                  Editor.Text = text ?? string.Empty;
          })
          .DisposeWith(_disposables);

        Editor.TextChanged += (_, _) =>
        {
            if (vm.SourceCode != Editor.Text)
                vm.SourceCode = Editor.Text;
        };

        await vm.InitializeAsync();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is CodeEditorViewModel vm &&
            e.Key == Key.S &&
            (e.KeyModifiers & KeyModifiers.Control) == KeyModifiers.Control)
        {
            vm.SaveCommand.Execute().Subscribe();
            e.Handled = true;
        }
    }
}