using System.IO;
using System.Windows.Input;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Tools;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Shared Preview/Publish lifecycle for composable measurement tools.
/// </summary>
public sealed partial class ToolWorkbenchViewModel
{
    private CancellationTokenSource? measurementPreviewCancellation;
    private ToolRecipeHeightMeasurementOutput? measurementPreviewOutput;
    private bool isMeasurementPreviewRunning;
    private bool isMeasurementPreviewStale;
    private bool isMeasurementPreviewPublished;
    private string measurementExecutionSummary = "Route a verified HeightField and recipe-owned GridRectangle, then Preview explicitly.";
    private RelayCommand capturePlaneFlatnessReferenceRoiCommand = null!;
    private RelayCommand capturePlaneFlatnessMeasurementRoiCommand = null!;
    private RelayCommand reusePlaneFlatnessReferenceRoiCommand = null!;
    private RelayCommand reusePlaneFlatnessMeasurementRoiCommand = null!;
    private bool isPlaneFlatnessMeasurementRole;
    private string? planeFlatnessTeachingStepId;

    public bool IsSelectedStepThickness => string.Equals(SelectedPipelineStep?.ToolId, "thickness", StringComparison.Ordinal);
    public bool IsSelectedStepWarpage => string.Equals(SelectedPipelineStep?.ToolId, "warpage", StringComparison.Ordinal);
    public bool IsSelectedStepPlaneFlatness => string.Equals(SelectedPipelineStep?.ToolId, "plane-flatness", StringComparison.Ordinal);
    public bool IsSelectedStepPointPairDimensions => string.Equals(SelectedPipelineStep?.ToolId, "point-pair-dimensions", StringComparison.Ordinal);
    public bool IsSelectedStepGapFlush => string.Equals(SelectedPipelineStep?.ToolId, "gap-flush", StringComparison.Ordinal);
    public bool IsSelectedStepVolume => string.Equals(SelectedPipelineStep?.ToolId, "volume", StringComparison.Ordinal);
    public bool IsSelectedStepDualRoiMeasurement => IsSelectedStepPlaneFlatness || IsSelectedStepGapFlush || IsSelectedStepVolume;
    public bool IsSelectedStepMeasurement => IsSelectedStepThickness || IsSelectedStepWarpage || IsSelectedStepDualRoiMeasurement || IsSelectedStepPointPairDimensions;
    public bool IsMeasurementPreviewRunning => isMeasurementPreviewRunning;
    public bool HasCurrentMeasurementPreview => measurementPreviewOutput is not null && !isMeasurementPreviewStale;
    public bool IsMeasurementPreviewPublished => isMeasurementPreviewPublished;
    public string MeasurementExecutionSummary => measurementExecutionSummary;
    public string MeasurementEvidenceSummary => measurementPreviewOutput?.EvidenceSummary ?? "No measurement evidence until Preview completes.";
    internal ToolRecipeHeightMeasurementOutput? CurrentMeasurementOutput => measurementPreviewOutput;
    public ICommand CapturePlaneFlatnessReferenceRoiCommand => capturePlaneFlatnessReferenceRoiCommand;
    public ICommand CapturePlaneFlatnessMeasurementRoiCommand => capturePlaneFlatnessMeasurementRoiCommand;
    public ICommand ReusePlaneFlatnessReferenceRoiCommand => reusePlaneFlatnessReferenceRoiCommand;
    public ICommand ReusePlaneFlatnessMeasurementRoiCommand => reusePlaneFlatnessMeasurementRoiCommand;
    public ToolRecipeSelection? PlaneFlatnessReferenceSelection => GetPlaneFlatnessRoleSelection(1);
    public ToolRecipeSelection? PlaneFlatnessMeasurementSelection => GetPlaneFlatnessRoleSelection(2);
    public bool IsPlaneFlatnessReferenceRoleActive => IsSelectedStepDualRoiMeasurement && !isPlaneFlatnessMeasurementRole;
    public bool IsPlaneFlatnessMeasurementRoleActive => IsSelectedStepDualRoiMeasurement && isPlaneFlatnessMeasurementRole;
    public bool CanTeachPlaneFlatnessMeasurementRoi => PlaneFlatnessReferenceSelection is not null;
    public string PlaneFlatnessReferenceState => PlaneFlatnessReferenceSelection is null ? Localization.RoiWaiting : Localization.RoiComplete;
    public string PlaneFlatnessMeasurementState => PlaneFlatnessMeasurementSelection is null ? Localization.RoiWaiting : Localization.RoiComplete;
    public string PlaneFlatnessReferenceSummary => PlaneFlatnessReferenceSelection is { } selection
        ? FormatTeachingSelection(selection)
        : Localization.NoRoiTaught;
    public string PlaneFlatnessMeasurementSummary => PlaneFlatnessMeasurementSelection is { } selection
        ? FormatTeachingSelection(selection)
        : CanTeachPlaneFlatnessMeasurementRoi ? Localization.NoRoiTaught : DualRoiFirstRequired;
    public string PlaneFlatnessReferenceActionText => PlaneFlatnessReferenceSelection is null ? Localization.CaptureRoi : Localization.ReplaceRoi;
    public string PlaneFlatnessMeasurementActionText => PlaneFlatnessMeasurementSelection is null ? Localization.CaptureRoi : Localization.ReplaceRoi;
    public string DualRoiTeachingTitle => IsSelectedStepGapFlush
        ? Localization.GapFlushRoiTeaching
        : IsSelectedStepVolume ? Localization.VolumeRoiTeaching : Localization.PlaneFlatnessRoiTeaching;
    public string DualRoiTeachingDetail => IsSelectedStepGapFlush
        ? Localization.GapFlushRoiTeachingDetail
        : IsSelectedStepVolume ? Localization.VolumeRoiTeachingDetail : Localization.PlaneFlatnessRoiTeachingDetail;
    public string DualRoiFirstLabel => IsSelectedStepGapFlush ? Localization.FirstRoi : Localization.ReferenceRoi;
    public string DualRoiSecondLabel => IsSelectedStepGapFlush ? Localization.SecondRoi : Localization.MeasurementRoi;
    public string DualRoiFirstRequired => IsSelectedStepGapFlush ? Localization.FirstRoiRequiredFirst : Localization.ReferenceRoiRequiredFirst;

