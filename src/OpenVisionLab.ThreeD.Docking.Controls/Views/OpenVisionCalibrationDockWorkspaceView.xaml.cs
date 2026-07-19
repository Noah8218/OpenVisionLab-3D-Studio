using System.Windows;
using System.Windows.Controls;

namespace OpenVisionLab.ThreeD.Docking.Controls;

public sealed partial class OpenVisionCalibrationDockWorkspaceView : UserControl
{
    public static readonly DependencyProperty ExplorerContentProperty =
        DependencyProperty.Register(
            nameof(ExplorerContent),
            typeof(object),
            typeof(OpenVisionCalibrationDockWorkspaceView),
            new PropertyMetadata(null));

    public static readonly DependencyProperty WorkspaceContentProperty =
        DependencyProperty.Register(
            nameof(WorkspaceContent),
            typeof(object),
            typeof(OpenVisionCalibrationDockWorkspaceView),
            new PropertyMetadata(null));

    public static readonly DependencyProperty InspectorContentProperty =
        DependencyProperty.Register(
            nameof(InspectorContent),
            typeof(object),
            typeof(OpenVisionCalibrationDockWorkspaceView),
            new PropertyMetadata(null));

    public static readonly DependencyProperty EvidenceContentProperty =
        DependencyProperty.Register(
            nameof(EvidenceContent),
            typeof(object),
            typeof(OpenVisionCalibrationDockWorkspaceView),
            new PropertyMetadata(null));

    public OpenVisionCalibrationDockWorkspaceView()
    {
        InitializeComponent();
        Loaded += (_, _) => ApplyInitialDockSizes();
    }

    public object? ExplorerContent
    {
        get => GetValue(ExplorerContentProperty);
        set => SetValue(ExplorerContentProperty, value);
    }

    public object? WorkspaceContent
    {
        get => GetValue(WorkspaceContentProperty);
        set => SetValue(WorkspaceContentProperty, value);
    }

    public object? InspectorContent
    {
        get => GetValue(InspectorContentProperty);
        set => SetValue(InspectorContentProperty, value);
    }

    public object? EvidenceContent
    {
        get => GetValue(EvidenceContentProperty);
        set => SetValue(EvidenceContentProperty, value);
    }

    public IReadOnlyList<DockingPaneContract> GetDockingPaneContracts() =>
    [
        ToContract(explorerAnchorable),
        new DockingPaneContract(
            workspaceDocument.ContentId,
            workspaceDocument.Title?.ToString() ?? string.Empty,
            workspaceDocument.CanFloat,
            workspaceDocument.CanClose,
            null,
            workspaceDocument.Content is not null),
        ToContract(inspectorAnchorable),
        ToContract(evidenceAnchorable),
    ];

    private static DockingPaneContract ToContract(AvalonDock.Layout.LayoutAnchorable pane) =>
        new(pane.ContentId, pane.Title?.ToString() ?? string.Empty, pane.CanFloat, pane.CanClose, pane.CanHide, pane.Content is not null);

    private void ApplyInitialDockSizes()
    {
        workbenchPane.DockHeight = new GridLength(1, GridUnitType.Star);
        evidencePane.DockHeight = new GridLength(175);
        explorerPane.DockWidth = new GridLength(245);
        inspectorPane.DockWidth = new GridLength(320);
    }
}
