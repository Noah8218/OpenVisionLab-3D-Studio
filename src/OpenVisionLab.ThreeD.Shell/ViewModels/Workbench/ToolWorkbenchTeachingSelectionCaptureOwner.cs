namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;

/// <summary>
/// Owns the explicit Viewer teaching-capture lifecycle and editable geometry drafts.
/// The existing capture session remains the single transient state store.
/// </summary>
internal sealed class ToolWorkbenchTeachingSelectionCaptureOwner : INotifyPropertyChanged
{
    private readonly ToolWorkbenchTeachingCaptureSession session;
    private readonly Func<bool, ToolWorkbenchTeachingCaptureContext?> getCaptureContext;
    private readonly Func<ToolWorkbenchTeachingSelectionRequirement?> getRequirement;
    private readonly Func<ToolRecipeSelection?> getSelectedSelection;
    private readonly Func<ToolRecipeSelectionSourceBinding?> getDraftBinding;
    private readonly Func<bool> isCrossSectionDimensions;
    private readonly Action<ToolRecipeSelection> persistSelection;
    private readonly Action advanceDualRoiRole;
    private readonly Action notifyAppliedSelectionsChanged;
    private readonly Action<string, string> appendLog;
    private readonly Func<bool> isAlternateCandidateActive;
    private readonly Func<bool> canApplyAlternateCandidate;
    private readonly Action applyAlternateCandidate;
    private readonly Func<bool> canCancelAlternateCandidate;
    private readonly Action cancelAlternateCandidate;
    private readonly RelayCommand beginCommand;
    private readonly RelayCommand beginAdditionalLevelSurfaceReferenceCommand;
    private readonly RelayCommand undoCommand;
    private readonly RelayCommand cancelCommand;
    private readonly RelayCommand applyCommand;
    private readonly RelayCommand addPolygonVertexCommand;
    private readonly RelayCommand removePolygonVertexCommand;
    private readonly RelayCommand movePolygonVertexUpCommand;
    private readonly RelayCommand movePolygonVertexDownCommand;
    private bool suppressGridRectangleDraftChanged;
    private bool suppressGridCircleDraftChanged;
    private bool suppressGridPolygonDraftChanged;

    public ToolWorkbenchTeachingSelectionCaptureOwner(
        ToolWorkbenchTeachingCaptureSession session,
        Func<bool, ToolWorkbenchTeachingCaptureContext?> getCaptureContext,
        Func<ToolWorkbenchTeachingSelectionRequirement?> getRequirement,
        Func<ToolRecipeSelection?> getSelectedSelection,
        Func<ToolRecipeSelectionSourceBinding?> getDraftBinding,
        Func<bool> isCrossSectionDimensions,
        Action<ToolRecipeSelection> persistSelection,
        Action advanceDualRoiRole,
        Action notifyAppliedSelectionsChanged,
        Action<string, string> appendLog,
        Func<bool> isAlternateCandidateActive,
        Func<bool> canApplyAlternateCandidate,
        Action applyAlternateCandidate,
        Func<bool> canCancelAlternateCandidate,
        Action cancelAlternateCandidate)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.getCaptureContext = getCaptureContext
            ?? throw new ArgumentNullException(nameof(getCaptureContext));
        this.getRequirement = getRequirement
            ?? throw new ArgumentNullException(nameof(getRequirement));
        this.getSelectedSelection = getSelectedSelection
            ?? throw new ArgumentNullException(nameof(getSelectedSelection));
        this.getDraftBinding = getDraftBinding
            ?? throw new ArgumentNullException(nameof(getDraftBinding));
        this.isCrossSectionDimensions = isCrossSectionDimensions
            ?? throw new ArgumentNullException(nameof(isCrossSectionDimensions));
        this.persistSelection = persistSelection
            ?? throw new ArgumentNullException(nameof(persistSelection));
        this.advanceDualRoiRole = advanceDualRoiRole
            ?? throw new ArgumentNullException(nameof(advanceDualRoiRole));
        this.notifyAppliedSelectionsChanged = notifyAppliedSelectionsChanged
            ?? throw new ArgumentNullException(nameof(notifyAppliedSelectionsChanged));
        this.appendLog = appendLog ?? throw new ArgumentNullException(nameof(appendLog));
        this.isAlternateCandidateActive = isAlternateCandidateActive
            ?? throw new ArgumentNullException(nameof(isAlternateCandidateActive));
        this.canApplyAlternateCandidate = canApplyAlternateCandidate
            ?? throw new ArgumentNullException(nameof(canApplyAlternateCandidate));
        this.applyAlternateCandidate = applyAlternateCandidate
            ?? throw new ArgumentNullException(nameof(applyAlternateCandidate));
        this.canCancelAlternateCandidate = canCancelAlternateCandidate
            ?? throw new ArgumentNullException(nameof(canCancelAlternateCandidate));
        this.cancelAlternateCandidate = cancelAlternateCandidate
            ?? throw new ArgumentNullException(nameof(cancelAlternateCandidate));

