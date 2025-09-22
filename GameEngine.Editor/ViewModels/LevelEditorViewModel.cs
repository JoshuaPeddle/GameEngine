using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using GameEngine.Core;
using GameEngine.Editor.Magic;
using ReactiveUI;

namespace GameEngine.Editor.ViewModels
{
    public class LevelEditorViewModel : ViewModelBase
    {
        public AssetEditorViewModel AssetEditorViewModel;

        public LevelEditorViewModel(AssetEditorViewModel assetEditorViewModel)
        {
            AssetEditorViewModel = assetEditorViewModel;
            ReloadScenesCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                await LoadScenesAsync(forceReload: true);
            }, this.WhenAnyValue(v => v.IsBusy, busy => !busy));

            EditSceneCommand = ReactiveCommand.Create(() =>
            {
                if (_sceneService == null) return;
                if (SelectedScene == null) return;

                var scenes = _sceneService.GetScenes();
                var scene = scenes.FirstOrDefault(s => s.Name == SelectedScene);
                if (scene == null || string.IsNullOrWhiteSpace(scene.FilePath)) return;
                if (!System.IO.File.Exists(scene.FilePath)) return;

                var window = new CodeEditor(scene.FilePath);
                window.Show();
            });
        }

        public LevelEditorViewModel() : this(new AssetEditorViewModel()) { }

        private readonly ObservableCollection<string> _scenes = new();
        public ObservableCollection<string> Scenes => _scenes;

        private string? _selectedScene;
        public string? SelectedScene
        {
            get => _selectedScene;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedScene, value);
                if (value != null)
                {
                    if (_lastCompileResult != null)
                    {
                        var t = _lastCompileResult.SceneTypes.FirstOrDefault(t => t.Name == value);
                        if (t != null)
                            SceneSelected?.Invoke(t);
                    }
                }
            }
        }

        // NEW: Engine running flag
        private bool _isEngineRunning = true;
        public bool IsEngineRunning
        {
            get => _isEngineRunning;
            set => this.RaiseAndSetIfChanged(ref _isEngineRunning, value);
        }

        private string? ProjectPath =>
            string.IsNullOrWhiteSpace(AssetEditorViewModel?.ProjectEditor?.ProjectFolderPath)
                ? null
                : AssetEditorViewModel.ProjectEditor.ProjectFolderPath;

        private SceneSelectionService? _sceneService;
        private SceneCompilationResult? _lastCompileResult;
        private bool _scenesLoaded;

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            private set => this.RaiseAndSetIfChanged(ref _isBusy, value);
        }

        private string _statusMessage = "Idle";
        public string StatusMessage
        {
            get => _statusMessage;
            private set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
        }

        public ReactiveCommand<Unit, Unit> ReloadScenesCommand { get; }
        public ReactiveCommand<Unit, Unit> EditSceneCommand { get; }


        public event Action<Type>? SceneSelected;

        public async Task EnsureScenesLoadedAsync()
        {
            if (_scenesLoaded) return;
            await LoadScenesAsync(forceReload: false);
        }

        private async Task LoadScenesAsync(bool forceReload)
        {
            if (IsBusy) return;
            if (ProjectPath == null) return;

            try
            {
                IsBusy = true;
                StatusMessage = "Loading scenes...";
                if (_sceneService == null || forceReload)
                {
                    if (_sceneService != null)
                        await _sceneService.DisposeAsync();

                    _sceneService = new SceneSelectionService(ProjectPath);
                    await _sceneService.InitializeAsync();
                }
                else
                {
                    await _sceneService.RefreshAsync();
                }

                var compile = await _sceneService.CompileAllAsync();
                _lastCompileResult = compile;

                _scenes.Clear();
                foreach (var t in compile.SceneTypes.OrderBy(t => t.Name))
                    _scenes.Add(t.Name);

                _scenesLoaded = true;
                StatusMessage = _scenes.Count == 0 ? "No scenes found." : $"Loaded {_scenes.Count} scenes.";

                // Auto-select first if none selected
                if (SelectedScene == null && _scenes.Count > 0)
                    SelectedScene = _scenes[0];
            }
            catch (Exception)
            {
                StatusMessage = "Error loading scenes";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private double _zoomLevel = 1.0;
        public double ZoomLevel
        {
            get => _zoomLevel;
            set => this.RaiseAndSetIfChanged(ref _zoomLevel, value);
        }
        public ReactiveCommand<Entity, Unit> EntitySelectedCommand { get; }
    }
}
