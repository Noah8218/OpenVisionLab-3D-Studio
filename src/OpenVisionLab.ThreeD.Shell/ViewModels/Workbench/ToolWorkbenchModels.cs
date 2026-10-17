using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Viewer.Hosting;
using OpenVisionLab.ThreeD.Shell;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

public sealed record ToolWorkbenchStepRemovalRequestEventArgs(
    string StepId,
    string StepName,
    IReadOnlyList<string> OrphanedSelectionNames);

public sealed record ToolWorkbenchTeachingSelectionRemovalRequestEventArgs(
    ToolRecipeSelection Selection,
    string SelectionSummary,
    IReadOnlyList<string> AffectedStepNames);

public enum ToolWorkbenchToolRouteKind
{
    OrderedRun,
    SpecialExperiment,
    ReviewOnly
}

public sealed record ToolWorkbenchToolItem(
    string Category,
    string Name,
    string Id,
    int MinimumInputCount,
    string InputContract,
    string OutputContract,
    string Description,
    IReadOnlyList<ToolWorkbenchParameterSeed> Parameters,
    string? LocalizedNameKey = null,
    string? KoreanDisplayName = null,
    IReadOnlyList<string>? SearchAliases = null,
    string? LocalizedDescriptionKey = null,
    string? KoreanDescription = null,
    string? EnglishDescription = null,
    ToolWorkbenchToolRouteKind RouteKind = ToolWorkbenchToolRouteKind.OrderedRun) : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public string DisplayName
    {
        get
        {
            var key = LocalizedNameKey;
            if (string.IsNullOrWhiteSpace(key))
            {
                return Name;
            }

            var korean = string.IsNullOrWhiteSpace(KoreanDisplayName)
                ? Name
                : KoreanDisplayName;
            return ThreeDLocalization.Shared.Resolve(key, korean, Name);
        }
    }

    public string InputContractDisplay => ThreeDLocalization.Shared.ContractLabel(InputContract);
    public string OutputContractDisplay => ThreeDLocalization.Shared.ContractLabel(OutputContract);

    public string DisplayDescription
    {
        get
        {
            var key = LocalizedDescriptionKey;
            if (string.IsNullOrWhiteSpace(key))
            {
                return Description;
            }

            var korean = string.IsNullOrWhiteSpace(KoreanDescription)
                ? Description
                : KoreanDescription;
            var english = string.IsNullOrWhiteSpace(EnglishDescription)
                ? Description
                : EnglishDescription;
            return ThreeDLocalization.Shared.Resolve(key, korean, english);
        }
    }

    internal void NotifyLocalizedPropertiesChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayName)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayDescription)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(InputContractDisplay)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OutputContractDisplay)));
    }
}

public sealed record ToolWorkbenchParameterSeed(string Name, string DefaultValue);

public sealed record ToolWorkbenchTeachingSelectionRequirement(
    string Name,
    string Kind,
    int RequiredPointCount,
    bool UsesViewerCapture,
    string Description);

public sealed class ToolWorkbenchTeachingCaptureRequestEventArgs(
    string stepId,
    string selectionId,
    string selectionName,
    string kind,
    int requiredPointCount,
    string rootSourceId,
    string frameId,
    ToolRecipeSelectionSourceBinding sourceBinding,
    ToolRecipeSelection? existingSelection) : EventArgs
{
    public string StepId { get; } = stepId;
    public string SelectionId { get; } = selectionId;
    public string SelectionName { get; } = selectionName;
    public string Kind { get; } = kind;
    public int RequiredPointCount { get; } = requiredPointCount;
    public string RootSourceId { get; } = rootSourceId;
    public string FrameId { get; } = frameId;
    public ToolRecipeSelectionSourceBinding SourceBinding { get; } = sourceBinding;
    public ToolRecipeSelection? ExistingSelection { get; } = existingSelection;
}

public sealed class ToolWorkbenchGridRectangleDraftChangedEventArgs(
    ToolRecipeGridRectangle rectangle) : EventArgs
{
    public ToolRecipeGridRectangle Rectangle { get; } = rectangle;
}

public sealed class ToolWorkbenchGridCircleDraftChangedEventArgs(
    ToolRecipeGridCircle circle) : EventArgs
{
    public ToolRecipeGridCircle Circle { get; } = circle;
}

