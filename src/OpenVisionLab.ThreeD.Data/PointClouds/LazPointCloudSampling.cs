namespace OpenVisionLab.ThreeD.Data;

/// <summary>
/// Defines the deterministic display-sampling policy for decoded LAS/LAZ
/// points. The policy covers the complete source index range and is not a
/// spatial or metrology resampling algorithm.
/// </summary>
internal static class LazPointCloudSampling
{
    public static ulong GetSampleCount(ulong pointCount, int maxSampledPoints)
    {
        if (maxSampledPoints < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxSampledPoints));
        }

        return Math.Min(pointCount, (ulong)maxSampledPoints);
    }

    public static ulong GetSampleIndex(ulong sampleOrdinal, ulong sampleCount, ulong pointCount)
    {
        if (sampleCount == 0 || sampleCount > pointCount || sampleOrdinal >= sampleCount)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleOrdinal));
        }

        if (sampleCount == 1)
        {
            return pointCount / 2;
        }

        return (ulong)(((UInt128)sampleOrdinal * (pointCount - 1)) / (sampleCount - 1));
    }

    public static int GetReportedStride(ulong pointCount, ulong sampleCount)
    {
        if (sampleCount == 0)
        {
            return 1;
        }

        var stride = ((pointCount - 1) / sampleCount) + 1;
        return (int)Math.Min((ulong)int.MaxValue, stride);
    }
}
