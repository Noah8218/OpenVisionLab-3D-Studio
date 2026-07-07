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

    public static readonly DependencyProperty DataLayersContentProperty =
        DependencyProperty.Register(
            nameof(DataLayersContent),
            typeof(object),
            typeof(OpenVisionDockWorkspaceView),
            new PropertyMetadata(null));

    public static readonly DependencyProperty ToolInspectorContentProperty =
        DependencyProperty.Register(
            nameof(ToolInspectorContent),
            typeof(object),
            typeof(OpenVisionDockWorkspaceView),
            new PropertyMetadata(null));

    public static readonly DependencyProperty EvidenceContentProperty =
        DependencyProperty.Register(
            nameof(EvidenceContent),
            typeof(object),
            typeof(OpenVisionDockWorkspaceView),
            new PropertyMetadata(null));

    public static readonly DependencyProperty LinkedViewContentProperty =
        DependencyProperty.Register(
            nameof(LinkedViewContent),
            typeof(object),
            typeof(OpenVisionDockWorkspaceView),
            new PropertyMetadata(null));

    public OpenVisionDockWorkspaceView()
    {
        InitializeComponent();
        Loaded += (_, _) => ApplyInitialDockSizes();
    }

    public object? ViewerContent
    {
        get => GetValue(ViewerContentProperty);
        set => SetValue(ViewerContentProperty, value);
    }

    public object? DataLayersContent
    {
        get => GetValue(DataLayersContentProperty);
        set => SetValue(DataLayersContentProperty, value);
    }

    public object? ToolInspectorContent
    {
        get => GetValue(ToolInspectorContentProperty);
        set => SetValue(ToolInspectorContentProperty, value);
    }

    public object? EvidenceContent
    {
        get => GetValue(EvidenceContentProperty);
        set => SetValue(EvidenceContentProperty, value);
    }

    public object? LinkedViewContent
    {
        get => GetValue(LinkedViewContentProperty);
        set => SetValue(LinkedViewContentProperty, value);
    }

    private void ApplyInitialDockSizes()
    {
        workbenchPane.DockHeight = new GridLength(4.1, GridUnitType.Star);
        evidencePane.DockHeight = new GridLength(1.9, GridUnitType.Star);
        linkedViewPane.DockHeight = new GridLength(0.65, GridUnitType.Star);
        dataLayersPane.DockWidth = new GridLength(260);
        toolInspectorPane.DockWidth = new GridLength(320);
    }
}
