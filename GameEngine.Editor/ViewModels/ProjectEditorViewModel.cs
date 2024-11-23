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

        private readonly IFilePickerService _filePickerService;
        private readonly AssetEditorViewModel _parentViewModel;

        public ProjectEditorViewModel(IFilePickerService filePickerService, AssetEditorViewModel parentViewModel)
        {
            _filePickerService = filePickerService;
            _parentViewModel = parentViewModel;
            OpenProjectCommand = ReactiveCommand.CreateFromTask(OpenProject);
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

        public void CreateProject()
        {
            // Create the project folder
            // Create the assets folder
            // Create the assets.txt file
        }
        public void DeleteProject()
        {
            // Delete the project folder
        }
        public void UpdateProject()
        {
            // Update the project folder
            // Update the assets folder
            // Update the assets.txt file
        }

        public async Task OpenProject()
        {
            var filePath = await _filePickerService.PromptForProjectPath();
            if (filePath != null)
            {
                ProjectFolderPath = filePath;
            }
        }
    }
}
