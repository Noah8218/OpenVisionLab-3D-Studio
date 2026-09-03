using System.IO;
using System.Text;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Tools.Authoring;

namespace OpenVisionLab.ThreeD.Verification.Shell.Workbench;

internal static class ThicknessRepeatGridAuthoringVerification
{
    private const string ExactRecipeRelativePath =
        "3D/Samples/ThicknessCouponV1/inspection-recipe.ov3d-recipe.json";
    private const string ExactSourceRelativePath =
        "3D/Samples/ThicknessCouponV1/thickness-coupon-v1.C3D";

    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var lines = new List<string>
        {
            "OpenVisionLab 3D Thickness repeat-grid authoring verification",
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
            var repositoryRoot = FindRepositoryRoot();
            var exactRecipePath = Path.Combine(repositoryRoot, ExactRecipeRelativePath);
            var exactSourcePath = Path.Combine(repositoryRoot, ExactSourceRelativePath);
            var reportDirectory = Path.GetDirectoryName(Path.GetFullPath(reportPath))
                ?? throw new InvalidOperationException("Verification report has no directory.");
            Directory.CreateDirectory(reportDirectory);

            var exactDocument = ToolRecipeDocumentStore.Load(exactRecipePath);
            var firstStep = exactDocument.Steps.Single(step =>
                string.Equals(step.Id, "step.pad-thickness.01", StringComparison.OrdinalIgnoreCase));
            var firstSelectionIds = firstStep.InputEntityIds.Skip(1).ToHashSet(
                StringComparer.OrdinalIgnoreCase);
            var starter = exactDocument with
            {
                Name = "Thickness Coupon v1 Repeat Starter",
                Source = exactDocument.Source with { Path = exactSourcePath },
                Steps = [firstStep],
                Selections = (exactDocument.Selections ?? [])
                    .Where(selection => firstSelectionIds.Contains(selection.Id))
                    .ToArray()
            };
            var starterPath = Path.Combine(
                reportDirectory,
                "repeat-grid-one-step.ov3d-recipe.json");
            ToolRecipeDocumentStore.Save(starterPath, starter);

            Check(
                "starter fixture uses the bundled Thickness C3D and one complete Thickness",
                File.Exists(exactSourcePath)
                && starter.Steps.Count == 1
                && starter.Selections?.Count == 2
                && ToolRecipeValidator.ValidateForStorage(starter).IsValid,
                $"source={exactSourcePath}; steps={starter.Steps.Count}; selections={starter.Selections?.Count ?? 0}");

            var request = new ThicknessRepeatGridRequest(4, 2, 288, 336, "Pad {n}");
            var authored = ThicknessRepeatGridAuthoringService.CreateCandidate(
                starter,
                firstStep.Id,
                request);
            Check(
                "pure authoring service creates one valid 4 by 2 candidate",
                authored.IsValid
                && authored.Draft is not null
                && authored.Candidates.Count == 8,
                $"valid={authored.IsValid}; candidates={authored.Candidates.Count}; errors={string.Join(" | ", authored.Errors)}");

            Check(
                "review candidate contains eight ordinary steps and sixteen selections",
                authored.Draft?.CandidateDocument.Steps.Count == 8
                && authored.Draft.CandidateDocument.Selections?.Count == 16,
                $"steps={authored.Draft?.CandidateDocument.Steps.Count ?? 0}; selections={authored.Draft?.CandidateDocument.Selections?.Count ?? 0}");

            Check(
                "candidate ordering and names are deterministic row-major Pad 1 through Pad 8",
                authored.Candidates.Select(candidate => candidate.InstanceNumber)
                    .SequenceEqual(Enumerable.Range(1, 8))
                && authored.Candidates.Select(candidate => candidate.ToolName)
                    .SequenceEqual(Enumerable.Range(1, 8).Select(index => $"Pad {index} Thickness")),
                string.Join(", ", authored.Candidates.Select(candidate => candidate.ToolName)));

            var expectedReferenceColumns = new[] { 128, 416, 704, 992, 128, 416, 704, 992 };
            var expectedMeasurementColumns = new[] { 196, 484, 772, 1060, 196, 484, 772, 1060 };
            var expectedRows = new[] { 156, 156, 156, 156, 492, 492, 492, 492 };
            Check(
                "candidate ROI coordinates use the requested X and Z pitches",
                authored.Candidates.Select(candidate => candidate.ReferenceSelection.GridRectangle!.Column)
                    .SequenceEqual(expectedReferenceColumns)
                && authored.Candidates.Select(candidate => candidate.MeasurementSelection.GridRectangle!.Column)
                    .SequenceEqual(expectedMeasurementColumns)
                && authored.Candidates.Select(candidate => candidate.ReferenceSelection.GridRectangle!.Row)
                    .SequenceEqual(expectedRows)
                && authored.Candidates.Select(candidate => candidate.MeasurementSelection.GridRectangle!.Row)
                    .SequenceEqual(expectedRows),
                string.Join(
                    "; ",
                    authored.Candidates.Select(candidate =>
                        $"{candidate.InstanceNumber}:R({candidate.ReferenceSelection.GridRectangle!.Column},{candidate.ReferenceSelection.GridRectangle.Row})"
                        + $"/M({candidate.MeasurementSelection.GridRectangle!.Column},{candidate.MeasurementSelection.GridRectangle.Row})")));

            Check(
                "translated ROI dimensions and Thickness parameters remain unchanged",
                authored.Candidates.All(candidate =>
                    candidate.ReferenceSelection.GridRectangle!.ColumnCount == 50
                    && candidate.ReferenceSelection.GridRectangle.RowCount == 144
                    && candidate.MeasurementSelection.GridRectangle!.ColumnCount == 90
                    && candidate.MeasurementSelection.GridRectangle.RowCount == 144
                    && candidate.Step.Parameters.SequenceEqual(firstStep.Parameters)),
                $"reference=50x144; measurement=90x144; parameters={firstStep.Parameters.Count}");

            var generatedIds = authored.Draft!.CandidateDocument.Steps
                .SelectMany(step => new[] { step.Id, step.OutputEntityId })
                .Concat(authored.Draft.CandidateDocument.Selections!.Select(selection => selection.Id))
                .ToArray();
            Check(
                "every generated step output and ROI owns a unique identity",
                generatedIds.Distinct(StringComparer.OrdinalIgnoreCase).Count() == generatedIds.Length,
                $"identities={generatedIds.Length}; unique={generatedIds.Distinct(StringComparer.OrdinalIgnoreCase).Count()}");

            Check(
                "first generated instance retains the authored source identities",
                authored.Candidates[0].Step.Id == firstStep.Id
                && authored.Candidates[0].Step.OutputEntityId == firstStep.OutputEntityId
                && authored.Candidates[0].ReferenceSelection.Id == firstStep.InputEntityIds[1]
                && authored.Candidates[0].MeasurementSelection.Id == firstStep.InputEntityIds[2],
                $"step={authored.Candidates[0].Step.Id}; output={authored.Candidates[0].Step.OutputEntityId}");

            Check(
                "candidate document passes current storage validation",
                ToolRecipeValidator.ValidateForStorage(authored.Draft.CandidateDocument).IsValid,
                $"schema={authored.Draft.CandidateDocument.SchemaVersion}");

            var badPattern = ThicknessRepeatGridAuthoringService.CreateCandidate(
                starter,
                firstStep.Id,
                request with { NamePattern = "Tab" });
            Check(
                "name pattern without the instance token fails closed",
                !badPattern.IsValid
                && badPattern.Errors.Any(error => error.Contains("{n}", StringComparison.Ordinal)),
                string.Join(" | ", badPattern.Errors));

            var outOfBounds = ThicknessRepeatGridAuthoringService.CreateCandidate(
                starter,
                firstStep.Id,
                request with { RowPitch = 1200 });
            Check(
                "out-of-grid candidates remain review errors and cannot be applied",
                !outOfBounds.IsValid
                && outOfBounds.Draft is null
                && outOfBounds.Candidates.Any(candidate => !candidate.IsValid),
                string.Join(" | ", outOfBounds.Errors));

            var session = new ThicknessRepeatGridAuthoringSession();
            session.Begin(starter, firstStep.Id);
            Check(
                "session review is display-only and keeps the original document unchanged",
                session.IsActive
                && session.IsValid
                && session.Candidates.Count == 8
                && starter.Steps.Count == 1
                && starter.Selections?.Count == 2,
                $"active={session.IsActive}; original={starter.Steps.Count}/{starter.Selections?.Count ?? 0}; candidate={session.Candidates.Count}");
            session.Cancel();
            Check(
                "session Cancel discards the candidate",
                session.IsInactive
                && session.Draft is null
                && session.Candidates.Count == 0,
                $"active={session.IsActive}; draft={session.Draft is not null}; candidates={session.Candidates.Count}");

            var recentPath = Path.Combine(reportDirectory, "repeat-grid-recent.json");
            var workbench = new ToolWorkbenchViewModel(recentPath);
            var opened = workbench.TryOpenTeachingRecipe(starterPath, out var openMessage);
            var beforeState = workbench.PipelineSteps.Select(step => step.State).ToArray();
            var previewSelections = Array.Empty<ToolRecipeSelection>();
            workbench.ThicknessRepeatGridPreviewChanged += (_, args) =>
                previewSelections = args.Selections.ToArray();
            Check(
                "Workbench opens the one-step Thickness starter ready for repetition",
                opened
                && workbench.PipelineSteps.Count == 1
                && workbench.Selections.Count == 2
                && !workbench.IsDirty
                && workbench.CanStartThicknessRepeatGrid,
                opened
                    ? $"steps={workbench.PipelineSteps.Count}; selections={workbench.Selections.Count}; dirty={workbench.IsDirty}"
                    : openMessage);

            workbench.BeginThicknessRepeatGridCommand.Execute(null);
            Check(
                "Begin publishes sixteen display-only ROI candidates without recipe or execution mutation",
                workbench.ThicknessRepeatGrid.IsActive
                && workbench.ThicknessRepeatGridCandidates.Count == 8
                && previewSelections.Length == 16
                && workbench.PipelineSteps.Count == 1
                && workbench.Selections.Count == 2
                && !workbench.IsDirty
                && !workbench.HasCurrentMeasurementPreview
                && workbench.CurrentMeasurementOutput is null
                && workbench.PipelineSteps.Select(step => step.State).SequenceEqual(beforeState),
                $"previewSelections={previewSelections.Length}; recipe={workbench.PipelineSteps.Count}/{workbench.Selections.Count}; dirty={workbench.IsDirty}");

            workbench.ThicknessRepeatColumnPitch = 289;
            workbench.CancelThicknessRepeatGridCommand.Execute(null);
            Check(
                "editing then Cancel restores the exact authored state",
                workbench.ThicknessRepeatGrid.IsInactive
                && previewSelections.Length == 0
                && workbench.PipelineSteps.Count == 1
                && workbench.Selections.Count == 2
                && !workbench.IsDirty
                && !workbench.HasCurrentMeasurementPreview
                && workbench.CurrentMeasurementOutput is null,
                $"active={workbench.ThicknessRepeatGrid.IsActive}; previewSelections={previewSelections.Length}; recipe={workbench.PipelineSteps.Count}/{workbench.Selections.Count}");

            workbench.BeginThicknessRepeatGridCommand.Execute(null);
            workbench.ApplyThicknessRepeatGridCommand.Execute(null);
            Check(
                "explicit Apply materializes eight independent Thickness steps and sixteen ROIs",
                workbench.ThicknessRepeatGrid.IsInactive
                && previewSelections.Length == 0
                && workbench.PipelineSteps.Count == 8
                && workbench.Selections.Count == 16
                && workbench.IsDirty
                && workbench.PipelineSteps.Select(step => step.Id)
                    .Distinct(StringComparer.OrdinalIgnoreCase).Count() == 8
                && workbench.PipelineSteps.Select(step => step.OutputEntityId)
                    .Distinct(StringComparer.OrdinalIgnoreCase).Count() == 8
                && workbench.Selections.Select(selection => selection.Id)
                    .Distinct(StringComparer.OrdinalIgnoreCase).Count() == 16,
                $"recipe={workbench.PipelineSteps.Count}/{workbench.Selections.Count}; dirty={workbench.IsDirty}");

            Check(
                "Apply exposes one compact Thickness group and still does not run inspection",
                workbench.HasThicknessRepeatGroup
                && workbench.ThicknessRepeatGroupSummary.Contains("8", StringComparison.Ordinal)
                && !workbench.HasCurrentMeasurementPreview
                && workbench.CurrentMeasurementOutput is null
                && workbench.PipelineSteps.All(step => step.State != "Published"),
                $"group={workbench.ThicknessRepeatGroupSummary}; preview={workbench.HasCurrentMeasurementPreview}");

            var appliedPath = Path.Combine(
                reportDirectory,
                "repeat-grid-applied.ov3d-recipe.json");
            var saved = workbench.TrySaveTeachingRecipe(appliedPath, out var saveMessage);
            var reopened = new ToolWorkbenchViewModel(
                Path.Combine(reportDirectory, "repeat-grid-reopened-recent.json"));
            var reopenMessage = string.Empty;
            var reopenedSuccessfully = saved
                && reopened.TryOpenTeachingRecipe(appliedPath, out reopenMessage);
            Check(
                "save and reopen preserves eight editable instances and sixteen ROIs",
                reopenedSuccessfully
                && reopened.PipelineSteps.Count == 8
                && reopened.Selections.Count == 16
                && reopened.PipelineSteps.Select(step => step.ToolName)
                    .SequenceEqual(Enumerable.Range(1, 8).Select(index => $"Pad {index} Thickness"))
                && reopened.HasThicknessRepeatGroup,
                reopenedSuccessfully
                    ? $"steps={reopened.PipelineSteps.Count}; selections={reopened.Selections.Count}; group={reopened.ThicknessRepeatGroupSummary}"
                    : saved ? reopenMessage : saveMessage);

            Check(
                "reopened instances remain ordinary locally editable recipe steps",
                reopened.PipelineSteps.All(step => step.InputEntityIds.Count == 3)
                && reopened.PipelineSteps.All(step =>
                    step.Parameters.Select(parameter => parameter.Name)
                        .SequenceEqual(firstStep.Parameters.Select(parameter => parameter.Name)))
                && reopened.Selections.All(selection =>
                    selection.GridRectangle is not null
                    && selection.RootSourceId == starter.Source.Id),
                $"steps={reopened.PipelineSteps.Count}; parameters={reopened.PipelineSteps.FirstOrDefault()?.Parameters.Count ?? 0}");

            lines.Add($"Evidence | starter={starterPath}");
            lines.Add($"Evidence | applied={appliedPath}");
            lines.Add($"Evidence | source={exactSourcePath}");
        }
        catch (Exception exception)
        {
            lines.Add($"FAIL | unexpected exception | {exception.GetType().Name}: {exception.Message}");
        }

        var succeeded = passed == total
            && total > 0
            && !lines.Any(line => line.StartsWith("FAIL | unexpected exception", StringComparison.Ordinal));
        lines.Add($"Result: {(succeeded ? "Pass" : "Fail")} ({passed}/{total} checks)");
        var fullReportPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
        File.WriteAllLines(fullReportPath, lines, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        summary =
            $"Thickness repeat-grid authoring verification: {(succeeded ? "Pass" : "Fail")} ({passed}/{total} checks)";
        return succeeded;
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, ExactRecipeRelativePath))
                && File.Exists(Path.Combine(directory.FullName, ExactSourceRelativePath)))
            {
                return directory.FullName;
            }
        }

        for (var directory = new DirectoryInfo(Environment.CurrentDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, ExactRecipeRelativePath))
                && File.Exists(Path.Combine(directory.FullName, ExactSourceRelativePath)))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException(
            $"Could not locate repository files {ExactRecipeRelativePath} and {ExactSourceRelativePath}.");
    }
}
