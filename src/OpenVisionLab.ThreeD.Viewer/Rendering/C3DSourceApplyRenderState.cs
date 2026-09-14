namespace OpenVisionLab.ThreeD.Viewer.Rendering;

/// <summary>
/// Tracks the temporary render gate used while a C3D source is applied.
/// OpenGL calls and source mutation remain owned by the Viewer control.
/// </summary>
internal sealed class C3DSourceApplyRenderState
{
    public bool IsActive { get; private set; }

    public bool IsRenderSuppressed { get; private set; }

    public int RenderRequestCount { get; private set; }

    public int SuppressedRenderRequestCount { get; private set; }

    public int RenderExecutionCount { get; private set; }

    public double RenderExecutionMilliseconds { get; private set; }

    public void Begin()
    {
        IsActive = true;
        IsRenderSuppressed = true;
        RenderRequestCount = 0;
        SuppressedRenderRequestCount = 0;
        RenderExecutionCount = 0;
        RenderExecutionMilliseconds = 0.0;
    }

    public void AllowRender() => IsRenderSuppressed = false;

    public bool TrySuppressRenderRequest()
    {
        if (!IsActive)
        {
            return false;
        }

        RenderRequestCount++;
        if (!IsRenderSuppressed)
        {
            return false;
        }

        SuppressedRenderRequestCount++;
        return true;
    }

    public void RecordRenderExecution(double elapsedMilliseconds)
    {
        if (!IsActive)
        {
            return;
        }

        RenderExecutionCount++;
        RenderExecutionMilliseconds += elapsedMilliseconds;
    }

    public void Complete()
    {
        IsRenderSuppressed = false;
        IsActive = false;
    }
}
