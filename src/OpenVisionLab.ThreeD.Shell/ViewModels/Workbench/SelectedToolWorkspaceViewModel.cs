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
    private string example = "Select an inspection step to see a concrete authoring example.";
    private string expectedOverlay = "Select an inspection step to see the expected review overlay.";
    private string commonState = "Empty";
    private string outputPolicy = "Enabled";

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
    public string Example => example;
    public string ExpectedOverlay => expectedOverlay;
    public string CommonState => commonState;
    public string OutputPolicy => outputPolicy;

    internal void Refresh(SelectedToolWorkspaceProjection projection)
    {
        selectedStep = projection.Step;
        parameterDraft = projection.ParameterDraft;
        isParameterEditorSupported = projection.IsParameterEditorSupported;
        hasPendingParameterChanges = projection.HasPendingParameterChanges;
        parameterStatus = projection.ParameterStatus;
        help = CreateHelp(projection.Step, projection.Localization);
        var guidance = CreateGuidance(projection);
        example = guidance.Example;
        expectedOverlay = guidance.ExpectedOverlay;
        commonState = CreateCommonState(projection.Step, projection.Localization);
        outputPolicy = projection.Step is { } policyStep
            ? projection.Localization.OutputPolicyLabel(policyStep.OutputEnabled)
            : projection.Localization.OutputPolicyLabel(true);

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
        OnPropertyChanged(nameof(Example));
        OnPropertyChanged(nameof(ExpectedOverlay));
        OnPropertyChanged(nameof(CommonState));
        OnPropertyChanged(nameof(OutputPolicy));
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

    private static string CreateHelp(
        ToolWorkbenchPipelineStepItem? step,
        ThreeDLocalization localization) =>
        step is null
            ? localization.Resolve(
                "ThreeD.Workbench.SelectedToolHelpEmpty",
                "검사 단계를 선택하면 입력, 파라미터, 영역, 출력, 작성 안내가 표시됩니다.",
                "Select an inspection step to see its inputs, parameters, regions, outputs, and authoring guidance.")
            : $"{step.Tool.Description} "
              + localization.Resolve(
                  "ThreeD.Workbench.SelectedToolHelpInputs",
                  $"필수 입력: {step.InputContract}.",
                  $"Required inputs: {step.InputContract}.")
              + " "
              + localization.Resolve(
                  "ThreeD.Workbench.SelectedToolHelpAuthoring",
                  "입력을 확인하고 영역을 티칭한 뒤 파라미터를 적용하고 Preview를 명시적으로 실행하세요.",
                  "Confirm inputs, teach regions, apply parameters, then select Preview explicitly.")
              + " "
              + localization.Resolve(
                  "ThreeD.Workbench.SelectedToolHelpOutput",
                  $"출력: {step.OutputContract}. 단위는 현재 소스 또는 기준 프레임이 선언한 값을 유지합니다.",
                  $"Output: {step.OutputContract}. Units remain those declared by the current source or reference frame.");

    private static string CreateCommonState(
        ToolWorkbenchPipelineStepItem? step,
        ThreeDLocalization localization)
    {
        if (step is null)
        {
            return localization.StateLabel(InspectionStepState.Empty);
        }

        var descriptor = InspectionStepStateMatrix.Describe(step.State);
        return $"{localization.StateLabel(descriptor.State)} ({descriptor.Key})";
    }

    private static ToolGuidance CreateGuidance(SelectedToolWorkspaceProjection projection)
    {
        if (projection.Step is not { } step)
        {
            return new(
                "Select an inspection step to see a concrete authoring example.",
                "Select an inspection step to see the expected review overlay.");
        }

        var localization = projection.Localization;
        if (string.Equals(step.ToolId, "connected-region", StringComparison.OrdinalIgnoreCase))
        {
            return new(
                localization.Resolve(
                    "ThreeD.Workbench.ConnectedRegionExample",
                    "예: Published FilteredHeightField를 연결하고 Connectivity=Four를 확인한 뒤 Preview를 누릅니다.",
                    "Example: connect a Published FilteredHeightField, confirm Connectivity=Four, then select Preview."),
                localization.Resolve(
                    "ThreeD.Workbench.ConnectedRegionExpectedOverlay",
                    "예상: 원본 격자 위에 연결 영역별 셀과 경계 상자가 표시됩니다. 입력과 상위 출력은 변경되지 않습니다.",
                    "Expected: connected-region cells and bounding boxes appear on the source grid; the input and upstream output stay unchanged."));
        }

        if (string.Equals(step.ToolId, "domain-mask", StringComparison.OrdinalIgnoreCase))
        {
            return new(
                localization.Resolve(
                    "ThreeD.Workbench.DomainMaskExample",
                    "예: Published HeightField와 그 결과를 만든 Published ConnectedRegionArtifact를 연결한 뒤 Preview, 결과를 확인하고 Publish합니다.",
                    "Example: connect the Published HeightField and its matching Published ConnectedRegionArtifact, then Preview, review, and Publish."),
                localization.Resolve(
                    "ThreeD.Workbench.DomainMaskExpectedOverlay",
                    "예상: 연결 영역의 모든 셀만 원래 값 또는 기존 missing으로 남고, 영역 밖은 missing인 별도 same-grid HeightField로 표시됩니다. 입력은 변경되지 않습니다.",
                    "Expected: every connected-region cell keeps its value or existing missing state, outside cells become missing in a separate same-grid HeightField, and inputs remain unchanged."));
        }

        if (string.Equals(step.ToolId, "editable-region", StringComparison.OrdinalIgnoreCase))
        {
            return new(
                localization.Resolve(
                    "ThreeD.Workbench.EditableRegionExample",
                    "예: Connected Region을 먼저 Publish한 뒤 SelectedRegionIndex=0으로 한 영역만 선택하고 Preview 후 Publish합니다.",
                    "Example: Publish Connected Region first, choose one region with SelectedRegionIndex=0, then Preview and Publish."),
                localization.Resolve(
                    "ThreeD.Workbench.EditableRegionExpectedOverlay",
                    "예상: 선택한 영역의 정확한 source-grid 셀과 경계가 강조되고, Connected Region 전체 출력은 유지됩니다.",
                    "Expected: the selected region's exact source-grid cells and bounds are highlighted while the full Connected Region output remains intact."));
        }

        var usesEditableRegion =
            string.Equals(step.ToolId, "completeness-grid", StringComparison.OrdinalIgnoreCase)
            && step.InputEntityIds.ElementAtOrDefault(2) is { } inspectionInputId
            && projection.Artifacts.Any(item =>
                string.Equals(item.Id, inspectionInputId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.Contract, "EditableRegionArtifact", StringComparison.OrdinalIgnoreCase));
        if (usesEditableRegion)
        {
            return new(
                localization.Resolve(
                    "ThreeD.Workbench.CompletenessEditableRegionExample",
                    "예: 기준 GridRectangle를 두 번째 입력으로 연결하고 EditableRegionArtifact를 세 번째 입력으로 연결한 뒤 Preview를 누릅니다.",
                    "Example: route the reference GridRectangle as input 2 and the EditableRegionArtifact as input 3, then select Preview."),
                localization.Resolve(
                    "ThreeD.Workbench.CompletenessEditableRegionExpectedOverlay",
                    "예상: 선택 영역의 provenance가 Completeness 결과에 기록되고, 파생 검사 영역과 셀별 Pass/Fail evidence가 표시됩니다. 기존 SDK 경로는 영역 경계를 평가합니다.",
                    "Expected: selected-region provenance is recorded in Completeness evidence and the derived inspection region and cell Pass/Fail evidence are shown. The existing SDK path evaluates the region bounds."));
        }

        return new(
            localization.Resolve(
                "ThreeD.Workbench.GenericToolExample",
                "예: 입력과 레시피 소유 영역을 확인하고 파라미터를 적용한 뒤 Preview를 누릅니다.",
                "Example: confirm inputs and recipe-owned regions, apply parameters, then select Preview."),
            localization.Resolve(
                "ThreeD.Workbench.GenericToolExpectedOverlay",
                "예상: 현재 도구가 선언한 입력·영역·출력 evidence만 표시되며 원본은 변경되지 않습니다.",
                "Expected: only this tool's declared inputs, regions, and output evidence are shown; the source remains unchanged."));
    }

    private sealed record ToolGuidance(string Example, string ExpectedOverlay);

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
