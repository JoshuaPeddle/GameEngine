using AvaloniaEdit.Utils;
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
            var recent = new RecentProjectsService();
            ProjectEditor = new ProjectEditorViewModel(filePickerService, this, recent);
            TextureEditor = new TextureEditorViewModel(filePickerService, this);
            AnimationEditor = new AnimationEditorViewModel(this);
            LevelEditor = new LevelEditorViewModel(this);
        }

        public AssetEditorViewModel()
        {
            var recent = new RecentProjectsService();
            ProjectEditor = new ProjectEditorViewModel();
            TextureEditor = new TextureEditorViewModel();
            AnimationEditor = new AnimationEditorViewModel();
            LevelEditor = new LevelEditorViewModel(this);
        }
        
        public void LoadAssetCollection(AssetCollection assetColleciton)
        {
            SharedTextures.Clear();
            SharedTextures.AddRange(assetColleciton.Textures);
            SharedAnimations.Clear();
            SharedAnimations.AddRange(assetColleciton.Animations);
            SharedSounds.Clear();
            SharedSounds.AddRange(assetColleciton.Sounds);
        }

        public ObservableCollection<Texture> SharedTextures { get; } = [];
        public ObservableCollection<Animation> SharedAnimations { get; } = [];
        public ObservableCollection<Sound> SharedSounds { get; } = [];
    }
}
