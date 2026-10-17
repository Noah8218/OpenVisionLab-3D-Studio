using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace OpenVisionLab.ThreeD.Shell.Views.Workbench;

public partial class InspectionQueueWorkspaceView : UserControl
{
    public static readonly DependencyProperty DockPaneCommandsProperty = DependencyProperty.Register(
        nameof(DockPaneCommands), typeof(object), typeof(InspectionQueueWorkspaceView), new PropertyMetadata(null));

    public static readonly DependencyProperty ResultsCommandProperty = DependencyProperty.Register(
        nameof(ResultsCommand), typeof(ICommand), typeof(InspectionQueueWorkspaceView), new PropertyMetadata(null));

    public ICommand? ResultsCommand
    {
        get => (ICommand?)GetValue(ResultsCommandProperty);
        set => SetValue(ResultsCommandProperty, value);
    }

    public object? DockPaneCommands
    {
        get => GetValue(DockPaneCommandsProperty);
        set => SetValue(DockPaneCommandsProperty, value);
    }

    public InspectionQueueWorkspaceView() => InitializeComponent();

    private void OnDistributionSizeChanged(object sender, SizeChangedEventArgs args)
    {
        if (sender is FrameworkElement distribution) distribution.Clip = new RectangleGeometry(new Rect(args.NewSize), 3, 3);
    }
}
