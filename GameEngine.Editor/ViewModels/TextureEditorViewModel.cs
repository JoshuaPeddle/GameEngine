using Avalonia.Media.Imaging;
using GameEngine.Editor.Models;
using GameEngine.Editor.Services;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Unit = ReactiveUI.Primitives.RxVoid;

namespace GameEngine.Editor.ViewModels;

public class TextureEditorViewModel : ViewModelBase
{
    public ReactiveCommand<Unit, Unit> OpenImageCommand { get; }
    public ReactiveCommand<Unit, Unit> CloseImageCommand { get; }
    public ReactiveCommand<Unit, Unit> ImportTextureCommand { get; }

    private readonly IFilePickerService _filePickerService;
    private readonly AssetEditorViewModel _parentViewModel;
    private Task<Bitmap>? _image;
    private Uri? _imagePath;
    private double _imageWidth;
    private double _imageHeight;
    private string textureName = string.Empty;

    public TextureEditorViewModel(IFilePickerService filePickerService, AssetEditorViewModel parentViewModel)
    {
        _filePickerService = filePickerService;
        _parentViewModel = parentViewModel;
        OpenImageCommand = ReactiveCommand.CreateFromTask(OpenImage);
        CloseImageCommand = ReactiveCommand.CreateFromTask(CloseImage);
        ImportTextureCommand = ReactiveCommand.CreateFromTask(ImportTexture);
    }

    public TextureEditorViewModel() // Designer constructor
    {
        _filePickerService = new FilePickerService();
        _parentViewModel = new AssetEditorViewModel(_filePickerService);
        OpenImageCommand = ReactiveCommand.CreateFromTask(OpenImage);
        CloseImageCommand = ReactiveCommand.CreateFromTask(CloseImage);
        ImportTextureCommand = ReactiveCommand.CreateFromTask(ImportTexture);
        foreach (var texture in new[]
        {
            new Texture { Name = "Texture 1", Path = "path/to/texture1.png", Bitmap = Task.FromResult(new Bitmap("GameEngine.Demo/assets/images/jeep.png")) },
            new Texture { Name = "Texture 2", Path = "path/to/texture2.png", Bitmap = Task.FromResult(new Bitmap("GameEngine.Demo/assets/images/grenade.png")) }
        })
            Textures.Add(texture);
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

    public ObservableCollection<Texture> Textures => _parentViewModel.SharedTextures;

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
            Path = _imagePath?.LocalPath ?? string.Empty,
            Bitmap = Image
        };
        Textures.Add(texture);
        await Reset();
    }

    public async Task OpenImage()
    {
        var filePath = await _filePickerService.PromptForImagePath();
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
}