        beginCommand = new RelayCommand(_ => Begin(), _ => CanBegin);
        beginAdditionalLevelSurfaceReferenceCommand = new RelayCommand(
            _ => BeginAdditionalLevelSurfaceReference(),
            _ => CanBeginAdditionalLevelSurfaceReference);
        undoCommand = new RelayCommand(
            _ => UndoRequested?.Invoke(this, EventArgs.Empty),
            _ => IsActive && CapturedPointCount > 0);
        cancelCommand = new RelayCommand(
            _ => CancelActiveCandidate(),
            _ => IsActive || canCancelAlternateCandidate());
        applyCommand = new RelayCommand(
            _ => ApplyActiveCandidate(),
            _ => CanApplyActiveCandidate);
        addPolygonVertexCommand = new RelayCommand(
            _ => AddPolygonVertex(),
            _ => IsGridPolygonEditorEnabled
                && GridPolygonVertices.Count
                    < ToolRecipeGridPolygonGeometry.MaximumVertexCount);
        removePolygonVertexCommand = new RelayCommand(
            parameter => RemovePolygonVertex(parameter as ToolWorkbenchGridPolygonVertexItem),
            parameter => IsGridPolygonEditorEnabled
                && parameter is ToolWorkbenchGridPolygonVertexItem item
                && GridPolygonVertices.Contains(item));
        movePolygonVertexUpCommand = new RelayCommand(
            parameter => MovePolygonVertex(
                parameter as ToolWorkbenchGridPolygonVertexItem,
                -1),
            parameter => CanMovePolygonVertex(
                parameter as ToolWorkbenchGridPolygonVertexItem,
                -1));
        movePolygonVertexDownCommand = new RelayCommand(
            parameter => MovePolygonVertex(
                parameter as ToolWorkbenchGridPolygonVertexItem,
                1),
            parameter => CanMovePolygonVertex(
                parameter as ToolWorkbenchGridPolygonVertexItem,
                1));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? StateChanged;

    public event EventHandler<ToolWorkbenchTeachingCaptureRequestEventArgs>?
        BeginRequested;

    public event EventHandler? UndoRequested;

    public event EventHandler? CancelRequested;

    public event EventHandler? ApplyRequested;

    public event EventHandler<ToolWorkbenchGridRectangleDraftChangedEventArgs>?
        GridRectangleDraftChanged;

    public event EventHandler<ToolWorkbenchGridCircleDraftChangedEventArgs>?
        GridCircleDraftChanged;

    public event EventHandler<ToolWorkbenchGridPolygonDraftChangedEventArgs>?
        GridPolygonDraftChanged;

    public RelayCommand BeginCommand => beginCommand;

    public RelayCommand BeginAdditionalLevelSurfaceReferenceCommand =>
        beginAdditionalLevelSurfaceReferenceCommand;

