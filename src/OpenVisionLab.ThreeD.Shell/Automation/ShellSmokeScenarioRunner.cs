extern alias OvlMessageDialogs;

using System.IO;
using System.Windows;
using System.Windows.Threading;
using OpenVisionLab.ThreeD.Shell;
using OpenVisionLab.ThreeD.Shell.Coordination;
using OpenVisionLab.ThreeD.Shell.Dialogs;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Shell.Verification.Smoke;
using OpenVisionLab.ThreeD.Shell.Views.Workbench;
using OpenVisionLab.ThreeD.Shell.Views.Tooling;
using OpenVisionLab.ThreeD.Viewer;
using OpenVisionLab.ThreeD.Viewer.Hosting;
using static OpenVisionLab.ThreeD.Shell.Verification.Smoke.ShellSmokeArtifacts;
using static OpenVisionLab.ThreeD.Shell.Verification.Smoke.ShellWindowNativeInterop;
using WpfMessageDialogButtons = OvlMessageDialogs::OpenVisionLab.Wpf.MessageDialogs.WpfMessageDialogButtons;
using WpfMessageDialogKind = OvlMessageDialogs::OpenVisionLab.Wpf.MessageDialogs.WpfMessageDialogKind;
using WpfMessageDialogOptions = OvlMessageDialogs::OpenVisionLab.Wpf.MessageDialogs.WpfMessageDialogOptions;
using WpfMessageDialogWindow = OvlMessageDialogs::OpenVisionLab.Wpf.MessageDialogs.WpfMessageDialogWindow;

namespace OpenVisionLab.ThreeD.Shell.Automation;

internal sealed class ShellSmokeScenarioContext
{
    public required Window Window { get; init; }
    public required OpenVisionThreeDViewerControl Viewer { get; init; }
    public required ShellMainWindowViewModel ViewModel { get; init; }
    public required ToolRecipeWorkbenchView Workbench { get; init; }
    public required ShellCommandLineArguments CommandLineArguments { get; init; }
    public required ShellStartupConfigurationPlan StartupConfiguration { get; init; }
    public required ShellWorkbenchLifecycleController WorkbenchLifecycle { get; init; }
    public required ShellMessageDialogController MessageDialogs { get; init; }
    public required ToolLabWindowManager ToolLabWindows { get; init; }
    public required ShellPreparationPresetAssistantSmoke PreparationPresetSmoke { get; init; }
    public required ShellValidationThresholdAssistantSmoke ValidationThresholdSmoke { get; init; }
    public required ShellSurfaceMatchInteractionSmoke SurfaceMatchInteractionSmoke { get; init; }
    public required ShellViewerWorkspaceSmoke ViewerWorkspaceSmoke { get; init; }
    public required ShellCurrentRecipeRunSmoke CurrentRecipeRunSmoke { get; init; }
    public required ShellPreparationPreviewSmokeCoordinator PreparationPreviewSmoke { get; init; }
    public required ShellRecipeMeasurementSmokeCoordinator RecipeMeasurementSmoke { get; init; }
    public required ShellWorkbenchInteractionSmokeCoordinator WorkbenchInteractionSmoke { get; init; }
    public required ShellSmokePublishCoordinator ShellSmokePublish { get; init; }
    public required ShellToolSelectionSmoke ToolSelectionSmoke { get; init; }
    public required ShellViewerPointerSmokeCoordinator ViewerPointerSmoke { get; init; }
    public required ShellTeachingSmokeCoordinator TeachingSmoke { get; init; }
    public required ShellNominalActualPreviewWaiter NominalActualPreviewWaiter { get; init; }
    public required ShellValidationSetSmokeState ValidationSetSmoke { get; init; }
    public required Action StartImportDialogSmokeTimer { get; init; }
    public required Action<ShellStartupConfigurationPlan> ConfigureResultsSectionFromCommandLine { get; init; }
    public required Func<bool> IsClosed { get; init; }
    public required Action<int> ShutdownApplication { get; init; }
}

internal sealed class ShellSmokeScenarioRunner : IDisposable
{
    private readonly ShellSmokeScenarioContext context;
    private readonly ShellSmokeOperation operation = new();
    private RoutedEventHandler loadedHandler = (_, _) => { };

    public ShellSmokeScenarioRunner(ShellSmokeScenarioContext context)
    {
        this.context = context;
    }

