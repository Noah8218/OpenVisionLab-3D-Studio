using System.IO;
using OpenVisionLab.ThreeD.Shell;
using OpenVisionLab.ThreeD.Shell.Services;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

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

            var inputPath = Path.Combine(reportDirectory, "mismatch-settings-source.c3d");
            C3DHeightFieldSnapshot.CreateForVerification("mismatch.settings", 4, 4, Enumerable.Range(0, 16).Select(value => (double)value).ToArray()).SaveC3D(inputPath);
            var identity = ToolRecipeSelectionSourceBindingVerifier.ReadIdentity(inputPath);
            var source = new ToolRecipeSource("mismatch.settings", "Settings fixture", "C3D", "model", "frame.c3d-grid-index", inputPath,
                new FileInfo(inputPath).Length, identity.ContentSha256, identity.GridWidth, identity.GridHeight);
            var reference = new ToolRecipeSelection("selection.reference", "Reference", ToolRecipeSelectionKinds.GridRectangle, source.Id, source.FrameId, identity, new ToolRecipeGridRectangle(0, 0, 2, 4), null, null);
            var measurement = new ToolRecipeSelection("selection.measurement", "Measurement", ToolRecipeSelectionKinds.GridRectangle, source.Id, source.FrameId, identity, new ToolRecipeGridRectangle(2, 0, 2, 4), null, null);
            var step = new ToolRecipeStep("step.settings", "thickness", "Recorded thickness", 3, [source.Id, reference.Id, measurement.Id], "result.thickness",
                [new ToolRecipeParameter("MinimumThickness", "0"), new ToolRecipeParameter("MaximumThickness", "10"), new ToolRecipeParameter("MinimumValidSampleCount", "1")]);
            var recipePath = Path.Combine(reportDirectory, "mismatch-settings.ov3d-recipe.json");
            ToolRecipeDocumentStore.Save(recipePath, new ToolRecipeDocument(ToolRecipeDocument.CurrentSchemaVersion, "Mismatch settings fixture", source, [], [step], [reference, measurement]));
            Check("mismatch-settings-authored-recipe-opens", viewModel.Workbench.TryOpenTeachingRecipe(recipePath, out var openMessage), openMessage);
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var workspace = (ValidationSetWorkspaceViewModel)typeof(ToolWorkbenchViewModel).GetField("validationSetWorkspace", flags)!.GetValue(viewModel.Workbench)!;
            var definition = (ToolWorkbenchValidationSetDefinitionOwner)typeof(ValidationSetWorkspaceViewModel).GetField("validationSetDefinitionOwner", flags)!.GetValue(workspace)!;
            var issue = new ValidationSetSampleRow(1, inputPath, ToolRecipeValidationSampleRole.Good, "Fail", "Fail", "Recorded failure", "",
                [new ValidationSetStepRow(1, step.Id, step.ToolName, "Fail", "Fail", "Recorded range violation", [new ValidationSetMetricRow("Mean", "13", "mm", "Fail", "Fail", 13)], [])]);
            var other = issue with { Order = 2, Role = ToolRecipeValidationSampleRole.Bad, Status = "Pass", StatusText = "Pass", Steps = [] };
            definition.ReplaceExecutionResult([issue, other]);
            workspace.RefreshValidationSetLocalization();
            viewModel.Workbench.SelectedValidationSetSample = other;
            viewModel.SelectWorkspaceCommand.Execute(ShellWorkspaceMode.Validate);
            issue = workspace.AllSamples.Single(row => row.Order == 1);
            other = workspace.AllSamples.Single(row => row.Order == 2);
            var beforeDirty = viewModel.Workbench.IsDirty;
            var beforeLogs = viewModel.Workbench.RunLog.Count;
            var beforeRows = workspace.AllSamples.ToArray();
            Check("unselected-card-settings-command-target-is-enabled", viewModel.OpenSelectedValidationIssueInTeachCommand.CanExecute(issue),  $"rowGuard={viewModel.Workbench.SelectValidationMismatchSampleCommand.CanExecute(issue)};mode={viewModel.SelectedWorkspaceMode};selectedStep={issue.MismatchStep?.StepId};steps={string.Join(',', viewModel.Workbench.PipelineSteps.Select(item => item.Id))};navigation={viewModel.InspectionStageNavigationStatus}");
            viewModel.OpenSelectedValidationIssueInTeachCommand.Execute(issue);
            Check("unselected-card-settings-routes-its-own-step-without-run-or-save", viewModel.SelectedWorkspaceMode == ShellWorkspaceMode.Teach
                && viewModel.Workbench.SelectedPipelineStep?.Id == step.Id && viewModel.Workbench.SelectedValidationSetSample?.Order == issue.Order
                && workspace.ActiveValidationFailureCorrectionContext?.StepId == step.Id && viewModel.Workbench.IsDirty == beforeDirty
                && viewModel.Workbench.RunLog.Count == beforeLogs && workspace.AllSamples.SequenceEqual(beforeRows), "row-aware public Shell command; Window0");
            var removed = issue with { Steps = [issue.Steps[0] with { StepId = "step.removed" }] };
            definition.ReplaceExecutionResult([removed, other]);
            workspace.RefreshValidationSetLocalization();
            removed = workspace.AllSamples.Single(row => row.Order == 1);
            other = workspace.AllSamples.Single(row => row.Order == 2);
            Check("settings-rejects-missing-step-and-unestablished-miss-cause", !viewModel.OpenSelectedValidationIssueInTeachCommand.CanExecute(removed)
                && !viewModel.OpenSelectedValidationIssueInTeachCommand.CanExecute(other), "No inferred cause or accidental currently-selected step fallback");

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
