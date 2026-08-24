using OpenVisionLab.ThreeD.Viewer;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Shell.Coordination;

/// <summary>
/// Owns the lifetime of Shell presentation-request subscriptions. The
/// callbacks remain explicit WPF adapters supplied by the composition root.
/// </summary>
internal sealed class ShellRequestCoordinator : IDisposable
{
    private readonly OpenVisionThreeDViewerControl viewer;
    private readonly ShellMainWindowViewModel viewModel;
    private readonly ShellRequestCallbacks callbacks;
    private bool disposed;

    public ShellRequestCoordinator(
        OpenVisionThreeDViewerControl viewer,
        ShellMainWindowViewModel viewModel,
        ShellRequestCallbacks callbacks)
    {
        this.viewer = viewer;
        this.viewModel = viewModel;
        this.callbacks = callbacks;

        viewer.ProfileViewRequested += callbacks.ProfileView;
        viewModel.RefreshRecipeComparisonRequested += callbacks.RefreshRecipeComparison;
        viewModel.SaveRecipeRequested += callbacks.SaveRecipe;
        viewModel.ApplyRoiAlignmentRequested += callbacks.ApplyRoiAlignment;
        viewModel.FitPlaneRequested += callbacks.FitPlane;
        viewModel.PublishInspectionResultRequested += callbacks.PublishInspectionResult;
        viewModel.Calibration.LoadStudyRequested += callbacks.CalibrationLoadStudy;
        viewModel.OpenEvidenceArtifactRequested += callbacks.OpenEvidenceArtifact;
        viewModel.OpenRunRecordRequested += callbacks.OpenRunRecord;
        viewModel.ExportRunRecordRequested += callbacks.ExportRunRecord;
        viewModel.ExportPrivacySafeSupportBundleRequested += callbacks.ExportPrivacySafeSupportBundle;
        viewModel.Workbench.NewTeachingRecipeRequested += callbacks.NewTeachingRecipe;
        viewModel.Workbench.BrowseFirstRecipeFolderRequested += callbacks.BrowseFirstRecipeFolder;
        viewModel.Workbench.BrowseFirstRecipeSourceRequested += callbacks.BrowseFirstRecipeSource;
        viewModel.Workbench.SaveTeachingRecipeRequested += callbacks.SaveTeachingRecipe;
        viewModel.Workbench.SaveTeachingRecipeAsRequested += callbacks.SaveTeachingRecipeAs;
        viewModel.Workbench.OpenToolLibraryRequested += callbacks.OpenToolLibrary;
        viewModel.Workbench.SelectedStepSetupRequested += callbacks.SelectedStepSetup;
        viewModel.Workbench.SourceQualityWorkspaceRequested += callbacks.SourceQualityWorkspace;
        viewModel.Workbench.OpenTeachingRecipeRequested += callbacks.OpenTeachingRecipe;
        viewModel.Workbench.RemoveSelectedStepRequested += callbacks.RemoveSelectedStep;
        viewModel.Workbench.OpenRecentTeachingRecipeRequested += callbacks.OpenRecentTeachingRecipe;
        viewModel.Workbench.LoadC3DSourceRequested += callbacks.LoadC3DSource;
        viewModel.Workbench.Import3DDataRequested += callbacks.Import3DData;
        viewModel.Workbench.CancelC3DSourceLoadRequested += callbacks.CancelC3DSourceLoad;
        viewModel.Workbench.ToolLabRequested += callbacks.ToolLab;
        viewModel.Workbench.SelectValidationSetSourcesRequested += callbacks.SelectValidationSetSources;
        viewModel.Workbench.ValidationSetComparisonRequested += callbacks.ValidationSetComparison;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        viewer.ProfileViewRequested -= callbacks.ProfileView;
        viewModel.RefreshRecipeComparisonRequested -= callbacks.RefreshRecipeComparison;
        viewModel.SaveRecipeRequested -= callbacks.SaveRecipe;
        viewModel.ApplyRoiAlignmentRequested -= callbacks.ApplyRoiAlignment;
        viewModel.FitPlaneRequested -= callbacks.FitPlane;
        viewModel.PublishInspectionResultRequested -= callbacks.PublishInspectionResult;
        viewModel.Calibration.LoadStudyRequested -= callbacks.CalibrationLoadStudy;
        viewModel.OpenEvidenceArtifactRequested -= callbacks.OpenEvidenceArtifact;
        viewModel.OpenRunRecordRequested -= callbacks.OpenRunRecord;
        viewModel.ExportRunRecordRequested -= callbacks.ExportRunRecord;
        viewModel.ExportPrivacySafeSupportBundleRequested -= callbacks.ExportPrivacySafeSupportBundle;
        viewModel.Workbench.NewTeachingRecipeRequested -= callbacks.NewTeachingRecipe;
        viewModel.Workbench.BrowseFirstRecipeFolderRequested -= callbacks.BrowseFirstRecipeFolder;
        viewModel.Workbench.BrowseFirstRecipeSourceRequested -= callbacks.BrowseFirstRecipeSource;
        viewModel.Workbench.SaveTeachingRecipeRequested -= callbacks.SaveTeachingRecipe;
        viewModel.Workbench.SaveTeachingRecipeAsRequested -= callbacks.SaveTeachingRecipeAs;
        viewModel.Workbench.OpenToolLibraryRequested -= callbacks.OpenToolLibrary;
        viewModel.Workbench.SelectedStepSetupRequested -= callbacks.SelectedStepSetup;
        viewModel.Workbench.SourceQualityWorkspaceRequested -= callbacks.SourceQualityWorkspace;
        viewModel.Workbench.OpenTeachingRecipeRequested -= callbacks.OpenTeachingRecipe;
        viewModel.Workbench.RemoveSelectedStepRequested -= callbacks.RemoveSelectedStep;
        viewModel.Workbench.OpenRecentTeachingRecipeRequested -= callbacks.OpenRecentTeachingRecipe;
        viewModel.Workbench.LoadC3DSourceRequested -= callbacks.LoadC3DSource;
        viewModel.Workbench.Import3DDataRequested -= callbacks.Import3DData;
        viewModel.Workbench.CancelC3DSourceLoadRequested -= callbacks.CancelC3DSourceLoad;
        viewModel.Workbench.ToolLabRequested -= callbacks.ToolLab;
        viewModel.Workbench.SelectValidationSetSourcesRequested -= callbacks.SelectValidationSetSources;
        viewModel.Workbench.ValidationSetComparisonRequested -= callbacks.ValidationSetComparison;
    }
}

