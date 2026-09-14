using System.IO;
using OpenVisionLab.ThreeD.Shell;
using OpenVisionLab.ThreeD.Shell.Services;

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
        var reportDirectory = Path.GetDirectoryName(reportPath) ?? Environment.CurrentDirectory;
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
            var resolverRoot = Path.Combine(reportDirectory, "path-resolver-root");
            var resolverNested = Path.Combine(resolverRoot, "nested", "fixture");
            Directory.CreateDirectory(resolverNested);
            File.WriteAllText(Path.Combine(resolverRoot, "OpenVisionLab.ThreeDStudio.slnx"), string.Empty);
            var discoveredRoot = ShellEvidencePathResolver.ResolveWorkspaceRoot(resolverNested);
            Check(
                "evidence-path-resolver-finds-solution-ancestor",
                string.Equals(discoveredRoot, Path.GetFullPath(resolverRoot), StringComparison.OrdinalIgnoreCase),
                $"discovered={discoveredRoot}");
            var fallbackRoot = Path.Combine(reportDirectory, "path-resolver-fallback");
            Directory.CreateDirectory(fallbackRoot);
            var fallback = ShellEvidencePathResolver.ResolveWorkspaceRoot(fallbackRoot);
            Check(
                "evidence-path-resolver-falls-back-to-start-directory",
                string.Equals(fallback, Path.GetFullPath(fallbackRoot), StringComparison.OrdinalIgnoreCase),
                $"fallback={fallback}");
            var relative = ShellEvidencePathResolver.ResolvePath(resolverRoot, "artifacts/run.json", "fallback.json");
            var absolute = Path.Combine(reportDirectory, "absolute-run.json");
            Check(
                "evidence-path-resolver-preserves-relative-and-absolute-contract",
                string.Equals(relative, Path.Combine(resolverRoot, "artifacts/run.json"), StringComparison.OrdinalIgnoreCase)
                    && string.Equals(ShellEvidencePathResolver.ResolvePath(resolverRoot, absolute, "fallback.json"), absolute, StringComparison.OrdinalIgnoreCase)
                    && ShellEvidencePathResolver.ResolveOptionalPath(resolverRoot, null) is null,
                $"relative={relative}; absolute={absolute}");
            var parsedRecipePath = ShellEvidenceTextParser.ExtractRecipePath(
                resolverRoot,
                ["Recipe|path=recipes/example.recipe.json"]);
            Check(
                "evidence-text-parser-reuses-path-owner",
                string.Equals(parsedRecipePath, Path.Combine(resolverRoot, "recipes/example.recipe.json"), StringComparison.OrdinalIgnoreCase),
                $"parsed={parsedRecipePath}");
            var relativeScreenshot = ShellEvidenceTextParser.FormatScreenshotTarget(resolverRoot, "screenshots/capture.png");
            var absoluteScreenshotPath = Path.Combine(resolverRoot, "screenshots", "absolute.png");
            var absoluteScreenshot = ShellEvidenceTextParser.FormatScreenshotTarget(resolverRoot, absoluteScreenshotPath);
            Check(
                "evidence-text-parser-screenshot-path-owner",
                relativeScreenshot.Equals(Path.Combine("screenshots", "capture.png"), StringComparison.OrdinalIgnoreCase)
                    && absoluteScreenshot.Equals(Path.Combine("screenshots", "absolute.png"), StringComparison.OrdinalIgnoreCase)
                    && ShellEvidenceTextParser.FormatScreenshotTarget(resolverRoot, " ") == "(not requested)",
                $"relative={relativeScreenshot}; absolute={absoluteScreenshot}; blank={ShellEvidenceTextParser.FormatScreenshotTarget(resolverRoot, " ")}");
            viewModel = new ShellMainWindowViewModel(
                recentRunRecordsPath: Path.Combine(reportDirectory, "recent-run-records.json"),
                recentRecipesPath: Path.Combine(reportDirectory, "recent-recipes.json"));
            Check("constructed-undisposed", !viewModel.IsDisposed, "isDisposed=false");
            Check("async-dispose-contract", viewModel is IAsyncDisposable, "IAsyncDisposable implemented");

            var firstDispose = viewModel.DisposeAsync().AsTask();
            var secondDispose = viewModel.DisposeAsync().AsTask();
            Check(
                "dispose-shares-completion",
                ReferenceEquals(firstDispose, secondDispose),
                "repeated callers share one cleanup task");
            Task.WhenAll(firstDispose, secondDispose).GetAwaiter().GetResult();
            Check("dispose-releases-subscriptions", viewModel.IsDisposed, "isDisposed=true");
            Check(
                "dispose-releases-integration-child",
                viewModel.IntegrationExchange.IsDisposed,
                "integrationExchange.isDisposed=true");

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
        Directory.CreateDirectory(reportDirectory);
        File.WriteAllLines(reportPath, lines);
        summary = $"ShellMainWindowViewModelLifecycle|pass={passed}|checks={passedChecks}/{totalChecks}|report={reportPath}";
        return true;
    }
}
