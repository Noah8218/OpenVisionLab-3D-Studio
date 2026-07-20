using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using OpenVisionLab.ThreeD.Docking.Controls;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.Views.Workbench;

public sealed partial class ToolRecipeWorkbenchView : UserControl
{
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

    public void ActivateSessionLogPane() => DockWorkspace.ActivateLinkedViewPane();

    public bool IsSessionLogPaneSelected => DockWorkspace.IsLinkedViewPaneSelected;

    public void ActivateFlowMap()
    {
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
        DockWorkspace.ActivateEvidencePane();
        if (DockWorkspace.EvidenceContent is RecipePipelineReviewView review)
        {
            review.ActivateProblems();
        }
    }

    public bool IsProblemsSelected => DockWorkspace.IsEvidencePaneSelected
                                      && DockWorkspace.EvidenceContent is RecipePipelineReviewView { IsProblemsSelected: true };

    public void ActivateOutputComparePane() => DockWorkspace.ActivateOutputComparePane();

    public bool IsOutputComparePaneSelected => DockWorkspace.IsOutputComparePaneSelected;

    public void ActivateDisplayedOutputsPane() => DockWorkspace.ActivateDisplayedOutputsPane();

    public bool IsDisplayedOutputsPaneSelected => DockWorkspace.IsDisplayedOutputsPaneSelected;

    public void ActivateProfilePane() => DockWorkspace.ActivateProfilePane();

    public bool IsProfilePaneSelected => DockWorkspace.IsProfilePaneSelected;

    public void ActivateFitDiagnosticsPane() => DockWorkspace.ActivateFitDiagnosticsPane();

    public bool IsFitDiagnosticsPaneSelected => DockWorkspace.IsFitDiagnosticsPaneSelected;

    public void ActivateIntersectionEvidencePane() => DockWorkspace.ActivateIntersectionEvidencePane();

    public bool IsIntersectionEvidencePaneSelected => DockWorkspace.IsIntersectionEvidencePaneSelected;

    public void ActivateCorrespondenceEvidencePane() => DockWorkspace.ActivateCorrespondenceEvidencePane();

    public bool IsCorrespondenceEvidencePaneSelected => DockWorkspace.IsCorrespondenceEvidencePaneSelected;

    public bool HasAllDockContentHosts =>
        DockWorkspace.DataLayersContent is not null
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
        if (DockWorkspace.ToolInspectorContent is ToolInspectorView inspector)
        {
            return inspector.CommitPendingParameterEdit(out message);
        }

        message = "The Step Parameters view is unavailable.";
        return false;
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

}
