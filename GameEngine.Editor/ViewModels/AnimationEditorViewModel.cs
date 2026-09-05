using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using GameEngine.Editor.Models;
using GameEngine.Editor.Services;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Unit = ReactiveUI.Primitives.RxVoid;

namespace GameEngine.Editor.ViewModels
{
    public class AnimationEditorViewModel : ViewModelBase
    {
        public ReactiveCommand<Unit, Unit> SaveAnimationCommand { get; }

        private readonly AssetEditorViewModel _parentViewModel;
        private Texture? _selectedTexture;
        private string _animationName = string.Empty;
        private int _frameCount;
        private int _frameDelay;
        private bool _animate;
        private readonly List<IImage> _frames = [];
        private int _currentFrameIndex;
        private IDisposable? _animationTimer;

        public AnimationEditorViewModel(AssetEditorViewModel parentViewModel)
        {
            _parentViewModel = parentViewModel;
            SaveAnimationCommand = ReactiveCommand.Create(
                SaveAnimation,
                this.WhenAnyValue(
                    x => x.SelectedTexture,
                    x => x.AnimationName,
                    x => x.FrameCount,
                    (texture, name, count) => texture != null && !string.IsNullOrWhiteSpace(name) && count > 0
                )
            );
        }

        public AnimationEditorViewModel() // Designer constructor
        {
            _parentViewModel = new AssetEditorViewModel(new FilePickerService());
            SaveAnimationCommand = ReactiveCommand.Create(SaveAnimation);
            foreach (var texture in new[]
            {
                new Texture { Name = "Texture 1", Path = "path/to/texture1.png", Bitmap = Task.FromResult(new Bitmap("GameEngine.Demo/assets/images/jeep.png")) },
                new Texture { Name = "Texture 2", Path = "path/to/texture2.png", Bitmap = Task.FromResult(new Bitmap("GameEngine.Demo/assets/images/grenade.png")) }
            })
                Textures.Add(texture);

            foreach (var animation in new[]
            {
                new Animation { Name = "Animation 1", Texture = Textures[0], FrameCount = 1 },
                new Animation { Name = "Animation 2", Texture = Textures[1], FrameCount = 4, Delay = 250 }
            })
                Animations.Add(animation);

            SelectedTexture = Textures[0];
        }

        public ObservableCollection<Texture> Textures => _parentViewModel.SharedTextures;

        public ObservableCollection<Animation> Animations => _parentViewModel.SharedAnimations;

        public IImage? Image
        {
            get
            {
                if (_frames.Count == 0)
                    return null;

                return _frames[_currentFrameIndex];
            }
        }

        public Texture? SelectedTexture
        {
            get => _selectedTexture;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedTexture, value);
                UpdateFrames();
            }
        }

        public string AnimationName
        {
            get => _animationName;
            set => this.RaiseAndSetIfChanged(ref _animationName, value);
        }

        public int FrameCount
        {
            get => _frameCount;
            set
            {
                this.RaiseAndSetIfChanged(ref _frameCount, value);
                UpdateFrames();
            }
        }

        public int FrameDelay
        {
            get => _frameDelay;
            set
            {
                this.RaiseAndSetIfChanged(ref _frameDelay, value);
                if (Animate)
                {
                    StartAnimation();
                }
            }
        }

        public bool Animate
        {
            get => _animate;
            set
            {
                this.RaiseAndSetIfChanged(ref _animate, value);
                if (Animate)
                {
                    StartAnimation();
                }
                else
                {
                    StopAnimation();
                    _currentFrameIndex = 0;
                    this.RaisePropertyChanged(nameof(Image));
                }
            }
        }

        private void UpdateFrames()
        {
            _frames.Clear();
            if (SelectedTexture == null || FrameCount <= 0)
                return;

            if (SelectedTexture.Bitmap == null)
                return;

            var bitmap = SelectedTexture.Bitmap.Result;

            int frameWidth = bitmap.PixelSize.Width / FrameCount;
            int frameHeight = bitmap.PixelSize.Height;

            for (int i = 0; i < FrameCount; i++)
            {
                var rect = new PixelRect(i * frameWidth, 0, frameWidth, frameHeight);
                var frame = new CroppedBitmap(bitmap, rect);
                _frames.Add(frame);
            }

            _currentFrameIndex = 0;
            this.RaisePropertyChanged(nameof(Image));

            if (Animate)
            {
                StartAnimation();
            }
        }

        private void StartAnimation()
        {
            StopAnimation();

            if (FrameDelay <= 0)
                FrameDelay = 100; // Default to 100ms if not set

            _animationTimer = Observable.Interval(TimeSpan.FromMilliseconds(FrameDelay))
                .Subscribe(_ => Dispatcher.UIThread.Post(() =>
                {
                    if (_frames.Count == 0)
                        return;
                    _currentFrameIndex = (_currentFrameIndex + 1) % _frames.Count;
                    this.RaisePropertyChanged(nameof(Image));
                }));
        }

        private void StopAnimation()
        {
            _animationTimer?.Dispose();
            _animationTimer = null;
        }

        private void SaveAnimation()
        {
            Animation animation = new(AnimationName)
            {
                Texture = SelectedTexture!,
                FrameCount = FrameCount,
                Delay = FrameDelay
            };
            Animations.Add(animation);
        }
    }
}
