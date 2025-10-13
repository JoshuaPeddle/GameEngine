using Avalonia.Controls;
using Avalonia.Input;
using GameEngine.Editor.ViewModels;
using System;

namespace GameEngine.Editor.Controls
{
    public partial class LevelEditor : UserControl
    {
        private double? _lastRightWidth; // remembers width before collapse
        private const double CollapseThreshold = 40; // px threshold after drag

        public LevelEditor()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private async void OnDataContextChanged(object? sender, EventArgs e)
        {
            if (DataContext is LevelEditorViewModel vm)
            {
                await vm.EnsureScenesLoadedAsync();
                vm.RefreshLevelFileOptions();
            }
        }

        private void ViewportSplitter_DragCompleted(object? sender, VectorEventArgs e)
        {
            // If user dragged so the right column became very small -> collapse
            var rightCol = WorkspaceGrid.ColumnDefinitions[2];
            if (rightCol.Width.Value <= CollapseThreshold && rightCol.Width.IsAbsolute)
                CollapseRightPanel();
        }

        private void ViewportSplitter_DoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
        {
            var rightCol = WorkspaceGrid.ColumnDefinitions[2];
            if (IsCollapsed(rightCol))
                RestoreRightPanel();
            else
                CollapseRightPanel();
        }

        private bool IsCollapsed(ColumnDefinition col)
            => col.Width.IsAbsolute && col.Width.Value <= 1;

        private void CollapseRightPanel()
        {
            var rightCol = WorkspaceGrid.ColumnDefinitions[2];
            // Remember previous width if not already collapsed
            if (!IsCollapsed(rightCol))
                _lastRightWidth = rightCol.Width.IsAbsolute ? rightCol.Width.Value : 300;

            rightCol.Width = new GridLength(0, GridUnitType.Pixel);
        }

        private void RestoreRightPanel()
        {
            var rightCol = WorkspaceGrid.ColumnDefinitions[2];
            var restoreWidth = _lastRightWidth is > 60 ? _lastRightWidth.Value : 300;
            rightCol.Width = new GridLength(restoreWidth, GridUnitType.Pixel);
        }
    }
}