    private void InitializePlaneFlatnessTeaching()
    {
        capturePlaneFlatnessReferenceRoiCommand = new RelayCommand(
            _ => BeginPlaneFlatnessRoleCapture(measurementRole: false),
            _ => CanCapturePlaneFlatnessRole(measurementRole: false));
        capturePlaneFlatnessMeasurementRoiCommand = new RelayCommand(
            _ => BeginPlaneFlatnessRoleCapture(measurementRole: true),
            _ => CanCapturePlaneFlatnessRole(measurementRole: true));
        reusePlaneFlatnessReferenceRoiCommand = new RelayCommand(
            _ => ReusePlaneFlatnessRoleSelection(measurementRole: false),
            _ => CanReusePlaneFlatnessRoleSelection(measurementRole: false));
        reusePlaneFlatnessMeasurementRoiCommand = new RelayCommand(
            _ => ReusePlaneFlatnessRoleSelection(measurementRole: true),
            _ => CanReusePlaneFlatnessRoleSelection(measurementRole: true));
        Localization.PropertyChanged += (_, _) => NotifyPlaneFlatnessTeachingState();
    }

    private ToolRecipeSelection? GetPlaneFlatnessRoleSelection(int inputIndex)
    {
        if (!IsSelectedStepDualRoiMeasurement
            || SelectedPipelineStep is not { } step
            || step.InputEntityIds.ElementAtOrDefault(inputIndex) is not { } selectionId)
        {
            return null;
        }

        return Selections.FirstOrDefault(selection =>
            string.Equals(selection.Id, selectionId, StringComparison.OrdinalIgnoreCase)
            && selection.GridRectangle is not null);
    }

    private void BeginPlaneFlatnessRoleCapture(bool measurementRole)
    {
        SetPlaneFlatnessTeachingRole(measurementRole);
        BeginTeachingSelectionCapture();
    }

    private void ReusePlaneFlatnessRoleSelection(bool measurementRole)
    {
        SetPlaneFlatnessTeachingRole(measurementRole);
        UseExistingTeachingSelection();
    }

    private bool CanCapturePlaneFlatnessRole(bool measurementRole) =>
        IsSelectedStepDualRoiMeasurement
        && !IsTeachingSelectionCaptureActive
        && (!measurementRole || CanTeachPlaneFlatnessMeasurementRoi)
        && !string.IsNullOrWhiteSpace(Source.Path)
        && SelectedPipelineStep is { } step
        && TryGetSelectionCaptureContext(step, out _, out _);

