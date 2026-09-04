namespace OpenVisionLab.ThreeD.Viewer.Rendering;

/// <summary>
/// Owns the managed identity and lifetime state for C3D OpenGL resources.
/// The View still performs every context-bound upload, draw, and delete call;
/// this type only keeps the handles and cache keys together so source changes
/// and control shutdown cannot scatter partial state across the control.
/// </summary>
internal sealed class C3DRenderResourceState
{
    internal uint DisplayListId;

    internal C3DDisplayListKey? DisplayListKey;

    internal uint InteractionDisplayListId;

    internal C3DDisplayListKey? InteractionDisplayListKey;

    internal C3DGpuBufferSet? GpuBuffers;

    internal C3DGpuBufferKey? GpuBufferKey;

    internal C3DGpuBufferKey? GpuFailedKey;

    internal bool GpuReleasePending;

    internal bool GpuBuffersAvailable;

    internal bool HasManagedHandles =>
        GpuBuffers is not null
        || DisplayListId != 0
        || InteractionDisplayListId != 0;

    /// <summary>
    /// Clears references when a new OpenGL context is initialized. The caller
    /// invokes this only at the existing context boundary, before rendering.
    /// </summary>
    internal void ResetForOpenGLInitialization()
    {
        GpuBuffers = null;
        GpuBufferKey = null;
        GpuFailedKey = null;
        GpuReleasePending = false;
        GpuBuffersAvailable = false;
        DisplayListId = 0;
        DisplayListKey = null;
        InteractionDisplayListId = 0;
        InteractionDisplayListKey = null;
    }

    /// <summary>
    /// Drops managed handles after context-bound retirement has completed or
    /// the context is unavailable during shutdown.
    /// </summary>
    internal void ClearManagedReferences() => ResetForOpenGLInitialization();
}
