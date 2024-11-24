using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using GameEngine.Editor.ViewModels;
using System.Reactive;

namespace GameEngine.Editor.Controls
{
    public partial class LevelEditor : UserControl
    {
        public LevelEditor()
        {
            InitializeComponent();

            // Attach event handler for tile clicks
            TileItemsControl.AddHandler(PointerPressedEvent, OnTilePointerPressed, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);

            // Attach event handler for mouse wheel zooming
            MainScrollViewer.AddHandler(PointerWheelChangedEvent, OnPointerWheelChanged, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
        }

        private void OnTilePointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.Source is Border border && border.DataContext is TileViewModel tile)
            {
                tile.PlaceTileCommand.Execute(Unit.Default);
                e.Handled = true;
            }
        }

        private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            if (DataContext is LevelEditorViewModel viewModel)
            {
                viewModel.ZoomLevel *= e.Delta.Y > 0 ? 1.1 : 0.9;
                e.Handled = true;
            }
        }
    }
}
