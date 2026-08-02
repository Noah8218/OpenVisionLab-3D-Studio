using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using OpenVisionLab.ThreeD.Docking.Controls;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.Views.Workbench;

public sealed partial class ToolRecipeWorkbenchView : UserControl
{
    private ViewerWorkspaceView? viewerWorkspaceSurface;
    private ShellMainWindowViewModel? shell;

    public static readonly DependencyProperty ViewerContentProperty =
        DependencyProperty.Register(
            nameof(ViewerContent),
            typeof(object),
            typeof(ToolRecipeWorkbenchView),
            new PropertyMetadata(null, OnViewerContentChanged));

    public ToolRecipeWorkbenchView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        BindStageHostedContext(DockWorkspace.ToolLibraryContent, "DataContext.Workbench");
        BindStageHostedContext(DockWorkspace.DataLayersContent, "DataContext.Workbench");
        BindStageHostedContext(DockWorkspace.ViewerContent, "DataContext");
        BindStageHostedContext(DockWorkspace.ResultsContent, "DataContext");
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
            review.SetBinding(
                RecipePipelineReviewView.RunRecordContextProperty,
                new Binding("DataContext") { Source = this });
            review.ActiveReviewChanged += (_, _) =>
                DockWorkspace.SetEvidenceAnalysisHeight(review.IsValidationSetSelected);
            DockWorkspace.SetEvidenceAnalysisHeight(review.IsValidationSetSelected);
        }
    }

    public OpenVisionOperatorStage OperatorStage => DockWorkspace.OperatorStage;

    public bool HasSetupStageComposition => DockWorkspace.HasSetupStageComposition;

    public bool HasTeachStageComposition => DockWorkspace.HasTeachStageComposition;

    public bool HasAuthoringStageComposition =>
        DockWorkspace.HasAuthoringPaneComposition;

    public bool HasValidateOrResultsStageComposition =>
        DockWorkspace.HasValidateOrResultsStageComposition;

    public bool HasValidateStageComposition =>
        DockWorkspace.HasValidateStageComposition;

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
        && HasDataContext(DockWorkspace.ToolLibraryContent, currentShell.Workbench)
        && HasDataContext(DockWorkspace.DataLayersContent, currentShell.Workbench)
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

    public bool HasNoVisibleWorkspaceCommandBar =>
        WorkspaceCommandBar.Visibility == Visibility.Collapsed;

    public bool IsToolInspectorPaneSelected =>
        DockWorkspace.IsToolInspectorPaneSelected;

    public bool HasRecipeFlowInspectorViewerOrder =>
        DockWorkspace.HasRecipeFlowInspectorViewerOrder;

    public bool HasVisibleAuthoringFirstActionGuide =>
        DockWorkspace.DataLayersContent is RecipeChainView
        {
            HasVisibleFirstActionGuide: true
        };

    public bool HasSingleVisibleAuthoringFirstAction =>
        DockWorkspace.DataLayersContent is RecipeChainView
        {
            HasSingleVisibleFirstAction: true
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
        && ViewerWorkspaceSurface is not null
        && WorkspaceCommandBar is not null;

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

    public void ActivateSelectedToolPane() =>
        DockWorkspace.ActivateToolInspectorPane();

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
            "vertical" => shell.Workbench.SplitViewerVerticallyCommand,
            "horizontal" => shell.Workbench.SplitViewerHorizontallyCommand,
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
            "vertical" => shell.Workbench.ViewerWorkspace.Layout == ViewerWorkspaceLayout.SplitVertical,
            "horizontal" => shell.Workbench.ViewerWorkspace.Layout == ViewerWorkspaceLayout.SplitHorizontal,
            "popout" => shell.Workbench.ViewerWorkspace.Layout == ViewerWorkspaceLayout.PopOut
                        && ViewerWorkspaceSurface?.IsPopoutVisible == true,
            _ => false
        };
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

    private void ViewerWorkspaceSurface_Loaded(object sender, RoutedEventArgs args) =>
        viewerWorkspaceSurface = sender as ViewerWorkspaceView;

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

    public void ActivateToolLibraryPane() => DockWorkspace.ActivateToolLibraryPane();

    public bool IsSessionLogPaneSelected => DockWorkspace.IsLinkedViewPaneSelected;

    public void ActivateFlowMap()
    {
        NavigateToStage(ShellWorkspaceMode.Inspect);
        DockWorkspace.ActivateEvidencePane();
        if (DataContext is ShellMainWindowViewModel shell)
        {
            shell.Workbench.SelectedReviewTabIndex = 1;
        }
        else if (DockWorkspace.EvidenceContent is RecipePipelineReviewView review)
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
        if (DataContext is ShellMainWindowViewModel shell)
        {
            shell.Workbench.SelectedReviewTabIndex = 2;
        }
        else if (DockWorkspace.EvidenceContent is RecipePipelineReviewView review)
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
        if (DataContext is ShellMainWindowViewModel shell)
        {
            shell.Workbench.SelectedReviewTabIndex = 3;
        }
        else if (DockWorkspace.EvidenceContent is RecipePipelineReviewView review)
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

    public void ActivateValidationSet()
    {
        NavigateToStage(ShellWorkspaceMode.Inspect);
        DockWorkspace.ActivateEvidencePane();
        if (DataContext is ShellMainWindowViewModel shell)
        {
            shell.Workbench.SelectedReviewTabIndex = 4;
        }
        else if (DockWorkspace.EvidenceContent is RecipePipelineReviewView review)
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
        DockWorkspace.ToolLibraryContent is not null
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
        if (shell?.SelectWorkspaceCommand.CanExecute(mode) == true)
        {
            shell.SelectWorkspaceCommand.Execute(mode);
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

    private void OnUnloaded(object sender, RoutedEventArgs args) =>
        DetachShell();

    private void AttachShell(ShellMainWindowViewModel? candidate)
    {
        if (candidate is null || ReferenceEquals(shell, candidate))
        {
            return;
        }

        shell = candidate;
        shell.PropertyChanged += OnShellPropertyChanged;
    }

    private void DetachShell()
    {
        if (shell is not null)
        {
            shell.PropertyChanged -= OnShellPropertyChanged;
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

    private void ApplyOperatorStage()
    {
        var stage = shell?.SelectedWorkspaceMode switch
        {
            ShellWorkspaceMode.Workbench => OpenVisionOperatorStage.Teach,
            ShellWorkspaceMode.Teach => OpenVisionOperatorStage.Teach,
            ShellWorkspaceMode.Inspect => OpenVisionOperatorStage.Validate,
            ShellWorkspaceMode.Review => OpenVisionOperatorStage.Results,
            _ => OpenVisionOperatorStage.Legacy,
        };

        DockWorkspace.SetOperatorStage(stage);
        if (DockWorkspace.EvidenceContent is RecipePipelineReviewView pipelineReview)
        {
            pipelineReview.SetPresentationMode(stage switch
            {
                OpenVisionOperatorStage.Validate => RecipeReviewPresentationMode.Validation,
                _ => RecipeReviewPresentationMode.Standard,
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

            Dispatcher.BeginInvoke(
                () =>
                {
                    if (shell?.SelectedWorkspaceMode == ShellWorkspaceMode.Teach
                        && shell.Workbench.HasActiveValidationFailureCorrectionContext)
                    {
                        DockWorkspace.ActivateToolInspectorPane();
                    }

                    ViewerWorkspaceSurface?.ReactivateMainViewer(ViewerContent);
                },
                DispatcherPriority.ContextIdle);
        }

        if (stage == OpenVisionOperatorStage.Validate)
        {
            shell!.Workbench.SelectedReviewTabIndex = 4;
        }
        else if (stage == OpenVisionOperatorStage.Results)
        {
            shell!.Workbench.SelectedReviewTabIndex = 3;
        }
    }

    private static void OnViewerContentChanged(DependencyObject owner, DependencyPropertyChangedEventArgs args)
    {
        if (owner is ToolRecipeWorkbenchView view)
        {
            if (view.DockWorkspace.ProfileContent is HeightProfileView profileView)
            {
                profileView.DataContext = (args.NewValue as OpenVisionThreeDViewerControl)?.ViewModel;
            }
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
