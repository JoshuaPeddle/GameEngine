using Avalonia.Controls;
using Avalonia.Platform.Storage;
using System.IO;
using System.Threading.Tasks;

namespace GameEngine.Editor.Services
{
    public class FilePickerService : IFilePickerService
    {
        private readonly UserControl? _userControl;
        private readonly Window? _window;

        public FilePickerService(UserControl userControl)
        {
            _userControl = userControl;
        }
        public FilePickerService(Window window)
        {
            _window = window;
        }
        public FilePickerService() { }

        public async Task<string?> PromptForImagePath()
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
            var topLevel = _userControl is null ? _window : TopLevel.GetTopLevel(_userControl);
            if (topLevel == null)
                return null;
            var result = await topLevel.StorageProvider.OpenFilePickerAsync(options);

            if (result != null && result.Count > 0)
            {
                return result[0].Path.LocalPath;
            }

            return null;
        }

        public async Task<string?> PromptForFolderPath()
        {
            var topLevel = _userControl is null ? _window : TopLevel.GetTopLevel(_userControl);
            if (topLevel == null)
                return null;

            var result = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Where should the new game go?",
                AllowMultiple = false,
                SuggestedStartLocation = await topLevel.StorageProvider.TryGetFolderFromPathAsync(Directory.GetCurrentDirectory())
            });

            return result.Count > 0 ? result[0].Path.LocalPath : null;
        }

        public async Task<string?> PromptForProjectPath()
        {
            var topLevel = _userControl is null ? _window : TopLevel.GetTopLevel(_userControl);
            if (topLevel == null)
                return null;


            var options = new FilePickerOpenOptions
            {
                FileTypeFilter =
                [
                    new FilePickerFileType("Project File")
                    {
                        Patterns = ["*.csproj"]
                    }
                ],
                SuggestedStartLocation = await topLevel.StorageProvider.TryGetFolderFromPathAsync(Directory.GetCurrentDirectory())
            };

            var result = await topLevel.StorageProvider.OpenFilePickerAsync(options);

            if (result != null && result.Count > 0)
                return result[0].Path.LocalPath;

            return null;
        }


    }
}
