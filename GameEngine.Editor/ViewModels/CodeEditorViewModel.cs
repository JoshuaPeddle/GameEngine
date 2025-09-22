using ReactiveUI;
using System;
using System.IO;
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
            set => this.RaiseAndSetIfChanged(ref _sourceCode, value);
        }

        public CodeEditorViewModel(string filePath)
        {
            _filePath = filePath;
        }

        public CodeEditorViewModel() : this("Program.cs") { }

        public async Task InitializeAsync()
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    var text = await File.ReadAllTextAsync(_filePath);
                    SourceCode = string.IsNullOrEmpty(text) ? "// (File is empty)" : text;
                }
                else
                {
                    SourceCode = $"// File not found: {_filePath}";
                }
            }
            catch (Exception ex)
            {
                SourceCode = $"// Error loading file: {ex.Message}";
            }
        }
    }
}