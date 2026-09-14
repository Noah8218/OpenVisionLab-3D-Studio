using System.Windows;
using System.Windows.Controls;
using OpenVisionLab.ThreeD.Viewer.Hosting;

namespace OpenVisionLab.ThreeD.Shell.Views.Workspace;

public partial class ThicknessTaskWorkspaceView : UserControl
{
    public static readonly DependencyProperty ViewerContentProperty =
        DependencyProperty.Register(
            nameof(ViewerContent),
            typeof(object),
            typeof(ThicknessTaskWorkspaceView),
            new PropertyMetadata(null));

    public static readonly DependencyProperty ViewerEditorProperty =
        DependencyProperty.Register(
            nameof(ViewerEditor),
            typeof(ViewerHostEditorSurface),
            typeof(ThicknessTaskWorkspaceView),
            new PropertyMetadata(null));

    public ThicknessTaskWorkspaceView()
    {
        InitializeComponent();
    }

    public object? ViewerContent
    {
        get => GetValue(ViewerContentProperty);
        set => SetValue(ViewerContentProperty, value);
    }

    public ViewerHostEditorSurface? ViewerEditor
    {
        get => (ViewerHostEditorSurface?)GetValue(ViewerEditorProperty);
        set => SetValue(ViewerEditorProperty, value);
    }
}
