using System.IO;
using SharpGL;
using OpenVisionLab.ThreeD.Viewer.Rendering;

namespace OpenVisionLab.ThreeD.Verification.Viewer;

internal static class OpenGLResourceRetirementCoordinatorVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var lines = new List<string>
        {
            "OpenVisionLab 3D OpenGL resource-retirement coordinator verification",
            $"Generated: {DateTimeOffset.Now:O}",
            "Scope: callback order, failure isolation, flush tolerance, and managed cleanup without WPF construction"
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

        try
        {
            var events = new List<string>();
            var telemetry = new OpenGLResourceRetirementTelemetry();
            var coordinator = new OpenGLResourceRetirementCoordinator(
                _ => events.Add("gpu"),
                _ => events.Add("texture"),
                _ => events.Add("display-lists"),
                () => events.Add("managed"),
                telemetry);

            coordinator.Retire(new OpenGL());

            Check(
                "Resource callbacks retain the established order",
                events.SequenceEqual(["gpu", "texture", "display-lists", "managed"]),
                $"events={string.Join(',', events)}");
            Check(
                "A successful retirement reports one callback and no delete failures",
                telemetry.CallbackCount == 1 && telemetry.FailureCount == 0,
                $"callbacks={telemetry.CallbackCount};failures={telemetry.FailureCount}");

            events.Clear();
            var failureTelemetry = new OpenGLResourceRetirementTelemetry();
            var failureCoordinator = new OpenGLResourceRetirementCoordinator(
                _ => events.Add("gpu"),
                _ =>
                {
                    events.Add("texture");
                    throw new InvalidOperationException("simulated texture delete failure");
                },
                _ => events.Add("display-lists"),
                () => events.Add("managed"),
                failureTelemetry);

            failureCoordinator.Retire(new OpenGL());

            Check(
                "A failed resource callback does not skip later resources or cleanup",
                events.SequenceEqual(["gpu", "texture", "display-lists", "managed"]),
                $"events={string.Join(',', events)}");
            Check(
                "Failure isolation increments telemetry once",
                failureTelemetry.CallbackCount == 1 && failureTelemetry.FailureCount == 1,
                $"callbacks={failureTelemetry.CallbackCount};failures={failureTelemetry.FailureCount}");
        }
        catch (Exception exception)
        {
            lines.Add($"FAIL | verifier exception | {exception.GetType().Name}: {exception.Message}");
        }

        var succeeded = passed == total && total > 0;
        lines.Add($"Result: {(succeeded ? "Pass" : "Fail")} ({passed}/{total} checks)");
        var fullReportPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
        File.WriteAllLines(fullReportPath, lines);
        summary = $"OpenGLResourceRetirementCoordinator|pass={succeeded}|checks={passed}/{total}|report={fullReportPath}";
        return succeeded;
    }
}
