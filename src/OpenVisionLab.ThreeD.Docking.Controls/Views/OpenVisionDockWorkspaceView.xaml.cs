using System.Windows;
using System.Windows.Controls;

namespace OpenVisionLab.ThreeD.Docking.Controls;

public enum OpenVisionOperatorStage
{
    Legacy,
    Setup,
    Teach,
    Validate,
    Results,
}

public sealed partial class OpenVisionDockWorkspaceView : UserControl
{
    private const double CompactWorkbenchWidth = 1500;
    private const double OutputCompareWorkbenchHeightRatio = 0.82;
    private const double ValidationWorkbenchHeightRatio = 0.85;
    private const double CompactValidationWorkbenchHeightRatio = 0.72;
    private const double CompactValidationHeight = 750;
    private const double StandardWorkbenchHeightRatio = 2;
    private bool bottomPaneDetachedForFocus;
    private bool dataLayersTabbedForCompactLayout;
    private bool evidenceUsesAnalysisHeight;
    private bool compactToolInspectorFocused;
    private OpenVisionOperatorStage operatorStage = OpenVisionOperatorStage.Legacy;

    public static readonly DependencyProperty ViewerContentProperty =
        DependencyProperty.Register(
            nameof(ViewerContent),
            typeof(object),
            typeof(OpenVisionDockWorkspaceView),
            new PropertyMetadata(null));

    public static readonly DependencyProperty ResultsContentProperty =
        DependencyProperty.Register(
            nameof(ResultsContent),
            typeof(object),
            typeof(OpenVisionDockWorkspaceView),
            new PropertyMetadata(null));

    public static readonly DependencyProperty DataLayersContentProperty =
        DependencyProperty.Register(
            nameof(DataLayersContent),
            typeof(object),
            typeof(OpenVisionDockWorkspaceView),
            new PropertyMetadata(null));

    public static readonly DependencyProperty ToolLibraryContentProperty =
        DependencyProperty.Register(
            nameof(ToolLibraryContent),
            typeof(object),
            typeof(OpenVisionDockWorkspaceView),
            new PropertyMetadata(null));

    public static readonly DependencyProperty ViewerTitleProperty =
        DependencyProperty.Register(
            nameof(ViewerTitle),
            typeof(string),
            typeof(OpenVisionDockWorkspaceView),
            new PropertyMetadata("3D Inspection View", OnViewerTitleChanged));

    public static readonly DependencyProperty ResultsTitleProperty =
        DependencyProperty.Register(
            nameof(ResultsTitle),
            typeof(string),
            typeof(OpenVisionDockWorkspaceView),
            new PropertyMetadata("Results", OnResultsTitleChanged));

    public static readonly DependencyProperty DataLayersTitleProperty =
        DependencyProperty.Register(
            nameof(DataLayersTitle),
            typeof(string),
            typeof(OpenVisionDockWorkspaceView),
            new PropertyMetadata("Data & Layers", OnDataLayersTitleChanged));

    public static readonly DependencyProperty ToolLibraryTitleProperty =
        DependencyProperty.Register(
            nameof(ToolLibraryTitle),
            typeof(string),
            typeof(OpenVisionDockWorkspaceView),
            new PropertyMetadata("Tool Library", OnToolLibraryTitleChanged));

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

    public static readonly DependencyProperty OutputCompareContentProperty =
        DependencyProperty.Register(
            nameof(OutputCompareContent),
            typeof(object),
            typeof(OpenVisionDockWorkspaceView),
            new PropertyMetadata(null));

    public static readonly DependencyProperty OutputCompareTitleProperty =
        DependencyProperty.Register(
            nameof(OutputCompareTitle),
            typeof(string),
            typeof(OpenVisionDockWorkspaceView),
            new PropertyMetadata("Output Compare", OnOutputCompareTitleChanged));

    public static readonly DependencyProperty DisplayedOutputsContentProperty =
        DependencyProperty.Register(
            nameof(DisplayedOutputsContent),
            typeof(object),
            typeof(OpenVisionDockWorkspaceView),
            new PropertyMetadata(null));

