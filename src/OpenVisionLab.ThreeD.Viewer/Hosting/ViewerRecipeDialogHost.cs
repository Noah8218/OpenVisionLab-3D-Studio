using System.Windows;
using Microsoft.Win32;
using OpenVisionLab.ThreeD.Viewer.Hosting;

namespace OpenVisionLab.ThreeD.Viewer.Hosting;

/// <summary>
/// Default WPF adapter for recipe path selection. The Viewer control composes
/// this adapter only when an external host does not provide its own policy.
/// </summary>
internal sealed class ViewerRecipeDialogHost : IViewerRecipeDialogHost
{
    private readonly DependencyObject owner;

    public ViewerRecipeDialogHost(DependencyObject owner)
    {
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    public string? SelectRecipeToOpen()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Open 3D Recipe",
            Filter = "OpenVisionLab 3D recipe (*.json)|*.json|All files (*.*)|*.*",
            CheckFileExists = true
        };

        return dialog.ShowDialog(Window.GetWindow(owner)) == true ? dialog.FileName : null;
    }

    public string? SelectRecipeToSave(string defaultFileName)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Save 3D Recipe",
            Filter = "OpenVisionLab 3D recipe (*.json)|*.json|All files (*.*)|*.*",
            FileName = defaultFileName,
            OverwritePrompt = true
        };

        return dialog.ShowDialog(Window.GetWindow(owner)) == true ? dialog.FileName : null;
    }
}
