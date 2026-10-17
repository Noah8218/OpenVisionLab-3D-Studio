using System.ComponentModel;
using System.Runtime.CompilerServices;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Shell;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Read-first projection for the selected tool's Inputs, Parameters, Regions,
/// Outputs, and Help sections. Existing recipe, PropertyGrid, capture, artifact,
/// and execution owners remain authoritative.
/// </summary>
public sealed class SelectedToolWorkspaceViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly Dictionary<string, HeightImageViewerViewModel> previews = new(StringComparer.Ordinal);
    private readonly Func<HeightImageViewerViewModel, string, Task>? loadPreview;
    private readonly Func<string, bool> isInputAvailable;
    private readonly Func<ToolWorkbenchFlowInputItem, string, bool> isInputCompatible;
    private readonly Dictionary<string, string> previewEntities = new(StringComparer.Ordinal);
    private bool disposed;
    private ToolWorkbenchPipelineStepItem? selectedStep;
    private object? parameterDraft;
    private bool isParameterEditorSupported;
    private bool hasPendingParameterChanges;
    private ThreeDLocalization localization = ThreeDLocalization.Shared;
    private string parameterStatus = ThreeDLocalization.Shared.StepParameterSelectTypedTool;
    private string help = CreateHelp(null, ThreeDLocalization.Shared);
    private string example = ThreeDLocalization.Shared.SelectedToolEmptyExample;
    private string expectedOverlay = ThreeDLocalization.Shared.SelectedToolEmptyExpectedOverlay;
    private string commonState = ThreeDLocalization.Shared.StateLabel(InspectionStepState.Empty);
    private string outputPolicy = ThreeDLocalization.Shared.OutputPolicyLabel(true);

    internal SelectedToolWorkspaceViewModel(InspectionWorkspaceSelectionSession selection,
        Func<HeightImageViewerViewModel, string, Task>? loadPreview = null, Func<string, bool>? isInputAvailable = null,
        Func<ToolWorkbenchFlowInputItem, string, bool>? isInputCompatible = null)
    {
        Selection = selection;
        this.loadPreview = loadPreview;
        this.isInputAvailable = isInputAvailable ?? (_ => false);
        this.isInputCompatible = isInputCompatible ?? ((_, _) => true);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public InspectionWorkspaceSelectionSession Selection { get; }
    public ToolWorkbenchPipelineStepItem? SelectedStep => selectedStep;
    public bool HasSelectedStep => selectedStep is not null;
    public string Title => selectedStep is null
        ? localization.NoTaughtStepSelected
        : string.Format(localization.SelectedPipelineStepTitleFormat, selectedStep.Order, selectedStep.ToolName);
    public string State => selectedStep?.State ?? localization.NoSelectedToolState;

    public ResettableObservableCollection<SelectedToolInputItem> Inputs { get; } = [];
    public IEnumerable<SelectedToolInputItem> DataInputs => Inputs.Where(item => !item.IsRegionInput);
    public ResettableObservableCollection<SelectedToolRegionItem> Regions { get; } = [];
    public ResettableObservableCollection<SelectedToolOutputItem> Outputs { get; } = [];
    public SelectedToolOutputItem? CurrentOutput => Outputs.FirstOrDefault(item => item.IsSelected)
        ?? Outputs.FirstOrDefault();
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
        if (disposed) return;
        localization = projection.Localization;
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
        OnPropertyChanged(nameof(DataInputs));
        Regions.ReplaceAll(CreateRegions(projection));
        Outputs.ReplaceAll(CreateOutputs(projection));
        var retainedPreviews = Inputs.Select(item => item.Preview).Concat(Outputs.Select(item => item.Preview)).ToHashSet();
        foreach (var key in previews.Keys.Where(key => !retainedPreviews.Contains(previews[key])).ToArray())
        {
            previews[key].Dispose();
            previews.Remove(key);
            previewEntities.Remove(key);
        }
        OnPropertyChanged(nameof(CurrentOutput));

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

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        foreach (var preview in previews.Values) preview.Dispose();
        previews.Clear();
        previewEntities.Clear();
    }

    private HeightImageViewerViewModel? GetPreview(string key, string entityId, string contract)
    {
        if (loadPreview is null || !contract.Contains("HeightField", StringComparison.OrdinalIgnoreCase)
            && !contract.Contains("C3D", StringComparison.OrdinalIgnoreCase)) return null;
        if (!previews.TryGetValue(key, out var preview))
        {
            preview = new HeightImageViewerViewModel(localization, new SharedHeightCursorSession());
            previews.Add(key, preview);
        }
        if (!previewEntities.TryGetValue(key, out var previousEntity) || previousEntity != entityId)
        {
            preview.ClearSource();
            previewEntities[key] = entityId;
        }
        preview.StartObservedLoad(() => loadPreview(preview, entityId), exception => preview.ReportSourceFailure(exception.Message));
        return preview;
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
                requiredContracts.ElementAtOrDefault(index)
                    ?? string.Format(projection.Localization.SelectedToolInputFormat, index + 1),
                entityId,
                artifact?.DisplayName ?? (entityId.Length == 0
                    ? projection.Localization.SelectedToolInputNotAssigned
                    : entityId),
                entityId.Length == 0 || artifact is null
                    ? projection.Localization.RoiMissing
                    : projection.Localization.SelectedToolOutputStateLabel(artifact.State),
                artifact?.FrameId ?? string.Empty,
                artifact?.Unit ?? string.Empty,
                string.Equals(
                    entityId,
                    Selection.SelectedInputEntityId,
                    StringComparison.OrdinalIgnoreCase))
            {
                Preview = GetPreview($"input:{index}", entityId, requiredContracts.ElementAtOrDefault(index) ?? string.Empty),
                Choices = CreateInputChoices(projection, index, entityId).ToArray()
            };
        }
    }

    private IEnumerable<SelectedToolInputChoice> CreateInputChoices(SelectedToolWorkspaceProjection projection, int index, string currentEntityId)
    {
        if (projection.Step is not { } step || step.FlowInputs.ElementAtOrDefault(index) is not { } port) yield break;
        foreach (var artifact in projection.Artifacts)
        {
            var isCurrent = string.Equals(artifact.Id, currentEntityId, StringComparison.OrdinalIgnoreCase);
            var isMap = artifact.Contract.Contains("HeightField", StringComparison.OrdinalIgnoreCase)
                || artifact.Contract.Contains("C3D", StringComparison.OrdinalIgnoreCase);
            var precedes = artifact.PipelineStep is null
                || int.TryParse(artifact.PipelineStep.Order, out var producerOrder) && int.TryParse(step.Order, out var consumerOrder)
                && producerOrder < consumerOrder;
            if (!isCurrent && (!isMap || !precedes || !isInputCompatible(port, artifact.Id))) continue;
            yield return new SelectedToolInputChoice(port, artifact.Id, artifact.DisplayName, isInputAvailable(artifact.Id));
        }
        if (!projection.Artifacts.Any(artifact => string.Equals(artifact.Id, currentEntityId, StringComparison.OrdinalIgnoreCase)))
            yield return new SelectedToolInputChoice(port, currentEntityId, string.IsNullOrEmpty(currentEntityId)
                ? projection.Localization.SelectedToolInputNotAssigned : currentEntityId, false);
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
                projection.Localization.TeachingSelectionPresentation(projection.SelectionRequirement).Name,
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
                FormatSelection(selection, projection.Localization),
            isActive) { Rectangle = selection?.GridRectangle };
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
        var valueLabel = string.IsNullOrWhiteSpace(evidence.ValueLabel)
            ? projection.Localization.SelectedToolOutputValueLabel
            : evidence.ValueLabel;
        yield return new SelectedToolOutputItem(
            step.OutputEntityId,
            artifact?.DisplayName ?? step.ToolName,
            artifact?.Contract ?? step.OutputContract,
            artifact is null
                ? projection.Localization.FlowPortDeclared
                : projection.Localization.SelectedToolOutputStateLabel(artifact.State),
            artifact?.Detail ?? string.Format(projection.Localization.FlowPortDeclaredDetailFormat, step.OutputEntityId),
            valueLabel,
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
                StringComparison.OrdinalIgnoreCase),
            string.Join(" → ", step.InputEntityIds.Select(id =>
                projection.Artifacts.FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase))?.DisplayName ?? id)),
            evidence.LimitSummary,
            evidence.Reason,
            evidence.HasExecutionResult)
        {
            Preview = GetPreview("output", step.OutputEntityId, artifact?.Contract ?? step.OutputContract)
        };
    }

    private static string FormatSelection(
        ToolRecipeSelection? selection,
        ThreeDLocalization localization) =>
        selection?.GridRectangle is { } rectangle
            ? string.Format(
                localization.SelectedToolGridRectangleFormat,
                rectangle.Column,
                rectangle.Row,
                rectangle.ColumnCount,
                rectangle.RowCount)
            : selection is null
                ? localization.NoRecipeOwnedRegion
                : $"{localization.SelectedToolSelectionKindLabel(selection.Kind)} | {selection.Id}";

    private static string CreateHelp(
        ToolWorkbenchPipelineStepItem? step,
        ThreeDLocalization localization) =>
        step is null
            ? localization.Resolve(
                "ThreeD.Workbench.SelectedToolHelpEmpty",
                "검사 단계를 선택하면 입력, 설정값, 영역, 출력, 작성 안내가 표시됩니다.",
                "Select an inspection step to see its inputs, parameters, regions, outputs, and authoring guidance.")
            : $"{step.Tool.DisplayDescription} "
              + localization.Resolve(
                  "ThreeD.Workbench.SelectedToolHelpInputs",
                  $"필수 입력: {step.InputContractDisplay}.",
                  $"Required inputs: {step.InputContract}.")
              + " "
              + localization.Resolve(
                  "ThreeD.Workbench.SelectedToolHelpAuthoring",
                  "입력과 영역을 확인하고 설정값을 적용한 뒤 선택 도구를 실행하세요.",
                  "Confirm inputs and regions, apply parameters, then run the selected tool.")
              + " "
              + localization.Resolve(
                  "ThreeD.Workbench.SelectedToolHelpOutput",
                  $"결과: {step.OutputContractDisplay}. 단위는 현재 입력 데이터 또는 기준 좌표계가 선언한 값을 유지합니다.",
                  $"Result: {step.OutputContract}. Units remain those declared by the current source or reference frame.");

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
                projection.Localization.SelectedToolEmptyExample,
                projection.Localization.SelectedToolEmptyExpectedOverlay);
        }

        var localization = projection.Localization;
        if (string.Equals(step.ToolId, "thickness", StringComparison.OrdinalIgnoreCase))
        {
            return new(
                localization.Resolve(
                    "ThreeD.Workbench.ThicknessExample",
                    "기준면과 측정면의 ROI를 지정하고 규격을 확인한 뒤 선택 도구를 실행하세요.",
                    "Set the reference and measurement ROIs, confirm the limits, then run the selected tool."),
                localization.Resolve(
                    "ThreeD.Workbench.ThicknessExpectedOverlay",
                    "예상: 기준면에서 측정면까지의 부호 있는 H축 두께와 유효 샘플·허용값 상태가 표시됩니다. 원본 높이와 티칭 ROI는 변경되지 않습니다.",
                    "Expected: signed H-axis thickness from the fitted reference surface to the measurement surface is shown with valid-sample and tolerance status. Source heights and taught ROIs remain unchanged."));
        }

        if (string.Equals(step.ToolId, "height-threshold-background-removal", StringComparison.OrdinalIgnoreCase))
        {
            return new(
                localization.Resolve(
                    "ThreeD.Workbench.HeightThresholdBackgroundRemovalExample",
                    "입력의 높이 기준값과 유지 방향을 설정한 뒤 선택 도구를 실행하세요.",
                    "Set the input height threshold and keep direction, then run the selected tool."),
                localization.Resolve(
                    "ThreeD.Workbench.HeightThresholdBackgroundRemovalExpectedOverlay",
                    "예상: 원본과 동일한 격자의 별도 출력에 유지 셀과 결측 셀이 표시되고, 원본·기존 결측·설정값 정보는 그대로 보존됩니다.",
                    "Expected: a separate same-grid output shows retained and missing cells while the source, existing missing cells, and parameter evidence remain preserved."));
        }

        if (string.Equals(step.ToolId, "signed-delta-background-subtraction", StringComparison.OrdinalIgnoreCase))
        {
            return new(
                localization.Resolve(
                    "ThreeD.Workbench.SignedDeltaBackgroundSubtractionExample",
                    "현재 입력과 저장된 배경을 연결하고 격자·좌표계가 일치하는지 확인한 뒤 선택 도구를 실행하세요.",
                    "Connect the current input and saved background, confirm matching grids and frames, then run the selected tool."),
                localization.Resolve(
                    "ThreeD.Workbench.SignedDeltaBackgroundSubtractionExpectedOverlay",
                    "현재 입력에서 배경을 뺀 높이 차이와 결측 수가 표시됩니다. 높이 차이 0은 결측으로 처리하지 않습니다.",
                    "Height differences and missing counts are shown. A zero height difference is not treated as missing."));
        }

        if (string.Equals(step.ToolId, "height-difference-edge", StringComparison.OrdinalIgnoreCase))
        {
            return new(
                localization.Resolve(
                    "ThreeD.Workbench.HeightDifferenceEdgeExample",
                    "처리한 높이 맵과 검색 영역을 연결하고 방향·최소 높이 차이를 설정한 뒤 선택 도구를 실행하세요. 검출한 점을 3D Line Fit에 연결할 수 있습니다.",
                    "Connect the processed height map and search region, set the direction and minimum height difference, then run the selected tool. Connect the detected points to 3D Line Fit."),
                localization.Resolve(
                    "ThreeD.Workbench.HeightDifferenceEdgeExpectedOverlay",
                    "예상: 각 스캔라인에서 선택된 인접 높이 차이의 중간점이 에지 점 집합으로 표시됩니다. 결측 쌍은 건너뛰고 입력 맵은 변경되지 않습니다.",
                    "Expected: the selected adjacent-height pair midpoint from each scanline is recorded in an EdgePointSet. Missing pairs are skipped and the input map is unchanged."));
        }

        if (string.Equals(step.ToolId, "three-d-line-fit", StringComparison.OrdinalIgnoreCase))
        {
            return new(
                localization.Resolve(
                    "ThreeD.Workbench.ThreeDLineFitExample",
                    "높이 차이 에지 결과를 연결하고 잔차·유효점 기준을 적용한 뒤 선택 도구를 실행하세요.",
                    "Connect the Height Difference Edge result, apply residual and inlier limits, then run the selected tool."),
                localization.Resolve(
                    "ThreeD.Workbench.ThreeDLineFitExpectedOverlay",
                    "예상: 기준을 통과한 점에 맞춘 3D 선과 피팅 근거가 생성됩니다. 잔차·유효점·스캔라인 범위를 충족하지 않으면 유효한 선을 만들지 않습니다.",
                    "Expected: a 3D line and fit evidence are produced from points that pass the gates. If residual, inlier, or scanline-span limits are not met, no valid line is produced."));
        }

        if (string.Equals(step.ToolId, "physical-edge-gap-flush", StringComparison.OrdinalIgnoreCase))
        {
            return new(
                localization.Resolve(
                    "ThreeD.Workbench.PhysicalEdgeGapFlushExample",
                    "첫째·둘째 직선과 각 직선에 대응하는 에지 점을 순서대로 연결한 뒤 선택 도구를 실행하세요.",
                    "Connect the first and second lines and their corresponding edge points in order, then run the selected tool."),
                localization.Resolve(
                    "ThreeD.Workbench.PhysicalEdgeGapFlushExpectedOverlay",
                    "틈새·단차와 측정 유효성을 확인하세요. 허용 범위와 입력 일치 여부는 결과 상세에서 확인할 수 있습니다.",
                    "Check gap, flush, and measurement validity. Limits and input correspondence are available in result details."));
        }

        if (string.Equals(step.ToolId, "connected-region", StringComparison.OrdinalIgnoreCase))
        {
            return new(
                localization.Resolve(
                    "ThreeD.Workbench.ConnectedRegionExample",
                    "처리된 높이 입력을 연결하고 연결 방향을 확인한 뒤 선택 도구를 실행하세요.",
                    "Connect the processed height input, confirm connectivity, then run the selected tool."),
                localization.Resolve(
                    "ThreeD.Workbench.ConnectedRegionExpectedOverlay",
                    "예상: 원본 격자 위에 연결 영역별 셀과 경계 상자가 표시됩니다. 원본 높이와 앞 단계의 결과는 바뀌지 않습니다.",
                    "Expected: connected-region cells and bounding boxes appear on the source grid; the input and upstream output stay unchanged."));
        }

        if (string.Equals(step.ToolId, "domain-mask", StringComparison.OrdinalIgnoreCase))
        {
            return new(
                localization.Resolve(
                    "ThreeD.Workbench.DomainMaskExample",
                    "높이 입력과 영역을 연결하세요. 검출 영역을 쓰는 경우 원본·검출 입력·검출 영역이 같은 입력에 속하는지 확인한 뒤 실행하세요.",
                    "Connect the height input and region. For a detected region, confirm that the source, detection input, and region belong to the same input, then run."),
                localization.Resolve(
                    "ThreeD.Workbench.DomainMaskExpectedOverlay",
                    "선택한 영역의 높이값과 결측값은 유지하고 영역 밖은 결측으로 표시합니다. 원본과 영역 검출용 높이 데이터는 바뀌지 않습니다.",
                    "Expected: v1 keeps values in the typed domain. v2 keeps raw values only at the selected detected-region cells, marks all other cells missing in a separate same-grid HeightField, and leaves both raw and detection inputs unchanged."));
        }

        if (string.Equals(step.ToolId, "connected-region-gate", StringComparison.OrdinalIgnoreCase))
        {
            return new(
                localization.Resolve(
                    "ThreeD.Workbench.ConnectedRegionGateExample",
                    "원본 입력, 영역 검출에 사용한 높이 맵, 검출 영역을 연결하세요. 검사할 영역과 커버리지·높이 기준을 설정한 뒤 선택 도구를 실행하세요.",
                    "Connect the original input, the height map used to detect regions, and the detected regions. Select the region and set coverage and height limits, then run the selected tool."),
                localization.Resolve(
                    "ThreeD.Workbench.ConnectedRegionGateExpectedOverlay",
                    "선택한 검출 영역의 유효 셀 비율과 평균 높이 판정을 표시합니다. 입력과 영역의 연결 관계도 확인할 수 있습니다. 영역이 없거나 원본이 다르면 검사 오류 또는 판정 불가로 표시하며, 검사 대상이 없는 것으로 판정하지 않습니다.",
                    "Expected: finite coverage, mean-height Pass/Fail, raw/detection/region provenance, and state are shown for one selected detected region. Missing or mismatched provenance remains error/not-evaluated and is not converted to expected-object absence."));
        }

        if (string.Equals(step.ToolId, "grid-polygon-domain", StringComparison.OrdinalIgnoreCase))
        {
            return new(
                localization.Resolve(
                    "ThreeD.Workbench.GridPolygonDomainExample",
                    "높이 입력과 현재 다각형 영역을 연결한 뒤 선택 도구를 실행하세요.",
                    "Connect the height input and current polygon region, then run the selected tool."),
                localization.Resolve(
                    "ThreeD.Workbench.GridPolygonDomainExpectedOverlay",
                    "영역에 포함된 셀이 표시됩니다. 입력의 높이값과 단위는 바뀌지 않습니다.",
                    "Included cells are shown. Input heights and units remain unchanged."));
        }

        if (string.Equals(step.ToolId, "editable-region", StringComparison.OrdinalIgnoreCase))
        {
            return new(
                localization.Resolve(
                    "ThreeD.Workbench.EditableRegionExample",
                    "연결 영역 도구를 먼저 실행한 뒤 사용할 영역 번호를 선택하고 현재 도구를 실행하세요.",
                    "Run Connected Region first, select the region index to use, then run the current tool."),
                localization.Resolve(
                    "ThreeD.Workbench.EditableRegionExpectedOverlay",
                    "선택한 영역에 해당하는 원본 격자의 셀과 경계를 강조합니다. 연결 영역 전체 결과는 유지됩니다.",
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
                    "기준 영역을 두 번째 입력으로, 편집 영역 결과를 세 번째 입력으로 연결한 뒤 선택 도구를 실행하세요.",
                    "Connect the reference region as input 2 and the editable-region result as input 3, then run the selected tool."),
                localization.Resolve(
                    "ThreeD.Workbench.CompletenessEditableRegionExpectedOverlay",
                    "검사 영역과 셀별 판정이 표시됩니다. 편집 영역의 경계를 기준으로 검사합니다.",
                    "The inspection region and cell decisions are shown. Inspection uses the editable region bounds."));
        }

        return new(
            localization.Resolve(
                "ThreeD.Workbench.GenericToolExample",
                "입력과 영역을 확인하고 설정값을 적용한 뒤 선택 도구를 실행하세요.",
                "Confirm inputs and regions, apply parameters, then run the selected tool."),
            localization.Resolve(
                "ThreeD.Workbench.GenericToolExpectedOverlay",
                "예상: 현재 도구의 입력·영역·결과와 검토 정보가 표시되며 원본은 변경되지 않습니다.",
                "Expected: this tool's inputs, regions, result, and review information are shown; the source remains unchanged."));
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
    bool IsSelected)
{
    public HeightImageViewerViewModel? Preview { get; init; }
    public IReadOnlyList<SelectedToolInputChoice> Choices { get; init; } = [];
    public bool IsRegionInput => RequiredContract.Contains("GridRectangle", StringComparison.OrdinalIgnoreCase)
        || RequiredContract.Contains("GridPolygon selection", StringComparison.OrdinalIgnoreCase);
    public bool IsMapInput => Preview is not null;
    public string RequiredContractDisplay => ThreeDLocalization.Shared.ContractLabel(RequiredContract);
}

