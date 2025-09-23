using ReactiveUI;
using System;
using System.IO;
using System.Reactive;
using System.Threading.Tasks;

namespace GameEngine.Editor.ViewModels
{
    internal class CodeEditorViewModel : ViewModelBase
    {
        private readonly string _filePath;
        public string FilePath => _filePath;

        private string _sourceCode = "// Loading...";
        public string SourceCode
        {
            get => _sourceCode;
            set
            {
                var s = this.RaiseAndSetIfChanged(ref _sourceCode, value);
                if (!string.IsNullOrWhiteSpace(s))
                {
                    if (_initialized) IsDirty = true;
                }
            }
        }

        private bool _isDirty;
        public bool IsDirty
        {
            get => _isDirty;
            private set => this.RaiseAndSetIfChanged(ref _isDirty, value);
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            private set => this.RaiseAndSetIfChanged(ref _isBusy, value);
        }

        private bool _initialized;

        public ReactiveCommand<Unit, Unit> SaveCommand { get; }

        public CodeEditorViewModel(string filePath)
        {
            _filePath = filePath;
            SaveCommand = ReactiveCommand.CreateFromTask(SaveAsync, this.WhenAnyValue(v => v.IsDirty));
        }

        public CodeEditorViewModel() : this("Program.cs") { }

        public async Task InitializeAsync()
        {
            try
            {
                IsBusy = true;
                if (File.Exists(_filePath))
                {
                    var text = await File.ReadAllTextAsync(_filePath);
                    SourceCode = string.IsNullOrEmpty(text) ? "// (File is empty)" : text;
                }
                else
                {
                    SourceCode = $"// File not found: {_filePath}";
                }
                IsDirty = false;
                _initialized = true;
            }
            catch (Exception ex)
            {
                SourceCode = $"// Error loading file: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task SaveAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_filePath)) return;
                await File.WriteAllTextAsync(_filePath, SourceCode ?? string.Empty);
                IsDirty = false;
                // Hook: trigger scene incremental compile here if applicable.
            }
            catch (Exception ex)
            {
                // Optionally expose a StatusMessage property.
                Console.WriteLine("Save failed: " + ex);
            }
        }
    }
}