public sealed class ToolWorkbenchGridPolygonDraftChangedEventArgs(
    ToolRecipeGridPolygon polygon) : EventArgs
{
    public ToolRecipeGridPolygon Polygon { get; } = polygon;
}

public sealed class ToolWorkbenchGridPolygonVertexItem : INotifyPropertyChanged
{
    private double row;
    private double column;

    public ToolWorkbenchGridPolygonVertexItem(
        int order,
        double row,
        double column,
        Action<ToolWorkbenchGridPolygonVertexItem> changed)
    {
        Order = order;
        this.row = row;
        this.column = column;
        Changed = changed;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    internal Action<ToolWorkbenchGridPolygonVertexItem>? Changed { get; set; }

    public int Order { get; private set; }

    public double Row
    {
        get => row;
        set
        {
            if (row.Equals(value))
            {
                return;
            }

            row = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Row)));
            Changed?.Invoke(this);
        }
    }

    public double Column
    {
        get => column;
        set
        {
            if (column.Equals(value))
            {
                return;
            }

            column = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Column)));
            Changed?.Invoke(this);
        }
    }

    internal void SetOrder(int order)
    {
        if (Order == order)
        {
            return;
        }

        Order = order;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Order)));
    }
}

public sealed class ToolWorkbenchSourceItem : INotifyPropertyChanged
{
    private string id;
    private string name;
    private string format;
    private string unit;
    private string frameId;
    private string path;
    private HeightMeasurementEvidence? measurementEvidence;
    private string? sensorId;