    public void Attach()
    {
        var smoke = ShellSmokeCommandLineOptions.Parse(context.CommandLineArguments);
        var shellScreenshotPath = smoke.ShellScreenshotPath;
        var screenshotQualityReportPath = smoke.ScreenshotQualityReportPath;
        var viewerLayoutSmoke = smoke.ViewerLayoutSmoke;
        var viewerPresentationSmoke = smoke.ViewerPresentationSmoke;
        var viewerPresentationPressedSmoke = smoke.ViewerPresentationPressedSmoke;
        string? viewerPresentationCameraLinkSmokeSummary = null;
        var integrationExchangeSmokeState = smoke.IntegrationExchangeSmokeState;
        var integrationExchangeExeRole = smoke.IntegrationExchangeExeRole;
        ShellIntegrationExchangeSmokeResult? integrationExchangeSmoke = null;
        var thicknessRepeatGridSmoke = smoke.ThicknessRepeatGridSmoke;
        var viewerPopoutScreenshotPath = smoke.ViewerPopoutScreenshotPath;
        var viewerPopoutScreenshotQualityReportPath = smoke.ViewerPopoutScreenshotQualityReportPath;
        var recipeManagerScreenshotPath = smoke.RecipeManagerScreenshotPath;
        var recipeManagerScreenshotQualityReportPath = smoke.RecipeManagerScreenshotQualityReportPath;
        var messageDialogScreenshotPath = smoke.MessageDialogScreenshotPath;
        var messageDialogScreenshotQualityReportPath = smoke.MessageDialogScreenshotQualityReportPath;
        WpfMessageDialogWindow? messageDialogSmokeWindow = null;
        var shellSmokeScreenshotEvidence = new ShellSmokeScreenshotEvidenceCoordinator(
            new ShellSmokeScreenshotEvidenceCallbacks
            {
                AppendWindowMonitorEvidence = reportPath =>
                    AppendWindowMonitorEvidence(context.Window, reportPath),
                AppendValidationThresholdEvidence = reportPath =>
                    context.ValidationThresholdSmoke.AppendEvidence(context.Window, reportPath),
                AppendPreparationPresetEvidence = (state, reportPath) =>
                    context.PreparationPresetSmoke.AppendEvidence(state, reportPath)
            },
            (path, lines) => File.AppendAllLines(path, lines));
        var shellSmokeScreenshotCapture = new ShellSmokeScreenshotCaptureCoordinator(
            new ShellSmokeScreenshotCaptureCallbacks
            {
                CaptureButtonPressed = (automationId, path, qualityPath, scope) =>
                    CaptureButtonPressedForSmokeAsync(
                        context.Window,
                        automationId,
                        path,
                        qualityPath,
                        scope),
                CaptureRecipeHealthNavigation = (path, qualityPath) =>
                    CaptureButtonPressedForSmokeAsync(
                        context.Window,
                        "NextRecipeHealthIssue",
                        path,
                        qualityPath,
                        "RecipeHealthNavigationPressed"),
                CaptureWindow = (path, qualityPath, scope) =>
                    CaptureWindowWithRetryAsync(
                        context.Window,
                        path,
                        qualityPath,
                        scope),
                AppendEvidence = request => shellSmokeScreenshotEvidence.Append(request)
            });
        var auxiliaryWindowScreenshot = new ShellAuxiliaryWindowScreenshotCoordinator(
            new ShellAuxiliaryWindowScreenshotCallbacks
            {
                CaptureViewerPopout = async (path, qualityPath) =>
                    context.Workbench.ViewerPopoutWindow is { IsVisible: true } viewerPopout
                    && await CaptureWindowWithRetryAsync(
                        viewerPopout,
                        path,
                        qualityPath,
                        "ViewerPopout"),
                CaptureRecipeManager = async (path, qualityPath, firstRecipeCreatePressed) =>
                {
                    if (context.WorkbenchLifecycle.RecipeManagerWindow is not { } recipeManagerWindow)
                    {
                        return false;
                    }

                    return firstRecipeCreatePressed
                        ? await CaptureButtonPressedForSmokeAsync(
                            recipeManagerWindow,
                            "CreateFirstRecipe",
                            path,
                            qualityPath,
                            "FirstRecipeCreatePressed")
                        : await CaptureWindowWithRetryAsync(
                            recipeManagerWindow,
                            path,
                            qualityPath,
                            "RecipeManager");
                },
                AppendRecipeManagerMonitorEvidence = qualityPath =>
                {
                    if (context.WorkbenchLifecycle.RecipeManagerWindow is { } recipeManagerWindow)
                    {
                        AppendWindowMonitorEvidence(
                            recipeManagerWindow,
                            qualityPath);
                    }
                },
                CaptureMessageDialog = async (path, qualityPath, holdPrimaryButton) =>
                    messageDialogSmokeWindow is not null
                    && await CaptureMessageDialogForSmokeAsync(
                        messageDialogSmokeWindow,
                        path,
                        qualityPath,
                        holdPrimaryButton)
            });
        var smokeSaveRecipePath = smoke.SmokeSaveRecipePath;
        var teachingSelectionSmokeMode = smoke.TeachingSelectionSmokeMode;
        var teachingSelectionSmokeReportPath = smoke.TeachingSelectionSmokeReportPath;
        var teachingRecipeSmokeSavePath = smoke.TeachingRecipeSmokeSavePath;
        var heightImagePaletteSmoke = smoke.HeightImagePaletteSmoke;
        var heightImageRangeMinimumSmoke = smoke.HeightImageRangeMinimumSmoke;
        var heightImageRangeMaximumSmoke = smoke.HeightImageRangeMaximumSmoke;
        var heightImageDisplayRangeSmokeReportPath =
            smoke.HeightImageDisplayRangeSmokeReportPath;
        var heightImagePaletteStateEvidenceDirectory =
            smoke.HeightImagePaletteStateEvidenceDirectory;
        var sharedHeightHoverRow = smoke.SharedHeightHoverRow;
        var sharedHeightHoverColumn = smoke.SharedHeightHoverColumn;
        var sharedHeightHoverSmokeReportPath =
            smoke.SharedHeightHoverSmokeReportPath;
        var heightImageRoiPointerSmoke = smoke.HeightImageRoiPointerSmoke;
        var heightImageRoiPointerSmokeReportPath =
            smoke.HeightImageRoiPointerSmokeReportPath;
        var heightImageRoiPointerSmokeSavePath =
            smoke.HeightImageRoiPointerSmokeSavePath;
        var planeFlatnessLiveA3PointerSmoke = smoke.PlaneFlatnessLiveA3PointerSmoke;
        var planeFlatnessLiveA3PointerReportPath = smoke.PlaneFlatnessLiveA3PointerReportPath;
        var planeFlatnessLiveA3PointerSavePath = smoke.PlaneFlatnessLiveA3PointerSavePath;
        var profilePointerSmokeReportPath = smoke.ProfilePointerSmokeReportPath;
        var orientedBoxPointerSmokeReportPath = smoke.OrientedBoxPointerSmokeReportPath;
        var smokeSelectToolId = smoke.SmokeSelectToolId;
        var expandSelectedToolParametersSmoke =
            smoke.ExpandSelectedToolParametersSmoke;
        var preparationPresetAssistantSmoke =
            smoke.PreparationPresetAssistantSmoke;
        var focusSelectedToolParameterSearchSmoke =
            smoke.FocusSelectedToolParameterSearchSmoke;
        var surfaceMatchExperimentPreviewSmoke =
            smoke.SurfaceMatchExperimentPreviewSmoke;
        var surfaceMatchExperimentFocusHoverSmoke =
            smoke.SurfaceMatchExperimentFocusHoverSmoke;
        var surfaceMatchCollectionPopupSmoke =
            smoke.SurfaceMatchCollectionPopupSmoke;
        var surfaceMatchCollectionPopupScreenshotPath =
            smoke.SurfaceMatchCollectionPopupScreenshotPath;
        var surfaceMatchCollectionDisabledSmoke =
            smoke.SurfaceMatchCollectionDisabledSmoke;
        var surfaceMatchCollectionNavigationFocusHoverSmoke =
            smoke.SurfaceMatchCollectionNavigationFocusHoverSmoke;
        var recipeHealthNavigationPressedSmoke =
            smoke.RecipeHealthNavigationPressedSmoke;
        var viewerToolbarPressedSmoke = smoke.ViewerToolbarPressedSmoke;
        var import3DDataPressedSmoke = smoke.Import3DDataPressedSmoke;
        var currentRecipeRunReadySmoke = smoke.CurrentRecipeRunReadySmoke;
        var currentRecipeRunPressedSmoke = smoke.CurrentRecipeRunPressedSmoke;
        var supportBundlePressedSmoke = smoke.SupportBundlePressedSmoke;
        var validationThresholdAssistantPressedSmoke =
            smoke.ValidationThresholdAssistantPressedSmoke;
        var validationThresholdAssistantDisabledSmoke =
            smoke.ValidationThresholdAssistantDisabledSmoke;
        var workbenchInteractionReportPath = smoke.WorkbenchInteractionReportPath;
        var workbenchRunLogSmoke = smoke.WorkbenchRunLogSmoke;
        var filterPublishSmoke = smoke.FilterPublishSmoke;
        var twoPointLinePublishSmoke = smoke.TwoPointLinePublishSmoke;
        var twoPointLinePreviewSmoke = smoke.TwoPointLinePreviewSmoke;
        var threePointPlanePublishSmoke = smoke.ThreePointPlanePublishSmoke;
        var threePointPlanePreviewSmoke = smoke.ThreePointPlanePreviewSmoke;
        var datumPlaneDeviationPublishSmoke = smoke.DatumPlaneDeviationPublishSmoke;
        var datumPlaneDeviationPreviewSmoke = smoke.DatumPlaneDeviationPreviewSmoke;
        var edgePublishSmoke = smoke.EdgePublishSmoke;
        var lineFitPreviewSmoke = smoke.LineFitPreviewSmoke;
        var edgePreviewSmoke = smoke.EdgePreviewSmoke;
        var invalidEdgeDraftSmoke = smoke.InvalidEdgeDraftSmoke;
        var edgeStepId = smoke.EdgeStepId;
        var edgeSmokeReportPath = smoke.EdgeSmokeReportPath;
        var lineFitSmokeReportPath = smoke.LineFitSmokeReportPath;
        if (smoke.NeedsCompactWorkbench)
        {
            context.Window.Width = 1280;
            context.Window.Height = 760;
        }

        if (smoke.WindowSize is { } smokeSize
            && smokeSize.Width >= context.Window.MinWidth
            && smokeSize.Height >= context.Window.MinHeight)
        {
            context.Window.WindowState = WindowState.Normal;
            context.Window.Width = smokeSize.Width;
            context.Window.Height = smokeSize.Height;
        }

        if (smoke.UseLeftmostVirtualScreenOrigin)
        {
            context.Window.WindowStartupLocation = WindowStartupLocation.Manual;
            _ = TryGetLeftmostWorkAreaOrigin(out var left, out var top);
            context.Window.Left = left;
            context.Window.Top = top;
        }

        var smokePublishResult = smoke.SmokePublishResult;
        var waitForNominalActualPreview = smoke.WaitForNominalActualPreview
            || context.Viewer.HostState.HasNominalActualInput;
        if (smoke.ShouldAttachLoadedHandler(context.Viewer.HasConfiguredSmokeScreenshot))
        {
            loadedHandler = async (_, _) =>
            {
                if (!operation.TryEnter())
                {
                    return;
                }

                await context.Window.Dispatcher.InvokeAsync(() => { });
                if (context.IsClosed() || !operation.IsActive)
                {
                    return;
                }

                if (smoke.OpenImport3DDataDialogSmoke)
                {
                    context.StartImportDialogSmokeTimer();
                    return;
                }
                context.ConfigureResultsSectionFromCommandLine(context.StartupConfiguration);
                if (!TryConfigureSurfaceMatchEvidenceFromCommandLine(
                        out var surfaceMatchFailure))
                {
                    context.ViewModel.SetViewerSmokeFailed(
                        surfaceMatchFailure);
                    context.ShutdownApplication(1);
                    return;
                }
                if (context.CommandLineArguments.HasFlag("--smoke-focus-selected-tool"))
                {
                    context.Workbench.ActivateSelectedToolPane();
                }

                if (context.CommandLineArguments.HasFlag("--smoke-collapse-selected-tool"))
                {
                    context.Workbench.ToggleSelectedToolSideCollapse();
                }

                if (workbenchRunLogSmoke)
                {
                    context.Workbench.ActivateSessionLogPane();
                }
                if (currentRecipeRunReadySmoke || currentRecipeRunPressedSmoke)
                {
                    var currentRecipeRun = await context.CurrentRecipeRunSmoke.PrepareAsync(
                        runSmoke: true,
                        cancellationToken: operation.Token);
                    if (currentRecipeRun.IsCanceled
                        || !operation.IsActive)
                    {
                        return;
                    }
                    if (!currentRecipeRun.Succeeded)
                    {
                        context.ViewModel.SetViewerSmokeFailed(
                            currentRecipeRun.Failure!);
                        context.ShutdownApplication(1);
                        return;
                    }
                }
                var recipeSourceWorkflow =
                    new ShellSmokeRecipeSourceWorkflowCoordinator(context, operation);
                var recipeSourceResult = await recipeSourceWorkflow.RunAsync(smoke);
                if (!recipeSourceResult.Succeeded)
                {
                    if (!recipeSourceResult.IsCanceled)
                    {
                        context.ViewModel.SetViewerSmokeFailed(
                            recipeSourceResult.Failure
                            ?? "Shell recipe/source Smoke did not complete.");
                        context.ShutdownApplication(1);
                    }

                    return;
                }

                if (messageDialogScreenshotPath is not null)
                {
                    var dialogOptions = smoke.StepRemovalDialogSmoke
                        ? context.ViewModel.Workbench.CreateSelectedStepRemovalRequest() is { } request
                            ? context.MessageDialogs.CreateRecipeStepRemovalDialogOptions(request)
                            : null
                        : new WpfMessageDialogOptions
                        {
                            Title = DialogText("ThreeD.Dialog.RecipeSave.Title", "레시피 저장", "Save Recipe"),
                            Message = DialogText(
                                "ThreeD.Dialog.RecipeSave.Failed",
                                "레시피 파일을 저장할 수 없습니다. 표시된 파일 또는 구조 오류를 확인하세요.",
                                "The recipe file could not be saved. Check the listed file or structural error."),
                            Details = "Access to the selected recipe folder was denied.",
                            Kind = WpfMessageDialogKind.Warning,
                            Buttons = WpfMessageDialogButtons.OK
                        };
                    if (dialogOptions is null)
                    {
                        context.ViewModel.SetViewerSmokeFailed(
                            "Step-removal dialog smoke requires an idle selected recipe step.");
                        context.ShutdownApplication(1);
                        return;
                    }
                    messageDialogSmokeWindow = new WpfMessageDialogWindow(dialogOptions)
                    {
                        Owner = context.Window
                    };
                    messageDialogSmokeWindow.Show();
                }

                var toolLabSmoke = new ShellToolLabSmoke(context.ToolLabWindows, context.ViewModel.Workbench);
                if (!toolLabSmoke.Prepare(smoke, out var toolLabPrepareFailure))
                {
                    context.ViewModel.SetViewerSmokeFailed(toolLabPrepareFailure);
                    context.ShutdownApplication(1);
                    return;
                }
                if (invalidEdgeDraftSmoke
                    && !context.ViewModel.Workbench.TryConfigureInvalidHeightDifferenceEdgeDraftForSmoke())
                {
                    context.ViewModel.SetViewerSmokeFailed("Invalid Edge WPG smoke requires a selected Height Difference Edge step.");
                    context.ShutdownApplication(1);
                    return;
                }

                var preparationPreview = await context.PreparationPreviewSmoke.RunAsync(
                    new ShellPreparationPreviewSmokeRequest(
                        smoke.FilterPreviewSmoke,
                        smoke.PreparationQualityComparisonSmoke,
                        smoke.RemoveOutlierPreviewSmoke,
                        smoke.LevelSurfacePreviewSmoke,
                        smoke.RoiCropPreviewSmoke,
                        smoke.MeasurementPreviewSmoke,
                        edgeStepId));
                if (!preparationPreview.Succeeded)
                {
                    context.ViewModel.SetViewerSmokeFailed(preparationPreview.Failure!);
                    context.ShutdownApplication(1);
                    return;
                }

                if (preparationPreview.BringMeasurementOutputIntoView)
                {
                    context.Workbench.BringSelectedOutputIntoView();
                    await context.Window.Dispatcher.InvokeAsync(() => { });
                }

                var recipeMeasurementSmokeFailure =
                    await context.RecipeMeasurementSmoke.RunAsync(
                        new ShellRecipeMeasurementSmokeRequest
                        {
                            ThicknessRepeatGridMode = thicknessRepeatGridSmoke,
                            FilterPublish = filterPublishSmoke,
                            TwoPointLinePreview = twoPointLinePreviewSmoke,
                            TwoPointLinePublish = twoPointLinePublishSmoke,
                            ThreePointPlanePreview = threePointPlanePreviewSmoke,
                            ThreePointPlanePublish = threePointPlanePublishSmoke,
                            DatumPlaneDeviationPreview = datumPlaneDeviationPreviewSmoke,
                            DatumPlaneDeviationPublish = datumPlaneDeviationPublishSmoke,
                            EdgePreview = edgePreviewSmoke,
                            EdgePublish = edgePublishSmoke,
                            LineFitPreview = lineFitPreviewSmoke,
                            EdgeStepId = edgeStepId,
                            EdgeSmokeReportPath = edgeSmokeReportPath,
                            LineFitSmokeReportPath = lineFitSmokeReportPath
                        },
                        () => context.Workbench.BringThicknessRepeatGridIntoView(),
                        async () => await context.Window.Dispatcher.InvokeAsync(
                            () => { },
                            DispatcherPriority.Render));
                if (recipeMeasurementSmokeFailure is not null)
                {
                    context.ViewModel.SetViewerSmokeFailed(recipeMeasurementSmokeFailure);
                    context.ShutdownApplication(1);
                    return;
                }

                if (viewerPresentationSmoke || viewerLayoutSmoke is not null)
                {
                    var viewerWorkspace = await context.ViewerWorkspaceSmoke.RunAsync(
                        viewerPresentationSmoke,
                        viewerLayoutSmoke,
                        screenshotQualityReportPath);
                    if (!viewerWorkspace.Succeeded)
                    {
                        context.ViewModel.SetViewerSmokeFailed(
                            viewerWorkspace.Failure!);
                        context.ShutdownApplication(1);
                        return;
                    }
                    viewerPresentationCameraLinkSmokeSummary =
                        viewerWorkspace.CameraLinkSummary;
                }

                if (smoke.HeightImageDisplayRangeSmoke
                    && !await RunHeightImageDisplayRangeSmokeAsync(
                        heightImagePaletteSmoke,
                        heightImageRangeMinimumSmoke,
                        heightImageRangeMaximumSmoke,
                        heightImageDisplayRangeSmokeReportPath))
                {
                    context.ViewModel.SetViewerSmokeFailed(
                        "Height Image display range did not apply as view-only state.");
                    context.ShutdownApplication(1);
                    return;
                }

                if (heightImagePaletteStateEvidenceDirectory is not null
                    && !await ShellHeightImagePaletteStateSmoke.RunAsync(
                        context.Window,
                        context.Workbench,
                        context.ViewModel.Workbench,
                        heightImagePaletteStateEvidenceDirectory))
                {
                    context.ViewModel.SetViewerSmokeFailed(
                        "Height Image palette selector runtime states were incomplete or changed recipe/execution state.");
                    context.ShutdownApplication(1);
                    return;
                }

                if (smoke.SharedHeightHoverSmoke
                    && !await RunSharedHeightHoverSmokeAsync(
                        sharedHeightHoverRow,
                        sharedHeightHoverColumn,
                        sharedHeightHoverSmokeReportPath))
                {
                    context.ViewModel.SetViewerSmokeFailed(
                        "Height Image and 3D Viewer did not share one view-only native-grid hover.");
                    context.ShutdownApplication(1);
                    return;
                }

                if (heightImageRoiPointerSmoke is not null
                    && !await RunHeightImageRoiPointerSmokeAsync(
                        heightImageRoiPointerSmoke,
                        heightImageRoiPointerSmokeReportPath,
                        heightImageRoiPointerSmokeSavePath))
                {
                    context.ViewModel.SetViewerSmokeFailed(
                        "Height Image ROI actual-pointer editing did not preserve the teaching lifecycle boundary.");
                    context.ShutdownApplication(1);
                    return;
                }

                var teachingSmoke = await context.TeachingSmoke.RunAsync(
                    new ShellTeachingSmokeRequest(
                        teachingSelectionSmokeMode,
                        teachingSelectionSmokeReportPath,
                        planeFlatnessLiveA3PointerSmoke,
                        planeFlatnessLiveA3PointerReportPath,
                        planeFlatnessLiveA3PointerSavePath,
                        teachingRecipeSmokeSavePath));
                if (!teachingSmoke.Succeeded)
                {
                    if (teachingSmoke.Failure is not null)
                    {
                        context.ViewModel.SetViewerSmokeFailed(teachingSmoke.Failure);
                    }
                    context.ShutdownApplication(1);
                    return;
                }

                var nominalActualReady = !waitForNominalActualPreview
                    || await context.NominalActualPreviewWaiter.WaitAsync(
                        TimeSpan.FromMinutes(10),
                        operation.Token);
                if (!operation.IsActive || context.IsClosed())
                {
                    return;
                }
                if (!nominalActualReady)
                {
                    context.ViewModel.SetViewerSmokeFailed(
                        "Nominal/actual Preview did not complete before Shell screenshot capture.");
                }

                var viewerPointerSmokeFailure = await context.ViewerPointerSmoke.RunAsync(
                    profilePointerSmokeReportPath,
                    orientedBoxPointerSmokeReportPath);
                if (viewerPointerSmokeFailure is not null)
                {
                    context.ViewModel.SetViewerSmokeFailed(viewerPointerSmokeFailure.Message);
                    if (viewerPointerSmokeFailure.Abort)
                    {
                        context.ShutdownApplication(1);
                        return;
                    }
                }

                if (!await operation.DelayAsync(TimeSpan.FromMilliseconds(900)))
                {
                    return;
                }
                if (!context.ShellSmokePublish.TryPublishAndSave(
                        smokePublishResult,
                        smokeSaveRecipePath,
                        out var smokePublishFailure))
                {
                    if (smokePublishFailure is not null)
                    {
                        context.ViewModel.SetViewerSmokeFailed(smokePublishFailure);
                    }
                    context.ShutdownApplication(1);
                    return;
                }

                if (context.Viewer.SmokeExitCode != 0)
                {
                    context.ViewModel.SetViewerSmokeFailed(context.Viewer.HostState.ViewerStatus);
                }

                if (!await context.Viewer.CaptureConfiguredSmokeViewAsync())
                {
                    context.ViewModel.SetViewerSmokeFailed(context.Viewer.HostState.ViewerStatus);
                }

                var toolSelectionFailure = await context.ToolSelectionSmoke.RunAsync(
                    smokeSelectToolId,
                    expandSelectedToolParametersSmoke,
                    focusSelectedToolParameterSearchSmoke);
                if (toolSelectionFailure is not null)
                {
                    context.ViewModel.SetViewerSmokeFailed(toolSelectionFailure);
                    context.ShutdownApplication(1);
                    return;
                }

                if (!string.IsNullOrWhiteSpace(preparationPresetAssistantSmoke)
                    && !context.PreparationPresetSmoke.Configure(
                        preparationPresetAssistantSmoke,
                        out var preparationPresetAssistantFailure))
                {
                    context.ViewModel.SetViewerSmokeFailed(preparationPresetAssistantFailure);
                    context.ShutdownApplication(1);
                    return;
                }

                var workbenchInteractionSmokeFailure =
                    await context.WorkbenchInteractionSmoke.RunAsync(
                        new ShellWorkbenchInteractionSmokeRequest
                        {
                            SurfaceMatchExperimentPreview = surfaceMatchExperimentPreviewSmoke,
                            CollectionNavigationFocusHover = surfaceMatchCollectionNavigationFocusHoverSmoke,
                            CollectionDisabled = surfaceMatchCollectionDisabledSmoke,
                            ExperimentFocusHover = surfaceMatchExperimentFocusHoverSmoke,
                            CollectionPopup = surfaceMatchCollectionPopupSmoke,
                            CollectionPopupScreenshotPath = surfaceMatchCollectionPopupScreenshotPath,
                            WorkbenchInteractionReportPath = workbenchInteractionReportPath,
                            SelectedToolId = smokeSelectToolId
                        },
                        context.Window.UpdateLayout,
                        () => context.Window.Dispatcher.InvokeAsync(
                            () => { },
                            DispatcherPriority.Render).Task,
                        () => context.SurfaceMatchInteractionSmoke.RunAsync(
                            surfaceMatchCollectionNavigationFocusHoverSmoke,
                            surfaceMatchCollectionDisabledSmoke,
                            surfaceMatchExperimentFocusHoverSmoke,
                            surfaceMatchCollectionPopupSmoke,
                            surfaceMatchCollectionPopupScreenshotPath),
                        () => context.Workbench.GetSelectedToolVisibleTextLayout());
                if (workbenchInteractionSmokeFailure is not null)
                {
                    context.ViewModel.SetViewerSmokeFailed(workbenchInteractionSmokeFailure);
                    context.ShutdownApplication(1);
                    return;
                }

                if (!string.IsNullOrWhiteSpace(integrationExchangeSmokeState))
                {
                    var integrationExchangeOutcome =
                        await ShellIntegrationExchangeSmoke.RunAsync(
                            integrationExchangeSmokeState,
                            context.ViewModel.IsIntegrationExchangeSelected,
                            context.ViewModel.IntegrationExchange,
                            context.Window,
                            context.Window.Dispatcher,
                            operation.Token);
                    integrationExchangeSmoke = integrationExchangeOutcome;
                    if (integrationExchangeOutcome.IsCanceled
                        || !operation.IsActive)
                    {
                        return;
                    }
                    if (!integrationExchangeOutcome.Succeeded)
                    {
                        context.ViewModel.SetViewerSmokeFailed(integrationExchangeOutcome.Failure!);
                        context.ShutdownApplication(1);
                        return;
                    }
                }

                if (!string.IsNullOrWhiteSpace(integrationExchangeExeRole))
                {
                    var integrationExchangeExeOutcome =
                        await ShellIntegrationExchangeExeSmoke.RunAsync(
                            integrationExchangeExeRole,
                            context.ViewModel,
                            () => context.Window.IsVisible,
                            () => context.Window.Dispatcher.InvokeAsync(
                                () => { },
                                DispatcherPriority.ApplicationIdle).Task,
                            smoke.IntegrationExchangeExeReportPath,
                            context.CommandLineArguments,
                            operation.Token);
                    if (!integrationExchangeExeOutcome.Succeeded)
                    {
                        context.ViewModel.SetViewerSmokeFailed(
                            integrationExchangeExeOutcome.Failure!);
                        context.ShutdownApplication(1);
                        return;
                    }
                }

                if (context.ValidationSetSmoke.ThresholdSelectionTask is not null)
                {
                    await context.ValidationSetSmoke.ThresholdSelectionTask;
                    await context.Window.Dispatcher.InvokeAsync(
                        () => { },
                        DispatcherPriority.Render);
                }

                if (context.ValidationSetSmoke.SectionSelectionTask is not null)
                {
                    await context.ValidationSetSmoke.SectionSelectionTask;
                    await context.Window.Dispatcher.InvokeAsync(
                        () => { },
                        DispatcherPriority.Render);
                }

                if (context.ValidationSetSmoke.ComparisonTask is not null)
                {
                    await context.ValidationSetSmoke.ComparisonTask;
                    await context.Window.Dispatcher.InvokeAsync(
                        () => { },
                        DispatcherPriority.Render);
                }

                if (validationThresholdAssistantPressedSmoke
                    || validationThresholdAssistantDisabledSmoke)
                {
                    await context.ValidationThresholdSmoke.ReassertForCaptureAsync();
                }

                if (!await operation.DelayAsync(TimeSpan.FromMilliseconds(100)))
                {
                    return;
                }
                if (shellScreenshotPath is not null
                    && preparationPresetAssistantSmoke?.Equals(
                        "dropdown",
                        StringComparison.OrdinalIgnoreCase) == true
                    && !await context.PreparationPresetSmoke.CapturePopupAsync(
                        context.Window,
                        shellScreenshotPath + ".popup.png",
                        screenshotQualityReportPath))
                {
                    context.ViewModel.SetViewerSmokeFailed(
                        "Preparation preset assistant dropdown popup remained unavailable or invalid.");
                    context.ShutdownApplication(1);
                    return;
                }
                var screenshotCapture = await shellSmokeScreenshotCapture.CaptureAsync(
                    new ShellSmokeScreenshotCaptureRequest
                    {
                        ScreenshotPath = shellScreenshotPath,
                        QualityReportPath = screenshotQualityReportPath,
                        Target = new ShellSmokeScreenshotTargetRequest
                        {
                            Import3DDataPressed = import3DDataPressedSmoke,
                            ValidationThresholdAssistantPressed = validationThresholdAssistantPressedSmoke,
                            ViewerToolbarPressed = viewerToolbarPressedSmoke,
                            ViewerPresentationPressed = viewerPresentationPressedSmoke,
                            RecipeHealthNavigationPressed = recipeHealthNavigationPressedSmoke,
                            SupportBundlePressed = supportBundlePressedSmoke,
                            CurrentRecipeRunPressed = currentRecipeRunPressedSmoke,
                            IntegrationExchangePressed = integrationExchangeSmoke?.HasPressedCapture == true,
                            IntegrationExchangeAutomationId = integrationExchangeSmoke?.PressedCaptureAutomationId,
                            IntegrationExchangeScope = integrationExchangeSmoke?.PressedCaptureScope,
                            PreparationPresetAssistantMode = preparationPresetAssistantSmoke
                        },
                        ViewerPresentationCameraLinkSummary = viewerPresentationCameraLinkSmokeSummary,
                        AppendValidationThresholdEvidence = context.ValidationSetSmoke.ThresholdSelectionTask is not null,
                        IntegrationExchangeEvidenceLine = integrationExchangeSmoke?.EvidenceLine,
                        PreparationPresetAssistantMode = preparationPresetAssistantSmoke
                    });
                if (!screenshotCapture.Succeeded)
                {
                    context.ViewModel.SetViewerSmokeFailed(screenshotCapture.Failure!);
                    context.ShutdownApplication(1);
                    return;
                }
                if (currentRecipeRunPressedSmoke)
                {
                    var currentRecipeRun =
                        await context.CurrentRecipeRunSmoke.ExecuteAfterCaptureAsync(
                            pressedSmoke: true,
                            screenshotQualityReportPath: screenshotQualityReportPath,
                            cancellationToken: operation.Token);
                    if (currentRecipeRun.IsCanceled
                        || !operation.IsActive)
                    {
                        return;
                    }
                    if (!currentRecipeRun.Succeeded)
                    {
                        context.ViewModel.SetViewerSmokeFailed(
                            currentRecipeRun.Failure!);
                        context.ShutdownApplication(1);
                        return;
                    }
                }

                var auxiliaryScreenshotFailure = await auxiliaryWindowScreenshot.CaptureAsync(
                    new ShellAuxiliaryWindowScreenshotRequest
                    {
                        ViewerPopoutScreenshotPath = viewerPopoutScreenshotPath,
                        ViewerPopoutQualityReportPath = viewerPopoutScreenshotQualityReportPath,
                        RecipeManagerScreenshotPath = recipeManagerScreenshotPath,
                        RecipeManagerQualityReportPath = recipeManagerScreenshotQualityReportPath,
                        FirstRecipeCreatePressed = smoke.FirstRecipeCreatePressedSmoke,
                        MessageDialogScreenshotPath = messageDialogScreenshotPath,
                        MessageDialogQualityReportPath = messageDialogScreenshotQualityReportPath,
                        MessageDialogPrimaryPressed = smoke.MessageDialogPrimaryPressedSmoke
                    });
                if (auxiliaryScreenshotFailure is not null)
                {
                    context.ViewModel.SetViewerSmokeFailed(auxiliaryScreenshotFailure);
                    context.ShutdownApplication(1);
                    return;
                }

                var toolLabCapture = await toolLabSmoke.CaptureAsync(smoke);
                if (!toolLabCapture.Passed)
                {
                    context.ViewModel.SetViewerSmokeFailed(toolLabCapture.Failure);
                    context.ShutdownApplication(1);
                    return;
                }
                if (!await operation.DelayAsync(TimeSpan.FromMilliseconds(100)))
                {
                    return;
                }
                toolLabSmoke.CloseTemporaryWindows(smoke);
                if (messageDialogSmokeWindow is { IsVisible: true })
                {
                    messageDialogSmokeWindow.Close();
                }
                if (recipeManagerScreenshotPath is not null
                    && !ShellRecipeLifecycleSmoke.RunWindowLifetime(
                        context.WorkbenchLifecycle.ShowRecipeManagerWindow,
                        context.WorkbenchLifecycle.CloseRecipeManager,
                        () => context.WorkbenchLifecycle.RecipeManagerWindow,
                        () => context.WorkbenchLifecycle.IsRecipeManagerVisible,
                        context.WorkbenchLifecycle.Dispose,
                        recipeManagerScreenshotQualityReportPath))
                {
                    context.ViewModel.SetViewerSmokeFailed("Recipe Manager window lifetime did not preserve hide/reopen semantics or clear the forced-close reference.");
                    context.ShutdownApplication(1);
                    return;
                }
                context.ShutdownApplication(
                    nominalActualReady ? context.Viewer.SmokeExitCode : 1);
            };

            context.Window.Loaded += loadedHandler;
        }
    }