    public static readonly DependencyProperty DisplayedOutputsTitleProperty =
        DependencyProperty.Register(
            nameof(DisplayedOutputsTitle),
            typeof(string),
            typeof(OpenVisionDockWorkspaceView),
            new PropertyMetadata("Displayed Outputs", OnDisplayedOutputsTitleChanged));

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

    public static readonly DependencyProperty IntersectionEvidenceContentProperty =
        DependencyProperty.Register(
            nameof(IntersectionEvidenceContent),
            typeof(object),
            typeof(OpenVisionDockWorkspaceView),
            new PropertyMetadata(null));

    public static readonly DependencyProperty IntersectionEvidenceTitleProperty =
        DependencyProperty.Register(
            nameof(IntersectionEvidenceTitle),
            typeof(string),
            typeof(OpenVisionDockWorkspaceView),
            new PropertyMetadata("Intersection Evidence", OnIntersectionEvidenceTitleChanged));

    public static readonly DependencyProperty CorrespondenceEvidenceContentProperty =
        DependencyProperty.Register(
            nameof(CorrespondenceEvidenceContent),
            typeof(object),
            typeof(OpenVisionDockWorkspaceView),
            new PropertyMetadata(null));

    public static readonly DependencyProperty CorrespondenceEvidenceTitleProperty =
        DependencyProperty.Register(
            nameof(CorrespondenceEvidenceTitle),
            typeof(string),
            typeof(OpenVisionDockWorkspaceView),
            new PropertyMetadata("Correspondence Evidence", OnCorrespondenceEvidenceTitleChanged));

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
        Loaded += (_, _) =>
        {
            ApplyInitialDockSizes();
            ApplyResponsiveDockLayout(ActualWidth);
        };
        SizeChanged += (_, args) =>
        {
            ApplyResponsiveDockLayout(args.NewSize.Width);
            ApplyBottomPanePresentation();
        };
        outputCompareAnchorable.IsSelectedChanged += (_, _) => ApplyBottomPanePresentation();
    }

    public object? ViewerContent
    {
        get => GetValue(ViewerContentProperty);
        set => SetValue(ViewerContentProperty, value);
    }

    public bool ReactivateViewerContent(object? requestedContent)
    {
        if (requestedContent is null)
        {
            return false;
        }

        SetCurrentValue(ViewerContentProperty, requestedContent);
        viewerContentPresenter.SetCurrentValue(
            ContentPresenter.ContentProperty,
            requestedContent);
        viewerAnchorable.IsSelected = true;
        viewerAnchorable.IsActive = true;
        UpdateLayout();
        return ReferenceEquals(ViewerContent, requestedContent)
            && ReferenceEquals(
                viewerContentPresenter.Content,
                requestedContent);
    }

    public object? ResultsContent
    {
        get => GetValue(ResultsContentProperty);
        set => SetValue(ResultsContentProperty, value);
    }

    public object? DataLayersContent
    {
        get => GetValue(DataLayersContentProperty);
        set => SetValue(DataLayersContentProperty, value);
    }

    public object? ToolLibraryContent
    {
        get => GetValue(ToolLibraryContentProperty);
        set => SetValue(ToolLibraryContentProperty, value);
    }

    public string ViewerTitle
    {
        get => (string)GetValue(ViewerTitleProperty);
        set => SetValue(ViewerTitleProperty, value);
    }

    public string ResultsTitle
    {
        get => (string)GetValue(ResultsTitleProperty);
        set => SetValue(ResultsTitleProperty, value);
    }

    public string DataLayersTitle
    {
        get => (string)GetValue(DataLayersTitleProperty);
        set => SetValue(DataLayersTitleProperty, value);
    }

    public string ToolLibraryTitle
    {
        get => (string)GetValue(ToolLibraryTitleProperty);
        set => SetValue(ToolLibraryTitleProperty, value);
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

    public object? OutputCompareContent
    {
        get => GetValue(OutputCompareContentProperty);
        set => SetValue(OutputCompareContentProperty, value);
    }

    public string OutputCompareTitle
    {
        get => (string)GetValue(OutputCompareTitleProperty);
        set => SetValue(OutputCompareTitleProperty, value);
    }

    public object? DisplayedOutputsContent
    {
        get => GetValue(DisplayedOutputsContentProperty);
        set => SetValue(DisplayedOutputsContentProperty, value);
    }

    public string DisplayedOutputsTitle
    {
        get => (string)GetValue(DisplayedOutputsTitleProperty);
        set => SetValue(DisplayedOutputsTitleProperty, value);
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

    public object? IntersectionEvidenceContent
    {
        get => GetValue(IntersectionEvidenceContentProperty);
        set => SetValue(IntersectionEvidenceContentProperty, value);
    }

    public string IntersectionEvidenceTitle
    {
        get => (string)GetValue(IntersectionEvidenceTitleProperty);
        set => SetValue(IntersectionEvidenceTitleProperty, value);
    }

    public object? CorrespondenceEvidenceContent
    {
        get => GetValue(CorrespondenceEvidenceContentProperty);
        set => SetValue(CorrespondenceEvidenceContentProperty, value);
    }

    public string CorrespondenceEvidenceTitle
    {
        get => (string)GetValue(CorrespondenceEvidenceTitleProperty);
        set => SetValue(CorrespondenceEvidenceTitleProperty, value);
    }

    public bool IsBottomPaneExpanded
    {
        get => (bool)GetValue(IsBottomPaneExpandedProperty);
        set => SetValue(IsBottomPaneExpandedProperty, value);
    }

    public bool IsCompactLayout => dataLayersTabbedForCompactLayout;

    public OpenVisionOperatorStage OperatorStage => operatorStage;

    public bool HasTopThemedDockTabs =>
        ReferenceEquals(
            workspaceDockingManager.AnchorablePaneControlStyle,
            Resources["OpenVisionTopAnchorablePaneStyle"])
        && workspaceDockingManager.AnchorablePaneControlStyle.Setters
            .OfType<Setter>()
            .Any(setter =>
                setter.Property == TabControl.TabStripPlacementProperty
                && Equals(setter.Value, Dock.Top));

    public bool HasSetupStageComposition =>
        operatorStage == OpenVisionOperatorStage.Setup
        && !IsBottomPaneAttached
        && (dataLayersTabbedForCompactLayout
            ? workbenchPane.Children.Count == 1
              && ReferenceEquals(workbenchPane.Children[0], toolLibraryPane)
              && toolLibraryPane.Children.Contains(toolLibraryAnchorable)
              && toolLibraryPane.Children.Contains(dataLayersAnchorable)
            : workbenchPane.Children.Count == 2
              && ReferenceEquals(workbenchPane.Children[0], toolLibraryPane)
              && ReferenceEquals(workbenchPane.Children[1], dataLayersPane));

    public bool HasTeachStageComposition =>
        operatorStage == OpenVisionOperatorStage.Teach
        && !IsBottomPaneAttached
        && (dataLayersTabbedForCompactLayout
            ? workbenchPane.Children.Count == 2
              && ReferenceEquals(workbenchPane.Children[0], dataLayersPane)
              && ReferenceEquals(workbenchPane.Children[1], primaryPane)
              && (compactToolInspectorFocused
                  ? dataLayersPane.Children.Count == 1
                    && dataLayersPane.Children.Contains(toolInspectorAnchorable)
                  : dataLayersPane.Children.Contains(dataLayersAnchorable)
                    && dataLayersPane.Children.Contains(toolInspectorAnchorable))
            : workbenchPane.Children.Count == 3
              && ReferenceEquals(workbenchPane.Children[0], dataLayersPane)
              && ReferenceEquals(workbenchPane.Children[1], primaryPane)
              && ReferenceEquals(workbenchPane.Children[2], toolInspectorPane));

    public bool HasValidateStageComposition =>
        operatorStage == OpenVisionOperatorStage.Validate
        && !IsBottomPaneAttached
        && workbenchPane.Children.Count == 1
        && ReferenceEquals(workbenchPane.Children[0], evidencePane);

    public bool HasResultsStageComposition =>
        operatorStage == OpenVisionOperatorStage.Results
        && !IsBottomPaneAttached
        && workbenchPane.Children.Count == 1
        && ReferenceEquals(workbenchPane.Children[0], resultsPane);

    public bool HasValidateOrResultsStageComposition =>
        HasValidateStageComposition || HasResultsStageComposition;

    public void SetOperatorStage(OpenVisionOperatorStage stage)
    {
        if (operatorStage == stage)
        {
            ApplyResponsiveDockLayout(ActualWidth);
            return;
        }

        if (stage != OpenVisionOperatorStage.Teach)
        {
            compactToolInspectorFocused = false;
        }

        operatorStage = stage;
        IsBottomPaneExpanded = stage == OpenVisionOperatorStage.Legacy;
        ApplyResponsiveDockLayout(ActualWidth > 0 ? ActualWidth : CompactWorkbenchWidth);
    }

    public bool HasRecipeFlowInspectorViewerOrder =>
        workbenchPane.Children.Count >= 4
        && ReferenceEquals(workbenchPane.Children[0], toolLibraryPane)
        && ReferenceEquals(workbenchPane.Children[1], dataLayersPane)
        && ReferenceEquals(workbenchPane.Children[2], toolInspectorPane)
        && ReferenceEquals(workbenchPane.Children[3], primaryPane);

    public bool HasDominantViewerWidth =>
        primaryPane.DockWidth.IsStar
        && toolInspectorPane.DockWidth.IsStar
        && primaryPane.DockWidth.Value >= toolInspectorPane.DockWidth.Value * 2.5;

    public void ActivateLinkedViewPane()
    {
        if (!IsBottomPaneExpanded)
        {
            IsBottomPaneExpanded = true;
        }

        linkedViewAnchorable.IsSelected = true;
        linkedViewAnchorable.IsActive = true;
    }

    public bool IsLinkedViewPaneSelected => linkedViewAnchorable.IsSelected && linkedViewAnchorable.IsActive;

    public void ActivateToolLibraryPane()
    {
        toolLibraryAnchorable.IsSelected = true;
        toolLibraryAnchorable.IsActive = true;
    }

    public bool IsToolLibraryPaneSelected => toolLibraryAnchorable.IsSelected && toolLibraryAnchorable.IsActive;

    public void ActivateToolInspectorPane()
    {
        if (dataLayersPane.Children.Contains(toolInspectorAnchorable))
        {
            compactToolInspectorFocused = true;
            FocusCompactToolInspector();
        }
        else if (toolInspectorPane.Children.Contains(toolInspectorAnchorable))
        {
            toolInspectorAnchorable.IsSelected = true;
            toolInspectorPane.SelectedContentIndex =
                toolInspectorPane.Children.IndexOf(toolInspectorAnchorable);
        }

        toolInspectorAnchorable.IsActive = true;
        workspaceDockingManager.ActiveContent = toolInspectorAnchorable.Content;
    }

    public bool IsToolInspectorPaneSelected =>
        toolInspectorAnchorable.IsSelected && toolInspectorAnchorable.IsActive;

    public IReadOnlyList<DockingPaneContract> GetDockingPaneContracts() =>
    [
        ToContract(toolLibraryAnchorable),
        ToContract(dataLayersAnchorable),
        ToContract(toolInspectorAnchorable),
        ToContract(viewerAnchorable),
        ToContract(evidenceAnchorable),
        ToContract(outputCompareAnchorable),
        ToContract(displayedOutputsAnchorable),
        ToContract(linkedViewAnchorable),
        ToContract(profileAnchorable),
        ToContract(fitDiagnosticsAnchorable),
        ToContract(intersectionEvidenceAnchorable),
        ToContract(correspondenceEvidenceAnchorable),
    ];

    public void ActivateEvidencePane()
    {
        if (!IsBottomPaneExpanded) IsBottomPaneExpanded = true;
        evidenceAnchorable.IsSelected = true;
        evidenceAnchorable.IsActive = true;
    }

    public bool IsEvidencePaneSelected => evidenceAnchorable.IsSelected && evidenceAnchorable.IsActive;

    public void SetEvidenceAnalysisHeight(bool enabled)
    {
        if (evidenceUsesAnalysisHeight == enabled)
        {
            return;
        }

        evidenceUsesAnalysisHeight = enabled;
        ApplyBottomPanePresentation();
    }

    public void ActivateOutputComparePane()
    {
        if (!IsBottomPaneExpanded) IsBottomPaneExpanded = true;
        outputCompareAnchorable.IsSelected = true;
        outputCompareAnchorable.IsActive = true;
        ApplyBottomPanePresentation();
    }

    public bool IsOutputComparePaneSelected => outputCompareAnchorable.IsSelected && outputCompareAnchorable.IsActive;

    public bool HasUsableOutputCompareDockHeight =>
        workbenchPane.DockHeight.IsStar
        && evidencePane.DockHeight.IsStar
        && workbenchPane.DockHeight.Value <= OutputCompareWorkbenchHeightRatio
        && evidencePane.DockHeight.Value >= 1;

    public bool HasStandardBottomPaneHeight =>
        workbenchPane.DockHeight.IsStar
        && evidencePane.DockHeight.IsStar
        && workbenchPane.DockHeight.Value >= StandardWorkbenchHeightRatio
        && evidencePane.DockHeight.Value == 1;

    public void ActivateDisplayedOutputsPane()
    {
        if (!IsBottomPaneExpanded) IsBottomPaneExpanded = true;
        displayedOutputsAnchorable.IsSelected = true;
        displayedOutputsAnchorable.IsActive = true;
    }

    public bool IsDisplayedOutputsPaneSelected => displayedOutputsAnchorable.IsSelected && displayedOutputsAnchorable.IsActive;

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

    public void ActivateIntersectionEvidencePane()
    {
        if (!IsBottomPaneExpanded) IsBottomPaneExpanded = true;
        intersectionEvidenceAnchorable.IsSelected = true;
        intersectionEvidenceAnchorable.IsActive = true;
    }

    public bool IsIntersectionEvidencePaneSelected => intersectionEvidenceAnchorable.IsSelected && intersectionEvidenceAnchorable.IsActive;

    public void ActivateCorrespondenceEvidencePane()
    {
        if (!IsBottomPaneExpanded) IsBottomPaneExpanded = true;
        correspondenceEvidenceAnchorable.IsSelected = true;
        correspondenceEvidenceAnchorable.IsActive = true;
    }

    public bool IsCorrespondenceEvidencePaneSelected => correspondenceEvidenceAnchorable.IsSelected && correspondenceEvidenceAnchorable.IsActive;

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

    private static void OnToolLibraryTitleChanged(DependencyObject owner, DependencyPropertyChangedEventArgs args)
    {
        if (owner is OpenVisionDockWorkspaceView view && view.toolLibraryAnchorable is not null)
        {
            view.toolLibraryAnchorable.Title = args.NewValue as string ?? string.Empty;
        }
    }

    private static void OnViewerTitleChanged(DependencyObject owner, DependencyPropertyChangedEventArgs args)
    {
        if (owner is OpenVisionDockWorkspaceView view && view.viewerAnchorable is not null)
        {
            view.viewerAnchorable.Title = args.NewValue as string ?? string.Empty;
        }
    }

    private static void OnResultsTitleChanged(DependencyObject owner, DependencyPropertyChangedEventArgs args)
    {
        if (owner is OpenVisionDockWorkspaceView view && view.resultsAnchorable is not null)
        {
            view.resultsAnchorable.Title = args.NewValue as string ?? string.Empty;
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

    private static void OnOutputCompareTitleChanged(DependencyObject owner, DependencyPropertyChangedEventArgs args)
    {
        if (owner is OpenVisionDockWorkspaceView view && view.outputCompareAnchorable is not null)
        {
            view.outputCompareAnchorable.Title = args.NewValue as string ?? string.Empty;
        }
    }

    private static void OnDisplayedOutputsTitleChanged(DependencyObject owner, DependencyPropertyChangedEventArgs args)
    {
        if (owner is OpenVisionDockWorkspaceView view && view.displayedOutputsAnchorable is not null)
        {
            view.displayedOutputsAnchorable.Title = args.NewValue as string ?? string.Empty;
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

    private static void OnIntersectionEvidenceTitleChanged(DependencyObject owner, DependencyPropertyChangedEventArgs args)
    {
        if (owner is OpenVisionDockWorkspaceView view && view.intersectionEvidenceAnchorable is not null)
        {
            view.intersectionEvidenceAnchorable.Title = args.NewValue as string ?? string.Empty;
        }
    }

    private static void OnCorrespondenceEvidenceTitleChanged(DependencyObject owner, DependencyPropertyChangedEventArgs args)
    {
        if (owner is OpenVisionDockWorkspaceView view && view.correspondenceEvidenceAnchorable is not null)
        {
            view.correspondenceEvidenceAnchorable.Title = args.NewValue as string ?? string.Empty;
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
        toolLibraryAnchorable.Title = ToolLibraryTitle;
        dataLayersAnchorable.Title = DataLayersTitle;
        viewerAnchorable.Title = ViewerTitle;
        resultsAnchorable.Title = ResultsTitle;
        toolInspectorAnchorable.Title = ToolInspectorTitle;
        evidenceAnchorable.Title = EvidenceTitle;
        outputCompareAnchorable.Title = OutputCompareTitle;
        displayedOutputsAnchorable.Title = DisplayedOutputsTitle;
        linkedViewAnchorable.Title = LinkedViewTitle;
        profileAnchorable.Title = ProfileTitle;
        fitDiagnosticsAnchorable.Title = FitDiagnosticsTitle;
        intersectionEvidenceAnchorable.Title = IntersectionEvidenceTitle;
        correspondenceEvidenceAnchorable.Title = CorrespondenceEvidenceTitle;
    }

    private void ApplyInitialDockSizes()
    {
        workbenchPane.DockHeight = new GridLength(2, GridUnitType.Star);
        ApplyBottomPaneHeight();
        toolLibraryPane.DockWidth = new GridLength(0.72, GridUnitType.Star);
        dataLayersPane.DockWidth = new GridLength(0.90, GridUnitType.Star);
        primaryPane.DockWidth = new GridLength(3.30, GridUnitType.Star);
        toolInspectorPane.DockWidth = new GridLength(1.05, GridUnitType.Star);
    }

    private void ApplyResponsiveDockLayout(double width)
    {
        if (width <= 0)
        {
            return;
        }

        var useCompactLayout = width < CompactWorkbenchWidth;
        dataLayersTabbedForCompactLayout = useCompactLayout;
        RestoreAnchorableOwners();
        DetachPrimaryPanes();

        switch (operatorStage)
        {
            case OpenVisionOperatorStage.Setup:
                ComposeSetupStage(useCompactLayout);
                break;
            case OpenVisionOperatorStage.Teach:
                ComposeTeachStage(useCompactLayout);
                break;
            case OpenVisionOperatorStage.Validate:
                AttachPane(evidencePane, 0);
                evidencePane.DockWidth = new GridLength(1, GridUnitType.Star);
                break;
            case OpenVisionOperatorStage.Results:
                AttachPane(resultsPane, 0);
                resultsPane.DockWidth = new GridLength(1, GridUnitType.Star);
                break;
            default:
                ComposeLegacyLayout(useCompactLayout);
                break;
        }

        if (operatorStage == OpenVisionOperatorStage.Teach
            && useCompactLayout
            && compactToolInspectorFocused)
        {
            FocusCompactToolInspector();
        }

        ApplyBottomPanePresentation();
    }

    private void FocusCompactToolInspector()
    {
        if (dataLayersPane.Children.Contains(dataLayersAnchorable))
        {
            MoveAnchorable(dataLayersAnchorable, toolInspectorPane);
        }

        if (!dataLayersPane.Children.Contains(toolInspectorAnchorable))
        {
            MoveAnchorable(toolInspectorAnchorable, dataLayersPane);
        }

        toolInspectorAnchorable.IsSelected = true;
        toolInspectorAnchorable.IsActive = true;
        dataLayersPane.SelectedContentIndex =
            dataLayersPane.Children.IndexOf(toolInspectorAnchorable);
        workspaceDockingManager.ActiveContent = toolInspectorAnchorable.Content;
    }

    private void ComposeSetupStage(bool compact)
    {
        if (compact)
        {
            MoveAnchorable(dataLayersAnchorable, toolLibraryPane);
            AttachPane(toolLibraryPane, 0);
            toolLibraryPane.DockWidth = new GridLength(1, GridUnitType.Star);
            return;
        }

        AttachPane(toolLibraryPane, 0);
        AttachPane(dataLayersPane, 1);
        toolLibraryPane.DockWidth = new GridLength(1, GridUnitType.Star);
        dataLayersPane.DockWidth = new GridLength(1.35, GridUnitType.Star);
    }

    private void ComposeTeachStage(bool compact)
    {
        if (compact)
        {
            MoveAnchorable(toolInspectorAnchorable, dataLayersPane);
            AttachPane(dataLayersPane, 0);
            AttachPane(primaryPane, 1);
            dataLayersPane.DockWidth = new GridLength(1.05, GridUnitType.Star);
            primaryPane.DockWidth = new GridLength(3.45, GridUnitType.Star);
            return;
        }

        AttachPane(dataLayersPane, 0);
        AttachPane(primaryPane, 1);
        AttachPane(toolInspectorPane, 2);
        dataLayersPane.DockWidth = new GridLength(0.82, GridUnitType.Star);
        primaryPane.DockWidth = new GridLength(3.65, GridUnitType.Star);
        toolInspectorPane.DockWidth = new GridLength(1.12, GridUnitType.Star);
    }

    private void ComposeLegacyLayout(bool compact)
    {
        AttachPane(toolLibraryPane, 0);
        if (compact)
        {
            MoveAnchorable(dataLayersAnchorable, toolLibraryPane);
        }
        else
        {
            AttachPane(dataLayersPane, 1);
        }

        AttachPane(toolInspectorPane, compact ? 1 : 2);
        AttachPane(primaryPane, compact ? 2 : 3);
        toolLibraryPane.DockWidth = new GridLength(compact ? 1.00 : 0.72, GridUnitType.Star);
        dataLayersPane.DockWidth = new GridLength(0.90, GridUnitType.Star);
        primaryPane.DockWidth = new GridLength(compact ? 3.00 : 3.30, GridUnitType.Star);
        toolInspectorPane.DockWidth = new GridLength(compact ? 1.15 : 1.05, GridUnitType.Star);
    }

    private void RestoreAnchorableOwners()
    {
        MoveAnchorable(toolLibraryAnchorable, toolLibraryPane);
        MoveAnchorable(dataLayersAnchorable, dataLayersPane);
        MoveAnchorable(toolInspectorAnchorable, toolInspectorPane);
        MoveAnchorable(viewerAnchorable, primaryPane);
        MoveAnchorable(resultsAnchorable, resultsPane);
    }

    private void DetachPrimaryPanes()
    {
        foreach (var pane in new[]
                 {
                     toolLibraryPane,
                     dataLayersPane,
                     toolInspectorPane,
                     primaryPane,
                     resultsPane,
                     evidencePane,
                 })
        {
            if (ReferenceEquals(pane.Parent, workbenchPane))
            {
                workbenchPane.Children.Remove(pane);
            }
        }
    }

    private void AttachPane(
        AvalonDock.Layout.LayoutAnchorablePane pane,
        int index)
    {
        if (ReferenceEquals(pane.Parent, workbenchPane))
        {
            var currentIndex = workbenchPane.Children.IndexOf(pane);
            if (currentIndex == index)
            {
                return;
            }

            workbenchPane.Children.Remove(pane);
        }
        else if (pane.Parent is AvalonDock.Layout.LayoutPanel parent)
        {
            parent.Children.Remove(pane);
        }

        workbenchPane.Children.Insert(Math.Min(index, workbenchPane.Children.Count), pane);
    }

    private static void MoveAnchorable(
        AvalonDock.Layout.LayoutAnchorable anchorable,
        AvalonDock.Layout.LayoutAnchorablePane target)
    {
        if (ReferenceEquals(anchorable.Parent, target))
        {
            return;
        }

        if (anchorable.Parent is AvalonDock.Layout.LayoutAnchorablePane parent)
        {
            parent.Children.Remove(anchorable);
        }

        target.Children.Add(anchorable);
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

        var workbenchHeightRatio = outputCompareAnchorable.IsSelected
            ? OutputCompareWorkbenchHeightRatio
            : evidenceUsesAnalysisHeight
                ? ActualHeight < CompactValidationHeight
                    ? CompactValidationWorkbenchHeightRatio
                    : ValidationWorkbenchHeightRatio
                : StandardWorkbenchHeightRatio;
        workbenchPane.DockHeight = new GridLength(workbenchHeightRatio, GridUnitType.Star);
        evidencePane.DockHeight = new GridLength(1, GridUnitType.Star);
    }
}