    public RelayCommand UndoCommand => undoCommand;

    public RelayCommand CancelCommand => cancelCommand;

    public RelayCommand ApplyCommand => applyCommand;

    public RelayCommand AddPolygonVertexCommand => addPolygonVertexCommand;

    public RelayCommand RemovePolygonVertexCommand => removePolygonVertexCommand;

    public RelayCommand MovePolygonVertexUpCommand => movePolygonVertexUpCommand;

    public RelayCommand MovePolygonVertexDownCommand => movePolygonVertexDownCommand;

    public ObservableCollection<ToolWorkbenchGridPolygonVertexItem>
        GridPolygonVertices { get; } = [];

    public ToolWorkbenchTeachingCaptureSession Session => session;

    public bool IsActive => session.IsActive;

    public bool IsCandidateActive => IsActive || isAlternateCandidateActive();

    public int CapturedPointCount => session.CapturedPointCount;

    public int RequiredPointCount => session.RequiredPointCount;

    public bool CanApply => session.CanApply;

    public bool IsGridRectangleEditorVisible =>
        getRequirement()?.Kind == ToolRecipeSelectionKinds.GridRectangle
        && getSelectedSelection()?.GridRectangle is not null;

    public bool IsGridRectangleEditorEnabled =>
        IsActive
        && getRequirement()?.Kind == ToolRecipeSelectionKinds.GridRectangle
        && CapturedPointCount == 2;

    public bool IsGridCircleEditorVisible =>
        getRequirement()?.Kind == ToolRecipeSelectionKinds.GridCircle
        && getSelectedSelection()?.GridCircle is not null;

    public bool IsGridCircleEditorEnabled =>
        IsActive
        && getRequirement()?.Kind == ToolRecipeSelectionKinds.GridCircle
        && CapturedPointCount == 2;

    public bool IsGridPolygonEditorVisible =>
        getRequirement()?.Kind == ToolRecipeSelectionKinds.GridPolygon
        && (IsActive || getSelectedSelection()?.GridPolygon is not null);

    public bool IsGridPolygonEditorEnabled =>
        IsActive
        && getRequirement()?.Kind == ToolRecipeSelectionKinds.GridPolygon;

    public bool IsGridRectangleDraftValid =>
        ToolWorkbenchTeachingSelectionPolicy.ValidateGridRectangle(
            session.GridRectangleDraft,
            getDraftBinding(),
            isCrossSectionDimensions(),
            out _);

    public string GridRectangleValidationSummary
    {
        get
        {
            ToolWorkbenchTeachingSelectionPolicy.ValidateGridRectangle(
                session.GridRectangleDraft,
                getDraftBinding(),
                isCrossSectionDimensions(),
                out var message);
            return message;
        }
    }

    public bool IsGridCircleDraftValid =>
        ToolWorkbenchTeachingSelectionPolicy.ValidateGridCircle(
            session.GridCircleDraft,
            getDraftBinding(),
            out _);

    public string GridCircleValidationSummary
    {
        get
        {
            ToolWorkbenchTeachingSelectionPolicy.ValidateGridCircle(
                session.GridCircleDraft,
                getDraftBinding(),
                out var message);
            return message;
        }
    }

    public bool IsGridPolygonDraftValid =>
        ToolWorkbenchTeachingSelectionPolicy.ValidateGridPolygon(
            session.GridPolygonDraft,
            getDraftBinding(),
            out _);

    public string GridPolygonValidationSummary
    {
        get
        {
            ToolWorkbenchTeachingSelectionPolicy.ValidateGridPolygon(
                session.GridPolygonDraft,
                getDraftBinding(),
                out var message);
            return message;
        }
    }

    public int GridRectangleRow
    {
        get => session.GridRectangleDraft.Row;
        set => SetGridRectangleDraftValue(
            session.GridRectangleDraft with { Row = value },
            nameof(GridRectangleRow));
    }

