using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using GameEngine.Core;
using GameEngine.Core.Utils;
using ReactiveUI;
using Unit = ReactiveUI.Primitives.RxVoid;

namespace GameEngine.Editor.ViewModels
{
    public class LevelDocumentViewModel : ViewModelBase
    {
        // List of supported component types for the editor
        public static readonly string[] ComponentTypes = new[]
        {
            "CTransform", "CAnimation", "CBoundingBox", "CInput", "CMovement"
        };

        private readonly Func<string?> _projectPath;
        private readonly EditorStatus _status;

        private LevelFile? _levelFile;

        public LevelDocumentViewModel(Func<string?> projectPath, EditorStatus status)
        {
            _projectPath = projectPath;
            _status = status;

            LoadLevelFileCommand = ReactiveCommand.CreateFromTask(LoadLevelFileAsync);
            SaveLevelFileCommand = ReactiveCommand.Create((Action)SaveLevelFile);
            AddEntityCommand = ReactiveCommand.Create((Action)AddEntity);

            var canModifyEntity = this.WhenAnyValue(vm => vm.SelectedLevelEntity).Select(e => e != null);
            var canModifyComponent = this.WhenAnyValue(vm => vm.SelectedLevelComponent).Select(c => c != null);

            RemoveEntityCommand = ReactiveCommand.Create((Action)RemoveEntity, canModifyEntity);
            AddComponentCommand = ReactiveCommand.Create((Action)AddComponent, canModifyEntity);
            RemoveComponentCommand = ReactiveCommand.Create((Action)RemoveComponent, canModifyComponent);
        }

        private string? _levelFilePath;
        public string? LevelFilePath
        {
            get => _levelFilePath;
            private set => this.RaiseAndSetIfChanged(ref _levelFilePath, value);
        }

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

        // Populate/refresh the list of level files from the project
        // Preserve current selection before clearing the collection (ComboBox will push null on clear)
        public void RefreshLevelFileOptions()
        {
            var previousSelection = _selectedLevelFile;

            _levelFileOptions.Clear();

            var projectPath = _projectPath();
            if (projectPath == null)
            {
                SelectedLevelFile = null;
                return;
            }

            var projectDir = Path.GetDirectoryName(projectPath);
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
            if (_projectPath() == null) return;
            try
            {
                var selectedAtClick = SelectedLevelFile;
                RefreshLevelFileOptions();
                var levelFile = (!string.IsNullOrWhiteSpace(selectedAtClick) && File.Exists(selectedAtClick))
                    ? selectedAtClick
                    : SelectedLevelFile;
                if (string.IsNullOrWhiteSpace(levelFile) || !File.Exists(levelFile))
                {
                    _status.Message = "Select a level file to load.";
                    return;
                }

                var lf = LevelFile.LoadFromFile(levelFile, new FileAssetSource());
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
                _status.Message = "Level loaded.";
            }
            catch (Exception ex)
            {
                _status.Message = $"Failed to load level: {ex.Message}";
            }
            await Task.CompletedTask;
        }

        // The document on disk and the document in memory are both replaced only once a
        // complete, validated candidate has been written, so a rejected save costs the author
        // neither their file nor their unsaved edits.
        private void SaveLevelFile()
        {
            if (_levelFile == null || LevelFilePath == null) return;

            LevelFile candidate;
            try
            {
                candidate = BuildCandidateDocument(_levelFile);
                candidate.Validate();
            }
            catch (Exception ex)
            {
                _status.Message = $"Level not saved: {ex.Message}";
                return;
            }

            try
            {
                candidate.SaveToFile(LevelFilePath);
            }
            catch (Exception ex)
            {
                _status.Message = $"Failed to save level: {ex.Message}";
                return;
            }

            _levelFile = candidate;
            _status.Message = "Level saved.";
        }

        private LevelFile BuildCandidateDocument(LevelFile current)
        {
            var candidate = new LevelFile
            {
                Schema = current.Schema,
                Metadata = current.Metadata
            };

            for (var entityIndex = 0; entityIndex < LevelEntities.Count; entityIndex++)
            {
                var evm = LevelEntities[entityIndex];
                var entityData = new EntityData { Tag = evm.Tag };

                for (var componentIndex = 0; componentIndex < evm.Components.Count; componentIndex++)
                {
                    entityData.Components.Add(
                        ReadComponent(evm.Components[componentIndex], evm.Tag, entityIndex, componentIndex));
                }

                candidate.Entities.Add(entityData);
            }

            return candidate;
        }

        private static ComponentData ReadComponent(
            LevelComponentViewModel component, string entityTag, int entityIndex, int componentIndex)
        {
            var location = LevelFile.Where(entityTag, entityIndex, componentIndex);

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(component.RawJson);
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException($"{location} is not valid JSON: {ex.Message}", ex);
            }

            using (document)
            {
                var root = document.RootElement;
                var type = component.Type;

                if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("type", out var typeProperty))
                {
                    if (typeProperty.ValueKind != JsonValueKind.String)
                        throw new InvalidDataException($"{location} has a non-string \"type\".");

                    type = typeProperty.GetString();
                }

                if (string.IsNullOrWhiteSpace(type))
                    throw new InvalidDataException($"{location} has no component type.");

                return new ComponentData { Type = type, Data = root.Clone() };
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
}
