namespace OpenVisionLab.ThreeD.Data;

/// <summary>
/// Keeps the measured Dev admission boundary for full-resolution C3D inputs.
/// The limit is a cell-count guard derived from the 3D-037 scale run; it is
/// not a machine-wide RAM budget and must be requalified for a new envelope.
/// </summary>
public static class C3DMemoryAdmissionPolicy
{
    /// <summary>
    /// Largest full-resolution grid that passed the current 3D-037 scale run:
    /// 4096 x 4096 cells. A cell-count guard avoids allocating the payload for
    /// a header that is already outside the measured support envelope.
    /// </summary>
    public const int MaxSupportedSampleCount = 16_777_216;

    /// <summary>
    /// ViewerSourceLoadOperationCoordinator already cancels and supersedes a
    /// previous load, so one Viewer admits one active C3D load at a time.
    /// </summary>
    public const int MaxConcurrentViewerLoads = 1;

    public static C3DMemoryAdmissionResult Evaluate(int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            return new C3DMemoryAdmissionResult(
                false,
                0,
                "C3D grid dimensions must be positive.");
        }

        var sampleCount = (long)width * height;
        if (sampleCount > MaxSupportedSampleCount)
        {
            return new C3DMemoryAdmissionResult(
                false,
                sampleCount,
                $"C3D grid contains {sampleCount:N0} cells; the measured Dev admission limit is {MaxSupportedSampleCount:N0} cells.");
        }

        return new C3DMemoryAdmissionResult(
            true,
            sampleCount,
            "C3D grid is within the measured Dev admission envelope.");
    }
}

public readonly record struct C3DMemoryAdmissionResult(
    bool IsAdmitted,
    long SampleCount,
    string Reason);
