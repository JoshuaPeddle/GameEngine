using System;
using System.Collections.Generic;
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

        private readonly ObservableCollection<CompileDiagnostic> _diagnostics = new();
        public ObservableCollection<CompileDiagnostic> Diagnostics => _diagnostics;

        private bool _hasDiagnostics;
        public bool HasDiagnostics
        {
            get => _hasDiagnostics;
            private set => this.RaiseAndSetIfChanged(ref _hasDiagnostics, value);
        }

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

        private void ShowDiagnostics(IReadOnlyList<CompileDiagnostic> diagnostics)
        {
            _diagnostics.Clear();
            foreach (var diagnostic in diagnostics.OrderByDescending(d => d.IsError))
                _diagnostics.Add(diagnostic);

            HasDiagnostics = _diagnostics.Count > 0;
        }

        private string NoScenesMessage()
        {
            var failures = _sceneService?.WorkspaceFailures ?? [];
            return failures.Count == 0
                ? "No scenes found."
                : $"No scenes found. The project did not load cleanly: {failures[0]}";
        }

        private string WarningSuffix()
        {
            var warnings = _diagnostics.Count(d => !d.IsError);
            return warnings == 0 ? string.Empty : $", {warnings} warning{(warnings == 1 ? "" : "s")}";
        }

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

                ShowDiagnostics(_sceneService.LastDiagnostics);

                _scenes.Clear();
                foreach (var t in compile.SceneTypes.OrderBy(t => t.Name))
                    _scenes.Add(t.Name);

                _scenesLoaded = true;
                _status.Message = _scenes.Count == 0
                    ? NoScenesMessage()
                    : $"Loaded {_scenes.Count} scenes{WarningSuffix()}.";

                if (SelectedScene == null && _scenes.Count > 0)
                    SelectedScene = _scenes[0];
            }
            catch (SceneCompilationException compileFailure)
            {
                ShowDiagnostics(compileFailure.Diagnostics);
                _scenes.Clear();

                var errors = _diagnostics.Count(d => d.IsError);
                _status.Message = errors == 1 ? "1 compile error." : $"{errors} compile errors.";
            }
            catch (Exception ex)
            {
                ShowDiagnostics([]);
                _status.Message = $"Could not load scenes: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
