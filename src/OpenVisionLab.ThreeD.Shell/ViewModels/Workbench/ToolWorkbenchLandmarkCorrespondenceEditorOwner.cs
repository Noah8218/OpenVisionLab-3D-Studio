namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using OpenVisionLab.ThreeD.Core;

/// <summary>
/// Owns landmark-correspondence editor state and explicit row commits.
/// It creates recipe selections only through the injected teaching-store seam.
/// </summary>
internal sealed class ToolWorkbenchLandmarkCorrespondenceEditorOwner
    : INotifyPropertyChanged
{
    private readonly Func<ToolWorkbenchLandmarkCorrespondenceEditorContext> getContext;
    private readonly Func<
        ToolWorkbenchPipelineStepItem,
        ToolWorkbenchTeachingSelectionRequirement,
        string> createSelectionId;
    private readonly Action<ToolRecipeSelection> persistSelection;
    private readonly Action<ToolRecipeSelection> removeSelection;
    private readonly Action notifyAppliedSelectionsChanged;
    private readonly Action<string, string> appendLog;
    private readonly RelayCommand addOrUpdateRowCommand;
    private readonly RelayCommand removeSelectedRowCommand;
    private ToolRecipeLandmarkCorrespondence? selectedRow;
    private string sourceEntityId = string.Empty;
    private string referenceLandmarkId = "fixture.landmark.01";
    private double referenceX;
    private double referenceY;
    private double referenceZ;
    private string referenceFrameId = "frame.fixture";
    private string referenceUnit = string.Empty;
    private string referenceProvenance = string.Empty;
    private string referenceRevision = string.Empty;
    private double minimumNormalizedTetrahedronVolume;

    public ToolWorkbenchLandmarkCorrespondenceEditorOwner(
        Func<ToolWorkbenchLandmarkCorrespondenceEditorContext> getContext,
        Func<
            ToolWorkbenchPipelineStepItem,
            ToolWorkbenchTeachingSelectionRequirement,
            string> createSelectionId,
        Action<ToolRecipeSelection> persistSelection,
        Action<ToolRecipeSelection> removeSelection,
        Action notifyAppliedSelectionsChanged,
        Action<string, string> appendLog)
    {
        this.getContext = getContext
            ?? throw new ArgumentNullException(nameof(getContext));
        this.createSelectionId = createSelectionId
            ?? throw new ArgumentNullException(nameof(createSelectionId));
        this.persistSelection = persistSelection
            ?? throw new ArgumentNullException(nameof(persistSelection));
        this.removeSelection = removeSelection
            ?? throw new ArgumentNullException(nameof(removeSelection));
        this.notifyAppliedSelectionsChanged = notifyAppliedSelectionsChanged
            ?? throw new ArgumentNullException(nameof(notifyAppliedSelectionsChanged));
        this.appendLog = appendLog ?? throw new ArgumentNullException(nameof(appendLog));

        addOrUpdateRowCommand = new RelayCommand(
            _ => AddOrUpdateRow(),
            _ => CanEditRows);
        removeSelectedRowCommand = new RelayCommand(
            _ => RemoveSelectedRow(),
            _ => SelectedRow is not null && getContext().IsCorrespondenceStep);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<ToolRecipeLandmarkCorrespondence> Rows { get; } = [];

    public ObservableCollection<string> AvailableSourceEntityIds { get; } = [];

    public RelayCommand AddOrUpdateRowCommand => addOrUpdateRowCommand;

    public RelayCommand RemoveSelectedRowCommand => removeSelectedRowCommand;

    public ToolRecipeLandmarkCorrespondence? SelectedRow
    {
        get => selectedRow;
        set
        {
            if (Equals(selectedRow, value))
            {
                return;
            }

            selectedRow = value;
            OnPropertyChanged();
            if (value is not null)
            {
                SourceEntityId = value.SourceEntityId;
                ReferenceLandmarkId = value.ReferenceLandmarkId;
                ReferenceX = value.ReferencePosition.X;
                ReferenceY = value.ReferencePosition.Y;
                ReferenceZ = value.ReferencePosition.Z;
                ReferenceFrameId = value.ReferenceFrameId;
            }

            OnPropertyChanged(nameof(CommitActionText));
            removeSelectedRowCommand.RaiseCanExecuteChanged();
        }
    }

    public string SourceEntityId
    {
        get => sourceEntityId;
        set
        {
            if (SetField(ref sourceEntityId, value ?? string.Empty))
            {
                addOrUpdateRowCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string ReferenceLandmarkId
    {
        get => referenceLandmarkId;
        set
        {
            if (SetField(ref referenceLandmarkId, value ?? string.Empty))
            {
                addOrUpdateRowCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public double ReferenceX
    {
        get => referenceX;
        set => SetField(ref referenceX, value);
    }

    public double ReferenceY
    {
        get => referenceY;
        set => SetField(ref referenceY, value);
    }

    public double ReferenceZ
    {
        get => referenceZ;
        set => SetField(ref referenceZ, value);
    }

    public string ReferenceFrameId
    {
        get => referenceFrameId;
        set
        {
            if (SetField(ref referenceFrameId, value ?? string.Empty))
            {
                addOrUpdateRowCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string ReferenceUnit
    {
        get => referenceUnit;
        set
        {
            if (SetField(ref referenceUnit, value ?? string.Empty))
            {
                addOrUpdateRowCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string ReferenceProvenance
    {
        get => referenceProvenance;
        set
        {
            if (SetField(ref referenceProvenance, value ?? string.Empty))
            {
                addOrUpdateRowCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string ReferenceRevision
    {
        get => referenceRevision;
        set
        {
            if (SetField(ref referenceRevision, value ?? string.Empty))
            {
                addOrUpdateRowCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public double MinimumNormalizedTetrahedronVolume
    {
        get => minimumNormalizedTetrahedronVolume;
        set
        {
            if (SetField(ref minimumNormalizedTetrahedronVolume, value))
            {
                addOrUpdateRowCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string CommitActionText => SelectedRow is null ? "Add row" : "Update row";

    public string SelectionSummary => Rows.Count switch
    {
        0 => "No correspondence rows. Teach exactly four Published CornerAnchor/reference mappings before Preview.",
        < 4 => $"{Rows.Count}/4 rows taught. Correspondence Preview remains blocked.",
        > 4 => $"{Rows.Count}/4 rows taught. v1 accepts exactly four; remove the extra rows.",
        _ when string.IsNullOrWhiteSpace(ReferenceUnit)
            || string.IsNullOrWhiteSpace(ReferenceProvenance)
            || string.IsNullOrWhiteSpace(ReferenceRevision)
            || !double.IsFinite(MinimumNormalizedTetrahedronVolume)
            || MinimumNormalizedTetrahedronVolume <= 0
            || MinimumNormalizedTetrahedronVolume >= 1 =>
                "Four rows exist, but reference unit/provenance/revision and a normalized tetrahedron-volume threshold are required.",
        _ => "Four correspondence rows and reference descriptor are taught. Preview validates only current Published anchors; no affine matrix is calculated."
    };

    public void Refresh()
    {
        var context = getContext();
        Rows.Clear();
        foreach (var row in context.Selection?.Rows ?? [])
        {
            Rows.Add(row);
        }

        if (SelectedRow is not null && !Rows.Contains(SelectedRow))
        {
            SelectedRow = null;
        }

        AvailableSourceEntityIds.Clear();
        if (context.SelectedStep is not null)
        {
            foreach (var step in context.PipelineSteps)
            {
                if (ReferenceEquals(step, context.SelectedStep))
                {
                    break;
                }

                if (string.Equals(
                    step.ToolId,
                    "line-intersection",
                    StringComparison.Ordinal))
                {
                    AvailableSourceEntityIds.Add(step.OutputEntityId);
                }
            }
        }

        if (context.Selection?.CorrespondenceDescriptor is { } descriptor)
        {
            ReferenceFrameId = descriptor.ReferenceFrameId;
            ReferenceUnit = descriptor.ReferenceUnit;
            ReferenceProvenance = descriptor.ReferenceProvenance;
            ReferenceRevision = descriptor.ReferenceRevision;
            MinimumNormalizedTetrahedronVolume =
                descriptor.MinimumNormalizedTetrahedronVolume ?? 0;
        }

        if (string.IsNullOrWhiteSpace(SourceEntityId)
            || !AvailableSourceEntityIds.Contains(
                SourceEntityId,
                StringComparer.OrdinalIgnoreCase))
        {
            SourceEntityId = AvailableSourceEntityIds.FirstOrDefault() ?? string.Empty;
        }

        OnPropertyChanged(nameof(SelectionSummary));
        RefreshCommandStates();
    }

    public void RefreshCommandStates()
    {
        addOrUpdateRowCommand.RaiseCanExecuteChanged();
        removeSelectedRowCommand.RaiseCanExecuteChanged();
    }

    private bool CanEditRows
    {
        get
        {
            var context = getContext();
            return context.IsCorrespondenceStep
                && context.SourceBinding is not null
                && !string.IsNullOrWhiteSpace(SourceEntityId)
                && !string.IsNullOrWhiteSpace(ReferenceLandmarkId)
                && !string.IsNullOrWhiteSpace(ReferenceFrameId)
                && !string.IsNullOrWhiteSpace(ReferenceUnit)
                && !string.IsNullOrWhiteSpace(ReferenceProvenance)
                && !string.IsNullOrWhiteSpace(ReferenceRevision)
                && double.IsFinite(MinimumNormalizedTetrahedronVolume)
                && MinimumNormalizedTetrahedronVolume > 0
                && MinimumNormalizedTetrahedronVolume < 1
                && double.IsFinite(ReferenceX)
                && double.IsFinite(ReferenceY)
                && double.IsFinite(ReferenceZ);
        }
    }

    private void AddOrUpdateRow()
    {
        var context = getContext();
        if (!CanEditRows
            || context.SelectedStep is null
            || context.SourceBinding is null
            || context.Requirement is null)
        {
            return;
        }

        var row = new ToolRecipeLandmarkCorrespondence(
            SourceEntityId.Trim(),
            ReferenceLandmarkId.Trim(),
            new ToolRecipeXyz(ReferenceX, ReferenceY, ReferenceZ),
            ReferenceFrameId.Trim());
        var rows = context.Selection?.Rows?.ToList() ?? [];
        if (SelectedRow is { } selected)
        {
            var index = rows.FindIndex(item => Equals(item, selected));
            if (index >= 0)
            {
                rows[index] = row;
            }
            else
            {
                rows.Add(row);
            }
        }
        else
        {
            rows.Add(row);
        }

        var descriptor = new ToolRecipeLandmarkCorrespondenceDescriptor(
            ReferenceFrameId.Trim(),
            ReferenceUnit.Trim(),
            ReferenceProvenance.Trim(),
            ReferenceRevision.Trim(),
            "ExactlyFour",
            "CurrentPublishedCornerAnchor",
            "RequireNonDegenerateTetrahedra",
            MinimumNormalizedTetrahedronVolume);
        var selection = new ToolRecipeSelection(
            context.Selection?.Id
                ?? createSelectionId(context.SelectedStep, context.Requirement),
            context.Selection?.Name
                ?? $"{context.SelectedStep.ToolName} correspondences",
            ToolRecipeSelectionKinds.LandmarkCorrespondenceSet,
            context.RootSourceId,
            context.SourceFrameId,
            context.SourceBinding,
            null,
            null,
            rows,
            descriptor);
        persistSelection(selection);
        SelectedRow = null;
        ResetEditor();
        notifyAppliedSelectionsChanged();
        appendLog(
            "Teach",
            $"Correspondence row authored for {context.SelectedStep.ToolName}; no affine calculation was run.");
    }

    private void RemoveSelectedRow()
    {
        var context = getContext();
        if (SelectedRow is not { } selected || context.Selection is not { } selection)
        {
            return;
        }

        var rows = (selection.Rows ?? [])
            .Where(row => !Equals(row, selected))
            .ToArray();
        if (rows.Length == 0)
        {
            removeSelection(selection);
        }
        else
        {
            persistSelection(selection with { Rows = rows });
            notifyAppliedSelectionsChanged();
        }

        SelectedRow = null;
        ResetEditor();
    }

    private void ResetEditor()
    {
        SourceEntityId = AvailableSourceEntityIds.FirstOrDefault() ?? string.Empty;
        ReferenceLandmarkId = "fixture.landmark.01";
        ReferenceX = 0;
        ReferenceY = 0;
        ReferenceZ = 0;
    }

    private bool SetField<T>(
        ref T field,
        T value,
        [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

internal sealed record ToolWorkbenchLandmarkCorrespondenceEditorContext(
    ToolWorkbenchPipelineStepItem? SelectedStep,
    bool IsCorrespondenceStep,
    ToolRecipeSelection? Selection,
    ToolRecipeSelectionSourceBinding? SourceBinding,
    string RootSourceId,
    string SourceFrameId,
    ToolWorkbenchTeachingSelectionRequirement? Requirement,
    IReadOnlyList<ToolWorkbenchPipelineStepItem> PipelineSteps);
