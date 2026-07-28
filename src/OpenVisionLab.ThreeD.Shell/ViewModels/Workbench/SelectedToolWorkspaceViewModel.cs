using System.ComponentModel;
using System.Runtime.CompilerServices;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Read-first projection for the selected tool's Inputs, Parameters, Regions,
/// Outputs, and Help sections. Existing recipe, PropertyGrid, capture, artifact,
/// and execution owners remain authoritative.
/// </summary>
public sealed class SelectedToolWorkspaceViewModel : INotifyPropertyChanged
{
    private ToolWorkbenchPipelineStepItem? selectedStep;
    private object? parameterDraft;
    private bool isParameterEditorSupported;
    private bool hasPendingParameterChanges;
    private string parameterStatus = "Select an inspection step.";
    private string help = "Select an inspection step to see its inputs, parameters, regions, outputs, and authoring guidance.";

    internal SelectedToolWorkspaceViewModel(InspectionWorkspaceSelectionSession selection)
    {
        Selection = selection;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public InspectionWorkspaceSelectionSession Selection { get; }
    public ToolWorkbenchPipelineStepItem? SelectedStep => selectedStep;
    public bool HasSelectedStep => selectedStep is not null;
    public string Title => selectedStep is null
        ? "No inspection step selected"
        : $"Step {selectedStep.Order}: {selectedStep.ToolName}";
    public string State => selectedStep?.State ?? "No selection";

    public ResettableObservableCollection<SelectedToolInputItem> Inputs { get; } = [];
    public ResettableObservableCollection<SelectedToolRegionItem> Regions { get; } = [];
    public ResettableObservableCollection<SelectedToolOutputItem> Outputs { get; } = [];
    public SelectedToolRegionItem? ActiveRegion => Regions.FirstOrDefault(item => item.IsActive);
    public string ActiveRegionPosition => ActiveRegion is null
        ? string.Empty
        : $"{Regions.IndexOf(ActiveRegion) + 1}/{Regions.Count}";

    /// <summary>
    /// The exact existing typed PropertyGrid draft. This is intentionally not
    /// a copied parameter model.
    /// </summary>
    public object? ParameterDraft => parameterDraft;
    public bool IsParameterEditorSupported => isParameterEditorSupported;
    public bool HasPendingParameterChanges => hasPendingParameterChanges;
    public string ParameterStatus => parameterStatus;
    public string Help => help;

    internal void Refresh(SelectedToolWorkspaceProjection projection)
    {
        selectedStep = projection.Step;
        parameterDraft = projection.ParameterDraft;
        isParameterEditorSupported = projection.IsParameterEditorSupported;
        hasPendingParameterChanges = projection.HasPendingParameterChanges;
        parameterStatus = projection.ParameterStatus;
        help = CreateHelp(projection.Step);

        Inputs.ReplaceAll(CreateInputs(projection));
        Regions.ReplaceAll(CreateRegions(projection));
        Outputs.ReplaceAll(CreateOutputs(projection));

        OnPropertyChanged(nameof(SelectedStep));
        OnPropertyChanged(nameof(HasSelectedStep));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(State));
        OnPropertyChanged(nameof(ParameterDraft));
        OnPropertyChanged(nameof(IsParameterEditorSupported));
        OnPropertyChanged(nameof(HasPendingParameterChanges));
        OnPropertyChanged(nameof(ParameterStatus));
        OnPropertyChanged(nameof(Help));
        OnPropertyChanged(nameof(ActiveRegion));
        OnPropertyChanged(nameof(ActiveRegionPosition));
    }

