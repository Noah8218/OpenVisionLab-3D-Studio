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
}
