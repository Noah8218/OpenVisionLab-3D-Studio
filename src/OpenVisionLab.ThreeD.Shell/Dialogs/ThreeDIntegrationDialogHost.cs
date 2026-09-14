using System.Windows;
using Microsoft.Win32;

namespace OpenVisionLab.ThreeD.Shell.Dialogs;

/// <summary>
/// Owns the WPF folder picker used by the Integration Exchange workspace.
/// The ViewModel receives only the selected path and remains UI independent.
/// </summary>
internal interface IThreeDIntegrationDialogHost
{
    bool TrySelectExchangeRoot(string? initialDirectory, string title, out string path);
}

internal sealed class ThreeDIntegrationDialogHost : IThreeDIntegrationDialogHost
{
    private readonly Func<Window> getOwner;

    public ThreeDIntegrationDialogHost(Func<Window> getOwner)
    {
        this.getOwner = getOwner ?? throw new ArgumentNullException(nameof(getOwner));
    }

    public bool TrySelectExchangeRoot(string? initialDirectory, string title, out string path)
    {
        var dialog = new OpenFolderDialog
        {
            Title = title,
            InitialDirectory = initialDirectory,
            Multiselect = false
        };
        if (dialog.ShowDialog(getOwner()) != true)
        {
            path = string.Empty;
            return false;
        }

        path = dialog.FolderName;
        return !string.IsNullOrWhiteSpace(path);
    }
}
