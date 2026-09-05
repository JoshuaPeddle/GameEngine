using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GameEngine.Core;
using GameEngine.Core.Components;
using GameEngine.Core.Utils;
using GameEngine.Editor.Magic;
using ReactiveUI;
using Unit = ReactiveUI.Primitives.RxVoid;

namespace GameEngine.Editor.ViewModels
{
    public class LevelEditorViewModel : ViewModelBase
    {
        public AssetEditorViewModel AssetEditorViewModel;

        public LevelEditorViewModel(AssetEditorViewModel assetEditorViewModel)
        {
            AssetEditorViewModel = assetEditorViewModel;

            Status = new EditorStatus();
            Inspector = new EntityInspectorViewModel();
            Level = new LevelDocumentViewModel(() => ProjectPath, Status);
            SceneCatalog = new SceneCatalogViewModel(() => ProjectPath, Status, () => Inspector.Select(null));

            SceneCatalog.SceneSelected += type => SceneSelected?.Invoke(type);
            SceneCatalog.ScenesReloading += () => ScenesReloading?.Invoke();

            EntitySelectedCommand = ReactiveCommand.Create<EntitySnapshot>(SelectEntity);
            PreviewLevelCommand = ReactiveCommand.Create((Action)PreviewLevel);

            // While the preview is paused it tracks the document; once it is running, the
            // simulation owns the entities and the document stops pushing at it. Either way the
            // document is the source of truth — nothing the simulation does is written back.
            Level.DocumentChanged += () =>
            {
                if (!IsEngineRunning && _previewingLevel)
                    PreviewLevel();
            };
        }

        public LevelEditorViewModel()
        {
            AssetEditorViewModel = null!;

            Status = new EditorStatus();
            Inspector = new EntityInspectorViewModel();
            Level = new LevelDocumentViewModel(() => ProjectPath, Status);
            SceneCatalog = new SceneCatalogViewModel(() => ProjectPath, Status, () => Inspector.Select(null));
            EntitySelectedCommand = ReactiveCommand.Create<EntitySnapshot>(entity => Inspector.Select(entity));
            PreviewLevelCommand = ReactiveCommand.Create((Action)PreviewLevel);

            SceneCatalog.Scenes.Add("No Project Loaded");

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
                Level.LevelEntities.Add(evm);
            }
            Level.SelectedLevelEntity = Level.LevelEntities.FirstOrDefault();

            var designAssetSource = new DelegateAssetSource(
                path => AssetManifest.IsManifestPath(path)
                    ? File.Open(Path.Combine("GameEngine.Demo", path), FileMode.Open)
                    : File.Open(Path.Combine("GameEngine.Demo", "assets", path), FileMode.Open));

            try
            {
                var designEntities = new EntityManager();
                new LevelLoader(new ComponentFactory(new Assets("assets.json", designAssetSource)))
                    .LoadLevel(level, designEntities);
                designEntities.Update();
                Inspector.Select(designEntities.GetEntities().FirstOrDefault()?.Capture());
            }
            catch (IOException)
            {
                Inspector.Select(null);
            }
        }

        public EditorStatus Status { get; }
        public SceneCatalogViewModel SceneCatalog { get; }
        public EntityInspectorViewModel Inspector { get; }
        public LevelDocumentViewModel Level { get; }

        public ReactiveCommand<EntitySnapshot, Unit> EntitySelectedCommand { get; }

        public ReactiveCommand<Unit, Unit> PreviewLevelCommand { get; }

        public event Action<Type>? SceneSelected;
        public event Action? ScenesReloading;
        public event Action<LevelDocumentScene>? PreviewLevelRequested;

        private bool _previewingLevel;
        private LevelDocumentScene? _previewScene;

        /// <summary>Builds a preview from the authored document and hands it to the view.</summary>
        public void PreviewLevel()
        {
            var handler = PreviewLevelRequested;
            if (handler == null)
                return;

            var projectFolder = AssetEditorViewModel?.ProjectEditor?.ProjectFolderPath;
            if (string.IsNullOrWhiteSpace(projectFolder))
            {
                Status.Message = "Open a project before previewing a level.";
                return;
            }

            var (level, documentIds) = Level.Project();

            try
            {
                level.Validate();
            }
            catch (Exception ex)
            {
                Status.Message = $"Level not previewed: {ex.Message}";
                return;
            }

            var scene = new LevelDocumentScene(
                level,
                documentIds,
                () => new Assets("assets.json", Controls.EngineView.CreateProjectAssetSource(projectFolder)));

            _previewScene = scene;
            _previewingLevel = true;
            handler(scene);
        }

        // A pick reports a runtime id. Only the preview knows which document row produced it,
        // and a runtime-only entity produces none — the inspector then shows it read-only.
        private void SelectEntity(EntitySnapshot entity)
        {
            Inspector.Select(entity);

            var documentId = _previewScene?.DocumentIdOf(entity.Id);
            if (documentId.HasValue)
                Level.SelectByDocumentId(documentId.Value);
        }

        public bool IsPreviewingLevel => _previewingLevel;

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

        public Task EnsureScenesLoadedAsync() => SceneCatalog.EnsureScenesLoadedAsync();

        public void RefreshLevelFileOptions() => Level.RefreshLevelFileOptions();
    }
}