    public int GridRectangleColumn
    {
        get => session.GridRectangleDraft.Column;
        set => SetGridRectangleDraftValue(
            session.GridRectangleDraft with { Column = value },
            nameof(GridRectangleColumn));
    }

    public int GridRectangleRowCount
    {
        get => session.GridRectangleDraft.RowCount;
        set => SetGridRectangleDraftValue(
            session.GridRectangleDraft with { RowCount = value },
            nameof(GridRectangleRowCount));
    }

    public int GridRectangleColumnCount
    {
        get => session.GridRectangleDraft.ColumnCount;
        set => SetGridRectangleDraftValue(
            session.GridRectangleDraft with { ColumnCount = value },
            nameof(GridRectangleColumnCount));
    }

    public int GridCircleCenterRow
    {
        get => session.GridCircleDraft.CenterRow;
        set => SetGridCircleDraftValue(
            session.GridCircleDraft with { CenterRow = value },
            nameof(GridCircleCenterRow));
    }

    public int GridCircleCenterColumn
    {
        get => session.GridCircleDraft.CenterColumn;
        set => SetGridCircleDraftValue(
            session.GridCircleDraft with { CenterColumn = value },
            nameof(GridCircleCenterColumn));
    }

    public double GridCircleRadius
    {
        get => session.GridCircleDraft.Radius;
        set => SetGridCircleDraftValue(
            session.GridCircleDraft with { Radius = value },
            nameof(GridCircleRadius));
    }

    public void Begin()
    {
        var additionalReference = session.IsAdditionalLevelSurfaceReference;
        var context = getCaptureContext(additionalReference);
        if (context is null)
        {
            appendLog(
                "Warning",
                "Selection capture rejected | step=(none) | role=selection | reason=missing step, Viewer requirement, or current source binding.");
            return;
        }

        session.SetOwningStep(context.Step.Id);
        UpdateGridRectangleDraft(context.ExistingSelection?.GridRectangle);
        UpdateGridPolygonDraft(context.ExistingSelection?.GridPolygon);
        SetState(
            true,
            0,
            context.Requirement.RequiredPointCount,
            false,
            $"Pick the first {context.SourceBinding.Format} grid cell.");
        BeginRequested?.Invoke(
            this,
            new ToolWorkbenchTeachingCaptureRequestEventArgs(
                context.Step.Id,
                context.SelectionId,
                context.SelectionName,
                context.Requirement.Kind,
                context.Requirement.RequiredPointCount,
                context.RootSourceId,
                context.FrameId,
                context.SourceBinding,
                context.ExistingSelection));
        if (IsActive)
        {
            session.SetOwningStep(context.Step.Id);
        }

        appendLog(
            "Teach",
            $"Selection capture started | step={context.Step.Id} | tool={context.Step.ToolId} | role={context.ActiveRoleName} | selection={context.SelectionId} | kind={context.Requirement.Kind} | requiredPoints={context.Requirement.RequiredPointCount} | existing={context.ExistingSelection is not null} | inspectionRun=false.");
    }

    public void BeginAdditionalLevelSurfaceReference()
    {
        session.BeginAdditionalLevelSurfaceReference();
        Begin();
    }

    public void Cancel()
    {
        if (!IsActive)
        {
            return;
        }

        CancelRequested?.Invoke(this, EventArgs.Empty);
        Clear("Capture cancelled; no recipe geometry changed.");
        appendLog("Teach", "Selection capture cancelled; authored recipe unchanged.");
    }

    public void UpdateState(
        bool active,
        int capturedPointCount,
        int requiredPointCount,
        bool canApply,
        string message)
    {
        if (!active)
        {
            Clear(message);
            return;
        }

        SetState(
            true,
            Math.Max(0, capturedPointCount),
            Math.Max(1, requiredPointCount),
            canApply,
            string.IsNullOrWhiteSpace(message) ? "Capture in progress." : message);
    }

    public void Reject(string message)
    {
        Clear(message);
        appendLog("Warning", message);
    }