    private bool CanReusePlaneFlatnessRoleSelection(bool measurementRole)
    {
        if (!IsSelectedStepDualRoiMeasurement
            || IsTeachingSelectionCaptureActive
            || SelectedCompatibleSelection is not { } candidate
            || (measurementRole && !CanTeachPlaneFlatnessMeasurementRoi))
        {
            return false;
        }

        var otherRole = measurementRole ? PlaneFlatnessReferenceSelection : PlaneFlatnessMeasurementSelection;
        return !string.Equals(candidate.Id, otherRole?.Id, StringComparison.OrdinalIgnoreCase);
    }

    private void SetPlaneFlatnessTeachingRole(bool measurementRole)
    {
        isPlaneFlatnessMeasurementRole = measurementRole;
        NotifyPlaneFlatnessTeachingState();
    }

    private void AdvancePlaneFlatnessTeachingRole()
    {
        if (IsSelectedStepDualRoiMeasurement && !isPlaneFlatnessMeasurementRole)
        {
            SetPlaneFlatnessTeachingRole(measurementRole: true);
        }
    }

    private void RefreshPlaneFlatnessTeachingState()
    {
        var selectedStepId = IsSelectedStepDualRoiMeasurement ? SelectedPipelineStep?.Id : null;
        if (!string.Equals(planeFlatnessTeachingStepId, selectedStepId, StringComparison.OrdinalIgnoreCase))
        {
            planeFlatnessTeachingStepId = selectedStepId;
            isPlaneFlatnessMeasurementRole = selectedStepId is not null
                && PlaneFlatnessReferenceSelection is not null
                && PlaneFlatnessMeasurementSelection is null;
        }

        NotifyPlaneFlatnessTeachingState();
    }

    private ToolWorkbenchTeachingSelectionRequirement CreatePlaneFlatnessSelectionRequirement() =>
        isPlaneFlatnessMeasurementRole
            ? new(DualRoiSecondLabel, ToolRecipeSelectionKinds.GridRectangle, 2, true, DualRoiTeachingDetail)
            : new(DualRoiFirstLabel, ToolRecipeSelectionKinds.GridRectangle, 2, true, DualRoiTeachingDetail);

    private string CreatePlaneFlatnessSelectionName(ToolWorkbenchPipelineStepItem step) =>
        $"{step.ToolName} {(isPlaneFlatnessMeasurementRole ? DualRoiSecondLabel : DualRoiFirstLabel)}";

    private bool CanUseActivePlaneFlatnessRole() =>
        !IsSelectedStepDualRoiMeasurement || !isPlaneFlatnessMeasurementRole || CanTeachPlaneFlatnessMeasurementRoi;

    private void RoutePlaneFlatnessRoleSelection(ToolWorkbenchPipelineStepItem step, string selectionId)
    {
        var primaryInput = step.InputEntityIds.FirstOrDefault(input =>
            !Selections.Any(selection => string.Equals(selection.Id, input, StringComparison.OrdinalIgnoreCase)))
            ?? step.InputEntityIds.FirstOrDefault();
        var referenceId = PlaneFlatnessReferenceSelection?.Id;
        var measurementId = PlaneFlatnessMeasurementSelection?.Id;
        if (isPlaneFlatnessMeasurementRole) measurementId = selectionId;
        else referenceId = selectionId;

        step.InputEntityIdsText = string.Join("; ", new[] { primaryInput, referenceId, measurementId }
            .Where(input => !string.IsNullOrWhiteSpace(input)));
    }

    private void NotifyPlaneFlatnessTeachingState()
    {
        OnPropertyChanged(nameof(PlaneFlatnessReferenceSelection));
        OnPropertyChanged(nameof(PlaneFlatnessMeasurementSelection));
        OnPropertyChanged(nameof(IsPlaneFlatnessReferenceRoleActive));
        OnPropertyChanged(nameof(IsPlaneFlatnessMeasurementRoleActive));
        OnPropertyChanged(nameof(CanTeachPlaneFlatnessMeasurementRoi));
        OnPropertyChanged(nameof(PlaneFlatnessReferenceState));
        OnPropertyChanged(nameof(PlaneFlatnessMeasurementState));
        OnPropertyChanged(nameof(PlaneFlatnessReferenceSummary));
        OnPropertyChanged(nameof(PlaneFlatnessMeasurementSummary));
        OnPropertyChanged(nameof(PlaneFlatnessReferenceActionText));
        OnPropertyChanged(nameof(PlaneFlatnessMeasurementActionText));
        OnPropertyChanged(nameof(DualRoiTeachingTitle));
        OnPropertyChanged(nameof(DualRoiTeachingDetail));
        OnPropertyChanged(nameof(DualRoiFirstLabel));
        OnPropertyChanged(nameof(DualRoiSecondLabel));
        OnPropertyChanged(nameof(DualRoiFirstRequired));
        capturePlaneFlatnessReferenceRoiCommand?.RaiseCanExecuteChanged();
        capturePlaneFlatnessMeasurementRoiCommand?.RaiseCanExecuteChanged();
        reusePlaneFlatnessReferenceRoiCommand?.RaiseCanExecuteChanged();
        reusePlaneFlatnessMeasurementRoiCommand?.RaiseCanExecuteChanged();
    }

