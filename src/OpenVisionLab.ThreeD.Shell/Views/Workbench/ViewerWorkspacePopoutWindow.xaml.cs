using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Shell.Views.Workbench;

public partial class ViewerWorkspacePopoutWindow : Window
{
    private bool allowClose;

    public ViewerWorkspacePopoutWindow()
    {
        InitializeComponent();
        Closing += OnClosing;
    }

    public event EventHandler? Dismissed;
    public event EventHandler? AuxiliarySlotFocused;

    public void SetViewerContent(object? content, string emptyText)
    {
        ViewerHost.Content = content;
        EmptyText.Text = emptyText;
        EmptyText.Visibility = content is null ? Visibility.Visible : Visibility.Collapsed;
    }

    public void ReleaseViewerContent() => ViewerHost.Content = null;

    public void CloseForOwner()
    {
        allowClose = true;
        Close();
    }

    private void OnClosing(object? sender, CancelEventArgs args)
    {
        if (allowClose)
        {
            return;
        }

        args.Cancel = true;
        Hide();
        Dismissed?.Invoke(this, EventArgs.Empty);
    }

    private void ViewerSurface_PreviewMouseDown(object sender, MouseButtonEventArgs args) =>
        AuxiliarySlotFocused?.Invoke(this, EventArgs.Empty);
}