    public bool TryApplyCapturedSelection(
        ToolRecipeSelection? selection,
        out string message)
    {
        var context = getCaptureContext(session.IsAdditionalLevelSurfaceReference);
        if (!IsActive)
        {
            message = "The teaching capture is no longer active.";
            appendLog(
                "Warning",
                $"Selection apply rejected | role={context?.ActiveRoleName ?? "selection"} | reason={message}");
            return false;
        }
        if (context is null || !context.Requirement.UsesViewerCapture)
        {
            message = "The selected recipe step no longer supports Viewer teaching capture.";
            appendLog("Warning", $"Selection apply rejected | role=selection | reason={message}");
            return false;
        }
        if (!string.Equals(
            session.OwningStepId,
            context.Step.Id,
            StringComparison.OrdinalIgnoreCase))
        {
            message = $"The teaching capture belongs to '{session.OwningStepId ?? "(none)"}', not the selected step '{context.Step.Id}'.";
            appendLog(
                "Warning",
                $"Selection apply rejected | role={context.ActiveRoleName} | reason={message}");
            return false;
        }

        if (selection is null
            || !ToolWorkbenchTeachingSelectionPolicy.MatchesRequirement(
                selection,
                context.Requirement)
            || !string.Equals(
                selection.RootSourceId,
                context.RootSourceId,
                StringComparison.OrdinalIgnoreCase)
            || !string.Equals(
                selection.FrameId,
                context.FrameId,
                StringComparison.Ordinal)
            || !ToolRecipeSelectionSourceBindingVerifier.BindingsEqual(
                selection.SourceBinding,
                context.SourceBinding))
        {
            message = "The captured selection kind, owner artifact, bytes, grid, or frame does not match the selected step.";
            appendLog(
                "Warning",
                $"Selection apply rejected | step={context.Step.Id} | role={context.ActiveRoleName} | selection={selection?.Id ?? "(none)"} | reason={message}");
            return false;
        }

        persistSelection(selection);
        Clear("Selection applied to the authored recipe.");
        advanceDualRoiRole();
        notifyAppliedSelectionsChanged();
        message = $"Selection applied: {selection.Name}";
        appendLog(
            "Teach",
            $"{message} | step={context.Step.Id} | role={ToolWorkbenchTeachingSelectionPolicy.GetAppliedRoleName(context.Step, selection.Id)} | geometry={ToolWorkbenchTeachingSelectionPolicy.FormatSelectionGeometryForLog(selection)} | route={string.Join(';', context.Step.InputEntityIds)} | inspectionRun=false.");
        return true;
    }

    public void UpdateGridRectangleDraft(ToolRecipeGridRectangle? rectangle)
    {
        suppressGridRectangleDraftChanged = true;
        try
        {
            session.SetGridRectangleDraft(rectangle);
            OnPropertyChanged(nameof(GridRectangleRow));
            OnPropertyChanged(nameof(GridRectangleColumn));
            OnPropertyChanged(nameof(GridRectangleRowCount));
            OnPropertyChanged(nameof(GridRectangleColumnCount));
            NotifyGridRectangleDraftChanged();
        }
        finally
        {
            suppressGridRectangleDraftChanged = false;
        }
    }

    public void UpdateGridCircleDraft(ToolRecipeGridCircle? circle)
    {
        suppressGridCircleDraftChanged = true;
        try
        {
            session.SetGridCircleDraft(circle);
            OnPropertyChanged(nameof(GridCircleCenterRow));
            OnPropertyChanged(nameof(GridCircleCenterColumn));
            OnPropertyChanged(nameof(GridCircleRadius));
            NotifyGridCircleDraftChanged();
        }
        finally
        {
            suppressGridCircleDraftChanged = false;
        }
    }

