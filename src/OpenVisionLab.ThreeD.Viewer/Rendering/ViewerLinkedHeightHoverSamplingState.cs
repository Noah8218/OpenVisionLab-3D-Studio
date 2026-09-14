using System.Diagnostics;

namespace OpenVisionLab.ThreeD.Viewer.Rendering;

/// <summary>
/// Owns the time-and-distance gate used by linked Height Image/3D hover
/// sampling. Screen coordinates remain a View concern; this owner only keeps
/// the last accepted sample and its deterministic admission rule.
/// </summary>
internal sealed class ViewerLinkedHeightHoverSamplingState
{
    public const double MinimumIntervalMilliseconds = 24.0;
    public const double MinimumDistance = 2.0;

    private long lastSampleTimestamp;
    private double lastSampleX;
    private double lastSampleY;

    public bool TryAccept(double x, double y, long timestamp)
    {
        if (lastSampleTimestamp != 0)
        {
            var elapsed = Stopwatch.GetElapsedTime(lastSampleTimestamp, timestamp).TotalMilliseconds;
            var deltaX = x - lastSampleX;
            var deltaY = y - lastSampleY;
            var distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
            if (elapsed < MinimumIntervalMilliseconds && distance < MinimumDistance)
            {
                return false;
            }
        }

        lastSampleTimestamp = timestamp;
        lastSampleX = x;
        lastSampleY = y;
        return true;
    }

    public void Reset()
    {
        lastSampleTimestamp = 0;
        lastSampleX = 0;
        lastSampleY = 0;
    }
}
