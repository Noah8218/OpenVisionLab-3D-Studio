namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

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
