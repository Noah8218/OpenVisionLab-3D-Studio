using OpenVisionLab.ThreeD.Shell.Verification.Smoke;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using static OpenVisionLab.ThreeD.Shell.Verification.Smoke.ShellSmokeArtifacts;

namespace OpenVisionLab.ThreeD.Shell.Automation;

/// <summary>
/// Owns the Loaded Shell Smoke sequence for source readiness and recipe lifecycle.
/// Existing Smoke classes retain the individual checks; this type only composes
/// their order and returns one failure result to the scenario runner.
/// </summary>
internal sealed class ShellSmokeRecipeSourceWorkflowCoordinator
{
    private readonly ShellSmokeScenarioContext context;
    private readonly ShellSmokeOperation operation;

    public ShellSmokeRecipeSourceWorkflowCoordinator(
        ShellSmokeScenarioContext context,
        ShellSmokeOperation operation)
    {
        this.context = context ?? throw new ArgumentNullException(nameof(context));
        this.operation = operation ?? throw new ArgumentNullException(nameof(operation));
    }

    public async Task<ShellSmokeRecipeSourceWorkflowResult> RunAsync(
        ShellSmokeCommandLineOptions smoke)
    {
        ArgumentNullException.ThrowIfNull(smoke);

        if (smoke.AsyncC3DLoadSmokePath is not null
            && !await ShellAsyncC3DLoadSmoke.RunAsync(
                context.Viewer,
                context.ViewModel.Workbench,
                context.Window.Dispatcher,
                smoke.AsyncC3DLoadSmokePath,
                smoke.AsyncC3DLoadSmokeReportPath,
                smoke.AsyncC3DLoadCancelAt,
                smoke.AsyncC3DLoadExpectFailure,
                smoke.AsyncC3DLoadExpectedStatusFragment,
                path => context.WorkbenchLifecycle.LoadWorkbenchC3DSourceAsync(
                    path,
                    showFailureDialog: false),
                context.WorkbenchLifecycle.IsViewerSourceAlreadyLoaded,
                () => context.WorkbenchLifecycle.LastWorkbenchSourceBindingMilliseconds))
        {
            return ShellSmokeRecipeSourceWorkflowResult.Failed(
                "Asynchronous C3D load smoke did not satisfy its source-retention, status, or responsiveness contract.");
        }

        if (smoke.ViewerOnlyImportSmokePath is not null
            && !await ShellViewerOnlyImportSmoke.RunAsync(
                context.Viewer,
                context.ViewModel.Workbench,
                context.WorkbenchLifecycle,
                smoke.ViewerOnlyImportSmokePath,
                smoke.ViewerOnlyImportSmokeReportPath))
        {
            return ShellSmokeRecipeSourceWorkflowResult.Failed(
                "Viewer-only Import did not activate the decoded source or preserve recipe state.");
        }

        if (smoke.SourceQualitySmoke
            && !await ShellSourceQualitySmoke.RunAsync(
                context.ViewModel.Workbench,
                context.Workbench,
                context.Window,
                context.Window.Dispatcher,
                smoke.SourceQualitySmokeReportPath))
        {
            return ShellSmokeRecipeSourceWorkflowResult.Failed(
                "Source Quality did not become ready or changed authored/execution state.");
        }

        if (!string.IsNullOrWhiteSpace(smoke.SourceAcquisitionProvenanceSmokeState))
        {
            var sourceAcquisitionFailure =
                await ShellSourceAcquisitionProvenanceSmoke.ConfigureAcquisitionProvenanceStateAsync(
                    context.ViewModel.Workbench,
                    context.Workbench,
                    context.Window.Dispatcher,
                    smoke.SourceAcquisitionProvenanceSmokeState,
                    smoke.SourceAcquisitionProvenancePopupScreenshotPath);
            if (sourceAcquisitionFailure is not null)
            {
                if (!string.IsNullOrWhiteSpace(
                        smoke.SourceAcquisitionProvenancePopupScreenshotPath))
                {
                    WriteTextReport(
                        smoke.SourceAcquisitionProvenancePopupScreenshotPath + ".failure.txt",
                        [sourceAcquisitionFailure]);
                }

                return ShellSmokeRecipeSourceWorkflowResult.Failed(sourceAcquisitionFailure);
            }
        }

        if (smoke.NewRecipeLifecycleSmokePath is not null
            && smoke.NewRecipeLifecycleSmokeSourcePath is not null
            && !await ShellRecipeLifecycleSmoke.RunNewAsync(
                context.ViewModel,
                context.Viewer,
                smoke.NewRecipeLifecycleSmokePath,
                smoke.NewRecipeLifecycleSmokeSourcePath,
                smoke.NewRecipeLifecycleSmokeStarterId
                    ?? ToolWorkbenchViewModel.EmptyFirstRecipeStarterId,
                smoke.NewRecipeLifecycleSmokeReportPath,
                context.WorkbenchLifecycle.ShowRecipeManagerWindow,
                context.WorkbenchLifecycle.ClickUnsavedRecipeDoNotSaveForSmokeAsync))
        {
            return ShellSmokeRecipeSourceWorkflowResult.Failed(
                "New recipe lifecycle smoke did not create and open a clean zero-step recipe.");
        }

        if (smoke.NewRecipeLifecycleSmokePath is not null
            && smoke.NewRecipeLifecycleSmokeSourcePath is null)
        {
            return ShellSmokeRecipeSourceWorkflowResult.Failed(
                "New recipe lifecycle smoke requires --smoke-new-recipe-source.");
        }

        if (smoke.OpenRecipeLifecycleSmokePath is not null
            && !ShellRecipeLifecycleSmoke.RunOpen(
                context.ViewModel,
                smoke.OpenRecipeLifecycleSmokePath,
                smoke.OpenRecipeLifecycleSmokeReportPath,
                context.WorkbenchLifecycle.ShowRecipeManagerWindow,
                context.WorkbenchLifecycle.OpenWorkbenchRecipe,
                () => context.WorkbenchLifecycle.IsRecipeManagerVisible,
                context.WorkbenchLifecycle.IsViewerSourceAlreadyLoaded))
        {
            return ShellSmokeRecipeSourceWorkflowResult.Failed(
                "Open recipe lifecycle smoke did not activate the saved recipe in Workbench.");
        }

        if (smoke.RecipeManagerScreenshotPath is not null)
        {
            context.WorkbenchLifecycle.ShowRecipeManagerWindow();
            var firstRecipeManagerWindow = context.WorkbenchLifecycle.RecipeManagerWindow;
            context.WorkbenchLifecycle.ShowRecipeManagerWindow();
            if (!ReferenceEquals(
                    firstRecipeManagerWindow,
                    context.WorkbenchLifecycle.RecipeManagerWindow))
            {
                return ShellSmokeRecipeSourceWorkflowResult.Failed(
                    "Recipe Manager smoke opened more than one window instance.");
            }

            context.WorkbenchLifecycle.ConfigureFirstRecipeSetupForSmoke(smoke);
        }

        return operation.IsActive
            ? ShellSmokeRecipeSourceWorkflowResult.Success()
            : ShellSmokeRecipeSourceWorkflowResult.Canceled();
    }
}

internal sealed record ShellSmokeRecipeSourceWorkflowResult(
    bool Succeeded,
    bool IsCanceled,
    string? Failure)
{
    public static ShellSmokeRecipeSourceWorkflowResult Success() =>
        new(true, false, null);

    public static ShellSmokeRecipeSourceWorkflowResult Canceled() =>
        new(false, true, null);

    public static ShellSmokeRecipeSourceWorkflowResult Failed(string message) =>
        new(false, false, message);
}