    private IEnumerable<SelectedToolInputItem> CreateInputs(SelectedToolWorkspaceProjection projection)
    {
        if (projection.Step is not { } step)
        {
            yield break;
        }

        var requiredContracts = step.InputContract
            .Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var rowCount = Math.Max(
            step.MinimumInputCount,
            Math.Max(step.InputEntityIds.Count, requiredContracts.Length));
        for (var index = 0; index < rowCount; index++)
        {
            var entityId = step.InputEntityIds.ElementAtOrDefault(index) ?? string.Empty;
            var artifact = projection.Artifacts.FirstOrDefault(item =>
                string.Equals(item.Id, entityId, StringComparison.OrdinalIgnoreCase));
            yield return new SelectedToolInputItem(
                index + 1,
                requiredContracts.ElementAtOrDefault(index) ?? $"Input {index + 1}",
                entityId,
                artifact?.DisplayName ?? (entityId.Length == 0 ? "Not assigned" : entityId),
                entityId.Length == 0 ? "Missing" : artifact?.State ?? "Missing",
                artifact?.FrameId ?? string.Empty,
                artifact?.Unit ?? string.Empty,
                string.Equals(
                    entityId,
                    Selection.SelectedInputEntityId,
                    StringComparison.OrdinalIgnoreCase));
        }
    }

    private IEnumerable<SelectedToolRegionItem> CreateRegions(SelectedToolWorkspaceProjection projection)
    {
        if (projection.Step is null)
        {
            yield break;
        }

        if (projection.IsDualRegionTool)
        {
            var firstRole = projection.IsGapFlush
                ? InspectionWorkspaceRegionRole.First
                : InspectionWorkspaceRegionRole.Reference;
            var secondRole = projection.IsGapFlush
                ? InspectionWorkspaceRegionRole.Second
                : InspectionWorkspaceRegionRole.Measurement;
            yield return CreateRegion(
                firstRole,
                projection.FirstRegionLabel,
                projection.FirstRegionSelection,
                projection);
            yield return CreateRegion(
                secondRole,
                projection.SecondRegionLabel,
                projection.SecondRegionSelection,
                projection);
            yield break;
        }

        if (projection.SelectionRequirement is not null)
        {
            yield return CreateRegion(
                InspectionWorkspaceRegionRole.Selection,
                projection.SelectionRequirement.Name,
                projection.ActiveRegionSelection,
                projection);
        }
    }

    private SelectedToolRegionItem CreateRegion(
        InspectionWorkspaceRegionRole role,
        string label,
        ToolRecipeSelection? selection,
        SelectedToolWorkspaceProjection projection)
    {
        var isActive = Selection.ActiveRegionRole == role;
        var lifecycle = isActive && projection.IsCaptureActive
            ? projection.CanApplyCapture
                ? InspectionWorkspaceRegionLifecycleState.Review
                : InspectionWorkspaceRegionLifecycleState.Drawing
            : selection is null
                ? InspectionWorkspaceRegionLifecycleState.Missing
                : InspectionWorkspaceRegionLifecycleState.Applied;
        return new SelectedToolRegionItem(
            role,
            label,
            selection?.Id ?? string.Empty,
            lifecycle,
            FormatLifecycleState(lifecycle, projection.Localization),
            FormatSelection(selection),
            isActive);
    }

    private static string FormatLifecycleState(
        InspectionWorkspaceRegionLifecycleState lifecycle,
        ThreeDLocalization localization) =>
        lifecycle switch
        {
            InspectionWorkspaceRegionLifecycleState.Missing => localization.RoiMissing,
            InspectionWorkspaceRegionLifecycleState.Drawing => localization.RoiDrawing,
            InspectionWorkspaceRegionLifecycleState.Review => localization.RoiReview,
            InspectionWorkspaceRegionLifecycleState.Applied => localization.RoiApplied,
            _ => lifecycle.ToString()
        };

    private IEnumerable<SelectedToolOutputItem> CreateOutputs(SelectedToolWorkspaceProjection projection)
    {
        if (projection.Step is not { } step)
        {
            yield break;
        }

        var artifact = projection.Artifacts.FirstOrDefault(item =>
            string.Equals(item.Id, step.OutputEntityId, StringComparison.OrdinalIgnoreCase));
        var displayedOutput = projection.DisplayedOutputs.FirstOrDefault(item =>
            string.Equals(item.Id, step.OutputEntityId, StringComparison.OrdinalIgnoreCase));
        var evidence = projection.OutputEvidence;
        yield return new SelectedToolOutputItem(
            step.OutputEntityId,
            artifact?.DisplayName ?? step.ToolName,
            artifact?.Contract ?? step.OutputContract,
            artifact?.State ?? "Declared",
            artifact?.Detail ?? "No Preview or Published output exists yet.",
            evidence.ValueLabel,
            evidence.Value,
            evidence.Unit.Length == 0 ? artifact?.Unit ?? string.Empty : evidence.Unit,
            evidence.ResultStatus,
            displayedOutput?.CanShowInViewer == true,
            displayedOutput?.CanPinToCompare == true,
            displayedOutput is { IsPinnedToCompare: true } or { CanPinToCompare: true },
            displayedOutput?.IsShownInViewer == true,
            displayedOutput?.IsPinnedToCompare == true,
            displayedOutput?.ComparePinsSummary ?? string.Empty,
            displayedOutput?.Availability ?? projection.Localization.NoCurrentDisplayableOutput,
            string.Equals(
                step.OutputEntityId,
                Selection.SelectedOutputEntityId,
                StringComparison.OrdinalIgnoreCase));
    }

