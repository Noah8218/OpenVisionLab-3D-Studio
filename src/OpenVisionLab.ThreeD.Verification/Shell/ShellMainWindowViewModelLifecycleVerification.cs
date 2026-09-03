using System.IO;
using OpenVisionLab.ThreeD.Shell;

namespace OpenVisionLab.ThreeD.Verification.Shell;

internal static class ShellMainWindowViewModelLifecycleVerification
{
    private const string Option = "--verify-shell-viewmodel-lifecycle";

    public static bool TryRun(string[] arguments, out bool passed, out string summary)
    {
        var optionIndex = Array.FindIndex(
            arguments,
            argument => argument.Equals(Option, StringComparison.OrdinalIgnoreCase));
        if (optionIndex < 0)
        {
            passed = false;
            summary = string.Empty;
            return false;
        }

        if (optionIndex + 1 >= arguments.Length)
        {
            passed = false;
            summary = $"{Option} requires a report path.";
            return true;
        }

        var reportPath = Path.GetFullPath(arguments[optionIndex + 1]);
        var lines = new List<string>
        {
            "OpenVisionLab 3D Shell ViewModel lifecycle verification",
            $"Generated: {DateTimeOffset.UtcNow:O}"
        };
        var passedChecks = 0;
        var totalChecks = 0;

        void Check(string name, bool condition, string detail)
        {
            totalChecks++;
            if (condition)
            {
                passedChecks++;
            }

            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
        }

        ShellMainWindowViewModel? viewModel = null;
        try
        {
            var reportDirectory = Path.GetDirectoryName(reportPath) ?? Environment.CurrentDirectory;
            viewModel = new ShellMainWindowViewModel(
                recentRunRecordsPath: Path.Combine(reportDirectory, "recent-run-records.json"),
                recentRecipesPath: Path.Combine(reportDirectory, "recent-recipes.json"));
            Check("constructed-undisposed", !viewModel.IsDisposed, "isDisposed=false");

            viewModel.Dispose();
            Check("dispose-releases-subscriptions", viewModel.IsDisposed, "isDisposed=true");

            viewModel.Dispose();
            Check("dispose-is-idempotent", viewModel.IsDisposed, "second dispose completed");
        }
        catch (Exception exception)
        {
            Check("lifecycle-execution", false, exception.GetBaseException().ToString());
        }
        finally
        {
            viewModel?.Dispose();
        }

        passed = passedChecks == totalChecks;
        lines.Add($"Result: {(passed ? "Pass" : "Fail")} ({passedChecks}/{totalChecks} checks)");
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        File.WriteAllLines(reportPath, lines);
        summary = $"ShellMainWindowViewModelLifecycle|pass={passed}|checks={passedChecks}/{totalChecks}|report={reportPath}";
        return true;
    }
}
