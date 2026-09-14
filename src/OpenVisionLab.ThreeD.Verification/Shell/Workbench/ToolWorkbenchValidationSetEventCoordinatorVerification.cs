using System.ComponentModel;
using System.IO;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Verification.Shell.Workbench;

internal static class ToolWorkbenchValidationSetEventCoordinatorVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var lines = new List<string>
        {
            "OpenVisionLab 3D ToolWorkbench Validation Set event coordinator verification",
            $"Generated: {DateTimeOffset.Now:O}",
            "Scope: definition, review, threshold notification routing and disposal"
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
            var definition = new PropertyChangedSource();
            var review = new PropertyChangedSource();
            var threshold = new PropertyChangedSource();
            var definitionCallbacks = 0;
            var reviewCallbacks = 0;
            var thresholdCallbacks = 0;
            using var coordinator = new ToolWorkbenchValidationSetEventCoordinator(
                definition,
                review,
                threshold,
                _ => definitionCallbacks++,
                _ => reviewCallbacks++,
                _ => thresholdCallbacks++);

            definition.Raise("Samples");
            review.Raise("SelectedValidationSetSample");
            threshold.Raise("ValidationThresholdSummary");
            Check(
                "Definition owner notification reaches its callback",
                definitionCallbacks == 1,
                $"callbacks={definitionCallbacks}");
            Check(
                "Review owner notification reaches its callback",
                reviewCallbacks == 1,
                $"callbacks={reviewCallbacks}");
            Check(
                "Threshold owner notification reaches its callback",
                thresholdCallbacks == 1,
                $"callbacks={thresholdCallbacks}");

            coordinator.Dispose();
            coordinator.Dispose();
            definition.Raise("Samples");
            review.Raise("SelectedValidationSetSample");
            threshold.Raise("ValidationThresholdSummary");
            Check(
                "Dispose is idempotent and suppresses all later callbacks",
                definitionCallbacks == 1
                && reviewCallbacks == 1
                && thresholdCallbacks == 1,
                $"definition={definitionCallbacks}; review={reviewCallbacks}; threshold={thresholdCallbacks}");
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
        summary = $"ToolWorkbenchValidationSetEventCoordinator|pass={succeeded}|checks={passed}/{total}|report={fullReportPath}";
        return succeeded;
    }

    private sealed class PropertyChangedSource : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public void Raise(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
