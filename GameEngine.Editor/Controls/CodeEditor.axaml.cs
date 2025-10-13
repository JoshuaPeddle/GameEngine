using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using AvaloniaEdit;
using AvaloniaEdit.TextMate;
using GameEngine.Editor.ViewModels;
using ReactiveUI;
using System;
using System.IO;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using TextMateSharp.Grammars;

namespace GameEngine.Editor;

public partial class CodeEditor : Window
{
    private readonly CompositeDisposable _disposables = new();

    public CodeEditor()
    {
        InitializeComponent();
        Unloaded += (_, _) => _disposables.Dispose();
        KeyDown += async (sender, e) => await OnKeyDown(sender, e);
        // Defer TextMate init until we know the file extension
    }

    public CodeEditor(string filePath) : this()
    {
        DataContext = new CodeEditorViewModel(filePath);
        Loaded += OnLoaded;
    }

    private void InitializeTextEditor(string fileExtension)
    {
        var _textEditor = this.FindControl<TextEditor>("Editor");
        var _registryOptions = new RegistryOptions(ThemeName.DarkPlus);
        var _textMateInstallation = _textEditor.InstallTextMate(_registryOptions);
        try
        {
            var lang = _registryOptions.GetLanguageByExtension(fileExtension);
            if (lang != null)
            {
                _textMateInstallation.SetGrammar(_registryOptions.GetScopeByLanguageId(lang.Id));
            }
        }
        catch
        {
            // ignore if grammar not found
        }
    }

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not CodeEditorViewModel vm) return;

        // Initialize syntax highlighting based on file extension
        var ext = Path.GetExtension(vm.FilePath);
        if (string.IsNullOrWhiteSpace(ext)) ext = ".txt";
        InitializeTextEditor(ext);

        // Bind Save menu to VM command and wire Save As
        if (this.FindControl<MenuItem>("SaveMenu") is { } saveMenu)
        {
            saveMenu.Command = vm.SaveCommand;
        }
        if (this.FindControl<MenuItem>("SaveAsMenu") is { } saveAsMenu)
        {
            saveAsMenu.Click += async (_, _) => await SaveAsAsync();
        }

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

        // Caret status
        Editor.TextArea.Caret.PositionChanged += (_, _) => UpdateCaretStatus();
        UpdateCaretStatus();

        await vm.InitializeAsync();
    }

    private void UpdateCaretStatus()
    {
        if (this.FindControl<TextBlock>("CaretStatus") is { } caret && Editor is not null)
        {
            var line = Editor.TextArea.Caret.Line;
            var col = Editor.TextArea.Caret.Column;
            caret.Text = $"Ln {line}, Col {col}";
        }
    }

    private async Task OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is CodeEditorViewModel vm &&
            e.Key == Key.S &&
            (e.KeyModifiers & KeyModifiers.Control) == KeyModifiers.Control)
        {
            if ((e.KeyModifiers & KeyModifiers.Shift) == KeyModifiers.Shift)
            {
                // Ctrl+Shift+S => Save As
                _ = SaveAsAsync();
                e.Handled = true;
                return;
            }

            if (await vm.SaveCommand.CanExecute.FirstAsync())
            {
                vm.SaveCommand.Execute().Subscribe();
            }
            else
            {
                // Allow save even if CanExecute false (e.g., new file): perform Save As
                _ = SaveAsAsync();
            }
            e.Handled = true;
        }
    }

    private async Task SaveAsAsync()
    {
        if (DataContext is not CodeEditorViewModel vm) return;
        var top = TopLevel.GetTopLevel(this);
        if (top?.StorageProvider is null) return;

        var suggestedName = string.IsNullOrWhiteSpace(vm.FilePath) ? "untitled.cs" : Path.GetFileName(vm.FilePath);
        var options = new FilePickerSaveOptions
        {
            SuggestedFileName = suggestedName,
            ShowOverwritePrompt = true,
        };
        var file = await top.StorageProvider.SaveFilePickerAsync(options);
        if (file is null) return;

        var localPath = file.TryGetLocalPath();
        if (!string.IsNullOrEmpty(localPath))
        {
            await vm.SaveToAsync(localPath);
            return;
        }

        // Fallback to stream when no local path is available
        await using var stream = await file.OpenWriteAsync();
        await using var writer = new StreamWriter(stream);
        await writer.WriteAsync(vm.SourceCode ?? string.Empty);
        vm.MarkSaved(file.Name);
    }
}