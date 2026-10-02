using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AvalonDock.Layout;

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
    private const double CompactValidationEvidenceToViewerRatio = 0.60;
    private const double StandardWorkbenchHeightRatio = 2;
    private const double AuthoringResultsWorkbenchHeightRatio = 2.20;
    private const double CompactAuthoringResultsWorkbenchHeightRatio = 2.60;
    private const double CompareResultsStarHeight = 0.001;
    private const double CompactAuthoringSupportToViewerRatio = 351d / 838d;
    private const double WideAuthoringSupportMinimumWidth = 414;
    private const double WideAuthoringInspectorMinimumWidth = 320;
    private readonly LayoutPanel viewerResultsColumn = new() { Orientation = Orientation.Vertical };
    private bool bottomPaneDetachedForFocus;
    private bool dataLayersTabbedForCompactLayout;
    private bool evidenceUsesAnalysisHeight;
    private double authoringResultsDefaultDockMinHeight;
    private Grid? outputCompareResultsGrid;
    private RowDefinition? outputCompareResultsRow;
    private double outputCompareResultsDefaultRowMinHeight;
    private RowDefinition? outputCompareResultsSplitterRow;
    private GridLength outputCompareResultsDefaultSplitterHeight;
    private bool? displayedOutputsVisibilityBeforeCompare;
    private bool displayedOutputsWasSelectedBeforeCompare;
    private string preferredSupportContentId = OpenVisionDockPresentationState.Default.SupportContentId;
    private OpenVisionOperatorStage operatorStage = OpenVisionOperatorStage.Legacy;
    private OpenVisionDockLayoutVariant wideLayout =
        OpenVisionDockPresentationState.Default.Wide;
    private OpenVisionDockLayoutVariant compactLayout =
        OpenVisionDockPresentationState.Default.Compact;

    public static readonly DependencyProperty IsAuthoringLayoutProperty = DependencyProperty.Register(
        nameof(IsAuthoringLayout), typeof(bool), typeof(OpenVisionDockWorkspaceView), new PropertyMetadata(false));

    public static readonly DependencyProperty IsOutputCompareViewerActiveProperty = DependencyProperty.Register(
        nameof(IsOutputCompareViewerActive),
        typeof(bool),
        typeof(OpenVisionDockWorkspaceView),
        new PropertyMetadata(false, OnOutputCompareViewerActiveChanged));

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

    public static readonly DependencyProperty CompactDataLayersTitleProperty =
        DependencyProperty.Register(
            nameof(CompactDataLayersTitle),
            typeof(string),
            typeof(OpenVisionDockWorkspaceView),
            new PropertyMetadata("Flow", OnCompactAuthoringTitleChanged));

    public static readonly DependencyProperty ToolLibraryTitleProperty =
        DependencyProperty.Register(
            nameof(ToolLibraryTitle),
            typeof(string),
            typeof(OpenVisionDockWorkspaceView),
            new PropertyMetadata("Tool Library", OnToolLibraryTitleChanged));

    public static readonly DependencyProperty CompactToolLibraryTitleProperty =
        DependencyProperty.Register(
            nameof(CompactToolLibraryTitle),
            typeof(string),
            typeof(OpenVisionDockWorkspaceView),
            new PropertyMetadata("Tools", OnCompactAuthoringTitleChanged));

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

    public static readonly DependencyProperty CompactToolInspectorTitleProperty =
        DependencyProperty.Register(
            nameof(CompactToolInspectorTitle),
            typeof(string),
            typeof(OpenVisionDockWorkspaceView),
            new PropertyMetadata("Selected", OnCompactAuthoringTitleChanged));

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

    public static readonly DependencyProperty ValidationEvidenceTitleProperty =
        DependencyProperty.Register(
            nameof(ValidationEvidenceTitle),
            typeof(string),
            typeof(OpenVisionDockWorkspaceView),
            new PropertyMetadata("Validation", OnValidationEvidenceTitleChanged));

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

    public static readonly DependencyProperty IsValidationTabFilterActiveProperty =
        DependencyProperty.Register(
            nameof(IsValidationTabFilterActive),
            typeof(bool),
            typeof(OpenVisionDockWorkspaceView),
            new PropertyMetadata(false));

    public static readonly DependencyProperty ValidationVisibleSupportContentIdProperty =
        DependencyProperty.Register(
            nameof(ValidationVisibleSupportContentId),
            typeof(string),
            typeof(OpenVisionDockWorkspaceView),
            new PropertyMetadata(null));

    public OpenVisionDockWorkspaceView()
    {
        InitializeComponent();
        authoringResultsDefaultDockMinHeight = resultsPane.DockMinHeight;
        ApplyDockTitles();
        Loaded += (_, _) =>
        {
            ApplyInitialDockSizes();
            ApplyResponsiveDockLayout(ActualWidth, rememberCurrent: false);
            ApplyStageSupportPaneAvailability();
            ApplyOutputCompareDisplayedOutputsVisibility();
        };
        SizeChanged += (_, args) =>
        {
            // Initial XAML sizes belong to Legacy; remember ratios only after the active stage is composed.
            ApplyResponsiveDockLayout(args.NewSize.Width, rememberCurrent: IsLoaded);
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

    public string CompactDataLayersTitle
    {
        get => (string)GetValue(CompactDataLayersTitleProperty);
        set => SetValue(CompactDataLayersTitleProperty, value);
    }

    public string ToolLibraryTitle
    {
        get => (string)GetValue(ToolLibraryTitleProperty);
        set => SetValue(ToolLibraryTitleProperty, value);
    }

    public string CompactToolLibraryTitle
    {
        get => (string)GetValue(CompactToolLibraryTitleProperty);
        set => SetValue(CompactToolLibraryTitleProperty, value);
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

    public string CompactToolInspectorTitle
    {
        get => (string)GetValue(CompactToolInspectorTitleProperty);
        set => SetValue(CompactToolInspectorTitleProperty, value);
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

    public string ValidationEvidenceTitle
    {
        get => (string)GetValue(ValidationEvidenceTitleProperty);
        set => SetValue(ValidationEvidenceTitleProperty, value);
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

    public bool IsValidationTabFilterActive
    {
        get => (bool)GetValue(IsValidationTabFilterActiveProperty);
        private set => SetValue(IsValidationTabFilterActiveProperty, value);
    }

    public string? ValidationVisibleSupportContentId
    {
        get => (string?)GetValue(ValidationVisibleSupportContentIdProperty);
        private set => SetValue(ValidationVisibleSupportContentIdProperty, value);
    }

    public bool IsCompactLayout => dataLayersTabbedForCompactLayout;

    public bool IsAuthoringLayout
    {
        get => (bool)GetValue(IsAuthoringLayoutProperty);
        private set => SetValue(IsAuthoringLayoutProperty, value);
    }

    public bool IsOutputCompareViewerActive
    {
        get => (bool)GetValue(IsOutputCompareViewerActiveProperty);
        set => SetValue(IsOutputCompareViewerActiveProperty, value);
    }

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

    public bool HasSideCollapsibleTaskPanes =>
        new[]
        {
            toolLibraryAnchorable,
            dataLayersAnchorable,
            toolInspectorAnchorable,
            resultsAnchorable,
            evidenceAnchorable,
            outputCompareAnchorable,
            displayedOutputsAnchorable,
            linkedViewAnchorable,
            profileAnchorable,
            fitDiagnosticsAnchorable,
            intersectionEvidenceAnchorable,
            correspondenceEvidenceAnchorable,
        }.All(pane => pane.CanHide)
        && !viewerAnchorable.CanHide;

    public (bool Collapsed, bool Restored) VerifySupportAutoHideRoundTrip()
    {
        var originalStage = operatorStage;
        var originalBottomPaneExpanded = IsBottomPaneExpanded;
        SetOperatorStage(OpenVisionOperatorStage.Teach);
        if (!dataLayersAnchorable.CanHide || dataLayersAnchorable.IsAutoHidden)
        {
            SetOperatorStage(originalStage);
            IsBottomPaneExpanded = originalBottomPaneExpanded;
            return (false, false);
        }

        dataLayersAnchorable.ToggleAutoHide();
        var collapsed = dataLayersAnchorable.IsAutoHidden;
        foreach (var pane in new[]
                 {
                     toolLibraryAnchorable,
                     dataLayersAnchorable,
                 })
        {
            if (pane.IsAutoHidden)
            {
                pane.ToggleAutoHide();
            }
        }

        ApplyResponsiveDockLayout(
            ActualWidth > 0 ? ActualWidth : CompactWorkbenchWidth,
            rememberCurrent: false);
        var restored = !dataLayersAnchorable.IsAutoHidden
                       && HasAuthoringPaneComposition;
        SetOperatorStage(originalStage);
        IsBottomPaneExpanded = originalBottomPaneExpanded;
        return (collapsed, restored);
    }

    public bool HasSetupStageComposition =>
        operatorStage == OpenVisionOperatorStage.Setup
        && !IsBottomPaneAttached
        && HasAuthoringPaneComposition
        && HasAuthoringResultsPaneComposition;

    public bool HasTeachStageComposition =>
        operatorStage == OpenVisionOperatorStage.Teach
        && !IsBottomPaneAttached
        && HasAuthoringPaneComposition
        && (HasAuthoringResultsPaneComposition || HasOutputCompareAuthoringComposition);

    public bool HasOutputCompareAuthoringComposition =>
        operatorStage is OpenVisionOperatorStage.Setup or OpenVisionOperatorStage.Teach
        && IsOutputCompareViewerActive
        && !IsBottomPaneAttached
        && ReferenceEquals(resultsPane.Parent, viewerResultsColumn)
        && ReferenceEquals(viewerResultsColumn.Parent, workbenchPane)
        && viewerResultsColumn.Orientation == Orientation.Vertical
        && viewerResultsColumn.Children.Count == 2
        && ReferenceEquals(viewerResultsColumn.Children[0], primaryPane)
        && ReferenceEquals(viewerResultsColumn.Children[1], resultsPane)
        && resultsPane.DockHeight.IsStar
        && resultsPane.DockHeight.Value == CompareResultsStarHeight
        && resultsPane.DockMinHeight == 0
        && !resultsAnchorable.IsSelected
        && primaryPane.DockHeight.IsStar
        && primaryPane.DockHeight.Value == 1
        && (dataLayersTabbedForCompactLayout
            ? workbenchPane.Children.Count == 2
              && ReferenceEquals(workbenchPane.Children[0], dataLayersPane)
              && ReferenceEquals(workbenchPane.Children[1], viewerResultsColumn)
              && dataLayersPane.Children.Contains(toolInspectorAnchorable)
              && !toolInspectorPane.Children.Contains(toolInspectorAnchorable)
            : workbenchPane.Children.Count == 3
              && ReferenceEquals(workbenchPane.Children[0], dataLayersPane)
              && ReferenceEquals(workbenchPane.Children[1], toolInspectorPane)
              && ReferenceEquals(workbenchPane.Children[2], viewerResultsColumn)
              && toolInspectorPane.Children.Contains(toolInspectorAnchorable));

    public bool HasAuthoringResultsPaneComposition =>
        resultsPane.Children.Contains(resultsAnchorable)
        && resultsAnchorable.IsSelected
        && resultsPane.DockHeight.IsStar
        && resultsPane.DockHeight.Value == 1
        && ReferenceEquals(resultsPane.Parent, viewerResultsColumn)
        && ReferenceEquals(viewerResultsColumn.Parent, workbenchPane)
        && viewerResultsColumn.Orientation == Orientation.Vertical
        && viewerResultsColumn.Children.Count == 2
        && ReferenceEquals(viewerResultsColumn.Children[0], primaryPane)
        && primaryPane.DockHeight.IsStar
        && primaryPane.DockHeight.Value == (dataLayersTabbedForCompactLayout
            ? CompactAuthoringResultsWorkbenchHeightRatio
            : AuthoringResultsWorkbenchHeightRatio)
        && ReferenceEquals(viewerResultsColumn.Children[1], resultsPane);

    public bool HasAuthoringPaneComposition =>
        dataLayersPane.Children.Contains(dataLayersAnchorable)
        && primaryPane.Children.Contains(viewerAnchorable)
        && primaryPane.Children.Contains(displayedOutputsAnchorable)
        && (HasOutputCompareAuthoringComposition
            || (dataLayersTabbedForCompactLayout
            ? workbenchPane.Children.Count == 2
              && ReferenceEquals(workbenchPane.Children[0], dataLayersPane)
              && ReferenceEquals(workbenchPane.Children[1], viewerResultsColumn)
              && viewerResultsColumn.Children.Count == 2
              && ReferenceEquals(viewerResultsColumn.Children[0], primaryPane)
              && ReferenceEquals(viewerResultsColumn.Children[1], resultsPane)
              && dataLayersPane.Children.Contains(toolInspectorAnchorable)
              && !toolInspectorPane.Children.Contains(toolInspectorAnchorable)
            : workbenchPane.Children.Count == 3
              && ReferenceEquals(workbenchPane.Children[0], dataLayersPane)
              && ReferenceEquals(workbenchPane.Children[1], toolInspectorPane)
              && ReferenceEquals(workbenchPane.Children[2], viewerResultsColumn)
              && viewerResultsColumn.Children.Count == 2
              && ReferenceEquals(viewerResultsColumn.Children[0], primaryPane)
              && ReferenceEquals(viewerResultsColumn.Children[1], resultsPane)
              && toolInspectorPane.Children.Contains(toolInspectorAnchorable)))
        && dataLayersPane.Children.Contains(dataLayersAnchorable)
        && primaryPane.Children.Contains(viewerAnchorable);

    public int AuthoringSupportTabCount => dataLayersPane.Children.Count;

    public bool IsToolLibraryDetachedFromAuthoring =>
        operatorStage is OpenVisionOperatorStage.Setup or OpenVisionOperatorStage.Teach
        && !dataLayersPane.Children.Contains(toolLibraryAnchorable)
        && !toolInspectorPane.Children.Contains(toolLibraryAnchorable);

    public bool HasValidateStageComposition =>
        operatorStage == OpenVisionOperatorStage.Validate
        && !IsBottomPaneAttached
        && workbenchPane.Children.Count == 2
        && ReferenceEquals(workbenchPane.Children[0], evidencePane)
        && ReferenceEquals(workbenchPane.Children[1], primaryPane)
        && primaryPane.Children.Contains(viewerAnchorable);

    public int VisibleValidationPaneTabCount =>
        IsValidationTabFilterActive
            ? string.IsNullOrWhiteSpace(ValidationVisibleSupportContentId) ? 1 : 2
            : ValidationPaneAnchorables.Count;

    public bool HasFocusedValidationPaneTabs =>
        operatorStage == OpenVisionOperatorStage.Validate
        && IsValidationTabFilterActive
        && VisibleValidationPaneTabCount == 1;

    public bool HasReadableValidationPaneRatio =>
        !IsCompactLayout
        || evidencePane.DockWidth.IsStar
           && primaryPane.DockWidth.IsStar
           && evidencePane.DockWidth.Value
           >= primaryPane.DockWidth.Value
           * CompactValidationEvidenceToViewerRatio;

    public bool HasAllLegacySupportPaneTabs =>
        operatorStage == OpenVisionOperatorStage.Legacy
        && !IsValidationTabFilterActive;

    public bool HasResultsStageComposition =>
        operatorStage == OpenVisionOperatorStage.Results
        && !IsBottomPaneAttached
        && workbenchPane.Children.Count == 2
        && ReferenceEquals(workbenchPane.Children[0], resultsPane)
        && ReferenceEquals(workbenchPane.Children[1], primaryPane)
        && primaryPane.Children.Contains(viewerAnchorable);

    public bool HasValidateOrResultsStageComposition =>
        HasValidateStageComposition || HasResultsStageComposition;

    public bool HasEvidenceLinkedViewerComposition =>
        (HasValidateStageComposition || HasResultsStageComposition)
        && primaryPane.DockWidth.IsStar
        && (operatorStage == OpenVisionOperatorStage.Validate
            ? evidencePane.DockWidth.IsStar
              && primaryPane.DockWidth.Value > evidencePane.DockWidth.Value
            : resultsPane.DockWidth.IsStar
              && primaryPane.DockWidth.Value > resultsPane.DockWidth.Value);

    public OpenVisionDockPresentationState CapturePresentationState()
    {
        RememberCurrentStageRatios();
        return new OpenVisionDockPresentationState(
            OpenVisionDockPresentationState.CurrentSchemaVersion,
            wideLayout,
            compactLayout,
            displayedOutputsAnchorable.IsSelected
                ? displayedOutputsAnchorable.ContentId
                : viewerAnchorable.ContentId,
            toolLibraryAnchorable.IsSelected
                ? toolLibraryAnchorable.ContentId
                : dataLayersAnchorable.ContentId);
    }

    public void ApplyPresentationState(OpenVisionDockPresentationState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.SchemaVersion != OpenVisionDockPresentationState.CurrentSchemaVersion)
        {
            state = OpenVisionDockPresentationState.Default;
        }

        wideLayout = SanitizeVariant(
            state.Wide,
            OpenVisionDockPresentationState.Default.Wide);
        compactLayout = SanitizeVariant(
            state.Compact,
            OpenVisionDockPresentationState.Default.Compact);
        preferredSupportContentId = state.SupportContentId;
        SelectPrimaryContent(state.PrimaryContentId);
        SelectSupportContent(state.SupportContentId);
        ApplyResponsiveDockLayout(
            ActualWidth > 0 ? ActualWidth : CompactWorkbenchWidth,
            rememberCurrent: false);
    }

    public void ResetPresentationState() =>
        ApplyPresentationState(OpenVisionDockPresentationState.Default);

    public void SetOperatorStage(OpenVisionOperatorStage stage)
    {
        if (operatorStage == stage)
        {
            ApplyResponsiveDockLayout(ActualWidth);
            return;
        }

        RememberCurrentStageRatios();
        operatorStage = stage;
        IsAuthoringLayout = stage is OpenVisionOperatorStage.Setup or OpenVisionOperatorStage.Teach;
        IsBottomPaneExpanded = stage == OpenVisionOperatorStage.Legacy;
        ApplyResponsiveDockLayout(
            ActualWidth > 0 ? ActualWidth : CompactWorkbenchWidth,
            rememberCurrent: false);
        ApplyStageSupportPaneAvailability();
    }

    public bool HasRecipeFlowInspectorViewerOrder =>
        ((operatorStage is OpenVisionOperatorStage.Setup or OpenVisionOperatorStage.Teach)
            && HasAuthoringPaneComposition
            && dataLayersPane.Children.Contains(dataLayersAnchorable)
            && (dataLayersTabbedForCompactLayout
                ? dataLayersPane.Children.Contains(toolInspectorAnchorable)
                : toolInspectorPane.Children.Contains(toolInspectorAnchorable))
            && primaryPane.Children.Contains(viewerAnchorable))
        || (operatorStage == OpenVisionOperatorStage.Legacy
            && workbenchPane.Children.Count >= 4
            && ReferenceEquals(workbenchPane.Children[1], dataLayersPane)
            && ReferenceEquals(workbenchPane.Children[2], toolInspectorPane)
            && ReferenceEquals(workbenchPane.Children[3], primaryPane));

    private void AutoHideTabButton_Click(object sender, RoutedEventArgs args)
    {
        if (sender is Button
            {
                Tag: LayoutAnchorable { CanHide: true } anchorable
            })
        {
            anchorable.ToggleAutoHide();
            args.Handled = true;
        }
    }

    public bool HasAdjacentViewerOutputs =>
        primaryPane.Children.Contains(viewerAnchorable)
        && primaryPane.Children.Contains(displayedOutputsAnchorable);

    public bool HasDominantViewerWidth =>
        primaryPane.DockWidth.IsStar
        && toolInspectorPane.DockWidth.IsStar
        && primaryPane.DockWidth.Value >= toolInspectorPane.DockWidth.Value * 2.5;

    public void ActivateLinkedViewPane()
    {
        ShowValidationSupportTab(linkedViewAnchorable);
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
        if (toolInspectorAnchorable.Parent is LayoutAnchorablePane pane)
        {
            toolInspectorAnchorable.IsSelected = true;
            pane.SelectedContentIndex = pane.Children.IndexOf(toolInspectorAnchorable);
        }

        toolInspectorAnchorable.IsActive = true;
        workspaceDockingManager.ActiveContent = toolInspectorAnchorable.Content;
    }

    public bool IsToolInspectorPaneSelected =>
        toolInspectorAnchorable.IsSelected && toolInspectorAnchorable.IsActive;

    public void ToggleToolInspectorAutoHide()
    {
        if (toolInspectorAnchorable.CanHide)
        {
            toolInspectorAnchorable.ToggleAutoHide();
        }
    }

    public bool IsToolInspectorAutoHidden =>
        toolInspectorAnchorable.IsAutoHidden;

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
        ShowValidationSupportTab(outputCompareAnchorable);
        if (!IsBottomPaneExpanded) IsBottomPaneExpanded = true;
        outputCompareAnchorable.IsSelected = true;
        outputCompareAnchorable.IsActive = true;
        ApplyBottomPanePresentation();
    }

    public void ActivateViewerPane()
    {
        if (IsBottomPaneExpanded)
        {
            IsBottomPaneExpanded = false;
        }

        viewerAnchorable.IsSelected = true;
        viewerAnchorable.IsActive = true;
    }

    public bool IsOutputComparePaneSelected => outputCompareAnchorable.IsSelected && outputCompareAnchorable.IsActive;

    public bool IsViewerPaneSelected => viewerAnchorable.IsSelected && viewerAnchorable.IsActive;

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
        if (operatorStage is not OpenVisionOperatorStage.Setup and not OpenVisionOperatorStage.Teach
            && !IsBottomPaneExpanded)
        {
            IsBottomPaneExpanded = true;
        }

        if (operatorStage is not OpenVisionOperatorStage.Setup and not OpenVisionOperatorStage.Teach)
        {
            evidenceAnchorable.IsSelected = true;
        }

        displayedOutputsAnchorable.IsSelected = true;
        displayedOutputsAnchorable.IsActive = true;
        ApplyBottomPanePresentation();
    }

    public bool IsDisplayedOutputsPaneSelected => displayedOutputsAnchorable.IsSelected && displayedOutputsAnchorable.IsActive;

    public void ActivateProfilePane()
    {
        ShowValidationSupportTab(profileAnchorable);
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
        ShowValidationSupportTab(fitDiagnosticsAnchorable);
        if (!IsBottomPaneExpanded) IsBottomPaneExpanded = true;
        fitDiagnosticsAnchorable.IsSelected = true;
        fitDiagnosticsAnchorable.IsActive = true;
    }

    public bool IsFitDiagnosticsPaneSelected => fitDiagnosticsAnchorable.IsSelected && fitDiagnosticsAnchorable.IsActive;

    public void ActivateIntersectionEvidencePane()
    {
        ShowValidationSupportTab(intersectionEvidenceAnchorable);
        if (!IsBottomPaneExpanded) IsBottomPaneExpanded = true;
        intersectionEvidenceAnchorable.IsSelected = true;
        intersectionEvidenceAnchorable.IsActive = true;
    }

    public bool IsIntersectionEvidencePaneSelected => intersectionEvidenceAnchorable.IsSelected && intersectionEvidenceAnchorable.IsActive;

    public void ActivateCorrespondenceEvidencePane()
    {
        ShowValidationSupportTab(correspondenceEvidenceAnchorable);
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
            view.ApplyAdaptiveAuthoringTitles();
        }
    }

    private static void OnToolLibraryTitleChanged(DependencyObject owner, DependencyPropertyChangedEventArgs args)
    {
        if (owner is OpenVisionDockWorkspaceView view && view.toolLibraryAnchorable is not null)
        {
            view.ApplyAdaptiveAuthoringTitles();
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
            view.ApplyAdaptiveAuthoringTitles();
        }
    }

    private static void OnCompactAuthoringTitleChanged(DependencyObject owner, DependencyPropertyChangedEventArgs args)
    {
        if (owner is OpenVisionDockWorkspaceView view && view.dataLayersAnchorable is not null)
        {
            view.ApplyAdaptiveAuthoringTitles();
        }
    }

    private static void OnEvidenceTitleChanged(DependencyObject owner, DependencyPropertyChangedEventArgs args)
    {
        if (owner is OpenVisionDockWorkspaceView view && view.evidenceAnchorable is not null)
        {
            view.ApplyAdaptiveEvidenceTitle();
        }
    }

    private static void OnValidationEvidenceTitleChanged(DependencyObject owner, DependencyPropertyChangedEventArgs args)
    {
        if (owner is OpenVisionDockWorkspaceView view && view.evidenceAnchorable is not null)
        {
            view.ApplyAdaptiveEvidenceTitle();
        }
    }

    private static void OnOutputCompareTitleChanged(DependencyObject owner, DependencyPropertyChangedEventArgs args)
    {
        if (owner is OpenVisionDockWorkspaceView view && view.outputCompareAnchorable is not null)
        {
            view.outputCompareAnchorable.Title = args.NewValue as string ?? string.Empty;
        }
    }

    private static void OnOutputCompareViewerActiveChanged(
        DependencyObject owner,
        DependencyPropertyChangedEventArgs args)
    {
        if (owner is not OpenVisionDockWorkspaceView view || !view.IsLoaded)
        {
            return;
        }

        if (args.NewValue is true)
        {
            view.RememberCurrentStageRatios(includeOutputCompare: true);
            view.ApplyOutputCompareDisplayedOutputsVisibility();
        }

        view.ApplyResponsiveDockLayout(view.ActualWidth, rememberCurrent: false);
        if (args.NewValue is false)
        {
            view.ApplyOutputCompareDisplayedOutputsVisibility();
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
        ApplyAdaptiveAuthoringTitles();
        viewerAnchorable.Title = ViewerTitle;
        resultsAnchorable.Title = ResultsTitle;
        ApplyAdaptiveEvidenceTitle();
        outputCompareAnchorable.Title = OutputCompareTitle;
        displayedOutputsAnchorable.Title = DisplayedOutputsTitle;
        linkedViewAnchorable.Title = LinkedViewTitle;
        profileAnchorable.Title = ProfileTitle;
        fitDiagnosticsAnchorable.Title = FitDiagnosticsTitle;
        intersectionEvidenceAnchorable.Title = IntersectionEvidenceTitle;
        correspondenceEvidenceAnchorable.Title = CorrespondenceEvidenceTitle;
    }

    private void ApplyAdaptiveAuthoringTitles()
    {
        toolLibraryAnchorable.Title = dataLayersTabbedForCompactLayout
            ? CompactToolLibraryTitle
            : ToolLibraryTitle;
        dataLayersAnchorable.Title = dataLayersTabbedForCompactLayout
            ? CompactDataLayersTitle
            : DataLayersTitle;
        toolInspectorAnchorable.Title = dataLayersTabbedForCompactLayout
            ? CompactToolInspectorTitle
            : ToolInspectorTitle;
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

    private void ApplyResponsiveDockLayout(
        double width,
        bool rememberCurrent = true)
    {
        if (width <= 0)
        {
            return;
        }

        if (rememberCurrent)
        {
            RememberCurrentStageRatios();
        }

        var ownerWidth = Window.GetWindow(this)?.ActualWidth ?? width;
        var useCompactLayout = ownerWidth < CompactWorkbenchWidth;
        resultsPane.DockMinHeight = IsOutputCompareViewerActive
            && operatorStage is (OpenVisionOperatorStage.Setup or OpenVisionOperatorStage.Teach)
                ? 0
                : authoringResultsDefaultDockMinHeight;
        if (operatorStage is (OpenVisionOperatorStage.Setup or OpenVisionOperatorStage.Teach)
            && useCompactLayout == dataLayersTabbedForCompactLayout
            && ReferenceEquals(viewerResultsColumn.Parent, workbenchPane)
            && viewerResultsColumn.Children.Count == 2
            && ReferenceEquals(viewerResultsColumn.Children[0], primaryPane)
            && ReferenceEquals(viewerResultsColumn.Children[1], resultsPane))
        {
            var layout = useCompactLayout ? compactLayout : wideLayout;
            if (useCompactLayout)
            {
                var viewerColumnWidth = layout.AuthoringInspector + layout.AuthoringViewer;
                dataLayersPane.DockWidth = new GridLength(
                    Math.Max(layout.AuthoringSupport, viewerColumnWidth * CompactAuthoringSupportToViewerRatio),
                    GridUnitType.Star);
                viewerResultsColumn.DockWidth = new GridLength(viewerColumnWidth, GridUnitType.Star);
            }
            else
            {
                dataLayersPane.DockWidth = new GridLength(layout.AuthoringSupport, GridUnitType.Star);
                toolInspectorPane.DockWidth = new GridLength(layout.AuthoringInspector, GridUnitType.Star);
                viewerResultsColumn.DockWidth = new GridLength(layout.AuthoringViewer, GridUnitType.Star);
            }

            primaryPane.DockHeight = IsOutputCompareViewerActive
                ? new GridLength(1, GridUnitType.Star)
                : new GridLength(
                    useCompactLayout
                        ? CompactAuthoringResultsWorkbenchHeightRatio
                        : AuthoringResultsWorkbenchHeightRatio,
                    GridUnitType.Star);
            resultsPane.DockHeight = new GridLength(
                IsOutputCompareViewerActive ? CompareResultsStarHeight : 1,
                GridUnitType.Star);
            resultsAnchorable.IsSelected = !IsOutputCompareViewerActive;
            toolInspectorAnchorable.IsSelected = true;
            ApplyOutputCompareResultsVisibility();
            ApplyBottomPanePresentation();
            return;
        }

        dataLayersTabbedForCompactLayout = useCompactLayout;
        ApplyAdaptiveAuthoringTitles();
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
                ComposeValidateStage(useCompactLayout);
                break;
            case OpenVisionOperatorStage.Results:
                ComposeResultsStage(useCompactLayout);
                break;
            default:
                ComposeLegacyLayout(useCompactLayout);
                break;
        }

        ApplyBottomPanePresentation();
        ApplyOutputCompareResultsVisibility();
    }

    private void ApplyOutputCompareDisplayedOutputsVisibility()
    {
        if (IsOutputCompareViewerActive)
        {
            if (displayedOutputsVisibilityBeforeCompare is null)
            {
                displayedOutputsVisibilityBeforeCompare = displayedOutputsAnchorable.IsVisible;
                displayedOutputsWasSelectedBeforeCompare =
                    displayedOutputsAnchorable.IsSelected && displayedOutputsAnchorable.IsActive;
            }

            if (displayedOutputsAnchorable.IsVisible)
            {
                displayedOutputsAnchorable.Hide();
            }

            viewerAnchorable.IsSelected = true;
            viewerAnchorable.IsActive = true;
            return;
        }

        if (displayedOutputsVisibilityBeforeCompare is not bool wasVisible)
        {
            return;
        }

        if (wasVisible)
        {
            displayedOutputsAnchorable.Show();
        }

        var selectedPane = displayedOutputsWasSelectedBeforeCompare
            ? displayedOutputsAnchorable
            : viewerAnchorable;
        selectedPane.IsSelected = true;
        selectedPane.IsActive = true;
        displayedOutputsVisibilityBeforeCompare = null;
        displayedOutputsWasSelectedBeforeCompare = false;
    }

    private void ApplyOutputCompareResultsVisibility()
    {
        var hideResults = IsOutputCompareViewerActive
            && operatorStage is (OpenVisionOperatorStage.Setup or OpenVisionOperatorStage.Teach);
        var visibility = hideResults ? Visibility.Collapsed : Visibility.Visible;
        var resultsControl = FindVisualDescendants<AvalonDock.Controls.LayoutAnchorablePaneControl>(workspaceDockingManager)
            .FirstOrDefault(paneControl => ReferenceEquals(paneControl.Model, resultsPane));
        if (resultsControl is null)
        {
            RestoreOutputCompareResultsRows();
            return;
        }

        var resultsColumnControl = VisualTreeHelper.GetParent(resultsControl);
        if (hideResults && resultsColumnControl is Grid resultsGrid)
        {
            if (!ReferenceEquals(outputCompareResultsGrid, resultsGrid))
            {
                RestoreOutputCompareResultsRows();
                var resultsRowIndex = Grid.GetRow(resultsControl);
                if (resultsRowIndex > 0 && resultsRowIndex < resultsGrid.RowDefinitions.Count)
                {
                    outputCompareResultsGrid = resultsGrid;
                    outputCompareResultsRow = resultsGrid.RowDefinitions[resultsRowIndex];
                    outputCompareResultsDefaultRowMinHeight = outputCompareResultsRow.MinHeight;
                    outputCompareResultsSplitterRow = resultsGrid.RowDefinitions[resultsRowIndex - 1];
                    outputCompareResultsDefaultSplitterHeight = outputCompareResultsSplitterRow.Height;
                }
            }

            if (outputCompareResultsRow is not null)
            {
                outputCompareResultsRow.MinHeight = 0;
                outputCompareResultsSplitterRow!.Height = new GridLength(0);
            }
        }
        else
        {
            RestoreOutputCompareResultsRows();
        }

        if (resultsControl.Visibility != visibility)
        {
            resultsControl.Visibility = visibility;
        }

        if (resultsColumnControl is not null)
        {
            foreach (var splitter in FindVisualDescendants<AvalonDock.Controls.LayoutGridResizerControl>(resultsColumnControl)
                         .Where(splitter => ReferenceEquals(VisualTreeHelper.GetParent(splitter), resultsColumnControl)))
            {
                if (splitter.Visibility != visibility)
                {
                    splitter.Visibility = visibility;
                }
            }
        }
    }

    private void RestoreOutputCompareResultsRows()
    {
        if (outputCompareResultsRow is not null)
        {
            outputCompareResultsRow.MinHeight = outputCompareResultsDefaultRowMinHeight;
            outputCompareResultsSplitterRow!.Height = outputCompareResultsDefaultSplitterHeight;
        }

        outputCompareResultsGrid = null;
        outputCompareResultsRow = null;
        outputCompareResultsSplitterRow = null;
    }

    private static IEnumerable<T> FindVisualDescendants<T>(DependencyObject parent)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match)
            {
                yield return match;
            }

            foreach (var descendant in FindVisualDescendants<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private IReadOnlyList<LayoutAnchorable> ValidationPaneAnchorables =>
    [
        evidenceAnchorable,
        outputCompareAnchorable,
        linkedViewAnchorable,
        profileAnchorable,
        fitDiagnosticsAnchorable,
        intersectionEvidenceAnchorable,
        correspondenceEvidenceAnchorable,
    ];

    private void ApplyStageSupportPaneAvailability()
    {
        var filterWasActive = IsValidationTabFilterActive;
        var filterIsActive =
            operatorStage == OpenVisionOperatorStage.Validate;
        IsValidationTabFilterActive = filterIsActive;
        ApplyAdaptiveEvidenceTitle();
        if (!filterIsActive)
        {
            ValidationVisibleSupportContentId = null;
            return;
        }

        if (!filterWasActive)
        {
            ValidationVisibleSupportContentId = null;
            evidenceAnchorable.IsSelected = true;
        }
    }

    private void ShowValidationSupportTab(LayoutAnchorable anchorable)
    {
        if (operatorStage == OpenVisionOperatorStage.Validate)
        {
            ValidationVisibleSupportContentId = anchorable.ContentId;
        }
    }

    private void ApplyAdaptiveEvidenceTitle() =>
        evidenceAnchorable.Title = IsValidationTabFilterActive
            ? ValidationEvidenceTitle
            : EvidenceTitle;

    private void ComposeSetupStage(bool compact)
        => ComposeTeachStage(compact);

    private void ComposeTeachStage(bool compact)
    {
        var layout = compact ? compactLayout : wideLayout;
        dataLayersPane.DockMinWidth = compact ? 0 : WideAuthoringSupportMinimumWidth;
        toolInspectorPane.DockMinWidth = compact ? 0 : WideAuthoringInspectorMinimumWidth;

        if (IsOutputCompareViewerActive)
        {
            resultsPane.DockHeight = new GridLength(CompareResultsStarHeight, GridUnitType.Star);
            resultsPane.DockMinHeight = 0;
            if (compact)
            {
                MoveAnchorable(toolInspectorAnchorable, dataLayersPane);
                var viewerColumnWidth = layout.AuthoringInspector + layout.AuthoringViewer;
                var authoringWidth = Math.Max(
                    layout.AuthoringSupport,
                    viewerColumnWidth * CompactAuthoringSupportToViewerRatio);
                AttachPane(dataLayersPane, 0);
                dataLayersPane.DockWidth = new GridLength(authoringWidth, GridUnitType.Star);
                viewerResultsColumn.DockWidth = new GridLength(viewerColumnWidth, GridUnitType.Star);
                AttachPane(primaryPane, viewerResultsColumn, 0);
                AttachPane(resultsPane, viewerResultsColumn, 1);
                AttachPanel(viewerResultsColumn, workbenchPane, 1);
            }
            else
            {
                AttachPane(dataLayersPane, 0);
                AttachPane(toolInspectorPane, 1);
                AttachPane(primaryPane, viewerResultsColumn, 0);
                AttachPane(resultsPane, viewerResultsColumn, 1);
                AttachPanel(viewerResultsColumn, workbenchPane, 2);
                dataLayersPane.DockWidth = new GridLength(layout.AuthoringSupport, GridUnitType.Star);
                toolInspectorPane.DockWidth = new GridLength(layout.AuthoringInspector, GridUnitType.Star);
                viewerResultsColumn.DockWidth = new GridLength(layout.AuthoringViewer, GridUnitType.Star);
            }

            primaryPane.DockHeight = new GridLength(1, GridUnitType.Star);
            resultsAnchorable.IsSelected = false;
            toolInspectorAnchorable.IsSelected = true;
            return;
        }

        if (compact)
        {
            MoveAnchorable(toolInspectorAnchorable, dataLayersPane);
        }

        toolLibraryAnchorable.IsSelected = false;
        dataLayersAnchorable.IsSelected = true;
        viewerAnchorable.IsSelected = true;
        resultsAnchorable.IsSelected = true;
        AttachPane(dataLayersPane, 0);
        if (compact)
        {
            var viewerColumnWidth = layout.AuthoringInspector + layout.AuthoringViewer;
            var authoringWidth = Math.Max(
                layout.AuthoringSupport,
                viewerColumnWidth * CompactAuthoringSupportToViewerRatio);
            AttachPane(primaryPane, viewerResultsColumn, 0);
            AttachPane(resultsPane, viewerResultsColumn, 1);
            AttachPanel(viewerResultsColumn, workbenchPane, 1);
            dataLayersPane.DockWidth = new GridLength(
                authoringWidth,
                GridUnitType.Star);
            viewerResultsColumn.DockWidth = new GridLength(
                viewerColumnWidth,
                GridUnitType.Star);
            primaryPane.DockHeight = new GridLength(
                CompactAuthoringResultsWorkbenchHeightRatio,
                GridUnitType.Star);
            resultsPane.DockHeight = new GridLength(1, GridUnitType.Star);
            toolInspectorAnchorable.IsSelected = true;
            return;
        }

        AttachPane(toolInspectorPane, 1);
        AttachPane(primaryPane, viewerResultsColumn, 0);
        AttachPane(resultsPane, viewerResultsColumn, 1);
        AttachPanel(viewerResultsColumn, workbenchPane, 2);
        dataLayersPane.DockWidth = new GridLength(
            layout.AuthoringSupport,
            GridUnitType.Star);
        toolInspectorPane.DockWidth = new GridLength(
            layout.AuthoringInspector,
            GridUnitType.Star);
        viewerResultsColumn.DockWidth = new GridLength(
            layout.AuthoringViewer,
            GridUnitType.Star);
        primaryPane.DockHeight = new GridLength(
            AuthoringResultsWorkbenchHeightRatio,
            GridUnitType.Star);
        resultsPane.DockHeight = new GridLength(1, GridUnitType.Star);
        toolInspectorAnchorable.IsSelected = true;
    }

    private void ComposeValidateStage(bool compact)
    {
        var layout = compact ? compactLayout : wideLayout;
        var evidenceWidth = compact
            ? Math.Max(
                layout.ValidateEvidence,
                layout.ValidateViewer
                * CompactValidationEvidenceToViewerRatio)
            : layout.ValidateEvidence;
        AttachPane(evidencePane, 0);
        AttachPane(primaryPane, 1);
        evidencePane.DockWidth = new GridLength(
            compact ? evidenceWidth : Math.Min(evidenceWidth, layout.ValidateViewer * 0.32),
            GridUnitType.Star);
        primaryPane.DockWidth = new GridLength(
            layout.ValidateViewer,
            GridUnitType.Star);
        evidenceAnchorable.IsSelected = true;
        viewerAnchorable.IsSelected = true;
    }

    private void ComposeResultsStage(bool compact)
    {
        var layout = compact ? compactLayout : wideLayout;
        AttachPane(resultsPane, 0);
        AttachPane(primaryPane, 1);
        resultsPane.DockWidth = new GridLength(
            layout.ResultsEvidence,
            GridUnitType.Star);
        primaryPane.DockWidth = new GridLength(
            layout.ResultsViewer,
            GridUnitType.Star);
        resultsAnchorable.IsSelected = true;
        viewerAnchorable.IsSelected = true;
    }

    private void ComposeLegacyLayout(bool compact)
    {
        var layout = compact ? compactLayout : wideLayout;
        dataLayersPane.DockMinWidth = 0;
        toolInspectorPane.DockMinWidth = 0;
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
        toolLibraryPane.DockWidth = new GridLength(
            layout.LegacyToolLibrary,
            GridUnitType.Star);
        dataLayersPane.DockWidth = new GridLength(
            layout.LegacyDataLayers,
            GridUnitType.Star);
        primaryPane.DockWidth = new GridLength(
            layout.LegacyViewer,
            GridUnitType.Star);
        toolInspectorPane.DockWidth = new GridLength(
            layout.LegacyInspector,
            GridUnitType.Star);
    }

    private void RememberCurrentStageRatios(bool includeOutputCompare = false)
    {
        if (workbenchPane.Children.Count == 0
            || IsOutputCompareViewerActive && !includeOutputCompare)
        {
            return;
        }

        if (operatorStage is OpenVisionOperatorStage.Setup or OpenVisionOperatorStage.Teach)
        {
            preferredSupportContentId = toolLibraryAnchorable.IsSelected
                ? toolLibraryAnchorable.ContentId
                : dataLayersAnchorable.ContentId;
        }

        var layout = dataLayersTabbedForCompactLayout
            ? compactLayout
            : wideLayout;
        if (operatorStage is OpenVisionOperatorStage.Setup or OpenVisionOperatorStage.Teach
            && dataLayersTabbedForCompactLayout
            && HasAuthoringPaneComposition)
        {
            var previousViewerColumn = layout.AuthoringInspector + layout.AuthoringViewer;
            var currentViewerColumn = StarValue(viewerResultsColumn.DockWidth, previousViewerColumn);
            var viewerColumnScale = currentViewerColumn / previousViewerColumn;
            layout = layout with
            {
                AuthoringSupport = StarValue(dataLayersPane.DockWidth, layout.AuthoringSupport),
                AuthoringInspector = StarValue(layout.AuthoringInspector * viewerColumnScale, layout.AuthoringInspector),
                AuthoringViewer = StarValue(layout.AuthoringViewer * viewerColumnScale, layout.AuthoringViewer),
            };
        }
        else
        {
            layout = operatorStage switch
            {
                OpenVisionOperatorStage.Setup or OpenVisionOperatorStage.Teach
                    when HasAuthoringPaneComposition => layout with
                    {
                        AuthoringSupport = StarValue(dataLayersPane.DockWidth, layout.AuthoringSupport),
                        AuthoringInspector = StarValue(toolInspectorPane.DockWidth, layout.AuthoringInspector),
                        AuthoringViewer = StarValue(viewerResultsColumn.DockWidth, layout.AuthoringViewer),
                    },
                OpenVisionOperatorStage.Validate when HasValidateStageComposition =>
                    layout with
                    {
                        ValidateEvidence = StarValue(evidencePane.DockWidth, layout.ValidateEvidence),
                        ValidateViewer = StarValue(primaryPane.DockWidth, layout.ValidateViewer),
                    },
                OpenVisionOperatorStage.Results when HasResultsStageComposition =>
                    layout with
                    {
                        ResultsEvidence = StarValue(resultsPane.DockWidth, layout.ResultsEvidence),
                        ResultsViewer = StarValue(primaryPane.DockWidth, layout.ResultsViewer),
                    },
                OpenVisionOperatorStage.Legacy => layout with
                {
                    LegacyToolLibrary = StarValue(toolLibraryPane.DockWidth, layout.LegacyToolLibrary),
                    LegacyDataLayers = StarValue(dataLayersPane.DockWidth, layout.LegacyDataLayers),
                    LegacyInspector = StarValue(toolInspectorPane.DockWidth, layout.LegacyInspector),
                    LegacyViewer = StarValue(primaryPane.DockWidth, layout.LegacyViewer),
                },
                _ => layout,
            };
        }

        if (dataLayersTabbedForCompactLayout)
        {
            compactLayout = layout;
        }
        else
        {
            wideLayout = layout;
        }
    }

    private static OpenVisionDockLayoutVariant SanitizeVariant(
        OpenVisionDockLayoutVariant? candidate,
        OpenVisionDockLayoutVariant fallback)
    {
        if (candidate is null)
        {
            return fallback;
        }

        return new OpenVisionDockLayoutVariant(
            StarValue(candidate.AuthoringSupport, fallback.AuthoringSupport),
            StarValue(candidate.AuthoringInspector, fallback.AuthoringInspector),
            StarValue(candidate.AuthoringViewer, fallback.AuthoringViewer),
            StarValue(candidate.ValidateEvidence, fallback.ValidateEvidence),
            StarValue(candidate.ValidateViewer, fallback.ValidateViewer),
            StarValue(candidate.ResultsEvidence, fallback.ResultsEvidence),
            StarValue(candidate.ResultsViewer, fallback.ResultsViewer),
            StarValue(candidate.LegacyToolLibrary, fallback.LegacyToolLibrary),
            StarValue(candidate.LegacyDataLayers, fallback.LegacyDataLayers),
            StarValue(candidate.LegacyInspector, fallback.LegacyInspector),
            StarValue(candidate.LegacyViewer, fallback.LegacyViewer));
    }

    private static double StarValue(GridLength length, double fallback) =>
        length.IsStar ? StarValue(length.Value, fallback) : fallback;

    private static double StarValue(double value, double fallback) =>
        double.IsFinite(value) && value is >= 0.20 and <= 8.00
            ? value
            : fallback;

    private void SelectPrimaryContent(string contentId)
    {
        var target = string.Equals(
            contentId,
            displayedOutputsAnchorable.ContentId,
            StringComparison.Ordinal)
            ? displayedOutputsAnchorable
            : viewerAnchorable;
        target.IsSelected = true;
        primaryPane.SelectedContentIndex = primaryPane.Children.IndexOf(target);
    }

    private void SelectSupportContent(string contentId)
    {
        var target = string.Equals(
            contentId,
            toolLibraryAnchorable.ContentId,
            StringComparison.Ordinal)
            ? toolLibraryAnchorable
            : dataLayersAnchorable;
        preferredSupportContentId = target.ContentId;
        target.IsSelected = true;
        toolLibraryAnchorable.IsSelected = ReferenceEquals(target, toolLibraryAnchorable);
        dataLayersAnchorable.IsSelected = ReferenceEquals(target, dataLayersAnchorable);
        if (target.Parent is LayoutAnchorablePane pane)
        {
            pane.SelectedContentIndex = pane.Children.IndexOf(target);
        }
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
                   })
        {
            if (pane.Parent is AvalonDock.Layout.LayoutPanel parent)
            {
                parent.Children.Remove(pane);
            }
        }

        if (ReferenceEquals(evidencePane.Parent, workbenchPane))
        {
            workbenchPane.Children.Remove(evidencePane);
        }

        foreach (var panel in new[]
                 {
                     viewerResultsColumn,
                 })
        {
            if (panel.Parent is AvalonDock.Layout.LayoutPanel parent)
            {
                parent.Children.Remove(panel);
            }

            panel.Children.Clear();
        }
    }

    private void AttachPane(
        AvalonDock.Layout.LayoutAnchorablePane pane,
        int index)
        => AttachPane(pane, workbenchPane, index);

    private static void AttachPane(
        AvalonDock.Layout.LayoutAnchorablePane pane,
        AvalonDock.Layout.LayoutPanel target,
        int index)
    {
        if (ReferenceEquals(pane.Parent, target))
        {
            var currentIndex = target.Children.IndexOf(pane);
            if (currentIndex == index)
            {
                return;
            }

            target.Children.Remove(pane);
        }
        else if (pane.Parent is AvalonDock.Layout.LayoutPanel parent)
        {
            parent.Children.Remove(pane);
        }

        target.Children.Insert(Math.Min(index, target.Children.Count), pane);
    }

    private static void AttachPanel(
        AvalonDock.Layout.LayoutPanel panel,
        AvalonDock.Layout.LayoutPanel target,
        int index)
    {
        if (ReferenceEquals(panel.Parent, target))
        {
            var currentIndex = target.Children.IndexOf(panel);
            if (currentIndex == index)
            {
                return;
            }

            target.Children.Remove(panel);
        }
        else if (panel.Parent is AvalonDock.Layout.LayoutPanel parent)
        {
            parent.Children.Remove(panel);
        }

        target.Children.Insert(Math.Min(index, target.Children.Count), panel);
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
