using Microsoft.Win32;
using OpenVisionLab;
using OpenVisionLab.Logging;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Viewer;
using OpenVisionLab.ThreeD.Viewer.Hosting;
using OpenVisionLab.ThreeD.Viewer.Models;
using OpenVisionLab.ThreeD.Viewer.Rendering;
using OpenVisionLab.ThreeD.Viewer.ViewModels;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Shell.Views.Recipe;
using OpenVisionLab.ThreeD.Shell.Views.Tooling;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace OpenVisionLab.ThreeD.Shell;

public partial class MainWindow : Window
{
    private readonly OpenVisionThreeDViewerControl _viewer = new();
    private readonly ShellMainWindowViewModel _viewModel;
    private readonly EventHandler<ViewerHostStateChangedEventArgs> _viewerHostStateChangedHandler;
    private readonly EventHandler _profileViewRequestedHandler;
    private readonly EventHandler _refreshRecipeComparisonRequestedHandler;
    private readonly EventHandler _saveRecipeRequestedHandler;
    private readonly EventHandler _applyRoiAlignmentRequestedHandler;
    private readonly EventHandler _fitPlaneRequestedHandler;
    private readonly EventHandler _publishInspectionResultRequestedHandler;
    private readonly EventHandler _calibrationLoadStudyRequestedHandler;
    private readonly EventHandler<EvidenceArtifactOpenRequestEventArgs> _openEvidenceArtifactRequestedHandler;
    private readonly EventHandler _workbenchNewTeachingRecipeRequestedHandler;
    private readonly EventHandler _workbenchSaveTeachingRecipeRequestedHandler;
    private readonly EventHandler _workbenchSaveTeachingRecipeAsRequestedHandler;
    private readonly EventHandler _workbenchOpenTeachingRecipeRequestedHandler;
    private readonly EventHandler<ToolWorkbenchRecipePathRequestEventArgs> _workbenchOpenRecentTeachingRecipeRequestedHandler;
    private readonly EventHandler _workbenchLoadC3DSourceRequestedHandler;
    private readonly EventHandler<ToolWorkbenchTeachingCaptureRequestEventArgs> _workbenchBeginTeachingCaptureRequestedHandler;
    private readonly EventHandler _workbenchUndoTeachingCaptureRequestedHandler;
    private readonly EventHandler _workbenchCancelTeachingCaptureRequestedHandler;
    private readonly EventHandler _workbenchApplyTeachingCaptureRequestedHandler;
    private readonly EventHandler _workbenchAppliedTeachingSelectionsChangedHandler;
    private readonly EventHandler<ToolWorkbenchFilterDisplayRequestEventArgs> _workbenchFilterDisplayRequestedHandler;
    private readonly EventHandler<ToolWorkbenchHeightDifferenceEdgeDisplayRequestEventArgs> _workbenchHeightDifferenceEdgeDisplayRequestedHandler;
    private readonly EventHandler<ToolWorkbenchLineFitDisplayRequestEventArgs> _workbenchLineFitDisplayRequestedHandler;
    private readonly EventHandler _workbenchLineFitDisplayClearedHandler;
    private readonly EventHandler<ToolWorkbenchLineIntersectionDisplayRequestEventArgs> _workbenchLineIntersectionDisplayRequestedHandler;
    private readonly EventHandler _workbenchLineIntersectionDisplayClearedHandler;
    private readonly EventHandler<ToolWorkbenchLandmarkCorrespondenceDisplayRequestEventArgs> _workbenchLandmarkCorrespondenceDisplayRequestedHandler;
    private readonly EventHandler _workbenchLandmarkCorrespondenceDisplayClearedHandler;
    private readonly EventHandler<WorkbenchLineFitPointSelectedEventArgs> _viewerWorkbenchLineFitPointSelectedHandler;
    private readonly EventHandler<TeachingCaptureStateChangedEventArgs> _viewerTeachingCaptureStateChangedHandler;
    private readonly PropertyChangedEventHandler _viewModelPropertyChangedHandler;
    private readonly EventHandler _inspectionTaskChangedHandler;
    private RecipeManagerWindow? recipeManagerWindow;
    private FilterToolLabWindow? filterToolLabWindow;
    private HeightDifferenceEdgeToolLabWindow? heightDifferenceEdgeToolLabWindow;
    private LineIntersectionToolLabWindow? lineIntersectionToolLabWindow;
    private LandmarkCorrespondenceToolLabWindow? landmarkCorrespondenceToolLabWindow;
    private RoutedEventHandler _shellSmokeLoadedHandler = (_, _) => { };

