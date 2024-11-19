using Avalonia.Controls;
using Avalonia.Platform.Storage;
using System.Threading.Tasks;

namespace GameEngine.Editor.Services
{
    public class FilePickerService : IFilePickerService
    {
        private readonly Window _window;

        public FilePickerService(Window window)
        {
            _window = window;
        }

        public async Task<string?> OpenFileAsync()
        {
            var options = new FilePickerOpenOptions
            {
                FileTypeFilter =
                [
                    new FilePickerFileType("Images")
                    {
                        Patterns = ["*.png", "*.jpg", "*.jpeg", "*.bmp"]
                    }
                ]
            };
            var result = await _window.StorageProvider.OpenFilePickerAsync(options);

            if (result != null && result.Count > 0)
            {
                return result[0].Path.LocalPath;
            }

            return null;
        }
    }
}
