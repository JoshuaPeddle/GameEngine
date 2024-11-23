using DynamicData;
using GameEngine.Editor.Services;
using ReactiveUI;
using System.IO;
using System.Reactive;
using System.Threading.Tasks;

namespace GameEngine.Editor.ViewModels
{
    public class ProjectEditorViewModel : ViewModelBase
    {
        public ReactiveCommand<Unit, Unit> OpenProjectCommand { get; }
        public ReactiveCommand<Unit, Unit> SaveProjectCommand { get; }

        private readonly IFilePickerService _filePickerService;
        private readonly AssetEditorViewModel _parentViewModel;

        public ProjectEditorViewModel(IFilePickerService filePickerService, AssetEditorViewModel parentViewModel)
        {
            _filePickerService = filePickerService;
            _parentViewModel = parentViewModel;
            OpenProjectCommand = ReactiveCommand.CreateFromTask(OpenProject);
            SaveProjectCommand = ReactiveCommand.CreateFromTask(SaveProject);
        }
        public ProjectEditorViewModel() : this(null!, null!) // Designer constructor
        {
            ProjectFolderPath = "path/to/project.csproj";
        }

        public string ProjectFolderPath
        {
            get => _projectFolderPath;
            set
            {
                this.RaiseAndSetIfChanged(ref _projectFolderPath, value);
                this.RaisePropertyChanged(nameof(ProjectName));
            }

        }

        private string _projectFolderPath; // Path to the folder containing the project file.
                                           // This is technically a csproj file. We dont really need to interact with it directly.
                                           // Instead, folders and files will be placed relative to this path.
                                           // Things such as assets like textures, and sounds will be placed in the assets folder.
                                           // An assets.txt file will be placed in the root of the project folder.
        public string? ProjectName
        {
            get
            {
                return Path.GetFileName(ProjectFolderPath)?.Replace(".csproj", "");
            }
        }


        public async Task OpenProject()
        {
            var filePath = await _filePickerService.PromptForProjectPath();
            if (filePath != null)
            {
                ProjectFolderPath = filePath;
                var projectFolder = Path.GetDirectoryName(ProjectFolderPath);
                var assetCollection = AssetFileLoader.LoadAssetCollection(projectFolder);
                _parentViewModel.SharedTextures.Clear();
                _parentViewModel.SharedTextures.AddRange(assetCollection.Textures);
                _parentViewModel.SharedAnimations.Clear();
                _parentViewModel.SharedAnimations.AddRange(assetCollection.Animations);
            }
        }

        public async Task SaveProject()
        {
            var assetCollection = new AssetCollection([.. _parentViewModel.SharedTextures], [.. _parentViewModel.SharedAnimations]);
            var projectFolder = Path.GetDirectoryName(ProjectFolderPath);
            await AssetFileWriter.WriteAssetFilesAsync(projectFolder, assetCollection);

            ProjectFolderPath = "";
            _parentViewModel.SharedTextures.Clear();
        }
    }
}
