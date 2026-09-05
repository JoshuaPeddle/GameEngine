using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using GameEngine.Core;
using GameEngine.Core.Systems;
using GameEngine.Editor.ViewModels;
using SkiaSharp;
using System;
using System.ComponentModel; 
using System.IO;
using System.Threading;

namespace GameEngine.Editor.Controls
{
    public class EngineView : Control
    {
        private Engine? _gameEngine;
        private bool _started;
        private LevelEditorViewModel? _vm;

        // Coalesce pending invalidations
        private int _invalidationsPending = 0;

        public EngineView()
        {
            SizeChanged += OnSizeChanged;
            DataContextChanged += OnDataContextChanged;
            PointerPressed += (s, e) => SelectEntityAt(e.GetPosition(this));
        }

        // The preview engine is owned by this view, so leaving the tree ends it. A later
        // scene selection builds a fresh one.
        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            DisposeEngine();
            base.OnDetachedFromVisualTree(e);
        }

        private void SelectEntityAt(Point screenPosition)
        {
            var engine = _gameEngine;
            var viewModel = _vm;
            if (engine == null || viewModel == null)
                return;

            var screenPoint = new Vec2(screenPosition.X, screenPosition.Y);

            // Entities are owned by the engine thread. Pick there and hand back an immutable
            // snapshot rather than reading live components from the UI thread.
            engine.Post(e =>
            {
                var snapshot = PickEntity(e, screenPoint);
                if (snapshot == null)
                    return;

                Dispatcher.UIThread.Post(() => viewModel.EntitySelectedCommand.Execute(snapshot).Subscribe());
            });
        }

        // Picking runs against the same snapshot geometry and the same camera transform the
        // renderer uses, so an entity is selected where it was drawn — camera position and
        // zoom, sprite centring, rotation, scale and layer order included.
        private static EntitySnapshot? PickEntity(Engine engine, Vec2 screenPoint)
        {
            var renderSystem = engine.Systems.TryGet<RenderSystem>();
            if (renderSystem == null)
                return null;

            var snapshot = new RenderSnapshot();
            engine.EntityManager.BuildRenderSnapshot(snapshot);

            if (!renderSystem.TryScreenToWorld(
                    screenPoint, engine.InputManager.RealResolution, snapshot.ActiveCamera, out var world))
                return null;

            var picked = RenderSystem.PickTopmost(snapshot, world);

            return picked.HasValue && engine.EntityManager.TryGetEntity(picked.Value, out var entity)
                ? entity.Capture()
                : null;
        }

        private void OnDataContextChanged(object? sender, EventArgs e)
        {
            if (_vm != null)
            {
                _vm.SceneSelected -= OnSceneSelected;
                _vm.ScenesReloading -= OnScenesReloading;
                _vm.PropertyChanged -= VmOnPropertyChanged;
            }

            DisposeEngine();

            _vm = DataContext as LevelEditorViewModel;

            if (_vm != null)
            {
                _vm.SceneSelected += OnSceneSelected;
                _vm.ScenesReloading += OnScenesReloading;
                _vm.PropertyChanged += VmOnPropertyChanged;
            }
        }

        private void OnScenesReloading() => DisposeEngine();

        private void DisposeEngine()
        {
            var engine = _gameEngine;
            _gameEngine = null;
            _started = false;

            if (engine == null)
                return;

            engine.InvalidateAction = null;
            engine.Stop();
            engine.Dispose();
        }

        private void VmOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_gameEngine == null || _vm == null) return;
            if (e.PropertyName == nameof(LevelEditorViewModel.IsEngineRunning))
            {
                _gameEngine.SetRunning(_vm.IsEngineRunning);
            }
        }

        private async void OnSceneSelected(Type sceneType)
        {
            if (_vm == null) return;
            if (string.IsNullOrWhiteSpace(_vm.AssetEditorViewModel.ProjectEditor.ProjectFolderPath))
                return;

            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                if (_gameEngine == null)
                {
                    var assetSource = CreateProjectAssetSource(
                        _vm.AssetEditorViewModel.ProjectEditor.ProjectFolderPath);

                    if (OperatingSystem.IsAndroid() || OperatingSystem.IsBrowser())
                        _gameEngine = new Engine(QueueInvalidate, audioEnabled: false, assetSource);
                    else
                        _gameEngine = new Engine(QueueInvalidate, assetSource: assetSource);
                    _gameEngine.RenderOptions.DrawBoundingBoxes = true;
                    _gameEngine.TargetFrameRate = 120;
                    _gameEngine.SetRunning(_vm.IsEngineRunning);
                    _gameEngine.SizeChanged((int)Bounds.Width, (int)Bounds.Height);
                }

                var engine = _gameEngine
                    ?? throw new InvalidOperationException("The preview engine was not created.");
                var sceneInstance = (Scene)Activator.CreateInstance(sceneType)!;
                engine.ChangeScene(sceneInstance);
                InvalidateVisual();
                if (!_started)
                {
                    _started = true;
                    await engine.Start();
                }
            });
        }

        private void QueueInvalidate()
        {
            if (Interlocked.Exchange(ref _invalidationsPending, 1) == 0)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    _invalidationsPending = 0;
                    InvalidateVisual();
                }, DispatcherPriority.Render);
            }
        }

        internal static IAssetSource CreateProjectAssetSource(string projectCsprojPath)
        {
            var projectDir = Path.GetDirectoryName(projectCsprojPath)!;

            return new DelegateAssetSource(path =>
            {
                if (AssetManifest.IsManifestPath(path) || path.Contains("levels"))
                {
                    var assetFilesPath = Path.GetFullPath(Path.Combine(projectDir, path));
                    return File.Open(assetFilesPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                }

                var fullPath = Path.GetFullPath(Path.Combine(projectDir, "assets/" + path));
                return File.Open(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            });
        }

        private void OnSizeChanged(object? sender, EventArgs args)
        {
            _gameEngine?.SizeChanged((int)Bounds.Width, (int)Bounds.Height);
        }

        public override void Render(DrawingContext context)
        {
            context.Custom(new CustomDrawOp(new Rect(0, 0, Bounds.Width, Bounds.Height), _gameEngine));
        }

        class CustomDrawOp : ICustomDrawOperation
        {
            public Rect Bounds { get; set; }
            private readonly Engine? _engine;

            public CustomDrawOp(Rect bounds, Engine? engine)
            {
                Bounds = bounds;
                _engine = engine;
            }

            public void Dispose() { }
            public bool Equals(ICustomDrawOperation? other) => false;
            public bool HitTest(Point p) => Bounds.Contains(p);

            public void Render(ImmediateDrawingContext context)
            {
                if (_engine == null || _engine.IsDisposed) return;
                var leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
                if (leaseFeature == null) return;

                var renderSystem = _engine.Systems.TryGet<RenderSystem>();
                if (renderSystem == null) return;

                using var lease = leaseFeature.Lease();
                var canvas = lease.SkCanvas;

                canvas.Save();
                canvas.ClipRect(new SKRect(0, 0, (float)Bounds.Width, (float)Bounds.Height));

                renderSystem.DrawEntitiesToCanvas(canvas, _engine.GetRenderSnapshot());

                canvas.Restore();

                // Resume updates after first visible frame of a new scene
                _engine.NotifyFirstPresent();
            }
        }
    }
}
