using DynamicData;
using GameEngine.Editor.Services;
using ReactiveUI;
using System.Collections.ObjectModel;
using System.IO;
using System.Reactive;
using System.Threading.Tasks;

namespace GameEngine.Editor.ViewModels
{
    public class ProjectEditorViewModel : ViewModelBase
    {
        public ReactiveCommand<Unit, Unit> OpenProjectCommand { get; }
        public ReactiveCommand<Unit, Unit> SaveProjectCommand { get; }
        public ReactiveCommand<Unit, Unit> OpenSelectedRecentProjectCommand { get; }

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
