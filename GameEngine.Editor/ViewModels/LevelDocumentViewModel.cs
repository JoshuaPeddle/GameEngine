using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using GameEngine.Core;
using GameEngine.Core.Components;
using GameEngine.Core.Utils;
using ReactiveUI;
using Unit = ReactiveUI.Primitives.RxVoid;

namespace GameEngine.Editor.ViewModels
{
    public class LevelDocumentViewModel : ViewModelBase
    {
        // Generated from the vocabulary the loader enforces, so the editor cannot offer a type
        // the engine does not know or omit one it does.
        public static readonly string[] ComponentTypes = ComponentSchemas.KnownTypes.ToArray();

        private readonly Func<string?> _projectPath;
        private readonly EditorStatus _status;

        private LevelFile? _levelFile;

        public EditHistory History { get; } = new();

        /// <summary>Raised whenever the authored document changes, however it changed.</summary>
        public event Action? DocumentChanged;

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

            UndoCommand = ReactiveCommand.Create((Action)Undo);
            RedoCommand = ReactiveCommand.Create((Action)Redo);

            History.Changed += OnHistoryChanged;
            LevelEntities.CollectionChanged += (_, args) =>
            {
                foreach (var added in args.NewItems?.Cast<LevelEntityViewModel>() ?? [])
                    Watch(added);
            };
        }

        private void OnHistoryChanged()
        {
            this.RaisePropertyChanged(nameof(CanUndo));
            this.RaisePropertyChanged(nameof(CanRedo));
            this.RaisePropertyChanged(nameof(HasUnsavedChanges));
            this.RaisePropertyChanged(nameof(UnsavedChangesSummary));
            DocumentChanged?.Invoke();
        }

        public bool CanUndo => History.CanUndo;

        public bool CanRedo => History.CanRedo;

        public bool HasUnsavedChanges => !History.IsClean;

        public string UnsavedChangesSummary => HasUnsavedChanges
            ? $"Unsaved changes ({History.Depth})"
            : "No unsaved changes";

        public ReactiveCommand<Unit, Unit> UndoCommand { get; }

        public ReactiveCommand<Unit, Unit> RedoCommand { get; }

        private void Undo() => History.Undo();

        private void Redo() => History.Redo();

        // Every component in the document reports its own edits here, so a value typed into the
        // inspector is as undoable as adding an entity.
        private void Watch(LevelEntityViewModel entity)
        {
            foreach (var component in entity.Components)
                Watch(component);

            entity.Components.CollectionChanged += (_, args) =>
            {
                foreach (var added in args.NewItems?.Cast<LevelComponentViewModel>() ?? [])
                    Watch(added);
            };
        }

        private void Watch(LevelComponentViewModel component)
        {
            component.Edited -= OnComponentEdited;
            component.Edited += OnComponentEdited;
        }

        private void OnComponentEdited(LevelComponentViewModel component, ComponentState before)
        {
            if (History.IsReplaying)
                return;

            History.Do(new EditComponentEdit(component, before, component.State));
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
                History.Clear();
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
            History.MarkClean();
            _status.Message = "Level saved.";
        }

        /// <summary>
        /// The authored document as the engine would read it, with the document identity of
        /// each entry alongside. Both the preview and the file are built from this, which is
        /// what keeps what an author sees and what they save the same thing.
        /// </summary>
        public (LevelFile Level, IReadOnlyList<Guid> DocumentIds) Project()
        {
            var candidate = BuildCandidateDocument(_levelFile ?? new LevelFile());
            var identities = LevelEntities.Select(entity => entity.DocumentId).ToList();
            return (candidate, identities);
        }

        public LevelEntityViewModel? FindByDocumentId(Guid documentId) =>
            LevelEntities.FirstOrDefault(entity => entity.DocumentId == documentId);

        public bool SelectByDocumentId(Guid documentId)
        {
            var entity = FindByDocumentId(documentId);
            if (entity == null)
                return false;

            SelectedLevelEntity = entity;
            SelectedLevelComponent = entity.Components.FirstOrDefault();
            return true;
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

            History.Do(new AddEntityEdit(LevelEntities, newEntity, LevelEntities.Count));
            SelectedLevelEntity = newEntity;
        }

        /// <summary>Places a new entity at a world position, which is what a viewport click does.</summary>
        public LevelEntityViewModel PlaceEntity(string tag, Vec2 position)
        {
            var entity = new LevelEntityViewModel { Tag = tag };
            var transform = new LevelComponentViewModel();
            transform.ApplyTypeWithDefaults("CTransform");
            transform.PosX = position.X;
            transform.PosY = position.Y;
            entity.Components.Add(transform);

            History.Do(new AddEntityEdit(LevelEntities, entity, LevelEntities.Count));
            SelectedLevelEntity = entity;
            SelectedLevelComponent = transform;
            return entity;
        }

        private void RemoveEntity()
        {
            if (SelectedLevelEntity == null) return;
            var idx = LevelEntities.IndexOf(SelectedLevelEntity);
            History.Do(new RemoveEntityEdit(LevelEntities, SelectedLevelEntity, idx));
            SelectedLevelEntity = LevelEntities.Count > 0 ? LevelEntities[Math.Clamp(idx - 1, 0, LevelEntities.Count - 1)] : null;
        }

        private void AddComponent()
        {
            if (SelectedLevelEntity == null) return;
            // Default to a CTransform with sensible defaults
            var comp = new LevelComponentViewModel();
            comp.ApplyTypeWithDefaults("CTransform");
            History.Do(new AddComponentEdit(SelectedLevelEntity, comp, SelectedLevelEntity.Components.Count));
            SelectedLevelComponent = comp;
        }

        private void RemoveComponent()
        {
            if (SelectedLevelEntity == null || SelectedLevelComponent == null) return;
            var list = SelectedLevelEntity.Components;
            var idx = list.IndexOf(SelectedLevelComponent);
            History.Do(new RemoveComponentEdit(SelectedLevelEntity, SelectedLevelComponent, idx));
            SelectedLevelComponent = list.Count > 0 ? list[Math.Clamp(idx - 1, 0, list.Count - 1)] : null;
        }

        public void RenameSelectedEntity(string tag)
        {
            if (SelectedLevelEntity == null || SelectedLevelEntity.Tag == tag) return;
            History.Do(new RenameEntityEdit(SelectedLevelEntity, SelectedLevelEntity.Tag, tag));
        }
    }
}
