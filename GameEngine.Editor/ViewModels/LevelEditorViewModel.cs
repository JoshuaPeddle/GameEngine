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
using System.Text;

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

            var designEntities = new EntityManager();
            new LevelLoader(new Core.Components.ComponentFactory(new Assets("assets.txt")))
                .LoadLevel(level, designEntities);
            designEntities.Update();
            SelectedEntity = designEntities.GetEntities().FirstOrDefault();
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
        public event Action? ScenesReloading;

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
                    {
                        // Runtime scene Types and the preview engine both keep the collectible
                        // load context alive. Release them before asking the compiler to unload.
                        _lastCompileResult = null;
                        SetSelectedEntity(null);
                        ScenesReloading?.Invoke();
                        await _sceneService.DisposeAsync();
                    }

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

        // New: list of available level files and current selection
        private readonly ObservableCollection<string> _levelFileOptions = new();
        public ObservableCollection<string> LevelFileOptions => _levelFileOptions;

        private string? _selectedLevelFile;
        public string? SelectedLevelFile
        {
            get => _selectedLevelFile;
            set => this.RaiseAndSetIfChanged(ref _selectedLevelFile, value);
        }

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

        // List of supported component types for the editor
        public static readonly string[] ComponentTypes = new[]
        {
            "CTransform", "CAnimation", "CBoundingBox", "CInput", "CMovement"
        };

        // Populate/refresh the list of level files from the project
        // Preserve current selection before clearing the collection (ComboBox will push null on clear)
        public void RefreshLevelFileOptions()
        {
            var previousSelection = _selectedLevelFile;

            _levelFileOptions.Clear();

            if (ProjectPath == null)
            {
                SelectedLevelFile = null;
                return;
            }

            var projectDir = Path.GetDirectoryName(ProjectPath);
            if (projectDir == null)
            {
                SelectedLevelFile = null;
                return;
            }

            var levelsDir = Path.Combine(projectDir, "levels");
            if (!Directory.Exists(levelsDir))
            {
                SelectedLevelFile = null;
                return;
            }

            foreach (var file in Directory.GetFiles(levelsDir, "*.json").OrderBy(Path.GetFileName))
                _levelFileOptions.Add(file);

            // Restore previous selection if it still exists; otherwise, only default if nothing is selected
            if (!string.IsNullOrWhiteSpace(previousSelection) && _levelFileOptions.Contains(previousSelection))
            {
                SelectedLevelFile = previousSelection;
            }
            else if (SelectedLevelFile == null && _levelFileOptions.Count > 0)
            {
                SelectedLevelFile = _levelFileOptions[0];
            }
        }

        private async Task LoadLevelFileAsync()
        {
            if (ProjectPath == null) return;
            try
            {
                var selectedAtClick = SelectedLevelFile;
                RefreshLevelFileOptions();
                var levelFile = (!string.IsNullOrWhiteSpace(selectedAtClick) && File.Exists(selectedAtClick))
                    ? selectedAtClick
                    : SelectedLevelFile;
                if (string.IsNullOrWhiteSpace(levelFile) || !File.Exists(levelFile))
                {
                    StatusMessage = "Select a level file to load.";
                    return;
                }

                var lf = LevelFile.LoadFromFile(levelFile);
                _levelFile = lf;
                LevelFilePath = levelFile;

                LevelEntities.Clear();
                foreach (var e in lf.Entities)
                {
                    var evm = new LevelEntityViewModel { Tag = e.Tag };
                    foreach (var c in e.Components)
                    {
                        var cvm = new LevelComponentViewModel
                        {
                            Type = c.Type
                        };
                        cvm.RawJson = c.Data.GetRawText();
                        evm.Components.Add(cvm);
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
            // Default to a CTransform with sensible defaults
            var comp = new LevelComponentViewModel();
            comp.ApplyTypeWithDefaults("CTransform");
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
        private bool _suppressSync;

        private string _type = string.Empty;
        public string Type
        {
            get => _type;
            set
            {
                var changed = _type != value;
                this.RaiseAndSetIfChanged(ref _type, value);
                if (changed)
                {
                    RaiseTypeFlags();
                    if (!_suppressSync)
                    {
                        ApplyTypeWithDefaults(_type);
                    }
                }
            }
        }

        private string _rawJson = string.Empty;
        public string RawJson
        {
            get => _rawJson;
            set
            {
                this.RaiseAndSetIfChanged(ref _rawJson, value);
                if (!_suppressSync)
                {
                    TryParseFromRawJson();
                }
            }
        }

        // Typed properties for known components
        private double _posX;
        public double PosX { get => _posX; set { this.RaiseAndSetIfChanged(ref _posX, value); UpdateRawJson(); } }
        private double _posY;
        public double PosY { get => _posY; set { this.RaiseAndSetIfChanged(ref _posY, value); UpdateRawJson(); } }

        private double _velX;
        public double VelX { get => _velX; set { this.RaiseAndSetIfChanged(ref _velX, value); UpdateRawJson(); } }
        private double _velY;
        public double VelY { get => _velY; set { this.RaiseAndSetIfChanged(ref _velY, value); UpdateRawJson(); } }

        private double _scaleX = 1;
        public double ScaleX { get => _scaleX; set { this.RaiseAndSetIfChanged(ref _scaleX, value); UpdateRawJson(); } }
        private double _scaleY = 1;
        public double ScaleY { get => _scaleY; set { this.RaiseAndSetIfChanged(ref _scaleY, value); UpdateRawJson(); } }

        private double _rotation;
        public double Rotation { get => _rotation; set { this.RaiseAndSetIfChanged(ref _rotation, value); UpdateRawJson(); } }

        private string _animationName = string.Empty;
        public string AnimationName { get => _animationName; set { this.RaiseAndSetIfChanged(ref _animationName, value); UpdateRawJson(); } }

        private double _width;
        public double Width { get => _width; set { this.RaiseAndSetIfChanged(ref _width, value); UpdateRawJson(); } }
        private double _height;
        public double Height { get => _height; set { this.RaiseAndSetIfChanged(ref _height, value); UpdateRawJson(); } }
        private bool _blockVision = false;
        public bool BlockVision { get => _blockVision; set { this.RaiseAndSetIfChanged(ref _blockVision, value); UpdateRawJson(); } }
        private bool _blockMovement = true;
        public bool BlockMovement { get => _blockMovement; set { this.RaiseAndSetIfChanged(ref _blockMovement, value); UpdateRawJson(); } }

        private double _speed;
        public double Speed { get => _speed; set { this.RaiseAndSetIfChanged(ref _speed, value); UpdateRawJson(); } }
        private double _maxSpeed;
        public double MaxSpeed { get => _maxSpeed; set { this.RaiseAndSetIfChanged(ref _maxSpeed, value); UpdateRawJson(); } }

        // Convenience flags for XAML
        public bool IsTransform => Type == "CTransform";
        public bool IsAnimation => Type == "CAnimation";
        public bool IsBoundingBox => Type == "CBoundingBox";
        public bool IsInput => Type == "CInput";
        public bool IsMovement => Type == "CMovement";

        public void ApplyTypeWithDefaults(string type)
        {
            _suppressSync = true;
            try
            {
                // set backing field directly to avoid recursion
                _type = type;
                this.RaisePropertyChanged(nameof(Type));
                RaiseTypeFlags();
                switch (type)
                {
                    case "CTransform":
                        _posX = 0; _posY = 0; _velX = 0; _velY = 0; _scaleX = 1; _scaleY = 1; _rotation = 0;
                        break;
                    case "CAnimation":
                        _animationName = "Ball";
                        break;
                    case "CBoundingBox":
                        _width = 32; _height = 32; _blockVision = false; _blockMovement = true;
                        break;
                    case "CInput":
                        break;
                    case "CMovement":
                        _speed = 100; _maxSpeed = 120;
                        break;
                }
                // Raise property changes for edited fields
                this.RaisePropertyChanged(nameof(PosX));
                this.RaisePropertyChanged(nameof(PosY));
                this.RaisePropertyChanged(nameof(VelX));
                this.RaisePropertyChanged(nameof(VelY));
                this.RaisePropertyChanged(nameof(ScaleX));
                this.RaisePropertyChanged(nameof(ScaleY));
                this.RaisePropertyChanged(nameof(Rotation));
                this.RaisePropertyChanged(nameof(AnimationName));
                this.RaisePropertyChanged(nameof(Width));
                this.RaisePropertyChanged(nameof(Height));
                this.RaisePropertyChanged(nameof(BlockVision));
                this.RaisePropertyChanged(nameof(BlockMovement));
                this.RaisePropertyChanged(nameof(Speed));
                this.RaisePropertyChanged(nameof(MaxSpeed));
                UpdateRawJson();
            }
            finally
            {
                _suppressSync = false;
            }
        }

        private void UpdateRawJson()
        {
            _suppressSync = true;
            try
            {
                using var ms = new MemoryStream();
                using (var writer = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = true }))
                {
                    writer.WriteStartObject();
                    writer.WriteString("type", Type);

                    switch (Type)
                    {
                        case "CTransform":
                            writer.WritePropertyName("position");
                            writer.WriteStartObject();
                            writer.WriteNumber("x", PosX);
                            writer.WriteNumber("y", PosY);
                            writer.WriteEndObject();

                            writer.WritePropertyName("velocity");
                            writer.WriteStartObject();
                            writer.WriteNumber("x", VelX);
                            writer.WriteNumber("y", VelY);
                            writer.WriteEndObject();

                            writer.WritePropertyName("scale");
                            writer.WriteStartObject();
                            writer.WriteNumber("x", ScaleX);
                            writer.WriteNumber("y", ScaleY);
                            writer.WriteEndObject();

                            writer.WriteNumber("rotation", Rotation);
                            break;
                        case "CAnimation":
                            writer.WriteString("animationName", AnimationName);
                            break;
                        case "CBoundingBox":
                            writer.WritePropertyName("size");
                            writer.WriteStartObject();
                            writer.WriteNumber("x", Width);
                            writer.WriteNumber("y", Height);
                            writer.WriteEndObject();
                            writer.WriteBoolean("blockVision", BlockVision);
                            writer.WriteBoolean("blockMovement", BlockMovement);
                            break;
                        case "CInput":
                            // no properties
                            break;
                        case "CMovement":
                            writer.WriteNumber("speed", Speed);
                            writer.WriteNumber("maxSpeed", MaxSpeed);
                            break;
                    }

                    writer.WriteEndObject();
                }
                RawJson = Encoding.UTF8.GetString(ms.ToArray());
            }
            finally
            {
                _suppressSync = false;
            }
        }

        private void TryParseFromRawJson()
        {
            try
            {
                using var doc = JsonDocument.Parse(RawJson);
                var root = doc.RootElement;
                if (root.TryGetProperty("type", out var typeProp))
                {
                    var t = typeProp.GetString();
                    if (!string.IsNullOrWhiteSpace(t) && t != Type)
                    {
                        _suppressSync = true;
                        try { _type = t!; this.RaisePropertyChanged(nameof(Type)); RaiseTypeFlags(); } finally { _suppressSync = false; }
                    }
                }

                switch (Type)
                {
                    case "CTransform":
                        if (root.TryGetProperty("position", out var pos))
                        {
                            PosX = pos.TryGetProperty("x", out var x) ? x.GetDouble() : 0;
                            PosY = pos.TryGetProperty("y", out var y) ? y.GetDouble() : 0;
                        }
                        if (root.TryGetProperty("velocity", out var vel))
                        {
                            VelX = vel.TryGetProperty("x", out var vx) ? vx.GetDouble() : 0;
                            VelY = vel.TryGetProperty("y", out var vy) ? vy.GetDouble() : 0;
                        }
                        if (root.TryGetProperty("scale", out var scale))
                        {
                            ScaleX = scale.TryGetProperty("x", out var sx) ? sx.GetDouble() : 1;
                            ScaleY = scale.TryGetProperty("y", out var sy) ? sy.GetDouble() : 1;
                        }
                        Rotation = root.TryGetProperty("rotation", out var rot) ? rot.GetDouble() : 0;
                        break;
                    case "CAnimation":
                        AnimationName = root.TryGetProperty("animationName", out var anim) ? anim.GetString() ?? string.Empty : string.Empty;
                        break;
                    case "CBoundingBox":
                        if (root.TryGetProperty("size", out var size))
                        {
                            Width = size.TryGetProperty("x", out var w) ? w.GetDouble() : 0;
                            Height = size.TryGetProperty("y", out var h) ? h.GetDouble() : 0;
                        }
                        BlockVision = root.TryGetProperty("blockVision", out var bv) && bv.GetBoolean();
                        BlockMovement = root.TryGetProperty("blockMovement", out var bm) ? bm.GetBoolean() : true;
                        break;
                    case "CInput":
                        break;
                    case "CMovement":
                        Speed = root.TryGetProperty("speed", out var sp) ? sp.GetDouble() : 0;
                        MaxSpeed = root.TryGetProperty("maxSpeed", out var ms) ? ms.GetDouble() : 0;
                        break;
                }
            }
            catch
            {
                // Ignore parse errors; keep current typed fields
            }
        }

        private void RaiseTypeFlags()
        {
            this.RaisePropertyChanged(nameof(IsTransform));
            this.RaisePropertyChanged(nameof(IsAnimation));
            this.RaisePropertyChanged(nameof(IsBoundingBox));
            this.RaisePropertyChanged(nameof(IsInput));
            this.RaisePropertyChanged(nameof(IsMovement));
        }
    }
}
