using GameEngine.Editor.Models;
using GameEngine.Editor.Services;
using System.Collections.ObjectModel;

namespace GameEngine.Editor.ViewModels
{
    public class AssetEditorViewModel : ViewModelBase
    {
        public ProjectEditorViewModel ProjectEditor { get; }
        public TextureEditorViewModel TextureEditor { get; }
        public AnimationEditorViewModel AnimationEditor { get; }
        public LevelEditorViewModel LevelEditor { get; }

        public AssetEditorViewModel(IFilePickerService filePickerService)
        {
            ProjectEditor = new ProjectEditorViewModel(filePickerService, this);
            TextureEditor = new TextureEditorViewModel(filePickerService, this);
            AnimationEditor = new AnimationEditorViewModel(this);
            LevelEditor = new LevelEditorViewModel();
        }

        public AssetEditorViewModel()
        {
            ProjectEditor = new ProjectEditorViewModel();
            TextureEditor = new TextureEditorViewModel();
            AnimationEditor = new AnimationEditorViewModel();
            LevelEditor = new LevelEditorViewModel();
        }

        public ObservableCollection<Texture> SharedTextures { get; } = [];
        public ObservableCollection<Animation> SharedAnimations { get; } = [];
    }
}
