using Avalonia.Controls;
using GameEngine.Editor.ViewModels;
using GameEngine.Editor.Services;

namespace GameEngine.Editor;

public partial class AssetEditorWindow : Window
{
    public AssetEditorWindow()
    {
        InitializeComponent();
        DataContext = new AssetEditorViewModel(new FilePickerService(this));
    }
}