    public void UpdateGridPolygonDraft(ToolRecipeGridPolygon? polygon)
    {
        suppressGridPolygonDraftChanged = true;
        try
        {
            session.SetGridPolygonDraft(polygon);
            foreach (var vertex in GridPolygonVertices)
            {
                vertex.Changed = null;
            }

            GridPolygonVertices.Clear();
            foreach (var (vertex, index) in (polygon?.Vertices ?? [])
                         .Select((vertex, index) => (vertex, index)))
            {
                GridPolygonVertices.Add(new ToolWorkbenchGridPolygonVertexItem(
                    index + 1,
                    vertex.Row,
                    vertex.Column,
                    OnGridPolygonVertexChanged));
            }
            NotifyGridPolygonDraftChanged();
        }
        finally
        {
            suppressGridPolygonDraftChanged = false;
        }
    }

    public bool TryUpdateHeightImageRoiCandidate(
        ToolRecipeGridRectangle rectangle,
        out string message)
    {
        ArgumentNullException.ThrowIfNull(rectangle);
        var binding = getDraftBinding();
        if (!IsActive
            || getRequirement()?.Kind != ToolRecipeSelectionKinds.GridRectangle
            || binding is null)
        {
            message = "Height Image ROI editing requires an active GridRectangle capture.";
            return false;
        }

        if (rectangle.Row < 0
            || rectangle.Column < 0
            || rectangle.RowCount <= 0
            || rectangle.ColumnCount <= 0
            || (long)rectangle.Row + rectangle.RowCount > binding.GridHeight
            || (long)rectangle.Column + rectangle.ColumnCount > binding.GridWidth)
        {
            message = "Height Image ROI candidate must stay inside the native source grid.";
            return false;
        }
        if (isCrossSectionDimensions()
            && (rectangle.RowCount != 1 || rectangle.ColumnCount < 2))
        {
            message = "Cross-section Dimensions requires one row and at least two columns.";
            return false;
        }

        UpdateGridRectangleDraft(rectangle);
        SetState(
            true,
            2,
            Math.Max(2, getRequirement()!.RequiredPointCount),
            true,
            "Height Image ROI candidate is ready for Review. Apply remains explicit.");
        GridRectangleDraftChanged?.Invoke(
            this,
            new ToolWorkbenchGridRectangleDraftChangedEventArgs(rectangle));
        message = "Height Image ROI candidate synchronized with the 3D Viewer.";
        return true;
    }

    public void RefreshContext()
    {
        if (!IsActive)
        {
            var selection = getSelectedSelection();
            UpdateGridRectangleDraft(selection?.GridRectangle);
            UpdateGridCircleDraft(selection?.GridCircle);
            UpdateGridPolygonDraft(selection?.GridPolygon);
        }

        NotifyStateChanged();
    }

    public void RefreshCommandStates()
    {
        beginCommand.RaiseCanExecuteChanged();
        beginAdditionalLevelSurfaceReferenceCommand.RaiseCanExecuteChanged();
        undoCommand.RaiseCanExecuteChanged();
        cancelCommand.RaiseCanExecuteChanged();
        applyCommand.RaiseCanExecuteChanged();
        addPolygonVertexCommand.RaiseCanExecuteChanged();
        removePolygonVertexCommand.RaiseCanExecuteChanged();
        movePolygonVertexUpCommand.RaiseCanExecuteChanged();
        movePolygonVertexDownCommand.RaiseCanExecuteChanged();
    }

    private bool CanBegin => !IsActive && getCaptureContext(false) is not null;

    private bool CanBeginAdditionalLevelSurfaceReference =>
        !IsActive
        && getCaptureContext(true) is { Step.ToolId: "level-surface" };

    private bool CanApplyActiveCandidate => IsActive
        ? CanApply
          && (getRequirement()?.Kind != ToolRecipeSelectionKinds.GridRectangle
              || IsGridRectangleDraftValid)
          && (getRequirement()?.Kind != ToolRecipeSelectionKinds.GridCircle
              || IsGridCircleDraftValid)
          && (getRequirement()?.Kind != ToolRecipeSelectionKinds.GridPolygon
              || IsGridPolygonDraftValid)
        : canApplyAlternateCandidate();

