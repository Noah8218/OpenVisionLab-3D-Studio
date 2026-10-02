using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Shell.Verification;

internal static class HeightMeasurementSnapshotVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var fullReportPath = Path.GetFullPath(reportPath);
        var reportDirectory = Path.GetDirectoryName(fullReportPath) ?? Environment.CurrentDirectory;
        var fixtureRoot = Path.Combine(reportDirectory, $"height-measurement-snapshot-{Guid.NewGuid():N}");
        var lines = new List<string>
        {
            "OpenVisionLab 3D Height Measurement snapshot verification",
            $"Generated: {DateTimeOffset.Now:O}",
            "Boundary|The UI thread captures the document, step route, immutable input artifacts, and recipe directory before Task.Run; a result is discarded when the current recipe fingerprint no longer matches that start snapshot.",
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
            var sourceContractPath = FindHeightMeasurementOwnerPath();
            Check(
                "source-contract-path-is-available",
                sourceContractPath is not null,
                sourceContractPath ?? "not found from the built Shell base directory");

            if (sourceContractPath is not null)
            {
                var ownerSource = File.ReadAllText(sourceContractPath);
                var taskStart = ownerSource.IndexOf(
                    "var evaluation = await Task.Run(",
                    StringComparison.Ordinal);
                var workerStart = ownerSource.IndexOf(
                    "() => ",
                    taskStart,
                    StringComparison.Ordinal);
                var workerEnd = ownerSource.IndexOf(
                    "cancellationToken);",
                    workerStart,
                    StringComparison.Ordinal);
                var snapshotNames = new[]
                {
                    "documentSnapshot",
                    "stepIdSnapshot",
                    "inputEntityIdsSnapshot",
                    "sourceEntityIdSnapshot",
                    "croppedHeightFieldSnapshot",
                    "transformedHeightFieldSnapshot",
                    "editableRegionSnapshot",
                    "recipeDirectorySnapshot",
                    "executionFingerprint",
                };
                var declarationsBeforeWorker = taskStart >= 0
                    && snapshotNames.All(name =>
                        ownerSource.IndexOf($"var {name}", StringComparison.Ordinal) >= 0
                        && ownerSource.IndexOf($"var {name}", StringComparison.Ordinal) < taskStart);
                Check(
                    "ui-snapshot-declarations-precede-task-run",
                    declarationsBeforeWorker,
                    $"taskStart={taskStart};names={string.Join(',', snapshotNames)}");

                var workerBody = workerStart >= 0 && workerEnd > workerStart
                    ? ownerSource[workerStart..workerEnd]
                    : string.Empty;
                var forbiddenWorkerReads = new[]
                {
                    "createDocument()",
                    "getSelectedPipelineStep()",
                    "getSourceEntityId()",
                    "getPublishedCroppedHeightField(",
                    "getPublishedHeightField(",
                    "getPublishedEditableRegion(",
                    "getRecipePath()",
                    "GetCurrentCroppedHeightField()",
                    "GetCurrentTransformedHeightField()",
                    "GetCurrentEditableRegion()",
                    "GetRecipeDirectory()",
                };
                var workerUsesOnlySnapshots = workerBody.Length > 0
                    && forbiddenWorkerReads.All(read => !workerBody.Contains(read, StringComparison.Ordinal));
                Check(
                    "worker-uses-snapshot-values-only",
                    workerUsesOnlySnapshots,
                    $"workerStart={workerStart};workerEnd={workerEnd};forbiddenReads={string.Join(',', forbiddenWorkerReads.Where(workerBody.Contains))}");
            }

            ToolResult DisplayResult(IReadOnlyList<Metric> metrics) => new("Measurement", ResultStatus.Pass, "", TimeSpan.Zero, metrics, []);
            var metricSelector = typeof(ToolWorkbenchViewModel).GetMethod("SelectPrimaryMeasurementMetric", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
            var limitSelector = typeof(ToolWorkbenchViewModel).GetMethod("MeasurementLimitSummary", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
            var measurementCases = new[]
            {
                (Tool: "thickness", Metric: "Mean", Unit: "raw-height"),
                (Tool: "warpage", Metric: "PeakToValley", Unit: "raw-height"),
                (Tool: "plane-flatness", Metric: "Flatness", Unit: "model"),
                (Tool: "datum-plane-raw-height-deviation", Metric: "PeakToValleyRawHeight", Unit: "raw-height"),
                (Tool: "point-pair-dimensions", Metric: "3D distance", Unit: "model"),
                (Tool: "gap-flush", Metric: "Signed gap", Unit: "model"),
                (Tool: "volume", Metric: "Signed net volume", Unit: "model^3"),
                (Tool: "cross-section-dimensions", Metric: "Section width", Unit: "model"),
                (Tool: "connected-region-gate", Metric: "Region finite coverage", Unit: "ratio"),
                (Tool: "completeness-grid", Metric: "Passed cells", Unit: "cells")
            };
            foreach (var test in measurementCases)
            {
                Metric[] metrics = [new("Valid sample count", MetricKind.Count, 100, "count"), new("Expected limit", MetricKind.Number, 999, test.Unit), new(test.Metric, test.Tool == "completeness-grid" ? MetricKind.Count : MetricKind.Number, -1.25, test.Unit, ResultStatus.Fail)];
                var primary = (Metric?)metricSelector.Invoke(null, [test.Tool, metrics]);
                Check($"{test.Tool} displays its measurement instead of a count or limit",
                    primary?.Name == test.Metric && primary.Value == -1.25 && primary.Unit == test.Unit,
                    $"actual={primary?.Name};value={primary?.Value};unit={primary?.Unit}");
            }
            Metric[] boundaryMetrics = [new("Mean", MetricKind.Length, 0.48, "raw-height", ResultStatus.Fail), new("LowerLimit", MetricKind.Length, 0, "raw-height"), new("UpperLimit", MetricKind.Length, 0.4799, "raw-height"), new("AboveUpperLimitCount", MetricKind.Count, 1, "count")];
            var boundaryLimits = (string)limitSelector.Invoke(null, ["thickness", DisplayResult(boundaryMetrics), Array.Empty<ToolRecipeParameter>()])!;
            Check("boundary NG limit retains precision and excludes violation count",
                boundaryLimits == "0–0.4799 raw-height", boundaryLimits);
            ToolRecipeParameter[] crossSectionParameters = [new("ExpectedWidth", "2"), new("WidthTolerance", "0.1"), new("ExpectedHeightRange", "3"), new("HeightTolerance", "0.2"), new("MinimumValidSampleCount", "25")];
            var sectionLimits = (string)limitSelector.Invoke(null, ["cross-section-dimensions", DisplayResult(Array.Empty<Metric>()), crossSectionParameters])!;
            Check("execution snapshot exposes both section dimensions without sample-count conditions",
                sectionLimits.Contains("ExpectedWidth: 2", StringComparison.Ordinal) && sectionLimits.Contains("HeightTolerance: 0.2", StringComparison.Ordinal)
                && !sectionLimits.Contains("SampleCount", StringComparison.Ordinal), sectionLimits);
            Metric[] warpageLimits = [new("MaximumPeakToValley", MetricKind.Deviation, 0.4799, "raw-height"), new("MaximumRms", MetricKind.Deviation, 0.1, "raw-height"), new("MinimumValidSamples", MetricKind.Count, 25, "count")];
            var warpageSummary = (string)limitSelector.Invoke(null, ["warpage", DisplayResult(warpageLimits), Array.Empty<ToolRecipeParameter>()])!;
            Check("SDK warpage acceptance metrics retain their own units and precision",
                warpageSummary.Contains("MaximumPeakToValley: 0.4799 raw-height", StringComparison.Ordinal)
                && warpageSummary.Contains("MaximumRms: 0.1 raw-height", StringComparison.Ordinal)
                && !warpageSummary.Contains("Samples", StringComparison.Ordinal), warpageSummary);

            OpenVisionLanguageService.Load();
            var originalLanguage = OpenVisionLanguageService.CurrentLanguage;
            try
            {
                foreach (var language in new[] { OpenVisionLanguage.Korean, OpenVisionLanguage.English })
                {
                    OpenVisionLanguageService.SetLanguage(language, save: false);
                    foreach (var wording in new[]
                    {
                        (Raw: "All measured H-axis separations from the fitted reference surface are within limits.", Korean: "기준면과의 높이 차이가 모두 규격 안에 있습니다."),
                        (Raw: "One or more measured H-axis separations from the reference surface exceed the limits.", Korean: "기준면과의 높이 차이 중 규격을 벗어난 값이 있습니다."),
                        (Raw: "One or more measured H-axis separations from the fitted reference surface exceed the limits.", Korean: "기준면과의 높이 차이 중 규격을 벗어난 값이 있습니다.")
                    })
                    {
                        var wordingResult = DisplayResult(Array.Empty<Metric>()) with { Message = wording.Raw };
                        var displayedMessage = ToolWorkbenchViewModel.MeasurementResultMessage(wordingResult);
                        var expectedMessage = language == OpenVisionLanguage.English ? wording.Raw : wording.Korean;
                        Check($"{language} thickness outcome localizes without changing the SDK message", displayedMessage == expectedMessage && wordingResult.Message == wording.Raw, displayedMessage);
                    }
                }
            }
            finally
            {
                OpenVisionLanguageService.SetLanguage(originalLanguage, save: false);
            }

            Directory.CreateDirectory(fixtureRoot);
            var sourcePath = Path.Combine(fixtureRoot, "source.c3d");
            var recipePath = Path.Combine(fixtureRoot, "height-measurement.ov3d-recipe.json");
            var fixtureSource = C3DHeightFieldSnapshot.CreateForVerification(
                "source.height-measurement-snapshot",
                4,
                4,
                Enumerable.Range(1, 16).Select(value => (double)value).ToArray(),
                "fixture-unit",
                "frame.raw");
            fixtureSource.SaveC3D(sourcePath);
            var initialInputIds = new[] { fixtureSource.EntityId, "selection.reference", "selection.measurement" };
            var initialParameters = new[]
            {
                new ToolRecipeParameter("MinimumThickness", "0"),
                new ToolRecipeParameter("MaximumThickness", "100000"),
                new ToolRecipeParameter("MinimumValidSampleCount", "1"),
            };
            var initialDocument = CreateDocument(fixtureSource, sourcePath, initialInputIds, initialParameters);
            var contextStep = initialDocument.Steps[0];
            var contextBefore = initialDocument.CreateStepExecutionContext(contextStep.Id);
            Check("descriptive recipe metadata does not invalidate calculation context", contextBefore == (initialDocument with
            {
                Name = "renamed", Category = "changed", InspectionTarget = "another label", Description = "new description",
                Source = initialDocument.Source with { Name = "source label" }, Steps = [contextStep with { ToolName = "tool label" }]
            }).CreateStepExecutionContext(contextStep.Id), "labels are separate from input and calculation conditions");
            var independent = contextStep with { Id = "step.independent", OutputEntityId = "output.independent", Parameters = [new("MinimumThickness", "123")] };
            var branched = initialDocument with { Steps = [contextStep, independent] };
            Check("independent graph changes preserve the unaffected step context", branched.CreateStepExecutionContext(contextStep.Id) == contextBefore
                && (branched with { Steps = [contextStep, independent with { OutputEnabled = false }] }).CreateStepExecutionContext(contextStep.Id) == contextBefore,
                "adding, editing and disabling an independent tool");
            var upstream = contextStep with { Id = "step.upstream", ToolId = "filter", OutputEntityId = "output.upstream", InputEntityIds = [initialDocument.Source.Id], Parameters = [new("KernelSize", "3")] };
            var consumer = contextStep with { InputEntityIds = [upstream.OutputEntityId, .. contextStep.InputEntityIds.Skip(1)] };
            var dependent = initialDocument with { Steps = [upstream, consumer, independent] };
            var changedUpstream = dependent with { Steps = [upstream with { Parameters = [new("KernelSize", "5")] }, consumer, independent] };
            Check("upstream settings invalidate only their dependency closure", dependent.CreateStepExecutionContext(consumer.Id) != changedUpstream.CreateStepExecutionContext(consumer.Id)
                && dependent.CreateStepExecutionContext(independent.Id) == changedUpstream.CreateStepExecutionContext(independent.Id), "filter change and independent measurement");
            var unusedRoi = initialDocument.Selections![0] with { Id = "selection.unused", GridRectangle = new(1, 1, 1, 1) };
            Check("unused ROI edits preserve current calculation context", (initialDocument with { Selections = [.. initialDocument.Selections, unusedRoi] }).CreateStepExecutionContext(contextStep.Id) == contextBefore, unusedRoi.Id);
            var changedRoi = initialDocument.Selections[0] with { GridRectangle = new(1, 1, 1, 1) };
            Check("used ROI edits invalidate its calculation context", (initialDocument with { Selections = [changedRoi, .. initialDocument.Selections.Skip(1)] }).CreateStepExecutionContext(contextStep.Id) != contextBefore, changedRoi.Id);
            var artifactBoundRoi = unusedRoi with { SourceBinding = unusedRoi.SourceBinding with { OwnerEntityId = upstream.OutputEntityId } };
            var roiConsumer = contextStep with { InputEntityIds = [initialDocument.Source.Id, artifactBoundRoi.Id] };
            var roiDependency = initialDocument with { Steps = [upstream, roiConsumer], Selections = [artifactBoundRoi] };
            Check("ROI artifact owner participates in the dependency closure", roiDependency.CreateStepExecutionContext(roiConsumer.Id)
                != (roiDependency with { Steps = [changedUpstream.Steps[0], roiConsumer] }).CreateStepExecutionContext(roiConsumer.Id), artifactBoundRoi.SourceBinding.OwnerEntityId!);
            Check("removed producers invalidate downstream context", dependent.CreateStepExecutionContext(consumer.Id)
                != (dependent with { Steps = [consumer, independent] }).CreateStepExecutionContext(consumer.Id), upstream.Id);
            Check("cycle context traversal terminates without approving the graph", (dependent with { Steps = [upstream with { InputEntityIds = [consumer.OutputEntityId] }, consumer] })
                .CreateStepExecutionContext(consumer.Id).Length > 0, "graph validator still owns cycle admission");
            var legacyPath = Path.Combine(fixtureRoot, "legacy-1.9.json");
            var legacyDocument = initialDocument with { SchemaVersion = ToolRecipeDocument.RecipeMetadataSchemaVersion };
            ToolRecipeDocumentStore.SaveNew(legacyPath, legacyDocument);
            var legacy = ToolRecipeDocumentStore.Load(legacyPath);
            Check("legacy missing limits flag retains judgement", legacy.Steps[0].AcceptanceLimitsConfigured is null
                && legacy.Steps[0].ApplyAcceptancePolicy(DisplayResult(boundaryMetrics)).Status == ResultStatus.Pass, legacy.SchemaVersion);
            var unconfiguredDocument = initialDocument with { Steps = [initialDocument.Steps[0] with { AcceptanceLimitsConfigured = false }] };
            var unconfiguredPath = Path.Combine(fixtureRoot, "unconfigured.json");
            ToolRecipeDocumentStore.SaveNew(unconfiguredPath, unconfiguredDocument);
            var reopenedUnconfigured = ToolRecipeDocumentStore.Load(unconfiguredPath);
            Check("unconfigured limits save and reopen", reopenedUnconfigured.Steps[0].AcceptanceLimitsConfigured == false
                && reopenedUnconfigured.SchemaVersion == ToolRecipeDocument.CurrentSchemaVersion, reopenedUnconfigured.SchemaVersion);
            var withheld = reopenedUnconfigured.Steps[0].ApplyAcceptancePolicy(DisplayResult(boundaryMetrics) with { Status = ResultStatus.Fail });
            var unconfiguredLimits = (string)limitSelector.Invoke(null, ["thickness", withheld, Array.Empty<ToolRecipeParameter>()])!;
            var expectedUnconfiguredLimits = OpenVisionLanguageService.CurrentLanguage == OpenVisionLanguage.English ? "Limits not configured" : "규격 미설정";
            Check("unconfigured limits withhold judgement without changing measured value", withheld.Status == ResultStatus.NotRun
                && withheld.Metrics[0].Value == 0.48 && withheld.Metrics[0].Status == ResultStatus.NotRun
                && unconfiguredLimits == expectedUnconfiguredLimits, withheld.Message + " | " + ToolWorkbenchViewModel.MeasurementResultMessage(withheld));
            var invalid = withheld with { Status = ResultStatus.Error, Message = "Invalid input" };
            Check("unconfigured limits preserve validity warnings", reopenedUnconfigured.Steps[0].ApplyAcceptancePolicy(invalid with { Status = ResultStatus.Warning }).Status == ResultStatus.Warning, "input validity remains visible");
            Check("unconfigured limits preserve input errors", ReferenceEquals(invalid, reopenedUnconfigured.Steps[0].ApplyAcceptancePolicy(invalid)), invalid.Message);
            var originalLegacyBytes = File.ReadAllBytes(legacyPath);
            var rejectedDowngrade = false;
            try { ToolRecipeDocumentStore.Save(legacyPath, unconfiguredDocument with { SchemaVersion = ToolRecipeDocument.RecipeMetadataSchemaVersion }); }
            catch (InvalidDataException) { rejectedDowngrade = true; }
            Check("unsupported downgrade cannot overwrite the prior recipe", rejectedDowngrade && originalLegacyBytes.SequenceEqual(File.ReadAllBytes(legacyPath)), "old file retained");
            ToolRecipeDocumentStore.Save(unconfiguredPath, unconfiguredDocument with { Steps = [unconfiguredDocument.Steps[0] with { AcceptanceLimitsConfigured = true }] });
            Check("explicit configured limits persist after recovery", ToolRecipeDocumentStore.Load(unconfiguredPath).Steps[0].AcceptanceLimitsConfigured == true, "saved true after rejected downgrade");
            var tool = ToolWorkbenchToolCatalog.Create().Single(item => item.Id == "thickness");
            var step = new ToolWorkbenchPipelineStepItem(
                "step.thickness",
                tool,
                string.Join("; ", initialInputIds),
                "measurement.output",
                initialDocument.Steps[0].Parameters);
            step.State = "Ready";
            step.UpdateResultPresentation("8 raw-height", "OK", true, false);
            Check("graph retains cached result status after selection resets readiness",
                step.FlowNodeStatusLabel == "OK" && step.State == "Ready" && !step.FlowNodeStatusHasIssue, step.FlowNodeStatusLabel);
            step.UpdateResultPresentation("8 raw-height", "Previous result", false, true);
            Check("graph marks previous failed result without changing execution state",
                step.FlowNodeStatusLabel == "Previous result" && step.FlowNodeStatusHasIssue && step.FlowNodeStatusIsFailure, step.FlowNodeStatusLabel);
            step.State = "Running";
            Check("running state takes precedence over cached result", step.FlowNodeStatusLabel == step.CanonicalStateLabel, step.FlowNodeStatusLabel);
            step.State = "Ready";
            step.OutputEnabled = false;
            Check("disabled output takes precedence over cached result", step.FlowNodeStatusLabel == step.CanonicalStateLabel, step.FlowNodeStatusLabel);
            step.OutputEnabled = true;
            step.UpdateResultPresentation(string.Empty, string.Empty, false, false);
            Check("cleared result has no stale failure decoration", !step.FlowNodeStatusIsFailure && step.ResultValueSummary.Length == 0, step.FlowNodeStatusLabel);
            var recipePathReads = 0;
            var mutationObserved = false;
            var logs = new List<string>();
            ToolWorkbenchHeightMeasurementExecutionOwner? owner = null;
            owner = new ToolWorkbenchHeightMeasurementExecutionOwner(
                () => true,
                () => step,
                () => false,
                () =>
                {
                    recipePathReads++;
                    if (recipePathReads == 2)
                    {
                        step.Parameters.Single(parameter => parameter.Name == "MinimumThickness").Value = "1";
                        mutationObserved = true;
                    }

                    return recipePath;
                },
                () => fixtureSource.EntityId,
                _ => null,
                _ => null,
                _ => null,
                outputEntityId => string.Equals(step.OutputEntityId, outputEntityId, StringComparison.OrdinalIgnoreCase)
                    ? step
                    : null,
                () => CreateDocument(
                    fixtureSource,
                    sourcePath,
                    step.InputEntityIds,
                    step.Parameters.Select(parameter => new ToolRecipeParameter(parameter.Name, parameter.Value)).ToArray()),
                (category, message) => logs.Add($"{category}:{message}"),
                _ => { },
                () => { });

            try
            {
                var previewResult = Task.Run(() => owner.PreviewAsync()).GetAwaiter().GetResult();
                var staleLog = logs.Any(log => log.Contains("start snapshot is no longer current", StringComparison.Ordinal));
                Check(
                    "changed-parameter-invalidates-in-flight-result",
                    mutationObserved
                    && !previewResult
                    && !owner.IsPreviewRunning
                    && !owner.HasCurrentPreview
                    && owner.CurrentOutput is null
                    && !owner.IsPreviewPublished
                    && step.State == "Preview stale"
                    && owner.ExecutionSummary.Contains("start snapshot", StringComparison.Ordinal)
                    && staleLog,
                    $"mutated={mutationObserved};previewResult={previewResult};running={owner.IsPreviewRunning};hasPreview={owner.HasCurrentPreview};stepState={step.State};summary={owner.ExecutionSummary};staleLog={staleLog}");
            }
            finally
            {
                owner.Dispose();
            }
            var steps = new[] { step, new ToolWorkbenchPipelineStepItem("step.second", tool, string.Join("; ", initialInputIds), "measurement.second", initialParameters, dualRoiRouting: initialDocument.Steps[0].DualRoiRouting) };
            var selected = steps[0];
            ToolRecipeDocument CurrentDocument() => initialDocument with
            {
                Steps = steps.Select(item => initialDocument.Steps[0] with
                {
                    Id = item.Id, OutputEntityId = item.OutputEntityId,
                    Parameters = item.Parameters.Select(parameter => new ToolRecipeParameter(parameter.Name, parameter.Value)).ToArray(),
                    AcceptanceLimitsConfigured = item.AcceptanceLimitsConfigured
                }).ToArray()
            };
            using var resultsOwner = new ToolWorkbenchHeightMeasurementExecutionOwner(
                () => true, () => selected, () => false, () => recipePath, () => fixtureSource.EntityId,
                _ => null, _ => null, _ => null,
                id => steps.FirstOrDefault(item => item.OutputEntityId == id), CurrentDocument,
                (_, _) => { }, _ => { }, () => { });
            var firstCompleted = Task.Run(() => resultsOwner.PreviewAsync()).GetAwaiter().GetResult();
            selected = steps[1];
            var secondCompleted = Task.Run(() => resultsOwner.PreviewAsync()).GetAwaiter().GetResult();
            Check("separate completed steps retain exact real outputs",
                firstCompleted && secondCompleted
                && resultsOwner.TryGetCompletedResult(steps[0], out var firstOutput, out var firstCurrent) && firstCurrent
                && resultsOwner.TryGetCompletedResult(steps[1], out var secondOutput, out var secondCurrent) && secondCurrent
                && firstOutput!.OutputEntityId == steps[0].OutputEntityId
                && secondOutput!.OutputEntityId == steps[1].OutputEntityId, "two SDK completions, separate output identities");
            steps[1].Parameters.Single(parameter => parameter.Name == "MaximumThickness").Value = "99999";
            Check("editing another completed step preserves the unaffected real result", resultsOwner.TryGetCompletedResult(steps[0], out _, out var unaffectedCurrent) && unaffectedCurrent
                && resultsOwner.TryGetCompletedResult(steps[1], out _, out var affectedCurrent) && !affectedCurrent, "one current result, one historical result, no execution");
            steps[1].Parameters.Single(parameter => parameter.Name == "MaximumThickness").Value = "100000";
            selected = steps[0];
            resultsOwner.RestoreSelectedResult();
            Check("selection restores an existing output without execution",
                resultsOwner.CurrentOutput?.OutputEntityId == selected.OutputEntityId
                && resultsOwner.HasCurrentPreview && !resultsOwner.IsPreviewRunning, "restored first output snapshot");
            var expectedConfiguredStatus = resultsOwner.CurrentOutput!.Result.Status;
            selected.AcceptanceLimitsConfigured = false;
            Check("acceptance policy changes invalidate prior result", resultsOwner.TryGetCompletedResult(selected, out _, out var limitsCurrent) && !limitsCurrent, "no implicit rerun");
            var measuredWithoutLimits = Task.Run(() => resultsOwner.PreviewAsync()).GetAwaiter().GetResult();
            Check("SDK measurement executes without acceptance judgement", measuredWithoutLimits && resultsOwner.CurrentOutput?.Result.Status == ResultStatus.NotRun
                && resultsOwner.CurrentOutput.Result.Metrics.Any(metric => metric.Name == "Mean"), resultsOwner.CurrentOutput?.Result.Message ?? "no result");
            selected.AcceptanceLimitsConfigured = true;
            var measuredWithLimits = Task.Run(() => resultsOwner.PreviewAsync()).GetAwaiter().GetResult();
            Check("explicit rerun restores configured judgement", measuredWithLimits && resultsOwner.CurrentOutput?.Result.Status == expectedConfiguredStatus, $"expected={expectedConfiguredStatus};actual={resultsOwner.CurrentOutput?.Result.Status}");
            selected.Parameters.Single(parameter => parameter.Name == "MaximumThickness").Value = "99999";
            Check("changed conditions make retained rows historical",
                resultsOwner.TryGetCompletedResult(selected, out _, out var changedCurrent) && !changedCurrent,
                "previous measurement retained; current fingerprint differs");
            Check("previous result preserves its execution-time limits",
                resultsOwner.GetCompletedParameters(selected).Single(parameter => parameter.Name == "MaximumThickness").Value == "100000",
                "edited limit 99999; completed limit 100000");
            resultsOwner.ForgetCompletedResult(selected);
            resultsOwner.RestoreSelectedResult();
            Check("tool removal forgets its result without discarding an independent result", !resultsOwner.TryGetCompletedResult(selected, out _, out _)
                && resultsOwner.CurrentOutput is null && resultsOwner.TryGetCompletedResult(steps[1], out _, out var remainingCurrent) && remainingCurrent, "same step ID cannot recover a deleted calculation");
            resultsOwner.Clear("new input");
            Check("input reset releases all retained outputs",
                !resultsOwner.TryGetCompletedResult(steps[0], out _, out _) && resultsOwner.CurrentOutput is null,
                "no cross-input result reuse");

        }
        catch (Exception exception)
        {
            lines.Add($"FAIL | unexpected | {exception}");
        }
        finally
        {
            if (Directory.Exists(fixtureRoot))
            {
                Directory.Delete(fixtureRoot, recursive: true);
            }
        }

        var succeeded = passed == total && total > 0 && !lines.Any(line => line.StartsWith("FAIL | unexpected", StringComparison.Ordinal));
        lines.Add($"Result: {(succeeded ? "Pass" : "Fail")} ({passed}/{total} checks)");
        Directory.CreateDirectory(reportDirectory);
        File.WriteAllLines(fullReportPath, lines);
        summary = $"HeightMeasurementSnapshot|pass={succeeded}|checks={passed}/{total}|report={fullReportPath}";
        return succeeded;
    }

    private static string? FindHeightMeasurementOwnerPath()
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
        {
            var directory = new DirectoryInfo(start);
            while (directory is not null)
            {
                var candidate = Path.Combine(
                    directory.FullName,
                    "src",
                    "OpenVisionLab.ThreeD.Shell",
                    "ViewModels",
                    "Workbench",
                    "ToolWorkbenchHeightMeasurementExecutionOwner.cs");
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }
        }

        return null;
    }

    private static ToolRecipeDocument CreateDocument(
        C3DHeightFieldSnapshot source,
        string sourcePath,
        IReadOnlyList<string> inputEntityIds,
        IReadOnlyList<ToolRecipeParameter> parameters)
    {
        var binding = ToolRecipeSelectionSourceBindingVerifier.ReadIdentity(sourcePath);
        var reference = new ToolRecipeSelection(
            "selection.reference",
            "Reference ROI",
            ToolRecipeSelectionKinds.GridRectangle,
            source.EntityId,
            source.FrameId,
            binding,
            new ToolRecipeGridRectangle(0, 0, 2, 2),
            null,
            null);
        var measurement = reference with
        {
            Id = "selection.measurement",
            Name = "Measurement ROI",
            GridRectangle = new ToolRecipeGridRectangle(2, 2, 2, 2)
        };
        return new ToolRecipeDocument(
            ToolRecipeDocument.CurrentSchemaVersion,
            "Height measurement snapshot fixture",
            new ToolRecipeSource(
                source.EntityId,
                "Snapshot fixture",
                "C3D",
                source.Unit,
                source.FrameId,
                Path.GetFileName(sourcePath),
                source.ByteLength,
                source.ContentSha256,
                source.Width,
                source.Height),
            [],
            [new ToolRecipeStep(
                "step.thickness",
                "thickness",
                "Thickness",
                3,
                inputEntityIds.ToArray(),
                "measurement.output",
                parameters.ToArray(),
                new ToolRecipeDualRoiRouting(reference.Id, measurement.Id))],
            [reference, measurement]);
    }
}
