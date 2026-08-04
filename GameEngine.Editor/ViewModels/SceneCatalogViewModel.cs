using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using GameEngine.Editor.Controls;
using GameEngine.Editor.Magic;
using ReactiveUI;
using Unit = ReactiveUI.Primitives.RxVoid;

namespace GameEngine.Editor.ViewModels
{
    public class SceneCatalogViewModel : ViewModelBase
    {
        private readonly Func<string?> _projectPath;
        private readonly EditorStatus _status;
        private readonly Action _clearEntitySelection;

        private SceneSelectionService? _sceneService;
        private SceneCompilationResult? _lastCompileResult;
        private bool _scenesLoaded;

        public SceneCatalogViewModel(
            Func<string?> projectPath,
            EditorStatus status,
            Action clearEntitySelection)
        {
            _projectPath = projectPath;
            _status = status;
            _clearEntitySelection = clearEntitySelection;

            ReloadScenesCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                await LoadScenesAsync(forceReload: true);
            }, this.WhenAnyValue(v => v.IsBusy).Select(busy => !busy));

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

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            private set => this.RaiseAndSetIfChanged(ref _isBusy, value);
        }

        public ReactiveCommand<Unit, Unit> ReloadScenesCommand { get; }
        public ReactiveCommand<Unit, Unit> EditSceneCommand { get; }

        public event Action<Type>? SceneSelected;
        public event Action? ScenesReloading;

        public async Task EnsureScenesLoadedAsync()
        {
            if (_scenesLoaded) return;
            await LoadScenesAsync(forceReload: false);
        }

        private async Task LoadScenesAsync(bool forceReload)
        {
            if (IsBusy) return;
            var projectPath = _projectPath();
            if (projectPath == null) return;

            try
            {
                IsBusy = true;
                _status.Message = "Loading scenes...";
                if (_sceneService == null || forceReload)
                {
                    if (_sceneService != null)
                    {
                        // Runtime scene Types and the preview engine both keep the collectible
                        // load context alive. Release them before asking the compiler to unload.
                        _lastCompileResult = null;
                        _clearEntitySelection();
                        ScenesReloading?.Invoke();
                        await _sceneService.DisposeAsync();
                    }

                    _sceneService = new SceneSelectionService(projectPath);
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
                _status.Message = _scenes.Count == 0 ? "No scenes found." : $"Loaded {_scenes.Count} scenes.";

                if (SelectedScene == null && _scenes.Count > 0)
                    SelectedScene = _scenes[0];
            }
            catch (Exception)
            {
                _status.Message = "Error loading scenes";
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
