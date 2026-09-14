namespace OpenVisionLab.ThreeD.Viewer.Rendering;

/// <summary>
/// Owns pointer-input and render-performance observations for one viewer instance.
/// The WPF control remains responsible for event dispatch, clocks, and rendering;
/// this state can be exercised without creating a window or an OpenGL context.
/// </summary>
internal sealed class ViewerInteractionTelemetry
{
    public int MouseDownCount { get; private set; }

    public int MouseMoveCount { get; private set; }

    public int MouseUpCount { get; private set; }

    public int MouseWheelCount { get; private set; }

    public int MouseMoveTimingCount { get; private set; }

    public double MouseMoveTotalMilliseconds { get; private set; }

    public double MouseMoveMaximumMilliseconds { get; private set; }

    public int NextFrameTimingCount { get; private set; }

    public double NextFrameTotalMilliseconds { get; private set; }

    public double NextFrameMaximumMilliseconds { get; private set; }

    public int ScheduledMouseMoveRenderCount { get; private set; }

    public int ImmediateMouseMoveRenderCount { get; private set; }

    public bool IsHandlingMouseMove { get; private set; }

    public long LastMouseMoveTimestamp { get; private set; }

    public long LastFrameTimestamp { get; private set; }

    public int PerformanceFrameCount { get; private set; }

    public int PerformanceDrawCount { get; private set; }

    public double AccumulatedFrameIntervalMilliseconds { get; private set; }

    public double AccumulatedDrawMilliseconds { get; private set; }

    public void ResetPointerInput()
    {
        MouseDownCount = 0;
        MouseMoveCount = 0;
        MouseUpCount = 0;
        MouseWheelCount = 0;
        MouseMoveTimingCount = 0;
        MouseMoveTotalMilliseconds = 0.0;
        MouseMoveMaximumMilliseconds = 0.0;
        NextFrameTimingCount = 0;
        NextFrameTotalMilliseconds = 0.0;
        NextFrameMaximumMilliseconds = 0.0;
        ScheduledMouseMoveRenderCount = 0;
        ImmediateMouseMoveRenderCount = 0;
        IsHandlingMouseMove = false;
        LastMouseMoveTimestamp = 0;
    }

    public void RecordMouseDown() => MouseDownCount++;

    public void BeginMouseMove(long timestamp, bool measure)
    {
        IsHandlingMouseMove = measure;
        if (!measure)
        {
            return;
        }

        MouseMoveCount++;
        LastMouseMoveTimestamp = timestamp;
    }

    public void CompleteMouseMove(double elapsedMilliseconds, bool measure)
    {
        IsHandlingMouseMove = false;
        if (!measure)
        {
            return;
        }

        MouseMoveTimingCount++;
        MouseMoveTotalMilliseconds += elapsedMilliseconds;
        MouseMoveMaximumMilliseconds = Math.Max(MouseMoveMaximumMilliseconds, elapsedMilliseconds);
    }

    public bool TryTakePendingMouseMoveTimestamp(out long timestamp)
    {
        timestamp = LastMouseMoveTimestamp;
        LastMouseMoveTimestamp = 0;
        return timestamp != 0;
    }

    public void RecordNextFrame(double elapsedMilliseconds)
    {
        NextFrameTimingCount++;
        NextFrameTotalMilliseconds += elapsedMilliseconds;
        NextFrameMaximumMilliseconds = Math.Max(NextFrameMaximumMilliseconds, elapsedMilliseconds);
    }

    public void RecordScheduledMouseMoveRender() => ScheduledMouseMoveRenderCount++;

    public void RecordImmediateMouseMoveRender() => ImmediateMouseMoveRenderCount++;

    public void RecordMouseUp() => MouseUpCount++;

    public void RecordMouseWheel() => MouseWheelCount++;

    public void ResetRenderPerformance()
    {
        LastFrameTimestamp = 0;
        PerformanceFrameCount = 0;
        PerformanceDrawCount = 0;
        AccumulatedFrameIntervalMilliseconds = 0.0;
        AccumulatedDrawMilliseconds = 0.0;
    }

    public void RecordFrameInterval(long timestamp, double elapsedMilliseconds)
    {
        if (LastFrameTimestamp != 0)
        {
            AccumulatedFrameIntervalMilliseconds += elapsedMilliseconds;
            PerformanceFrameCount++;
        }

        LastFrameTimestamp = timestamp;
    }

    public void RecordDraw(double elapsedMilliseconds)
    {
        AccumulatedDrawMilliseconds += elapsedMilliseconds;
        PerformanceDrawCount++;
    }

    public bool TryTakeRenderPerformance(out double averageFramesPerSecond, out double averageDrawMilliseconds)
    {
        if (PerformanceFrameCount < 15 || AccumulatedFrameIntervalMilliseconds <= 0.0)
        {
            averageFramesPerSecond = 0.0;
            averageDrawMilliseconds = 0.0;
            return false;
        }

        var averageFrameInterval = AccumulatedFrameIntervalMilliseconds / PerformanceFrameCount;
        averageFramesPerSecond = 1000.0 / averageFrameInterval;
        averageDrawMilliseconds = AccumulatedDrawMilliseconds / Math.Max(1, PerformanceDrawCount);
        ResetRenderPerformance();
        return true;
    }
}
