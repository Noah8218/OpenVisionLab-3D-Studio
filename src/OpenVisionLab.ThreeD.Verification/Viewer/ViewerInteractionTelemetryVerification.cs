using System.IO;
using OpenVisionLab.ThreeD.Viewer.Rendering;

namespace OpenVisionLab.ThreeD.Verification.Viewer;

internal static class ViewerInteractionTelemetryVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var lines = new List<string>
        {
            "OpenVisionLab 3D Viewer interaction telemetry verification",
            $"Generated: {DateTimeOffset.Now:O}"
        };
        var passed = 0;
        var total = 0;

        void Check(string name, bool condition, string detail)
        {
            total++;
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
            if (condition)
            {
                passed++;
            }
        }

        var telemetry = new ViewerInteractionTelemetry();
        telemetry.RecordMouseDown();
        telemetry.RecordMouseUp();
        telemetry.RecordMouseWheel();
        telemetry.BeginMouseMove(100, measure: true);
        Check(
            "pointer event counters and active move state are owned together",
            telemetry.MouseDownCount == 1
                && telemetry.MouseUpCount == 1
                && telemetry.MouseWheelCount == 1
                && telemetry.MouseMoveCount == 1
                && telemetry.IsHandlingMouseMove
                && telemetry.LastMouseMoveTimestamp == 100,
            $"down={telemetry.MouseDownCount}|move={telemetry.MouseMoveCount}|up={telemetry.MouseUpCount}|wheel={telemetry.MouseWheelCount}|active={telemetry.IsHandlingMouseMove}");

        telemetry.CompleteMouseMove(4.5, measure: true);
        Check(
            "move timing records total and maximum after handling completes",
            !telemetry.IsHandlingMouseMove
                && telemetry.MouseMoveTimingCount == 1
                && telemetry.MouseMoveTotalMilliseconds == 4.5
                && telemetry.MouseMoveMaximumMilliseconds == 4.5,
            $"timings={telemetry.MouseMoveTimingCount}|total={telemetry.MouseMoveTotalMilliseconds}|max={telemetry.MouseMoveMaximumMilliseconds}");

        var pendingTimestampAvailable = telemetry.TryTakePendingMouseMoveTimestamp(out var pendingTimestamp);
        Check(
            "pending move timestamp is consumed once by the render loop",
            pendingTimestampAvailable
                && pendingTimestamp == 100
                && !telemetry.TryTakePendingMouseMoveTimestamp(out _),
            $"available={pendingTimestampAvailable}|timestamp={pendingTimestamp}");

        telemetry.RecordNextFrame(8.0);
        telemetry.RecordScheduledMouseMoveRender();
        telemetry.RecordImmediateMouseMoveRender();
        Check(
            "next-frame and render-path observations are counted explicitly",
            telemetry.NextFrameTimingCount == 1
                && telemetry.NextFrameTotalMilliseconds == 8.0
                && telemetry.NextFrameMaximumMilliseconds == 8.0
                && telemetry.ScheduledMouseMoveRenderCount == 1
                && telemetry.ImmediateMouseMoveRenderCount == 1,
            $"nextFrame={telemetry.NextFrameTimingCount}|scheduled={telemetry.ScheduledMouseMoveRenderCount}|immediate={telemetry.ImmediateMouseMoveRenderCount}");

        telemetry.BeginMouseMove(200, measure: false);
        telemetry.CompleteMouseMove(3.0, measure: false);
        Check(
            "unmeasured pointer handling does not contaminate regression timing",
            !telemetry.IsHandlingMouseMove
                && telemetry.MouseMoveCount == 1
                && telemetry.MouseMoveTimingCount == 1,
            $"move={telemetry.MouseMoveCount}|timings={telemetry.MouseMoveTimingCount}");

        telemetry.ResetPointerInput();
        Check(
            "pointer reset clears counters, pending timestamp, and active state",
            telemetry.MouseDownCount == 0
                && telemetry.MouseMoveCount == 0
                && telemetry.MouseUpCount == 0
                && telemetry.MouseWheelCount == 0
                && telemetry.MouseMoveTimingCount == 0
                && telemetry.NextFrameTimingCount == 0
                && telemetry.ScheduledMouseMoveRenderCount == 0
                && telemetry.ImmediateMouseMoveRenderCount == 0
                && !telemetry.IsHandlingMouseMove
                && telemetry.LastMouseMoveTimestamp == 0,
            $"down={telemetry.MouseDownCount}|move={telemetry.MouseMoveCount}|nextFrame={telemetry.NextFrameTimingCount}");

        telemetry.RecordFrameInterval(100, 0.0);
        telemetry.RecordDraw(2.0);
        for (var index = 0; index < 15; index++)
        {
            telemetry.RecordFrameInterval(101 + index, 10.0);
            telemetry.RecordDraw(2.0);
        }

        var performanceAvailable = telemetry.TryTakeRenderPerformance(
            out var averageFramesPerSecond,
            out var averageDrawMilliseconds);
        Check(
            "render performance is emitted only after the frame sample threshold",
            performanceAvailable
                && Math.Abs(averageFramesPerSecond - 100.0) < 0.0001
                && Math.Abs(averageDrawMilliseconds - 2.0) < 0.0001,
            $"available={performanceAvailable}|fps={averageFramesPerSecond}|drawMs={averageDrawMilliseconds}");
        Check(
            "consuming render performance resets the rolling sample",
            telemetry.LastFrameTimestamp == 0
                && telemetry.PerformanceFrameCount == 0
                && telemetry.PerformanceDrawCount == 0
                && telemetry.AccumulatedFrameIntervalMilliseconds == 0.0
                && telemetry.AccumulatedDrawMilliseconds == 0.0,
            $"frames={telemetry.PerformanceFrameCount}|draws={telemetry.PerformanceDrawCount}");

        telemetry.ResetRenderPerformance();
        telemetry.RecordFrameInterval(300, 0.0);
        for (var index = 0; index < 15; index++)
        {
            telemetry.RecordFrameInterval(301 + index, 0.0);
        }

        Check(
            "zero-duration frame samples remain pending instead of producing invalid FPS",
            !telemetry.TryTakeRenderPerformance(out _, out _)
                && telemetry.PerformanceFrameCount == 15,
            $"frames={telemetry.PerformanceFrameCount}|intervalMs={telemetry.AccumulatedFrameIntervalMilliseconds}");

        telemetry.ResetRenderPerformance();
        Check(
            "render reset is independent from pointer counters",
            telemetry.PerformanceFrameCount == 0
                && telemetry.PerformanceDrawCount == 0
                && telemetry.MouseDownCount == 0
                && telemetry.MouseMoveCount == 0,
            $"frames={telemetry.PerformanceFrameCount}|pointerMove={telemetry.MouseMoveCount}");

        summary = $"Viewer interaction telemetry verification: {(passed == total ? "Pass" : "Fail")} ({passed}/{total} checks)";
        lines.Add(summary);
        var fullReportPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
        File.WriteAllLines(fullReportPath, lines);
        return passed == total;
    }
}