    public void Dispose()
    {
        context.Window.Loaded -= loadedHandler;
        operation.Dispose();
        GC.SuppressFinalize(this);
    }

    private Task<bool> RunHeightImageDisplayRangeSmokeAsync(
        string? paletteText,
        double? minimum,
        double? maximum,
        string? reportPath) =>
        ShellHeightImageDisplayRangeSmoke.RunAsync(
            paletteText,
            minimum,
            maximum,
            reportPath,
            context.ViewModel.Workbench,
            context.Viewer,
            context.Window.Dispatcher);

    private Task<bool> RunSharedHeightHoverSmokeAsync(
        int? row,
        int? column,
        string? reportPath) =>
        ShellSharedHeightHoverSmoke.RunAsync(
            context.ViewModel.Workbench,
            context.Viewer,
            context.Window.Dispatcher,
            row,
            column,
            reportPath);

    private Task<bool> RunHeightImageRoiPointerSmokeAsync(
        string mode,
        string? reportPath,
        string? savePath) =>
        ShellHeightImageRoiPointerSmoke.RunAsync(
            mode,
            reportPath,
            savePath,
            context.ViewModel.Workbench,
            context.Workbench,
            context.Viewer,
            context.Window.Dispatcher);

    private bool TryConfigureSurfaceMatchEvidenceFromCommandLine(out string failure) =>
        ShellSurfaceMatchSmoke.TryConfigureEvidenceFromCommandLine(
            context.CommandLineArguments,
            context.ViewModel.Workbench,
            out failure);

    private static string DialogText(string key, string korean, string english) =>
        ThreeDLocalization.Shared.Resolve(key, korean, english);

    private static async Task<bool> CaptureMessageDialogForSmokeAsync(
        WpfMessageDialogWindow dialog,
        string screenshotPath,
        string? qualityReportPath,
        bool holdPrimaryButton)
    {
        if (holdPrimaryButton)
        {
            return await CaptureButtonPressedForSmokeAsync(
                dialog,
                "MessageDialogPrimaryButton",
                screenshotPath,
                qualityReportPath,
                "MessageDialogPrimaryPressed");
        }

        return await CaptureWindowWithRetryAsync(
            dialog,
            screenshotPath,
            qualityReportPath,
            "MessageDialog");
    }
}
