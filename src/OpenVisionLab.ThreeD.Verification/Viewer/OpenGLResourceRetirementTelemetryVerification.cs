using System.IO;
using OpenVisionLab.ThreeD.Viewer.Rendering;

namespace OpenVisionLab.ThreeD.Verification.Viewer;

internal static class OpenGLResourceRetirementTelemetryVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var lines = new List<string>
        {
            "OpenVisionLab 3D OpenGL resource-retirement telemetry verification",
            $"Generated: {DateTimeOffset.Now:O}",
            "Scope: cumulative retirement observations without WPF/OpenGL construction"
        };
        var passed = 0;
        var total = 0;
        Exception? failure = null;

        void Check(string name, bool condition, string detail)
        {
            total++;
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
            if (condition)
            {
                passed++;
            }
        }

        try
        {
            var telemetry = new OpenGLResourceRetirementTelemetry();
            Check(
                "Fresh telemetry starts at zero",
                telemetry.AttemptCount == 0
                && telemetry.CallbackCount == 0
                && telemetry.ContextUnavailableCount == 0
                && telemetry.FailureCount == 0,
                $"attempts={telemetry.AttemptCount}; callbacks={telemetry.CallbackCount}; contextUnavailable={telemetry.ContextUnavailableCount}; failures={telemetry.FailureCount}");

            telemetry.RecordAttempt();
            telemetry.RecordAttempt();
            telemetry.RecordCallback();
            telemetry.RecordContextUnavailable();
            telemetry.RecordFailure();
            telemetry.RecordFailure();
            Check(
                "Each boundary observation is accumulated independently",
                telemetry.AttemptCount == 2
                && telemetry.CallbackCount == 1
                && telemetry.ContextUnavailableCount == 1
                && telemetry.FailureCount == 2,
                $"attempts={telemetry.AttemptCount}; callbacks={telemetry.CallbackCount}; contextUnavailable={telemetry.ContextUnavailableCount}; failures={telemetry.FailureCount}");

            var replacement = new OpenGLResourceRetirementTelemetry();
            Check(
                "Replacement owner does not inherit prior observations",
                replacement.AttemptCount == 0
                && replacement.CallbackCount == 0
                && replacement.ContextUnavailableCount == 0
                && replacement.FailureCount == 0,
                $"attempts={replacement.AttemptCount}; callbacks={replacement.CallbackCount}; contextUnavailable={replacement.ContextUnavailableCount}; failures={replacement.FailureCount}");
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        if (failure is not null)
        {
            lines.Add($"FAIL | verifier exception | {failure.GetType().Name}: {failure.Message}");
        }

        var succeeded = failure is null && passed == total && total > 0;
        lines.Add($"Result: {(succeeded ? "Pass" : "Fail")} ({passed}/{total} checks)");
        var fullReportPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
        File.WriteAllLines(fullReportPath, lines);
        summary = $"OpenGLResourceRetirementTelemetry|pass={succeeded}|checks={passed}/{total}|report={fullReportPath}";
        return succeeded;
    }
}