public sealed record SelectedToolInputChoice(ToolWorkbenchFlowInputItem Port, string EntityId, string DisplayName, bool IsAvailable);

public sealed record SelectedToolRegionItem(
    InspectionWorkspaceRegionRole Role,
    string Label,
    string SelectionId,
    InspectionWorkspaceRegionLifecycleState Lifecycle,
    string State,
    string Detail,
    bool IsActive)
{
    public ToolRecipeGridRectangle? Rectangle { get; init; }
    public bool HasRectangle => Rectangle is not null;
}

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
    bool IsSelected,
    string InputSummary,
    string LimitSummary,
    string Reason,
    bool HasExecutionResult)
{
    public HeightImageViewerViewModel? Preview { get; init; }
    public bool IsMapOutput => Preview is not null;
    public string ContractDisplay => ThreeDLocalization.Shared.ContractLabel(Contract);
    public bool HasResultStatus => !string.IsNullOrWhiteSpace(ResultStatus);
    public bool HasEvidenceValue => !string.IsNullOrWhiteSpace(Value) && Value != "\u2014";
    public bool HasComparePins => !string.IsNullOrWhiteSpace(ComparePinsSummary);
    public bool HasInputSummary => !string.IsNullOrWhiteSpace(InputSummary);
}

internal sealed record SelectedToolOutputEvidence(
    string ValueLabel,
    string Value,
    string Unit,
    string ResultStatus,
    string LimitSummary = "\u2014",
    string Reason = "\u2014",
    bool HasExecutionResult = true)
{
    public ResultStatus? ExecutionStatus { get; init; }
    public static SelectedToolOutputEvidence Empty { get; } =
        new(string.Empty, "\u2014", string.Empty, string.Empty, HasExecutionResult: false);
}
