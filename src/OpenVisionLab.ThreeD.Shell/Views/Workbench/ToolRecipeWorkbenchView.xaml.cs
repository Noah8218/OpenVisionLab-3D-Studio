using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using OpenVisionLab.ThreeD.Docking.Controls;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.Views.Workbench;

public sealed partial class ToolRecipeWorkbenchView : UserControl
{
    private ViewerWorkspaceView? viewerWorkspaceSurface;

    public static readonly DependencyProperty ViewerContentProperty =
        DependencyProperty.Register(
            nameof(ViewerContent),
            typeof(object),
            typeof(ToolRecipeWorkbenchView),
            new PropertyMetadata(null, OnViewerContentChanged));

    public ToolRecipeWorkbenchView()
    {
        InitializeComponent();
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
            review.ActiveReviewChanged += (_, _) =>
                DockWorkspace.SetEvidenceAnalysisHeight(review.IsValidationSetSelected);
            DockWorkspace.SetEvidenceAnalysisHeight(review.IsValidationSetSelected);
        }
    }

    public object? ViewerContent
    {
        get => GetValue(ViewerContentProperty);
        set => SetValue(ViewerContentProperty, value);
    }

    public IReadOnlyList<DockingPaneContract> GetDockingPaneContracts() =>
        DockWorkspace.GetDockingPaneContracts();

    public DockingFloatDockResult VerifyFirstPaneFloatDockRoundTrip() =>
        DockWorkspace.VerifyFirstPaneFloatDockRoundTrip();

    public bool IsBottomPaneExpanded
    {
        get => DockWorkspace.IsBottomPaneExpanded;
        set => DockWorkspace.IsBottomPaneExpanded = value;
    }

    public bool IsBottomPaneAttached => DockWorkspace.IsBottomPaneAttached;

    public bool IsCompactDockLayout => DockWorkspace.IsCompactLayout;

    public bool HasRecipeFlowInspectorViewerOrder =>
        DockWorkspace.HasRecipeFlowInspectorViewerOrder;

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

    public void ActivateSessionLogPane() => DockWorkspace.ActivateLinkedViewPane();

    public void ActivateToolLibraryPane() => DockWorkspace.ActivateToolLibraryPane();

    public bool IsSessionLogPaneSelected => DockWorkspace.IsLinkedViewPaneSelected;

    public void ActivateFlowMap()
    {
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
        DockWorkspace.ActivateEvidencePane();
        if (DataContext is ShellMainWindowViewModel shell)
        {
            shell.Workbench.SelectedReviewTabIndex = 3;
        }
        else if (DockWorkspace.EvidenceContent is RecipePipelineReviewView review)
        {
            review.ActivateRunRecord();
        }
    }

    public bool IsRunRecordSelected => DockWorkspace.IsEvidencePaneSelected
                                        && DockWorkspace.EvidenceContent is RecipePipelineReviewView { IsRunRecordSelected: true };

    public bool HasRunRecordHistoryControls =>
        DockWorkspace.EvidenceContent is RecipePipelineReviewView { HasRunRecordHistoryControls: true };

    public void ActivateValidationSet()
    {
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

    public void ActivateOutputComparePane() => DockWorkspace.ActivateOutputComparePane();

    public bool IsOutputComparePaneSelected => DockWorkspace.IsOutputComparePaneSelected;

    public bool HasUsableOutputCompareDockHeight => DockWorkspace.HasUsableOutputCompareDockHeight;

    public void ActivateDisplayedOutputsPane() => DockWorkspace.ActivateDisplayedOutputsPane();

    public bool IsDisplayedOutputsPaneSelected => DockWorkspace.IsDisplayedOutputsPaneSelected;

    public bool HasStandardBottomPaneHeight => DockWorkspace.HasStandardBottomPaneHeight;

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

    private void OpenPipelineReview_Click(object sender, RoutedEventArgs args) =>
        DockWorkspace.ActivateEvidencePane();

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

}
