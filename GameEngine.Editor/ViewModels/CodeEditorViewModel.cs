using ReactiveUI;
using System;
using System.IO;
using Unit = ReactiveUI.Primitives.RxVoid;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace GameEngine.Editor.ViewModels
{
    internal class CodeEditorViewModel : ViewModelBase
    {
        private string _filePath;
        public string FilePath
        {
            get => _filePath;
            private set => this.RaiseAndSetIfChanged(ref _filePath, value);
        }

        public string WindowTitle
        {
            get
            {
                var name = string.IsNullOrWhiteSpace(FilePath) ? "(untitled)" : Path.GetFileName(FilePath);
                return ($"{(IsDirty ? "*" : string.Empty)}{name} - Code Editor");
            }
        }

        private string _sourceCode = "// Loading...";
        public string SourceCode
        {
            get => _sourceCode;
            set
            {
                if (!string.Equals(value, _sourceCode, StringComparison.Ordinal))
                {
                    this.RaiseAndSetIfChanged(ref _sourceCode, value);
                    if (_initialized)
                    {
                        IsDirty = true;
                    }
                }
            }
        }

        private bool _isDirty;
        public bool IsDirty
        {
            get => _isDirty;
            internal set
            {
                this.RaiseAndSetIfChanged(ref _isDirty, value);
                this.RaisePropertyChanged(nameof(WindowTitle));
            }
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
            // Can save only when dirty and not busy
            SaveCommand = ReactiveCommand.CreateFromTask(SaveAsync,
                this.WhenAnyValue(v => v.IsDirty, v => v.IsBusy, (dirty, busy) => dirty && !busy));
        }

        public CodeEditorViewModel() : this("Program.cs") { }

        public async Task InitializeAsync()
        {
            try
            {
                IsBusy = true;
                if (!string.IsNullOrWhiteSpace(FilePath) && File.Exists(FilePath))
                {
                    var text = await File.ReadAllTextAsync(FilePath);
                    SourceCode = string.IsNullOrEmpty(text) ? "// (File is empty)" : text;
                }
                else if (!string.IsNullOrWhiteSpace(FilePath))
                {
                    SourceCode = $"// File not found: {FilePath}";
                }
                else
                {
                    SourceCode = "// New file";
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

        public void SetFilePath(string newPath)
        {
            FilePath = newPath;
            this.RaisePropertyChanged(nameof(WindowTitle));
        }

        public void MarkSaved(string? newPath = null)
        {
            if (!string.IsNullOrWhiteSpace(newPath))
            {
                SetFilePath(newPath);
            }
            IsDirty = false;
        }

        public async Task SaveToAsync(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path)) return;
                IsBusy = true;
                await File.WriteAllTextAsync(path, SourceCode ?? string.Empty);
                SetFilePath(path);
                IsDirty = false;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Save failed: " + ex);
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
                if (string.IsNullOrWhiteSpace(FilePath)) return;
                IsBusy = true;
                await File.WriteAllTextAsync(FilePath, SourceCode ?? string.Empty);
                IsDirty = false;
                // Hook: trigger scene incremental compile here if applicable.
            }
            catch (Exception ex)
            {
                // Optionally expose a StatusMessage property.
                Console.WriteLine("Save failed: " + ex);
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
