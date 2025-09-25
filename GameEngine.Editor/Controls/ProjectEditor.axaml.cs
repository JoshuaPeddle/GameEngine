using Avalonia.Controls;
using Avalonia.Input;
using GameEngine.Editor.ViewModels;
using ReactiveUI;
using System;

namespace GameEngine.Editor.Controls;

public partial class ProjectEditor : UserControl
{
    public ProjectEditor()
    {
        InitializeComponent();
    }

    // Double-click (DoubleTapped) on a recent project ListBox item opens it.
    private void RecentProjects_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is not ProjectEditorViewModel vm)
            return;

        // Ensure a project is selected
        if (string.IsNullOrWhiteSpace(vm.SelectedRecentProject))
            return;

        // Execute the existing command
        vm.OpenSelectedRecentProjectCommand.Execute().Subscribe();
        e.Handled = true;
    }
}