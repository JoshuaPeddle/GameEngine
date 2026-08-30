using GameEngine.Editor.Services;
using ReactiveUI;
using System.Collections.ObjectModel;
using System.IO;
using Unit = ReactiveUI.Primitives.RxVoid;
using System.Threading.Tasks;

namespace GameEngine.Editor.ViewModels
{
    public class ProjectEditorViewModel : ViewModelBase
    {
        public ReactiveCommand<Unit, Unit> OpenProjectCommand { get; }
        public ReactiveCommand<Unit, Unit> SaveProjectCommand { get; }
        public ReactiveCommand<Unit, Unit> OpenSelectedRecentProjectCommand { get; }
        public ReactiveCommand<Unit, Unit> NewProjectCommand { get; }

        private readonly IFilePickerService _filePickerService;
        private readonly AssetEditorViewModel _parentViewModel;
        private readonly IRecentProjectsService _recentProjectsService;

        public ObservableCollection<string> RecentProjects { get; } = [];

        public ProjectEditorViewModel(IFilePickerService filePickerService,
                                      AssetEditorViewModel parentViewModel,
                                      IRecentProjectsService? recentProjectsService = null)
        {
            _filePickerService = filePickerService;
            _parentViewModel = parentViewModel;
            _recentProjectsService = recentProjectsService ?? new RecentProjectsService();

            OpenProjectCommand = ReactiveCommand.CreateFromTask(OpenProject);
            SaveProjectCommand = ReactiveCommand.CreateFromTask(SaveProject);

            var canOpenRecent = this.WhenAnyValue(vm => vm.SelectedRecentProject,
                                                  p => !string.IsNullOrWhiteSpace(p));
            OpenSelectedRecentProjectCommand = ReactiveCommand.CreateFromTask(OpenSelectedRecentProject, canOpenRecent);

            var canCreate = this.WhenAnyValue(vm => vm.NewProjectName,
                                              name => GameProjectCreator.Validate(name) == null);
            NewProjectCommand = ReactiveCommand.CreateFromTask(NewProject, canCreate);

            LoadRecentProjects();
        }

        public ProjectEditorViewModel() : this(null!, null!)
        {
            ProjectFolderPath = "path/to/project.csproj";
        }

        private void LoadRecentProjects()
        {
            RecentProjects.Clear();
            foreach (var p in _recentProjectsService.Load())
                RecentProjects.Add(p);
        }

        private void AddRecent(string path)
        {
            _recentProjectsService.Add(path);
            LoadRecentProjects();
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

        private string _projectFolderPath;

        public string? ProjectName => Path.GetFileName(ProjectFolderPath)?.Replace(".csproj", "");

        private string? _selectedRecentProject;
        public string? SelectedRecentProject
        {
            get => _selectedRecentProject;
            set => this.RaiseAndSetIfChanged(ref _selectedRecentProject, value);
        }

        private string _newProjectName = "MyGame";
        public string NewProjectName
        {
            get => _newProjectName;
            set => this.RaiseAndSetIfChanged(ref _newProjectName, value);
        }

        private string _newProjectStatus = string.Empty;
        public string NewProjectStatus
        {
            get => _newProjectStatus;
            private set => this.RaiseAndSetIfChanged(ref _newProjectStatus, value);
        }

        // Creates a game from the dotnet new template and opens it, so the editor's starting
        // point is a project the user owns rather than one they had to find.
        public async Task NewProject()
        {
            if (_filePickerService == null) return;

            var folder = await _filePickerService.PromptForFolderPath();
            if (folder == null) return;

            NewProjectStatus = $"Creating {NewProjectName}...";
            var result = await GameProjectCreator.CreateAsync(folder, NewProjectName);
            NewProjectStatus = result.Message;

            if (!result.Success || result.ProjectPath == null) return;

            await LoadProjectInternal(result.ProjectPath);
            AddRecent(result.ProjectPath);
        }

        public async Task OpenProject()
        {
            if (_filePickerService == null) return;
            var filePath = await _filePickerService.PromptForProjectPath();
            if (filePath != null)
            {
                await LoadProjectInternal(filePath);
                AddRecent(filePath);
            }
        }

        private async Task OpenSelectedRecentProject()
        {
            if (string.IsNullOrWhiteSpace(SelectedRecentProject))
                return;

            if (!File.Exists(SelectedRecentProject))
            {
                // Remove stale entry
                RecentProjects.Remove(SelectedRecentProject);
                return;
            }

            await LoadProjectInternal(SelectedRecentProject);
            AddRecent(SelectedRecentProject);
        }

        private async Task LoadProjectInternal(string filePath)
        {
            ProjectFolderPath = filePath;
            var projectFolder = Path.GetDirectoryName(ProjectFolderPath);
            if (projectFolder == null) return;
            var assetCollection = await AssetFileLoader.LoadAssetCollectionAsync(projectFolder);
            _parentViewModel.LoadAssetCollection(assetCollection);
        }

        public async Task SaveProject()
        {
            var assetCollection = new AssetCollection([.. _parentViewModel.SharedTextures], [.. _parentViewModel.SharedAnimations], [.. _parentViewModel.SharedSounds]);
            var projectFolder = Path.GetDirectoryName(ProjectFolderPath);
            if (projectFolder != null)
                await AssetFileWriter.WriteAssetFilesAsync(projectFolder, assetCollection);

            ProjectFolderPath = "";
            _parentViewModel.SharedTextures.Clear();
        }
    }
}
