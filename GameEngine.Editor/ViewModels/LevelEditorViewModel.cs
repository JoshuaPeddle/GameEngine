using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using GameEngine.Core;
using GameEngine.Editor.Magic;
using ReactiveUI;
using System.Collections.Generic;
using System.IO;
using GameEngine.Core.Utils;
using System.Text.Json;
using System.Reactive.Linq;

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

            EntitySelectedCommand = ReactiveCommand.Create<Entity>(entity =>
            {
                SetSelectedEntity(entity);
            });

            // --- Level file editor commands ---
            LoadLevelFileCommand = ReactiveCommand.CreateFromTask(LoadLevelFileAsync);
            SaveLevelFileCommand = ReactiveCommand.Create((Action)SaveLevelFile);
            AddEntityCommand = ReactiveCommand.Create((Action)AddEntity);

            var canModifyEntity = this.WhenAnyValue(vm => vm.SelectedLevelEntity).Select(e => e != null);
            var canModifyComponent = this.WhenAnyValue(vm => vm.SelectedLevelComponent).Select(c => c != null);

            RemoveEntityCommand = ReactiveCommand.Create((Action)RemoveEntity, canModifyEntity);
            AddComponentCommand = ReactiveCommand.Create((Action)AddComponent, canModifyEntity);
            RemoveComponentCommand = ReactiveCommand.Create((Action)RemoveComponent, canModifyComponent);
        }

        public LevelEditorViewModel()
        { 
            _scenes.Add("No Project Loaded");

            var levelBuilder = new LevelBuilder();
            levelBuilder.AddEntity("entity1", AddEntity =>
            {
                AddEntity.AddTransform(0, 0);
                AddEntity.AddAnimation("Ball");
                AddEntity.AddBoundingBox(32, 32);
            });
            
            var level = levelBuilder.Build();
            foreach (var e in level.Entities)
            {
                var evm = new LevelEntityViewModel { Tag = e.Tag };
                foreach (var c in e.Components)
                {
                    evm.Components.Add(new LevelComponentViewModel
                    {
                        RawJson = c.Data.GetRawText(),
                        Type = c.Type
                    });
                }
                LevelEntities.Add(evm);
            }
            SelectedLevelEntity = LevelEntities.FirstOrDefault();

            Assets._fileFetcher =
                path => path.Contains("assets.txt")
                    ? File.Open(Path.Combine("GameEngine.Demo", path), FileMode.Open)
                    : File.Open(Path.Combine("GameEngine.Demo", "assets", path), FileMode.Open);

            SelectedEntity = EntityData.ToEntity(level.Entities.FirstOrDefault(), new EntityManager(), new Core.Components.ComponentFactory(new Assets("assets.txt")));
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

        // Engine running flag
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

        private Entity? _selectedEntity;
        public Entity? SelectedEntity
        {
            get => _selectedEntity;
            private set => this.RaiseAndSetIfChanged(ref _selectedEntity, value);
        }

        public bool HasEntitySelection => SelectedEntity != null;
        public int? SelectedEntityId => SelectedEntity?.Id;
        public string? SelectedEntityTag => SelectedEntity?.Tag;
        public bool? SelectedEntityActive => SelectedEntity?.Active;

        public IReadOnlyList<string>? SelectedEntityComponents =>
            SelectedEntity == null
                ? null
                : SelectedEntity.Components.Keys
                    .Select(t => t.Name)
                    .OrderBy(n => n)
                    .ToList();

        private void SetSelectedEntity(Entity? entity)
        {
            SelectedEntity = entity;
            this.RaisePropertyChanged(nameof(HasEntitySelection));
            this.RaisePropertyChanged(nameof(SelectedEntityId));
            this.RaisePropertyChanged(nameof(SelectedEntityTag));
            this.RaisePropertyChanged(nameof(SelectedEntityActive));
            this.RaisePropertyChanged(nameof(SelectedEntityComponents));
        }

        // ================= LEVEL FILE EDITOR =================
        private LevelFile? _levelFile;
        public string? LevelFilePath
        {
            get => _levelFilePath;
            private set => this.RaiseAndSetIfChanged(ref _levelFilePath, value);
        }
        private string? _levelFilePath;

        public ObservableCollection<LevelEntityViewModel> LevelEntities { get; } = new();

        private LevelEntityViewModel? _selectedLevelEntity;
        public LevelEntityViewModel? SelectedLevelEntity
        {
            get => _selectedLevelEntity;
            set => this.RaiseAndSetIfChanged(ref _selectedLevelEntity, value);
        }

        private LevelComponentViewModel? _selectedLevelComponent;
        public LevelComponentViewModel? SelectedLevelComponent
        {
            get => _selectedLevelComponent;
            set => this.RaiseAndSetIfChanged(ref _selectedLevelComponent, value);
        }

        public ReactiveCommand<Unit, Unit> LoadLevelFileCommand { get; }
        public ReactiveCommand<Unit, Unit> SaveLevelFileCommand { get; }
        public ReactiveCommand<Unit, Unit> AddEntityCommand { get; }
        public ReactiveCommand<Unit, Unit> RemoveEntityCommand { get; }
        public ReactiveCommand<Unit, Unit> AddComponentCommand { get; }
        public ReactiveCommand<Unit, Unit> RemoveComponentCommand { get; }

        private async Task LoadLevelFileAsync()
        {
            if (ProjectPath == null) return;
            try
            {
                var projectDir = Path.GetDirectoryName(ProjectPath);
                if (projectDir == null) return;
                var levelsDir = Path.Combine(projectDir, "levels");
                if (!Directory.Exists(levelsDir)) return;
                var levelFile = Directory.GetFiles(levelsDir, "*.json").FirstOrDefault();
                if (levelFile == null) return;

                var lf = LevelFile.LoadFromFile(levelFile);
                _levelFile = lf;
                LevelFilePath = levelFile;
                LevelEntities.Clear();
                foreach (var e in lf.Entities)
                {
                    var evm = new LevelEntityViewModel { Tag = e.Tag };
                    foreach (var c in e.Components)
                    {
                        evm.Components.Add(new LevelComponentViewModel
                        {
                            RawJson = c.Data.GetRawText(),
                            Type = c.Type
                        });
                    }
                    LevelEntities.Add(evm);
                }
                SelectedLevelEntity = LevelEntities.FirstOrDefault();
                StatusMessage = "Level loaded.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to load level: {ex.Message}";
            }
            await Task.CompletedTask;
        }

        private void SaveLevelFile()
        {
            if (_levelFile == null || LevelFilePath == null) return;
            try
            {
                _levelFile.Entities.Clear();
                foreach (var evm in LevelEntities)
                {
                    var entityData = new EntityData { Tag = evm.Tag };
                    foreach (var cvm in evm.Components)
                    {
                        try
                        {
                            using var doc = JsonDocument.Parse(cvm.RawJson);
                            var root = doc.RootElement;
                            string? type = cvm.Type;
                            if (root.TryGetProperty("type", out var typeProp))
                            {
                                type = typeProp.GetString();
                            }
                            if (string.IsNullOrWhiteSpace(type))
                                continue;

                            entityData.Components.Add(new ComponentData
                            {
                                Type = type!,
                                Data = root.Clone()
                            });
                        }
                        catch
                        {
                            // ignore malformed component json
                        }
                    }
                    _levelFile.Entities.Add(entityData);
                }
                _levelFile.SaveToFile(LevelFilePath);
                StatusMessage = "Level saved.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to save level: {ex.Message}";
            }
        }

        private void AddEntity()
        {
            var baseTag = "entity";
            int i = 1;
            while (LevelEntities.Any(e => e.Tag == baseTag + i)) i++;
            var newEntity = new LevelEntityViewModel { Tag = baseTag + i };
            LevelEntities.Add(newEntity);
            SelectedLevelEntity = newEntity;
        }

        private void RemoveEntity()
        {
            if (SelectedLevelEntity == null) return;
            var idx = LevelEntities.IndexOf(SelectedLevelEntity);
            LevelEntities.Remove(SelectedLevelEntity);
            SelectedLevelEntity = LevelEntities.Count > 0 ? LevelEntities[Math.Clamp(idx - 1, 0, LevelEntities.Count - 1)] : null;
        }

        private void AddComponent()
        {
            if (SelectedLevelEntity == null) return;
            var comp = new LevelComponentViewModel
            {
                Type = "CTransform",
                RawJson = "{\n  \"type\": \"CTransform\"\n}"
            };
            SelectedLevelEntity.Components.Add(comp);
            SelectedLevelComponent = comp;
        }

        private void RemoveComponent()
        {
            if (SelectedLevelEntity == null || SelectedLevelComponent == null) return;
            var list = SelectedLevelEntity.Components;
            var idx = list.IndexOf(SelectedLevelComponent);
            list.Remove(SelectedLevelComponent);
            SelectedLevelComponent = list.Count > 0 ? list[Math.Clamp(idx - 1, 0, list.Count - 1)] : null;
        }
    }

    // Helper view models for level editing
    public class LevelEntityViewModel : ReactiveObject
    {
        private string _tag = string.Empty;
        public string Tag
        {
            get => _tag;
            set => this.RaiseAndSetIfChanged(ref _tag, value);
        }

        public ObservableCollection<LevelComponentViewModel> Components { get; } = new();
    }

    public class LevelComponentViewModel : ReactiveObject
    {
        private string _type = string.Empty;
        public string Type
        {
            get => _type;
            set => this.RaiseAndSetIfChanged(ref _type, value);
        }

        private string _rawJson = string.Empty;
        public string RawJson
        {
            get => _rawJson;
            set => this.RaiseAndSetIfChanged(ref _rawJson, value);
        }
    }
}
