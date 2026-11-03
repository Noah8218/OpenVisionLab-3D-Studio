using System.Collections.Specialized;
using System.ComponentModel;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using OpenVisionLab.ThreeD.Docking.Controls;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Viewer;
using OpenVisionLab.ThreeD.Viewer.Hosting;

namespace OpenVisionLab.ThreeD.Shell.Views.Workbench;

public sealed partial class ToolRecipeWorkbenchView : UserControl, IDisposable
{
    private ViewerWorkspaceView? viewerWorkspaceSurface;
    private RecipePipelineReviewView? pipelineReview;
    private ShellMainWindowViewModel? shell;
    private DispatcherOperation? teachStageActivationOperation;
    private IInputElement? toolLibraryReturnFocus;
    private int toolLibraryStepCountAtOpen;
    private int disposalState;
    private bool? isCompactDataLayout;

    private static readonly DependencyPropertyKey IsCompactLayoutPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(IsCompactLayout), typeof(bool), typeof(ToolRecipeWorkbenchView), new PropertyMetadata(false));

    public static readonly DependencyProperty IsCompactLayoutProperty = IsCompactLayoutPropertyKey.DependencyProperty;

    public static readonly DependencyProperty IsTeachingDataPinnedProperty = DependencyProperty.Register(
        nameof(IsTeachingDataPinned), typeof(bool), typeof(ToolRecipeWorkbenchView), new PropertyMetadata(false));

    public bool IsTeachingDataPinned
    {
        get => (bool)GetValue(IsTeachingDataPinnedProperty);
        set => SetCurrentValue(IsTeachingDataPinnedProperty, value);
    }

    public static CustomPopupPlacementCallback TeachingDataPlacement { get; } = (popup, target, offset) =>
        [new(new Point(-popup.Width - 6 + offset.X, offset.Y), PopupPrimaryAxis.Vertical)];

    public static readonly DependencyProperty IsTeachingDataExpandedProperty = DependencyProperty.Register(
        nameof(IsTeachingDataExpanded), typeof(bool), typeof(ToolRecipeWorkbenchView), new PropertyMetadata(true));

    public static readonly DependencyProperty ViewerContentProperty =
        DependencyProperty.Register(
            nameof(ViewerContent),
            typeof(object),
            typeof(ToolRecipeWorkbenchView),
            new PropertyMetadata(null, OnViewerContentChanged));

    public static readonly DependencyProperty WorkspaceSelectionCommandProperty =
        DependencyProperty.Register(
            nameof(WorkspaceSelectionCommand),
            typeof(ICommand),
            typeof(ToolRecipeWorkbenchView),
            new PropertyMetadata(null));

    public ToolRecipeWorkbenchView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += OnWorkbenchSizeChanged;
        SetBinding(
            WorkspaceSelectionCommandProperty,
            new Binding("DataContext.SelectWorkspaceCommand") { Source = this });
        BindStageHostedContext(ToolLibraryDialogContent, "DataContext.Workbench");
        BindStageHostedContext(DockWorkspace.DataLayersContent, "DataContext.Workbench");
        BindStageHostedContext(DockWorkspace.ValidationSamplesContent, "DataContext.Workbench");
        if (DockWorkspace.ValidationSamplesContent is ValidationSamplesView samples)
            samples.SetBinding(ValidationSamplesView.DockPaneCommandsProperty,
                new Binding(nameof(OpenVisionDockWorkspaceView.DataLayersPaneLayoutItem)) { Source = DockWorkspace });
        BindStageHostedContext(RecipeDataSurface, "DataContext.Workbench");
        BindStageHostedContext(RecipeDataPopupSurface, "DataContext.Workbench");
        TeachingDataPopupHeader.SetBinding(ContentControl.ContentProperty, new Binding("DataContext") { Source = this });
        TeachingDataPopupHeader.Tag = this;
        BindStageHostedContext(DockWorkspace.ViewerContent, "DataContext");
        BindStageHostedContext(DockWorkspace.ResultsContent, "DataContext");
        if (DockWorkspace.ResultsContent is ResultsWorkspaceView resultsView)
        {
            resultsView.SetBinding(ResultsWorkspaceView.DockPaneCommandsProperty,
                new Binding(nameof(OpenVisionDockWorkspaceView.ResultsPaneLayoutItem)) { Source = DockWorkspace });
        }
        BindStageHostedContext(DockWorkspace.ToolInspectorContent, "DataContext.Workbench");
        BindStageHostedContext(DockWorkspace.EvidenceContent, "DataContext.Workbench");
        BindStageHostedContext(DockWorkspace.OutputCompareContent, "DataContext.Workbench");
        BindStageHostedContext(DockWorkspace.DisplayedOutputsContent, "DataContext.Workbench");
        BindStageHostedContext(DockWorkspace.LinkedViewContent, "DataContext.Workbench");
        if (DockWorkspace.FitDiagnosticsContent is LineFitDiagnosticsView fitDiagnosticsView)
        {
            fitDiagnosticsView.SetBinding(
                DataContextProperty,
                new Binding("DataContext.Workbench") { Source = this });
        }
        if (DockWorkspace.IntersectionEvidenceContent is LineIntersectionEvidenceView intersectionEvidenceView)
        {
            intersectionEvidenceView.SetBinding(
                DataContextProperty,
                new Binding("DataContext.Workbench") { Source = this });
        }
        if (DockWorkspace.CorrespondenceEvidenceContent is LandmarkCorrespondenceEvidenceView correspondenceEvidenceView)
        {
            correspondenceEvidenceView.SetBinding(
                FrameworkElement.DataContextProperty,
                new Binding("DataContext.Workbench") { Source = this });
        }
        if (DockWorkspace.EvidenceContent is RecipePipelineReviewView review)
        {
            pipelineReview = review;
            review.SetBinding(RecipePipelineReviewView.DockPaneCommandsProperty,
                new Binding(nameof(OpenVisionDockWorkspaceView.EvidencePaneLayoutItem)) { Source = DockWorkspace });
            review.SetBinding(
                RecipePipelineReviewView.RunRecordContextProperty,
                new Binding("DataContext") { Source = this });
            review.ActiveReviewChanged += OnActiveReviewChanged;
            DockWorkspace.SetEvidenceAnalysisHeight(review.IsValidationSetSelected);
        }
    }

    /// <summary>
    /// Releases resources owned by nested viewer workspace surfaces when the
    /// containing Shell Window closes. Unloaded remains a reversible WPF event.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposalState, 1) != 0)
        {
            return;
        }

        CancelTeachStageActivation();
        if (pipelineReview is not null)
        {
            pipelineReview.ActiveReviewChanged -= OnActiveReviewChanged;
            pipelineReview = null;
        }
        DetachShell();
        if (DockWorkspace.ResultsContent is ResultsWorkspaceView resultsWorkspace)
        {
            resultsWorkspace.Dispose();
        }
        if (DockWorkspace.OutputCompareContent is OutputCompareView outputCompare)
        {
            outputCompare.Dispose();
        }
        if (DockWorkspace.LinkedViewContent is RunLogView runLog)
        {
            runLog.Dispose();
        }
        if (DockWorkspace.ProfileContent is HeightProfileView profileView)
        {
            profileView.Dispose();
        }
        ViewerWorkspaceSurface?.Dispose();
        DataContextChanged -= OnDataContextChanged;
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
        SizeChanged -= OnWorkbenchSizeChanged;
    }

    public bool IsTeachingDataExpanded
    {
        get => (bool)GetValue(IsTeachingDataExpandedProperty);
        set => SetCurrentValue(IsTeachingDataExpandedProperty, value);
    }

    private void OnTeachingDataPinClicked(object sender, RoutedEventArgs args)
    {
        if (IsCompactLayout) IsTeachingDataPinned = sender is ToggleButton { IsChecked: true };
        else IsTeachingDataExpanded = false;
    }

    private void OnTeachingDataHeaderAction(object sender, RoutedEventArgs args)
    {
        if (IsCompactLayout) IsTeachingDataExpanded = false;
        else if (sender is FrameworkElement { ContextMenu: { } menu } button)
        {
            menu.PlacementTarget = button;
            menu.IsOpen = true;
        }
    }

    private void OnTeachingDataCollapseClicked(object sender, RoutedEventArgs args) => IsTeachingDataExpanded = false;

    private void OnTeachingDataPopupOpened(object? sender, EventArgs args)
    {
        RecipeDataPopupSurface.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
    }

    private void OnTeachingDataPopupClosed(object? sender, EventArgs args)
    {
        if (IsCompactLayout && !IsTeachingDataPinned) IsTeachingDataExpanded = false;
    }

    private void OnTeachingDataPopupKeyDown(object sender, KeyEventArgs args)
    {
        if (args.Key != Key.Escape || args.Handled) return;
        IsTeachingDataExpanded = false;
        TeachingDataToggle.Focus();
        args.Handled = true;
    }

    private void OnWorkbenchSizeChanged(object sender, SizeChangedEventArgs args)
    {
        var compact = (Window.GetWindow(this)?.ActualWidth ?? args.NewSize.Width) < 1500;
        SetValue(IsCompactLayoutPropertyKey, compact);
        if (compact != isCompactDataLayout)
        {
            isCompactDataLayout = compact;
            IsTeachingDataExpanded = !compact;
        }
    }

    public bool IsCompactLayout => (bool)GetValue(IsCompactLayoutProperty);

    public OpenVisionOperatorStage OperatorStage => DockWorkspace.OperatorStage;

    public ICommand? WorkspaceSelectionCommand
    {
        get => (ICommand?)GetValue(WorkspaceSelectionCommandProperty);
        set => SetValue(WorkspaceSelectionCommandProperty, value);
    }

    public bool HasSetupStageComposition => DockWorkspace.HasSetupStageComposition;

    public bool HasTeachStageComposition => DockWorkspace.HasTeachStageComposition;

    public bool HasAuthoringStageComposition =>
        DockWorkspace.HasAuthoringPaneComposition;

    public bool HasValidateOrResultsStageComposition =>
        DockWorkspace.HasValidateOrResultsStageComposition;

    public bool HasValidateStageComposition =>
        DockWorkspace.HasValidateStageComposition;

    public bool HasRecipeValidationStageComposition => DockWorkspace.HasRecipeValidationStageComposition;

    public int VisibleValidationPaneTabCount =>
        DockWorkspace.VisibleValidationPaneTabCount;

    public bool HasFocusedValidationPaneTabs =>
        DockWorkspace.HasFocusedValidationPaneTabs;

    public bool HasReadableValidationPaneRatio =>
        DockWorkspace.HasReadableValidationPaneRatio;

    public bool HasAllLegacySupportPaneTabs =>
        DockWorkspace.HasAllLegacySupportPaneTabs;

    public bool HasResultsStageComposition =>
        DockWorkspace.HasResultsStageComposition;

    public bool HasEvidenceLinkedViewerComposition =>
        DockWorkspace.HasEvidenceLinkedViewerComposition;

    public bool HasStableStageHostedDataContexts =>
        DataContext is ShellMainWindowViewModel currentShell
        && HasDataContext(ToolLibraryDialogContent, currentShell.Workbench)
        && HasDataContext(DockWorkspace.DataLayersContent, currentShell.Workbench)
        && HasDataContext(DockWorkspace.ValidationSamplesContent, currentShell.Workbench)
        && HasDataContext(RecipeDataSurface, currentShell.Workbench)
        && HasDataContext(DockWorkspace.ViewerContent, currentShell)
        && HasDataContext(DockWorkspace.ResultsContent, currentShell)
        && HasDataContext(DockWorkspace.ToolInspectorContent, currentShell.Workbench)
        && HasDataContext(DockWorkspace.EvidenceContent, currentShell.Workbench)
        && DockWorkspace.EvidenceContent is RecipePipelineReviewView
        {
            RunRecordContext: not null
        } review
        && ReferenceEquals(review.RunRecordContext, currentShell)
        && HasDataContext(DockWorkspace.OutputCompareContent, currentShell.Workbench)
        && HasDataContext(DockWorkspace.DisplayedOutputsContent, currentShell.Workbench)
        && HasDataContext(DockWorkspace.LinkedViewContent, currentShell.Workbench);

    public bool HasLocalizedValidationNavigation =>
        DockWorkspace.EvidenceContent is RecipePipelineReviewView
        {
            HasLocalizedValidationNavigation: true
        };

    public bool HasLocalizedResultsNavigation =>
        DockWorkspace.ResultsContent is ResultsWorkspaceView
        {
            HasLocalizedNavigationAndAdvancedRoute: true
        };

    public bool HasResultsOperatorSummary =>
        DockWorkspace.ResultsContent is ResultsWorkspaceView
        {
            HasOperatorSummaryAndCorrectionRoute: true
        };

    public bool HasAccessibleValidationSampleSetAction =>
        DockWorkspace.EvidenceContent is RecipePipelineReviewView
        {
            HasAccessibleValidationSampleSetAction: true
        };

    public bool HasValidationSamplesFirstUseClarity =>
        DockWorkspace.EvidenceContent is RecipePipelineReviewView
        {
            HasValidationSamplesFirstUseClarity: true
        };

    public bool HasValidationResultsReviewControls =>
        DockWorkspace.EvidenceContent is RecipePipelineReviewView
        {
            HasValidationResultsReviewControls: true
        };

    public bool IsValidationIssueNavigationVisible =>
        DockWorkspace.EvidenceContent is RecipePipelineReviewView
        {
            IsValidationIssueNavigationVisible: true
        };

    public bool HasValidationFailureOperatorSummary =>
        DockWorkspace.EvidenceContent is RecipePipelineReviewView
        {
            IsFailureOperatorSummaryVisible: true
        };

    public int ValidationSetSampleCount =>
        DataContext is ShellMainWindowViewModel currentShell
            ? currentShell.Workbench.ValidationSetSamples.Count
            : 0;

    public bool CanRunValidationSet =>
        DataContext is ShellMainWindowViewModel currentShell
        && currentShell.Workbench.RunValidationSetCommand.CanExecute(null);

    public bool IsDedicatedResultsWorkspace =>
        DockWorkspace.ResultsContent is ResultsWorkspaceView
        {
            IsReadOnlyComposition: true
        };

    public ResultsWorkspaceSection ActiveResultsWorkspaceSection =>
        DockWorkspace.ResultsContent is ResultsWorkspaceView results
            ? results.ActiveSection
            : ResultsWorkspaceSection.RunRecord;

    public void SetResultsWorkspaceSection(ResultsWorkspaceSection section)
    {
        if (DockWorkspace.ResultsContent is ResultsWorkspaceView results)
        {
            results.SetSection(section);
        }
    }

    public bool IsDedicatedValidationWorkspace =>
        DockWorkspace.EvidenceContent is RecipePipelineReviewView review
        && review.IsDedicatedValidationWorkspace;

    public ValidationWorkspaceSection ActiveValidationWorkspaceSection =>
        DockWorkspace.EvidenceContent is RecipePipelineReviewView review
            ? review.ValidationSection
            : ValidationWorkspaceSection.Samples;

    public void SetValidationWorkspaceSection(ValidationWorkspaceSection section)
    {
        if (DockWorkspace.EvidenceContent is RecipePipelineReviewView review)
        {
            review.SetValidationSection(section);
        }
    }

    public object? ViewerContent
    {
        get => GetValue(ViewerContentProperty);
        set => SetValue(ViewerContentProperty, value);
    }

    internal object? ProfileViewerDataContext =>
        (DockWorkspace.ProfileContent as HeightProfileView)?.DataContext;

    internal IOpenVisionThreeDViewerHost? ProfileViewerHost =>
        (DockWorkspace.ProfileContent as HeightProfileView)?.ViewerHost;

    public bool ReleaseMainViewer(object? requestedContent) =>
        ViewerWorkspaceSurface?.ReleaseMainViewer(requestedContent) == true;

    public IReadOnlyList<DockingPaneContract> GetDockingPaneContracts() =>
        DockWorkspace.GetDockingPaneContracts();

    public DockingFloatDockResult VerifyFirstPaneFloatDockRoundTrip() =>
        DockWorkspace.VerifyFirstPaneFloatDockRoundTrip();

    public OpenVisionDockPresentationState CaptureDockPresentationState() =>
        DockWorkspace.CapturePresentationState();

    public void ApplyDockPresentationState(
        OpenVisionDockPresentationState state) =>
        DockWorkspace.ApplyPresentationState(state);

    public void ResetDockPresentationState() =>
        DockWorkspace.ResetPresentationState();

    public bool IsBottomPaneExpanded
    {
        get => DockWorkspace.IsBottomPaneExpanded;
        set => DockWorkspace.IsBottomPaneExpanded = value;
    }

    public bool IsBottomPaneAttached => DockWorkspace.IsBottomPaneAttached;

    public bool IsCompactDockLayout => DockWorkspace.IsCompactLayout;

    public bool HasTopThemedDockTabs => DockWorkspace.HasTopThemedDockTabs;

    public bool HasSideCollapsibleTaskPanes =>
        DockWorkspace.HasSideCollapsibleTaskPanes;

    public (bool Collapsed, bool Restored) VerifySupportAutoHideRoundTrip() =>
        DockWorkspace.VerifySupportAutoHideRoundTrip();

    public bool IsToolInspectorPaneSelected =>
        DockWorkspace.IsToolInspectorPaneSelected;

    public bool HasRecipeFlowInspectorViewerOrder =>
        DockWorkspace.HasRecipeFlowInspectorViewerOrder;

    public bool HasRecipeHealthNavigation =>
        DockWorkspace.DataLayersContent is RecipeChainView
        {
            HasRecipeHealthNavigation: true
        };

    public bool IsViewerContextRibbonVisible =>
        FindLogicalChildByAutomationId<Border>(
            DockWorkspace.ViewerContent,
            "ViewerContextRibbon")?.Visibility == Visibility.Visible;

    public bool IsNoRecipeStepBannerVisible =>
        FindLogicalChildByAutomationId<Border>(
            DockWorkspace.ViewerContent,
            "NoRecipeStepBanner")?.Visibility == Visibility.Visible;

    public bool IsViewerInputFirstActionVisible =>
        ViewerWorkspaceSurface?.IsInputFirstActionVisible == true;

    public bool UsesInspectionWorkspaceV3Composition =>
        DockWorkspace.DataLayersContent is RecipeChainView
        && DockWorkspace.ToolInspectorContent is SelectedToolWorkspaceView
        && ViewerWorkspaceSurface is not null;

    public bool HasThicknessRepeatGridAuthoringControls =>
        DockWorkspace.ToolInspectorContent is SelectedToolWorkspaceView
        {
            HasThicknessRepeatGridAuthoringControls: true
        };

    public bool HasExplicitSelectedToolActions =>
        DockWorkspace.ToolInspectorContent is SelectedToolWorkspaceView
        {
            HasExplicitAuthoringActions: true
        };

    public bool HasContextualSelectedStepSetup =>
        DockWorkspace.ToolInspectorContent is SelectedToolWorkspaceView
        {
            IsContextualStepSetupVisible: true,
            HasDirectToolCatalogReturn: true,
            HasVisibleStepSetupStatus: true,
            VisibleContextualStepSetupActionCount: 0,
            HasExplicitAuthoringActions: true,
        };

    public string ContextualSelectedStepSetupState =>
        DockWorkspace.ToolInspectorContent is SelectedToolWorkspaceView selectedTool
            ? $"card={selectedTool.IsContextualStepSetupVisible}; catalogReturn={selectedTool.HasDirectToolCatalogReturn}; status={selectedTool.HasVisibleStepSetupStatus}; setupActions={selectedTool.VisibleContextualStepSetupActionCount}; commandBar={selectedTool.HasExplicitAuthoringActions}"
            : $"content={DockWorkspace.ToolInspectorContent?.GetType().Name ?? "null"}";

    public bool HasVisibleInspectionContext =>
        DockWorkspace.ToolInspectorContent is SelectedToolWorkspaceView
        {
            HasVisibleInspectionContext: true
        };

    public string InspectionContextState =>
        DockWorkspace.ToolInspectorContent is SelectedToolWorkspaceView selectedTool
            ? selectedTool.InspectionContextState
            : $"content={DockWorkspace.ToolInspectorContent?.GetType().Name ?? "null"}";

    public bool HasExclusiveSelectedToolWorkspaceSurface =>
        DockWorkspace.ToolInspectorContent is SelectedToolWorkspaceView
        {
            HasExclusiveWorkspaceSurface: true
        };

    public bool HasAdjacentViewerOutputs =>
        DockWorkspace.HasAdjacentViewerOutputs;

    public bool IsFailureCorrectionContextVisible =>
        DockWorkspace.ToolInspectorContent is SelectedToolWorkspaceView
        {
            IsFailureCorrectionContextVisible: true
        };

    public void ActivateSelectedToolPane()
    {
        CloseToolLibraryPane();
        DockWorkspace.ActivateToolInspectorPane();
    }

    public void ToggleSelectedToolSideCollapse() =>
        DockWorkspace.ToggleToolInspectorAutoHide();

    public bool IsSelectedToolSideCollapsed =>
        DockWorkspace.IsToolInspectorAutoHidden;

    public IReadOnlyList<string> GetSelectedToolVisibleTextLayout() =>
        DockWorkspace.ToolInspectorContent is SelectedToolWorkspaceView view
            ? view.GetVisibleTextLayout()
            : [];

    public bool HasDominantViewerWidth => DockWorkspace.HasDominantViewerWidth;

    public bool HasViewerWorkspaceLayoutToolbar =>
        ViewerWorkspaceSurface?.HasLayoutToolbarAndTwoSlots == true;

    public bool IsAuxiliaryViewerInlineVisible =>
        ViewerWorkspaceSurface?.IsAuxiliaryInlineVisible == true;

    private ViewerWorkspaceView? ViewerWorkspaceSurface =>
        viewerWorkspaceSurface ?? FindLogicalChild<ViewerWorkspaceView>(DockWorkspace.ViewerContent);

    internal IReadOnlyDictionary<string, OpenVisionLab.ThreeD.Data.HeightDisplayProfile> CaptureViewerDisplayProfiles() => ViewerWorkspaceSurface?.CaptureDisplayProfiles() ?? new Dictionary<string, OpenVisionLab.ThreeD.Data.HeightDisplayProfile>();
    internal void RestoreViewerDisplayProfiles(IReadOnlyDictionary<string, OpenVisionLab.ThreeD.Data.HeightDisplayProfile> profiles) => ViewerWorkspaceSurface?.RestoreDisplayProfiles(profiles);

    public bool IsViewerPopoutVisible => ViewerWorkspaceSurface?.IsPopoutVisible == true;

    public Window? ViewerPopoutWindow => ViewerWorkspaceSurface?.PopoutWindow;

    public bool ConfigureViewerWorkspaceLayoutForSmoke(string? layout)
    {
        if (DataContext is not ShellMainWindowViewModel shell)
        {
            return false;
        }

        var command = layout?.Trim().ToLowerInvariant() switch
        {
            "single" => shell.Workbench.SetSingleViewerLayoutCommand,
            "height" => shell.Workbench.ShowHeightImageOnlyCommand,
            "vertical" => shell.Workbench.SplitViewerVerticallyCommand,
            "horizontal" => shell.Workbench.SplitViewerHorizontallyCommand,
            "four" => shell.Workbench.SetFourViewerLayoutCommand,
            "popout" => shell.Workbench.PopOutViewerCommand,
            _ => null
        };
        if (command?.CanExecute(null) != true)
        {
            return false;
        }

        command.Execute(null);
        return layout?.Trim().ToLowerInvariant() switch
        {
            "single" => shell.Workbench.ViewerWorkspace.Layout == ViewerWorkspaceLayout.Single,
            "height" => shell.Workbench.ViewerWorkspace.Layout == ViewerWorkspaceLayout.HeightImageOnly,
            "vertical" => shell.Workbench.ViewerWorkspace.Layout == ViewerWorkspaceLayout.SplitVertical,
            "horizontal" => shell.Workbench.ViewerWorkspace.Layout == ViewerWorkspaceLayout.SplitHorizontal,
            "four" => shell.Workbench.ViewerWorkspace.Layout == ViewerWorkspaceLayout.Four,
            "popout" => shell.Workbench.ViewerWorkspace.Layout == ViewerWorkspaceLayout.PopOut
                        && ViewerWorkspaceSurface?.IsPopoutVisible == true,
            _ => false
        };
    }

    public bool ConfigureViewerWorkspacePresentationForSmoke()
    {
        if (DataContext is not ShellMainWindowViewModel shell
            || !ConfigureViewerWorkspaceLayoutForSmoke("vertical"))
        {
            return false;
        }

        var mainContentId = shell.Workbench.MainViewerContentId;
        if (string.IsNullOrWhiteSpace(mainContentId))
        {
            return false;
        }

        shell.Workbench.AuxiliaryViewerContentId = mainContentId;
        if (!string.Equals(
                shell.Workbench.AuxiliaryViewerContentId,
                mainContentId,
                StringComparison.OrdinalIgnoreCase)
            || !shell.Workbench.ToggleViewerCameraLinkCommand.CanExecute(null))
        {
            return false;
        }

        shell.Workbench.ToggleViewerCameraLinkCommand.Execute(null);
        return shell.Workbench.IsViewerCameraLinked;
    }

    public bool VerifyViewerWorkspaceCameraLinkForSmoke(out string summary)
    {
        if (ViewerWorkspaceSurface is null)
        {
            summary = "CameraLinkPropagation|failure=workspace-surface-unavailable";
            return false;
        }

        return ViewerWorkspaceSurface.VerifyLinkedCameraPropagationForSmoke(out summary);
    }

    public Task<HeightImageRoiPointerSmokeResult> RunHeightImageRoiPointerSmokeAsync() =>
        ViewerWorkspaceSurface?.RunHeightImageRoiPointerSmokeAsync()
        ?? Task.FromResult(new HeightImageRoiPointerSmokeResult(
            false,
            "The Viewer Workspace surface is unavailable.",
            null,
            null,
            default,
            default,
            string.Empty));

    private void ViewerWorkspaceSurface_Loaded(object sender, RoutedEventArgs args)
    {
        if (Volatile.Read(ref disposalState) != 0)
        {
            (sender as ViewerWorkspaceView)?.Dispose();
            return;
        }

        viewerWorkspaceSurface = sender as ViewerWorkspaceView;
    }

    private static T? FindLogicalChild<T>(object? parent)
        where T : DependencyObject
    {
        if (parent is T match)
        {
            return match;
        }

        if (parent is not DependencyObject dependencyObject)
        {
            return null;
        }

        foreach (var child in LogicalTreeHelper.GetChildren(dependencyObject))
        {
            if (FindLogicalChild<T>(child) is { } descendant)
            {
                return descendant;
            }
        }

        return null;
    }

    private static T? FindLogicalChildByAutomationId<T>(
        object? parent,
        string automationId)
        where T : DependencyObject
    {
        if (parent is T match
            && string.Equals(
                AutomationProperties.GetAutomationId(match),
                automationId,
                StringComparison.Ordinal))
        {
            return match;
        }

        if (parent is not DependencyObject dependencyObject)
        {
            return null;
        }

        foreach (var child in LogicalTreeHelper.GetChildren(dependencyObject))
        {
            if (FindLogicalChildByAutomationId<T>(child, automationId) is { } descendant)
            {
                return descendant;
            }
        }

        return null;
    }

    public void ActivateSessionLogPane() => DockWorkspace.ActivateLinkedViewPane();

    public void ActivateToolLibraryPane()
    {
        if (shell is null || ToolLibraryOverlay.Visibility == Visibility.Visible)
        {
            return;
        }

        toolLibraryReturnFocus = Keyboard.FocusedElement;
        toolLibraryStepCountAtOpen = shell.Workbench.PipelineSteps.Count;
        ToolLibraryOverlay.Visibility = Visibility.Visible;
    }

    public void CloseToolLibraryPane() => CloseToolLibraryPane(restoreFocus: true);

    public bool IsToolLibraryPaneVisible => ToolLibraryOverlay.Visibility == Visibility.Visible;

    public int AuthoringSupportTabCount => DockWorkspace.AuthoringSupportTabCount;

    public bool IsToolLibraryDetachedFromAuthoring => DockWorkspace.IsToolLibraryDetachedFromAuthoring;

    public bool IsSessionLogPaneSelected => DockWorkspace.IsLinkedViewPaneSelected;

    public void ActivateFlowMap()
    {
        NavigateToStage(ShellWorkspaceMode.Inspect);
        DockWorkspace.ActivateEvidencePane();
        if (DockWorkspace.EvidenceContent is RecipePipelineReviewView review)
        {
            review.ActivateFlowMap();
        }
    }

    public bool IsFlowMapSelected => DockWorkspace.IsEvidencePaneSelected
                                     && DockWorkspace.EvidenceContent is RecipePipelineReviewView { IsFlowMapSelected: true };

    public void ActivateProblems()
    {
        NavigateToStage(ShellWorkspaceMode.Inspect);
        DockWorkspace.ActivateEvidencePane();
        if (DockWorkspace.EvidenceContent is RecipePipelineReviewView review)
        {
            review.ActivateProblems();
        }
    }

    public bool IsProblemsSelected => DockWorkspace.IsEvidencePaneSelected
                                       && DockWorkspace.EvidenceContent is RecipePipelineReviewView { IsProblemsSelected: true };

    public void ActivateRunRecord()
    {
        NavigateToStage(ShellWorkspaceMode.Review);
        if (OperatorStage == OpenVisionOperatorStage.Results)
        {
            SetResultsWorkspaceSection(ResultsWorkspaceSection.RunRecord);
        }
        else
        {
            DockWorkspace.ActivateEvidencePane();
        }
        if (DockWorkspace.EvidenceContent is RecipePipelineReviewView review)
        {
            review.ActivateRunRecord();
        }
    }

    public bool IsRunRecordSelected =>
        OperatorStage == OpenVisionOperatorStage.Results
            ? ActiveResultsWorkspaceSection == ResultsWorkspaceSection.RunRecord
            : DockWorkspace.IsEvidencePaneSelected
              && DockWorkspace.EvidenceContent is RecipePipelineReviewView { IsRunRecordSelected: true };

    public bool HasRunRecordHistoryControls =>
        DockWorkspace.ResultsContent is ResultsWorkspaceView { HasRunRecordHistoryControls: true };

    public bool HasPrivacySafeSupportBundleControls =>
        DockWorkspace.ResultsContent is ResultsWorkspaceView
        {
            HasPrivacySafeSupportBundleControls: true
        };

    public void ActivateValidationSet()
    {
        NavigateToStage(ShellWorkspaceMode.Validate);
        DockWorkspace.ActivateEvidencePane();
        if (DockWorkspace.EvidenceContent is RecipePipelineReviewView review)
        {
            review.ActivateValidationSet();
        }
    }

    public bool IsValidationSetSelected => DockWorkspace.IsEvidencePaneSelected
                                           && DockWorkspace.EvidenceContent is RecipePipelineReviewView { IsValidationSetSelected: true };

    public void ActivateOutputComparePane()
    {
        NavigateToStage(ShellWorkspaceMode.Review);
        if (OperatorStage == OpenVisionOperatorStage.Results)
        {
            SetResultsWorkspaceSection(ResultsWorkspaceSection.OutputCompare);
        }
        else
        {
            DockWorkspace.ActivateOutputComparePane();
        }
    }

    public bool IsOutputComparePaneSelected =>
        OperatorStage == OpenVisionOperatorStage.Results
            ? ActiveResultsWorkspaceSection == ResultsWorkspaceSection.OutputCompare
            : DockWorkspace.IsOutputComparePaneSelected;

    public void ActivateViewerPane() => DockWorkspace.ActivateViewerPane();

    public bool IsViewerPaneSelected => DockWorkspace.IsViewerPaneSelected;

    public bool HasUsableOutputCompareDockHeight =>
        OperatorStage == OpenVisionOperatorStage.Results
            ? HasResultsStageComposition
            : DockWorkspace.HasUsableOutputCompareDockHeight;

    public void ActivateDisplayedOutputsPane()
    {
        NavigateToStage(ShellWorkspaceMode.Review);
        if (OperatorStage == OpenVisionOperatorStage.Results)
        {
            SetResultsWorkspaceSection(ResultsWorkspaceSection.OutputCompare);
        }
        else
        {
            DockWorkspace.ActivateDisplayedOutputsPane();
        }
    }

    public bool IsDisplayedOutputsPaneSelected =>
        OperatorStage == OpenVisionOperatorStage.Results
            ? ActiveResultsWorkspaceSection == ResultsWorkspaceSection.OutputCompare
            : DockWorkspace.IsDisplayedOutputsPaneSelected;

    public bool HasStandardBottomPaneHeight =>
        OperatorStage == OpenVisionOperatorStage.Results
            ? !DockWorkspace.IsBottomPaneAttached
            : DockWorkspace.HasStandardBottomPaneHeight;

    public void ActivateProfilePane() => DockWorkspace.ActivateProfilePane();

    public bool IsProfilePaneSelected => DockWorkspace.IsProfilePaneSelected;

    public void ActivateFitDiagnosticsPane() => DockWorkspace.ActivateFitDiagnosticsPane();

    public bool IsFitDiagnosticsPaneSelected => DockWorkspace.IsFitDiagnosticsPaneSelected;

    public void ActivateIntersectionEvidencePane() => DockWorkspace.ActivateIntersectionEvidencePane();

    public bool IsIntersectionEvidencePaneSelected => DockWorkspace.IsIntersectionEvidencePaneSelected;

    public void ActivateCorrespondenceEvidencePane() => DockWorkspace.ActivateCorrespondenceEvidencePane();

    public bool IsCorrespondenceEvidencePaneSelected => DockWorkspace.IsCorrespondenceEvidencePaneSelected;

    public bool HasAllDockContentHosts =>
        ToolLibraryDialogContent is not null
        && DockWorkspace.DataLayersContent is not null
        && DockWorkspace.ViewerContent is not null
        && DockWorkspace.ResultsContent is not null
        && DockWorkspace.ToolInspectorContent is not null
        && DockWorkspace.EvidenceContent is not null
        && DockWorkspace.OutputCompareContent is not null
        && DockWorkspace.DisplayedOutputsContent is not null
        && DockWorkspace.LinkedViewContent is not null
        && DockWorkspace.ProfileContent is not null
        && DockWorkspace.FitDiagnosticsContent is not null
        && DockWorkspace.IntersectionEvidenceContent is not null
        && DockWorkspace.CorrespondenceEvidenceContent is not null;

    public bool CommitPendingParameterEdit(out string message)
    {
        if (DockWorkspace.ToolInspectorContent is SelectedToolWorkspaceView selectedToolWorkspace)
        {
            return selectedToolWorkspace.CommitPendingParameterEdit(out message);
        }

        if (DockWorkspace.ToolInspectorContent is ToolInspectorView inspector)
        {
            return inspector.CommitPendingParameterEdit(out message);
        }

        message = "The Step Parameters view is unavailable.";
        return false;
    }

    public void BringSelectedOutputIntoView()
    {
        if (DockWorkspace.ToolInspectorContent is SelectedToolWorkspaceView selectedToolWorkspace)
        {
            selectedToolWorkspace.BringOutputIntoView();
        }
    }

    public void BringThicknessRepeatGridIntoView()
    {
        if (DockWorkspace.ToolInspectorContent is SelectedToolWorkspaceView selectedToolWorkspace)
        {
            selectedToolWorkspace.BringThicknessRepeatGridIntoView();
        }
    }

    private void NavigateToStage(ShellWorkspaceMode mode)
    {
        var command = WorkspaceSelectionCommand;
        if (command?.CanExecute(mode) == true)
        {
            command.Execute(mode);
        }
    }

    private void OnDataContextChanged(
        object sender,
        DependencyPropertyChangedEventArgs args)
    {
        DetachShell();
        AttachShell(args.NewValue as ShellMainWindowViewModel);
        ApplyOperatorStage();
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        AttachShell(DataContext as ShellMainWindowViewModel);
        ApplyOperatorStage();
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        TeachingDataPopup.SetCurrentValue(Popup.IsOpenProperty, false);
        CancelTeachStageActivation();
        DetachShell();
    }

    private void OnRecipeInfoEditorScopeIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs args)
    {
        if (args.NewValue is not true || sender is not FrameworkElement focusScope)
        {
            return;
        }

        focusScope.Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            if (focusScope.IsVisible)
            {
                focusScope.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
            }
        }));
    }

    private void OnToolLibraryScopeIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs args)
    {
        if (args.NewValue is not true || sender is not FrameworkElement focusScope)
        {
            return;
        }

        focusScope.Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            if (focusScope.IsVisible)
            {
                focusScope.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
            }
        }));
    }

    private void OnToolLibraryOverlayPreviewKeyDown(object sender, KeyEventArgs args)
    {
        if (args.Key != Key.Escape)
        {
            return;
        }

        CloseToolLibraryPane();
        args.Handled = true;
    }

    private void CloseToolLibraryButton_Click(object sender, RoutedEventArgs args)
    {
        CloseToolLibraryPane();
        args.Handled = true;
    }

    private void AttachShell(ShellMainWindowViewModel? candidate)
    {
        if (candidate is null || ReferenceEquals(shell, candidate))
        {
            return;
        }

        shell = candidate;
        shell.PropertyChanged += OnShellPropertyChanged;
        shell.Workbench.PipelineSteps.CollectionChanged += OnPipelineStepsChanged;
        shell.Workbench.SelectedStepRunCompleted += OnSelectedStepRunCompleted;
    }

    private void DetachShell()
    {
        if (shell is not null)
        {
            shell.PropertyChanged -= OnShellPropertyChanged;
            shell.Workbench.PipelineSteps.CollectionChanged -= OnPipelineStepsChanged;
            shell.Workbench.SelectedStepRunCompleted -= OnSelectedStepRunCompleted;
            shell = null;
        }
    }

    private void OnShellPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(ShellMainWindowViewModel.SelectedWorkspaceMode))
        {
            ApplyOperatorStage();
        }
    }

    private void OnSelectedStepRunCompleted(object? sender, EventArgs args)
    {
        if (Volatile.Read(ref disposalState) == 0 && IsLoaded)
        {
            BringSelectedOutputIntoView();
        }
    }

    private void OnPipelineStepsChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        if (ToolLibraryOverlay.Visibility == Visibility.Visible
            && shell is not null
            && shell.Workbench.PipelineSteps.Count > toolLibraryStepCountAtOpen)
        {
            CloseToolLibraryPane();
        }
    }

    private void OnActiveReviewChanged(object? sender, EventArgs args)
    {
        if (Volatile.Read(ref disposalState) != 0
            || pipelineReview is null)
        {
            return;
        }

        DockWorkspace.SetEvidenceAnalysisHeight(pipelineReview.IsValidationSetSelected);
    }

    private void ApplyOperatorStage()
    {
        CancelTeachStageActivation();
        var stage = shell?.SelectedWorkspaceMode switch
        {
            ShellWorkspaceMode.Workbench => OpenVisionOperatorStage.Teach,
            ShellWorkspaceMode.Teach => OpenVisionOperatorStage.Teach,
            ShellWorkspaceMode.Validate => OpenVisionOperatorStage.Validate,
            ShellWorkspaceMode.Inspect => OpenVisionOperatorStage.Validate,
            ShellWorkspaceMode.Review => OpenVisionOperatorStage.Results,
            _ => OpenVisionOperatorStage.Legacy,
        };

        if (stage != OpenVisionOperatorStage.Teach)
        {
            CloseToolLibraryPane(restoreFocus: false);
        }

        DockWorkspace.IsRecipeValidationWorkspace = shell?.IsValidateWorkspaceSelected == true;
        DockWorkspace.SetOperatorStage(stage);
        if (DockWorkspace.EvidenceContent is RecipePipelineReviewView pipelineReview)
        {
            pipelineReview.SetPresentationMode(shell?.SelectedWorkspaceMode switch
            {
                ShellWorkspaceMode.Inspect => RecipeReviewPresentationMode.Inspection,
                ShellWorkspaceMode.Review => RecipeReviewPresentationMode.Results,
                _ => stage == OpenVisionOperatorStage.Validate
                    ? RecipeReviewPresentationMode.Validation
                    : RecipeReviewPresentationMode.Standard,
            });
        }
        if (stage == OpenVisionOperatorStage.Results
            && DockWorkspace.ResultsContent is ResultsWorkspaceView resultsWorkspace)
        {
            resultsWorkspace.SetSection(ResultsWorkspaceSection.RunRecord);
        }
        if (DockWorkspace.DataLayersContent is RecipeChainView recipeChain)
        {
            recipeChain.IsTeachingMode = stage == OpenVisionOperatorStage.Teach;
        }

        if (stage == OpenVisionOperatorStage.Teach)
        {
            var failureCorrection =
                shell?.Workbench.HasActiveValidationFailureCorrectionContext == true;
            if (failureCorrection)
            {
                DockWorkspace.ActivateToolInspectorPane();
            }

            QueueTeachStageActivation();
        }

        if (stage == OpenVisionOperatorStage.Validate
            && shell?.SelectedWorkspaceMode == ShellWorkspaceMode.Inspect)
        {
            if (DockWorkspace.EvidenceContent is RecipePipelineReviewView review)
            {
                review.ActivateInspectionQueue();
            }
        }
        else if (stage == OpenVisionOperatorStage.Validate)
        {
            if (DockWorkspace.EvidenceContent is RecipePipelineReviewView review)
            {
                review.ActivateValidationSet();
            }
        }
        else if (stage == OpenVisionOperatorStage.Results)
        {
            if (DockWorkspace.EvidenceContent is RecipePipelineReviewView review)
            {
                review.ActivateRunRecord();
            }
        }
    }

    private void QueueTeachStageActivation()
    {
        if (Volatile.Read(ref disposalState) != 0
            || Dispatcher.HasShutdownStarted
            || Dispatcher.HasShutdownFinished)
        {
            return;
        }

        if (teachStageActivationOperation?.Status is DispatcherOperationStatus.Pending or DispatcherOperationStatus.Executing)
        {
            return;
        }

        try
        {
            teachStageActivationOperation = Dispatcher.BeginInvoke(
                DispatcherPriority.ContextIdle,
                new Action(ApplyTeachStageActivation));
        }
        catch (InvalidOperationException)
        {
            teachStageActivationOperation = null;
        }
    }

    private void ApplyTeachStageActivation()
    {
        teachStageActivationOperation = null;
        if (Volatile.Read(ref disposalState) != 0)
        {
            return;
        }

        if (shell?.SelectedWorkspaceMode == ShellWorkspaceMode.Teach
            && shell.Workbench.HasActiveValidationFailureCorrectionContext)
        {
            DockWorkspace.ActivateToolInspectorPane();
        }

        ViewerWorkspaceSurface?.ReactivateMainViewer(ViewerContent);
    }

    private void CancelTeachStageActivation()
    {
        var operation = teachStageActivationOperation;
        teachStageActivationOperation = null;
        if (operation?.Status == DispatcherOperationStatus.Pending)
        {
            operation.Abort();
        }
    }

    private void CloseToolLibraryPane(bool restoreFocus)
    {
        if (ToolLibraryOverlay.Visibility != Visibility.Visible)
        {
            return;
        }

        ToolLibraryOverlay.Visibility = Visibility.Collapsed;
        var returnFocus = toolLibraryReturnFocus;
        toolLibraryReturnFocus = null;
        if (restoreFocus && returnFocus is not null)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => Keyboard.Focus(returnFocus)));
        }
    }

    private static void OnViewerContentChanged(DependencyObject owner, DependencyPropertyChangedEventArgs args)
    {
        if (owner is not ToolRecipeWorkbenchView view
            || Volatile.Read(ref view.disposalState) != 0)
        {
            return;
        }

        if (view.DockWorkspace.ProfileContent is HeightProfileView profileView)
        {
            profileView.ViewerHost = args.NewValue as IOpenVisionThreeDViewerHost;
        }
    }

    private void BindStageHostedContext(object? content, string path)
    {
        if (content is FrameworkElement element)
        {
            element.SetBinding(
                FrameworkElement.DataContextProperty,
                new Binding(path) { Source = this });
        }
    }

    private static bool HasDataContext(object? content, object expected) =>
        content is FrameworkElement element
        && ReferenceEquals(element.DataContext, expected);

}