    public async Task<bool> PreviewSelectedMeasurementAsync()
    {
        if (!CanPreviewSelectedMeasurement() || SelectedPipelineStep is not { } step)
        {
            if (SelectedPipelineStep is { } waiting) waiting.State = "Taught incomplete";
            SetMeasurementSummary("A current raw or Published transformed HeightField and its owned GridRectangle are required.");
            return false;
        }

        measurementPreviewCancellation?.Dispose();
        measurementPreviewCancellation = new CancellationTokenSource();
        isMeasurementPreviewRunning = true;
        isMeasurementPreviewStale = false;
        isMeasurementPreviewPublished = false;
        step.State = "Preview running";
        SetMeasurementSummary($"{step.ToolName} Preview is evaluating only the selected tool step.");
        AppendLog("Preview", $"{step.ToolName} Preview started: {step.Id}.");
        RefreshMeasurementCommands();
        try
        {
            var recipeDirectory = RecipePath is null ? Environment.CurrentDirectory : Path.GetDirectoryName(Path.GetFullPath(RecipePath));
            TryGetCurrentMeasurementHeightField(out var transformedHeightField);
            var evaluation = await Task.Run(
                () => ToolRecipeHeightMeasurementExecution.Execute(
                    CreateDocument(), step.Id, transformedHeightField, recipeDirectory, measurementPreviewCancellation.Token),
                measurementPreviewCancellation.Token);
            if (evaluation.Output is null || evaluation.Result.Status == ResultStatus.Error)
            {
                measurementPreviewOutput = null;
                step.State = "Error";
                SetMeasurementSummary(evaluation.Result.Message);
                AppendLog("Error", $"{step.ToolName} Preview failed: {evaluation.Result.Message}");
                return false;
            }

            measurementPreviewOutput = evaluation.Output;
            step.State = "Preview ready";
            SetMeasurementSummary($"Preview ready | {evaluation.Output.EvidenceSummary} | {evaluation.Result.Status} | declared source units only.");
            AppendLog("Preview", $"{step.ToolName} Preview ready: {evaluation.Output.ContentSha256}.");
            return true;
        }
        catch (OperationCanceledException)
        {
            step.State = "Ready";
            SetMeasurementSummary("Preview canceled. The source, ROI, and authored recipe were not changed.");
            return false;
        }
        finally
        {
            isMeasurementPreviewRunning = false;
            OnPropertyChanged(nameof(IsMeasurementPreviewRunning));
            OnPropertyChanged(nameof(IsSelectedStepPreviewRunning));
            RefreshMeasurementCommands();
        }
    }

    private bool CanPreviewSelectedMeasurement()
    {
        if (!IsSelectedStepMeasurement || HasPendingStepParameterChanges || isMeasurementPreviewRunning
            || SelectedPipelineStep is not { } step) return false;
        var recipeDirectory = RecipePath is null ? Environment.CurrentDirectory : Path.GetDirectoryName(Path.GetFullPath(RecipePath));
        TryGetCurrentMeasurementHeightField(out var transformedHeightField);
        return ToolRecipeHeightMeasurementExecution.TryPrepare(
            CreateDocument(), step.Id, transformedHeightField, recipeDirectory, out _, out _);
    }

    private void PublishSelectedMeasurement()
    {
        if (SelectedPipelineStep is not { } step || !HasCurrentMeasurementPreview) return;
        isMeasurementPreviewPublished = true;
        step.State = "Published";
        SetMeasurementSummary($"Published exact Preview as {step.OutputEntityId} | SHA-256 {measurementPreviewOutput!.ContentSha256} | no recalculation.");
        AppendLog("Publish", $"{step.ToolName} output published without re-running: {step.OutputEntityId}.");
    }

