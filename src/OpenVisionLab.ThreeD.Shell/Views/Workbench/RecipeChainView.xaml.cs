using System.Windows;
using System.Windows.Controls;

namespace OpenVisionLab.ThreeD.Shell.Views.Workbench;

public partial class RecipeChainView : UserControl
{
    public static readonly DependencyProperty IsTeachingModeProperty =
        DependencyProperty.Register(
            nameof(IsTeachingMode),
            typeof(bool),
            typeof(RecipeChainView),
            new PropertyMetadata(false));

    public RecipeChainView()
    {
        InitializeComponent();
    }

    public bool IsTeachingMode
    {
        get => (bool)GetValue(IsTeachingModeProperty);
        set => SetValue(IsTeachingModeProperty, value);
    }

    public bool HasVisibleFirstActionGuide =>
        AuthoringFirstActionGuide.Visibility == Visibility.Visible;

    public int VisibleFirstActionCount =>
        new[]
        {
            InputFirstActionText,
            SelectToolFirstActionText,
            RoiPreviewFirstActionText,
        }.Count(text => text.Visibility == Visibility.Visible);

    public bool HasSingleVisibleFirstAction =>
        HasVisibleFirstActionGuide
        && VisibleFirstActionCount == 1;
}
