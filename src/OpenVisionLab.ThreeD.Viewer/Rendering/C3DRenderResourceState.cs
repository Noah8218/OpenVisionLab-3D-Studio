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

    internal C3DGpuBufferSet? GpuBuffers { get; private set; }

    internal C3DGpuBufferKey? GpuBufferKey { get; private set; }

    internal C3DGpuBufferKey? GpuFailedKey { get; private set; }

    internal bool GpuReleasePending { get; private set; }

    internal bool GpuBuffersAvailable { get; private set; }

    internal bool HasManagedHandles =>
        GpuBuffers is not null
        || DisplayListId != 0
        || InteractionDisplayListId != 0;

    /// <summary>
    /// Returns whether the active GPU snapshot does not match the requested
    /// render identity. The View uses this decision before any context-bound
    /// upload and keeps the release-before-upload order explicit.
    /// </summary>
    internal bool RequiresGpuReplacement(C3DGpuBufferKey key) =>
        GpuBuffers is null || GpuBufferKey != key;

    internal bool IsGpuReplacementFailed(C3DGpuBufferKey key) => GpuFailedKey == key;

    /// <summary>
    /// Records a failed replacement attempt without hiding the OpenGL cleanup
    /// policy in this managed state holder. The caller has already retired the
    /// previous context-bound buffers before invoking this method.
    /// </summary>
    internal void MarkGpuReplacementFailed(C3DGpuBufferKey key)
    {
        GpuFailedKey = key;
        GpuBuffersAvailable = false;
    }

    /// <summary>
    /// Commits a successfully uploaded snapshot as the active GPU resource.
    /// All related identity flags change together so a partial View update
    /// cannot advertise a buffer that has no matching key.
    /// </summary>
    internal void SetGpuReplacement(C3DGpuBufferKey key, C3DGpuBufferSet buffers)
    {
        ArgumentNullException.ThrowIfNull(buffers);
        GpuBuffers = buffers;
        GpuBufferKey = key;
        GpuFailedKey = null;
        GpuReleasePending = false;
        GpuBuffersAvailable = true;
    }

    /// <summary>
    /// Clears managed GPU references after the View has executed the
    /// context-bound delete call (or after deletion was rejected during close).
    /// </summary>
    internal void MarkGpuBuffersReleased()
    {
        GpuBuffers = null;
        GpuBufferKey = null;
        GpuReleasePending = false;
        GpuBuffersAvailable = false;
    }

    /// <summary>
    /// Invalidates the current render identity while preserving the existing
    /// buffer reference until the next render callback can delete it.
    /// </summary>
    internal void InvalidateGpuForRenderProxy()
    {
        GpuReleasePending = GpuBuffers is not null;
        GpuBufferKey = null;
        GpuFailedKey = null;
    }

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
