using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GameEngine.Core;
using GameEngine.Core.Components;
using GameEngine.Core.Utils;
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

            EntitySelectedCommand = ReactiveCommand.Create<EntitySnapshot>(entity => Inspector.Select(entity));
        }

        public LevelEditorViewModel()
        {
            AssetEditorViewModel = null!;

            Status = new EditorStatus();
            Inspector = new EntityInspectorViewModel();
            Level = new LevelDocumentViewModel(() => ProjectPath, Status);
            SceneCatalog = new SceneCatalogViewModel(() => ProjectPath, Status, () => Inspector.Select(null));
            EntitySelectedCommand = ReactiveCommand.Create<EntitySnapshot>(entity => Inspector.Select(entity));

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
                path => path.Contains("assets.txt")
                    ? File.Open(Path.Combine("GameEngine.Demo", path), FileMode.Open)
                    : File.Open(Path.Combine("GameEngine.Demo", "assets", path), FileMode.Open));

            try
            {
                var designEntities = new EntityManager();
                new LevelLoader(new ComponentFactory(new Assets("assets.txt", designAssetSource)))
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

        public event Action<Type>? SceneSelected;
        public event Action? ScenesReloading;

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
