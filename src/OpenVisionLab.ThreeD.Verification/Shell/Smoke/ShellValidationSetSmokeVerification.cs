using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell.Verification.Smoke;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Verification.Shell.Smoke;

internal static class ShellValidationSetSmokeVerification
{
    public static bool Verify(
        string artifactDirectory,
        string reportPath,
        out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);

        var root = Path.GetFullPath(artifactDirectory);
        Directory.CreateDirectory(root);
        var lines = new List<string>
        {
            "Shell Validation Set Smoke owner verification",
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

        try
        {
            var sourcePath = Path.Combine(root, "validation-source.C3D");
            C3DHeightFieldSnapshot.CreateForVerification(
                "source.shell-validation-set-smoke",
                3,
                3,
                [1, 2, 3, 2, 3, 4, 3, 4, 5])
                .SaveC3D(sourcePath);
            var sourceInfo = new FileInfo(sourcePath);
            var sourceBinding =
                ToolRecipeSelectionSourceBindingVerifier.ReadIdentity(sourcePath);
            var recipePath = Path.Combine(
                root,
                "validation-set-smoke.ov3d-recipe.json");
            ToolRecipeDocumentStore.Save(
                recipePath,
                new ToolRecipeDocument(
                    ToolRecipeDocument.CurrentSchemaVersion,
                    "Shell Validation Set Smoke",
                    new ToolRecipeSource(
                        "source.shell-validation-set-smoke",
                        "Validation Set Smoke source",
                        "C3D",
                        "raw-height",
                        "frame.c3d-grid-index",
                        sourcePath,
                        sourceInfo.Length,
                        sourceBinding.ContentSha256,
                        sourceBinding.GridWidth,
                        sourceBinding.GridHeight),
                    [],
                    []));

            var noOpWorkbench = new ToolWorkbenchViewModel(
                Path.Combine(root, "no-op-recent.json"));
            var noOpActivationCount = 0;
            var noOpState = ShellValidationSetSmoke.Configure(
                ["verification"],
                noOpWorkbench,
                () => noOpActivationCount++,
                () => { },
                () => { },
                _ => Task.CompletedTask,
                _ => false);
            Check(
                "no validation flags remain a no-op",
                ReferenceEquals(
                    noOpState,
                    ShellValidationSetSmokeState.Empty)
                && noOpActivationCount == 0
                && noOpWorkbench.ValidationSetSamples.Count == 0,
                $"stateEmpty={ReferenceEquals(noOpState, ShellValidationSetSmokeState.Empty)};activations={noOpActivationCount};samples={noOpWorkbench.ValidationSetSamples.Count}");

            var incompleteWorkbench = new ToolWorkbenchViewModel(
                Path.Combine(root, "incomplete-recent.json"));
            var incompleteState = ShellValidationSetSmoke.Configure(
                [
                    "verification",
                    "--smoke-validation-set-recipe",
                    recipePath
                ],
                incompleteWorkbench,
                () => { },
                () => { },
                () => { },
                _ => Task.CompletedTask,
                _ => false);
            Check(
                "recipe without sources fails closed as an unconfigured route",
                ReferenceEquals(
                    incompleteState,
                    ShellValidationSetSmokeState.Empty)
                && incompleteWorkbench.ValidationSetSamples.Count == 0
                && string.IsNullOrWhiteSpace(incompleteWorkbench.RecipePath),
                $"stateEmpty={ReferenceEquals(incompleteState, ShellValidationSetSmokeState.Empty)};samples={incompleteWorkbench.ValidationSetSamples.Count};recipe={incompleteWorkbench.RecipePath}");

            var workbench = new ToolWorkbenchViewModel(
                Path.Combine(root, "configured-recent.json"));
            var activationCount = 0;
            var evidenceExpansionCount = 0;
            var thresholdExpansionCount = 0;
            var evidenceExpandedAtCallback = false;
            var thresholdExpandedAtCallback = false;
            var activeSection = ValidationWorkspaceSection.Samples;
            var recipeOpened = workbench.TryOpenTeachingRecipe(
                recipePath,
                out var openMessage);
            var beforeRecipePath = workbench.RecipePath;
            var beforeSourcePath = workbench.Source.Path;
            var beforeStepCount = workbench.PipelineSteps.Count;
            var beforeRunLogCount = workbench.RunLog.Count;
            var state = ShellValidationSetSmoke.Configure(
                [
                    "verification",
                    "--smoke-validation-set-recipe",
                    recipePath,
                    "--smoke-validation-set-sources",
                    sourcePath,
                    "--smoke-validation-set-roles",
                    "Bad",
                    "--smoke-validation-set-expand-evidence",
                    "--smoke-validation-set-expand-thresholds",
                    "--smoke-validation-section",
                    "Thresholds"
                ],
                workbench,
                () => activationCount++,
                () =>
                {
                    evidenceExpansionCount++;
                    workbench.IsValidationEvidenceExpanded = true;
                    evidenceExpandedAtCallback = workbench.IsValidationEvidenceExpanded;
                },
                () =>
                {
                    thresholdExpansionCount++;
                    workbench.IsValidationThresholdExpanded = true;
                    thresholdExpandedAtCallback = workbench.IsValidationThresholdExpanded;
                },
                section =>
                {
                    activeSection = section;
                    return Task.CompletedTask;
                },
                section => activeSection == section);
            state.SectionSelectionTask?.GetAwaiter().GetResult();
            var selected = workbench.ValidationSetSamples.SingleOrDefault();
            Check(
                "configured setup assigns sources and roles without running inspection",
                recipeOpened
                && string.Equals(
                    workbench.RecipePath,
                    beforeRecipePath,
                    StringComparison.OrdinalIgnoreCase)
                && string.Equals(
                    workbench.Source.Path,
                    beforeSourcePath,
                    StringComparison.OrdinalIgnoreCase)
                && workbench.ValidationSetSamples.Count == 1
                && selected?.Role == ToolRecipeValidationSampleRole.Bad
                && selected.Status == "Pending"
                && workbench.PipelineSteps.Count == beforeStepCount
                && !workbench.IsValidationSetRunning
                && workbench.RunLog.Count >= beforeRunLogCount,
                $"open={recipeOpened};openMessage={openMessage};recipe={workbench.RecipePath};source={workbench.Source.Path};samples={workbench.ValidationSetSamples.Count};role={selected?.Role};status={selected?.Status};steps={workbench.PipelineSteps.Count}/{beforeStepCount};running={workbench.IsValidationSetRunning};logs={workbench.RunLog.Count}/{beforeRunLogCount}");
            Check(
                "setup activates the requested Validation Set presentation and section",
                !state.RunRequested
                && state.ThresholdSelectionTask is null
                && state.SectionSelectionTask is not null
                && activationCount == 1
                && evidenceExpansionCount == 1
                && thresholdExpansionCount == 1
                && activeSection == ValidationWorkspaceSection.Thresholds
                && workbench.IsValidationEvidenceExpanded
                && workbench.IsValidationThresholdExpanded,
                $"run={state.RunRequested};thresholdTask={state.ThresholdSelectionTask is not null};sectionTask={state.SectionSelectionTask is not null};activations={activationCount};evidence={evidenceExpansionCount};thresholds={thresholdExpansionCount};section={activeSection};evidenceExpanded={workbench.IsValidationEvidenceExpanded};thresholdExpanded={workbench.IsValidationThresholdExpanded};evidenceAtCallback={evidenceExpandedAtCallback};thresholdAtCallback={thresholdExpandedAtCallback}");
            Check(
                "setup keeps authored recipe identity and records only the intended validation definition draft",
                workbench.PipelineSteps.Count == beforeStepCount
                && string.Equals(
                    workbench.RecipePath,
                    beforeRecipePath,
                    StringComparison.OrdinalIgnoreCase)
                && string.Equals(
                    workbench.Source.Path,
                    beforeSourcePath,
                    StringComparison.OrdinalIgnoreCase)
                && workbench.IsValidationSetDefinitionDirty,
                $"recipe={workbench.RecipePath};source={workbench.Source.Path};steps={workbench.PipelineSteps.Count};validationDefinitionDirty={workbench.IsValidationSetDefinitionDirty}");

            var invalidWorkbench = new ToolWorkbenchViewModel(
                Path.Combine(root, "invalid-recent.json"));
            var invalidThrown = false;
            var invalidMessage = string.Empty;
            try
            {
                ShellValidationSetSmoke.Configure(
                    [
                        "verification",
                        "--smoke-validation-set-recipe",
                        Path.Combine(root, "missing-recipe.json"),
                        "--smoke-validation-set-sources",
                        sourcePath
                    ],
                    invalidWorkbench,
                    () => { },
                    () => { },
                    () => { },
                    _ => Task.CompletedTask,
                    _ => false);
            }
            catch (InvalidDataException exception)
            {
                invalidThrown = true;
                invalidMessage = exception.Message;
            }
            Check(
                "unreadable recipe reports the existing fail-closed configuration error",
                invalidThrown
                && invalidMessage.Contains(
                    "Validation Set smoke recipe could not be opened",
                    StringComparison.Ordinal)
                && invalidWorkbench.ValidationSetSamples.Count == 0,
                $"thrown={invalidThrown};message={invalidMessage};samples={invalidWorkbench.ValidationSetSamples.Count}");
        }
        catch (Exception exception)
        {
            lines.Add($"FAIL | unexpected exception | {exception}");
        }

        var passedAll = total > 0 && passed == total;
        lines.Add($"ShellValidationSetSmokeVerification|{(passedAll ? "PASS" : "FAIL")}|checks={passed}/{total}");
        var fullReportPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(
            Path.GetDirectoryName(fullReportPath) ?? Environment.CurrentDirectory);
        File.WriteAllLines(fullReportPath, lines);
        summary = lines[^1];
        return passedAll;
    }
}
