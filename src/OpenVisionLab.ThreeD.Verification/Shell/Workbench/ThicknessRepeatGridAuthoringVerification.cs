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
        var fullReportPath = Path.GetFullPath(reportPath);
        var reportDirectory = Path.GetDirectoryName(fullReportPath)
            ?? throw new InvalidOperationException("Verification report has no directory.");
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
            VerifyGroupContract(reportDirectory, authored.Draft!.CandidateDocument, exactDocument, exactSourcePath, reopened, Check);
        }
        catch (Exception exception)
        {
            lines.Add($"FAIL | unexpected exception | {exception.GetType().Name}: {exception.Message}");
        }

        var succeeded = passed == total
            && total > 0
            && !lines.Any(line => line.StartsWith("FAIL | unexpected exception", StringComparison.Ordinal));
        lines.Add($"Result: {(succeeded ? "Pass" : "Fail")} ({passed}/{total} checks)");
        Directory.CreateDirectory(reportDirectory);
        File.WriteAllLines(fullReportPath, lines, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        summary =
            $"Thickness repeat-grid authoring verification: {(succeeded ? "Pass" : "Fail")} ({passed}/{total} checks)";
        return succeeded;
    }

    private static void VerifyGroupContract(string root, ToolRecipeDocument authored, ToolRecipeDocument exact, string sourcePath, ToolWorkbenchViewModel reopened, Action<string, bool, string> check)
    {
        var group = authored.Steps[0].Group;
        check("actual repeat membership survives Store reload and fresh Workbench reopen", group is not null && group.Name == "Pad"
            && authored.Steps.All(step => step.Group == group) && reopened.PipelineSteps.All(step => step.Group == group), "stable metadata; existing Apply and cancel checks establish no implicit execution");
        var legacy = authored with { SchemaVersion = ToolRecipeDocument.AcceptanceLimitsSchemaVersion, Steps = authored.Steps.Select(step => step with { Group = null }).ToArray() };
        check("legacy schema and group-free execution context preserve metadata exclusion", ToolRecipeValidator.ValidateForStorage(legacy).IsValid
            && authored.Steps.All(step => authored.CreateStepExecutionContext(step.Id) == legacy.CreateStepExecutionContext(step.Id)), "group name/membership has no execution-context effect");
        var plainStep = legacy.Steps[0] with { InputEntityIds = [legacy.Source.Id] };
        var plain = legacy with { Steps = [plainStep], References = [], Selections = [] };
        var oldContext = System.Text.Json.JsonSerializer.Serialize(new
        {
            Source = plain.Source with { Name = string.Empty },
            Steps = new[] { new { plainStep.Id, plainStep.ToolId, ToolName = string.Empty, plainStep.MinimumInputCount, plainStep.InputEntityIds,
                plainStep.OutputEntityId, plainStep.Parameters, plainStep.DualRoiRouting, plainStep.OutputEnabled, plainStep.SignedDeltaSidecar, plainStep.AcceptanceLimitsConfigured } },
            Selections = Array.Empty<ToolRecipeSelection>(), References = Array.Empty<ToolRecipeReference>()
        });
        check("ungrouped context bytes match the prior explicit step contract", plain.CreateStepExecutionContext(plainStep.Id) == oldContext, "all pre-group property order and null fields retained");
        var versions = new[] { "1.0", "1.1", "1.2", "1.3", "1.4", "1.5", "1.6", "1.7", "1.8", "1.9", "1.10" };
        foreach (var version in versions)
        {
            var path = Path.Combine(root, $"legacy-schema-{version}.ov3d-recipe.json");
            ToolRecipeDocumentStore.Save(path, legacy with { SchemaVersion = version, Steps = [], Selections = null, Category = null, InspectionTarget = null, Description = null });
            check($"schema {version} remains readable without group migration", ToolRecipeDocumentStore.Load(path).SchemaVersion == version, "legacy document shape and explicit schema retained");
        }
        var badNames = authored with { Steps = authored.Steps.Select((step, index) => index == 1 ? step with { Group = new ToolRecipeStepGroup(group!.Id.ToUpperInvariant(), "Conflicting name") } : step).ToArray() };
        check("group storage boundary rejects conflicting case-insensitive identity and empty descriptors", !ToolRecipeValidator.ValidateForStorage(badNames).IsValid
            && !ToolRecipeValidator.ValidateForStorage(authored with { Steps = [authored.Steps[0] with { Group = new ToolRecipeStepGroup("", "") }] }).IsValid
            && !ToolRecipeValidator.ValidateForStorage(legacy with { Steps = authored.Steps }).IsValid
            && ToolRecipeValidator.ValidateForStorage(authored with { Steps = [null!] }).Errors.Any(error => error.Contains("required", StringComparison.OrdinalIgnoreCase)), "malformed input returns errors, not NullReferenceException");
        var removed = reopened.PipelineSteps[1];
        reopened.SelectedPipelineStep = removed;
        check("member deletion and explicit restore preserve group identity without Run", reopened.ConfirmSelectedStepRemoval(removed.Id)
            && reopened.PipelineSteps.Count == 7 && reopened.TryRestoreLastRemovedStep(out _)
            && reopened.PipelineSteps.Count == 8 && reopened.PipelineSteps.Single(step => step.Id == removed.Id).Group == group && !reopened.IsValidationSetRunning,
            "existing removal snapshot owns restoration");

        var fixtureRoot = Path.Combine(root, "native-group-fixture");
        Directory.CreateDirectory(fixtureRoot);
        var inputPath = Path.Combine(fixtureRoot, "coupon.C3D");
        File.Copy(sourcePath, inputPath, overwrite: true);
        // Author through the real repeat service, then retain the bundled per-pad authored tolerances.
        var seed = exact with { Source = exact.Source with { Path = inputPath }, Steps = [exact.Steps[0]],
            Selections = exact.Selections!.Where(selection => exact.Steps[0].InputEntityIds.Contains(selection.Id)).ToArray() };
        var named = ThicknessRepeatGridAuthoringService.CreateCandidate(seed, seed.Steps[0].Id, new ThicknessRepeatGridRequest(4, 2, 288, 336, "Pad 두께 {n}")).Draft!.CandidateDocument;
        var fixture = named with { Name = "Repeated Thickness group parity",
            Steps = named.Steps.Select((step, index) => step with { Parameters = exact.Steps[index].Parameters }).ToArray() };
        var groupedPath = Path.Combine(fixtureRoot, "grouped.ov3d-recipe.json");
        var legacyPath = Path.Combine(fixtureRoot, "legacy.ov3d-recipe.json");
        ToolRecipeDocumentStore.Save(groupedPath, fixture);
        ToolRecipeDocumentStore.Save(legacyPath, fixture with { SchemaVersion = ToolRecipeDocument.AcceptanceLimitsSchemaVersion, Steps = fixture.Steps.Select(step => step with { Group = null }).ToArray() });
        check("group JSON is explicit and null group fields are absent from legacy JSON", File.ReadAllText(groupedPath).Contains("\"group\"")
            && !File.ReadAllText(legacyPath).Contains("\"group\"") && ToolRecipeDocumentStore.Load(legacyPath).Steps.All(step => step.Group is null), "old reader rejects new schema; new reader retains old contract");
        var snapshot = C3DHeightFieldSnapshot.LoadIdentified(inputPath, fixture.Source.Id, fixture.Source.Unit, fixture.Source.FrameId);
        var values = snapshot.Values.ToArray();
        var roi = fixture.Selections!.Single(selection => selection.Id == fixture.Steps[0].InputEntityIds[2]).GridRectangle!;
        for (var row = roi.Row; row < roi.Row + roi.RowCount; row++)
            for (var column = roi.Column; column < roi.Column + roi.ColumnCount; column++) values[row * snapshot.Width + column] += 5;
        var ngPath = Path.Combine(fixtureRoot, "single-member-ng.C3D");
        C3DHeightFieldSnapshot.CreateForVerification(fixture.Source.Id, snapshot.Width, snapshot.Height, values, snapshot.Unit, snapshot.FrameId).SaveC3D(ngPath);
        var errorPath = Path.Combine(fixtureRoot, "single-member-missing.C3D");
        for (var row = roi.Row; row < roi.Row + roi.RowCount; row++)
            for (var column = roi.Column; column < roi.Column + roi.ColumnCount; column++) values[row * snapshot.Width + column] = double.NaN;
        C3DHeightFieldSnapshot.CreateForVerification(fixture.Source.Id, snapshot.Width, snapshot.Height, values, snapshot.Unit, snapshot.FrameId).SaveC3D(errorPath);
        var roles = new[] { new global::OpenVisionLab.ThreeD.Tools.ToolRecipeValidationSampleInput(inputPath, ToolRecipeValidationSampleRole.Good), new global::OpenVisionLab.ThreeD.Tools.ToolRecipeValidationSampleInput(ngPath, ToolRecipeValidationSampleRole.Bad), new global::OpenVisionLab.ThreeD.Tools.ToolRecipeValidationSampleInput(errorPath, ToolRecipeValidationSampleRole.HeldOut) };
        foreach (var path in new[] { groupedPath, legacyPath }) ToolRecipeValidationSetDefinitionStore.SaveForRecipe(path,
            new ToolRecipeValidationSetDefinition(ToolRecipeValidationSetDefinition.CurrentSchemaVersion, fixture.Name, fixture.Source.ContentSha256!,
                roles.Select((sample, index) => new ToolRecipeValidationSampleDefinition(index + 1, sample.SourcePath, sample.Role)).ToArray()));
        var execution = global::OpenVisionLab.ThreeD.Tools.ToolRecipeValidationSetExecution.Execute(fixture, roles);
        File.WriteAllText(Path.Combine(fixtureRoot, "actual-execution.json"), System.Text.Json.JsonSerializer.Serialize(execution, new System.Text.Json.JsonSerializerOptions { WriteIndented = true, NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals }));
        check("native paired fixtures use actual C3D execution with member NG and Error", execution.Samples.Count == 3
            && execution.Samples[1].Steps[0].Status == ResultStatus.Fail && execution.Samples[2].Steps[0].Status == ResultStatus.Error,
            $"grouped={groupedPath};legacy={legacyPath};states={string.Join(',', execution.Samples.Select(sample => sample.Status))};no injected UI result");
        using var clone = new ToolWorkbenchViewModel(Path.Combine(root, "group-clone-recent.json"));
        clone.TryOpenTeachingRecipe(groupedPath, out _);
        clone.BeginCompatibleSourceVariantSetup();
        clone.FirstRecipeSourcePath = ngPath;
        clone.IsVariantRoiReuseConfirmed = true;
        var clonePath = Path.Combine(fixtureRoot, "clone-" + Guid.NewGuid().ToString("N") + ".ov3d-recipe.json");
        var setup = new ToolWorkbenchFirstRecipeSetup("Group clone", fixtureRoot, ngPath, "", clonePath, IsCompatibleSourceVariant: true);
        var cloned = clone.TryCreateCompatibleSourceVariant(setup, ToolRecipeSelectionSourceBindingVerifier.ReadIdentity(ngPath), out var cloneMessage);
        check("existing compatible-source clone preserves group and remains idle", cloned && ToolRecipeDocumentStore.Load(clonePath).Steps.All(step => step.Group == fixture.Steps[0].Group)
            && !clone.IsValidationSetRunning && !clone.HasCurrentMeasurementPreview, cloneMessage);
        var disabledPath = Path.Combine(fixtureRoot, "disabled-member.ov3d-recipe.json");
        ToolRecipeDocumentStore.Save(disabledPath, fixture with { Steps = fixture.Steps.Select((step, index) => index == 7 ? step with { OutputEnabled = false } : step).ToArray() });
        check("disabled member survives actual save reload", ToolRecipeDocumentStore.Load(disabledPath).Steps.Count(step => step.OutputEnabled) == 7, disabledPath);
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
