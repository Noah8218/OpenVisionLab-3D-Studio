using System.IO;
using OpenVisionLab.ThreeD.Viewer.Rendering;

namespace OpenVisionLab.ThreeD.Verification.Viewer;

internal static class C3DSourceApplyRenderStateVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var lines = new List<string>
        {
            "OpenVisionLab 3D C3D source-apply render state verification",
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

        var state = new C3DSourceApplyRenderState();
        Check(
            "inactive state does not suppress or count requests",
            !state.TrySuppressRenderRequest()
                && state.RenderRequestCount == 0
                && state.SuppressedRenderRequestCount == 0,
            $"active={state.IsActive}|requests={state.RenderRequestCount}");

        state.Begin();
        Check(
            "begin opens a fresh suppressed apply window",
            state.IsActive
                && state.IsRenderSuppressed
                && state.RenderRequestCount == 0
                && state.SuppressedRenderRequestCount == 0
                && state.RenderExecutionCount == 0
                && state.RenderExecutionMilliseconds == 0.0,
            $"active={state.IsActive}|suppressed={state.IsRenderSuppressed}");

        var firstRequestSuppressed = state.TrySuppressRenderRequest();
        var secondRequestSuppressed = state.TrySuppressRenderRequest();
        Check(
            "requests during the guarded stage are suppressed and counted",
            firstRequestSuppressed
                && secondRequestSuppressed
                && state.RenderRequestCount == 2
                && state.SuppressedRenderRequestCount == 2,
            $"requests={state.RenderRequestCount}|suppressed={state.SuppressedRenderRequestCount}");

        state.AllowRender();
        var finalRequestSuppressed = state.TrySuppressRenderRequest();
        state.RecordRenderExecution(3.25);
        state.RecordRenderExecution(1.75);
        Check(
            "allowing the final render preserves request and execution evidence",
            !state.IsRenderSuppressed
                && !finalRequestSuppressed
                && state.RenderRequestCount == 3
                && state.SuppressedRenderRequestCount == 2
                && state.RenderExecutionCount == 2
                && state.RenderExecutionMilliseconds == 5.0,
            $"requests={state.RenderRequestCount}|suppressed={state.SuppressedRenderRequestCount}|executions={state.RenderExecutionCount}|ms={state.RenderExecutionMilliseconds}");

        state.Complete();
        Check(
            "complete closes admission without erasing the apply evidence",
            !state.IsActive
                && !state.IsRenderSuppressed
                && state.RenderRequestCount == 3
                && state.SuppressedRenderRequestCount == 2
                && state.RenderExecutionCount == 2
                && state.RenderExecutionMilliseconds == 5.0
                && !state.TrySuppressRenderRequest(),
            $"active={state.IsActive}|requests={state.RenderRequestCount}|executions={state.RenderExecutionCount}");

        state.Begin();
        Check(
            "a replacement apply starts with no stale counters",
            state.IsActive
                && state.IsRenderSuppressed
                && state.RenderRequestCount == 0
                && state.SuppressedRenderRequestCount == 0
                && state.RenderExecutionCount == 0
                && state.RenderExecutionMilliseconds == 0.0,
            $"requests={state.RenderRequestCount}|executions={state.RenderExecutionCount}");

        state.RecordRenderExecution(9.0);
        state.Complete();
        Check(
            "execution is ignored after the apply window closes",
            state.RenderExecutionCount == 1
                && state.RenderExecutionMilliseconds == 9.0,
            $"executions={state.RenderExecutionCount}|ms={state.RenderExecutionMilliseconds}");

        summary = $"C3D source-apply render state verification: {(passed == total ? "Pass" : "Fail")} ({passed}/{total} checks)";
        lines.Add(summary);
        var fullReportPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
        File.WriteAllLines(fullReportPath, lines);
        return passed == total;
    }
}
