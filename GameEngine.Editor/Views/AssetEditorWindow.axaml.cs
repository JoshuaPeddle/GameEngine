using Avalonia.Controls;
using GameEngine.Editor.Services;
using GameEngine.Editor.ViewModels;

namespace GameEngine.Editor;

public partial class AssetEditorWindow : Window
{
    public AssetEditorWindow()
    {
        InitializeComponent();
        DataContext = new AssetEditorViewModel(new FilePickerService(this));
    }
}
