using System.Windows;
using System.Windows.Controls;

namespace OpenVisionLab.ThreeD.Shell.Views.Workbench;

public partial class ValidationSamplesView : UserControl
{
    public static readonly DependencyProperty DockPaneCommandsProperty = DependencyProperty.Register(
        nameof(DockPaneCommands), typeof(object), typeof(ValidationSamplesView), new PropertyMetadata(null));

    public object? DockPaneCommands
    {
        get => GetValue(DockPaneCommandsProperty);
        set => SetValue(DockPaneCommandsProperty, value);
    }

    public ValidationSamplesView()
    {
        InitializeComponent();
    }
}