    public ToolWorkbenchSourceItem(string id, string name, string format, string unit, string frameId, string path)
    {
        this.id = id;
        this.name = name;
        this.format = format;
        this.unit = unit;
        this.frameId = frameId;
        this.path = path;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Id { get => id; set => SetField(ref id, value ?? string.Empty); }
    public string Name { get => name; set => SetField(ref name, value ?? string.Empty); }
    public string Format { get => format; set => SetField(ref format, value ?? string.Empty); }
    public string Unit { get => unit; set => SetField(ref unit, value ?? string.Empty); }
    public string FrameId { get => frameId; set => SetField(ref frameId, value ?? string.Empty); }
    public string Path { get => path; set => SetField(ref path, value ?? string.Empty); }
    public HeightMeasurementEvidence? MeasurementEvidence
    {
        get => measurementEvidence;
        set => SetField(ref measurementEvidence, value);
    }
    public string? SensorId
    {
        get => sensorId;
        set => SetField(ref sensorId, value);
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class ToolWorkbenchReferenceItem : INotifyPropertyChanged
{
    private string id;
    private string name;
    private string kind;

    public ToolWorkbenchReferenceItem(string id, string name, string kind)
    {
        this.id = id;
        this.name = name;
        this.kind = kind;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Id { get => id; set => SetField(ref id, value ?? string.Empty); }
    public string Name { get => name; set => SetField(ref name, value ?? string.Empty); }
    public string Kind { get => kind; set => SetField(ref kind, value ?? string.Empty); }

    private void SetField(ref string field, string value, [CallerMemberName] string? propertyName = null)
    {
        if (field == value) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed record ToolWorkbenchFlowInputItem(
    ToolWorkbenchPipelineStepItem Step, int Index, string Contract, string EntityId,
    C3DHeightImageDisplayFrame? PreviewFrame = null, ToolRecipeSelection? Selection = null, string? EntityName = null)
{
    public string Label => Contract.Contains("PointSet", StringComparison.Ordinal) ? ThreeDLocalization.Shared.Resolve("ThreeD.Flow.Points", "선택 점", "Selected points")
        : Contract.Contains("HeightField", StringComparison.Ordinal) ? ThreeDLocalization.Shared.Resolve("ThreeD.Flow.Height", "높이 맵", "Height map")
        : Contract.Contains("GridRectangle", StringComparison.Ordinal) ? ContractDisplay
        : Contract.Contains("LineFeature", StringComparison.Ordinal) ? ThreeDLocalization.Shared.Resolve("ThreeD.Flow.Line", "직선", "Line") + $" {Index + 1}"
        : ThreeDLocalization.Shared.Resolve("ThreeD.Flow.Input", "입력", "Input") + $" {Index + 1}";
    public string ContractDisplay => ThreeDLocalization.Shared.ContractLabel(Contract);
    public bool HasContractDetail => !string.Equals(Label, ContractDisplay, StringComparison.Ordinal);
    public string EntityDisplayName => string.IsNullOrWhiteSpace(EntityName) ? EntityId : EntityName;
    public bool IsConnected => !string.IsNullOrWhiteSpace(EntityId) && EntityName is not null;
    public bool IsRegionInput => Contract.Contains("GridRectangle", StringComparison.Ordinal) || Contract.Contains("RegionArtifact", StringComparison.Ordinal);
    public bool HasMapPreview => HasPreview && !IsRegionInput;
    public bool HasPreview => PreviewFrame is not null;
    public bool HasSelectionPreview => PreviewFrame is not null && Selection?.GridRectangle is not null;
    public double SelectionPreviewLeft => Selection?.GridRectangle is { } roi ? 64d * roi.Column / Selection.SourceBinding.GridWidth : 0;
    public double SelectionPreviewTop => Selection?.GridRectangle is { } roi ? 42d * roi.Row / Selection.SourceBinding.GridHeight : 0;
    public double SelectionPreviewWidth => Selection?.GridRectangle is { } roi ? 64d * roi.ColumnCount / Selection.SourceBinding.GridWidth : 0;
    public double SelectionPreviewHeight => Selection?.GridRectangle is { } roi ? 42d * roi.RowCount / Selection.SourceBinding.GridHeight : 0;
    public string AccessibleName => $"{Step.ToolName}. {Label}. {ContractDisplay}. {EntityDisplayName}. {EntityId}";
}

public sealed class ToolWorkbenchPipelineStepItem : INotifyPropertyChanged
{
    private string id;
    private string toolName;
    private string inputEntityIdsText;
    private string outputEntityId;
    private string resultValueSummary = string.Empty;
    private string resultStatusSummary = string.Empty;
    private bool resultIsCurrent;
    private bool resultIsFailure;
    private bool? acceptanceLimitsConfigured;
    private ToolWorkbenchPipelineStepItem? flowRepeatGroupAnchor;
    private int flowRepeatGroupCount;
    private bool isFlowRepeatGroupLast;
    private string flowRepeatGroupOrderRange = string.Empty;
    private string order = "00";
    private string state = "Taught / pending";
    private string inputPortState = string.Empty;
    private string inputPortDetail = string.Empty;
    private bool inputPortHasIssue;
    private string outputPortState = string.Empty;
    private string outputPortDetail = string.Empty;
    private bool outputPortHasIssue;
    private ToolRecipeDualRoiRouting? dualRoiRouting;
    private ToolRecipeSignedDeltaSidecarReference? signedDeltaSidecar;
    private bool outputEnabled = true;
    private IReadOnlyDictionary<string, C3DHeightImageDisplayFrame> flowPreviews = new Dictionary<string, C3DHeightImageDisplayFrame>();
    private IReadOnlyDictionary<string, ToolRecipeSelection> flowSelections = new Dictionary<string, ToolRecipeSelection>();
    private IReadOnlyDictionary<string, string> flowEntityNames = new Dictionary<string, string>();

    public ToolWorkbenchPipelineStepItem(
        string id,
        ToolWorkbenchToolItem tool,
        string inputEntityIdsText,
        string outputEntityId,
        IReadOnlyList<ToolRecipeParameter>? parameters = null,
        string? toolName = null,
        ToolRecipeDualRoiRouting? dualRoiRouting = null,
        bool outputEnabled = true,
        ToolRecipeSignedDeltaSidecarReference? signedDeltaSidecar = null,
        bool? acceptanceLimitsConfigured = null,
        ToolRecipeStepGroup? group = null)
    {
        this.id = id;
        Tool = tool;
        this.toolName = string.IsNullOrWhiteSpace(toolName) ? tool.Name : toolName.Trim();
        this.inputEntityIdsText = inputEntityIdsText;
        this.outputEntityId = outputEntityId;
        this.dualRoiRouting = dualRoiRouting;
        this.signedDeltaSidecar = signedDeltaSidecar;
        this.outputEnabled = outputEnabled;
        this.acceptanceLimitsConfigured = acceptanceLimitsConfigured;
        Group = group;
        Parameters = new ObservableCollection<ToolWorkbenchParameterItem>(
            parameters is null
                ? tool.Parameters.Select(parameter => new ToolWorkbenchParameterItem(parameter.Name, parameter.DefaultValue))
                : parameters.Select(parameter => new ToolWorkbenchParameterItem(parameter.Name, parameter.Value)));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ToolWorkbenchToolItem Tool { get; }
    public ToolRecipeStepGroup? Group { get; }
    public string ToolId => Tool.Id;
    public string ToolName
    {
        get => toolName;
        set
        {
            if (SetField(ref toolName, string.IsNullOrWhiteSpace(value) ? Tool.Name : value.Trim())) OnPropertyChanged(nameof(HasLongFlowName));
        }
    }
    public bool HasLongFlowName => ToolName.Length > 24;
    public int MinimumInputCount => Tool.MinimumInputCount;
    public string InputContract => Tool.InputContract;
    public string OutputContract => Tool.OutputContract;
    public string InputContractDisplay => Tool.InputContractDisplay;
    public string OutputContractDisplay => Tool.OutputContractDisplay;
    public ObservableCollection<ToolWorkbenchParameterItem> Parameters { get; }

    public string Id { get => id; set => SetField(ref id, value ?? string.Empty); }
    public string Order { get => order; internal set => SetField(ref order, value); }
    public string InputEntityIdsText
    {
        get => inputEntityIdsText;
        set
        {
            if (!SetField(ref inputEntityIdsText, value ?? string.Empty)) return;
            OnPropertyChanged(nameof(InputEntityIds));
            OnPropertyChanged(nameof(InputSummary));
            OnPropertyChanged(nameof(FlowInputs));
            OnPropertyChanged(nameof(PrimaryFlowInput));
            OnPropertyChanged(nameof(CollapsedInputSummary));
            OnPropertyChanged(nameof(SecondaryFlowInputs));
            OnPropertyChanged(nameof(CollapsedPrimaryInputLabel));
        }
    }

    public IReadOnlyList<string> InputEntityIds => inputEntityIdsText
        .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .ToArray();
    public IReadOnlyList<ToolWorkbenchFlowInputItem> FlowInputs
    {
        get
        {
            var contracts = InputContract.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            var inputs = InputEntityIds;
            return Enumerable.Range(0, Math.Max(contracts.Length, Math.Max(inputs.Count, MinimumInputCount)))
                .Select(index => new ToolWorkbenchFlowInputItem(this, index, index < contracts.Length ? contracts[index] : InputContract,
                    index < inputs.Count ? inputs[index] : string.Empty,
                    index < inputs.Count ? flowPreviews.GetValueOrDefault(inputs[index]) : null,
                    index < inputs.Count ? flowSelections.GetValueOrDefault(inputs[index]) : null,
                    index < inputs.Count ? flowEntityNames.GetValueOrDefault(inputs[index]) : null))
                .ToArray();
        }
    }

    public ToolWorkbenchFlowInputItem? PrimaryFlowInput => FlowInputs.FirstOrDefault();
    public string CollapsedInputSummary
    {
        get
        {
            var inputs = FlowInputs;
            if (inputs.Count <= 1) return inputs.FirstOrDefault()?.ContractDisplay ?? InputContractDisplay;
            var label = inputs[0].Contract.Contains("HeightField", StringComparison.Ordinal)
                ? ThreeDLocalization.Shared.Resolve("ThreeD.Flow.HeightMap", "높이 맵", "Height map") : inputs[0].Label;
            return $"{label} +{inputs.Count - 1}";
        }
    }
    public IReadOnlyList<ToolWorkbenchFlowInputItem> SecondaryFlowInputs => FlowInputs.Skip(1).ToArray();
    public string CollapsedPrimaryInputLabel => PrimaryFlowInput?.Label ?? InputContractDisplay;
    public string CollapsedOutputSummary => !string.IsNullOrWhiteSpace(ResultValueSummary) ? ResultValueSummary
        : OutputContract == "MeasurementResult" ? "—" : OutputContractDisplay;
    public ToolWorkbenchPipelineStepItem? FlowRepeatGroupAnchor => flowRepeatGroupAnchor;
    public bool IsFlowRepeatGroupMember => flowRepeatGroupAnchor is not null;
    public bool IsFlowRepeatGroupLead => ReferenceEquals(flowRepeatGroupAnchor, this);
    public bool IsFlowRepeatGroupLast => isFlowRepeatGroupLast;
    public int FlowRepeatGroupCount => flowRepeatGroupCount;
    public string FlowRepeatGroupOrderRange => flowRepeatGroupOrderRange;
    public string FlowRepeatGroupLabel => $"{ThreeDLocalization.Shared.Resolve("ThreeD.Flow.Thickness", "두께", "Thickness")} ×{flowRepeatGroupCount}";

    internal void UpdateFlowRepeatGroup(ToolWorkbenchPipelineStepItem? anchor, int count, bool last, string orderRange)
    {
        if (ReferenceEquals(flowRepeatGroupAnchor, anchor) && flowRepeatGroupCount == count
            && isFlowRepeatGroupLast == last && flowRepeatGroupOrderRange == orderRange) return;
        flowRepeatGroupAnchor = anchor;
        flowRepeatGroupCount = count;
        isFlowRepeatGroupLast = last;
        flowRepeatGroupOrderRange = orderRange;
        OnPropertyChanged(nameof(FlowRepeatGroupAnchor));
        OnPropertyChanged(nameof(IsFlowRepeatGroupMember));
        OnPropertyChanged(nameof(IsFlowRepeatGroupLead));
        OnPropertyChanged(nameof(IsFlowRepeatGroupLast));
        OnPropertyChanged(nameof(FlowRepeatGroupCount));
        OnPropertyChanged(nameof(FlowRepeatGroupOrderRange));
        OnPropertyChanged(nameof(FlowRepeatGroupLabel));
    }
    public C3DHeightImageDisplayFrame? OutputPreviewFrame => flowPreviews.GetValueOrDefault(OutputEntityId);
    public bool HasOutputPreview => OutputPreviewFrame is not null;

    internal void UpdateFlowPreviews(
        IReadOnlyDictionary<string, C3DHeightImageDisplayFrame> previews,
        IReadOnlyDictionary<string, ToolRecipeSelection> selections,
        IReadOnlyDictionary<string, string> entityNames)
    {
        if (flowPreviews.Count == previews.Count && flowPreviews.All(pair => ReferenceEquals(pair.Value, previews.GetValueOrDefault(pair.Key)))
            && flowSelections.Count == selections.Count && flowSelections.All(pair => Equals(pair.Value, selections.GetValueOrDefault(pair.Key)))
            && flowEntityNames.Count == entityNames.Count && flowEntityNames.All(pair => pair.Value == entityNames.GetValueOrDefault(pair.Key))) return;
        flowPreviews = previews;
        flowSelections = selections;
        flowEntityNames = entityNames;
        OnPropertyChanged(nameof(FlowInputs));
        OnPropertyChanged(nameof(PrimaryFlowInput));
        OnPropertyChanged(nameof(CollapsedInputSummary));
        OnPropertyChanged(nameof(SecondaryFlowInputs));
        OnPropertyChanged(nameof(CollapsedPrimaryInputLabel));
        OnPropertyChanged(nameof(OutputPreviewFrame));
        OnPropertyChanged(nameof(HasOutputPreview));
    }

    public string InputSummary => string.IsNullOrWhiteSpace(InputEntityIdsText)
        ? ThreeDLocalization.Shared.FlowPortNoInputDetail
        : InputEntityIdsText;
    public string OutputEntityId { get => outputEntityId; set => SetField(ref outputEntityId, value ?? string.Empty); }
    public bool OutputEnabled
    {
        get => outputEnabled;
        set
        {
            if (outputEnabled == value) return;
            outputEnabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OutputEnabled)));
            RaiseCanonicalStatePresentationChanged();
        }
    }
    public ToolRecipeDualRoiRouting? DualRoiRouting
    {
        get => dualRoiRouting;
        set
        {
            if (Equals(dualRoiRouting, value)) return;
            dualRoiRouting = value;
            OnPropertyChanged();
        }
    }
    public ToolRecipeSignedDeltaSidecarReference? SignedDeltaSidecar
    {
        get => signedDeltaSidecar;
        internal set
        {
            if (Equals(signedDeltaSidecar, value)) return;
            signedDeltaSidecar = value;
            OnPropertyChanged();
        }
    }
    public bool? AcceptanceLimitsConfigured
    {
        get => acceptanceLimitsConfigured;
        internal set
        {
            if (acceptanceLimitsConfigured == value) return;
            acceptanceLimitsConfigured = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasConfiguredAcceptanceLimits));
            OnPropertyChanged(nameof(HasUnconfiguredAcceptanceLimits));
        }
    }
    public bool HasConfiguredAcceptanceLimits => ToolRecipeStep.RequiresAcceptanceLimits(ToolId) && AcceptanceLimitsConfigured != false;
    public bool HasUnconfiguredAcceptanceLimits => ToolRecipeStep.RequiresAcceptanceLimits(ToolId) && AcceptanceLimitsConfigured == false;
    public string State
    {
        get => state;
        internal set
        {
            if (!SetField(ref state, value)) return;
            RaiseCanonicalStatePresentationChanged();
        }
    }
    public InspectionStepState CanonicalState =>
        OutputEnabled
            ? InspectionStepStateMatrix.Classify(State)
            : InspectionStepState.Incomplete;
    public string CanonicalStateKey =>
        OutputEnabled ? InspectionStepStateMatrix.Describe(State).Key : "disabled";
    public string CanonicalStateLabel =>
        OutputEnabled
            ? ThreeDLocalization.Shared.StateLabel(CanonicalState)
            : ThreeDLocalization.Shared.OutputDisabled;
    public string CanonicalStateAccessibleName =>
        $"{CanonicalStateLabel} ({CanonicalStateKey})";
    public string ResultValueSummary
    {
        get => resultValueSummary;
        internal set => SetField(ref resultValueSummary, value);
    }
    internal void UpdateResultPresentation(string value, string status, bool isCurrent, bool isFailure)
    {
        ResultValueSummary = value;
        resultStatusSummary = status;
        resultIsCurrent = isCurrent;
        resultIsFailure = isFailure;
        OnPropertyChanged(nameof(CollapsedOutputSummary));
        OnPropertyChanged(nameof(FlowNodeStatusLabel));
        OnPropertyChanged(nameof(FlowNodeStatusHasIssue));
        OnPropertyChanged(nameof(FlowNodeStatusIsFailure));
    }

    public string FlowNodeStatusLabel
    {
        get
        {
            if (!OutputEnabled || CanonicalState is InspectionStepState.Running or InspectionStepState.Error or InspectionStepState.Fail)
            {
                return CanonicalStateLabel;
            }

            if (InputPortHasIssue)
            {
                return InputPortState;
            }

            if (OutputPortHasIssue)
            {
                return OutputPortState;
            }

            if (!string.IsNullOrEmpty(resultStatusSummary)) return resultStatusSummary;

            if (OutputPortState == ThreeDLocalization.Shared.FlowPortDeclared
                && CanonicalState is InspectionStepState.Empty or InspectionStepState.Ready)
            {
                return ThreeDLocalization.Shared.ResultStatusLabel(ResultStatus.NotRun);
            }

            return CanonicalStateLabel;
        }
    }
    public bool FlowNodeStatusHasIssue =>
        InputPortHasIssue
        || OutputPortHasIssue
        || !string.IsNullOrEmpty(resultStatusSummary) && !resultIsCurrent
        || CanonicalState is InspectionStepState.Incomplete or InspectionStepState.Stale or InspectionStepState.Error;
    public bool FlowNodeStatusIsFailure =>
        resultIsFailure || CanonicalState is InspectionStepState.Error or InspectionStepState.Fail;
    public string InputPortState => inputPortState;
    public string InputPortDetail => inputPortDetail;
    public bool InputPortHasIssue => inputPortHasIssue;
    public string OutputPortState => outputPortState;
    public string OutputPortDetail => outputPortDetail;
    public bool OutputPortHasIssue => outputPortHasIssue;

    internal void UpdateFlowPortPresentation(
        string newInputPortState,
        string newInputPortDetail,
        bool newInputPortHasIssue,
        string newOutputPortState,
        string newOutputPortDetail,
        bool newOutputPortHasIssue)
    {
        SetField(ref inputPortState, newInputPortState, nameof(InputPortState));
        SetField(ref inputPortDetail, newInputPortDetail, nameof(InputPortDetail));
        SetField(ref inputPortHasIssue, newInputPortHasIssue, nameof(InputPortHasIssue));
        SetField(ref outputPortState, newOutputPortState, nameof(OutputPortState));
        SetField(ref outputPortDetail, newOutputPortDetail, nameof(OutputPortDetail));
        SetField(ref outputPortHasIssue, newOutputPortHasIssue, nameof(OutputPortHasIssue));
        OnPropertyChanged(nameof(CollapsedOutputSummary));
        OnPropertyChanged(nameof(FlowNodeStatusLabel));
        OnPropertyChanged(nameof(FlowNodeStatusHasIssue));
        OnPropertyChanged(nameof(FlowNodeStatusIsFailure));
    }

    private bool SetField(ref string field, string value, [CallerMemberName] string? propertyName = null)
    {
        if (field == value) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    private bool SetField(ref bool field, bool value, [CallerMemberName] string? propertyName = null)
    {
        if (field == value) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private void RaiseCanonicalStatePresentationChanged()
    {
        OnPropertyChanged(nameof(CanonicalState));
        OnPropertyChanged(nameof(CanonicalStateKey));
        OnPropertyChanged(nameof(CanonicalStateLabel));
        OnPropertyChanged(nameof(CanonicalStateAccessibleName));
        OnPropertyChanged(nameof(CollapsedOutputSummary));
        OnPropertyChanged(nameof(FlowNodeStatusLabel));
        OnPropertyChanged(nameof(FlowNodeStatusHasIssue));
        OnPropertyChanged(nameof(FlowNodeStatusIsFailure));
    }

    internal void RefreshLocalizedStatePresentation()
    {
        OnPropertyChanged(nameof(InputContractDisplay));
        OnPropertyChanged(nameof(FlowRepeatGroupLabel));
        OnPropertyChanged(nameof(OutputContractDisplay));
        OnPropertyChanged(nameof(FlowInputs));
        OnPropertyChanged(nameof(PrimaryFlowInput));
        OnPropertyChanged(nameof(CollapsedInputSummary));
        OnPropertyChanged(nameof(SecondaryFlowInputs));
        OnPropertyChanged(nameof(CollapsedPrimaryInputLabel));
        OnPropertyChanged(nameof(InputSummary));
        OnPropertyChanged(nameof(CanonicalStateLabel));
        OnPropertyChanged(nameof(CanonicalStateAccessibleName));
        OnPropertyChanged(nameof(CollapsedOutputSummary));
        OnPropertyChanged(nameof(FlowNodeStatusLabel));
    }
}

public sealed class ToolWorkbenchParameterItem : INotifyPropertyChanged
{
    private string value;

    public ToolWorkbenchParameterItem(string name, string value)
    {
        Name = name;
        this.value = value;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public string Name { get; }
    public string Value
    {
        get => value;
        set
        {
            var normalized = value ?? string.Empty;
            if (this.value == normalized) return;
            this.value = normalized;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
        }
    }
}

public sealed record ToolWorkbenchEntityItem(string Id, string Kind, string State, string Detail);

public sealed record ToolWorkbenchValidationItem(string Level, string Message);

public sealed record ToolWorkbenchC3DSourceStatePerformance(
    double CaptureMilliseconds,
    double ClearPreviewMilliseconds,
    double IdentityMilliseconds,
    double RecipeStateMilliseconds,
    double SelectionSyncMilliseconds,
    double LoggingMilliseconds,
    double TotalMilliseconds);

public sealed record TeachingMeasurementResultItem(
    ToolWorkbenchPipelineStepItem Step,
    string Status,
    string Value,
    string Unit,
    string Limits,
    string Reason,
    bool IsCurrent,
    IReadOnlyList<Metric> Metrics)
{
    public TeachingResultLabelItem ResultLabel { get; init; } = new();
    public int Sequence { get; init; }
    public bool IsSelectedTool { get; init; }
    public string OutputLabel { get; init; } = string.Empty;
    public DateTimeOffset? RecordedAt { get; init; }
    public ResultStatus? ExecutionStatus { get; init; }
    public ToolWorkbenchDisplayedOutputItem? DisplayedOutput { get; init; }
    public string SequenceText => Sequence > 0 ? Sequence.ToString("00", CultureInfo.InvariantCulture) : string.Empty;
    private Metric? PrimaryMetric => ToolWorkbenchViewModel.SelectPrimaryMeasurementMetric(Step.ToolId, Metrics);
    private Metric? Lower => Metrics.FirstOrDefault(metric => metric.Name == "LowerLimit" && double.IsFinite(metric.Value));
    private Metric? Upper => Metrics.FirstOrDefault(metric => metric.Name == "UpperLimit" && double.IsFinite(metric.Value));
    public string OutputName => PrimaryMetric is { } metric ? Step.ToolId == "thickness" && metric.Name == "Mean"
        ? ThreeDLocalization.Shared.Resolve("ThreeD.Results.ThicknessOutput", "두께", "Thickness") : metric.Name : !string.IsNullOrWhiteSpace(OutputLabel) ? OutputLabel : "—";
    public string DisplayValue => State is "Error" or "오류" ? "—" : PrimaryMetric is { } metric
        ? double.IsFinite(metric.Value) ? metric.Value.ToString(metric.Kind == MetricKind.Count ? "N0" : "0.##", CultureInfo.InvariantCulture) : "—"
        : double.TryParse(Value.Contains(':') ? Value[(Value.IndexOf(':') + 1)..] : Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value)
            ? value.ToString("0.##", CultureInfo.InvariantCulture) : Value;
    public string DisplayLimits => Lower is { } lower && Upper is { } upper && lower.Unit == upper.Unit && lower.Unit == Unit
        ? $"{lower.Value.ToString("F2", CultureInfo.InvariantCulture)} – {upper.Value.ToString("F2", CultureInfo.InvariantCulture)}" : Limits;
    public string ValidSamples => Metrics.FirstOrDefault(metric => metric.Name == "ValidSampleCount" && double.IsFinite(metric.Value)) is { } metric
        ? metric.Value.ToString("N0", CultureInfo.InvariantCulture) : "—";
    public string RunTime => RecordedAt?.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture) ?? "—";
    public string State => !IsCurrent ? "Previous" : ExecutionStatus?.ToString() ?? Status;
    public string StatusText => State switch
    {
        "Pass" or "통과" => "OK",
        "Fail" or "실패" => "NG",
        "Error" => ThreeDLocalization.Shared.Resolve("ThreeD.Results.Error", "오류", "Error"),
        "Previous" => ThreeDLocalization.Shared.Resolve("ThreeD.Results.Previous", "이전 결과", "Previous"),
        _ => Status
    };
    public string StatusIcon => State switch { "Pass" or "통과" => "✓", "Fail" or "실패" => "×", "Error" => "!", "Previous" => "↻", _ => "–" };
    public bool HasMarginScale => IsCurrent && PrimaryMetric is { } metric && double.IsFinite(metric.Value)
        && Lower is { } lower && Upper is { } upper && lower.Unit == metric.Unit && upper.Unit == metric.Unit
        && upper.Value > lower.Value && double.IsFinite(upper.Value - lower.Value) && State is not ("Error" or "오류");
    // Screen position on the existing 110px tolerance scale; this never judges a result or computes a new measurement.
    public double MarginMarkerOffset => HasMarginScale
        ? Math.Clamp(((PrimaryMetric!.Value - Lower!.Value) / (Upper!.Value - Lower.Value) + .6) / 2.2, 0, 1) * 110 - 1 : 0;
}

public sealed class TeachingResultLabelItem : INotifyPropertyChanged
{
    private ViewerTeachingResultLabel? snapshot;
    private bool hidden;
    public event PropertyChangedEventHandler? PropertyChanged;
    public ViewerTeachingResultLabel? Snapshot => snapshot;
    public bool CanShow => snapshot is { IsValid: true };
    public bool IsVisible => CanShow && !hidden;

    internal void Update(ViewerTeachingResultLabel? value, bool isHidden)
    {
        snapshot = value;
        hidden = isHidden;
        PropertyChanged?.Invoke(this, new(nameof(Snapshot)));
        PropertyChanged?.Invoke(this, new(nameof(CanShow)));
        PropertyChanged?.Invoke(this, new(nameof(IsVisible)));
    }
}

public sealed record TeachingResultSummaryItem(string State, string StatusText, string StatusIcon, int Count);
