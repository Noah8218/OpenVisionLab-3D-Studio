using SharpGL;

namespace OpenVisionLab.ThreeD.Viewer.Rendering;

/// <summary>
/// Coordinates the context-bound retirement sequence for Viewer-owned OpenGL
/// resources. Resource owners still perform their own delete calls; this type
/// only keeps ordering, failure isolation, and final managed-reference cleanup
/// out of the WPF control.
/// </summary>
internal sealed class OpenGLResourceRetirementCoordinator
{
    private readonly Action<OpenGL> releaseC3DGpuBuffers;
    private readonly Action<OpenGL> releaseImportedMeshTexture;
    private readonly Action<OpenGL> releaseC3DDisplayLists;
    private readonly Action clearManagedReferences;
    private readonly OpenGLResourceRetirementTelemetry telemetry;

    public OpenGLResourceRetirementCoordinator(
        Action<OpenGL> releaseC3DGpuBuffers,
        Action<OpenGL> releaseImportedMeshTexture,
        Action<OpenGL> releaseC3DDisplayLists,
        Action clearManagedReferences,
        OpenGLResourceRetirementTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(releaseC3DGpuBuffers);
        ArgumentNullException.ThrowIfNull(releaseImportedMeshTexture);
        ArgumentNullException.ThrowIfNull(releaseC3DDisplayLists);
        ArgumentNullException.ThrowIfNull(clearManagedReferences);
        ArgumentNullException.ThrowIfNull(telemetry);

        this.releaseC3DGpuBuffers = releaseC3DGpuBuffers;
        this.releaseImportedMeshTexture = releaseImportedMeshTexture;
        this.releaseC3DDisplayLists = releaseC3DDisplayLists;
        this.clearManagedReferences = clearManagedReferences;
        this.telemetry = telemetry;
    }

    /// <summary>
    /// Retires each resource group in the established order. A resource delete
    /// failure is isolated so later resource groups and managed cleanup still
    /// execute while the existing telemetry records the failure.
    /// </summary>
    internal void Retire(OpenGL gl)
    {
        ArgumentNullException.ThrowIfNull(gl);
        telemetry.RecordCallback();

        TryRetire(gl, releaseC3DGpuBuffers);
        TryRetire(gl, releaseImportedMeshTexture);
        TryRetire(gl, releaseC3DDisplayLists);

        try
        {
            gl.Flush();
        }
        catch
        {
            // The context may already be unavailable during Window shutdown.
        }

        clearManagedReferences();
    }

    private void TryRetire(OpenGL gl, Action<OpenGL> release)
    {
        try
        {
            release(gl);
        }
        catch (Exception)
        {
            // A closing context may reject deletion; later resources still need
            // an opportunity to release and managed handles are cleared below.
            telemetry.RecordFailure();
        }
    }
}