    private static string FormatSelection(ToolRecipeSelection? selection) =>
        selection?.GridRectangle is { } rectangle
            ? $"column {rectangle.Column}, row {rectangle.Row}, columns {rectangle.ColumnCount}, rows {rectangle.RowCount}"
            : selection is null
                ? "No recipe-owned region."
                : $"{selection.Kind} | {selection.Id}";

    private static string CreateHelp(ToolWorkbenchPipelineStepItem? step) =>
        step is null
            ? "Select an inspection step to see its inputs, parameters, regions, outputs, and authoring guidance."
            : $"{step.Tool.Description} Required inputs: {step.InputContract}. "
              + $"Authoring: confirm inputs, teach regions, apply parameters, then run Preview explicitly. "
              + $"Output: {step.OutputContract}; units remain those declared by the current source or reference frame.";

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

internal sealed record SelectedToolWorkspaceProjection(
    ToolWorkbenchPipelineStepItem? Step,
    object? ParameterDraft,
    bool IsParameterEditorSupported,
    bool HasPendingParameterChanges,
    string ParameterStatus,
    IReadOnlyList<ToolWorkbenchArtifactItem> Artifacts,
    IReadOnlyList<ToolWorkbenchDisplayedOutputItem> DisplayedOutputs,
    SelectedToolOutputEvidence OutputEvidence,
    ToolWorkbenchTeachingSelectionRequirement? SelectionRequirement,
    ToolRecipeSelection? ActiveRegionSelection,
    bool IsDualRegionTool,
    bool IsGapFlush,
    ToolRecipeSelection? FirstRegionSelection,
    ToolRecipeSelection? SecondRegionSelection,
    string FirstRegionLabel,
    string SecondRegionLabel,
    bool IsCaptureActive,
    bool CanApplyCapture,
    ThreeDLocalization Localization);

public enum InspectionWorkspaceRegionLifecycleState
{
    Missing,
    Drawing,
    Review,
    Applied
}

public sealed record SelectedToolInputItem(
    int Position,
    string RequiredContract,
    string EntityId,
    string DisplayName,
    string State,
    string FrameId,
    string Unit,
    bool IsSelected);

public sealed record SelectedToolRegionItem(
    InspectionWorkspaceRegionRole Role,
    string Label,
    string SelectionId,
    InspectionWorkspaceRegionLifecycleState Lifecycle,
    string State,
    string Detail,
    bool IsActive);

public sealed record SelectedToolOutputItem(
    string EntityId,
    string DisplayName,
    string Contract,
    string State,
    string Detail,
    string ValueLabel,
    string Value,
    string Unit,
    string ResultStatus,
    bool CanShowInViewer,
    bool CanPinToCompare,
    bool CanCompare,
    bool IsShownInViewer,
    bool IsPinnedToCompare,
    string ComparePinsSummary,
    string Availability,
    bool IsSelected)
{
    public bool HasResultStatus => !string.IsNullOrWhiteSpace(ResultStatus);
    public bool HasComparePins => !string.IsNullOrWhiteSpace(ComparePinsSummary);
}

internal sealed record SelectedToolOutputEvidence(
    string ValueLabel,
    string Value,
    string Unit,
    string ResultStatus)
{
    public static SelectedToolOutputEvidence Empty { get; } =
        new("Value", "\u2014", string.Empty, string.Empty);
}
