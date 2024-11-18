using Avalonia.Media.Imaging;
using System.Threading.Tasks;
using System;

namespace GameEngine.Editor.ViewModels;

public class AssetEditorViewModel : ViewModelBase
{
    public string Greeting => "Welcome to Avalonia!";
    public Task<Bitmap?> ImageFromWebsite { get; } = Task.FromResult(new Bitmap(@"C:\Users\Josh\Downloads\back.png"));

}
