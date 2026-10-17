namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

using System.Globalization;
using OpenVisionLab.ThreeD.Core;

internal sealed class ToolWorkbenchTeachingCaptureSession
{
    public bool IsActive { get; private set; }

    public string? OwningStepId { get; private set; }

    public int CapturedPointCount { get; private set; }

    public int RequiredPointCount { get; private set; }

    public bool CanApply { get; private set; }

    public bool IsAdditionalLevelSurfaceReference { get; private set; }

    public ToolRecipeGridRectangle GridRectangleDraft { get; private set; } = new(0, 0, 0, 0);

    public string GridRectangleColumnText { get; private set; } = "0";

    public string GridRectangleRowText { get; private set; } = "0";

    public string GridRectangleColumnCountText { get; private set; } = "0";

    public string GridRectangleRowCountText { get; private set; } = "0";

    public ToolRecipeGridCircle GridCircleDraft { get; private set; } = new(0, 0, 0);

    public ToolRecipeGridPolygon GridPolygonDraft { get; private set; } = new([]);

    public void SetOwningStep(string stepId)
    {
        OwningStepId = stepId;
    }

    public void BeginAdditionalLevelSurfaceReference()
    {
        IsAdditionalLevelSurfaceReference = true;
    }

    public void SetState(
        bool active,
        int capturedPointCount,
        int requiredPointCount,
        bool canApply)
    {
        IsActive = active;
        CapturedPointCount = capturedPointCount;
        RequiredPointCount = requiredPointCount;
        CanApply = canApply;
    }

    public void SetGridRectangleDraft(ToolRecipeGridRectangle? rectangle)
    {
        GridRectangleDraft = rectangle ?? new ToolRecipeGridRectangle(0, 0, 0, 0);
        SetGridRectangleText(
            GridRectangleDraft.Column.ToString(CultureInfo.InvariantCulture),
            GridRectangleDraft.Row.ToString(CultureInfo.InvariantCulture),
            GridRectangleDraft.ColumnCount.ToString(CultureInfo.InvariantCulture),
            GridRectangleDraft.RowCount.ToString(CultureInfo.InvariantCulture));
    }

    public void SetGridRectangleText(string column, string row, string columnCount, string rowCount)
    {
        GridRectangleColumnText = column;
        GridRectangleRowText = row;
        GridRectangleColumnCountText = columnCount;
        GridRectangleRowCountText = rowCount;
    }

    public void SetGridCircleDraft(ToolRecipeGridCircle? circle)
    {
        GridCircleDraft = circle ?? new ToolRecipeGridCircle(0, 0, 0);
    }

    public void SetGridPolygonDraft(ToolRecipeGridPolygon? polygon)
    {
        GridPolygonDraft = polygon ?? new ToolRecipeGridPolygon([]);
    }

    public void Clear()
    {
        OwningStepId = null;
        IsAdditionalLevelSurfaceReference = false;
        SetState(false, 0, 0, false);
    }
}
