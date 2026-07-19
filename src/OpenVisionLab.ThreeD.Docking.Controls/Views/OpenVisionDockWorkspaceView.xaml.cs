using System.Windows;
using System.Windows.Controls;

namespace OpenVisionLab.ThreeD.Docking.Controls;

public sealed partial class OpenVisionDockWorkspaceView : UserControl
{
    private bool bottomPaneDetachedForFocus;

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

    public static readonly DependencyProperty ViewerTitleProperty =
        DependencyProperty.Register(
            nameof(ViewerTitle),
            typeof(string),
            typeof(OpenVisionDockWorkspaceView),
            new PropertyMetadata("3D Inspection View", OnViewerTitleChanged));

    public static readonly DependencyProperty DataLayersTitleProperty =
        DependencyProperty.Register(
            nameof(DataLayersTitle),
            typeof(string),
            typeof(OpenVisionDockWorkspaceView),
            new PropertyMetadata("Data & Layers", OnDataLayersTitleChanged));

    public static readonly DependencyProperty ToolInspectorContentProperty =
        DependencyProperty.Register(
            nameof(ToolInspectorContent),
            typeof(object),
            typeof(OpenVisionDockWorkspaceView),
            new PropertyMetadata(null));

    public static readonly DependencyProperty ToolInspectorTitleProperty =
        DependencyProperty.Register(
            nameof(ToolInspectorTitle),
            typeof(string),
            typeof(OpenVisionDockWorkspaceView),
            new PropertyMetadata("Tool / Inspector", OnToolInspectorTitleChanged));

    public static readonly DependencyProperty EvidenceContentProperty =
        DependencyProperty.Register(
            nameof(EvidenceContent),
            typeof(object),
            typeof(OpenVisionDockWorkspaceView),
            new PropertyMetadata(null));

    public static readonly DependencyProperty EvidenceTitleProperty =
        DependencyProperty.Register(
            nameof(EvidenceTitle),
            typeof(string),
            typeof(OpenVisionDockWorkspaceView),
            new PropertyMetadata("Evidence Workbench", OnEvidenceTitleChanged));

    public static readonly DependencyProperty LinkedViewContentProperty =
        DependencyProperty.Register(
            nameof(LinkedViewContent),
            typeof(object),
            typeof(OpenVisionDockWorkspaceView),
            new PropertyMetadata(null));

    public static readonly DependencyProperty LinkedViewTitleProperty =
        DependencyProperty.Register(
            nameof(LinkedViewTitle),
            typeof(string),
            typeof(OpenVisionDockWorkspaceView),
            new PropertyMetadata("Linked View", OnLinkedViewTitleChanged));

    public static readonly DependencyProperty ProfileContentProperty =
        DependencyProperty.Register(
            nameof(ProfileContent),
            typeof(object),
            typeof(OpenVisionDockWorkspaceView),
            new PropertyMetadata(null));

    public static readonly DependencyProperty ProfileTitleProperty =
        DependencyProperty.Register(
            nameof(ProfileTitle),
            typeof(string),
            typeof(OpenVisionDockWorkspaceView),
            new PropertyMetadata("Height Profile", OnProfileTitleChanged));

    public static readonly DependencyProperty FitDiagnosticsContentProperty =
        DependencyProperty.Register(
            nameof(FitDiagnosticsContent),
            typeof(object),
            typeof(OpenVisionDockWorkspaceView),
            new PropertyMetadata(null));

    public static readonly DependencyProperty FitDiagnosticsTitleProperty =
        DependencyProperty.Register(
            nameof(FitDiagnosticsTitle),
            typeof(string),
            typeof(OpenVisionDockWorkspaceView),
            new PropertyMetadata("Fit Diagnostics", OnFitDiagnosticsTitleChanged));

    public static readonly DependencyProperty IsBottomPaneExpandedProperty =
        DependencyProperty.Register(
            nameof(IsBottomPaneExpanded),
            typeof(bool),
            typeof(OpenVisionDockWorkspaceView),
            new PropertyMetadata(true, OnIsBottomPaneExpandedChanged));

