using System.Windows;
using System.Windows.Controls;

namespace OpenVisionLab.ThreeD.Docking.Controls;

public sealed partial class OpenVisionCalibrationDockWorkspaceView : UserControl
{
    public static readonly DependencyProperty ExplorerTitleProperty =
        DependencyProperty.Register(
            nameof(ExplorerTitle),
            typeof(string),
            typeof(OpenVisionCalibrationDockWorkspaceView),
            new PropertyMetadata("Calibration Explorer", OnExplorerTitleChanged));

    public static readonly DependencyProperty WorkspaceTitleProperty =
        DependencyProperty.Register(
            nameof(WorkspaceTitle),
            typeof(string),
            typeof(OpenVisionCalibrationDockWorkspaceView),
            new PropertyMetadata("Calibration Workspace", OnWorkspaceTitleChanged));

    public static readonly DependencyProperty InspectorTitleProperty =
        DependencyProperty.Register(
            nameof(InspectorTitle),
            typeof(string),
            typeof(OpenVisionCalibrationDockWorkspaceView),
            new PropertyMetadata("Calibration Inspector", OnInspectorTitleChanged));

    public static readonly DependencyProperty EvidenceTitleProperty =
        DependencyProperty.Register(
            nameof(EvidenceTitle),
            typeof(string),
            typeof(OpenVisionCalibrationDockWorkspaceView),
            new PropertyMetadata("Calibration Evidence", OnEvidenceTitleChanged));

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
        ApplyDockTitles();
        Loaded += (_, _) => ApplyInitialDockSizes();
    }

    public string ExplorerTitle
    {
        get => (string)GetValue(ExplorerTitleProperty);
        set => SetValue(ExplorerTitleProperty, value);
    }

    public string WorkspaceTitle
    {
        get => (string)GetValue(WorkspaceTitleProperty);
        set => SetValue(WorkspaceTitleProperty, value);
    }

    public string InspectorTitle
    {
        get => (string)GetValue(InspectorTitleProperty);
        set => SetValue(InspectorTitleProperty, value);
    }

    public string EvidenceTitle
    {
        get => (string)GetValue(EvidenceTitleProperty);
        set => SetValue(EvidenceTitleProperty, value);
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

    private static void OnExplorerTitleChanged(DependencyObject owner, DependencyPropertyChangedEventArgs args)
    {
        if (owner is OpenVisionCalibrationDockWorkspaceView view && view.explorerAnchorable is not null)
        {
            view.explorerAnchorable.Title = args.NewValue as string ?? string.Empty;
        }
    }

    private static void OnWorkspaceTitleChanged(DependencyObject owner, DependencyPropertyChangedEventArgs args)
    {
        if (owner is OpenVisionCalibrationDockWorkspaceView view && view.workspaceDocument is not null)
        {
            view.workspaceDocument.Title = args.NewValue as string ?? string.Empty;
        }
    }

    private static void OnInspectorTitleChanged(DependencyObject owner, DependencyPropertyChangedEventArgs args)
    {
        if (owner is OpenVisionCalibrationDockWorkspaceView view && view.inspectorAnchorable is not null)
        {
            view.inspectorAnchorable.Title = args.NewValue as string ?? string.Empty;
        }
    }

    private static void OnEvidenceTitleChanged(DependencyObject owner, DependencyPropertyChangedEventArgs args)
    {
        if (owner is OpenVisionCalibrationDockWorkspaceView view && view.evidenceAnchorable is not null)
        {
            view.evidenceAnchorable.Title = args.NewValue as string ?? string.Empty;
        }
    }

    private void ApplyDockTitles()
    {
        explorerAnchorable.Title = ExplorerTitle;
        workspaceDocument.Title = WorkspaceTitle;
        inspectorAnchorable.Title = InspectorTitle;
        evidenceAnchorable.Title = EvidenceTitle;
    }

    private void ApplyInitialDockSizes()
    {
        workbenchPane.DockHeight = new GridLength(1, GridUnitType.Star);
        evidencePane.DockHeight = new GridLength(175);
        explorerPane.DockWidth = new GridLength(245);
        inspectorPane.DockWidth = new GridLength(320);
    }
}
