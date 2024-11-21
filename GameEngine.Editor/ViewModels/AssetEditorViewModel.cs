using Avalonia.Media.Imaging;
using System.Threading.Tasks;
using ReactiveUI;
using System.Reactive;
using GameEngine.Editor.Services;
using System;
using System.Collections.ObjectModel;

namespace GameEngine.Editor.ViewModels;

public class AssetEditorViewModel : ViewModelBase
{
    public ReactiveCommand<Unit, Unit> OpenImageCommand { get; }
    public ReactiveCommand<Unit, Unit> CloseImageCommand { get; }
    public ReactiveCommand<Unit, Unit> ImportTextureCommand { get; }

    private readonly IFilePickerService _filePickerService;
    private ObservableCollection<Texture> _textures;
    private Task<Bitmap>? _image;
    private Uri _imagePath;
    private double _imageWidth;
    private double _imageHeight;
    private string textureName;

    public AssetEditorViewModel(IFilePickerService filePickerService)
    {
        _filePickerService = filePickerService;
        OpenImageCommand = ReactiveCommand.CreateFromTask(OpenImage);
        CloseImageCommand = ReactiveCommand.CreateFromTask(CloseImage);
        ImportTextureCommand = ReactiveCommand.CreateFromTask(ImportTexture);
        Textures = new ObservableCollection<Texture>();
    }
    public AssetEditorViewModel() : this(null!) // Designer constructor
    {
        Textures =
        [
            new Texture { Name = "Texture 1", Path = "path/to/texture1.png", Bitmap = Task.FromResult(new Bitmap("GameEngine.Demo/assets/images/jeep.png")) },
            new Texture { Name = "Texture 2", Path = "path/to/texture2.png", Bitmap = Task.FromResult(new Bitmap("GameEngine.Demo/assets/images/grenade.png")) },
        ];
        Image = Task.FromResult(new Bitmap("GameEngine.Demo/assets/images/jeep.png"));
        ImageHeight = 100;
        ImageWidth = 80;
    }

    public Task<Bitmap>? Image
    {
        get => _image;
        set
        {
            this.RaiseAndSetIfChanged(ref _image, value);
            this.RaisePropertyChanged(nameof(ImageDimensions));
            this.RaisePropertyChanged(nameof(ImportButtonEnabled));
        }
    }

    public string ImageDimensions => Image != null ? $"{ImageWidth}x{ImageHeight}" : string.Empty;

    public double ImageWidth
    {
        get => _imageWidth;
        set
        {
            this.RaiseAndSetIfChanged(ref _imageWidth, value);
            this.RaisePropertyChanged(nameof(ImageDimensions));
            this.RaisePropertyChanged(nameof(ImageDisplayWidth));
        }
    }

    public double ImageDisplayWidth => ImageWidth < 800 ? ImageWidth : 800;

    public double ImageHeight
    {
        get => _imageHeight;
        set
        {
            this.RaiseAndSetIfChanged(ref _imageHeight, value);
            this.RaisePropertyChanged(nameof(ImageDimensions));
            this.RaisePropertyChanged(nameof(ImageDisplayHeight));
        }
    }

    public double ImageDisplayHeight => ImageHeight < 600 ? ImageHeight : 600;

    public ObservableCollection<Texture> Textures
    {
        get => _textures;
        set => this.RaiseAndSetIfChanged(ref _textures, value);
    }

    public string TextureName
    {
        get => textureName;
        set
        {
            this.RaiseAndSetIfChanged(ref textureName, value);
            this.RaisePropertyChanged(nameof(ImportButtonEnabled));
        }
    }

    public async Task ImportTexture()
    {
        var texture = new Texture
        {
            Name = TextureName,
            Path = _imagePath.LocalPath,
            Bitmap = Image
        };
        Textures.Add(texture);
        await Reset();
    }

    public async Task OpenImage()
    {
        var filePath = await _filePickerService.OpenFileAsync();
        if (filePath != null)
            if (filePath != null)
            {
                _imagePath = new Uri(filePath);
                var bitmap = new Bitmap(filePath);
                Image = Task.FromResult(bitmap);
                ImageWidth = bitmap.PixelSize.Width;
                ImageHeight = bitmap.PixelSize.Height;
            }
    }

    public async Task CloseImage()
    {
        await Reset();
    }

    private async Task Reset()
    {
        if (Image != null)
        {
            await Image;
            Image = null;
        }
        ImageWidth = 0;
        ImageHeight = 0;
        TextureName = string.Empty;
    }

    public bool ImportButtonEnabled => Image != null && !string.IsNullOrWhiteSpace(TextureName);

    public class Texture
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public Task<Bitmap> Bitmap { get; set; }
    }
}