    public OpenVisionDockWorkspaceView()
    {
        InitializeComponent();
        ApplyDockTitles();
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

    public string ViewerTitle
    {
        get => (string)GetValue(ViewerTitleProperty);
        set => SetValue(ViewerTitleProperty, value);
    }

    public string DataLayersTitle
    {
        get => (string)GetValue(DataLayersTitleProperty);
        set => SetValue(DataLayersTitleProperty, value);
    }

    public object? ToolInspectorContent
    {
        get => GetValue(ToolInspectorContentProperty);
        set => SetValue(ToolInspectorContentProperty, value);
    }

    public string ToolInspectorTitle
    {
        get => (string)GetValue(ToolInspectorTitleProperty);
        set => SetValue(ToolInspectorTitleProperty, value);
    }

    public object? EvidenceContent
    {
        get => GetValue(EvidenceContentProperty);
        set => SetValue(EvidenceContentProperty, value);
    }

    public string EvidenceTitle
    {
        get => (string)GetValue(EvidenceTitleProperty);
        set => SetValue(EvidenceTitleProperty, value);
    }

    public object? LinkedViewContent
    {
        get => GetValue(LinkedViewContentProperty);
        set => SetValue(LinkedViewContentProperty, value);
    }

    public string LinkedViewTitle
    {
        get => (string)GetValue(LinkedViewTitleProperty);
        set => SetValue(LinkedViewTitleProperty, value);
    }

    public object? ProfileContent
    {
        get => GetValue(ProfileContentProperty);
        set => SetValue(ProfileContentProperty, value);
    }

    public string ProfileTitle
    {
        get => (string)GetValue(ProfileTitleProperty);
        set => SetValue(ProfileTitleProperty, value);
    }

    public object? FitDiagnosticsContent
    {
        get => GetValue(FitDiagnosticsContentProperty);
        set => SetValue(FitDiagnosticsContentProperty, value);
    }

    public string FitDiagnosticsTitle
    {
        get => (string)GetValue(FitDiagnosticsTitleProperty);
        set => SetValue(FitDiagnosticsTitleProperty, value);
    }

    public bool IsBottomPaneExpanded
    {
        get => (bool)GetValue(IsBottomPaneExpandedProperty);
        set => SetValue(IsBottomPaneExpandedProperty, value);
    }

    public IReadOnlyList<DockingPaneContract> GetDockingPaneContracts() =>
    [
        ToContract(dataLayersAnchorable),
        ToContract(viewerAnchorable),
        ToContract(toolInspectorAnchorable),
        ToContract(evidenceAnchorable),
        ToContract(linkedViewAnchorable),
        ToContract(profileAnchorable),
        ToContract(fitDiagnosticsAnchorable),
    ];

    public void ActivateProfilePane()
    {
        if (!IsBottomPaneExpanded)
        {
            IsBottomPaneExpanded = true;
        }

        profileAnchorable.IsSelected = true;
        profileAnchorable.IsActive = true;
    }

    public bool IsProfilePaneSelected => profileAnchorable.IsSelected && profileAnchorable.IsActive;

    public void ActivateFitDiagnosticsPane()
    {
        if (!IsBottomPaneExpanded) IsBottomPaneExpanded = true;
        fitDiagnosticsAnchorable.IsSelected = true;
        fitDiagnosticsAnchorable.IsActive = true;
    }

    public bool IsFitDiagnosticsPaneSelected => fitDiagnosticsAnchorable.IsSelected && fitDiagnosticsAnchorable.IsActive;

    public DockingFloatDockResult VerifyFirstPaneFloatDockRoundTrip()
    {
        var layout = workspaceDockingManager.Layout;
        var initialParent = dataLayersAnchorable.Parent;
        var before = layout.FloatingWindows.Count;

        try
        {
            dataLayersAnchorable.Float();
            var afterFloat = layout.FloatingWindows.Count;
            var floated = afterFloat == before + 1;

            dataLayersAnchorable.Dock();
            var afterDock = layout.FloatingWindows.Count;
            var redocked = afterDock == before
                && dataLayersAnchorable.Parent is AvalonDock.Layout.LayoutAnchorablePane;

            return new DockingFloatDockResult(
                floated,
                redocked,
                before,
                afterFloat,
                afterDock,
                floated && redocked ? "Float/Dock model transition passed." : "Float/Dock model transition did not restore the initial layout.");
        }
        catch (Exception exception)
        {
            if (!ReferenceEquals(dataLayersAnchorable.Parent, initialParent))
            {
                dataLayersAnchorable.Dock();
            }

            return new DockingFloatDockResult(
                false,
                false,
                before,
                layout.FloatingWindows.Count,
                layout.FloatingWindows.Count,
                exception.GetType().Name + ": " + exception.Message);
        }
    }

    public bool IsBottomPaneAttached =>
        ReferenceEquals(evidencePane.Parent, workspaceRootPanel);

    private static DockingPaneContract ToContract(AvalonDock.Layout.LayoutAnchorable pane) =>
        new(pane.ContentId, pane.Title?.ToString() ?? string.Empty, pane.CanFloat, pane.CanClose, pane.CanHide, pane.Content is not null);

    private static void OnDataLayersTitleChanged(DependencyObject owner, DependencyPropertyChangedEventArgs args)
    {
        if (owner is OpenVisionDockWorkspaceView view && view.dataLayersAnchorable is not null)
        {
            view.dataLayersAnchorable.Title = args.NewValue as string ?? string.Empty;
        }
    }

    private static void OnViewerTitleChanged(DependencyObject owner, DependencyPropertyChangedEventArgs args)
    {
        if (owner is OpenVisionDockWorkspaceView view && view.viewerAnchorable is not null)
        {
            view.viewerAnchorable.Title = args.NewValue as string ?? string.Empty;
        }
    }

    private static void OnToolInspectorTitleChanged(DependencyObject owner, DependencyPropertyChangedEventArgs args)
    {
        if (owner is OpenVisionDockWorkspaceView view && view.toolInspectorAnchorable is not null)
        {
            view.toolInspectorAnchorable.Title = args.NewValue as string ?? string.Empty;
        }
    }

    private static void OnEvidenceTitleChanged(DependencyObject owner, DependencyPropertyChangedEventArgs args)
    {
        if (owner is OpenVisionDockWorkspaceView view && view.evidenceAnchorable is not null)
        {
            view.evidenceAnchorable.Title = args.NewValue as string ?? string.Empty;
        }
    }

    private static void OnLinkedViewTitleChanged(DependencyObject owner, DependencyPropertyChangedEventArgs args)
    {
        if (owner is OpenVisionDockWorkspaceView view && view.linkedViewAnchorable is not null)
        {
            view.linkedViewAnchorable.Title = args.NewValue as string ?? string.Empty;
        }
    }

    private static void OnProfileTitleChanged(DependencyObject owner, DependencyPropertyChangedEventArgs args)
    {
        if (owner is OpenVisionDockWorkspaceView view && view.profileAnchorable is not null)
        {
            view.profileAnchorable.Title = args.NewValue as string ?? string.Empty;
        }
    }

    private static void OnFitDiagnosticsTitleChanged(DependencyObject owner, DependencyPropertyChangedEventArgs args)
    {
        if (owner is OpenVisionDockWorkspaceView view && view.fitDiagnosticsAnchorable is not null)
        {
            view.fitDiagnosticsAnchorable.Title = args.NewValue as string ?? string.Empty;
        }
    }

    private static void OnIsBottomPaneExpandedChanged(DependencyObject owner, DependencyPropertyChangedEventArgs args)
    {
        if (owner is OpenVisionDockWorkspaceView view && view.evidencePane is not null)
        {
            view.ApplyBottomPaneHeight();
        }
    }

    private void ApplyDockTitles()
    {
        dataLayersAnchorable.Title = DataLayersTitle;
        viewerAnchorable.Title = ViewerTitle;
        toolInspectorAnchorable.Title = ToolInspectorTitle;
        evidenceAnchorable.Title = EvidenceTitle;
        linkedViewAnchorable.Title = LinkedViewTitle;
        profileAnchorable.Title = ProfileTitle;
        fitDiagnosticsAnchorable.Title = FitDiagnosticsTitle;
    }

    private void ApplyInitialDockSizes()
    {
        workbenchPane.DockHeight = new GridLength(2, GridUnitType.Star);
        ApplyBottomPaneHeight();
        dataLayersPane.DockWidth = new GridLength(0.9, GridUnitType.Star);
        primaryPane.DockWidth = new GridLength(1.8, GridUnitType.Star);
        toolInspectorPane.DockWidth = new GridLength(1.1, GridUnitType.Star);
    }

    private void ApplyBottomPaneHeight() =>
        ApplyBottomPanePresentation();

    private void ApplyBottomPanePresentation()
    {
        if (!IsBottomPaneExpanded)
        {
            if (ReferenceEquals(evidencePane.Parent, workspaceRootPanel))
            {
                workspaceRootPanel.Children.Remove(evidencePane);
                bottomPaneDetachedForFocus = true;
            }

            return;
        }

        if (bottomPaneDetachedForFocus && evidencePane.Parent is null)
        {
            workspaceRootPanel.Children.Add(evidencePane);
            bottomPaneDetachedForFocus = false;
        }

        evidencePane.DockHeight = new GridLength(1, GridUnitType.Star);
    }
}