    private void CancelMeasurementPreview() => measurementPreviewCancellation?.Cancel();

    private void ClearMeasurementPreview(string summary)
    {
        measurementPreviewCancellation?.Cancel();
        measurementPreviewOutput = null;
        isMeasurementPreviewStale = false;
        isMeasurementPreviewPublished = false;
        SetMeasurementSummary(summary);
    }

    private void MarkMeasurementPreviewStaleIfNeeded(object? sender = null)
    {
        if (measurementPreviewOutput is null || isMeasurementPreviewRunning) return;
        var step = PipelineSteps.FirstOrDefault(candidate =>
            string.Equals(candidate.OutputEntityId, measurementPreviewOutput.OutputEntityId, StringComparison.OrdinalIgnoreCase));
        if (step is null) return;
        if (sender is not null
            && !ReferenceEquals(sender, step)
            && (sender is not ToolWorkbenchParameterItem parameter || !step.Parameters.Contains(parameter)))
        {
            return;
        }
        isMeasurementPreviewStale = true;
        isMeasurementPreviewPublished = false;
        step.State = "Preview stale";
        SetMeasurementSummary("Source, route, ROI, output, or parameter changed. Preview again before Publish.");
    }

    private void RefreshMeasurementExecutionState()
    {
        OnPropertyChanged(nameof(IsSelectedStepThickness));
        OnPropertyChanged(nameof(IsSelectedStepWarpage));
        OnPropertyChanged(nameof(IsSelectedStepPlaneFlatness));
        OnPropertyChanged(nameof(IsSelectedStepPointPairDimensions));
        OnPropertyChanged(nameof(IsSelectedStepGapFlush));
        OnPropertyChanged(nameof(IsSelectedStepVolume));
        OnPropertyChanged(nameof(IsSelectedStepDualRoiMeasurement));
        OnPropertyChanged(nameof(IsSelectedStepMeasurement));
        RefreshPlaneFlatnessTeachingState();
        OnPropertyChanged(nameof(IsSelectedStepPreviewRunning));
        if (SelectedPipelineStep is { } step && IsSelectedStepMeasurement
            && (measurementPreviewOutput is null || isMeasurementPreviewStale)
            && !isMeasurementPreviewRunning)
        {
            var recipeDirectory = RecipePath is null ? Environment.CurrentDirectory : Path.GetDirectoryName(Path.GetFullPath(RecipePath));
            TryGetCurrentMeasurementHeightField(out var transformedHeightField);
            if (ToolRecipeHeightMeasurementExecution.TryPrepare(
                CreateDocument(), step.Id, transformedHeightField, recipeDirectory, out _, out var message))
            {
                step.State = "Ready";
                measurementExecutionSummary = $"{step.ToolName} is ready for explicit Preview. It remains one composable recipe step.";
            }
            else
            {
                step.State = "Taught incomplete";
                measurementExecutionSummary = message;
            }
        }
        OnPropertyChanged(nameof(MeasurementExecutionSummary));
        OnPropertyChanged(nameof(MeasurementEvidenceSummary));
        OnPropertyChanged(nameof(HasCurrentMeasurementPreview));
        OnPropertyChanged(nameof(IsMeasurementPreviewPublished));
        RefreshMeasurementCommands();
    }

    private void SetMeasurementSummary(string value)
    {
        measurementExecutionSummary = value;
        RebuildEntities();
        OnPropertyChanged(nameof(MeasurementExecutionSummary));
        OnPropertyChanged(nameof(MeasurementEvidenceSummary));
        OnPropertyChanged(nameof(HasCurrentMeasurementPreview));
        OnPropertyChanged(nameof(IsMeasurementPreviewPublished));
        RefreshMeasurementCommands();
    }

    private void RefreshMeasurementCommands()
    {
        previewSelectedStepCommand?.RaiseCanExecuteChanged();
        publishSelectedStepCommand?.RaiseCanExecuteChanged();
        cancelFilterPreviewCommand?.RaiseCanExecuteChanged();
    }

    private bool TryGetCurrentMeasurementHeightField(out C3DTransformedHeightField? output)
    {
        output = null;
        return SelectedPipelineStep is { InputEntityIds.Count: > 0 } step
            && !string.Equals(step.InputEntityIds[0], Source.Id, StringComparison.OrdinalIgnoreCase)
            && TryGetPublishedRegridHeightFieldOutput(step.InputEntityIds[0], out output)
            && output is not null;
    }
}
