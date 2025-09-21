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
using System.IO;
using System.Threading.Tasks;

namespace GameEngine.Editor.Controls
{
    public class EngineView : Control
    {
        private Engine? _gameEngine;
        private bool _started;
        private LevelEditorViewModel? _vm;

        public EngineView()
        {
            SizeChanged += OnSizeChanged;
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object? sender, EventArgs e)
        {
            if (_vm != null)
                _vm.SceneSelected -= OnSceneSelected;

            _vm = DataContext as LevelEditorViewModel;

            if (_vm != null)
            {
                _vm.SceneSelected += OnSceneSelected;
                // Scenes may already be loaded & selected
                if (_vm.SelectedScene != null && _vm is { })
                {
                    // SceneSelected event will fire on setter only; manually trigger if already set
                    // Re-resolve type from last compilation indirectly handled already in VM
                }
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
                    _gameEngine = new Engine(InvalidateVisual, audioEnabled: false);
                    _gameEngine.Systems.TryGet<RenderSystem>().options.DrawBoundingBoxes = true;
                    InitializeAssetFileFetcher(_vm.AssetEditorViewModel.ProjectEditor.ProjectFolderPath);
                    _gameEngine.InitializeSystems();
                }

                var sceneInstance = (Scene)Activator.CreateInstance(sceneType)!;
                _gameEngine.ChangeScene(sceneInstance);
                InvalidateVisual();
                if (!_started)
                {
                    _started = true;
                    await _gameEngine.Start();
                }
            });
        }

        private static void InitializeAssetFileFetcher(string projectCsprojPath)
        {
            Assets._fileFetcher = (string path) =>
            {
                var projectDir = Path.GetDirectoryName(projectCsprojPath)!;
                if (path.Contains("assets.txt") || path.Contains("levels"))
                {
                    var assetFilesPath = Path.GetFullPath(Path.Combine(projectDir, path));
                    return File.Open(assetFilesPath, FileMode.Open, FileAccess.Read);
                }
                var fullPath = Path.GetFullPath(Path.Combine(projectDir, "assets/" + path));
                return File.Open(fullPath, FileMode.Open, FileAccess.Read);
            };
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

                _engine.Systems.Get<RenderSystem>().DrawEntitiesToCanvas(canvas);

                canvas.Restore();
            }
        }
    }
}
