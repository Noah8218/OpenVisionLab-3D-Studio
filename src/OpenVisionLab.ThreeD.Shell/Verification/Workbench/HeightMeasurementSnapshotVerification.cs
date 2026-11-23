using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Tools;

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
            // 휨 결과 표: 출력(P–V, RMS)마다 행을 두고 허용 범위는 "≤ 상한", 상태는 출력별 상한 초과로 표시한다.
            var warpageStep = new ToolWorkbenchPipelineStepItem("step.warpage", ToolWorkbenchToolCatalog.Create().Single(item => item.Id == "warpage"),
                string.Join("; ", initialInputIds), "warpage.output", []);
            Metric[] WarpageMetrics(double peakToValley, double rms) =>
                [new("PeakToValley", MetricKind.Deviation, peakToValley, "raw-height"), new("Rms", MetricKind.Deviation, rms, "raw-height"),
                    new("ValidSampleCount", MetricKind.Count, 23040, "count"),
                    new("MaximumPeakToValley", MetricKind.Deviation, 5, "raw-height"), new("MaximumRms", MetricKind.Deviation, 2, "raw-height")];
            TeachingMeasurementResultItem WarpageRow(Metric[] rowMetrics, ResultStatus? execution = ResultStatus.Fail) =>
                new(warpageStep, execution == ResultStatus.Fail ? "Fail" : "Pass", "PeakToValley: 1", "raw-height",
                    "MaximumPeakToValley: 5 raw-height; MaximumRms: 2 raw-height", "reason", true, rowMetrics) { ExecutionStatus = execution, Sequence = 9 };
            var bothExceed = WarpageRow(WarpageMetrics(7.93, 2.11));
            var bothExceedRms = bothExceed with { OutputMetricName = "Rms", IsSecondaryOutput = true };
            Check("warpage table shows P–V and RMS as separate output rows with plain limits",
                bothExceed.OutputName == "P–V" && bothExceedRms.OutputName == "RMS"
                && bothExceed.DisplayLimits == "≤ 5.00" && bothExceedRms.DisplayLimits == "≤ 2.00"
                && !bothExceed.DisplayLimits.Contains("Maximum", StringComparison.Ordinal) && bothExceedRms.DisplayValue == "2.11",
                $"{bothExceed.OutputName}:{bothExceed.DisplayLimits}|{bothExceedRms.OutputName}:{bothExceedRms.DisplayLimits}:{bothExceedRms.DisplayValue}");
            Check("warpage secondary output row repeats neither the tool name, sequence, samples nor run time",
                bothExceed.SequenceText == "09" && bothExceed.ToolNameText == warpageStep.ToolName && warpageStep.ToolName.Length > 0
                && bothExceedRms.SequenceText.Length == 0 && bothExceedRms.ToolNameText.Length == 0
                && bothExceedRms.ValidSamples.Length == 0 && bothExceedRms.RunTime.Length == 0 && !bothExceedRms.IsPrimaryOutput,
                $"seq='{bothExceedRms.SequenceText}';tool='{bothExceedRms.ToolNameText}'");
            Check("warpage output status marks each output that exceeds its own limit",
                bothExceed.OutputStatusText == "NG" && bothExceedRms.OutputStatusText == "NG"
                && bothExceedRms.OutputStatusReason.Contains("P–V 7.93 > 5", StringComparison.Ordinal) && bothExceedRms.OutputStatusReason.Contains("RMS 2.11 > 2", StringComparison.Ordinal),
                bothExceedRms.OutputStatusReason);
            var rmsOnly = WarpageRow(WarpageMetrics(4, 2.5));
            var rmsOnlyRms = rmsOnly with { OutputMetricName = "Rms", IsSecondaryOutput = true };
            Check("warpage P–V within its limit reads OK beside an RMS NG while the tool result stays NG",
                rmsOnly.OutputStatusText == "OK" && rmsOnlyRms.OutputStatusText == "NG" && rmsOnly.State == "Fail" && rmsOnly.StatusText == "NG",
                $"{rmsOnly.OutputStatusText}/{rmsOnlyRms.OutputStatusText}/{rmsOnly.State}");
            var noLimitExceeded = WarpageRow(WarpageMetrics(4, 1));
            Check("warpage NG without an exceeded limit keeps the tool-level status on every output",
                noLimitExceeded.OutputStatusText == noLimitExceeded.StatusText && noLimitExceeded.OutputStatusReason == noLimitExceeded.Reason, noLimitExceeded.OutputStatusText);
            var unjudged = WarpageRow(WarpageMetrics(7.93, 2.11), null);
            Check("unjudged warpage keeps the stored limits text", unjudged.DisplayLimits == unjudged.Limits && unjudged.OutputStatusText == unjudged.StatusText, unjudged.DisplayLimits);
            // 새로 추가하는 두께·휨 도구의 기본 최소 유효 샘플 수는 시안 값(3,000 / 1,000)이다. 이미 저장된 레시피의 값은 이 기본값과 무관하다.
            var catalogTools = ToolWorkbenchToolCatalog.Create();
            string CatalogDefault(string toolId) => catalogTools.Single(item => item.Id == toolId).Parameters.Single(parameter => parameter.Name == "MinimumValidSampleCount").DefaultValue;
            Check("new Thickness and Warpage start with the mockup's minimum valid sample counts",
                CatalogDefault("thickness") == "3000" && CatalogDefault("warpage") == "1000", $"{CatalogDefault("thickness")}/{CatalogDefault("warpage")}");
            // 검증 화면: NG 샘플의 원인 문구와 휨 지표의 표시용 상태. 결과 표와 같은 비교 규칙(상한 지표 초과)이다.
            ToolRecipeValidationStepResult ValidationStep(string toolName, ResultStatus status, Metric[] stepMetrics) => new(1, "step.1", toolName, status, "evidence", stepMetrics, []);
            var warpageCause = ValidationFailureCauseText.Describe([ValidationStep("Warpage", ResultStatus.Fail, WarpageMetrics(7.93, 2.11))]);
            Check("validation cause names each exceeded warpage output with its limit",
                warpageCause == "Warpage: P–V 7.93 > 5 · RMS 2.11 > 2", warpageCause);
            var rmsOnlyCause = ValidationFailureCauseText.Describe([ValidationStep("Warpage", ResultStatus.Fail, WarpageMetrics(4, 2.5))]);
            Check("validation cause lists only the output that exceeds its limit", rmsOnlyCause == "Warpage: RMS 2.5 > 2", rmsOnlyCause);
            var thicknessCause = ValidationFailureCauseText.Describe([ValidationStep("Thickness", ResultStatus.Fail,
                [new("Mean", MetricKind.Length, 694.134, "raw-height", ResultStatus.Fail), new("LowerLimit", MetricKind.Length, 600, "raw-height"), new("UpperLimit", MetricKind.Length, 690, "raw-height")])]);
            Check("validation cause names a thickness mean above its upper limit", thicknessCause.StartsWith("Thickness: ", StringComparison.Ordinal) && thicknessCause.EndsWith("694.13 > 690", StringComparison.Ordinal), thicknessCause);
            var belowCause = ValidationFailureCauseText.Describe([ValidationStep("Thickness", ResultStatus.Fail,
                [new("Mean", MetricKind.Length, 590.5, "raw-height", ResultStatus.Fail), new("LowerLimit", MetricKind.Length, 600, "raw-height"), new("UpperLimit", MetricKind.Length, 690, "raw-height")])]);
            Check("validation cause names a thickness mean below its lower limit", belowCause.EndsWith("590.5 < 600", StringComparison.Ordinal), belowCause);
            var thicknessAllFailCause = ValidationFailureCauseText.Describe([ValidationStep("Thickness", ResultStatus.Fail,
                [new("Mean", MetricKind.Length, 768.141, "raw-height", ResultStatus.Fail), new("Minimum", MetricKind.Length, 734.458, "raw-height", ResultStatus.Fail),
                    new("Maximum", MetricKind.Length, 794.183, "raw-height", ResultStatus.Fail), new("LowerLimit", MetricKind.Length, 600, "raw-height"), new("UpperLimit", MetricKind.Length, 722, "raw-height")])]);
            Check("validation cause names only the first exceeded thickness value when mean, minimum and maximum all fail",
                thicknessAllFailCause.EndsWith("768.14 > 722", StringComparison.Ordinal) && thicknessAllFailCause.Count(character => character == '>') == 1, thicknessAllFailCause);
            var noLimitCause = ValidationFailureCauseText.Describe([ValidationStep("Warpage", ResultStatus.Fail, WarpageMetrics(4, 1)), ValidationStep("Thickness", ResultStatus.Pass, WarpageMetrics(9, 9))]);
            Check("validation cause is empty when no limit is exceeded or the step did not fail", noLimitCause.Length == 0, noLimitCause);
            var twoStepCause = ValidationFailureCauseText.Describe([ValidationStep("Warpage", ResultStatus.Fail, WarpageMetrics(7.93, 1)), ValidationStep("Warpage 2", ResultStatus.Fail, WarpageMetrics(4, 3))]);
            Check("validation cause joins failing steps in order", twoStepCause == "Warpage: P–V 7.93 > 5; Warpage 2: RMS 3 > 2", twoStepCause);
            var failedWarpage = ValidationStep("Warpage", ResultStatus.Fail, WarpageMetrics(7.93, 1));
            Check("warpage P–V and RMS get a display status from their limit metrics",
                ValidationSetWorkspaceViewModel.LimitedMetricStatus(failedWarpage, failedWarpage.Metrics[0]) == ResultStatus.Fail
                && ValidationSetWorkspaceViewModel.LimitedMetricStatus(failedWarpage, failedWarpage.Metrics[1]) == ResultStatus.Pass
                && ValidationSetWorkspaceViewModel.LimitedMetricStatus(failedWarpage, failedWarpage.Metrics[2]) is null
                && ValidationSetWorkspaceViewModel.LimitedMetricStatus(failedWarpage, failedWarpage.Metrics[3]) is null,
                string.Join("/", failedWarpage.Metrics.Select(metric => ValidationSetWorkspaceViewModel.LimitedMetricStatus(failedWarpage, metric)?.ToString() ?? "-")));
            var erroredWarpage = ValidationStep("Warpage", ResultStatus.Error, WarpageMetrics(7.93, 1));
            Check("an errored step gets no display status", ValidationSetWorkspaceViewModel.LimitedMetricStatus(erroredWarpage, erroredWarpage.Metrics[0]) is null, "error step");

            // 두께 기준면 최대 RMS(시안 B2, 선택 파라미터): 레시피 계약 규칙, 계산 거부, 한글 문구, 편집 값 검사.
            // 값이 없으면 기존 레시피와 같은 동작이어야 하고, 값이 있어도 한계 이하면 결과 판정은 그대로여야 한다.
            ToolRecipeParameter Param(string name, string value) => new(name, value);
            var referenceFitName = ToolRecipeParameterContract.ThicknessMaximumReferenceFitRms;
            ToolRecipeParameter[] thicknessRequired = [Param("MinimumThickness", "0"), Param("MaximumThickness", "10"), Param("MinimumValidSampleCount", "1")];
            bool HasParameters(string toolId, params ToolRecipeParameter[] parameters) => ToolRecipeParameterContract.HasExpectedHeightMeasurementParameters(toolId, parameters);
            Check("thickness parameter contract accepts the required set with or without the optional reference-fit RMS limit",
                HasParameters("thickness", thicknessRequired) && HasParameters("thickness", [.. thicknessRequired, Param(referenceFitName, "0.05")]), "required=3; optional=0|1");
            Check("thickness parameter contract rejects a repeated optional limit, an unknown name and a missing required name",
                !HasParameters("thickness", [.. thicknessRequired, Param(referenceFitName, "0.05"), Param(referenceFitName, "0.06")])
                && !HasParameters("thickness", [.. thicknessRequired, Param("Extra", "1")])
                && !HasParameters("thickness", [.. thicknessRequired.Skip(1)])
                && !HasParameters("thickness", [.. thicknessRequired.Skip(1), Param(referenceFitName, "0.05")]), "repeat/unknown/missing");
            Check("only thickness takes the optional limit; warpage keeps its exact parameter set",
                HasParameters("warpage", Param("MaximumPeakToValley", "5"), Param("MaximumRms", "2"), Param("MinimumValidSampleCount", "3"))
                && !HasParameters("warpage", Param("MaximumPeakToValley", "5"), Param("MaximumRms", "2"), Param("MinimumValidSampleCount", "3"), Param(referenceFitName, "0.05")),
                "optional names: " + string.Join(",", ToolRecipeParameterContract.GetOptionalHeightMeasurementParameterNames("warpage")));
            bool ParsesLimit(string? text) => ToolRecipeParameterContract.TryParseThicknessMaximumReferenceFitRms(text, out _);
            Check("reference-fit RMS limit text is an invariant finite number greater than zero",
                ParsesLimit("0.05") && ParsesLimit("1e-3") && !ParsesLimit("0") && !ParsesLimit("-1") && !ParsesLimit("abc") && !ParsesLimit("1,5")
                && !ParsesLimit(" 0.05") && !ParsesLimit("") && !ParsesLimit(null) && !ParsesLimit("NaN") && !ParsesLimit("Infinity"), "0.05|1e-3 accepted; zero, negative, text, comma, spaces, NaN, Infinity rejected");

            // 기준 영역: 평면 위에서 ±0.5씩 번갈아 벗어난 격자(맞춤 RMS 0.5). 측정 영역은 약 5만큼 높다.
            // 위치의 Y와 원시 높이는 같은 높이 값이어야 한다(표본이 높이 맵 위의 한 점이다).
            HeightFieldPlaneSample PlaneSample(int x, int z, double height) => new(new System.Numerics.Vector3(x, (float)height, z), height);
            var referencePlaneSamples = Enumerable.Range(0, 6).SelectMany(x => Enumerable.Range(0, 6)
                .Select(z => PlaneSample(x, z, 10d + ((x + z) % 2 == 0 ? 0.5 : -0.5)))).ToArray();
            var measurementPlaneSamples = Enumerable.Range(0, 4).SelectMany(x => Enumerable.Range(0, 4)
                .Select(z => PlaneSample(x + 10, z + 10, 15d))).ToArray();
            DualSurfaceThicknessEvaluation EvaluateThickness(double? rmsLimit) => DualSurfaceThicknessRule.Evaluate(
                new DualSurfaceThicknessInput("source.fixture", referencePlaneSamples, measurementPlaneSamples, 0, 100, 1, "raw-height", rmsLimit));
            double MetricValue(DualSurfaceThicknessEvaluation evaluation, string name) => evaluation.Result.Metrics.Single(metric => metric.Name == name).Value;
            var unlimitedThickness = EvaluateThickness(null);
            var measuredReferenceRms = MetricValue(unlimitedThickness, DualSurfaceThicknessRule.ReferenceFitRmsMetricName);
            Check("without the limit a thickness result is unchanged and carries no limit metric",
                unlimitedThickness.Result.Status == ResultStatus.Pass && Math.Abs(measuredReferenceRms - 0.5) < 1e-9
                && Math.Abs(unlimitedThickness.Mean - 5) < 1e-9
                && unlimitedThickness.Result.Metrics.All(metric => metric.Name != DualSurfaceThicknessRule.MaximumReferenceFitRmsMetricName),
                $"status={unlimitedThickness.Result.Status};referenceRms={measuredReferenceRms:G9};mean={unlimitedThickness.Mean:G9}");
            var looseLimit = EvaluateThickness(1.0);
            Check("a reference surface within the limit keeps the thickness verdict and records the limit",
                looseLimit.Result.Status == ResultStatus.Pass && Math.Abs(looseLimit.Mean - unlimitedThickness.Mean) < 1e-12
                && MetricValue(looseLimit, DualSurfaceThicknessRule.MaximumReferenceFitRmsMetricName) == 1.0,
                $"status={looseLimit.Result.Status};limit={MetricValue(looseLimit, DualSurfaceThicknessRule.MaximumReferenceFitRmsMetricName)}");
            Check("a reference surface exactly at the limit is accepted", EvaluateThickness(measuredReferenceRms).Result.Status == ResultStatus.Pass, $"limit={measuredReferenceRms:G17}");
            var tightLimit = EvaluateThickness(0.1);
            Check("a reference surface above the limit gives an error with the measured RMS and the limit, not a thickness",
                tightLimit.Result.Status == ResultStatus.Error && double.IsNaN(tightLimit.Mean) && tightLimit.ReferencePlane is null
                && tightLimit.Result.Message.StartsWith(DualSurfaceThicknessRule.ReferenceFitExceededMessagePrefix, StringComparison.Ordinal)
                && Math.Abs(MetricValue(tightLimit, DualSurfaceThicknessRule.ReferenceFitRmsMetricName) - 0.5) < 1e-9
                && MetricValue(tightLimit, DualSurfaceThicknessRule.MaximumReferenceFitRmsMetricName) == 0.1,
                tightLimit.Result.Message);
            var languageBeforeReasons = OpenVisionLanguageService.CurrentLanguage;
            string koreanReason, englishReason, koreanValidationReason;
            try
            {
                OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
                koreanReason = ToolWorkbenchViewModel.MeasurementResultMessage(tightLimit.Result);
                koreanValidationReason = ThicknessReferenceFitMessage.Localize("Thickness: " + tightLimit.Result.Message);
                OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
                englishReason = ToolWorkbenchViewModel.MeasurementResultMessage(tightLimit.Result);
            }
            finally
            {
                OpenVisionLanguageService.SetLanguage(languageBeforeReasons, save: false);
            }

            Check("Korean result and validation reasons keep the measured RMS and the limit, and English keeps the original sentence",
                koreanReason.Contains("기준면 맞춤 RMS 0.5 > 상한 0.1 raw-height", StringComparison.Ordinal) && !koreanReason.Contains("Reference surface", StringComparison.Ordinal)
                && koreanValidationReason == koreanReason && englishReason == tightLimit.Result.Message,
                $"ko={koreanReason}|validation={koreanValidationReason}|en={englishReason}");
            ThicknessStepProperties ThicknessDraft(string limit) => new() { MinimumThickness = 0, MaximumThickness = 10, MinimumValidSampleCount = 1, MaximumReferenceFitRms = limit };
            var smallLimitDraft = ThicknessDraft("0.0005");
            var smallLimitAccepted = smallLimitDraft.TryValidate(out var smallLimitMessage);
            Check("the editor accepts an empty limit and rejects values below 0.001 or malformed text",
                ThicknessDraft("").TryValidate(out _) && ThicknessDraft("   ").TryValidate(out _) && ThicknessDraft("0.05").TryValidate(out _) && ThicknessDraft("0.001").TryValidate(out _)
                && !smallLimitAccepted && smallLimitMessage.Contains("0.001", StringComparison.Ordinal)
                && !ThicknessDraft("0").TryValidate(out _) && !ThicknessDraft("-1").TryValidate(out _) && !ThicknessDraft("abc").TryValidate(out _) && !ThicknessDraft("1,5").TryValidate(out _),
                smallLimitMessage);
            string LimitMessage(string text) { ThicknessDraft(text).TryValidate(out var limitMessage); return limitMessage; }
            Check("zero and negative limits get the minimum message and malformed text gets the format message",
                LimitMessage("0") == ThicknessStepProperties.ReferenceFitMinimumMessage && LimitMessage("-1") == ThicknessStepProperties.ReferenceFitMinimumMessage
                && LimitMessage("0.0005") == ThicknessStepProperties.ReferenceFitMinimumMessage
                && LimitMessage("abc") == ThicknessStepProperties.ReferenceFitFormatMessage && LimitMessage("1,5") == ThicknessStepProperties.ReferenceFitFormatMessage
                && LimitMessage("NaN") == ThicknessStepProperties.ReferenceFitFormatMessage,
                $"0={LimitMessage("0")}|abc={LimitMessage("abc")}");
            var languageBeforeStatus = OpenVisionLanguageService.CurrentLanguage;
            string koreanMinimumStatus, koreanFormatStatus, englishMinimumStatus;
            try
            {
                OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
                koreanMinimumStatus = ThreeDLocalization.Shared.LocalizeStepParameterStatus(ThicknessStepProperties.ReferenceFitMinimumMessage);
                koreanFormatStatus = ThreeDLocalization.Shared.LocalizeStepParameterStatus(ThicknessStepProperties.ReferenceFitFormatMessage);
                OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
                englishMinimumStatus = ThreeDLocalization.Shared.LocalizeStepParameterStatus(ThicknessStepProperties.ReferenceFitMinimumMessage);
            }
            finally
            {
                OpenVisionLanguageService.SetLanguage(languageBeforeStatus, save: false);
            }

            Check("the two limit messages are shown in Korean on the Korean screen and unchanged in English",
                koreanMinimumStatus == "기준면 최대 RMS는 0.001 이상으로 입력하거나 비워 두세요."
                && koreanFormatStatus == "기준면 최대 RMS는 쉼표 없이 유한한 수로 입력하거나 비워 두세요."
                && englishMinimumStatus == ThicknessStepProperties.ReferenceFitMinimumMessage,
                $"ko={koreanMinimumStatus}|{koreanFormatStatus}|en={englishMinimumStatus}");
            var emptyWritten = ToolWorkbenchStepPropertyAdapterCatalog.TryCreateParameterValues(ThicknessDraft(""), out var emptyValues, out _);
            var setWritten = ToolWorkbenchStepPropertyAdapterCatalog.TryCreateParameterValues(ThicknessDraft(" 0.05 "), out var setValues, out _);
            Check("applying an empty limit writes no optional key and a value is written as typed",
                emptyWritten && emptyValues.Count == 3 && !emptyValues.ContainsKey(referenceFitName)
                && setWritten && setValues.Count == 4 && setValues[referenceFitName] == "0.05",
                $"empty={string.Join(",", emptyValues.Keys)}|set={(setValues.TryGetValue(referenceFitName, out var written) ? written : "(none)")}");
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
