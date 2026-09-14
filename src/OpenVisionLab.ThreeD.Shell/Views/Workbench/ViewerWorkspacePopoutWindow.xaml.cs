using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace OpenVisionLab.ThreeD.Shell.Views.Workbench;

public partial class ViewerWorkspacePopoutWindow : Window
{
    public static readonly DependencyProperty DismissedCommandProperty =
        DependencyProperty.Register(
            nameof(DismissedCommand),
            typeof(ICommand),
            typeof(ViewerWorkspacePopoutWindow),
            new PropertyMetadata(null));

    private bool allowClose;

    public ViewerWorkspacePopoutWindow()
    {
        InitializeComponent();
        SetBinding(
            DismissedCommandProperty,
            new Binding("SetSingleViewerLayoutCommand"));
        Closing += OnClosing;
    }

    public event EventHandler? Dismissed;

    public ICommand? DismissedCommand
    {
        get => (ICommand?)GetValue(DismissedCommandProperty);
        set => SetValue(DismissedCommandProperty, value);
    }

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
}
