using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Shell.Verification.Smoke;

internal sealed record ShellPreparationPreviewSmokeRequest(
    bool FilterPreview,
    bool PreparationQualityComparison,
    bool RemoveOutlierPreview,
    bool LevelSurfacePreview,
    bool RoiCropPreview,
    bool MeasurementPreview,
    string? MeasurementStepId);

internal sealed record ShellPreparationPreviewSmokeResult(
    bool Succeeded,
    string? Failure,
    bool BringMeasurementOutputIntoView)
{
    public static ShellPreparationPreviewSmokeResult Success(
        bool bringMeasurementOutputIntoView = false) =>
        new(true, null, bringMeasurementOutputIntoView);

    public static ShellPreparationPreviewSmokeResult Failed(string message) =>
        new(false, message, false);
}

/// <summary>
/// Owns the WPF-neutral preparation Preview Smoke sequence. MainWindow keeps
/// only visual output framing and application failure/shutdown policy.
/// </summary>
internal sealed class ShellPreparationPreviewSmokeCoordinator
{
    private readonly ToolWorkbenchViewModel workbench;

    public ShellPreparationPreviewSmokeCoordinator(ToolWorkbenchViewModel workbench)
    {
        this.workbench = workbench ?? throw new ArgumentNullException(nameof(workbench));
    }

    public async Task<ShellPreparationPreviewSmokeResult> RunAsync(
        ShellPreparationPreviewSmokeRequest request)
    {
        if (request.FilterPreview
            && !await workbench.PreviewSelectedFilterAsync())
        {
            return ShellPreparationPreviewSmokeResult.Failed(
                workbench.FilterExecutionSummary);
        }

        if (request.PreparationQualityComparison)
        {
            var preparationOutput = workbench.SelectedToolWorkspace.Outputs
                .SingleOrDefault();
            if (preparationOutput is null
                || !workbench.TryOpenPreparationQualityComparison(
                    workbench.DisplayedOutputs.SingleOrDefault(item =>
                        string.Equals(
                            item.Id,
                            preparationOutput.EntityId,
                            StringComparison.OrdinalIgnoreCase))))
            {
                return ShellPreparationPreviewSmokeResult.Failed(
                    "Preparation quality comparison smoke could not normalize the current source and Filter Preview.");
            }
        }

        if (request.RemoveOutlierPreview
            && !await workbench.PreviewSelectedRemoveOutlierPixelsAsync())
        {
            return ShellPreparationPreviewSmokeResult.Failed(
                workbench.RemoveOutlierExecutionSummary);
        }

        if (request.LevelSurfacePreview
            && !await workbench.PreviewSelectedLevelSurfaceAsync())
        {
            return ShellPreparationPreviewSmokeResult.Failed(
                workbench.LevelSurfaceExecutionSummary);
        }

        if (request.RoiCropPreview
            && !await workbench.PreviewSelectedRoiCropAsync())
        {
            return ShellPreparationPreviewSmokeResult.Failed(
                workbench.RoiCropExecutionSummary);
        }

        if (request.MeasurementPreview
            && ((!string.IsNullOrWhiteSpace(request.MeasurementStepId)
                 && !workbench.SelectPipelineStep(request.MeasurementStepId))
                || !await workbench.PreviewSelectedMeasurementAsync()))
        {
            return ShellPreparationPreviewSmokeResult.Failed(
                workbench.MeasurementExecutionSummary);
        }

        return ShellPreparationPreviewSmokeResult.Success(request.MeasurementPreview);
    }
}
