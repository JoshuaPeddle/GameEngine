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

        private void SelectEntityAt(Point screenPosition)
        {
            if (_gameEngine == null || _vm == null)
                return;

            var viewport = _gameEngine.Systems.Get<RenderSystem>()
                .ViewportFor(_gameEngine.InputManager.RealResolution);

            if (!viewport.TryToVirtual(new Vec2(screenPosition.X, screenPosition.Y), out var pick))
                return;

            foreach (var entity in _gameEngine.EntityManager.GetEntities())
            {
                if (!entity.TryGetComponent<Core.Components.CTransform>(out var transform))
                    continue;

                if (!TryGetPickSize(entity, out var size))
                    continue;

                if (pick.X >= transform.Position.X && pick.X <= transform.Position.X + size.X &&
                    pick.Y >= transform.Position.Y && pick.Y <= transform.Position.Y + size.Y)
                {
                    _vm.EntitySelectedCommand.Execute(entity).Subscribe();
                    return;
                }
            }
        }

        private static bool TryGetPickSize(Core.Entity entity, out Vec2 size)
        {
            if (entity.TryGetComponent<Core.Components.CBoundingBox>(out var boundingBox))
            {
                size = boundingBox.Size;
                return true;
            }

            if (entity.TryGetComponent<Core.Components.CAnimation>(out var animation))
            {
                size = new Vec2(animation.GetSourceRect().Size);
                return true;
            }

            size = default;
            return false;
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
            _gameEngine?.Dispose();
            _gameEngine = null;
            _started = false;
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
                if (path.Contains("assets.txt") || path.Contains("levels"))
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
                if (_engine == null) return;
                var leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
                if (leaseFeature == null) return;

                using var lease = leaseFeature.Lease();
                var canvas = lease.SkCanvas;

                canvas.Save();
                canvas.ClipRect(new SKRect(0, 0, (float)Bounds.Width, (float)Bounds.Height));
                
                _engine.Systems.Get<RenderSystem>().DrawEntitiesToCanvas(canvas, _engine.GetRenderSnapshot());

                canvas.Restore();

                // Resume updates after first visible frame of a new scene
                _engine.NotifyFirstPresent();
            }
        }
    }
}