    public MainWindow()
    {
        OpenVisionLanguageService.Load();
        OVLog.Write(LogCategory.System, LogLevel.Info, "OpenVisionLab 3D Studio starting.");
        InitializeComponent();
        _viewModel = new ShellMainWindowViewModel(
            GetCommandLineValue("--recipe-comparison-contract"),
            GetCommandLineValue("--recipe-comparison-report"),
            GetCommandLineValue("--shell-smoke-screenshot"),
            GetCommandLineValue("--run-record"),
            GetCommandLineValue("--html-report"),
            GetCommandLineValue("--csv-report"));
        _viewModel.SelectedEvidenceTabIndex = GetEvidenceTabIndex(GetCommandLineValue("--shell-evidence-tab"));
        DataContext = _viewModel;
        if (Workspace.ProfileContent is Views.Workbench.HeightProfileView advancedHeightProfileView)
        {
            advancedHeightProfileView.DataContext = _viewer.ViewModel;
        }
        OVLog.Write(LogCategory.UI, LogLevel.Info, "Tool Workbench is the default Shell workspace.");
        SyncWorkbenchSourceFromViewer();
        _viewer.SidePanelsVisible = false;
        TaskWorkspace.ViewerViewModel = _viewer.ViewModel;
        _viewModelPropertyChangedHandler = OnShellViewModelPropertyChanged;
        _viewModel.PropertyChanged += _viewModelPropertyChangedHandler;
        _inspectionTaskChangedHandler = (_, _) => LoadSelectedInspectionTask();
        _viewModel.InspectionTaskChanged += _inspectionTaskChangedHandler;
        UpdateViewerHost();
        ConfigureWorkspaceFromCommandLine();
        ConfigureInspectionTaskFromCommandLine();
        _viewModel.UpdateC3DSampleVisible(_viewer.HostState.C3DSampleVisible);

        _viewerHostStateChangedHandler = OnViewerHostStateChanged;
        _viewer.HostStateChanged += _viewerHostStateChangedHandler;
        _profileViewRequestedHandler = OnProfileViewRequested;
        _viewer.ProfileViewRequested += _profileViewRequestedHandler;
        _viewer.EnableSmokeFromCommandLine(ownsApplicationLifecycle: false);

        _refreshRecipeComparisonRequestedHandler = (_, _) => _viewModel.RefreshRecipeComparison();
        _saveRecipeRequestedHandler = (_, _) => _viewer.SaveCurrentRecipeWithDialog();
        _applyRoiAlignmentRequestedHandler = (_, _) => _viewer.ApplyRoiReferenceAlignment();
        _fitPlaneRequestedHandler = (_, _) => _viewer.FitC3DReferencePlane();
        _publishInspectionResultRequestedHandler = (_, _) => OnPublishInspectionResultRequested();
        _calibrationLoadStudyRequestedHandler = OnCalibrationLoadStudyRequested;
        _openEvidenceArtifactRequestedHandler = OnOpenEvidenceArtifactRequested;
        _workbenchNewTeachingRecipeRequestedHandler = OnWorkbenchNewTeachingRecipeRequested;
        _workbenchSaveTeachingRecipeRequestedHandler = OnWorkbenchSaveTeachingRecipeRequested;
        _workbenchSaveTeachingRecipeAsRequestedHandler = OnWorkbenchSaveTeachingRecipeAsRequested;
        _workbenchOpenTeachingRecipeRequestedHandler = OnWorkbenchOpenTeachingRecipeRequested;
        _workbenchOpenRecentTeachingRecipeRequestedHandler = OnWorkbenchOpenRecentTeachingRecipeRequested;
        _workbenchLoadC3DSourceRequestedHandler = OnWorkbenchLoadC3DSourceRequested;
        _workbenchBeginTeachingCaptureRequestedHandler = OnWorkbenchBeginTeachingCaptureRequested;
        _workbenchUndoTeachingCaptureRequestedHandler = OnWorkbenchUndoTeachingCaptureRequested;
        _workbenchCancelTeachingCaptureRequestedHandler = OnWorkbenchCancelTeachingCaptureRequested;
        _workbenchApplyTeachingCaptureRequestedHandler = OnWorkbenchApplyTeachingCaptureRequested;
        _workbenchAppliedTeachingSelectionsChangedHandler = (_, _) => SyncAppliedTeachingSelections();
        _workbenchFilterDisplayRequestedHandler = OnWorkbenchFilterDisplayRequested;
        _workbenchHeightDifferenceEdgeDisplayRequestedHandler = OnWorkbenchHeightDifferenceEdgeDisplayRequested;
        _workbenchLineFitDisplayRequestedHandler = OnWorkbenchLineFitDisplayRequested;
        _workbenchLineFitDisplayClearedHandler = (_, _) => _viewer.ClearWorkbenchLineFit();
        _workbenchLineIntersectionDisplayRequestedHandler = OnWorkbenchLineIntersectionDisplayRequested;
        _workbenchLineIntersectionDisplayClearedHandler = (_, _) => _viewer.ClearWorkbenchLineIntersection();
        _workbenchLandmarkCorrespondenceDisplayRequestedHandler = OnWorkbenchLandmarkCorrespondenceDisplayRequested;
        _workbenchLandmarkCorrespondenceDisplayClearedHandler = (_, _) => _viewer.ClearWorkbenchLandmarkCorrespondence();
        _viewerWorkbenchLineFitPointSelectedHandler = (_, args) => _viewModel.Workbench.SelectLineFitDiagnostic(args.InputPointIndex);
        _viewerTeachingCaptureStateChangedHandler = OnViewerTeachingCaptureStateChanged;
        _viewModel.RefreshRecipeComparisonRequested += _refreshRecipeComparisonRequestedHandler;
        _viewModel.SaveRecipeRequested += _saveRecipeRequestedHandler;
        _viewModel.ApplyRoiAlignmentRequested += _applyRoiAlignmentRequestedHandler;
        _viewModel.FitPlaneRequested += _fitPlaneRequestedHandler;
        _viewModel.PublishInspectionResultRequested += _publishInspectionResultRequestedHandler;
        _viewModel.Calibration.LoadStudyRequested += _calibrationLoadStudyRequestedHandler;
        _viewModel.OpenEvidenceArtifactRequested += _openEvidenceArtifactRequestedHandler;
        _viewModel.Workbench.NewTeachingRecipeRequested += _workbenchNewTeachingRecipeRequestedHandler;
        _viewModel.Workbench.SaveTeachingRecipeRequested += _workbenchSaveTeachingRecipeRequestedHandler;
        _viewModel.Workbench.SaveTeachingRecipeAsRequested += _workbenchSaveTeachingRecipeAsRequestedHandler;
        _viewModel.Workbench.OpenTeachingRecipeRequested += _workbenchOpenTeachingRecipeRequestedHandler;
        _viewModel.Workbench.OpenRecentTeachingRecipeRequested += _workbenchOpenRecentTeachingRecipeRequestedHandler;
        _viewModel.Workbench.LoadC3DSourceRequested += _workbenchLoadC3DSourceRequestedHandler;
        _viewModel.Workbench.BeginTeachingSelectionCaptureRequested += _workbenchBeginTeachingCaptureRequestedHandler;
        _viewModel.Workbench.UndoTeachingSelectionCaptureRequested += _workbenchUndoTeachingCaptureRequestedHandler;
        _viewModel.Workbench.CancelTeachingSelectionCaptureRequested += _workbenchCancelTeachingCaptureRequestedHandler;
        _viewModel.Workbench.ApplyTeachingSelectionCaptureRequested += _workbenchApplyTeachingCaptureRequestedHandler;
        _viewModel.Workbench.AppliedTeachingSelectionsChanged += _workbenchAppliedTeachingSelectionsChangedHandler;
        _viewModel.Workbench.FilterDisplayRequested += _workbenchFilterDisplayRequestedHandler;
        _viewModel.Workbench.HeightDifferenceEdgeDisplayRequested += _workbenchHeightDifferenceEdgeDisplayRequestedHandler;
        _viewModel.Workbench.LineFitDisplayRequested += _workbenchLineFitDisplayRequestedHandler;
        _viewModel.Workbench.LineFitDisplayCleared += _workbenchLineFitDisplayClearedHandler;
        _viewModel.Workbench.LineIntersectionDisplayRequested += _workbenchLineIntersectionDisplayRequestedHandler;
        _viewModel.Workbench.LineIntersectionDisplayCleared += _workbenchLineIntersectionDisplayClearedHandler;
        _viewModel.Workbench.LandmarkCorrespondenceDisplayRequested += _workbenchLandmarkCorrespondenceDisplayRequestedHandler;
        _viewModel.Workbench.LandmarkCorrespondenceDisplayCleared += _workbenchLandmarkCorrespondenceDisplayClearedHandler;
        _viewer.WorkbenchLineFitPointSelected += _viewerWorkbenchLineFitPointSelectedHandler;
        _viewer.TeachingCaptureStateChanged += _viewerTeachingCaptureStateChangedHandler;

        ConfigureCalibrationStudyFromCommandLine();
        ConfigureToolTeachingRecipeFromCommandLine();
        ConfigureWorkbenchBottomPaneFromCommandLine();
        SyncAppliedTeachingSelections();
        Loaded += EnsureWorkbenchViewerSourceConsistency;
        EnableShellSmokeFromCommandLine();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!IsAutomatedShellRun() && !TryResolveWorkbenchChanges("closing 3D Studio"))
        {
            e.Cancel = true;
            return;
        }

        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        OVLog.Write(LogCategory.System, LogLevel.Info, "OpenVisionLab 3D Studio shutdown.");
        _viewer.HostStateChanged -= _viewerHostStateChangedHandler;
        _viewer.ProfileViewRequested -= _profileViewRequestedHandler;
        _viewModel.RefreshRecipeComparisonRequested -= _refreshRecipeComparisonRequestedHandler;
        _viewModel.SaveRecipeRequested -= _saveRecipeRequestedHandler;
        _viewModel.ApplyRoiAlignmentRequested -= _applyRoiAlignmentRequestedHandler;
        _viewModel.FitPlaneRequested -= _fitPlaneRequestedHandler;
        _viewModel.PublishInspectionResultRequested -= _publishInspectionResultRequestedHandler;
        _viewModel.Calibration.LoadStudyRequested -= _calibrationLoadStudyRequestedHandler;
        _viewModel.OpenEvidenceArtifactRequested -= _openEvidenceArtifactRequestedHandler;
        _viewModel.Workbench.NewTeachingRecipeRequested -= _workbenchNewTeachingRecipeRequestedHandler;
        _viewModel.Workbench.SaveTeachingRecipeRequested -= _workbenchSaveTeachingRecipeRequestedHandler;
        _viewModel.Workbench.SaveTeachingRecipeAsRequested -= _workbenchSaveTeachingRecipeAsRequestedHandler;
        _viewModel.Workbench.OpenTeachingRecipeRequested -= _workbenchOpenTeachingRecipeRequestedHandler;
        _viewModel.Workbench.OpenRecentTeachingRecipeRequested -= _workbenchOpenRecentTeachingRecipeRequestedHandler;
        _viewModel.Workbench.LoadC3DSourceRequested -= _workbenchLoadC3DSourceRequestedHandler;
        _viewModel.Workbench.BeginTeachingSelectionCaptureRequested -= _workbenchBeginTeachingCaptureRequestedHandler;
        _viewModel.Workbench.UndoTeachingSelectionCaptureRequested -= _workbenchUndoTeachingCaptureRequestedHandler;
        _viewModel.Workbench.CancelTeachingSelectionCaptureRequested -= _workbenchCancelTeachingCaptureRequestedHandler;
        _viewModel.Workbench.ApplyTeachingSelectionCaptureRequested -= _workbenchApplyTeachingCaptureRequestedHandler;
        _viewModel.Workbench.AppliedTeachingSelectionsChanged -= _workbenchAppliedTeachingSelectionsChangedHandler;
        _viewModel.Workbench.FilterDisplayRequested -= _workbenchFilterDisplayRequestedHandler;
        _viewModel.Workbench.HeightDifferenceEdgeDisplayRequested -= _workbenchHeightDifferenceEdgeDisplayRequestedHandler;
        _viewModel.Workbench.LineFitDisplayRequested -= _workbenchLineFitDisplayRequestedHandler;
        _viewModel.Workbench.LineFitDisplayCleared -= _workbenchLineFitDisplayClearedHandler;
        _viewModel.Workbench.LineIntersectionDisplayRequested -= _workbenchLineIntersectionDisplayRequestedHandler;
        _viewModel.Workbench.LineIntersectionDisplayCleared -= _workbenchLineIntersectionDisplayClearedHandler;
        _viewModel.Workbench.LandmarkCorrespondenceDisplayRequested -= _workbenchLandmarkCorrespondenceDisplayRequestedHandler;
        _viewModel.Workbench.LandmarkCorrespondenceDisplayCleared -= _workbenchLandmarkCorrespondenceDisplayClearedHandler;
        _viewer.WorkbenchLineFitPointSelected -= _viewerWorkbenchLineFitPointSelectedHandler;
        _viewer.TeachingCaptureStateChanged -= _viewerTeachingCaptureStateChangedHandler;
        _viewModel.PropertyChanged -= _viewModelPropertyChangedHandler;
        _viewModel.InspectionTaskChanged -= _inspectionTaskChangedHandler;
        Loaded -= _shellSmokeLoadedHandler;
        Loaded -= EnsureWorkbenchViewerSourceConsistency;
        base.OnClosed(e);
    }

    private void EnsureWorkbenchViewerSourceConsistency(object sender, RoutedEventArgs args)
    {
        Loaded -= EnsureWorkbenchViewerSourceConsistency;
        if (_viewModel.IsWorkbenchWorkspaceSelected && !_viewModel.Workbench.IsSourceReadyForRecipe)
        {
            _viewer.ClearC3DTeachingSource(_viewModel.Workbench.SourceReadinessSummary);
            _viewModel.UpdateC3DSampleVisible(false);
        }
    }

    private void EnableShellSmokeFromCommandLine()
    {
        var shellScreenshotPath = GetCommandLineValue("--shell-smoke-screenshot");
        var screenshotQualityReportPath = GetCommandLineValue("--shell-screenshot-quality-report");
        var recipeManagerScreenshotPath = GetCommandLineValue("--recipe-manager-screenshot");
        var recipeManagerScreenshotQualityReportPath = GetCommandLineValue("--recipe-manager-screenshot-quality-report");
        var filterToolLabScreenshotPath = GetCommandLineValue("--filter-tool-lab-screenshot");
        var filterToolLabScreenshotQualityReportPath = GetCommandLineValue("--filter-tool-lab-screenshot-quality-report");
        var edgeToolLabScreenshotPath = GetCommandLineValue("--edge-tool-lab-screenshot");
        var edgeToolLabScreenshotQualityReportPath = GetCommandLineValue("--edge-tool-lab-screenshot-quality-report");
        var lineIntersectionToolLabScreenshotPath = GetCommandLineValue("--line-intersection-tool-lab-screenshot");
        var lineIntersectionToolLabScreenshotQualityReportPath = GetCommandLineValue("--line-intersection-tool-lab-screenshot-quality-report");
        var landmarkCorrespondenceToolLabScreenshotPath = GetCommandLineValue("--landmark-correspondence-tool-lab-screenshot");
        var landmarkCorrespondenceToolLabScreenshotQualityReportPath = GetCommandLineValue("--landmark-correspondence-tool-lab-screenshot-quality-report");
        var smokeSaveRecipePath = GetCommandLineValue("--smoke-save-recipe");
        var teachingSelectionSmokeMode = GetCommandLineValue("--smoke-tool-teaching-selection");
        var teachingSelectionSmokeReportPath = GetCommandLineValue("--smoke-tool-teaching-selection-report");
        var teachingRecipeSmokeSavePath = GetCommandLineValue("--smoke-save-tool-teaching-recipe");
        var profilePointerSmokeReportPath = GetCommandLineValue("--smoke-profile-pointer-report");
        var filterPublishSmoke = Environment.GetCommandLineArgs()
            .Contains("--smoke-tool-filter-publish", StringComparer.OrdinalIgnoreCase);
        var filterPreviewSmoke = filterPublishSmoke || Environment.GetCommandLineArgs()
            .Contains("--smoke-tool-filter-preview", StringComparer.OrdinalIgnoreCase);
        var edgePublishSmoke = Environment.GetCommandLineArgs()
            .Contains("--smoke-tool-edge-publish", StringComparer.OrdinalIgnoreCase);
        var lineFitPreviewSmoke = Environment.GetCommandLineArgs()
            .Contains("--smoke-tool-line-fit-preview", StringComparer.OrdinalIgnoreCase);
        var edgePreviewSmoke = edgePublishSmoke || lineFitPreviewSmoke || Environment.GetCommandLineArgs()
            .Contains("--smoke-tool-edge-preview", StringComparer.OrdinalIgnoreCase);
        var invalidEdgeDraftSmoke = Environment.GetCommandLineArgs()
            .Contains("--smoke-wpg-invalid-edge", StringComparer.OrdinalIgnoreCase);
        var edgeStepId = GetCommandLineValue("--tool-teaching-step");
        var edgeSmokeReportPath = GetCommandLineValue("--smoke-tool-edge-report");
        var lineFitSmokeReportPath = GetCommandLineValue("--smoke-tool-line-fit-report");
        if (teachingSelectionSmokeMode is not null || profilePointerSmokeReportPath is not null || edgePreviewSmoke || lineFitPreviewSmoke)
        {
            Width = 1280;
            Height = 760;
        }

        if (int.TryParse(GetCommandLineValue("--shell-smoke-width"), out var smokeWidth)
            && int.TryParse(GetCommandLineValue("--shell-smoke-height"), out var smokeHeight)
            && smokeWidth >= MinWidth
            && smokeHeight >= MinHeight)
        {
            Width = smokeWidth;
            Height = smokeHeight;
        }

        var smokePublishResult = Environment.GetCommandLineArgs()
            .Contains("--smoke-publish-result", StringComparer.OrdinalIgnoreCase);
        var waitForNominalActualPreview = Environment.GetCommandLineArgs()
            .Contains("--smoke-nominal-actual", StringComparer.OrdinalIgnoreCase)
            || _viewer.ViewModel.NominalActualInput is not null;
        if (shellScreenshotPath is not null
            || recipeManagerScreenshotPath is not null
            || filterToolLabScreenshotPath is not null
            || edgeToolLabScreenshotPath is not null
            || lineIntersectionToolLabScreenshotPath is not null
            || landmarkCorrespondenceToolLabScreenshotPath is not null
            || _viewer.HasConfiguredSmokeScreenshot
            || teachingSelectionSmokeMode is not null
            || profilePointerSmokeReportPath is not null
            || filterPreviewSmoke
            || edgePreviewSmoke
            || lineFitPreviewSmoke)
        {
            _shellSmokeLoadedHandler = async (_, _) =>
            {
                await Dispatcher.InvokeAsync(() => { });
                if (recipeManagerScreenshotPath is not null)
                {
                    ShowRecipeManagerWindow();
                    var firstRecipeManagerWindow = recipeManagerWindow;
                    ShowRecipeManagerWindow();
                    if (!ReferenceEquals(firstRecipeManagerWindow, recipeManagerWindow))
                    {
                        _viewModel.SetViewerSmokeFailed("Recipe Manager smoke opened more than one window instance.");
                        Application.Current.Shutdown(1);
                        return;
                    }
                }

                if (filterToolLabScreenshotPath is not null
                    && !ShowFilterToolLabWindow(showMissingFilterMessage: false))
                {
                    _viewModel.SetViewerSmokeFailed("Filter Tool Lab smoke requires a Filter recipe step.");
                    Application.Current.Shutdown(1);
                    return;
                }

                if (edgeToolLabScreenshotPath is not null
                    && !ShowHeightDifferenceEdgeToolLabWindow(showMissingEdgeMessage: false))
                {
                    _viewModel.SetViewerSmokeFailed("Edge Tool Lab smoke requires a Height Difference Edge recipe step.");
                    Application.Current.Shutdown(1);
                    return;
                }

                if (lineIntersectionToolLabScreenshotPath is not null
                    && !ShowLineIntersectionToolLabWindow(showMissingLineIntersectionMessage: false))
                {
                    _viewModel.SetViewerSmokeFailed("Line Intersection Tool Lab smoke requires a Line Intersection recipe step.");
                    Application.Current.Shutdown(1);
                    return;
                }

                if (landmarkCorrespondenceToolLabScreenshotPath is not null
                    && !ShowLandmarkCorrespondenceToolLabWindow(showMissingCorrespondenceMessage: false))
                {
                    _viewModel.SetViewerSmokeFailed("Landmark Correspondence Tool Lab smoke requires a Landmark Correspondence recipe step.");
                    Application.Current.Shutdown(1);
                    return;
                }

                if (invalidEdgeDraftSmoke
                    && !_viewModel.Workbench.TryConfigureInvalidHeightDifferenceEdgeDraftForSmoke())
                {
                    _viewModel.SetViewerSmokeFailed("Invalid Edge WPG smoke requires a selected Height Difference Edge step.");
                    Application.Current.Shutdown(1);
                    return;
                }

                if (filterPreviewSmoke
                    && !await _viewModel.Workbench.PreviewSelectedFilterAsync())
                {
                    _viewModel.SetViewerSmokeFailed(_viewModel.Workbench.FilterExecutionSummary);
                    Application.Current.Shutdown(1);
                    return;
                }

                if (filterPublishSmoke)
                {
                    _viewModel.Workbench.PublishSelectedStepCommand.Execute(null);
                    if (!_viewModel.Workbench.IsFilterPreviewPublished)
                    {
                        _viewModel.SetViewerSmokeFailed("Filter Publish did not accept the current Preview output.");
                        Application.Current.Shutdown(1);
                        return;
                    }
                }

                if (edgePreviewSmoke)
                {
                    var filterStep = _viewModel.Workbench.PipelineSteps.FirstOrDefault(step =>
                        string.Equals(step.ToolId, "filter", StringComparison.Ordinal));
                    if (filterStep is null
                        || !_viewModel.Workbench.SelectPipelineStep(filterStep.Id)
                        || !await _viewModel.Workbench.PreviewSelectedFilterAsync())
                    {
                        _viewModel.SetViewerSmokeFailed("Edge smoke could not create the explicit Filter Preview prerequisite.");
                        Application.Current.Shutdown(1);
                        return;
                    }
                    _viewModel.Workbench.PublishSelectedStepCommand.Execute(null);
                    if (!_viewModel.Workbench.IsFilterPreviewPublished
                        || string.IsNullOrWhiteSpace(edgeStepId)
                        || !_viewModel.Workbench.TryConfigureHeightDifferenceEdgeSmoke(
                            edgeStepId,
                            new ToolRecipeGridRectangle(285, 290, 135, 16),
                            "AcrossColumns",
                            "Rising",
                            "100",
                            out var edgeConfiguration))
                    {
                        _viewModel.SetViewerSmokeFailed("Edge smoke prerequisite failed: Published Filter or smoke-only search band is unavailable.");
                        Application.Current.Shutdown(1);
                        return;
                    }
                    OVLog.Write(LogCategory.UI, LogLevel.Info, $"Edge smoke-only configuration: {edgeConfiguration}; no recipe file was saved.");
                    if (!await _viewModel.Workbench.PreviewSelectedHeightDifferenceEdgeAsync())
                    {
                        _viewModel.SetViewerSmokeFailed(_viewModel.Workbench.HeightDifferenceEdgeExecutionSummary);
                        Application.Current.Shutdown(1);
                        return;
                    }
                    if (edgeSmokeReportPath is not null
                        && _viewModel.Workbench.CurrentHeightDifferenceEdgeOutput is { } edgeOutput)
                    {
                        var diagnostics = edgeOutput.Diagnostics;
                        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(edgeSmokeReportPath))!);
                        File.WriteAllLines(edgeSmokeReportPath,
                        [
                            "OpenVisionLab 3D Height Difference Edge smoke-only report",
                            "Boundary|Smoke-only band and threshold are not saved teaching values or production evidence.",
                            $"Input|entity={edgeOutput.InputEntityId}|sha256={edgeOutput.InputContentSha256}|rootSha256={edgeOutput.RootSourceSha256}",
                            $"Selection|row={edgeOutput.Selection.Row}|column={edgeOutput.Selection.Column}|rowCount={edgeOutput.Selection.RowCount}|columnCount={edgeOutput.Selection.ColumnCount}",
                            $"Rule|axis={edgeOutput.ComparisonAxis}|polarity={edgeOutput.Polarity}|minimumDelta={edgeOutput.MinimumDelta:R}",
                            $"Output|entity={edgeOutput.OutputEntityId}|points={edgeOutput.Points.Count}|sha256={edgeOutput.ContentSha256}",
                            $"Diagnostics|scanlines={diagnostics.ScanlineCount}|eligiblePairs={diagnostics.EligiblePairCount}|missingPairSkips={diagnostics.SkippedMissingPairCount}|accepted={diagnostics.AcceptedScanlineCount}|noCandidate={diagnostics.NoCandidateScanlineCount}|magnitudeMin={diagnostics.AcceptedMagnitudeMinimum:R}|magnitudeMax={diagnostics.AcceptedMagnitudeMaximum:R}|magnitudeMean={diagnostics.AcceptedMagnitudeMean:R}"
                        ]);
                    }
                    if (edgePublishSmoke)
                    {
                        _viewModel.Workbench.PublishSelectedStepCommand.Execute(null);
                        if (!_viewModel.Workbench.IsEdgePreviewPublished)
                        {
                            _viewModel.SetViewerSmokeFailed("Height Difference Edge Publish did not reuse the current Preview output.");
                            Application.Current.Shutdown(1);
                            return;
                        }
                    }
                }

                if (lineFitPreviewSmoke)
                {
                    if (!_viewModel.Workbench.IsEdgePreviewPublished)
                    {
                        _viewModel.Workbench.PublishSelectedStepCommand.Execute(null);
                    }
                    if (_viewModel.Workbench.CurrentHeightDifferenceEdgeOutput is not { } edgeOutput
                        || !_viewModel.Workbench.IsEdgePreviewPublished
                        || !_viewModel.Workbench.TryConfigureLineFitSmoke(
                            edgeOutput.OutputEntityId,
                            "100",
                            "3",
                            "0.10",
                            "2",
                            out var lineFitConfiguration)
                        || !await _viewModel.Workbench.PreviewSelectedLineFitAsync())
                    {
                        _viewModel.SetViewerSmokeFailed($"Line Fit smoke prerequisite failed: {_viewModel.Workbench.LineFitExecutionSummary}");
                        Application.Current.Shutdown(1);
                        return;
                    }
                    OVLog.Write(LogCategory.UI, LogLevel.Info, lineFitConfiguration);
                    if (lineFitSmokeReportPath is not null && _viewModel.Workbench.CurrentLineFitOutput is { } lineFitOutput)
                    {
                        var diagnostics = lineFitOutput.Diagnostics;
                        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(lineFitSmokeReportPath))!);
                        File.WriteAllLines(lineFitSmokeReportPath,
                        [
                            "OpenVisionLab 3D Line Fit smoke-only report",
                            "Boundary|Smoke-only limits are not saved teaching values, production evidence, inspection OK/NG, or metrology evidence.",
                            $"Input|entity={lineFitOutput.InputEdgePointSetEntityId}|sha256={lineFitOutput.InputContentSha256}|rootSha256={lineFitOutput.RootSourceSha256}",
                            $"Output|entity={lineFitOutput.OutputEntityId}|sha256={lineFitOutput.ContentSha256}|points={diagnostics.InputPointCount}|inliers={diagnostics.InlierCount}|outliers={diagnostics.OutlierCount}",
                            $"Diagnostics|residualRms={diagnostics.ResidualRms:R}|residualMax={diagnostics.ResidualMaximum:R}|scanlineSpan={diagnostics.InlierScanlineSpan}|plotPoints={_viewModel.Workbench.LineFitResidualPlotPoints.Count}",
                            $"Line|anchor={lineFitOutput.AnchorX:R},{lineFitOutput.AnchorY:R},{lineFitOutput.AnchorZ:R}|direction={lineFitOutput.DirectionX:R},{lineFitOutput.DirectionY:R},{lineFitOutput.DirectionZ:R}"
                        ]);
                    }
                }

                if (teachingSelectionSmokeMode is not null
                    && !await RunToolTeachingSelectionSmokeAsync(
                        teachingSelectionSmokeMode,
                        teachingSelectionSmokeReportPath))
                {
                    Application.Current.Shutdown(1);
                    return;
                }

                if (teachingRecipeSmokeSavePath is not null
                    && !_viewModel.Workbench.TrySaveTeachingRecipe(teachingRecipeSmokeSavePath, out var teachingSaveMessage))
                {
                    _viewModel.SetViewerSmokeFailed(teachingSaveMessage);
                    Application.Current.Shutdown(1);
                    return;
                }

                var nominalActualReady = !waitForNominalActualPreview
                    || await WaitForNominalActualPreviewAsync(TimeSpan.FromMinutes(10));
                if (!nominalActualReady)
                {
                    _viewModel.SetViewerSmokeFailed(
                        "Nominal/actual Preview did not complete before Shell screenshot capture.");
                }

                if (!_viewer.ApplyConfiguredSmokeNextDensity())
                {
                    _viewModel.SetViewerSmokeFailed(_viewer.HostState.ViewerStatus);
                }

                if (!_viewer.ApplyConfiguredSmokePick())
                {
                    _viewModel.SetViewerSmokeFailed(_viewer.HostState.ViewerStatus);
                }

                if (!await _viewer.RunConfiguredPointerInputRegressionAsync())
                {
                    _viewModel.SetViewerSmokeFailed(_viewer.HostState.ViewerStatus);
                }

                if (profilePointerSmokeReportPath is not null
                    && !await _viewer.RunProfilePointerSmokeAsync(profilePointerSmokeReportPath))
                {
                    _viewModel.SetViewerSmokeFailed("Interactive height-profile pointer smoke failed.");
                    Application.Current.Shutdown(1);
                    return;
                }

                await Task.Delay(900);
                if (smokePublishResult && !_viewer.PublishCurrentPreviewResult())
                {
                    _viewModel.SetViewerSmokeFailed(
                        "Viewer Publish failed because current Preview evidence was unavailable.");
                    Application.Current.Shutdown(1);
                    return;
                }

                if (smokePublishResult)
                {
                    _viewModel.ShowReviewWorkspace();
                }

                if (smokeSaveRecipePath is not null && !_viewer.SaveCurrentRecipe(smokeSaveRecipePath, isSmoke: true))
                {
                    Application.Current.Shutdown(1);
                    return;
                }

                if (_viewer.SmokeExitCode != 0)
                {
                    _viewModel.SetViewerSmokeFailed(_viewer.HostState.ViewerStatus);
                }

                if (!await _viewer.CaptureConfiguredSmokeViewAsync())
                {
                    _viewModel.SetViewerSmokeFailed(_viewer.HostState.ViewerStatus);
                }

                UpdateLayout();
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
                await Task.Delay(100);
                if (shellScreenshotPath is not null
                    && !await CaptureWindowWithRetryAsync(this, shellScreenshotPath, screenshotQualityReportPath, "Shell"))
                {
                    _viewModel.SetViewerSmokeFailed("Shell screenshot remained blank or invalid after 3 attempts.");
                    Application.Current.Shutdown(1);
                    return;
                }

                if (recipeManagerScreenshotPath is not null
                    && (recipeManagerWindow is null
                        || !await CaptureWindowWithRetryAsync(
                            recipeManagerWindow,
                            recipeManagerScreenshotPath,
                            recipeManagerScreenshotQualityReportPath,
                            "RecipeManager")))
                {
                    _viewModel.SetViewerSmokeFailed("Recipe Manager screenshot remained blank or invalid after 3 attempts.");
                    Application.Current.Shutdown(1);
                    return;
                }

                if (filterToolLabScreenshotPath is not null
                    && (filterToolLabWindow is null
                        || !await CaptureWindowWithRetryAsync(
                            filterToolLabWindow,
                            filterToolLabScreenshotPath,
                            filterToolLabScreenshotQualityReportPath,
                            "FilterToolLab")))
                {
                    _viewModel.SetViewerSmokeFailed("Filter Tool Lab screenshot remained blank or invalid after 3 attempts.");
                    Application.Current.Shutdown(1);
                    return;
                }

                if (edgeToolLabScreenshotPath is not null
                    && (heightDifferenceEdgeToolLabWindow is null
                        || !await CaptureWindowWithRetryAsync(
                            heightDifferenceEdgeToolLabWindow,
                            edgeToolLabScreenshotPath,
                            edgeToolLabScreenshotQualityReportPath,
                            "EdgeToolLab")))
                {
                    _viewModel.SetViewerSmokeFailed("Edge Tool Lab screenshot remained blank or invalid after 3 attempts.");
                    Application.Current.Shutdown(1);
                    return;
                }

                if (lineIntersectionToolLabScreenshotPath is not null
                    && (lineIntersectionToolLabWindow is null
                        || !await CaptureWindowWithRetryAsync(
                            lineIntersectionToolLabWindow,
                            lineIntersectionToolLabScreenshotPath,
                            lineIntersectionToolLabScreenshotQualityReportPath,
                            "LineIntersectionToolLab")))
                {
                    _viewModel.SetViewerSmokeFailed("Line Intersection Tool Lab screenshot remained blank or invalid after 3 attempts.");
                    Application.Current.Shutdown(1);
                    return;
                }

                if (landmarkCorrespondenceToolLabScreenshotPath is not null
                    && (landmarkCorrespondenceToolLabWindow is null
                        || !await CaptureWindowWithRetryAsync(
                            landmarkCorrespondenceToolLabWindow,
                            landmarkCorrespondenceToolLabScreenshotPath,
                            landmarkCorrespondenceToolLabScreenshotQualityReportPath,
                            "LandmarkCorrespondenceToolLab")))
                {
                    _viewModel.SetViewerSmokeFailed("Landmark Correspondence Tool Lab screenshot remained blank or invalid after 3 attempts.");
                    Application.Current.Shutdown(1);
                    return;
                }

                await Task.Delay(100);
                Application.Current.Shutdown(
                    nominalActualReady ? _viewer.SmokeExitCode : 1);
            };

            Loaded += _shellSmokeLoadedHandler;
        }
    }

    private async Task<bool> RunToolTeachingSelectionSmokeAsync(string modeValue, string? reportPath)
    {
        var mode = modeValue.Trim().ToLowerInvariant();
        var workbench = _viewModel.Workbench;
        var step = workbench.SelectedPipelineStep;
        var previewBefore = _viewer.ViewModel.PreviewToolResult;
        var resultEntitiesBefore = _viewer.ViewModel.ResultEntities;
        var dirtyBefore = workbench.IsDirty;
        var schemaBefore = workbench.RecipeSchemaVersion;
        var selectionCountBefore = workbench.Selections.Count;
        var inputIdsBefore = step?.InputEntityIds.ToArray() ?? [];
        var lines = new List<string>
        {
            "OpenVisionLab 3D generic teaching-selection Shell smoke",
            $"Mode={mode}",
            $"Step={step?.Id ?? "(none)"}",
            $"AuthoredBefore|dirty={dirtyBefore}|schema={schemaBefore}|selections={selectionCountBefore}|inputs={string.Join(';', inputIdsBefore)}",
            $"ExecutionBefore|preview={previewBefore.Status}|results={resultEntitiesBefore.Count}"
        };

        bool Complete(bool passed, string message)
        {
            var state = _viewer.TeachingCaptureSnapshot;
            var previewUnchanged = ReferenceEquals(previewBefore, _viewer.ViewModel.PreviewToolResult);
            var resultsUnchanged = ReferenceEquals(resultEntitiesBefore, _viewer.ViewModel.ResultEntities);
            lines.Add($"CaptureAfter|active={state.IsActive}|progress={state.CapturedPointCount}/{state.RequiredPointCount}|canUndo={state.CanUndo}|canApply={state.CanApply}|message={state.Message}");
            lines.Add($"AuthoredAfter|dirty={workbench.IsDirty}|schema={workbench.RecipeSchemaVersion}|selections={workbench.Selections.Count}|inputs={string.Join(';', step?.InputEntityIds ?? [])}");
            lines.Add($"ExecutionAfter|preview={_viewer.ViewModel.PreviewToolResult.Status}|results={_viewer.ViewModel.ResultEntities.Count}|previewReferenceUnchanged={previewUnchanged}|resultReferenceUnchanged={resultsUnchanged}");
            lines.Add($"Result={(passed ? "PASS" : "FAIL")}|{message}");
            WriteTeachingSelectionSmokeReport(reportPath, lines);
            Console.WriteLine(lines[^1]);
            if (!passed)
            {
                _viewModel.SetViewerSmokeFailed(message);
            }

            return passed;
        }

        bool AuthoredStateUnchanged() =>
            workbench.IsDirty == dirtyBefore
            && string.Equals(workbench.RecipeSchemaVersion, schemaBefore, StringComparison.Ordinal)
            && workbench.Selections.Count == selectionCountBefore
            && (step?.InputEntityIds ?? []).SequenceEqual(inputIdsBefore, StringComparer.OrdinalIgnoreCase);

        bool ExecutionStateUnchanged() =>
            ReferenceEquals(previewBefore, _viewer.ViewModel.PreviewToolResult)
            && ReferenceEquals(resultEntitiesBefore, _viewer.ViewModel.ResultEntities);

        if (step is null)
        {
            return Complete(false, "No teaching pipeline step is selected.");
        }

        if (dirtyBefore
            || !string.Equals(schemaBefore, ToolRecipeDocument.LegacySchemaVersion, StringComparison.Ordinal)
            || selectionCountBefore != 0)
        {
            return Complete(
                false,
                "Teaching-selection smoke requires a clean legacy 1.0 recipe with no authored selections.");
        }

        if (mode == "inactive")
        {
            var passed = !_viewer.TeachingCaptureSnapshot.IsActive
                && AuthoredStateUnchanged()
                && ExecutionStateUnchanged();
            return Complete(
                passed,
                passed
                    ? "Inactive state retained legacy authored data and did not run Preview or Run."
                    : "Inactive state changed capture, authored recipe, or execution evidence.");
        }

        if (mode is not ("capturing" or "applied"))
        {
            return Complete(false, $"Unsupported teaching-selection smoke mode: {modeValue}");
        }

        if (!workbench.BeginTeachingSelectionCaptureCommand.CanExecute(null))
        {
            return Complete(false, "The selected step cannot begin Viewer teaching capture.");
        }

        workbench.BeginTeachingSelectionCaptureCommand.Execute(null);
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        if (!_viewer.TeachingCaptureSnapshot.IsActive)
        {
            return Complete(false, "Viewer teaching capture did not become active.");
        }

        if (mode == "capturing")
        {
            var state = _viewer.TeachingCaptureSnapshot;
            var passed = state.CapturedPointCount == 0
                && state.RequiredPointCount == 2
                && !state.CanApply
                && AuthoredStateUnchanged()
                && ExecutionStateUnchanged();
            return Complete(
                passed,
                passed
                    ? "Capture ribbon is active at 0/2; transient capture did not dirty the recipe or run inspection."
                    : "Capturing state violated the transient authored/execution boundary.");
        }

        var cancelPointerReportPath = string.IsNullOrWhiteSpace(reportPath)
            ? null
            : Path.Combine(
                Path.GetDirectoryName(Path.GetFullPath(reportPath))!,
                $"{Path.GetFileNameWithoutExtension(reportPath)}.cancel-pointer.txt");
        if (!await _viewer.RunTeachingCapturePointerSmokeAsync(
                cancelWhenReady: false,
                cancelPointerReportPath))
        {
            return Complete(false, _viewer.HostState.ViewerStatus);
        }

        var cancelCandidateState = _viewer.TeachingCaptureSnapshot;
        if (!cancelCandidateState.IsActive
            || cancelCandidateState.CapturedPointCount != 2
            || !cancelCandidateState.CanApply
            || !workbench.CancelTeachingSelectionCaptureCommand.CanExecute(null))
        {
            return Complete(false, "Actual Viewer pointer capture did not produce a cancellable 2-point candidate.");
        }

        workbench.CancelTeachingSelectionCaptureCommand.Execute(null);
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        var cancelBoundaryPassed = !_viewer.TeachingCaptureSnapshot.IsActive
            && !workbench.IsTeachingSelectionCaptureActive
            && AuthoredStateUnchanged()
            && ExecutionStateUnchanged();
        lines.Add($"CancelBoundary|pass={cancelBoundaryPassed}|authoredUnchanged={AuthoredStateUnchanged()}|executionUnchanged={ExecutionStateUnchanged()}");
        if (!cancelBoundaryPassed)
        {
            return Complete(false, "Cancel after two real Viewer picks changed authored or execution state.");
        }

        if (!workbench.BeginTeachingSelectionCaptureCommand.CanExecute(null))
        {
            return Complete(false, "The selected step could not restart Viewer teaching capture after Cancel.");
        }

        workbench.BeginTeachingSelectionCaptureCommand.Execute(null);
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        if (!_viewer.TeachingCaptureSnapshot.IsActive
            || !await _viewer.RunTeachingCapturePointerSmokeAsync(exerciseNavigationGestures: false))
        {
            return Complete(false, "Viewer teaching capture could not restart and produce a second candidate after Cancel.");
        }

        var candidateState = _viewer.TeachingCaptureSnapshot;
        if (candidateState.CapturedPointCount != 2
            || !candidateState.CanApply
            || !workbench.ApplyTeachingSelectionCaptureCommand.CanExecute(null))
        {
            return Complete(false, "Second actual Viewer pointer capture did not produce an applicable 2-point candidate.");
        }

        workbench.ApplyTeachingSelectionCaptureCommand.Execute(null);
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        _viewer.ResetView();
        _viewer.FitAll();
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        lines.Add("EvidenceView|reset=True|fitAll=True|inspectionExecution=False");
        var appliedSelection = workbench.SelectedStepTeachingSelection;
        var applied = !_viewer.TeachingCaptureSnapshot.IsActive
            && appliedSelection is not null
            && workbench.Selections.Count == selectionCountBefore + 1
            && string.Equals(workbench.RecipeSchemaVersion, ToolRecipeDocument.CurrentSchemaVersion, StringComparison.Ordinal)
            && workbench.IsDirty
            && step.InputEntityIds.Contains(appliedSelection.Id, StringComparer.OrdinalIgnoreCase)
            && ExecutionStateUnchanged();
        return Complete(
            applied,
            applied
                ? "Two real Viewer picks were applied, schema promoted to 1.1, the step route became dirty, and Preview/Run remained untouched."
                : "Applying the Viewer candidate did not satisfy recipe persistence or execution-boundary checks.");
    }

    private static void WriteTeachingSelectionSmokeReport(string? path, IReadOnlyList<string> lines)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllLines(fullPath, lines, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private async Task<bool> WaitForNominalActualPreviewAsync(TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (_viewer.ViewModel.NominalActual.State == NominalActualComparisonState.PreviewRunning
            && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(100);
        }

        return _viewer.ViewModel.NominalActual.State is NominalActualComparisonState.PreviewReady
            or NominalActualComparisonState.Published;
    }

    private async Task<bool> CaptureWindowWithRetryAsync(
        Window window,
        string path,
        string? qualityReportPath,
        string scope)
    {
        const int maximumAttempts = 3;
        var fullPath = Path.GetFullPath(path);
        var qualityLines = new List<string>();
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        for (var attempt = 1; attempt <= maximumAttempts; attempt++)
        {
            var previousRejectedPath = GetRejectedScreenshotPath(fullPath, attempt);
            if (File.Exists(previousRejectedPath))
            {
                File.Delete(previousRejectedPath);
            }
        }

        for (var attempt = 1; attempt <= maximumAttempts; attempt++)
        {
            UpdateLayout();
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);

            var result = WpfScreenshotCapture.Capture(window);
            var qualityLine = $"{scope}Screenshot|attempt={attempt}|{result.Quality.Summary}";
            qualityLines.Add(qualityLine);
            Console.WriteLine(qualityLine);
            if (result.Quality.IsAcceptable)
            {
                WpfScreenshotCapture.Save(result.Bitmap, fullPath);
                qualityLines.Add($"{scope}ScreenshotResult|accepted=True|attempts={attempt}|screenshot={fullPath}");
                WriteScreenshotQualityReport(qualityReportPath, qualityLines);
                return true;
            }

            var rejectedPath = GetRejectedScreenshotPath(fullPath, attempt);
            WpfScreenshotCapture.Save(result.Bitmap, rejectedPath);
            await Task.Delay(250);
        }

        qualityLines.Add($"{scope}ScreenshotResult|accepted=False|attempts={maximumAttempts}|screenshot={fullPath}");
        WriteScreenshotQualityReport(qualityReportPath, qualityLines);
        return false;
    }

    private static void WriteScreenshotQualityReport(string? path, IReadOnlyList<string> lines)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllLines(path, lines);
    }

    private static string GetRejectedScreenshotPath(string fullPath, int attempt) =>
        Path.Combine(
            Path.GetDirectoryName(fullPath)!,
            $"{Path.GetFileNameWithoutExtension(fullPath)}.rejected-attempt-{attempt}{Path.GetExtension(fullPath)}");

    private static string? GetCommandLineValue(string name)
    {
        var args = Environment.GetCommandLineArgs();
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    private static int GetEvidenceTabIndex(string? tabName)
    {
        return tabName?.Trim().ToLowerInvariant() switch
        {
            "runner" or "runner-report" => 1,
            "snapshot" or "run" or "run-record" => 2,
            "steps" or "timeline" => 3,
            "history" => 4,
            _ => 0
        };
    }

    private void ConfigureCalibrationStudyFromCommandLine()
    {
        var studyPath = GetCommandLineValue("--calibration-study");
        if (studyPath is null)
        {
            return;
        }

        _viewModel.IsCalibrationWorkspaceSelected = true;
        _viewModel.Calibration.SelectedSection = CalibrationSection.Repeatability;
        if (_viewModel.Calibration.LoadStudy(studyPath)
            && Environment.GetCommandLineArgs()
                .Contains("--smoke-calibration-calculate", StringComparer.OrdinalIgnoreCase))
        {
            _viewModel.Calibration.CalculateCommand.Execute(null);
        }
    }

    private void ConfigureToolTeachingRecipeFromCommandLine()
    {
        var recipePath = GetCommandLineValue("--tool-teaching-recipe");
        if (string.IsNullOrWhiteSpace(recipePath))
        {
            return;
        }

        if (!_viewModel.Workbench.TryOpenTeachingRecipe(recipePath, out var message))
        {
            OVLog.Write(LogCategory.UI, LogLevel.Error, $"Tool teaching recipe command-line load failed: {message}");
            return;
        }

        var requestedStepId = GetCommandLineValue("--tool-teaching-step");
        if (!string.IsNullOrWhiteSpace(requestedStepId)
            && !_viewModel.Workbench.SelectPipelineStep(requestedStepId))
        {
            _viewModel.SetViewerSmokeFailed($"Tool teaching step was not found: {requestedStepId}");
        }

        var source = _viewModel.Workbench.Source;
        if (!_viewModel.Workbench.IsSourceReadyForRecipe)
        {
            _viewer.ClearC3DTeachingSource(_viewModel.Workbench.SourceReadinessSummary);
            _viewModel.UpdateC3DSampleVisible(false);
            OVLog.Write(LogCategory.UI, LogLevel.Warning, $"Tool teaching recipe source is not ready: {_viewModel.Workbench.SourceReadinessSummary}");
            return;
        }

        if (_viewer.LoadC3DSource(source.Path) && _viewer.CurrentC3DSourcePath is { } loadedSourcePath)
        {
            _viewModel.Workbench.SetC3DSource(loadedSourcePath);
            _viewModel.UpdateC3DSampleVisible(_viewer.HostState.C3DSampleVisible);
            if (_viewModel.IsWorkbenchWorkspaceSelected)
            {
                _viewer.ViewModel.HudDetailsVisible = false;
            }
            return;
        }

        OVLog.Write(LogCategory.UI, LogLevel.Error, $"Tool teaching recipe source load failed: {_viewer.HostState.ViewerStatus}");
    }

    private void ConfigureWorkspaceFromCommandLine()
    {
        var requestedWorkspace = GetCommandLineValue("--shell-workspace");
        if (Enum.TryParse<ShellWorkspaceMode>(requestedWorkspace, ignoreCase: true, out var workspace)
            && Enum.IsDefined(typeof(ShellWorkspaceMode), workspace))
        {
            _viewModel.SelectWorkspaceCommand.Execute(workspace);
        }
    }

    private void ConfigureInspectionTaskFromCommandLine()
    {
        var requestedTask = GetCommandLineValue("--shell-task");
        if (Enum.TryParse<ShellInspectionTask>(requestedTask, ignoreCase: true, out var task)
            && Enum.IsDefined(typeof(ShellInspectionTask), task))
        {
            _viewModel.SelectInspectionTask(task);
        }
    }

    private void ConfigureWorkbenchBottomPaneFromCommandLine()
    {
        switch (GetCommandLineValue("--workbench-bottom-pane")?.Trim().ToLowerInvariant())
        {
            case "session" or "session-log":
                ToolWorkbench.ActivateSessionLogPane();
                break;
            case "profile" or "height-profile":
                ToolWorkbench.ActivateProfilePane();
                break;
            case "fit" or "fit-diagnostics":
                ToolWorkbench.ActivateFitDiagnosticsPane();
                break;
            case "intersection" or "intersection-evidence":
                ToolWorkbench.ActivateIntersectionEvidencePane();
                break;
            case "correspondence" or "correspondence-evidence":
                ToolWorkbench.ActivateCorrespondenceEvidencePane();
                break;
        }
    }

    private void LoadSelectedInspectionTask()
    {
        var recipeFileName = _viewModel.SelectedInspectionTask == ShellInspectionTask.Warpage
            ? "c3d-warpage.recipe.json"
            : "c3d-thickness.recipe.json";
        _viewer.LoadInspectionTaskRecipe(recipeFileName);
    }

    private void OnCalibrationLoadStudyRequested(object? sender, EventArgs args)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Load Thickness Repeatability Study",
            Filter = "Thickness Repeatability Study (*.json)|*.json|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) == true)
        {
            _viewModel.Calibration.LoadStudy(dialog.FileName);
        }
    }

    private void OnWorkbenchLoadC3DSourceRequested(object? sender, EventArgs args)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Load C3D Source for Tool Recipe Teaching",
            Filter = "C3D height map (*.C3D)|*.C3D|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        if (_viewer.LoadC3DSource(dialog.FileName) && _viewer.CurrentC3DSourcePath is { } sourcePath)
        {
            _viewModel.Workbench.SetC3DSource(sourcePath);
            _viewer.ViewModel.HudDetailsVisible = false;
            return;
        }

        MessageBox.Show(
            this,
            _viewer.HostState.ViewerStatus,
            "Load C3D Source",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private void OnWorkbenchBeginTeachingCaptureRequested(
        object? sender,
        ToolWorkbenchTeachingCaptureRequestEventArgs args)
    {
        var request = new TeachingCaptureRequest(
            args.SelectionId,
            args.SelectionName,
            args.Kind,
            args.RequiredPointCount,
            args.RootSourceId,
            args.FrameId,
            args.SourceBinding);
        if (!_viewer.BeginC3DTeachingCapture(request, out var message))
        {
            _viewModel.Workbench.RejectTeachingSelectionCapture(message);
            return;
        }

        ApplyViewerTeachingCaptureState(_viewer.TeachingCaptureSnapshot);
    }

    private void OnWorkbenchUndoTeachingCaptureRequested(object? sender, EventArgs args)
    {
        _viewer.UndoC3DTeachingCapture();
        ApplyViewerTeachingCaptureState(_viewer.TeachingCaptureSnapshot);
    }

    private void OnWorkbenchCancelTeachingCaptureRequested(object? sender, EventArgs args)
    {
        _viewer.CancelC3DTeachingCapture();
        ApplyViewerTeachingCaptureState(_viewer.TeachingCaptureSnapshot);
    }

    private void OnWorkbenchApplyTeachingCaptureRequested(object? sender, EventArgs args)
    {
        if (!_viewer.TryGetC3DTeachingCandidate(out var selection, out var message))
        {
            var state = _viewer.TeachingCaptureSnapshot;
            _viewModel.Workbench.UpdateTeachingSelectionCaptureState(
                state.IsActive,
                state.CapturedPointCount,
                state.RequiredPointCount,
                state.CanApply,
                message);
            return;
        }

        if (!_viewModel.Workbench.TryApplyCapturedTeachingSelection(selection, out message))
        {
            var state = _viewer.TeachingCaptureSnapshot;
            _viewModel.Workbench.UpdateTeachingSelectionCaptureState(
                state.IsActive,
                state.CapturedPointCount,
                state.RequiredPointCount,
                state.CanApply,
                message);
            return;
        }

        _viewer.ConfirmC3DTeachingCaptureApplied();
        SyncAppliedTeachingSelections();
    }

    private void OnViewerTeachingCaptureStateChanged(
        object? sender,
        TeachingCaptureStateChangedEventArgs args) =>
        ApplyViewerTeachingCaptureState(args.State);

    private void ApplyViewerTeachingCaptureState(TeachingCaptureState state) =>
        _viewModel.Workbench.UpdateTeachingSelectionCaptureState(
            state.IsActive,
            state.CapturedPointCount,
            state.RequiredPointCount,
            state.CanApply,
            state.Message);

    private void SyncAppliedTeachingSelections() =>
        _viewer.SetAppliedTeachingSelections(_viewModel.Workbench.GetCurrentAppliedTeachingSelections());

    private void OnWorkbenchFilterDisplayRequested(
        object? sender,
        ToolWorkbenchFilterDisplayRequestEventArgs args)
    {
        if (filterToolLabWindow is { IsVisible: true })
        {
            filterToolLabWindow.ShowFilterResult(args);
            return;
        }

        var hashLabel = args.ContentSha256.Length >= 12
            ? args.ContentSha256[..12]
            : args.ContentSha256;
        var label = args.IsSource
            ? $"Source | {Path.GetFileName(args.C3DPath)}"
            : $"Filter Preview | {hashLabel}";
        if (_viewer.ShowC3DWorkbenchResult(args.C3DPath, label))
        {
            _viewModel.UpdateC3DSampleVisible(_viewer.HostState.C3DSampleVisible);
            SyncAppliedTeachingSelections();
            return;
        }

        OVLog.Write(LogCategory.UI, LogLevel.Error, _viewer.HostState.ViewerStatus);
    }

    private void OpenRecipeManagerRequested(object? sender, EventArgs args)
    {
        ShowRecipeManagerWindow();
    }

    private void ShowRecipeManagerWindow()
    {
        if (recipeManagerWindow is null)
        {
            recipeManagerWindow = new RecipeManagerWindow
            {
                Owner = this,
                DataContext = _viewModel.Workbench
            };
            recipeManagerWindow.Closed += (_, _) => recipeManagerWindow = null;
        }

        recipeManagerWindow.Show();
        recipeManagerWindow.Activate();
    }

    private void OpenFilterToolLabRequested(object? sender, EventArgs args)
    {
        ShowFilterToolLabWindow(showMissingFilterMessage: true);
    }

    private bool ShowFilterToolLabWindow(bool showMissingFilterMessage)
    {
        if (!_viewModel.Workbench.SelectFirstPipelineStepForTool("filter"))
        {
            if (showMissingFilterMessage)
            {
                MessageBox.Show(
                    this,
                    "Open or add a Filter step before opening Filter Tool Lab.",
                    "Filter Tool Lab",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            return false;
        }

        if (filterToolLabWindow is null)
        {
            filterToolLabWindow = new FilterToolLabWindow(_viewModel.Workbench)
            {
                Owner = this
            };
            filterToolLabWindow.Closed += (_, _) => filterToolLabWindow = null;
        }

        filterToolLabWindow.RefreshViews();
        filterToolLabWindow.Show();
        filterToolLabWindow.Activate();
        return true;
    }

    private void OpenEdgeToolLabRequested(object? sender, EventArgs args)
    {
        ShowHeightDifferenceEdgeToolLabWindow(showMissingEdgeMessage: true);
    }

    private bool ShowHeightDifferenceEdgeToolLabWindow(bool showMissingEdgeMessage)
    {
        if (!_viewModel.Workbench.SelectFirstPipelineStepForTool("height-difference-edge"))
        {
            if (showMissingEdgeMessage)
            {
                MessageBox.Show(
                    this,
                    "Open or add a Height Difference Edge step before opening Edge Tool Lab.",
                    "Edge Tool Lab",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            return false;
        }

        if (heightDifferenceEdgeToolLabWindow is null)
        {
            heightDifferenceEdgeToolLabWindow = new HeightDifferenceEdgeToolLabWindow(_viewModel.Workbench)
            {
                Owner = this
            };
            heightDifferenceEdgeToolLabWindow.Closed += (_, _) => heightDifferenceEdgeToolLabWindow = null;
        }

        heightDifferenceEdgeToolLabWindow.RefreshViews();
        heightDifferenceEdgeToolLabWindow.Show();
        heightDifferenceEdgeToolLabWindow.Activate();
        return true;
    }

    private void OpenLineIntersectionToolLabRequested(object? sender, EventArgs args)
    {
        ShowLineIntersectionToolLabWindow(showMissingLineIntersectionMessage: true);
    }

    private bool ShowLineIntersectionToolLabWindow(bool showMissingLineIntersectionMessage)
    {
        if (!_viewModel.Workbench.SelectFirstPipelineStepForTool("line-intersection"))
        {
            if (showMissingLineIntersectionMessage)
            {
                MessageBox.Show(
                    this,
                    "Open or add a Line Intersection step before opening Line Intersection Tool Lab.",
                    "Line Intersection Tool Lab",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            return false;
        }

        if (lineIntersectionToolLabWindow is null)
        {
            lineIntersectionToolLabWindow = new LineIntersectionToolLabWindow(_viewModel.Workbench)
            {
                Owner = this
            };
            lineIntersectionToolLabWindow.Closed += (_, _) => lineIntersectionToolLabWindow = null;
        }

        lineIntersectionToolLabWindow.RefreshViews();
        lineIntersectionToolLabWindow.Show();
        lineIntersectionToolLabWindow.Activate();
        return true;
    }

    private void OpenLandmarkCorrespondenceToolLabRequested(object? sender, EventArgs args)
    {
        ShowLandmarkCorrespondenceToolLabWindow(showMissingCorrespondenceMessage: true);
    }

    private bool ShowLandmarkCorrespondenceToolLabWindow(bool showMissingCorrespondenceMessage)
    {
        if (!_viewModel.Workbench.SelectFirstPipelineStepForTool("landmark-correspondence"))
        {
            if (showMissingCorrespondenceMessage)
            {
                MessageBox.Show(
                    this,
                    "Open or add a Landmark Correspondence step before opening Landmark Correspondence Tool Lab.",
                    "Landmark Correspondence Tool Lab",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            return false;
        }

        if (landmarkCorrespondenceToolLabWindow is null)
        {
            landmarkCorrespondenceToolLabWindow = new LandmarkCorrespondenceToolLabWindow(_viewModel.Workbench)
            {
                Owner = this
            };
            landmarkCorrespondenceToolLabWindow.Closed += (_, _) => landmarkCorrespondenceToolLabWindow = null;
        }

        landmarkCorrespondenceToolLabWindow.RefreshViews();
        landmarkCorrespondenceToolLabWindow.Show();
        landmarkCorrespondenceToolLabWindow.Activate();
        return true;
    }

    private void OnWorkbenchHeightDifferenceEdgeDisplayRequested(
        object? sender,
        ToolWorkbenchHeightDifferenceEdgeDisplayRequestEventArgs args)
    {
        if (heightDifferenceEdgeToolLabWindow is { IsVisible: true })
        {
            heightDifferenceEdgeToolLabWindow.ShowEdgeResult(args);
            return;
        }

        var state = args.IsPublished ? "Published" : "Preview - not published";
        var label = $"Height Difference Edge {state} | {args.Output.ContentSha256[..12]}";
        if (_viewer.ShowC3DWorkbenchResult(args.C3DPath, label))
        {
            _viewer.ShowWorkbenchHeightDifferenceEdge(args.Output, args.IsPublished);
            _viewModel.UpdateC3DSampleVisible(_viewer.HostState.C3DSampleVisible);
            SyncAppliedTeachingSelections();
            return;
        }

        OVLog.Write(LogCategory.UI, LogLevel.Error, _viewer.HostState.ViewerStatus);
    }

    private void OnWorkbenchLineFitDisplayRequested(
        object? sender,
        ToolWorkbenchLineFitDisplayRequestEventArgs args)
    {
        _viewer.ShowWorkbenchLineFit(args.Output, args.IsPublished);
        _viewModel.UpdateC3DSampleVisible(_viewer.HostState.C3DSampleVisible);
        ToolWorkbench.ActivateFitDiagnosticsPane();
    }

    private void OnWorkbenchLineIntersectionDisplayRequested(
        object? sender,
        ToolWorkbenchLineIntersectionDisplayRequestEventArgs args)
    {
        if (lineIntersectionToolLabWindow is { IsVisible: true })
        {
            lineIntersectionToolLabWindow.ShowLineIntersectionResult(args);
        }
        _viewer.ShowWorkbenchLineIntersection(args.FirstLine, args.SecondLine, args.Output, args.IsPublished);
        _viewModel.UpdateC3DSampleVisible(_viewer.HostState.C3DSampleVisible);
        if (_viewModel.IsExpertWorkspaceSelected)
        {
            Workspace.ActivateIntersectionEvidencePane();
        }
        else
        {
            ToolWorkbench.ActivateIntersectionEvidencePane();
        }
    }

    private void OnWorkbenchLandmarkCorrespondenceDisplayRequested(
        object? sender,
        ToolWorkbenchLandmarkCorrespondenceDisplayRequestEventArgs args)
    {
        if (landmarkCorrespondenceToolLabWindow is { IsVisible: true })
        {
            landmarkCorrespondenceToolLabWindow.ShowLandmarkCorrespondenceResult(args);
        }
        _viewer.ShowWorkbenchLandmarkCorrespondence(args.Anchors, args.Output, args.IsPublished);
        _viewModel.UpdateC3DSampleVisible(_viewer.HostState.C3DSampleVisible);
        if (_viewModel.IsExpertWorkspaceSelected)
        {
            Workspace.ActivateCorrespondenceEvidencePane();
        }
        else
        {
            ToolWorkbench.ActivateCorrespondenceEvidencePane();
        }
    }

    private void OnWorkbenchNewTeachingRecipeRequested(object? sender, EventArgs args)
    {
        if (!TryResolveWorkbenchChanges("creating a new recipe"))
        {
            return;
        }

        _viewModel.Workbench.CreateNewTeachingRecipe();
    }

    private void OnWorkbenchSaveTeachingRecipeRequested(object? sender, EventArgs args)
    {
        if (TryResolveParameterDraft())
        {
            SaveWorkbenchRecipe(forceDialog: false);
        }
    }

    private void OnWorkbenchSaveTeachingRecipeAsRequested(object? sender, EventArgs args)
    {
        if (TryResolveParameterDraft())
        {
            SaveWorkbenchRecipe(forceDialog: true);
        }
    }

    private bool SaveWorkbenchRecipe(bool forceDialog)
    {
        var path = _viewModel.Workbench.RecipePath;
        if (forceDialog || string.IsNullOrWhiteSpace(path))
        {
            var dialog = new SaveFileDialog
            {
                Title = forceDialog ? "Save 3D Tool Teaching Recipe As" : "Save 3D Tool Teaching Recipe",
                Filter = "OpenVisionLab 3D teaching recipe (*.ov3d-teach.json)|*.ov3d-teach.json|JSON files (*.json)|*.json|All files (*.*)|*.*",
                FileName = string.IsNullOrWhiteSpace(path) ? "tool-recipe.ov3d-teach.json" : Path.GetFileName(path),
                InitialDirectory = string.IsNullOrWhiteSpace(path) ? null : Path.GetDirectoryName(path),
                OverwritePrompt = true
            };
            if (dialog.ShowDialog(this) != true)
            {
                return false;
            }

            path = dialog.FileName;
        }

        if (_viewModel.Workbench.TrySaveTeachingRecipe(path, out var message))
        {
            return true;
        }

        MessageBox.Show(this, message, "Save Teaching Recipe", MessageBoxButton.OK, MessageBoxImage.Warning);
        return false;
    }

    private void OnWorkbenchOpenTeachingRecipeRequested(object? sender, EventArgs args)
    {
        if (!TryResolveWorkbenchChanges("opening another recipe"))
        {
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "Open 3D Tool Teaching Recipe",
            Filter = "OpenVisionLab 3D teaching recipe (*.ov3d-teach.json)|*.ov3d-teach.json|JSON files (*.json)|*.json|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        OpenWorkbenchRecipe(dialog.FileName);
    }

    private void OnWorkbenchOpenRecentTeachingRecipeRequested(
        object? sender,
        ToolWorkbenchRecipePathRequestEventArgs args)
    {
        if (!TryResolveWorkbenchChanges("opening a recent recipe"))
        {
            return;
        }

        OpenWorkbenchRecipe(args.Path);
    }

    private void OpenWorkbenchRecipe(string path)
    {
        if (!File.Exists(path))
        {
            MessageBox.Show(this, $"Recipe file is unavailable:{Environment.NewLine}{path}", "Open Teaching Recipe", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!_viewModel.Workbench.TryOpenTeachingRecipe(path, out var message))
        {
            MessageBox.Show(this, message, "Open Teaching Recipe", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var source = _viewModel.Workbench.Source;
        if (!_viewModel.Workbench.IsSourceReadyForRecipe)
        {
            _viewer.ClearC3DTeachingSource(_viewModel.Workbench.SourceReadinessSummary);
            _viewModel.UpdateC3DSampleVisible(false);
            MessageBox.Show(
                this,
                $"The teaching recipe was opened, but its source is not ready. The recipe remains editable and no inspection was run.{Environment.NewLine}{Environment.NewLine}{_viewModel.Workbench.SourceReadinessSummary}",
                "Teaching Recipe Source",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        if (!_viewer.LoadC3DSource(source.Path))
        {
            var loadFailure = _viewer.HostState.ViewerStatus;
            _viewer.ClearC3DTeachingSource("Recipe source could not be loaded. Relink a valid C3D source.");
            _viewModel.UpdateC3DSampleVisible(false);
            MessageBox.Show(this, loadFailure, "Teaching Recipe Source", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_viewer.CurrentC3DSourcePath is { } loadedSourcePath)
        {
            _viewModel.Workbench.SetC3DSource(loadedSourcePath);
            SyncAppliedTeachingSelections();
        }
    }

    private bool TryResolveWorkbenchChanges(string action)
    {
        if (!TryResolveParameterDraft())
        {
            return false;
        }

        if (!_viewModel.Workbench.IsDirty)
        {
            return true;
        }

        var result = MessageBox.Show(
            this,
            $"Save recipe changes before {action}?",
            "Unsaved 3D Recipe",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question);
        return result switch
        {
            MessageBoxResult.Yes => SaveWorkbenchRecipe(forceDialog: false),
            MessageBoxResult.No => true,
            _ => false
        };
    }

    private bool TryResolveParameterDraft()
    {
        if (!_viewModel.Workbench.HasPendingStepParameterChanges)
        {
            return true;
        }

        var result = MessageBox.Show(
            this,
            "Apply the selected step's parameter changes? No discards only the unapplied PropertyGrid draft.",
            "Unapplied Step Parameters",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question);
        if (result == MessageBoxResult.Cancel)
        {
            return false;
        }

        if (result == MessageBoxResult.No)
        {
            _viewModel.Workbench.DiscardSelectedStepParameterDraft();
            return true;
        }

        if (!ToolWorkbench.CommitPendingParameterEdit(out var message)
            || !_viewModel.Workbench.TryApplySelectedStepParameterDraft(out message))
        {
            _viewModel.Workbench.ReportParameterDraftCommitError(message);
            MessageBox.Show(this, message, "Step Parameters", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        return true;
    }

    private static bool IsAutomatedShellRun() => Environment.GetCommandLineArgs().Any(argument =>
        argument.StartsWith("--smoke-", StringComparison.OrdinalIgnoreCase)
        || argument.StartsWith("--verify-", StringComparison.OrdinalIgnoreCase)
        || argument.StartsWith("--line-intersection-tool-lab-", StringComparison.OrdinalIgnoreCase)
        || argument.StartsWith("--landmark-correspondence-tool-lab-", StringComparison.OrdinalIgnoreCase)
        || argument.Equals("--shell-smoke-screenshot", StringComparison.OrdinalIgnoreCase));

    private void SyncWorkbenchSourceFromViewer()
    {
        if (_viewer.CurrentC3DSourcePath is { } sourcePath
            && string.IsNullOrWhiteSpace(_viewModel.Workbench.Source.Path))
        {
            _viewModel.Workbench.SetC3DSource(sourcePath);
        }
    }

    private void OnOpenEvidenceArtifactRequested(object? sender, EvidenceArtifactOpenRequestEventArgs args)
    {
        if (!File.Exists(args.Path))
        {
            MessageBox.Show(
                this,
                $"{args.Label} artifact was not found.\n\n{args.Path}",
                "Open Evidence Artifact",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(args.Path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                $"Could not open {args.Label} artifact.\n\n{args.Path}\n\n{ex.Message}",
                "Open Evidence Artifact",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void OnViewerHostStateChanged(object? sender, ViewerHostStateChangedEventArgs args)
    {
        if (args.PropertyName == nameof(ViewerHostState.C3DSampleVisible))
        {
            _viewModel.UpdateC3DSampleVisible(args.State.C3DSampleVisible);
        }
    }

    private void OnProfileViewRequested(object? sender, EventArgs args)
    {
        if (_viewModel.IsExpertWorkspaceSelected)
        {
            Workspace.ActivateProfilePane();
            return;
        }

        if (!_viewModel.IsWorkbenchWorkspaceSelected)
        {
            _viewModel.IsWorkbenchWorkspaceSelected = true;
        }

        ToolWorkbench.ActivateProfilePane();
    }

    private void OnPublishInspectionResultRequested()
    {
        if (_viewer.PublishCurrentPreviewResult())
        {
            _viewModel.ShowReviewWorkspace();
        }
    }

    private void OnShellViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(ShellMainWindowViewModel.SelectedWorkspaceMode))
        {
            UpdateViewerHost();
        }
    }

    private void UpdateViewerHost()
    {
        if (!_viewModel.IsWorkbenchWorkspaceSelected && _viewer.TeachingCaptureSnapshot.IsActive)
        {
            _viewer.CancelC3DTeachingCapture();
        }

        if (_viewModel.IsWorkbenchWorkspaceSelected)
        {
            TaskWorkspace.ViewerContent = null;
            Workspace.ViewerContent = null;
            _viewer.ViewModel.HudDetailsVisible = false;
            if (!ReferenceEquals(ToolWorkbench.ViewerContent, _viewer))
            {
                ToolWorkbench.ViewerContent = _viewer;
            }

            return;
        }

        if (_viewModel.IsExpertWorkspaceSelected)
        {
            ToolWorkbench.ViewerContent = null;
            _viewer.ViewModel.HudDetailsVisible = true;
            if (!ReferenceEquals(Workspace.ViewerContent, _viewer))
            {
                TaskWorkspace.ViewerContent = null;
                Workspace.ViewerContent = _viewer;
            }

            return;
        }

        if (_viewModel.IsTaskWorkspaceSelected)
        {
            ToolWorkbench.ViewerContent = null;
            Workspace.ViewerContent = null;
            if (!ReferenceEquals(TaskWorkspace.ViewerContent, _viewer))
            {
                TaskWorkspace.ViewerContent = _viewer;
            }

            return;
        }

        ToolWorkbench.ViewerContent = null;
        Workspace.ViewerContent = null;
        TaskWorkspace.ViewerContent = null;
    }
}
