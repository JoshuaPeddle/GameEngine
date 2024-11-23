using GameEngine.Editor.Models;
using GameEngine.Editor.Services;
using System.Collections.ObjectModel;

namespace GameEngine.Editor.ViewModels
{
    public class AssetEditorViewModel : ViewModelBase
    {
        public ProjectEditorViewModel ProjectEditor { get; }
        public TextureEditorViewModel TextureEditor { get; }

        public AssetEditorViewModel(IFilePickerService filePickerService)
        {
            ProjectEditor = new ProjectEditorViewModel(filePickerService, this);
            TextureEditor = new TextureEditorViewModel(filePickerService, this);
        }

        public AssetEditorViewModel()
        {
            ProjectEditor = new ProjectEditorViewModel();
            TextureEditor = new TextureEditorViewModel();
        }

        public ObservableCollection<Texture> SharedTextures { get; } = [];
    }
}
