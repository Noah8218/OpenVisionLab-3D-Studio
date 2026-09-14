namespace OpenVisionLab.ThreeD.Viewer.Rendering;

/// <summary>
/// Owns cumulative observations for the C3D GPU render path.
/// The WPF control remains responsible for OpenGL calls; this state holder
/// keeps upload, draw, fallback, and release evidence independently testable.
/// </summary>
internal sealed class C3DGpuTelemetry
{
    public int UploadCount { get; private set; }

    public int ReleaseCount { get; private set; }

    public int ReleaseFailureCount { get; private set; }

    public int FallbackCount { get; private set; }

    public int DrawCount { get; private set; }

    public long UploadedBytes { get; private set; }

    public double LastUploadMilliseconds { get; private set; }

    public string LastFailure { get; private set; } = string.Empty;

    public void RecordUpload(long uploadedBytes, double elapsedMilliseconds)
    {
        UploadCount++;
        UploadedBytes = uploadedBytes;
        LastUploadMilliseconds = elapsedMilliseconds;
        LastFailure = string.Empty;
    }

    public void RecordFallback(string failure)
    {
        FallbackCount++;
        LastFailure = failure;
    }

    public void RecordDraw() => DrawCount++;

    public void RecordRelease(bool succeeded)
    {
        if (succeeded)
        {
            ReleaseCount++;
        }
        else
        {
            ReleaseFailureCount++;
        }
    }
}
