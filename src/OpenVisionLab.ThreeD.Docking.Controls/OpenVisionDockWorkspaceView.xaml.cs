using System.Windows;
using System.Windows.Controls;

namespace OpenVisionLab.ThreeD.Docking.Controls;

public sealed partial class OpenVisionDockWorkspaceView : UserControl
{
    public static readonly DependencyProperty ViewerContentProperty =
        DependencyProperty.Register(
            nameof(ViewerContent),
            typeof(object),
            typeof(OpenVisionDockWorkspaceView),
            new PropertyMetadata(null));

    public static readonly DependencyProperty RecipeComparisonContentProperty =
        DependencyProperty.Register(
            nameof(RecipeComparisonContent),
            typeof(object),
            typeof(OpenVisionDockWorkspaceView),
            new PropertyMetadata(null));

    public OpenVisionDockWorkspaceView()
    {
        InitializeComponent();
    }

    public object? ViewerContent
    {
        get => GetValue(ViewerContentProperty);
        set => SetValue(ViewerContentProperty, value);
    }

    public object? RecipeComparisonContent
    {
        get => GetValue(RecipeComparisonContentProperty);
        set => SetValue(RecipeComparisonContentProperty, value);
    }
}
