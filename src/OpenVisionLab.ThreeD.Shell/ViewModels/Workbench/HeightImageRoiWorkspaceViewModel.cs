using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Input;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// WPF-neutral presentation and pointer-gesture owner for ROI overlays in the
/// native-grid Height Image. Recipe mutation remains owned by
/// <see cref="ToolWorkbenchViewModel"/> and is requested only through events.
/// </summary>
public sealed class HeightImageRoiWorkspaceViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly ThreeDLocalization localization;
    private readonly RelayCommand applyCommand;
    private readonly RelayCommand cancelCommand;
    private readonly RelayCommand deleteCommand;
    private IReadOnlyList<HeightImageRoiOverlayItem> overlays = [];
    private InspectionWorkspaceRegionRole activeRole;
    private InspectionWorkspaceRegionLifecycleState lifecycle =
        InspectionWorkspaceRegionLifecycleState.Missing;
    private ToolRecipeGridRectangle? candidate;
    private bool hasContext;
    private bool isCaptureActive;
    private bool isGestureActive;
    private HeightImageRoiGestureMode gestureMode;
    private int anchorRow;
    private int anchorColumn;
    private int moveRowOffset;
    private int moveColumnOffset;
    private int gridWidth;
    private int gridHeight;
    private int disposalState;

    public HeightImageRoiWorkspaceViewModel(ThreeDLocalization localization)
    {
        this.localization = localization;
        applyCommand = new RelayCommand(
            _ => ApplyRequested?.Invoke(this, EventArgs.Empty),
            _ => IsCaptureActive && Lifecycle == InspectionWorkspaceRegionLifecycleState.Review);
        cancelCommand = new RelayCommand(
            _ => CancelRequested?.Invoke(this, EventArgs.Empty),
            _ => IsCaptureActive);
        deleteCommand = new RelayCommand(
            _ => DeleteRequested?.Invoke(this, EventArgs.Empty),
            _ => !IsCaptureActive && ActiveOverlay is not null);
        localization.PropertyChanged += OnLocalizationChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler<HeightImageRoiCandidateChangedEventArgs>? CandidateChanged;
    public event EventHandler<HeightImageRoiSelectionRequestedEventArgs>? SelectionRequested;
    public event EventHandler? ApplyRequested;
    public event EventHandler? CancelRequested;
    public event EventHandler? DeleteRequested;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposalState, 1) != 0)
        {
            return;
        }

        localization.PropertyChanged -= OnLocalizationChanged;
    }

    public ICommand ApplyCommand => applyCommand;
    public ICommand CancelCommand => cancelCommand;
    public ICommand DeleteCommand => deleteCommand;
    public IReadOnlyList<HeightImageRoiOverlayItem> Overlays => overlays;
    public InspectionWorkspaceRegionRole ActiveRole => activeRole;
    public InspectionWorkspaceRegionLifecycleState Lifecycle => lifecycle;
    public ToolRecipeGridRectangle? Candidate => candidate;
    public bool HasContext => hasContext;
    public bool HasOverlays => VisibleOverlays.Count > 0;
    public bool IsCaptureActive => isCaptureActive;
    public bool IsGestureActive => isGestureActive;

    public HeightImageRoiOverlayItem? ActiveOverlay => overlays.FirstOrDefault(item =>
        item.IsActive);

    public IReadOnlyList<HeightImageRoiOverlayItem> VisibleOverlays
    {
        get
        {
            if (!IsCaptureActive || Candidate is null)
            {
                return overlays;
            }

            var active = ActiveOverlay;
            var candidateId = active?.SelectionId ?? string.Empty;
            var visible = overlays
                .Where(item =>
                    !item.IsActive
                    || !string.Equals(
                        item.SelectionId,
                        candidateId,
                        StringComparison.OrdinalIgnoreCase))
                .ToList();
            visible.Add(new HeightImageRoiOverlayItem(
                candidateId,
                active?.Name ?? ActiveRoleSummary,
                ActiveRole,
                Lifecycle,
                Candidate,
                true,
                true));
            return visible;
        }
    }

    public string ActiveRoleSummary => ActiveRole switch
    {
        InspectionWorkspaceRegionRole.Reference => localization.ReferenceRoi,
        InspectionWorkspaceRegionRole.Measurement => localization.MeasurementRoi,
        InspectionWorkspaceRegionRole.First => localization.FirstRoi,
        InspectionWorkspaceRegionRole.Second => localization.SecondRoi,
        InspectionWorkspaceRegionRole.Selection => localization.TeachingSelections,
        _ => localization.TeachingSelections
    };

    public string LifecycleSummary => Lifecycle switch
    {
        InspectionWorkspaceRegionLifecycleState.Drawing => localization.RoiDrawing,
        InspectionWorkspaceRegionLifecycleState.Review => localization.RoiReview,
        InspectionWorkspaceRegionLifecycleState.Applied => localization.RoiApplied,
        _ => localization.RoiMissing
    };

    public string InteractionHint => IsCaptureActive
        ? Lifecycle == InspectionWorkspaceRegionLifecycleState.Review
            ? localization.HeightImageRoiReviewHint
            : localization.HeightImageRoiDrawHint
        : localization.HeightImageRoiSelectHint;

    public void SetProjection(HeightImageRoiProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);
        overlays = projection.Overlays;
        activeRole = projection.ActiveRole;
        lifecycle = projection.Lifecycle;
        hasContext = projection.HasContext;
        isCaptureActive = projection.IsCaptureActive;
        gridWidth = Math.Max(0, projection.GridWidth);
        gridHeight = Math.Max(0, projection.GridHeight);
        if (!isGestureActive)
        {
            candidate = projection.Candidate;
        }

        OnProjectionChanged();
    }

    public bool TryBeginPointer(
        int row,
        int column,
        int rowTolerance,
        int columnTolerance)
    {
        if (!HasContext || gridWidth <= 0 || gridHeight <= 0)
        {
            return false;
        }

        row = Math.Clamp(row, 0, gridHeight - 1);
        column = Math.Clamp(column, 0, gridWidth - 1);
        if (!IsCaptureActive)
        {
            var selected = FindOverlay(row, column);
            if (selected is null)
            {
                return false;
            }

            SelectionRequested?.Invoke(
                this,
                new HeightImageRoiSelectionRequestedEventArgs(selected.SelectionId));
            return false;
        }

        if (Candidate is null)
        {
            gestureMode = HeightImageRoiGestureMode.Draw;
            anchorRow = row;
            anchorColumn = column;
            isGestureActive = true;
            PublishCandidate(CreateRectangle(anchorRow, anchorColumn, row, column));
            OnPropertyChanged(nameof(IsGestureActive));
            return true;
        }

        var rectangle = Candidate;
        var top = rectangle.Row;
        var left = rectangle.Column;
        var bottom = rectangle.Row + rectangle.RowCount - 1;
        var right = rectangle.Column + rectangle.ColumnCount - 1;
        rowTolerance = Math.Max(0, rowTolerance);
        columnTolerance = Math.Max(0, columnTolerance);

        if (Near(row, column, top, left, rowTolerance, columnTolerance))
        {
            BeginResize(bottom, right);
        }
        else if (Near(row, column, top, right, rowTolerance, columnTolerance))
        {
            BeginResize(bottom, left);
        }
        else if (Near(row, column, bottom, left, rowTolerance, columnTolerance))
        {
            BeginResize(top, right);
        }
        else if (Near(row, column, bottom, right, rowTolerance, columnTolerance))
        {
            BeginResize(top, left);
        }
        else if (Contains(rectangle, row, column))
        {
            gestureMode = HeightImageRoiGestureMode.Move;
            moveRowOffset = row - rectangle.Row;
            moveColumnOffset = column - rectangle.Column;
            isGestureActive = true;
        }
        else
        {
            return false;
        }

        OnPropertyChanged(nameof(IsGestureActive));
        return true;
    }

    public bool TryUpdatePointer(int row, int column)
    {
        if (!isGestureActive || Candidate is null)
        {
            return false;
        }

        row = Math.Clamp(row, 0, gridHeight - 1);
        column = Math.Clamp(column, 0, gridWidth - 1);
        var next = gestureMode switch
        {
            HeightImageRoiGestureMode.Draw or HeightImageRoiGestureMode.Resize =>
                CreateRectangle(anchorRow, anchorColumn, row, column),
            HeightImageRoiGestureMode.Move => MoveRectangle(Candidate, row, column),
            _ => Candidate
        };
        PublishCandidate(next);
        return true;
    }

    public void EndPointer()
    {
        if (!isGestureActive)
        {
            return;
        }

        isGestureActive = false;
        gestureMode = HeightImageRoiGestureMode.None;
        OnPropertyChanged(nameof(IsGestureActive));
    }

    public void CancelPointer()
    {
        if (!isGestureActive)
        {
            return;
        }

        isGestureActive = false;
        gestureMode = HeightImageRoiGestureMode.None;
        OnPropertyChanged(nameof(IsGestureActive));
    }

    private void BeginResize(int oppositeRow, int oppositeColumn)
    {
        gestureMode = HeightImageRoiGestureMode.Resize;
        anchorRow = oppositeRow;
        anchorColumn = oppositeColumn;
        isGestureActive = true;
    }

    private ToolRecipeGridRectangle MoveRectangle(
        ToolRecipeGridRectangle rectangle,
        int row,
        int column)
    {
        var nextRow = Math.Clamp(
            row - moveRowOffset,
            0,
            Math.Max(0, gridHeight - rectangle.RowCount));
        var nextColumn = Math.Clamp(
            column - moveColumnOffset,
            0,
            Math.Max(0, gridWidth - rectangle.ColumnCount));
        return rectangle with
        {
            Row = nextRow,
            Column = nextColumn
        };
    }

    private void PublishCandidate(ToolRecipeGridRectangle rectangle)
    {
        if (Equals(candidate, rectangle))
        {
            return;
        }

        candidate = rectangle;
        OnPropertyChanged(nameof(Candidate));
        OnPropertyChanged(nameof(VisibleOverlays));
        OnPropertyChanged(nameof(HasOverlays));
        CandidateChanged?.Invoke(
            this,
            new HeightImageRoiCandidateChangedEventArgs(rectangle));
    }

    private HeightImageRoiOverlayItem? FindOverlay(int row, int column) =>
        overlays
            .Where(item => Contains(item.Rectangle, row, column))
            .OrderByDescending(item => item.IsActive)
            .ThenBy(item => (long)item.Rectangle.RowCount * item.Rectangle.ColumnCount)
            .FirstOrDefault();

    private ToolRecipeGridRectangle CreateRectangle(
        int firstRow,
        int firstColumn,
        int secondRow,
        int secondColumn) =>
        new(
            Math.Min(firstRow, secondRow),
            Math.Min(firstColumn, secondColumn),
            Math.Abs(secondRow - firstRow) + 1,
            Math.Abs(secondColumn - firstColumn) + 1);

    private static bool Contains(
        ToolRecipeGridRectangle rectangle,
        int row,
        int column) =>
        row >= rectangle.Row
        && row < rectangle.Row + rectangle.RowCount
        && column >= rectangle.Column
        && column < rectangle.Column + rectangle.ColumnCount;

    private static bool Near(
        int row,
        int column,
        int targetRow,
        int targetColumn,
        int rowTolerance,
        int columnTolerance) =>
        Math.Abs(row - targetRow) <= rowTolerance
        && Math.Abs(column - targetColumn) <= columnTolerance;

    private void OnProjectionChanged()
    {
        OnPropertyChanged(nameof(Overlays));
        OnPropertyChanged(nameof(VisibleOverlays));
        OnPropertyChanged(nameof(HasOverlays));
        OnPropertyChanged(nameof(ActiveRole));
        OnPropertyChanged(nameof(ActiveOverlay));
        OnPropertyChanged(nameof(Lifecycle));
        OnPropertyChanged(nameof(Candidate));
        OnPropertyChanged(nameof(HasContext));
        OnPropertyChanged(nameof(IsCaptureActive));
        OnPropertyChanged(nameof(ActiveRoleSummary));
        OnPropertyChanged(nameof(LifecycleSummary));
        OnPropertyChanged(nameof(InteractionHint));
        applyCommand.RaiseCanExecuteChanged();
        cancelCommand.RaiseCanExecuteChanged();
        deleteCommand.RaiseCanExecuteChanged();
    }

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs args)
    {
        OnPropertyChanged(nameof(ActiveRoleSummary));
        OnPropertyChanged(nameof(LifecycleSummary));
        OnPropertyChanged(nameof(InteractionHint));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed record HeightImageRoiProjection(
    bool HasContext,
    int GridWidth,
    int GridHeight,
    InspectionWorkspaceRegionRole ActiveRole,
    InspectionWorkspaceRegionLifecycleState Lifecycle,
    bool IsCaptureActive,
    ToolRecipeGridRectangle? Candidate,
    IReadOnlyList<HeightImageRoiOverlayItem> Overlays);

public sealed record HeightImageRoiOverlayItem(
    string SelectionId,
    string Name,
    InspectionWorkspaceRegionRole Role,
    InspectionWorkspaceRegionLifecycleState Lifecycle,
    ToolRecipeGridRectangle Rectangle,
    bool IsActive,
    bool IsCandidate);

public sealed class HeightImageRoiCandidateChangedEventArgs(
    ToolRecipeGridRectangle rectangle) : EventArgs
{
    public ToolRecipeGridRectangle Rectangle { get; } = rectangle;
}

public sealed class HeightImageRoiSelectionRequestedEventArgs(
    string selectionId) : EventArgs
{
    public string SelectionId { get; } = selectionId;
}

internal enum HeightImageRoiGestureMode
{
    None,
    Draw,
    Move,
    Resize
}
