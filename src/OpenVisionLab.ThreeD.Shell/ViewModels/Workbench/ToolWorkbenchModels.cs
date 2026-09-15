using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Shell;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

public sealed record ToolWorkbenchStepRemovalRequestEventArgs(
    string StepId,
    string StepName,
    IReadOnlyList<string> OrphanedSelectionNames);

public sealed record ToolWorkbenchToolItem(
    string Category,
    string Name,
    string Id,
    int MinimumInputCount,
    string InputContract,
    string OutputContract,
    string Description,
    IReadOnlyList<ToolWorkbenchParameterSeed> Parameters);

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

public sealed class ToolWorkbenchPipelineStepItem : INotifyPropertyChanged
{
    private string id;
    private string toolName;
    private string inputEntityIdsText;
    private string outputEntityId;
    private string order = "00";
    private string state = "Taught / pending";
    private string inputPortState = string.Empty;
    private string inputPortDetail = string.Empty;
    private bool inputPortHasIssue;
    private string outputPortState = string.Empty;
    private string outputPortDetail = string.Empty;
    private bool outputPortHasIssue;
    private ToolRecipeDualRoiRouting? dualRoiRouting;
    private bool outputEnabled = true;

    public ToolWorkbenchPipelineStepItem(
        string id,
        ToolWorkbenchToolItem tool,
        string inputEntityIdsText,
        string outputEntityId,
        IReadOnlyList<ToolRecipeParameter>? parameters = null,
        string? toolName = null,
        ToolRecipeDualRoiRouting? dualRoiRouting = null,
        bool outputEnabled = true)
    {
        this.id = id;
        Tool = tool;
        this.toolName = string.IsNullOrWhiteSpace(toolName) ? tool.Name : toolName.Trim();
        this.inputEntityIdsText = inputEntityIdsText;
        this.outputEntityId = outputEntityId;
        this.dualRoiRouting = dualRoiRouting;
        this.outputEnabled = outputEnabled;
        Parameters = new ObservableCollection<ToolWorkbenchParameterItem>(
            parameters is null
                ? tool.Parameters.Select(parameter => new ToolWorkbenchParameterItem(parameter.Name, parameter.DefaultValue))
                : parameters.Select(parameter => new ToolWorkbenchParameterItem(parameter.Name, parameter.Value)));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ToolWorkbenchToolItem Tool { get; }
    public string ToolId => Tool.Id;
    public string ToolName
    {
        get => toolName;
        set => SetField(ref toolName, string.IsNullOrWhiteSpace(value) ? Tool.Name : value.Trim());
    }
    public int MinimumInputCount => Tool.MinimumInputCount;
    public string InputContract => Tool.InputContract;
    public string OutputContract => Tool.OutputContract;
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
        }
    }

    public IReadOnlyList<string> InputEntityIds => inputEntityIdsText
        .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .ToArray();
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
    }

    internal void RefreshLocalizedStatePresentation()
    {
        OnPropertyChanged(nameof(InputSummary));
        OnPropertyChanged(nameof(CanonicalStateLabel));
        OnPropertyChanged(nameof(CanonicalStateAccessibleName));
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