internal sealed record ShellRequestCallbacks
{
    public required EventHandler ProfileView { get; init; }
    public required EventHandler RefreshRecipeComparison { get; init; }
    public required EventHandler SaveRecipe { get; init; }
    public required EventHandler ApplyRoiAlignment { get; init; }
    public required EventHandler FitPlane { get; init; }
    public required EventHandler PublishInspectionResult { get; init; }
    public required EventHandler CalibrationLoadStudy { get; init; }
    public required EventHandler<EvidenceArtifactOpenRequestEventArgs> OpenEvidenceArtifact { get; init; }
    public required EventHandler OpenRunRecord { get; init; }
    public required EventHandler ExportRunRecord { get; init; }
    public required EventHandler ExportPrivacySafeSupportBundle { get; init; }
    public required EventHandler NewTeachingRecipe { get; init; }
    public required EventHandler BrowseFirstRecipeFolder { get; init; }
    public required EventHandler BrowseFirstRecipeSource { get; init; }
    public required EventHandler SaveTeachingRecipe { get; init; }
    public required EventHandler SaveTeachingRecipeAs { get; init; }
    public required EventHandler OpenToolLibrary { get; init; }
    public required EventHandler SelectedStepSetup { get; init; }
    public required EventHandler SourceQualityWorkspace { get; init; }
    public required EventHandler OpenTeachingRecipe { get; init; }
    public required EventHandler<ToolWorkbenchStepRemovalRequestEventArgs> RemoveSelectedStep { get; init; }
    public required EventHandler<ToolWorkbenchRecipePathRequestEventArgs> OpenRecentTeachingRecipe { get; init; }
    public required EventHandler LoadC3DSource { get; init; }
    public required EventHandler Import3DData { get; init; }
    public required EventHandler CancelC3DSourceLoad { get; init; }
    public required EventHandler<ToolWorkbenchToolLabRequestEventArgs> ToolLab { get; init; }
    public required EventHandler SelectValidationSetSources { get; init; }
    public required EventHandler ValidationSetComparison { get; init; }
}