    private void ApplyActiveCandidate()
    {
        if (IsActive)
        {
            ApplyRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (canApplyAlternateCandidate())
        {
            applyAlternateCandidate();
        }
    }

    private void CancelActiveCandidate()
    {
        if (IsActive)
        {
            Cancel();
            return;
        }

        if (canCancelAlternateCandidate())
        {
            cancelAlternateCandidate();
        }
    }

    private void SetState(
        bool active,
        int capturedPointCount,
        int requiredPointCount,
        bool canApply,
        string message)
    {
        session.SetState(active, capturedPointCount, requiredPointCount, canApply);
        NotifyStateChanged();
    }

    private void Clear(string message)
    {
        session.Clear();
        NotifyStateChanged();
        var selection = getSelectedSelection();
        UpdateGridRectangleDraft(selection?.GridRectangle);
        UpdateGridCircleDraft(selection?.GridCircle);
        UpdateGridPolygonDraft(selection?.GridPolygon);
    }

    private void SetGridRectangleDraftValue(
        ToolRecipeGridRectangle rectangle,
        string propertyName)
    {
        if (session.GridRectangleDraft == rectangle)
        {
            return;
        }

        session.SetGridRectangleDraft(rectangle);
        OnPropertyChanged(propertyName);
        NotifyGridRectangleDraftChanged();
        if (suppressGridRectangleDraftChanged
            || !IsGridRectangleEditorEnabled
            || !IsGridRectangleDraftValid)
        {
            return;
        }

        GridRectangleDraftChanged?.Invoke(
            this,
            new ToolWorkbenchGridRectangleDraftChangedEventArgs(rectangle));
    }

    private void NotifyGridRectangleDraftChanged()
    {
        OnPropertyChanged(nameof(IsGridRectangleDraftValid));
        OnPropertyChanged(nameof(GridRectangleValidationSummary));
        RefreshCommandStates();
    }

    private void SetGridCircleDraftValue(
        ToolRecipeGridCircle circle,
        string propertyName)
    {
        if (session.GridCircleDraft == circle)
        {
            return;
        }

        session.SetGridCircleDraft(circle);
        OnPropertyChanged(propertyName);
        NotifyGridCircleDraftChanged();
        if (suppressGridCircleDraftChanged
            || !IsGridCircleEditorEnabled
            || !IsGridCircleDraftValid)
        {
            return;
        }

        GridCircleDraftChanged?.Invoke(
            this,
            new ToolWorkbenchGridCircleDraftChangedEventArgs(circle));
    }

    private void NotifyGridCircleDraftChanged()
    {
        OnPropertyChanged(nameof(IsGridCircleDraftValid));
        OnPropertyChanged(nameof(GridCircleValidationSummary));
        RefreshCommandStates();
    }

    private void OnGridPolygonVertexChanged(ToolWorkbenchGridPolygonVertexItem item)
    {
        if (!suppressGridPolygonDraftChanged)
        {
            UpdateGridPolygonDraftFromItems();
        }
    }

    private void AddPolygonVertex()
    {
        if (!IsGridPolygonEditorEnabled
            || GridPolygonVertices.Count
                >= ToolRecipeGridPolygonGeometry.MaximumVertexCount)
        {
            return;
        }

        var binding = getDraftBinding();
        var last = GridPolygonVertices.LastOrDefault();
        var row = last?.Row
                  ?? (binding is null
                      ? 0
                      : Math.Max(0, (binding.GridHeight - 1) / 2.0));
        var column = last?.Column
                     ?? (binding is null
                         ? 0
                         : Math.Max(0, (binding.GridWidth - 1) / 2.0));
        if (last is not null)
        {
            var maxRow = binding is null
                ? row + 1
                : Math.Max(0, binding.GridHeight - 1);
            var maxColumn = binding is null
                ? column + 1
                : Math.Max(0, binding.GridWidth - 1);
            row = Math.Min(maxRow, row + 1);
            column = Math.Min(maxColumn, column + 1);
        }

        GridPolygonVertices.Add(new ToolWorkbenchGridPolygonVertexItem(
            GridPolygonVertices.Count + 1,
            row,
            column,
            OnGridPolygonVertexChanged));
        UpdateGridPolygonDraftFromItems();
    }

    private void RemovePolygonVertex(ToolWorkbenchGridPolygonVertexItem? item)
    {
        if (!IsGridPolygonEditorEnabled
            || item is null
            || !GridPolygonVertices.Remove(item))
        {
            return;
        }

        ReindexPolygonVertices();
        UpdateGridPolygonDraftFromItems();
    }

    private bool CanMovePolygonVertex(
        ToolWorkbenchGridPolygonVertexItem? item,
        int offset)
    {
        if (!IsGridPolygonEditorEnabled || item is null)
        {
            return false;
        }

        var index = GridPolygonVertices.IndexOf(item);
        var target = index + offset;
        return index >= 0 && target >= 0 && target < GridPolygonVertices.Count;
    }

    private void MovePolygonVertex(
        ToolWorkbenchGridPolygonVertexItem? item,
        int offset)
    {
        if (!CanMovePolygonVertex(item, offset) || item is null)
        {
            return;
        }

        GridPolygonVertices.Move(
            GridPolygonVertices.IndexOf(item),
            GridPolygonVertices.IndexOf(item) + offset);
        ReindexPolygonVertices();
        UpdateGridPolygonDraftFromItems();
    }

    private void ReindexPolygonVertices()
    {
        for (var index = 0; index < GridPolygonVertices.Count; index++)
        {
            GridPolygonVertices[index].SetOrder(index + 1);
        }
    }

    private void UpdateGridPolygonDraftFromItems()
    {
        session.SetGridPolygonDraft(new ToolRecipeGridPolygon(
            GridPolygonVertices
                .Select(vertex => new ToolRecipeGridPolygonVertex(
                    vertex.Row,
                    vertex.Column))
                .ToArray()));
        NotifyGridPolygonDraftChanged();
        if (!IsGridPolygonEditorEnabled || !IsGridPolygonDraftValid)
        {
            return;
        }

        GridPolygonDraftChanged?.Invoke(
            this,
            new ToolWorkbenchGridPolygonDraftChangedEventArgs(
                session.GridPolygonDraft));
    }

    private void NotifyGridPolygonDraftChanged()
    {
        OnPropertyChanged(nameof(IsGridPolygonDraftValid));
        OnPropertyChanged(nameof(GridPolygonValidationSummary));
        RefreshCommandStates();
    }

    private void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(IsActive));
        OnPropertyChanged(nameof(IsCandidateActive));
        OnPropertyChanged(nameof(CapturedPointCount));
        OnPropertyChanged(nameof(RequiredPointCount));
        OnPropertyChanged(nameof(CanApply));
        OnPropertyChanged(nameof(IsGridRectangleEditorVisible));
        OnPropertyChanged(nameof(IsGridRectangleEditorEnabled));
        OnPropertyChanged(nameof(IsGridCircleEditorVisible));
        OnPropertyChanged(nameof(IsGridCircleEditorEnabled));
        OnPropertyChanged(nameof(IsGridPolygonEditorVisible));
        OnPropertyChanged(nameof(IsGridPolygonEditorEnabled));
        RefreshCommandStates();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

internal sealed record ToolWorkbenchTeachingCaptureContext(
    ToolWorkbenchPipelineStepItem Step,
    ToolWorkbenchTeachingSelectionRequirement Requirement,
    ToolRecipeSelection? ExistingSelection,
    string SelectionId,
    string SelectionName,
    string RootSourceId,
    string FrameId,
    ToolRecipeSelectionSourceBinding SourceBinding,
    string ActiveRoleName);
