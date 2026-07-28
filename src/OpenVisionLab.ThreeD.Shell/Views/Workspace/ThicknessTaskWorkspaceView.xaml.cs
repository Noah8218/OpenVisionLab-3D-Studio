using System.Windows;
using System.Windows.Controls;

namespace OpenVisionLab.ThreeD.Shell.Views.Workspace;

public partial class ThicknessTaskWorkspaceView : UserControl
{
    public static readonly DependencyProperty ViewerContentProperty =
        DependencyProperty.Register(
            nameof(ViewerContent),
            typeof(object),
            typeof(ThicknessTaskWorkspaceView),
            new PropertyMetadata(null));

    public static readonly DependencyProperty ViewerViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewerViewModel),
            typeof(object),
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

    public object? ViewerViewModel
    {
        get => GetValue(ViewerViewModelProperty);
        set => SetValue(ViewerViewModelProperty, value);
    }
}
