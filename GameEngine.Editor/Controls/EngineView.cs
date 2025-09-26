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
            PointerPressed += (s, e) => 
            {
                var entityManager = _gameEngine?.EntityManager.GetEntities();
                if (entityManager == null || _vm == null) return;
                foreach (var entity in entityManager)
                {
                    if (entity.HasComponent<Core.Components.CTransform>() && entity.HasComponent<Core.Components.CAnimation>())
                    {
                        var transform = entity.GetComponent<Core.Components.CTransform>();
                        var sprite = entity.GetComponent<Core.Components.CAnimation>();
                        var inputManager = _gameEngine.InputManager;

                        var realResolution = inputManager.RealResolution;
                        var virtualResolution = inputManager.VirtualResolution;

                        double scaleX = realResolution.X / virtualResolution.X;
                        double scaleY = realResolution.Y / virtualResolution.Y;

                        double finalScale = Math.Min(scaleX, scaleY);

                        double scaledWidth = virtualResolution.X * finalScale;
                        double scaledHeight = virtualResolution.Y * finalScale;
                        double leftoverX = (realResolution.X - scaledWidth) / 2;
                        double leftoverY = (realResolution.Y - scaledHeight) / 2;

                        double adjustedX = e.GetPosition(this).X - leftoverX;
                        double adjustedY = e.GetPosition(this).Y - leftoverY;
                        double virtualX = adjustedX / finalScale;
                        double virtualY = adjustedY / finalScale;

                        if (entity.TryGetComponent<Core.Components.CBoundingBox>(out var boundingBox))
                        {
                            var boxPos = transform.Position - (boundingBox.Size / 2);
                            if (virtualX >= boxPos.X + boundingBox.Size.X/2 && virtualX <= boxPos.X + boundingBox.Size.X *1.5 &&
                                virtualY >= boxPos.Y + boundingBox.Size.Y/2 && virtualY <= boxPos.Y + boundingBox.Size.Y *1.5)
                            {
                                _vm?.EntitySelectedCommand.Execute(entity).Subscribe();
                                break;
                            }
                        }
                        else
                        {
                            var boxPos = transform.Position - new Vec2(sprite.GetSourceRect().Size);
                            if (virtualX >= boxPos.X && virtualX <= boxPos.X + new Vec2(sprite.GetSourceRect().Size).X &&
                                virtualY >= boxPos.Y && virtualY <= boxPos.Y + new Vec2(sprite.GetSourceRect().Size).Y)
                            {
                                _vm?.EntitySelectedCommand.Execute(entity).Subscribe();
                                break;
                            }
                        }
                    }
                }
            };
        }

        private void OnDataContextChanged(object? sender, EventArgs e)
        {
            if (_vm != null)
            {
                _vm.SceneSelected -= OnSceneSelected;
                _vm.PropertyChanged -= VmOnPropertyChanged;
            }

            _vm = DataContext as LevelEditorViewModel;

            if (_vm != null)
            {
                _vm.SceneSelected += OnSceneSelected;
                _vm.PropertyChanged += VmOnPropertyChanged;
            }
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
                    _gameEngine = new Engine(InvalidateVisual, audioEnabled: false);
                    _gameEngine.Systems.TryGet<RenderSystem>().options.DrawBoundingBoxes = true;
                    InitializeAssetFileFetcher(_vm.AssetEditorViewModel.ProjectEditor.ProjectFolderPath);
                    _gameEngine.InitializeSystems();
                    _gameEngine.SetRunning(_vm.IsEngineRunning);
                    _gameEngine?.SizeChanged((int)Bounds.Width, (int)Bounds.Height);
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
                    return File.Open(assetFilesPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                }
                var fullPath = Path.GetFullPath(Path.Combine(projectDir, "assets/" + path));
                return File.Open(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
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
