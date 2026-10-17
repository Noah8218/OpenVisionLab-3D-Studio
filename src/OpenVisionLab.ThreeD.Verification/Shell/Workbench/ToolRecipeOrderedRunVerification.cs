using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Reporting.RunRecords;
using OpenVisionLab.ThreeD.Shell;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Verification.Shell.Workbench;

internal static class ToolRecipeOrderedRunVerification
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static bool Verify(string reportPath, out string summary)
    {
        var fullReportPath = Path.GetFullPath(reportPath);
        var reportDirectory = Path.GetDirectoryName(fullReportPath)
            ?? throw new InvalidOperationException("Ordered Run verification report has no directory.");
        var lines = new List<string>
        {
            "OpenVisionLab 3D Studio current-recipe ordered Run verification",
            $"Generated: {DateTimeOffset.Now:O}"
        };
        var total = 0;
        var passed = 0;
        void Check(string name, bool condition, string detail)
        {
            total++;
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
            if (condition) passed++;
        }

        try
        {
            Directory.CreateDirectory(reportDirectory);
            var root = Path.Combine(
                reportDirectory,
                "ordered-run-verification",
                Guid.NewGuid().ToString("N"));
            var runRoot = Path.Combine(root, "runs");
            Directory.CreateDirectory(root);

            var sourcePath = Path.Combine(root, "thickness-pass.C3D");
            C3DHeightFieldSnapshot.CreateForVerification(
                "source.c3d.height-map",
                4,
                4,
                [10, 10, 10, 10, 10, 10, 10, 10, 15, 15, 15, 15, 15, 15, 15, 15])
                .SaveC3D(sourcePath);
            var passRecipePath = Path.Combine(root, "thickness-pass.ov3d-recipe.json");
            var passDocument = CreateThicknessDocument(sourcePath, 4.5, 5.5) with
            {
                SchemaVersion = ToolRecipeDocument.OutputPolicySchemaVersion
            };
            ToolRecipeDocumentStore.Save(passRecipePath, passDocument);

            var ownerLifecycle = VerifyOrderedRunOwnerLifecycle(
                passDocument,
                passRecipePath,
                out var ownerLifecycleDetail);
            Check(
                "ordered Run owner rejects concurrent callers and supports cancellation",
                ownerLifecycle,
                ownerLifecycleDetail);

            var shell = CreateShell(root, "pass", runRoot);
            var initialResultsSummary = shell.ResultsOperatorDecisionSummary;
            Check(
                "Results reports the missing current Run Record before any Run",
                initialResultsSummary.Contains("NotRun", StringComparison.Ordinal)
                && (initialResultsSummary.Contains("Not recorded", StringComparison.Ordinal)
                    || initialResultsSummary.Contains("기록되지 않음", StringComparison.Ordinal)),
                initialResultsSummary.Replace(Environment.NewLine, " | "));
            var fullRunCount = 0;
            shell.Workbench.OrderedRunCompleted += (_, _) => fullRunCount++;
            var readinessPropertyChanges = new List<string?>();
            shell.Workbench.PropertyChanged += (_, args) => readinessPropertyChanges.Add(args.PropertyName);
            var initialReadinessStatus = shell.Workbench.OrderedRunStatus;
            var opened = shell.Workbench.TryOpenTeachingRecipe(
                passRecipePath,
                out var openMessage);
            Check(
                "saved valid Thickness enables current-recipe Run",
                opened
                && shell.Workbench.RunTeachingRecipeCommand.CanExecute(null)
                && shell.Workbench.OrderedRunStatus == shell.Workbench.Localization.RecipeHealthReady
                && shell.Workbench.OrderedRunCapabilitySummary.Contains("1", StringComparison.Ordinal),
                opened ? shell.Workbench.OrderedRunCapabilitySummary : openMessage);
            Check(
                "opening a saved recipe refreshes the bound Run readiness projection",
                opened
                && shell.Workbench.RunTeachingRecipeCommand.CanExecute(null)
                && !string.Equals(
                    initialReadinessStatus,
                    shell.Workbench.OrderedRunStatus,
                    StringComparison.Ordinal)
                && readinessPropertyChanges.Contains(nameof(ToolWorkbenchViewModel.OrderedRunStatus))
                && readinessPropertyChanges.Contains(nameof(ToolWorkbenchViewModel.OrderedRunCapabilitySummary)),
                $"initial={initialReadinessStatus};current={shell.Workbench.OrderedRunStatus};notifications={string.Join(',', readinessPropertyChanges.Where(name => name is not null))}");
            Check(
                "open does not auto-run",
                fullRunCount == 0
                && !shell.Workbench.HasOrderedRunResult
                && shell.InspectionSteps.Count == 0,
                $"count={fullRunCount}; result={shell.Workbench.HasOrderedRunResult}; recordSteps={shell.InspectionSteps.Count}");

            var previewed = shell.Workbench.PreviewSelectedMeasurementAsync()
                .GetAwaiter().GetResult();
            shell.Workbench.PublishSelectedStepCommand.Execute(null);
            Check(
                "Preview and Publish do not invoke full Run",
                previewed
                && shell.Workbench.IsMeasurementPreviewPublished
                && fullRunCount == 0
                && !shell.Workbench.HasOrderedRunResult,
                $"preview={previewed}; published={shell.Workbench.IsMeasurementPreviewPublished}; fullRuns={fullRunCount}");
            var previewOutput = shell.Workbench.CurrentMeasurementOutput;
            var saved = shell.Workbench.TrySaveTeachingRecipe(
                passRecipePath,
                out var saveMessage);
            Check(
                "save does not invoke full Run or change the published Preview output",
                saved
                && fullRunCount == 0
                && ReferenceEquals(previewOutput, shell.Workbench.CurrentMeasurementOutput),
                saved ? $"fullRuns={fullRunCount}" : saveMessage);
            shell.Workbench.SelectPipelineStep(passDocument.Steps[0].Id);
            readinessPropertyChanges.Clear();
            shell.Workbench.MarkSelectedStepParameterDraftDirty();
            var pendingDraftCanRun = shell.Workbench.RunTeachingRecipeCommand.CanExecute(null);
            Check(
                "pending parameter draft refreshes blocked readiness without running",
                !pendingDraftCanRun
                && !string.Equals(
                    shell.Workbench.OrderedRunStatus,
                    shell.Workbench.Localization.RecipeHealthReady,
                    StringComparison.Ordinal)
                && readinessPropertyChanges.Contains(nameof(ToolWorkbenchViewModel.OrderedRunStatus))
                && readinessPropertyChanges.Contains(nameof(ToolWorkbenchViewModel.OrderedRunCapabilitySummary))
                && fullRunCount == 0
                && !shell.Workbench.HasOrderedRunResult,
                $"canRun={pendingDraftCanRun};status={shell.Workbench.OrderedRunStatus};summary={shell.Workbench.OrderedRunCapabilitySummary};notifications={string.Join(',', readinessPropertyChanges.Where(name => name is not null))}");
            readinessPropertyChanges.Clear();
            shell.Workbench.DiscardSelectedStepParameterDraft();
            Check(
                "discarding the parameter draft restores readiness without mutation or Run",
                shell.Workbench.RunTeachingRecipeCommand.CanExecute(null)
                && shell.Workbench.OrderedRunStatus == shell.Workbench.Localization.RecipeHealthReady
                && readinessPropertyChanges.Contains(nameof(ToolWorkbenchViewModel.OrderedRunStatus))
                && readinessPropertyChanges.Contains(nameof(ToolWorkbenchViewModel.OrderedRunCapabilitySummary))
                && !shell.Workbench.HasOrderedRunResult
                && fullRunCount == 0,
                $"canRun={shell.Workbench.RunTeachingRecipeCommand.CanExecute(null)};status={shell.Workbench.OrderedRunStatus};notifications={string.Join(',', readinessPropertyChanges.Where(name => name is not null))}");

            var reopenedShell = CreateShell(root, "reopen", runRoot);
            var reopenRunCount = 0;
            ToolRecipeOrderedGraphExecutionResult? reopenedExecution = null;
            reopenedShell.Workbench.OrderedRunCompleted += (_, args) =>
            {
                reopenRunCount++;
                reopenedExecution = args.Execution;
            };
            var reopened = reopenedShell.Workbench.TryOpenTeachingRecipe(
                passRecipePath,
                out var reopenMessage);
            var uiSourceQuality = WaitForSourceQuality(
                reopenedShell.Workbench.SourceQuality);
            Check(
                "reopen restores the saved recipe without running it",
                reopened
                && uiSourceQuality is not null
                && reopenRunCount == 0
                && !reopenedShell.Workbench.HasOrderedRunResult
                && reopenedShell.Workbench.RunTeachingRecipeCommand.CanExecute(null),
                reopened
                    ? $"{reopenedShell.Workbench.OrderedRunCapabilitySummary};sourceQuality={uiSourceQuality is not null}"
                    : reopenMessage);

            var runCompleted = reopenedShell.Workbench.RunTeachingRecipeAsync()
                .GetAwaiter().GetResult();
            var recordPath = reopenedShell.Workbench.CurrentOrderedRunRecordPath;
            var record = ReadRecord(recordPath);
            var gridDiagnosticsJson = JsonSerializer.Serialize(
                uiSourceQuality?.GridDiagnostics,
                JsonOptions);
            var orderedReport = record?.Artifacts.RunnerTextReport is { } orderedReportPath
                && File.Exists(orderedReportPath)
                    ? File.ReadAllText(orderedReportPath)
                    : string.Empty;
            Check(
                "explicit Run executes once and persists a Results-ready Run Record",
                runCompleted
                && reopenRunCount == 1
                && record is not null
                && record.SchemaVersion == InspectionRunRecord.CurrentSchemaVersion
                && record.Status == ResultStatus.Pass
                && record.Steps?.Count == 1
                && record.SourceQualityEvidence is
                {
                    State: InspectionRunSourceQualityEvidenceState.Available,
                    Report: not null
                } sourceQualityEvidence
                && sourceQualityEvidence.TryValidate(record.Source, out _)
                && reopenedShell.InspectionSteps.Count == 1
                && reopenedShell.InspectionSteps[0].Timing.Contains(
                    InspectionRunTiming.ToolExecutionStage,
                    StringComparison.Ordinal)
                && reopenedShell.RunSnapshotSummary.Contains("Pass", StringComparison.OrdinalIgnoreCase),
                $"completed={runCompleted}; fullRuns={reopenRunCount}; record={recordPath}; schema={record?.SchemaVersion}; status={record?.Status}; sourceQuality={record?.SourceQualityEvidence?.State}; timing={reopenedShell.InspectionSteps.FirstOrDefault()?.Timing}");
            Check("ordered Run projects actual step metrics into teaching results",
                reopenedShell.Workbench.TeachingResults.Count == reopenedExecution!.Steps.Count
                && reopenedShell.Workbench.TeachingResults.All(row => row.IsCurrent && row.Metrics.Count > 0)
                && reopenedShell.Workbench.TeachingResults.Single().Value.Contains("5", StringComparison.Ordinal),
                string.Join(";", reopenedShell.Workbench.TeachingResults.Select(row => $"{row.Step.Id}:{row.Value}:{row.Status}")));
            reopenedShell.Workbench.SelectedTeachingResult = reopenedShell.Workbench.TeachingResults.Single();
            Check("selecting an ordered teaching result preserves execution identity without running",
                reopenRunCount == 1 && ReferenceEquals(reopenedExecution, reopenedShell.Workbench.CurrentOrderedRunResult)
                && reopenedShell.Workbench.SelectedPipelineStep == reopenedShell.Workbench.SelectedTeachingResult.Step,
                $"runs={reopenRunCount};step={reopenedShell.Workbench.SelectedPipelineStep?.Id}");
            Check("teaching limits show acceptance bounds without violation counters",
                !reopenedShell.Workbench.TeachingResults.Single().Limits.Contains("Count", StringComparison.Ordinal)
                && reopenedShell.Workbench.TeachingResults.Single().Limits.Contains("4.5", StringComparison.Ordinal)
                && reopenedShell.Workbench.TeachingResults.Single().Limits.Contains("5.5", StringComparison.Ordinal),
                reopenedShell.Workbench.TeachingResults.Single().Limits);
            Check("teaching metric projection does not dirty the saved recipe", !reopenedShell.Workbench.IsDirty, $"dirty={reopenedShell.Workbench.IsDirty}");
            reopenedShell.Workbench.MarkSelectedStepParameterDraftDirty();
            Check("pending draft distinguishes ordered teaching rows as previous without running",
                reopenedShell.Workbench.TeachingResults.All(row => !row.IsCurrent)
                && reopenRunCount == 1 && ReferenceEquals(reopenedExecution, reopenedShell.Workbench.CurrentOrderedRunResult),
                string.Join(";", reopenedShell.Workbench.TeachingResults.Select(row => row.Status)));
            reopenedShell.Workbench.DiscardSelectedStepParameterDraft();
            Check("discarding draft restores current ordered rows without running",
                reopenedShell.Workbench.TeachingResults.All(row => row.IsCurrent) && reopenRunCount == 1,
                $"runs={reopenRunCount}");
            var runResultBeforeRecipeInfo = reopenedShell.Workbench.CurrentOrderedRunResult;
            var recipeBytesBeforeInfoEdit = File.ReadAllBytes(passRecipePath);
            var recordBytesBeforeInfoEdit = File.ReadAllBytes(recordPath!);
            var runLogCountBeforeInfoEdit = reopenedShell.Workbench.RunLog.Count;
            reopenedShell.Workbench.OpenRecipeInfoEditorCommand.Execute(null);
            Check(
                "opening recipe information is a read-only draft and preserves Run evidence",
                reopenedShell.Workbench.IsRecipeInfoEditorVisible
                && !reopenedShell.Workbench.IsDirty
                && ReferenceEquals(runResultBeforeRecipeInfo, reopenedShell.Workbench.CurrentOrderedRunResult)
                && reopenedShell.Workbench.CurrentOrderedRunRecordPath == recordPath
                && reopenedShell.Workbench.RunLog.Count == runLogCountBeforeInfoEdit
                && recipeBytesBeforeInfoEdit.SequenceEqual(File.ReadAllBytes(passRecipePath)),
                $"visible={reopenedShell.Workbench.IsRecipeInfoEditorVisible};dirty={reopenedShell.Workbench.IsDirty};runLog={reopenedShell.Workbench.RunLog.Count};result={reopenedShell.Workbench.HasOrderedRunResult}");
            reopenedShell.Workbench.RecipeInfoCategoryDraft = "Measurement";
            reopenedShell.Workbench.RecipeInfoInspectionTargetDraft = "Power module A-01 pad";
            reopenedShell.Workbench.RecipeInfoDescriptionDraft = "Measure the height difference between the reference and pad areas.";
            reopenedShell.Workbench.CancelRecipeInfoEditorCommand.Execute(null);
            Check(
                "cancel discards recipe information edits without changing recipe, result, or Run Record",
                !reopenedShell.Workbench.IsRecipeInfoEditorVisible
                && !reopenedShell.Workbench.IsDirty
                && ReferenceEquals(runResultBeforeRecipeInfo, reopenedShell.Workbench.CurrentOrderedRunResult)
                && recipeBytesBeforeInfoEdit.SequenceEqual(File.ReadAllBytes(passRecipePath))
                && recordBytesBeforeInfoEdit.SequenceEqual(File.ReadAllBytes(recordPath!))
                && reopenedShell.Workbench.RunLog.Count == runLogCountBeforeInfoEdit,
                $"visible={reopenedShell.Workbench.IsRecipeInfoEditorVisible};dirty={reopenedShell.Workbench.IsDirty};fullRuns={reopenRunCount}");
            reopenedShell.Workbench.OpenRecipeInfoEditorCommand.Execute(null);
            reopenedShell.Workbench.RecipeInfoCategoryDraft = "Measurement";
            reopenedShell.Workbench.RecipeInfoInspectionTargetDraft = "Power module A-01 pad";
            reopenedShell.Workbench.RecipeInfoDescriptionDraft = new string('x', ToolRecipeDocument.MaximumMetadataFieldLength + 1);
            reopenedShell.Workbench.ApplyRecipeInfoCommand.Execute(null);
            var overlengthMetadataRejected = reopenedShell.Workbench.IsRecipeInfoEditorVisible
                && reopenedShell.Workbench.RecipeInfoEditError == reopenedShell.Workbench.Localization.RecipeInfoFieldTooLong
                && !reopenedShell.Workbench.IsDirty
                && ReferenceEquals(runResultBeforeRecipeInfo, reopenedShell.Workbench.CurrentOrderedRunResult)
                && recipeBytesBeforeInfoEdit.SequenceEqual(File.ReadAllBytes(passRecipePath));
            Check(
                "recipe information rejects overlength values without closing or mutating the recipe",
                overlengthMetadataRejected,
                $"visible={reopenedShell.Workbench.IsRecipeInfoEditorVisible};dirty={reopenedShell.Workbench.IsDirty};error={reopenedShell.Workbench.RecipeInfoEditError}");
            reopenedShell.Workbench.RecipeInfoDescriptionDraft = "Measure the height difference between the reference and pad areas.";
            reopenedShell.Workbench.ApplyRecipeInfoCommand.Execute(null);
            var metadataApplyPreservedRun = !reopenedShell.Workbench.IsRecipeInfoEditorVisible
                && reopenedShell.Workbench.IsDirty
                && ReferenceEquals(runResultBeforeRecipeInfo, reopenedShell.Workbench.CurrentOrderedRunResult)
                && reopenedShell.Workbench.CurrentOrderedRunRecordPath == recordPath
                && recordBytesBeforeInfoEdit.SequenceEqual(File.ReadAllBytes(recordPath!))
                && reopenedShell.Workbench.RunLog.Count == runLogCountBeforeInfoEdit
                && reopenRunCount == 1;
            var metadataSaveMessage = string.Empty;
            var metadataSaved = reopenedShell.Workbench.TrySaveTeachingRecipe(
                passRecipePath,
                out metadataSaveMessage);
            var recipeAfterMetadataSave = ToolRecipeDocumentStore.Load(passRecipePath);
            Check(
                "applying and saving recipe information upgrades 1.8 to 1.9 without rerunning or clearing Results",
                metadataSaved
                && recipeAfterMetadataSave.SchemaVersion == ToolRecipeDocument.RecipeMetadataSchemaVersion
                && recipeAfterMetadataSave.Category == "Measurement"
                && recipeAfterMetadataSave.InspectionTarget == "Power module A-01 pad"
                && recipeAfterMetadataSave.Description == "Measure the height difference between the reference and pad areas."
                && reopenedShell.Workbench.IsDirty == false
                && metadataApplyPreservedRun
                && ReferenceEquals(runResultBeforeRecipeInfo, reopenedShell.Workbench.CurrentOrderedRunResult)
                && reopenedShell.Workbench.CurrentOrderedRunRecordPath == recordPath
                && recordBytesBeforeInfoEdit.SequenceEqual(File.ReadAllBytes(recordPath!))
                && reopenRunCount == 1,
                $"saved={metadataSaved};schema={recipeAfterMetadataSave.SchemaVersion};category={recipeAfterMetadataSave.Category};target={recipeAfterMetadataSave.InspectionTarget};description={recipeAfterMetadataSave.Description};applyPreservedRun={metadataApplyPreservedRun};sameResult={ReferenceEquals(runResultBeforeRecipeInfo, reopenedShell.Workbench.CurrentOrderedRunResult)};sameRecordPath={reopenedShell.Workbench.CurrentOrderedRunRecordPath == recordPath};sameRecordBytes={recordBytesBeforeInfoEdit.SequenceEqual(File.ReadAllBytes(recordPath!))};runCount={reopenRunCount};save={metadataSaveMessage}");
            var valueValiditySummary = reopenedShell.ResultsValueValiditySummary;
            var validSampleMetric = record?.Steps?.SingleOrDefault()?.Metrics.SingleOrDefault(metric =>
                metric.Name.Equals("ValidSampleCount", StringComparison.OrdinalIgnoreCase));
            var referenceFitRmsMetric = record?.Steps?.SingleOrDefault()?.Metrics.SingleOrDefault(metric =>
                metric.Name.Contains("fit", StringComparison.OrdinalIgnoreCase)
                && metric.Name.Contains("rms", StringComparison.OrdinalIgnoreCase));
            Check(
                "Results explains stored definition, raw-height unit basis, ROI, valid count, and recorded fit residual without recomputation",
                (valueValiditySummary.Contains("Definition", StringComparison.Ordinal)
                    || valueValiditySummary.Contains("정의", StringComparison.Ordinal))
                && (valueValiditySummary.Contains("Unit basis", StringComparison.Ordinal)
                    || valueValiditySummary.Contains("단위 근거", StringComparison.Ordinal))
                && valueValiditySummary.Contains("ROI", StringComparison.OrdinalIgnoreCase)
                && valueValiditySummary.Contains("ValidSampleCount", StringComparison.Ordinal)
                && validSampleMetric is not null
                && valueValiditySummary.Contains(
                    validSampleMetric.Value.ToString("G6", System.Globalization.CultureInfo.CurrentCulture),
                    StringComparison.Ordinal)
                && (valueValiditySummary.Contains("physical calibration", StringComparison.OrdinalIgnoreCase)
                    || valueValiditySummary.Contains("물리 보정", StringComparison.Ordinal)
                    || valueValiditySummary.Contains("물리 교정", StringComparison.Ordinal))
                && referenceFitRmsMetric is not null
                && valueValiditySummary.Contains(
                    FormatRecordedMetric(referenceFitRmsMetric),
                    StringComparison.Ordinal),
                valueValiditySummary.Replace(Environment.NewLine, " | "));
            var operatorDecisionSummary = reopenedShell.ResultsOperatorDecisionSummary;
            var meanMetric = record?.Steps?.SingleOrDefault()?.Metrics.SingleOrDefault(metric =>
                metric.Name.Equals("Mean", StringComparison.OrdinalIgnoreCase));
            var lowerLimitMetric = record?.Steps?.SingleOrDefault()?.Metrics.SingleOrDefault(metric =>
                metric.Name.Equals("LowerLimit", StringComparison.OrdinalIgnoreCase));
            var upperLimitMetric = record?.Steps?.SingleOrDefault()?.Metrics.SingleOrDefault(metric =>
                metric.Name.Equals("UpperLimit", StringComparison.OrdinalIgnoreCase));
            Check(
                "Results foregrounds the recorded primary measurement, authored limits, and reference-fit RMS",
                meanMetric is not null
                && lowerLimitMetric is not null
                && upperLimitMetric is not null
                && referenceFitRmsMetric is not null
                && operatorDecisionSummary.Contains(FormatRecordedMetric(meanMetric), StringComparison.Ordinal)
                && operatorDecisionSummary.Contains(FormatRecordedMetric(lowerLimitMetric), StringComparison.Ordinal)
                && operatorDecisionSummary.Contains(FormatRecordedMetric(upperLimitMetric), StringComparison.Ordinal)
                && operatorDecisionSummary.Contains(FormatRecordedMetric(referenceFitRmsMetric), StringComparison.Ordinal)
                && operatorDecisionSummary.Contains("Pass", StringComparison.Ordinal),
                operatorDecisionSummary.Replace(Environment.NewLine, " | "));
            var valueValidityScenarios = VerifyResultsValueValidityScenarios(
                root,
                runRoot,
                record,
                out var valueValidityScenarioDetail);
            Check(
                "Results keeps Warpage residuals, unknown units, and failure reasons distinguishable",
                valueValidityScenarios,
                valueValidityScenarioDetail);
            var otherMeasurementScenarios = VerifyOtherRecordedMeasurements(root, runRoot, record, out var otherMeasurementDetail);
            Check(
                "Results preserves recorded flatness, gap/flush, volume and section metrics after preparation, including invalid values",
                otherMeasurementScenarios,
                otherMeasurementDetail);
            var measurementMeaning = VerifyRecordedMeasurementMeaning(root, runRoot, record, out var measurementMeaningDetail);
            Check("Results distinguishes measurement direction, version and recorded unit evidence", measurementMeaning, measurementMeaningDetail);
            var multiMeasurement = VerifyMultipleMeasurementExplanation(root, runRoot, record, out var multiMeasurementDetail);
            Check("Results prioritizes failed recorded measurements with step and ROI identity", multiMeasurement, multiMeasurementDetail);
            var runComparisonVerified = VerifyRunRecordComparison(
                root,
                runRoot,
                record,
                out var runComparisonDetail);
            Check(
                "Results compares two saved Runs without replacing current evidence or treating time as a condition",
                runComparisonVerified,
                runComparisonDetail);
            var stagedRunDirectories = Directory.Exists(runRoot)
                ? Directory.GetDirectories(runRoot, "*.staging.*")
                : Array.Empty<string>();
            Check(
                "ordered Run publishes a complete directory and removes staging directories",
                recordPath is { } publishedRecordPath
                && record?.Artifacts.RunnerTextReport is { } publishedReportPath
                && File.Exists(publishedRecordPath)
                && File.Exists(publishedReportPath)
                && Directory.Exists(Path.GetDirectoryName(publishedRecordPath))
                && stagedRunDirectories.Length == 0,
                $"record={recordPath};report={record?.Artifacts.RunnerTextReport};staging={stagedRunDirectories.Length}");
            var atomicReplacementPassed = false;
            var atomicReplacementDetail = "record unavailable";
            if (record is not null)
            {
                var atomicRecordPath = Path.Combine(root, "atomic-replacement-run-record.json");
                InspectionRunRecordJson.Write(atomicRecordPath, record);
                var initialAtomicRecordBytes = File.ReadAllBytes(atomicRecordPath);
                var replacementRecord = record with
                {
                    RunId = "atomic-replacement",
                    Message = "atomic replacement"
                };
                InspectionRunRecordJson.Write(atomicRecordPath, replacementRecord);
                var replacementAtomicRecordBytes = File.ReadAllBytes(atomicRecordPath);
                var reloadedReplacementRecord = InspectionRunRecordJson.Read(atomicRecordPath);
                var stagedAtomicRecordFiles = Directory.GetFiles(
                    root,
                    $"{Path.GetFileName(atomicRecordPath)}.tmp.*");
                atomicReplacementPassed =
                    !initialAtomicRecordBytes.SequenceEqual(replacementAtomicRecordBytes)
                    && reloadedReplacementRecord.RunId == "atomic-replacement"
                    && reloadedReplacementRecord.Message == "atomic replacement"
                    && stagedAtomicRecordFiles.Length == 0;
                atomicReplacementDetail =
                    $"runId={reloadedReplacementRecord.RunId};staged={stagedAtomicRecordFiles.Length}";
            }
            Check(
                "Run Record JSON replacement preserves the contract and cleans staged files",
                atomicReplacementPassed,
                atomicReplacementDetail);
            Check(
                "Shell reuses the exact loaded Source Quality report and Results exposes its decision evidence",
                uiSourceQuality is not null
                && reopenedExecution is not null
                && ReferenceEquals(uiSourceQuality, reopenedExecution.SourceQuality)
                && record?.SourceQualityEvidence?.SourceQualitySha256
                    == SourceQualityReportContentIdentity.CalculateSha256(
                        uiSourceQuality)
                && uiSourceQuality.GridDiagnostics is { } gridDiagnostics
                && JsonSerializer.Serialize(
                        record?.SourceQualityEvidence?.Report?.GridDiagnostics,
                        JsonOptions)
                    == gridDiagnosticsJson
                && reopenedShell.SourceQualityState == "Pass"
                && reopenedShell.SourceQualitySummary.Contains(
                    "4 × 4",
                    StringComparison.Ordinal)
                && reopenedShell.SourceQualityDetail.Contains(
                    uiSourceQuality.Coverage.InvalidCellMask.Sha256,
                    StringComparison.Ordinal)
                && reopenedShell.SourceQualityDetail.Contains(
                    "Height=Available",
                    StringComparison.Ordinal)
                && gridDiagnostics.Checks.All(check =>
                    reopenedShell.SourceQualityDetail.Contains(
                        check.Code.ToString(),
                        StringComparison.Ordinal)
                    && reopenedShell.SourceQualityDetail.Contains(
                        check.Message,
                        StringComparison.Ordinal))
                && orderedReport.Contains(
                    $"gridDiagnostics={gridDiagnosticsJson}",
                    StringComparison.Ordinal),
                $"sameInstance={ReferenceEquals(uiSourceQuality, reopenedExecution?.SourceQuality)};state={reopenedShell.SourceQualityState};summary={reopenedShell.SourceQualitySummary};sha={record?.SourceQualityEvidence?.SourceQualitySha256}");

            var diagnosticsErrorProjected = false;
            var diagnosticsErrorEvidence = "current Source Quality report unavailable";
            if (record is not null
                && uiSourceQuality?.GridDiagnostics is { } passDiagnostics)
            {
                const string errorMessage =
                    "Coordinate component Z is non-finite at the first affected sample.";
                var errorDiagnostics = passDiagnostics with
                {
                    State = SourceQualityGridDiagnosticState.Error,
                    Checks = passDiagnostics.Checks.Select(check =>
                        check.Code == SourceQualityGridDiagnosticCode.CoordinateFiniteness
                            ? check with
                            {
                                State = SourceQualityGridDiagnosticState.Error,
                                AffectedCount = 1,
                                FirstSampleOrdinal = 2,
                                FirstRow = 0,
                                FirstColumn = 2,
                                FirstComponent = "Z",
                                Message = errorMessage
                            }
                            : check).ToArray()
                };
                var diagnosticErrorReport = uiSourceQuality with
                {
                    GridDiagnostics = errorDiagnostics
                };
                var diagnosticErrorRecord = record with
                {
                    SourceQualityEvidence =
                        InspectionRunSourceQualityEvidence.Available(
                            record.Source,
                            diagnosticErrorReport)
                };
                var diagnosticErrorPath = Path.Combine(
                    root,
                    "diagnostic-error-run-record.json");
                InspectionRunRecordJson.Write(
                    diagnosticErrorPath,
                    diagnosticErrorRecord);
                var diagnosticShell = CreateShell(
                    root,
                    "diagnostic-error",
                    runRoot);
                var diagnosticLoaded = diagnosticShell.LoadRunRecord(
                    diagnosticErrorPath,
                    out var diagnosticLoadMessage);
                diagnosticsErrorProjected = diagnosticLoaded
                    && diagnosticShell.SourceQualityState == "Error"
                    && diagnosticShell.SourceQualitySummary.Contains(
                        "Error",
                        StringComparison.Ordinal)
                    && diagnosticShell.SourceQualityDetail.Contains(
                        SourceQualityGridDiagnosticCode.CoordinateFiniteness.ToString(),
                        StringComparison.Ordinal)
                    && diagnosticShell.SourceQualityDetail.Contains(
                        "ordinal=2, row=0, column=2, component=Z",
                        StringComparison.Ordinal)
                    && diagnosticShell.SourceQualityDetail.Contains(
                        errorMessage,
                        StringComparison.Ordinal);
                diagnosticsErrorEvidence = diagnosticLoaded
                    ? $"state={diagnosticShell.SourceQualityState};summary={diagnosticShell.SourceQualitySummary}"
                    : diagnosticLoadMessage;
            }

            Check(
                "Results prioritizes persisted grid-diagnostic Error and exact evidence",
                diagnosticsErrorProjected,
                diagnosticsErrorEvidence);

            var directExecution = ToolRecipeOrderedGraphExecution.Execute(
                passDocument,
                sourcePath);
            var runnerProjection = ToolRecipeOrderedGraphRunRecordProjection.Create(
                passDocument,
                directExecution);
            var recordStep = record?.Steps?.SingleOrDefault();
            var projectedStep = runnerProjection.SingleOrDefault();
            Check(
                "Studio and Runner share status, metric, identity, and timing contracts",
                record is not null
                && record.Status == directExecution.Status
                && recordStep is not null
                && projectedStep is not null
                && recordStep.Id == projectedStep.Id
                && recordStep.Status == projectedStep.Status
                && recordStep.OutputEntityId == projectedStep.OutputEntityId
                && recordStep.OutputContentSha256 == projectedStep.OutputContentSha256
                && recordStep.Metrics.SequenceEqual(projectedStep.Metrics)
                && recordStep.Timing is { State: InspectionRunTimingState.Available } timing
                && timing.TryValidate(out _)
                && timing.Clock == InspectionRunTiming.StopwatchClock
                && timing.TotalElapsedMilliseconds == recordStep.ElapsedMilliseconds
                && timing.Stages is
                [
                    {
                        StageId: InspectionRunTiming.ToolExecutionStage
                    }
                ]
                && projectedStep.Timing is { State: InspectionRunTimingState.Available } projectedTiming
                && projectedTiming.TryValidate(out _)
                && projectedTiming.Clock == InspectionRunTiming.StopwatchClock
                && projectedTiming.TotalElapsedMilliseconds == projectedStep.ElapsedMilliseconds
                && projectedTiming.Stages is
                [
                    {
                        StageId: InspectionRunTiming.ToolExecutionStage
                }
                ],
                $"status={record?.Status}/{directExecution.Status}; step={recordStep?.Id}/{projectedStep?.Id}; output={recordStep?.OutputEntityId}; hash={recordStep?.OutputContentSha256}; studioTiming={recordStep?.Timing?.TotalElapsedMilliseconds:G17};runnerTiming={projectedStep?.Timing?.TotalElapsedMilliseconds:G17}");
            var publishedCropParity = VerifyPublishedCropParity(
                root,
                runRoot,
                out var publishedCropParityDetail);
            Check(
                "published ROI/Crop input keeps UI Ordered Run and headless Run Record semantically equivalent",
                publishedCropParity,
                publishedCropParityDetail);
            var noDataParity = VerifyNoDataParity(
                root,
                runRoot,
                out var noDataParityDetail);
            Check(
                "NoData remains an NG/Fail (not execution Error) in UI Ordered Run and headless replay",
                noDataParity,
                noDataParityDetail);
            var recordSaveFailure = VerifyRecordSaveFailure(
                root,
                passRecipePath,
                runRoot,
                out var recordSaveFailureDetail);
            Check(
                "Run completion remains distinct from Run Record save failure",
                recordSaveFailure,
                recordSaveFailureDetail);
            VerifySavedRecipeContext(root, runRoot, passDocument, Check);
            VerifyRealQueueRecordRecovery(root, passRecipePath, Check);
            Check(
                "Thickness Pass metric remains exact",
                recordStep?.Metrics.Single(metric => metric.Name == "Mean").Value is { } mean
                && Math.Abs(mean - 5d) <= 1e-12,
                $"mean={recordStep?.Metrics.SingleOrDefault(metric => metric.Name == "Mean")?.Value:G17}");
            var mismatchedSourceQuality = uiSourceQuality! with
            {
                Source = uiSourceQuality.Source with
                {
                    EntityId = "source.c3d.other"
                }
            };
            var mismatchedExecution = ToolRecipeOrderedGraphExecution.Execute(
                passDocument,
                sourcePath,
                mismatchedSourceQuality);
            Check(
                "mismatched existing Source Quality fails closed before inspection",
                mismatchedExecution.Status == ResultStatus.Error
                && mismatchedExecution.Steps.Count == 0
                && mismatchedExecution.SourceQuality is null
                && mismatchedExecution.Message.Contains(
                    "does not match",
                    StringComparison.OrdinalIgnoreCase),
                $"status={mismatchedExecution.Status};steps={mismatchedExecution.Steps.Count};quality={mismatchedExecution.SourceQuality is not null};message={mismatchedExecution.Message}");

            var selectedStep = reopenedShell.Workbench.SelectedPipelineStep!;
            selectedStep.Parameters.Single(parameter => parameter.Name == "MaximumThickness").Value = "4.9";
            Check(
                "editing invalidates current evidence and requires save without auto-run",
                reopenedShell.Workbench.IsDirty
                && !reopenedShell.Workbench.HasOrderedRunResult
                && reopenedShell.Workbench.CurrentOrderedRunRecordPath is null
                && reopenedShell.InspectionSteps.Count == 0
                && (reopenedShell.ResultsOperatorDecisionSummary.Contains("Not recorded", StringComparison.Ordinal)
                    || reopenedShell.ResultsOperatorDecisionSummary.Contains("기록되지 않음", StringComparison.Ordinal))
                && reopenRunCount == 1
                && !reopenedShell.Workbench.RunTeachingRecipeCommand.CanExecute(null)
                && reopenedShell.Workbench.OrderedRunCapabilitySummary.Contains("저장", StringComparison.Ordinal),
                $"dirty={reopenedShell.Workbench.IsDirty}; fullRuns={reopenRunCount}; summary={reopenedShell.Workbench.OrderedRunCapabilitySummary}");

            var failRecipePath = Path.Combine(root, "thickness-fail.ov3d-recipe.json");
            var failSaved = reopenedShell.Workbench.TrySaveTeachingRecipe(
                failRecipePath,
                out var failSaveMessage);
            Check(
                "saving the edited Fail recipe still does not auto-run",
                failSaved
                && reopenRunCount == 1
                && !reopenedShell.Workbench.HasOrderedRunResult,
                failSaved ? $"fullRuns={reopenRunCount}" : failSaveMessage);
            var failRun = reopenedShell.Workbench.RunTeachingRecipeAsync()
                .GetAwaiter().GetResult();
            var failRecord = ReadRecord(
                reopenedShell.Workbench.CurrentOrderedRunRecordPath);
            Check(
                "explicit out-of-tolerance Run records Fail as a completed execution",
                failRun
                && reopenRunCount == 2
                && failRecord?.Status == ResultStatus.Fail
                && failRecord.Steps?.Single().Status == ResultStatus.Fail,
                $"completed={failRun}; status={failRecord?.Status}; fullRuns={reopenRunCount}");

            var samePathSourceChange = VerifySamePathInputBindingChange(
                root,
                passRecipePath,
                runRoot,
                out var samePathSourceChangeDetail);
            Check(
                "same-path input content change marks the recipe dirty and invalidates prior evidence",
                samePathSourceChange,
                samePathSourceChangeDetail);

            var corruptSourcePath = Path.Combine(root, "corrupt.C3D");
            File.Copy(sourcePath, corruptSourcePath);
            var errorDocument = passDocument with
            {
                Source = passDocument.Source with { Path = corruptSourcePath }
            };
            var errorRecipePath = Path.Combine(root, "thickness-error.ov3d-recipe.json");
            ToolRecipeDocumentStore.Save(errorRecipePath, errorDocument);
            File.WriteAllText(corruptSourcePath, "not a C3D file");
            var errorExecution = ToolRecipeOrderedGraphExecution.Execute(
                errorDocument,
                corruptSourcePath);
            var errorArtifact = ShellOrderedRunRecordWriter.Write(
                errorRecipePath,
                errorDocument,
                corruptSourcePath,
                errorExecution,
                runRoot);
            var errorRecord = ReadRecord(errorArtifact.JsonPath);
            Check(
                "Error execution remains Error in the same Run Record projection",
                errorExecution.Status == ResultStatus.Error
                && errorRecord?.Status == ResultStatus.Error
                && errorRecord.SourceQualityEvidence is
                {
                    State: InspectionRunSourceQualityEvidenceState.Unavailable,
                    Report: null
                } unavailableQuality
                && unavailableQuality.TryValidate(errorRecord.Source, out _)
                && errorRecord.Metrics.Count == 0,
                $"execution={errorExecution.Status}; record={errorRecord?.Status}; sourceQuality={errorRecord?.SourceQualityEvidence?.State}; message={errorExecution.Message}");

            var unsupportedDocument = passDocument with
            {
                Steps =
                [
                    passDocument.Steps[0] with
                    {
                        ToolId = "unsupported-tool",
                        ToolName = "Unsupported Tool"
                    }
                ]
            };
            var unsupported = ToolRecipeOrderedGraphExecution.CanExecute(
                unsupportedDocument,
                out var unsupportedMessage);
            Check(
                "unsupported ordered step fails closed with its exact reason",
                !unsupported
                && unsupportedMessage.Contains("unsupported", StringComparison.OrdinalIgnoreCase),
                unsupportedMessage);

            var hadRunEvidenceBeforeDispose =
                reopenedShell.Workbench.HasOrderedRunResult
                && reopenedShell.Workbench.CurrentOrderedRunRecordPath is not null;
            reopenedShell.Dispose();
            reopenedShell.Dispose();
            Check(
                "Workbench disposal releases Ordered Run state idempotently",
                hadRunEvidenceBeforeDispose
                && !reopenedShell.Workbench.HasOrderedRunResult
                && reopenedShell.Workbench.CurrentOrderedRunRecordPath is null
                && !reopenedShell.Workbench.IsOrderedRunRunning
                && !reopenedShell.Workbench.RunTeachingRecipeCommand.CanExecute(null),
                $"before={hadRunEvidenceBeforeDispose};result={reopenedShell.Workbench.HasOrderedRunResult};record={reopenedShell.Workbench.CurrentOrderedRunRecordPath ?? "(none)"};running={reopenedShell.Workbench.IsOrderedRunRunning}");
            shell.Dispose();
        }
        catch (Exception exception)
        {
            Check("unhandled exception", false, exception.ToString());
        }

        summary = $"ToolRecipeOrderedRunVerification|{(passed == total ? "Pass" : "Fail")}|checks={total}|passed={passed}|failed={total - passed}";
        lines.Add(summary);
        Directory.CreateDirectory(reportDirectory);
        File.WriteAllLines(fullReportPath, lines);
        return passed == total;
    }


    public static bool VerifyEssentialWorkflow(string reportPath, out string summary)
    {
        var root = Path.GetDirectoryName(Path.GetFullPath(reportPath))!;
        Directory.CreateDirectory(root);
        var lines = new List<string> { "Boundary|Actual Studio execution and record stores on isolated synthetic C3D; no CU, SDK numerical qualification, grouped judgement or Cp/Cpk." };
        var passed = 0;
        var total = 0;
        void Check(string name, bool condition, string detail)
        {
            total++;
            if (condition) passed++;
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
            File.AppendAllLines(reportPath + ".progress.txt", [lines[^1]]);
        }
        try
        {
            var sourcePath = Path.Combine(root, "source.C3D");
            C3DHeightFieldSnapshot.CreateForVerification("source.c3d.height-map", 4, 4,
                [10, 10, 10, 10, 10, 10, 10, 10, 15, 15, 15, 15, 15, 15, 15, 15]).SaveC3D(sourcePath);
            var sourceBytes = File.ReadAllBytes(sourcePath);
            var recipePath = Path.Combine(root, "workflow.ov3d-recipe.json");
            var document = CreateThicknessDocument(sourcePath, 4.5, 5.5) with { SchemaVersion = ToolRecipeDocument.OutputPolicySchemaVersion };
            ToolRecipeDocumentStore.Save(recipePath, document);
            using var shell = CreateShell(root, "workflow", Path.Combine(root, "runs"));
            var workbench = shell.Workbench;
            var runs = 0;
            workbench.OrderedRunCompleted += (_, _) => runs++;
            Check("recipe Open and source restoration wait for explicit Run", workbench.TryOpenTeachingRecipe(recipePath, out var message)
                && WaitForSourceQuality(workbench.SourceQuality) is not null && runs == 0 && !workbench.HasOrderedRunResult, message);
            var step = workbench.PipelineSteps.Single();
            workbench.SelectPipelineStep(step.Id);
            var originalParameters = step.Parameters.Select(parameter => (parameter.Name, parameter.Value)).ToArray();
            var draft = (ThicknessStepProperties)workbench.SelectedStepPropertyDraft!;
            draft.MaximumThickness = 6.5;
            workbench.MarkSelectedStepParameterDraftDirtyCommand.Execute(null);
            Check("Apply records the actual condition without Run", workbench.TryApplySelectedStepParameterDraft(out message)
                && step.Parameters.Single(parameter => parameter.Name == "MaximumThickness").Value == "6.5" && runs == 0, message);
            draft = (ThicknessStepProperties)workbench.SelectedStepPropertyDraft!;
            draft.MaximumThickness = 8.5;
            workbench.MarkSelectedStepParameterDraftDirtyCommand.Execute(null);
            workbench.DiscardSelectedStepParameterDraftCommand.Execute(null);
            Check("explicit Cancel restores the last applied condition", !workbench.HasPendingStepParameterChanges
                && ((ThicknessStepProperties)workbench.SelectedStepPropertyDraft!).MaximumThickness == 6.5 && runs == 0, workbench.StepParameterEditStatus);
            workbench.UndoSelectedStepParametersCommand.Execute(null);
            step = workbench.PipelineSteps.Single();
            Check("Undo restores the original condition values", step.Parameters.Select(parameter => (parameter.Name, parameter.Value)).SequenceEqual(originalParameters)
                && !workbench.HasSelectedStepParameterHistory && runs == 0, workbench.StepParameterEditStatus);
            var reference = workbench.PlaneFlatnessReferenceSelection!;
            workbench.CapturePlaneFlatnessReferenceRoiCommand.Execute(null);
            var replacement = reference with { GridRectangle = reference.GridRectangle! with { Row = 1 } };
            workbench.UpdateTeachingGridRectangleDraft(replacement.GridRectangle);
            workbench.UpdateTeachingSelectionCaptureState(true, 2, 2, true, "Owned ROI replacement");
            Check("ROI Apply changes the same region identity without Run", workbench.TryApplyCapturedTeachingSelection(replacement, out message)
                && workbench.Selections.Single(selection => selection.Id == reference.Id).GridRectangle == replacement.GridRectangle && runs == 0, message);
            workbench.UndoSelectedStepParametersCommand.Execute(null);
            Check("ROI Undo restores the complete original region", workbench.Selections.Single(selection => selection.Id == reference.Id) == reference && runs == 0, reference.Id);
            workbench.SetValidationSetSources([sourcePath]);
            workbench.SelectedValidationSetSample = workbench.ValidationSetSamples.Single();
            workbench.SetValidationSampleRoleCommand.Execute("Good");
            workbench.SetValidationSampleRoleCommand.Execute("HeldOut");
            Check("Save persists conditions and independent sample use", workbench.TrySaveTeachingRecipe(recipePath, out message)
                && !workbench.IsDirty && !workbench.IsValidationSetDefinitionDirty && runs == 0, message);
            Check("explicit Run creates an actual stored record", workbench.RunTeachingRecipeAsync().GetAwaiter().GetResult()
                && runs == 1 && workbench.CurrentOrderedRunRecordPath is { } path && File.Exists(path), workbench.OrderedRunEvidenceSummary);
            var recordPath = workbench.CurrentOrderedRunRecordPath!;
            var record = ReadRecord(recordPath)!;
            var recordBytes = File.ReadAllBytes(recordPath);
            Check("record identifies executed source and exact saved recipe bytes", record.Source.Sha256.Equals(ComputeHashBytes(sourceBytes), StringComparison.OrdinalIgnoreCase)
                && record.Recipe.Sha256.Equals(ComputeHashBytes(File.ReadAllBytes(recipePath)), StringComparison.OrdinalIgnoreCase)
                && record.Steps?.Count == 1 && record.Steps[0].Metrics.Count > 0, record.RunId);
            draft = (ThicknessStepProperties)workbench.SelectedStepPropertyDraft!;
            draft.MaximumThickness = 6.5;
            workbench.MarkSelectedStepParameterDraftDirtyCommand.Execute(null);
            workbench.TryApplySelectedStepParameterDraft(out _);
            Check("later condition edit invalidates the current result and retains stored bytes", !workbench.HasCurrentMeasurementPreview
                && workbench.TeachingResults.All(row => !row.IsCurrent) && recordBytes.SequenceEqual(File.ReadAllBytes(recordPath)) && runs == 1, record.Recipe.Sha256);
            Check("new Save does not rewrite historical record or rerun", workbench.TrySaveTeachingRecipe(recipePath, out message)
                && recordBytes.SequenceEqual(File.ReadAllBytes(recordPath)) && runs == 1
                && !record.Recipe.Sha256.Equals(ComputeHashBytes(File.ReadAllBytes(recipePath)), StringComparison.OrdinalIgnoreCase), message);
            Check("reopen restores saved conditions and sample expectation/use without Run", workbench.TryOpenTeachingRecipe(recipePath, out message)
                && ((ThicknessStepProperties)workbench.SelectedStepPropertyDraft!).MaximumThickness == 6.5
                && workbench.ValidationSetSamples.Single().IsGood && workbench.ValidationSetSamples.Single().IsHeldOut
                && !workbench.HasOrderedRunResult && !workbench.IsDirty && !workbench.IsValidationSetDefinitionDirty && runs == 1, message);

            var malformed = Path.Combine(root, "truncated.C3D");
            File.WriteAllText(malformed, "C3D");
            var valid = Path.Combine(root, "next-valid.C3D");
            File.Copy(sourcePath, valid);
            var queue = workbench.InspectionQueue;
            queue.AddSources([malformed, valid]);
            queue.RunAllCommand.Execute(null);
            if (!SpinWait.SpinUntil(() => !queue.IsRunning, TimeSpan.FromSeconds(20))) throw new TimeoutException("Real malformed-input queue did not complete.");
            Check("real truncated input is Error and following input gets its own stored result", queue.Samples[0].State == InspectionQueueItemState.Error
                && queue.Samples[0].Record is null && queue.Samples[1].Record is not null && queue.Samples[1].CanOpenCurrentRunRecord
                && ReadRecord(queue.Samples[1].RunRecordPath)?.Source.Path == valid, string.Join(',', queue.Samples.Select(item => item.State)));
            var validRun = queue.Samples[1].RunRecordPath;
            File.WriteAllBytes(malformed, sourceBytes);
            queue.RunRemainingCommand.Execute(null);
            if (!SpinWait.SpinUntil(() => !queue.IsRunning, TimeSpan.FromSeconds(20))) throw new TimeoutException("Real repaired-input retry did not complete.");
            Check("repair and explicit retry leave the completed file record unchanged", queue.Samples[0].CanOpenCurrentRunRecord
                && ReadRecord(queue.Samples[0].RunRecordPath)?.Source.Path == malformed && queue.Samples[1].RunRecordPath == validRun
                && sourceBytes.SequenceEqual(File.ReadAllBytes(sourcePath)) && recordBytes.SequenceEqual(File.ReadAllBytes(recordPath)), queue.SummaryText);

        }
        catch (Exception exception) { Check("unexpected exception", false, exception.ToString()); }
        summary = $"EssentialRecipeWorkflow|pass={passed == total}|checks={passed}/{total}";
        File.WriteAllLines(reportPath, lines.Append(summary));
        return passed == total;
    }


    public static bool VerifyRealRecordRecovery(string reportPath, out string summary)
    {
        var root = Path.GetDirectoryName(Path.GetFullPath(reportPath))!;
        Directory.CreateDirectory(root);
        var lines = new List<string> { "Boundary|Isolated actual Studio Queue record-save failure/retry; separate process from ROI lifecycle." };
        var total = 0;
        var passed = 0;
        void Check(string name, bool condition, string detail)
        {
            total++;
            if (condition) passed++;
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
            File.AppendAllLines(reportPath + ".progress.txt", [lines[^1]]);
        }
        try
        {
            var source = Path.Combine(root, "source.C3D");
            C3DHeightFieldSnapshot.CreateForVerification("source.c3d.height-map", 4, 4,
                [10, 10, 10, 10, 10, 10, 10, 10, 15, 15, 15, 15, 15, 15, 15, 15]).SaveC3D(source);
            var recipe = Path.Combine(root, "record-recovery.ov3d-recipe.json");
            ToolRecipeDocumentStore.Save(recipe, CreateThicknessDocument(source, 4.5, 5.5) with { SchemaVersion = ToolRecipeDocument.OutputPolicySchemaVersion });
            VerifyRealQueueRecordRecovery(root, recipe, Check);
        }
        catch (Exception exception) { Check("unexpected exception", false, exception.ToString()); }
        summary = $"RealQueueRecordRecovery|pass={passed == total}|checks={passed}/{total}";
        File.WriteAllLines(reportPath, lines.Append(summary));
        return passed == total;
    }

    public static bool VerifyExecutionLog(string reportPath, out string summary)
    {
        var root = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(reportPath))!, "activity-execution");
        Directory.CreateDirectory(root);
        var lines = new List<string>();
        var passed = 0;
        var total = 0;
        void Check(string name, bool condition, string detail)
        {
            total++;
            if (condition) passed++;
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
        }
        try
        {
            var sourcePath = Path.Combine(root, "input.C3D");
            C3DHeightFieldSnapshot.CreateForVerification("source.c3d.height-map", 4, 4,
                [10, 10, 10, 10, 10, 10, 10, 10, 15, 15, 15, 15, 15, 15, 15, 15]).SaveC3D(sourcePath);
            foreach (var (expected, minimum, maximum) in new[] { (ResultStatus.Pass, 4.5, 5.5), (ResultStatus.Fail, 6.0, 7.0) })
            {
                var path = Path.Combine(root, $"{expected}.ov3d-recipe.json");
                var document = CreateThicknessDocument(sourcePath, minimum, maximum);
                ToolRecipeDocumentStore.Save(path, document);
                using var shell = CreateShell(root, expected.ToString(), Path.Combine(root, "runs"));
                Check($"{expected} open starts with empty execution display", shell.Workbench.TryOpenTeachingRecipe(path, out var opened)
                    && shell.Workbench.ExecutionLog.Count == 0 && shell.Workbench.RunLog.Count > 0, opened);
                _ = WaitForSourceQuality(shell.Workbench.SourceQuality);
                shell.Workbench.RunSelectedStepCommand.Execute(null);
                var selectedFinished = SpinWait.SpinUntil(() => !shell.Workbench.IsSelectedStepPreviewRunning, TimeSpan.FromSeconds(5));
                Check($"Selected {expected} reaches Activity through its real producer", selectedFinished && shell.Workbench.ExecutionLog.Count == 1
                    && shell.Workbench.ExecutionLog[0].Status == expected && shell.Workbench.CurrentMeasurementOutput?.Result.Status == expected,
                    $"finished={selectedFinished}; entries={shell.Workbench.ExecutionLog.Count}; actual={shell.Workbench.CurrentMeasurementOutput?.Result.Status}");
                var expectedSelectedMessage = expected == ResultStatus.Pass ? "Thickness Mean 5 raw-height · OK" : "Thickness Mean 5 raw-height · NG";
                Check("Selected execution matches the HTML name/value/status order and preserves the measured value",
                    shell.Workbench.ExecutionLog[0].Message.StartsWith(expectedSelectedMessage, StringComparison.Ordinal)
                    && (expected != ResultStatus.Fail || shell.Workbench.ExecutionLog[0].Message.EndsWith("기준면과의 높이 차이 중 규격을 벗어난 값이 있습니다.", StringComparison.Ordinal)),
                    shell.Workbench.ExecutionLog[0].Message);
                var ran = shell.Workbench.RunTeachingRecipeAsync().GetAwaiter().GetResult();
                Check($"Ordered {expected} agrees with the actual graph result", ran && shell.Workbench.ExecutionLog.Count == 2
                    && shell.Workbench.ExecutionLog[0].Status == expected && shell.Workbench.CurrentOrderedRunResult?.Status == expected,
                    $"entries={shell.Workbench.ExecutionLog.Count}; result={shell.Workbench.CurrentOrderedRunResult?.Status}");
                var expectedCounts = expected == ResultStatus.Pass ? "OK 1 · NG 0 · 오류 0" : "OK 0 · NG 1 · 오류 0";
                Check("Activity describes the judged result without raw-log categories", shell.Workbench.ExecutionLog[0].Message == $"모든 도구 실행 · {expectedCounts}", shell.Workbench.ExecutionLog[0].Message);
                var count = shell.Workbench.ExecutionLog.Count;
                Check("Save does not execute or clear Activity", shell.Workbench.TrySaveTeachingRecipe(path, out var saved)
                    && shell.Workbench.ExecutionLog.Count == count, saved);
                Check("Rejected open retains the current job's execution display", !shell.Workbench.TryOpenTeachingRecipe(path + ".missing", out _)
                    && shell.Workbench.ExecutionLog.Count == count, "Missing isolated recipe.");
                var rawCount = shell.Workbench.RunLog.Count;
                Check("Successful reopen retains execution display without a new execution", shell.Workbench.TryOpenTeachingRecipe(path, out _)
                    && shell.Workbench.ExecutionLog.Count == count && shell.Workbench.RunLog.Count > rawCount, "Matches HTML recipeDialog; full session activity retained.");
                shell.Workbench.CreateNewTeachingRecipe();
                Check("New recipe clears only execution display without executing", shell.Workbench.ExecutionLog.Count == 0 && !shell.Workbench.IsOrderedRunRunning
                    && !shell.Workbench.IsSelectedStepPreviewRunning && shell.Workbench.RunLog.Count > rawCount, "Matches HTML newRecipeDialog; session activity retained.");
            }
            var lifecycleDocument = CreateThicknessDocument(sourcePath, 4.5, 5.5);
            var lifecyclePath = Path.Combine(root, "lifecycle.ov3d-recipe.json");
            ToolRecipeDocumentStore.Save(lifecyclePath, lifecycleDocument);
            Check("Ordered execution still gates, cancels, rejects stale completion, and observes failure",
                VerifyOrderedRunOwnerLifecycle(lifecycleDocument, lifecyclePath, out var lifecycle), lifecycle);
        }
        catch (Exception exception) { Check("Unexpected exception", false, exception.ToString()); }
        summary = $"TeachingExecutionLog|{(passed == total ? "Pass" : "Fail")}|checks={passed}/{total}";
        lines.Add(summary);
        File.WriteAllLines(reportPath, lines);
        return total > 0 && passed == total;
    }

    private static bool VerifyOrderedRunOwnerLifecycle(
        ToolRecipeDocument document,
        string recipePath,
        out string detail)
    {
        var capabilityCalls = 0;
        using var capabilityEntered = new ManualResetEventSlim();
        using var releaseCapability = new ManualResetEventSlim();
        var gatedOwner = new ToolWorkbenchOrderedRunExecutionOwner(
            () => document,
            _ =>
            {
                Interlocked.Increment(ref capabilityCalls);
                capabilityEntered.Set();
                releaseCapability.Wait();
                return (false, "blocked for single-flight verification");
            },
            () => recipePath,
            () => string.Empty,
            () => null,
            () => Array.Empty<ToolWorkbenchPipelineStepItem>(),
            _ => string.Empty,
            (_, english) => english,
            (_, _) => { },
            () => { },
            _ => { },
            () => { });

        var firstRun = Task.Run(() => gatedOwner.RunAsync());
        if (!capabilityEntered.Wait(TimeSpan.FromSeconds(5)))
        {
            detail = "single-flight capability gate was not entered within 5 seconds";
            return false;
        }

        // RunAsync checks the gate before its first await, so submit the
        // competing call directly while the first capability callback is
        // still blocked. A second ThreadPool work item would race the release
        // signal and make this focused proof scheduler-dependent.
        var secondRun = gatedOwner.RunAsync();
        releaseCapability.Set();
        var firstResult = firstRun.GetAwaiter().GetResult();
        var secondResult = secondRun.GetAwaiter().GetResult();
        var singleFlight = !firstResult
            && !secondResult
            && capabilityCalls == 1;

        using var sourceEntered = new ManualResetEventSlim();
        using var releaseSource = new ManualResetEventSlim();
        var completionCount = 0;
        var cancelledRecords = 0;
        var cancellationOwner = new ToolWorkbenchOrderedRunExecutionOwner(
            () => document,
            _ => (true, "ready"),
            () => recipePath,
            () =>
            {
                sourceEntered.Set();
                releaseSource.Wait();
                return string.Empty;
            },
            () => null,
            () => Array.Empty<ToolWorkbenchPipelineStepItem>(),
            _ => "completed",
            (_, english) => english,
            (_, _) => { },
            () => { },
            _ => Interlocked.Increment(ref completionCount),
            () => { },
            recordExecution: (_, status, cancelled) => { if (cancelled && status is null) Interlocked.Increment(ref cancelledRecords); });
        var cancellationRun = Task.Run(() => cancellationOwner.RunAsync());
        if (!sourceEntered.Wait(TimeSpan.FromSeconds(5)))
        {
            detail = "cancellation source gate was not entered within 5 seconds";
            releaseSource.Set();
            cancellationRun.GetAwaiter().GetResult();
            return false;
        }

        var cancelCommandWasEnabled = cancellationOwner.CancelCommand.CanExecute(null);
        cancellationOwner.Cancel();
        releaseSource.Set();
        var cancellationResult = cancellationRun.GetAwaiter().GetResult();
        var cancellationWorked = !cancellationResult
            && !cancellationOwner.IsRunning
            && completionCount == 0
            && cancellationOwner.Summary.Contains("canceled", StringComparison.OrdinalIgnoreCase)
            && cancelCommandWasEnabled
            && cancelledRecords == 1;

        using var delayedSourceEntered = new ManualResetEventSlim();
        using var releaseDelayedSource = new ManualResetEventSlim();
        var delayedSourcePath = document.Source.Path;
        var delayedCompletionCount = 0;
        var delayedOwner = new ToolWorkbenchOrderedRunExecutionOwner(
            () => document,
            _ => (true, "ready"),
            () => recipePath,
            () => delayedSourcePath,
            () =>
            {
                delayedSourceEntered.Set();
                releaseDelayedSource.Wait();
                return null;
            },
            () => Array.Empty<ToolWorkbenchPipelineStepItem>(),
            _ => "completed",
            (_, english) => english,
            (_, _) => { },
            () => { },
            _ => Interlocked.Increment(ref delayedCompletionCount),
            () => { });
        var delayedRun = Task.Run(() => delayedOwner.RunAsync());
        if (!delayedSourceEntered.Wait(TimeSpan.FromSeconds(5)))
        {
            detail = "delayed Run source gate was not entered within 5 seconds";
            releaseDelayedSource.Set();
            delayedRun.GetAwaiter().GetResult();
            return false;
        }

        delayedSourcePath += ".changed";
        delayedOwner.Invalidate("The input changed while the saved recipe was running.");
        releaseDelayedSource.Set();
        var delayedRunResult = delayedRun.GetAwaiter().GetResult();
        var staleCompletionRejected = !delayedRunResult
            && delayedCompletionCount == 0
            && delayedOwner.Result is null
            && delayedOwner.RecordPath is null
            && !delayedOwner.IsRunning
            && delayedOwner.Summary.Contains("canceled", StringComparison.OrdinalIgnoreCase);
        delayedOwner.Dispose();

        using var commandFailureObserved = new ManualResetEventSlim();
        var failedRecords = 0;
        var commandFailureOwner = new ToolWorkbenchOrderedRunExecutionOwner(
            () => document,
            _ => (true, "ready"),
            () => recipePath,
            () => document.Source.Path,
            () => null,
            () => throw new InvalidOperationException("pipeline callback failure"),
            _ => "completed",
            (_, english) => english,
            (category, _) =>
            {
                if (string.Equals(category, "Error", StringComparison.Ordinal))
                {
                    commandFailureObserved.Set();
                }
            },
            () => { },
            _ => { },
            () => { },
            recordExecution: (_, status, cancelled) => { if (!cancelled && status == ResultStatus.Error) Interlocked.Increment(ref failedRecords); });
        var commandWasEnabled = commandFailureOwner.RunCommand.CanExecute(null);
        commandFailureOwner.RunCommand.Execute(null);
        var commandFailureWasObserved = commandFailureObserved.Wait(TimeSpan.FromSeconds(5))
            && !commandFailureOwner.IsRunning
            && commandFailureOwner.Summary.Contains("failed", StringComparison.OrdinalIgnoreCase)
            && failedRecords == 1;
        commandFailureOwner.Dispose();

        detail = $"singleFlight={singleFlight};capabilityCalls={capabilityCalls};"
            + $"cancellation={cancellationWorked};commandEnabled={commandWasEnabled};"
            + $"staleCompletionRejected={staleCompletionRejected};"
            + $"commandFailureObserved={commandFailureWasObserved};completionCount={completionCount};"
            + $"summary={cancellationOwner.Summary}";
        return singleFlight && cancellationWorked && staleCompletionRejected
            && commandWasEnabled && commandFailureWasObserved;
    }

    private static bool VerifySamePathInputBindingChange(
        string root,
        string recipePath,
        string runRoot,
        out string detail)
    {
        using var shell = CreateShell(root, "same-path-input-change", runRoot);
        if (!shell.Workbench.TryOpenTeachingRecipe(recipePath, out var openMessage))
        {
            detail = $"recipe open failed: {openMessage}";
            return false;
        }

        if (WaitForSourceQuality(shell.Workbench.SourceQuality) is null
            || !shell.Workbench.RunTeachingRecipeAsync().GetAwaiter().GetResult()
            || shell.Workbench.SourceSession.SourceBinding is not { } sourceBinding)
        {
            detail = $"Run or source binding unavailable: {shell.Workbench.OrderedRunCapabilitySummary}";
            return false;
        }

        var changedSourceBinding = sourceBinding with
        {
            ContentSha256 = new string(sourceBinding.ContentSha256[0] == 'A' ? 'B' : 'A', 64)
        };
        shell.Workbench.SetC3DSourceFromLoadedViewer(shell.Workbench.Source.Path, changedSourceBinding);
        var staleEvidenceInvalidated = shell.Workbench.IsDirty
            && !shell.Workbench.HasOrderedRunResult
            && shell.Workbench.CurrentOrderedRunRecordPath is null;
        shell.Workbench.SetC3DSourceFromLoadedViewer(shell.Workbench.Source.Path, sourceBinding);
        var recoveredQuality = WaitForSourceQuality(shell.Workbench.SourceQuality);
        var saved = recoveredQuality is not null
            && shell.Workbench.TrySaveTeachingRecipe(recipePath, out var saveMessage);
        var rerunCompleted = saved
            && shell.Workbench.RunTeachingRecipeAsync().GetAwaiter().GetResult();
        detail = $"invalidated={staleEvidenceInvalidated};dirtyAfterRecovery={shell.Workbench.IsDirty};"
            + $"saved={saved};rerun={rerunCompleted};record={shell.Workbench.CurrentOrderedRunRecordPath ?? "(none)"}";
        return staleEvidenceInvalidated
            && saved
            && rerunCompleted
            && !shell.Workbench.IsDirty
            && shell.Workbench.HasOrderedRunResult
            && shell.Workbench.CurrentOrderedRunRecordPath is not null;
    }

    private static ShellMainWindowViewModel CreateShell(
        string root,
        string name,
        string runRoot) =>
        new(
            recentRunRecordsPath: Path.Combine(root, $"recent-runs-{name}.json"),
            recentRecipesPath: Path.Combine(root, $"recent-recipes-{name}.json"),
            orderedRunRecordRoot: runRoot);

    private static bool VerifyRunRecordComparison(
        string root,
        string runRoot,
        InspectionRunRecord? sourceRecord,
        out string detail)
    {
        if (sourceRecord?.Steps is not { Count: > 0 } sourceSteps
            || sourceSteps.Any(step => string.IsNullOrWhiteSpace(step.SemanticFingerprint)
                                       || step.AlgorithmEvidence is null))
        {
            detail = "generated Run Record does not contain semantic and algorithm evidence";
            return false;
        }

        var comparisonRoot = Path.Combine(root, "run-record-comparison");
        Directory.CreateDirectory(comparisonRoot);
        var currentPath = Path.Combine(comparisonRoot, "current.json");
        var timeOnlyPath = Path.Combine(comparisonRoot, "time-only.json");
        var changedConditionPath = Path.Combine(comparisonRoot, "changed-condition.json");
        var incompatiblePath = Path.Combine(comparisonRoot, "incompatible-unit.json");
        var recoveryPath = Path.Combine(comparisonRoot, "malformed-then-recovered.json");
        InspectionRunRecordJson.Write(currentPath, sourceRecord);
        var timeOnlyRecord = sourceRecord with
        {
            RunId = "comparison-time-only",
            RecordedAtUtc = sourceRecord.RecordedAtUtc.AddMinutes(5),
            ElapsedMilliseconds = sourceRecord.ElapsedMilliseconds + 321.0,
            Timing = sourceRecord.Timing is { } timing
                ? timing with
                {
                    TotalElapsedMilliseconds = timing.TotalElapsedMilliseconds + 321.0,
                    Stages = timing.Stages.Select((stage, index) => index == 0
                        ? stage with { ElapsedMilliseconds = stage.ElapsedMilliseconds + 321.0 }
                        : stage).ToArray()
                }
                : null,
            Steps = sourceSteps.Select(step => step with
            {
                ElapsedMilliseconds = step.ElapsedMilliseconds + 10.0
            }).ToArray()
        };
        InspectionRunRecordJson.Write(timeOnlyPath, timeOnlyRecord);
        var changedSteps = sourceSteps.Select((step, index) => index == 0
            ? step with
            {
                SemanticFingerprint = ChangeSha256(step.SemanticFingerprint!),
                AlgorithmEvidence = step.AlgorithmEvidence! with
                {
                    AlgorithmDefinitionVersion = step.AlgorithmEvidence.AlgorithmDefinitionVersion + ".changed"
                }
            }
            : step).ToArray();
        var changedConditionRecord = sourceRecord with
        {
            RunId = "comparison-changed-condition",
            Recipe = sourceRecord.Recipe with
            {
                Sha256 = ChangeSha256(sourceRecord.Recipe.Sha256)
            },
            Steps = changedSteps
        };
        InspectionRunRecordJson.Write(changedConditionPath, changedConditionRecord);
        var incompatibleRecord = sourceRecord with
        {
            RunId = "comparison-incompatible-unit",
            Source = sourceRecord.Source with { Unit = sourceRecord.Source.Unit + ".changed" }
        };
        InspectionRunRecordJson.Write(incompatiblePath, incompatibleRecord);
        InspectionRunRecordJson.Write(recoveryPath, timeOnlyRecord with
        {
            RunId = "comparison-recovery"
        });

        var recentPath = Path.Combine(comparisonRoot, "recent-runs.json");
        using var shell = new ShellMainWindowViewModel(
            runRecordPath: currentPath,
            recentRunRecordsPath: recentPath,
            recentRecipesPath: Path.Combine(comparisonRoot, "recent-recipes.json"),
            orderedRunRecordRoot: runRoot);
        shell.LoadRunRecord(timeOnlyPath, out _);
        shell.LoadRunRecord(changedConditionPath, out _);
        shell.LoadRunRecord(incompatiblePath, out _);
        shell.LoadRunRecord(recoveryPath, out _);
        shell.LoadRunRecord(currentPath, out _);
        var initialRunSummary = shell.RunSnapshotSummary;
        var initialStepSummary = shell.InspectionStepSummary;

        shell.SelectedRecentRunRecord = SelectRecent(shell, timeOnlyPath);
        var timeCommandEnabled = shell.CompareRecentRunRecordCommand.CanExecute(null);
        shell.CompareRecentRunRecordCommand.Execute(null);
        var timeOnlyPassed = timeCommandEnabled
            && shell.RunRecordComparisonStatus == RunRecordComparisonState.DirectlyComparable
            && shell.RunRecordComparisonDetails.Contains("ms", StringComparison.Ordinal)
            && (shell.RunRecordComparisonDetails.Contains("판정/동일성", StringComparison.Ordinal)
                || shell.RunRecordComparisonDetails.Contains("excluded from condition identity", StringComparison.Ordinal));

        shell.SelectedRecentRunRecord = SelectRecent(shell, changedConditionPath);
        var changedCompared = shell.CompareSelectedRunRecord(out var changedMessage);
        var changedPassed = changedCompared
            && shell.RunRecordComparisonStatus == RunRecordComparisonState.ConditionsChanged
            && (shell.RunRecordComparisonDetails.Contains("fingerprint", StringComparison.OrdinalIgnoreCase)
                || changedMessage.Contains("조건", StringComparison.Ordinal));

        shell.SelectedRecentRunRecord = SelectRecent(shell, incompatiblePath);
        var incompatibleCompared = shell.CompareSelectedRunRecord(out _);
        var incompatiblePassed = incompatibleCompared
            && shell.RunRecordComparisonStatus == RunRecordComparisonState.NotDirectlyComparable;

        shell.SelectedRecentRunRecord = SelectRecent(shell, currentPath);
        var sameRecordDisabled = !shell.CompareRecentRunRecordCommand.CanExecute(null);

        shell.SelectedRecentRunRecord = SelectRecent(shell, recoveryPath);
        File.WriteAllText(recoveryPath, "{ malformed");
        var malformedRejected = !shell.CompareSelectedRunRecord(out _)
            && shell.RunRecordComparisonStatus == RunRecordComparisonState.Error;
        File.WriteAllText(recoveryPath, "{}");
        var missingShapeRejected = !shell.CompareSelectedRunRecord(out _)
            && shell.RunRecordComparisonStatus == RunRecordComparisonState.Error;
        InspectionRunRecordJson.Write(recoveryPath, timeOnlyRecord with
        {
            RunId = "comparison-recovered"
        });
        var recoveryAccepted = shell.CompareSelectedRunRecord(out _)
            && shell.RunRecordComparisonStatus == RunRecordComparisonState.DirectlyComparable;
        var currentEvidenceUnchanged = string.Equals(
                initialRunSummary,
                shell.RunSnapshotSummary,
                StringComparison.Ordinal)
            && string.Equals(
                initialStepSummary,
                shell.InspectionStepSummary,
                StringComparison.Ordinal)
            && !shell.Workbench.HasOrderedRunResult;

        detail = $"timeOnly={timeOnlyPassed};changed={changedPassed};"
            + $"incompatible={incompatiblePassed};sameDisabled={sameRecordDisabled};"
            + $"malformed={malformedRejected};missingShape={missingShapeRejected};recovery={recoveryAccepted};"
            + $"currentUnchanged={currentEvidenceUnchanged};state={shell.RunRecordComparisonStatus}";
        return timeOnlyPassed
            && changedPassed
            && incompatiblePassed
            && sameRecordDisabled
            && malformedRejected
            && missingShapeRejected
            && recoveryAccepted
            && currentEvidenceUnchanged;
    }

    private static RunRecordRecentItem SelectRecent(
        ShellMainWindowViewModel shell,
        string path) => shell.RecentRunRecords.Single(item => string.Equals(
            item.Path,
            Path.GetFullPath(path),
            StringComparison.OrdinalIgnoreCase));

    private static string ChangeSha256(string value) =>
        (value.StartsWith('0') ? "1" : "0") + value[1..];

    private static SourceQualityReport? WaitForSourceQuality(
        SourceQualityWorkspaceViewModel workspace)
    {
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(5);
        while (workspace.Report is null
               && !workspace.HasError
               && DateTimeOffset.UtcNow < deadline)
        {
            Thread.Sleep(10);
        }

        return workspace.Report;
    }

    private static ToolRecipeDocument CreateThicknessDocument(
        string sourcePath,
        double minimum,
        double maximum)
    {
        var identity = ToolRecipeSelectionSourceBindingVerifier.ReadIdentity(
            sourcePath);
        var source = new ToolRecipeSource(
            "source.c3d.height-map",
            "Thickness verification",
            "C3D",
            "raw-height",
            "frame.c3d-grid-index",
            Path.GetFullPath(sourcePath),
            new FileInfo(sourcePath).Length,
            identity.ContentSha256,
            identity.GridWidth,
            identity.GridHeight);
        var reference = new ToolRecipeSelection(
            "selection.reference",
            "Reference surface ROI",
            ToolRecipeSelectionKinds.GridRectangle,
            source.Id,
            source.FrameId,
            identity,
            new ToolRecipeGridRectangle(0, 0, 2, 4),
            null,
            null);
        var measurement = new ToolRecipeSelection(
            "selection.measurement",
            "Measurement ROI",
            ToolRecipeSelectionKinds.GridRectangle,
            source.Id,
            source.FrameId,
            identity,
            new ToolRecipeGridRectangle(2, 0, 2, 4),
            null,
            null);
        var step = new ToolRecipeStep(
            "step.thickness.01",
            "thickness",
            "Thickness",
            3,
            [source.Id, reference.Id, measurement.Id],
            "derived.measurementresult.01",
            [
                new ToolRecipeParameter("MinimumThickness", minimum.ToString("G17", System.Globalization.CultureInfo.InvariantCulture)),
                new ToolRecipeParameter("MaximumThickness", maximum.ToString("G17", System.Globalization.CultureInfo.InvariantCulture)),
                new ToolRecipeParameter("MinimumValidSampleCount", "1")
            ]);
        return new ToolRecipeDocument(
            ToolRecipeDocument.CurrentSchemaVersion,
            "Thickness ordered Run verification",
            source,
            [],
            [step],
            [reference, measurement]);
    }

    private static bool VerifyPublishedCropParity(
        string root,
        string runRoot,
        out string detail)
    {
        var sourcePath = Path.Combine(root, "thickness-crop-source.C3D");
        var sourceValues = Enumerable.Range(0, 4)
            .SelectMany(_ => new[] { 99d, 10d, 10d, 15d, 15d, 88d })
            .ToArray();
        C3DHeightFieldSnapshot.CreateForVerification(
            "source.c3d.height-map",
            6,
            4,
            sourceValues)
            .SaveC3D(sourcePath);
        var sourceIdentity = ToolRecipeSelectionSourceBindingVerifier.ReadIdentity(sourcePath);
        var source = new ToolRecipeSource(
            "source.c3d.height-map",
            "Thickness crop verification",
            "C3D",
            "raw-height",
            "frame.c3d-grid-index",
            Path.GetFullPath(sourcePath),
            new FileInfo(sourcePath).Length,
            sourceIdentity.ContentSha256,
            sourceIdentity.GridWidth,
            sourceIdentity.GridHeight);
        var cropSelection = new ToolRecipeSelection(
            "selection.thickness.crop",
            "Published crop ROI",
            ToolRecipeSelectionKinds.GridRectangle,
            source.Id,
            source.FrameId,
            new ToolRecipeSelectionSourceBinding(
                "C3D",
                source.ContentSha256!,
                source.GridWidth!.Value,
                source.GridHeight!.Value),
            new ToolRecipeGridRectangle(0, 1, 4, 4),
            null,
            null);
        var cropStep = new ToolRecipeStep(
            "step.thickness.crop",
            "roi-crop",
            "ROI / Crop",
            1,
            [source.Id, cropSelection.Id],
            "derived.thickness.crop",
            [
                new ToolRecipeParameter("ROI", "Select in Viewer"),
                new ToolRecipeParameter("Output frame", "Keep source frame")
            ]);
        var seedDocument = new ToolRecipeDocument(
            ToolRecipeDocument.CurrentSchemaVersion,
            "Thickness published crop verification",
            source,
            [],
            [cropStep],
            [cropSelection]);
        var cropEvaluation = ToolRecipeRoiCropExecution.Execute(
            seedDocument,
            cropStep.Id,
            root);
        if (cropEvaluation.Output is not C3DHeightFieldSnapshot cropOutput)
        {
            detail = $"crop preparation failed: status={cropEvaluation.Result.Status};message={cropEvaluation.Result.Message}";
            return false;
        }

        var cropBinding = new ToolRecipeSelectionSourceBinding(
            "HeightField",
            cropOutput.ContentSha256,
            cropOutput.Width,
            cropOutput.Height,
            cropOutput.EntityId,
            source.ContentSha256,
            cropOutput.Unit,
            cropOutput.FrameId);
        var reference = new ToolRecipeSelection(
            "selection.thickness.crop.reference",
            "Cropped reference ROI",
            ToolRecipeSelectionKinds.GridRectangle,
            source.Id,
            cropOutput.FrameId,
            cropBinding,
            new ToolRecipeGridRectangle(0, 0, 4, 2),
            null,
            null);
        var measurement = new ToolRecipeSelection(
            "selection.thickness.crop.measurement",
            "Cropped measurement ROI",
            ToolRecipeSelectionKinds.GridRectangle,
            source.Id,
            cropOutput.FrameId,
            cropBinding,
            new ToolRecipeGridRectangle(0, 2, 4, 2),
            null,
            null);
        var measurementStep = new ToolRecipeStep(
            "step.thickness.crop.measurement",
            "thickness",
            "Thickness",
            3,
            [cropStep.OutputEntityId, reference.Id, measurement.Id],
            "derived.thickness.crop.measurement",
            [
                new ToolRecipeParameter("MinimumThickness", "4.5"),
                new ToolRecipeParameter("MaximumThickness", "5.5"),
                new ToolRecipeParameter("MinimumValidSampleCount", "1")
            ]);
        var document = seedDocument with
        {
            Steps = [cropStep, measurementStep],
            Selections = [cropSelection, reference, measurement]
        };
        var recipePath = Path.Combine(root, "thickness-published-crop.ov3d-recipe.json");
        ToolRecipeDocumentStore.Save(recipePath, document);
        var savedRecipePath = Path.Combine(root, "thickness-published-crop-saved.ov3d-recipe.json");
        using (var firstShell = CreateShell(root, "published-crop-first", runRoot))
        {
            var opened = firstShell.Workbench.TryOpenTeachingRecipe(
                recipePath,
                out var openMessage);
            var firstSourceQuality = WaitForSourceQuality(
                firstShell.Workbench.SourceQuality);
            if (!opened || firstSourceQuality is null)
            {
                detail = $"initial crop recipe open failed: {openMessage}";
                return false;
            }

            if (!firstShell.Workbench.SelectPipelineStep(cropStep.Id)
                || !firstShell.Workbench.PreviewSelectedRoiCropAsync().GetAwaiter().GetResult()
                || !firstShell.Workbench.HasCurrentRoiCropPreview)
            {
                detail = $"crop Preview failed: {firstShell.Workbench.RoiCropExecutionSummary}";
                return false;
            }

            firstShell.Workbench.PublishSelectedStepCommand.Execute(null);
            if (!firstShell.Workbench.IsRoiCropPreviewPublished)
            {
                detail = $"crop Publish failed: {firstShell.Workbench.RoiCropExecutionSummary}";
                return false;
            }

            if (!firstShell.Workbench.TrySaveTeachingRecipe(savedRecipePath, out var saveMessage))
            {
                detail = $"crop recipe save failed: {saveMessage}";
                return false;
            }
        }

        var reopenedDocument = ToolRecipeDocumentStore.Load(savedRecipePath);
        if (!Path.IsPathFullyQualified(reopenedDocument.Source.Path))
        {
            reopenedDocument = reopenedDocument with
            {
                Source = reopenedDocument.Source with
                {
                    Path = Path.GetFullPath(Path.Combine(
                        Path.GetDirectoryName(savedRecipePath)!,
                        reopenedDocument.Source.Path))
                }
            };
        }
        using var reopenedShell = CreateShell(root, "published-crop-reopen", runRoot);
        if (!reopenedShell.Workbench.TryOpenTeachingRecipe(savedRecipePath, out var reopenMessage))
        {
            detail = $"reopened crop recipe failed: {reopenMessage}";
            return false;
        }

        var sourceQuality = WaitForSourceQuality(reopenedShell.Workbench.SourceQuality);
        if (sourceQuality is null || !reopenedShell.Workbench.RunTeachingRecipeCommand.CanExecute(null))
        {
            detail = $"reopened crop recipe is not Run-ready: quality={sourceQuality is not null};summary={reopenedShell.Workbench.OrderedRunCapabilitySummary}";
            return false;
        }

        var runCompleted = reopenedShell.Workbench.RunTeachingRecipeAsync()
            .GetAwaiter()
            .GetResult();
        var recordPath = reopenedShell.Workbench.CurrentOrderedRunRecordPath;
        var uiRecord = ReadRecord(recordPath);
        var headless = ToolRecipeOrderedGraphExecution.Execute(
            reopenedDocument,
            reopenedDocument.Source.Path,
            sourceQuality);
        var parity = CompareSemanticRunRecord(
            uiRecord,
            reopenedDocument,
            headless,
            savedRecipePath,
            out var parityDetail);
        var measuredStep = uiRecord?.Steps?.FirstOrDefault(step =>
            step.ToolId.Equals("thickness", StringComparison.OrdinalIgnoreCase));
        var operatorSummary = reopenedShell.ResultsOperatorDecisionSummary;
        var multistepResultsProjection = measuredStep?.Metrics.SingleOrDefault(metric =>
            metric.Name.Equals("Mean", StringComparison.OrdinalIgnoreCase)) is { } meanMetric
            && operatorSummary.Contains(FormatRecordedMetric(meanMetric), StringComparison.Ordinal)
            && operatorSummary.Contains("Pass", StringComparison.Ordinal);
        detail = $"opened=1;crop={cropOutput.Width}x{cropOutput.Height}@{cropOutput.GridOriginColumn},{cropOutput.GridOriginRow};"
            + $"runCompleted={runCompleted};uiRecord={recordPath};headless={headless.Status};"
            + $"multistepResults={multistepResultsProjection};operator={operatorSummary.Replace(Environment.NewLine, " | ")};{parityDetail}";
        return runCompleted && parity && multistepResultsProjection;
    }

    private static bool CompareSemanticRunRecord(
        InspectionRunRecord? uiRecord,
        ToolRecipeDocument document,
        ToolRecipeOrderedGraphExecutionResult headless,
        string recipePath,
        out string detail)
    {
        var projection = ToolRecipeOrderedGraphRunRecordProjection.Create(
            document,
            headless);
        var uiSteps = uiRecord?.Steps ?? [];
        var stepParity = uiSteps.Count == projection.Count
            && uiSteps.Zip(projection).All(pair =>
                pair.First.Id == pair.Second.Id
                && pair.First.ToolId == pair.Second.ToolId
                && pair.First.InputEntityIds.SequenceEqual(pair.Second.InputEntityIds)
                && pair.First.OutputEntityId == pair.Second.OutputEntityId
                && pair.First.Status == pair.Second.Status
                && pair.First.OutputContentSha256 == pair.Second.OutputContentSha256
                && pair.First.Metrics.SequenceEqual(pair.Second.Metrics));
        var sourceParity = uiRecord is not null
            && uiRecord.Status == headless.Status
            && uiRecord.Source.EntityId == document.Source.Id
            && uiRecord.Source.Sha256 == document.Source.ContentSha256
            && uiRecord.Source.ByteLength == document.Source.ByteLength
            && uiRecord.Source.Unit == document.Source.Unit
            && headless.SourceContentSha256 == document.Source.ContentSha256;
        var recipeBytes = File.ReadAllBytes(recipePath);
        var recipeHash = Convert.ToHexString(SHA256.HashData(recipeBytes));
        var headlessCropOutputSha256 = headless.Steps.FirstOrDefault()?.OutputContentSha256;
        var recipeParity = uiRecord?.Recipe.Path == Path.GetFullPath(recipePath)
            && string.Equals(uiRecord.Recipe.Sha256, recipeHash, StringComparison.OrdinalIgnoreCase)
            && document.Steps.Any(step => step.ToolId == "roi-crop")
            && document.Selections?.Where(selection => selection.SourceBinding.OwnerEntityId == "derived.thickness.crop")
                .Count() == 2
            && headless.ReboundDocument.Selections?.Where(selection => selection.SourceBinding.OwnerEntityId == "derived.thickness.crop")
                .All(selection => headlessCropOutputSha256 is not null
                    && selection.SourceBinding.ContentSha256 == headlessCropOutputSha256)
                == true;
        detail = $"status={uiRecord?.Status}/{headless.Status};steps={uiSteps.Count}/{projection.Count};"
            + $"source={sourceParity};recipe={recipeParity};stepFields={stepParity};"
            + $"headlessMessage={headless.Message};outputs={string.Join(',', uiSteps.Select(step => step.OutputContentSha256 ?? "(none)"))}";
        return sourceParity && recipeParity && stepParity;
    }

    private static bool VerifyNoDataParity(
        string root,
        string runRoot,
        out string detail)
    {
        var sourcePath = Path.Combine(root, "thickness-no-data-source.C3D");
        var sourceValues = Enumerable.Range(0, 4)
            .SelectMany(_ => new[] { 10d, 10d, double.NaN, double.NaN })
            .ToArray();
        C3DHeightFieldSnapshot.CreateForVerification(
            "source.c3d.height-map",
            4,
            4,
            sourceValues)
            .SaveC3D(sourcePath);
        var recipePath = Path.Combine(root, "thickness-no-data.ov3d-recipe.json");
        var document = CreateThicknessDocument(sourcePath, 4.5, 5.5);
        ToolRecipeDocumentStore.Save(recipePath, document);
        using var shell = CreateShell(root, "no-data", runRoot);
        if (!shell.Workbench.TryOpenTeachingRecipe(recipePath, out var openMessage))
        {
            detail = $"NoData recipe open failed: {openMessage}";
            return false;
        }

        var sourceQuality = WaitForSourceQuality(shell.Workbench.SourceQuality);
        var runCompleted = shell.Workbench.RunTeachingRecipeAsync()
            .GetAwaiter()
            .GetResult();
        var record = ReadRecord(shell.Workbench.CurrentOrderedRunRecordPath);
        var headless = ToolRecipeOrderedGraphExecution.Execute(
            document,
            sourcePath,
            sourceQuality);
        var uiStep = record?.Steps?.SingleOrDefault();
        var headlessStep = headless.Steps.SingleOrDefault();
        var noDataMeanMetric = uiStep?.Metrics.SingleOrDefault(metric =>
            metric.Name.Equals("Mean", StringComparison.OrdinalIgnoreCase));
        var passed = runCompleted
            && headless.Status == ResultStatus.Fail
            && record?.Status == ResultStatus.Fail
            && headlessStep?.Result.Status == ResultStatus.Fail
            && uiStep?.Status == ResultStatus.Fail
            && headless.Status is not ResultStatus.Error
            && record.Status is not ResultStatus.Error
            && noDataMeanMetric is not null
            && shell.ResultsOperatorDecisionSummary.Contains(
                FormatRecordedMetric(noDataMeanMetric),
                StringComparison.Ordinal)
            && shell.ResultsOperatorDecisionSummary.Contains(
                "Fail",
                StringComparison.Ordinal)
            && (shell.ResultsOperatorDecisionSummary.Contains(
                    "Reason",
                    StringComparison.Ordinal)
                || shell.ResultsOperatorDecisionSummary.Contains(
                    "사유",
                    StringComparison.Ordinal));
        detail = $"quality={sourceQuality is not null};runCompleted={runCompleted};"
            + $"ui={record?.Status};headless={headless.Status};uiStep={uiStep?.Status};"
            + $"headlessStep={headlessStep?.Result.Status};message={headless.Message};"
            + $"operator={shell.ResultsOperatorDecisionSummary.Replace(Environment.NewLine, " | ")}";
        return passed;
    }

    private static bool VerifyRecordSaveFailure(
        string root,
        string recipePath,
        string runRoot,
        out string detail)
    {
        var blockedRoot = Path.Combine(root, "run-record-root-file");
        File.WriteAllText(blockedRoot, "record root intentionally occupied by a file");
        using var shell = CreateShell(root, "record-save-failure", blockedRoot);
        if (!shell.Workbench.TryOpenTeachingRecipe(recipePath, out var openMessage))
        {
            detail = $"record-save-failure recipe open failed: {openMessage}";
            return false;
        }

        _ = WaitForSourceQuality(shell.Workbench.SourceQuality);
        var runCompleted = shell.Workbench.RunTeachingRecipeAsync()
            .GetAwaiter()
            .GetResult();
        var failedStatus = shell.StatusText;
        var failedValidity = shell.ResultsValueValiditySummary;
        var failedDecision = shell.ResultsOperatorDecisionSummary;
        var failedToPersist = shell.Workbench.CurrentOrderedRunRecordPath is null
            && shell.Workbench.HasOrderedRunResult
            && shell.Workbench.HasPendingOrderedRunRecordSave
            && (failedStatus.Contains("Run Record", StringComparison.OrdinalIgnoreCase)
                || failedStatus.Contains("검사 기록", StringComparison.Ordinal))
            && (failedValidity.Contains("Run Record", StringComparison.OrdinalIgnoreCase)
                || failedValidity.Contains("검사 기록", StringComparison.Ordinal))
            && (failedValidity.Contains("Error", StringComparison.Ordinal)
                || failedValidity.Contains("오류", StringComparison.Ordinal))
            && failedDecision.Contains("Error", StringComparison.Ordinal)
            && (failedDecision.Contains("Run Record", StringComparison.OrdinalIgnoreCase)
                || failedDecision.Contains("검사 기록", StringComparison.Ordinal));
        var calculated = shell.Workbench.CurrentOrderedRunResult;
        var pendingRecord = shell.Workbench.CurrentOrderedRunRecord;
        var recipeBeforeRetry = File.ReadAllBytes(recipePath);
        var sourcePath = shell.Workbench.Source.Path;
        if (!Path.GetFullPath(sourcePath).StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Fault injection source must stay inside the owned verification fixture.");
        var sourceBeforeRetry = File.ReadAllBytes(sourcePath);
        var blockedNewRun = !shell.Workbench.RunTeachingRecipeAsync().GetAwaiter().GetResult()
            && !shell.Workbench.RunTeachingRecipeCommand.CanExecute(null);
        var blockedRecipeOpen = !shell.Workbench.TryOpenTeachingRecipe(recipePath, out _);
        shell.Workbench.CreateNewTeachingRecipe("must remain unchanged");
        var blockedReplacement = blockedNewRun && blockedRecipeOpen && shell.Workbench.IsRecipeMutationBlocked
            && !shell.Workbench.CanSaveTeachingRecipe && ReferenceEquals(calculated, shell.Workbench.CurrentOrderedRunResult)
            && shell.Workbench.RecipePath == recipePath;
        shell.Workbench.RetryOrderedRunRecordSaveCommand.Execute(null);
        var retryFailurePreserves = shell.Workbench.HasPendingOrderedRunRecordSave
            && ReferenceEquals(calculated, shell.Workbench.CurrentOrderedRunResult)
            && ReferenceEquals(pendingRecord, shell.Workbench.CurrentOrderedRunRecord);
        File.Delete(blockedRoot);
        InspectionRunRecord? recovered;
        try
        {
            // Only isolated fixture bytes are changed; saving must use the original calculation identity.
            File.AppendAllText(recipePath, " ");
            File.Delete(sourcePath);
            shell.Workbench.RetryOrderedRunRecordSaveCommand.Execute(null);
            recovered = ReadRecord(shell.Workbench.CurrentOrderedRunRecordPath);
        }
        finally
        {
            File.WriteAllBytes(recipePath, recipeBeforeRetry);
            File.WriteAllBytes(sourcePath, sourceBeforeRetry);
        }
        var retrySavedOriginal = recovered is not null && pendingRecord is not null
            && recovered.RunId == pendingRecord.RunId && recovered.RecordedAtUtc == pendingRecord.RecordedAtUtc
            && recovered.Recipe.Sha256 == pendingRecord.Recipe.Sha256 && recovered.Source.Sha256 == pendingRecord.Source.Sha256
            && ReferenceEquals(calculated, shell.Workbench.CurrentOrderedRunResult) && !shell.Workbench.HasPendingOrderedRunRecordSave
            && recovered.ElapsedMilliseconds == pendingRecord.ElapsedMilliseconds
            && recovered.Steps![0].Metrics.SequenceEqual(pendingRecord.Steps![0].Metrics);
        detail = $"runCompleted={runCompleted};record={(shell.Workbench.CurrentOrderedRunRecordPath ?? "(none)")};"
            + $"result={shell.Workbench.HasOrderedRunResult};status={shell.StatusText};initialStatus={failedStatus};"
            + $"initialValidity={failedValidity.Replace(Environment.NewLine, " | ")};"
            + $"initialDecision={failedDecision.Replace(Environment.NewLine, " | ")};"
            + $"operator={shell.ResultsOperatorDecisionSummary.Replace(Environment.NewLine, " | ")};"
            + $"pending={pendingRecord?.RunId};replacementBlocked={blockedReplacement};retryFailurePreserves={retryFailurePreserves};savedOriginal={retrySavedOriginal}";
        return runCompleted && failedToPersist && blockedReplacement && retryFailurePreserves && retrySavedOriginal;
    }

    private static void VerifySavedRecipeContext(string root, string runRoot, ToolRecipeDocument document, Action<string, bool, string> check)
    {
        var path = Path.Combine(root, "saved-context.ov3d-recipe.json");
        ToolRecipeDocumentStore.Save(path, document, out var writtenHash);
        _ = ToolRecipeDocumentStore.Load(path, out var loadedHash);
        check("recipe store identities describe the bytes written and loaded", writtenHash == loadedHash && loadedHash == ComputeHashBytes(File.ReadAllBytes(path)), loadedHash);
        using var shell = CreateShell(root, "saved-context", runRoot);
        shell.Workbench.TryOpenTeachingRecipe(path, out _);
        _ = WaitForSourceQuality(shell.Workbench.SourceQuality);
        var count = 0;
        shell.Workbench.OrderedRunCompleted += (_, _) => count++;
        var originalBytes = File.ReadAllBytes(path);
        File.AppendAllText(path, " ");
        check("external recipe edit before the first Run fails closed without a record", !shell.Workbench.RunTeachingRecipeCommand.CanExecute(null)
            && !shell.Workbench.RunTeachingRecipeAsync().GetAwaiter().GetResult() && count == 0 && !shell.Workbench.HasOrderedRunResult, shell.Workbench.OrderedRunCapabilitySummary);
        File.WriteAllBytes(path, originalBytes);
        check("restoring the accepted recipe bytes recovers readiness without execution", shell.Workbench.RunTeachingRecipeCommand.CanExecute(null) && count == 0, shell.Workbench.OrderedRunCapabilitySummary);
        shell.Workbench.RunTeachingRecipeAsync().GetAwaiter().GetResult();
        var previousResult = shell.Workbench.CurrentOrderedRunResult;
        var previousRecord = shell.Workbench.CurrentOrderedRunRecord;
        File.AppendAllText(path, " ");
        check("rejected external recipe edit retains the previous calculation and record", !shell.Workbench.RunTeachingRecipeAsync().GetAwaiter().GetResult()
            && count == 1 && ReferenceEquals(previousResult, shell.Workbench.CurrentOrderedRunResult) && ReferenceEquals(previousRecord, shell.Workbench.CurrentOrderedRunRecord), shell.Workbench.OrderedRunCapabilitySummary);
        var externalBytes = File.ReadAllBytes(path);
        var rejectedSave = !shell.Workbench.TrySaveTeachingRecipe(path, out var rejectionMessage);
        check("save rejects external recipe edits without overwriting bytes or calculation", rejectedSave
            && externalBytes.SequenceEqual(File.ReadAllBytes(path)) && count == 1
            && ReferenceEquals(previousResult, shell.Workbench.CurrentOrderedRunResult)
            && ReferenceEquals(previousRecord, shell.Workbench.CurrentOrderedRunRecord), rejectionMessage);
        File.WriteAllBytes(path, originalBytes);
        var saved = shell.Workbench.TrySaveTeachingRecipe(path, out var saveMessage);
        check("explicit save after restoring accepted bytes preserves the calculation", saved && count == 1 && shell.Workbench.RunTeachingRecipeCommand.CanExecute(null)
            && ReferenceEquals(previousResult, shell.Workbench.CurrentOrderedRunResult)
            && ReferenceEquals(previousRecord, shell.Workbench.CurrentOrderedRunRecord), saveMessage);

        var inject = false;
        var completions = 0;
        using var owner = new ToolWorkbenchOrderedRunExecutionOwner(() => document, _ =>
        {
            if (inject) File.AppendAllText(path, " ");
            return (true, "ready");
        }, () => path, () => document.Source.Path, () => null, () => [], _ => "complete", (_, english) => english,
            (_, _) => { }, () => { }, _ => completions++, () => { }, getSavedRecipeSha256: () => ComputeHashBytes(originalBytes));
        File.WriteAllBytes(path, originalBytes);
        owner.RunAsync().GetAwaiter().GetResult();
        var ownerPrevious = owner.Result;
        inject = true;
        check("recipe edit between admission and identity capture retains the old result", !owner.RunAsync().GetAwaiter().GetResult()
            && completions == 1 && ReferenceEquals(ownerPrevious, owner.Result) && owner.Summary.Contains("changed", StringComparison.OrdinalIgnoreCase), owner.Summary);
        inject = false;
        File.WriteAllBytes(path, originalBytes);
        check("identity race recovery executes only on a new explicit Run", owner.RunAsync().GetAwaiter().GetResult() && completions == 2, $"completions={completions}");
    }

    private static string ComputeHashBytes(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    private static void VerifyRealQueueRecordRecovery(string root, string recipePath, Action<string, bool, string> check)
    {
        var blockedRoot = Path.Combine(root, "queue-record-root-file");
        File.WriteAllText(blockedRoot, "owned queue record-save fault");
        using var shell = CreateShell(root, "queue-record-recovery", blockedRoot);
        shell.Workbench.TryOpenTeachingRecipe(recipePath, out _);
        _ = WaitForSourceQuality(shell.Workbench.SourceQuality);
        var secondSource = Path.Combine(root, "queue-second.C3D");
        File.Copy(shell.Workbench.Source.Path, secondSource, overwrite: true);
        var queue = shell.Workbench.InspectionQueue;
        queue.AddSources([shell.Workbench.Source.Path, secondSource]);
        var runs = 0;
        shell.Workbench.OrderedRunCompleted += (_, _) => runs++;
        queue.RunAllCommand.Execute(null);
        if (!SpinWait.SpinUntil(() => !queue.IsRunning, TimeSpan.FromSeconds(15))) throw new TimeoutException("Owned queue record-save fault did not finish.");
        var first = queue.Samples[0];
        var pending = first.Record;
        var calculation = shell.Workbench.CurrentOrderedRunResult;
        check("real queue stops at record-save failure with calculation retained", runs == 1 && queue.HasPendingRecordSave && pending is not null
            && calculation?.Status == ResultStatus.Pass && first.Steps.Count == 1 && queue.Samples[1].State == InspectionQueueItemState.Untested,
            $"runs={runs};pending={queue.HasPendingRecordSave};next={queue.Samples[1].State}");
        check("pending real queue blocks teaching import and re-entry", !queue.UseSelectedInputForTeachingCommand.CanExecute(null)
            && !queue.RunAllCommand.CanExecute(null) && shell.Workbench.IsRecipeMutationBlocked, $"import={queue.CanUseSelectedInputForTeaching}");
        queue.RetryRecordSaveCommand.Execute(null);
        check("failed real save retry does not calculate again", runs == 2 && queue.HasPendingRecordSave && ReferenceEquals(pending, first.Record)
            && ReferenceEquals(calculation, shell.Workbench.CurrentOrderedRunResult), $"completionNotifications={runs};runId={first.Record?.RunId}");
        File.Delete(blockedRoot);
        queue.RetryRecordSaveCommand.Execute(null);
        var record = ReadRecord(first.RunRecordPath);
        check("successful real save retry retains original identity and leaves remainder idle", !queue.HasPendingRecordSave && record is not null && pending is not null && record.RunId == pending.RunId
            && record?.RecordedAtUtc == pending?.RecordedAtUtc && record?.Recipe.Sha256 == pending?.Recipe.Sha256
            && record?.Source.Sha256 == pending?.Source.Sha256 && ReferenceEquals(calculation, shell.Workbench.CurrentOrderedRunResult)
            && queue.Samples[1].State == InspectionQueueItemState.Untested && !queue.IsRunning, first.RunRecordPath ?? "no record");
        var firstRecordPath = first.RunRecordPath;
        queue.RunRemainingCommand.Execute(null);
        if (!SpinWait.SpinUntil(() => !queue.IsRunning, TimeSpan.FromSeconds(15))) throw new TimeoutException("Owned remaining-file Run did not finish.");
        check("explicit real remainder Run preserves the saved first record", first.RunRecordPath == firstRecordPath && first.Record?.RunId == pending?.RunId
            && queue.Samples[1].State == InspectionQueueItemState.Pass && queue.Samples[1].Record?.Source.Path == secondSource, $"first={first.Record?.RunId};second={queue.Samples[1].Record?.RunId}");
    }

    private static bool VerifyResultsValueValidityScenarios(
        string root,
        string runRoot,
        InspectionRunRecord? sourceRecord,
        out string detail)
    {
        if (sourceRecord?.Steps?.SingleOrDefault() is not { } sourceStep)
        {
            detail = "source Run Record step is unavailable";
            return false;
        }

        var warpageStep = sourceStep with
        {
            ToolId = "warpage",
            ToolName = "C3D Warpage",
            Status = ResultStatus.Pass,
            Message = "Best-fit residuals use declared raw-height scalar values; physical calibration is not inferred.",
            Metrics =
            [
                new InspectionRunMetric("PeakToValley", MetricKind.Deviation, 0.3, "raw-height", ResultStatus.Pass),
                new InspectionRunMetric("Rms", MetricKind.Deviation, 0.1, "raw-height", ResultStatus.Pass),
                new InspectionRunMetric("MinimumResidual", MetricKind.Deviation, -0.2, "raw-height", ResultStatus.Pass),
                new InspectionRunMetric("MaximumResidual", MetricKind.Deviation, 0.1, "raw-height", ResultStatus.Pass),
                new InspectionRunMetric("ValidSampleCount", MetricKind.Count, 8, "count", ResultStatus.Pass)
            ],
            Overlays =
            [
                new InspectionRunOverlay(
                    "overlay.c3d-warpage-roi",
                    OverlayKind.Box,
                    "C3D Warpage best-fit inspection ROI",
                    ResultStatus.Pass,
                    sourceRecord.Source.EntityId)
            ],
            AlgorithmEvidence = new InspectionRunAlgorithmEvidence(
                "C3DWarpage.v1",
                "OpenVisionLab.VisionSdk.ThreeD",
                "verification")
        };
        var warpageRecord = sourceRecord with
        {
            ToolName = "C3D Warpage",
            Status = ResultStatus.Pass,
            Message = "Warpage result uses the declared raw-height frame.",
            Steps = [warpageStep]
        };
        var warpagePath = Path.Combine(root, "results-value-validity-warpage.json");
        InspectionRunRecordJson.Write(warpagePath, warpageRecord);
        using var warpageShell = CreateShell(root, "results-value-validity-warpage", runRoot);
        var warpageLoaded = warpageShell.LoadRunRecord(warpagePath, out var warpageMessage);
        var warpageSummary = warpageShell.ResultsValueValiditySummary;
        var warpageOperatorSummary = warpageShell.ResultsOperatorDecisionSummary;
        var warpagePass = warpageLoaded
            && warpageSummary.Contains("PeakToValley=0.3", StringComparison.Ordinal)
            && warpageSummary.Contains("Rms=0.1", StringComparison.Ordinal)
            && warpageSummary.Contains("ValidSampleCount=8", StringComparison.Ordinal)
            && warpageSummary.Contains("best-fit inspection ROI", StringComparison.OrdinalIgnoreCase)
            && warpageSummary.Contains("Pass", StringComparison.Ordinal)
            && warpageOperatorSummary.Contains("PeakToValley=0.3", StringComparison.Ordinal)
            && warpageOperatorSummary.Contains("Rms=0.1", StringComparison.Ordinal)
            && warpageOperatorSummary.Contains("Pass", StringComparison.Ordinal);

        var invalidMetrics = warpageStep.Metrics
            .Select(metric => metric with { Unit = string.Empty })
            .ToArray();
        var invalidRecord = warpageRecord with
        {
            Source = warpageRecord.Source with { Unit = string.Empty },
            SourceQualityEvidence = InspectionRunSourceQualityEvidence.Unavailable(
                "Unit was not declared for this failure fixture."),
            Status = ResultStatus.Fail,
            Message = "Bad reference plane prevented a valid warpage decision.",
            Steps =
            [
                warpageStep with
                {
                    Status = ResultStatus.Fail,
                    Message = "Insufficient valid ROI samples for the fitted reference plane.",
                    Metrics = invalidMetrics
                }
            ]
        };
        var invalidPath = Path.Combine(root, "results-value-validity-invalid.json");
        InspectionRunRecordJson.Write(invalidPath, invalidRecord);
        using var invalidShell = CreateShell(root, "results-value-validity-invalid", runRoot);
        var invalidLoaded = invalidShell.LoadRunRecord(invalidPath, out var invalidMessage);
        var invalidSummary = invalidShell.ResultsValueValiditySummary;
        var invalidOperatorSummary = invalidShell.ResultsOperatorDecisionSummary;
        var invalidPass = invalidLoaded
            && invalidSummary.Contains("Unknown", StringComparison.Ordinal)
            && invalidSummary.Contains("Fail (NG)", StringComparison.Ordinal)
            && invalidSummary.Contains("Bad reference plane", StringComparison.Ordinal)
            && invalidSummary.Contains("Insufficient valid ROI samples", StringComparison.Ordinal)
            && invalidOperatorSummary.Contains("PeakToValley=0.3", StringComparison.Ordinal)
            && invalidOperatorSummary.Contains("Rms=0.1", StringComparison.Ordinal)
            && invalidOperatorSummary.Contains("Fail (NG)", StringComparison.Ordinal)
            && invalidOperatorSummary.Contains("Bad reference plane", StringComparison.Ordinal)
            && invalidOperatorSummary.Contains("Insufficient valid ROI samples", StringComparison.Ordinal);

        var warningRecord = warpageRecord with
        {
            Status = ResultStatus.Warning,
            Message = "Reference-fit evidence is incomplete.",
            Steps =
            [
                warpageStep with
                {
                    Status = ResultStatus.Warning,
                    Message = "Reference fit RMS was not recorded.",
                    Metrics = warpageStep.Metrics
                        .Where(metric => !metric.Name.Equals("Rms", StringComparison.OrdinalIgnoreCase))
                        .ToArray()
                }
            ]
        };
        var warningPath = Path.Combine(root, "results-value-validity-warning.json");
        InspectionRunRecordJson.Write(warningPath, warningRecord);
        using var warningShell = CreateShell(root, "results-value-validity-warning", runRoot);
        var warningLoaded = warningShell.LoadRunRecord(warningPath, out var warningMessage);
        var warningSummary = warningShell.ResultsOperatorDecisionSummary;
        var warningPass = warningLoaded
            && warningSummary.Contains("Warning", StringComparison.Ordinal)
            && warningSummary.Contains("PeakToValley=0.3", StringComparison.Ordinal)
            && (warningSummary.Contains("Reference-fit RMS", StringComparison.Ordinal)
                || warningSummary.Contains("기준면 Fit RMS", StringComparison.Ordinal))
            && (warningSummary.Contains("Not recorded", StringComparison.Ordinal)
                || warningSummary.Contains("기록되지 않음", StringComparison.Ordinal))
            && warningSummary.Contains("Reference fit RMS was not recorded", StringComparison.Ordinal);

        var cancelledRecord = warpageRecord with
        {
            Status = ResultStatus.NotRun,
            Message = "Run was cancelled before measurement evidence was recorded.",
            Steps =
            [
                warpageStep with
                {
                    Status = ResultStatus.NotRun,
                    Message = "Run was cancelled before measurement evidence was recorded.",
                    Metrics = []
                }
            ]
        };
        var cancelledPath = Path.Combine(root, "results-value-validity-cancelled.json");
        InspectionRunRecordJson.Write(cancelledPath, cancelledRecord);
        using var cancelledShell = CreateShell(root, "results-value-validity-cancelled", runRoot);
        var cancelledLoaded = cancelledShell.LoadRunRecord(cancelledPath, out var cancelledMessage);
        var cancelledSummary = cancelledShell.ResultsOperatorDecisionSummary;
        var cancelledPass = cancelledLoaded
            && cancelledSummary.Contains("NotRun", StringComparison.Ordinal)
            && (cancelledSummary.Contains("Not recorded", StringComparison.Ordinal)
                || cancelledSummary.Contains("기록되지 않음", StringComparison.Ordinal))
            && cancelledSummary.Contains("Run was cancelled before measurement evidence was recorded", StringComparison.Ordinal);

        detail = $"warpageLoaded={warpageLoaded};warpagePass={warpagePass};warpageMessage={warpageMessage};"
            + $"invalidLoaded={invalidLoaded};invalidPass={invalidPass};invalidMessage={invalidMessage};"
            + $"warningLoaded={warningLoaded};warningPass={warningPass};warningMessage={warningMessage};"
            + $"cancelledLoaded={cancelledLoaded};cancelledPass={cancelledPass};cancelledMessage={cancelledMessage};"
            + $"warpage={warpageSummary.Replace(Environment.NewLine, " | ")};"
            + $"invalid={invalidSummary.Replace(Environment.NewLine, " | ")};"
            + $"warning={warningSummary.Replace(Environment.NewLine, " | ")};"
            + $"cancelled={cancelledSummary.Replace(Environment.NewLine, " | ")}";
        return warpagePass && invalidPass && warningPass && cancelledPass;
    }

    private static bool VerifyOtherRecordedMeasurements(string root, string runRoot, InspectionRunRecord? sourceRecord, out string detail)
    {
        if (sourceRecord?.Steps?.SingleOrDefault() is not { } sourceStep)
        {
            detail = "source Run Record step is unavailable";
            return false;
        }

        var cases = new[]
        {
            (ToolId: "plane-flatness", Primary: new[] { "Reference-plane deviation range", "Reference-plane absolute peak" }, Limits: new[] { "Flatness tolerance" }, Unit: "model"),
            (ToolId: "gap-flush", Primary: new[] { "Signed gap", "Signed flush" }, Limits: new[] { "Expected gap", "Gap tolerance", "Expected flush", "Flush tolerance" }, Unit: "model"),
            (ToolId: "volume", Primary: new[] { "Signed net volume", "Above-plane volume", "Below-plane volume" }, Limits: new[] { "Expected net volume", "Volume tolerance" }, Unit: "model^3"),
            (ToolId: "cross-section-dimensions", Primary: new[] { "Section width", "H range" }, Limits: Array.Empty<string>(), Unit: "model")
        };
        var results = new List<string>();
        var passed = true;
        foreach (var test in cases)
        {
            var metrics = test.Primary.Concat(test.Limits)
                .Select((name, index) => new InspectionRunMetric(name, MetricKind.Number, name.StartsWith("Signed", StringComparison.Ordinal) ? -1.25 : index + 0.25, test.Unit, ResultStatus.Pass)).ToArray();
            var measurementStep = sourceStep with
            {
                Id = "measurement." + test.ToolId,
                ToolId = test.ToolId,
                ToolName = test.ToolId,
                Metrics = metrics,
                AlgorithmEvidence = null
            };
            var preparationStep = sourceStep with
            {
                Id = "preparation",
                ToolId = "filter",
                ToolName = "Filter",
                Metrics = [new InspectionRunMetric("Mean", MetricKind.Number, 999, test.Unit, ResultStatus.Pass)]
            };
            var fixture = sourceRecord with { Steps = [preparationStep, measurementStep] };
            var path = Path.Combine(root, "results-recorded-" + test.ToolId + ".json");
            InspectionRunRecordJson.Write(path, fixture);
            var originalBytes = File.ReadAllBytes(path);
            using var shell = CreateShell(root, "results-recorded-" + test.ToolId, runRoot);
            var loaded = shell.LoadRunRecord(path, out var message);
            var summary = shell.ResultsOperatorDecisionSummary;
            var normalPass = loaded
                && metrics.All(metric => summary.Contains(FormatRecordedMetric(metric), StringComparison.Ordinal))
                && !summary.Contains("Mean=999", StringComparison.Ordinal)
                && originalBytes.SequenceEqual(File.ReadAllBytes(path));

            var invalidFixture = fixture with
            {
                Status = ResultStatus.Error,
                Message = "Measurement evidence is unavailable.",
                Steps = [preparationStep, measurementStep with
                {
                    Status = ResultStatus.Error,
                    Message = "Measurement evidence is unavailable.",
                    Metrics = [metrics[0] with { Value = double.NaN, Status = ResultStatus.Error }]
                }]
            };
            var invalidPath = Path.Combine(root, "results-recorded-" + test.ToolId + "-invalid.json");
            InspectionRunRecordJson.Write(invalidPath, invalidFixture);
            var invalidBytes = File.ReadAllBytes(invalidPath);
            var invalidLoaded = shell.LoadRunRecord(invalidPath, out var invalidMessage);
            var invalidSummary = shell.ResultsOperatorDecisionSummary;
            var invalidPass = invalidLoaded
                && invalidSummary.Contains("Error", StringComparison.Ordinal)
                && invalidSummary.Contains("Measurement evidence is unavailable.", StringComparison.Ordinal)
                && !invalidSummary.Contains("NaN", StringComparison.Ordinal)
                && !invalidSummary.Contains("Mean=999", StringComparison.Ordinal)
                && invalidSummary.Contains(test.Primary[0] + "=", StringComparison.Ordinal)
                && (invalidSummary.Contains("Unavailable", StringComparison.Ordinal) || invalidSummary.Contains("계산 불가", StringComparison.Ordinal))
                && invalidBytes.SequenceEqual(File.ReadAllBytes(invalidPath));
            passed &= normalPass && invalidPass;
            results.Add($"{test.ToolId}:normal={normalPass},invalid={invalidPass},load={message}/{invalidMessage}; summary={summary.Replace(Environment.NewLine, " | ")}; invalid={invalidSummary.Replace(Environment.NewLine, " | ")}");
        }

        detail = string.Join("; ", results);
        return passed;
    }

    private static bool VerifyRecordedMeasurementMeaning(string root, string runRoot, InspectionRunRecord? sourceRecord, out string detail)
    {
        if (sourceRecord?.Steps?.SingleOrDefault() is not { } sourceStep)
        {
            detail = "source Run Record step is unavailable";
            return false;
        }

        var results = new List<string>();
        var passed = true;
        var cases = new[]
        {
            (Id: "thickness", Tool: "thickness", Version: "height-measurement-execution-v1", Unit: "raw-height", Evidence: HeightMeasurementEvidence.RawHeight(), Token: "H-axis height difference", Korean: "H축 높이차"),
            (Id: "flatness", Tool: "plane-flatness", Version: "height-measurement-execution-v1", Unit: "mm", Evidence: HeightMeasurementEvidence.DeclaredUnit("Declared mm only"), Token: "not GD&T minimum-zone", Korean: "GD&T 최소영역"),
            (Id: "future", Tool: "thickness", Version: "future-definition-v2", Unit: "raw-height", Evidence: HeightMeasurementEvidence.RawHeight(), Token: "Measurement definition unavailable", Korean: "측정 방식 확인 불가"),
            (Id: "legacy", Tool: "thickness", Version: (string?)null, Unit: "raw-height", Evidence: HeightMeasurementEvidence.Unavailable(), Token: "Measurement definition unavailable", Korean: "측정 방식 확인 불가"),
            (Id: "declared", Tool: "thickness", Version: "height-measurement-execution-v1", Unit: "mm", Evidence: HeightMeasurementEvidence.DeclaredUnit("Declared mm only"), Token: "Declared input unit", Korean: "입력 단위만 선언됨"),
            (Id: "calibrated", Tool: "thickness", Version: "height-measurement-execution-v1", Unit: "mm", Evidence: HeightMeasurementEvidence.CalibratedPhysical("sensor.test", "calibration.recorded", "frame.test", "Saved calibration evidence"), Token: "Recorded input calibration", Korean: "기록된 입력 교정"),
            (Id: "unavailable", Tool: "thickness", Version: "height-measurement-execution-v1", Unit: "mm", Evidence: HeightMeasurementEvidence.Unavailable(), Token: "Input unit evidence unavailable", Korean: "입력 단위 근거 없음"),
            (Id: "volume-missing-unit", Tool: "volume", Version: "height-measurement-execution-v1", Unit: "mm", Evidence: HeightMeasurementEvidence.DeclaredUnit("Declared mm only"), Token: "Measurement units: Unknown", Korean: "측정 단위: Unknown"),
            (Id: "mixed-units", Tool: "gap-flush", Version: "height-measurement-execution-v1", Unit: "raw-height", Evidence: HeightMeasurementEvidence.RawHeight(), Token: "model, raw-height", Korean: "model, raw-height"),
            (Id: "error-unit", Tool: "volume", Version: "height-measurement-execution-v1", Unit: "mm", Evidence: HeightMeasurementEvidence.DeclaredUnit("Declared mm only"), Token: "Measurement units: mm^3", Korean: "측정 단위: mm^3"),
            (Id: "gap-definition", Tool: "gap-flush", Version: "height-measurement-execution-v1", Unit: "model", Evidence: HeightMeasurementEvidence.Unavailable(), Token: "ROI order", Korean: "ROI 순서"),
            (Id: "volume-definition", Tool: "volume", Version: "height-measurement-execution-v1", Unit: "model^3", Evidence: HeightMeasurementEvidence.Unavailable(), Token: "cell area", Korean: "셀 면적"),
            (Id: "section-definition", Tool: "cross-section-dimensions", Version: "height-measurement-execution-v1", Unit: "model", Evidence: HeightMeasurementEvidence.Unavailable(), Token: "not length along", Korean: "따라 잰 길이가 아닙니다"),
            (Id: "future-gap", Tool: "gap-flush", Version: "future-definition-v2", Unit: "model", Evidence: HeightMeasurementEvidence.Unavailable(), Token: "Measurement definition unavailable", Korean: "측정 방식 확인 불가")
        };
        foreach (var test in cases)
        {
            var metrics = test.Id switch
            {
                "volume-missing-unit" => new[] { new InspectionRunMetric("Signed net volume", MetricKind.Number, 2.5, "", ResultStatus.Pass) },
                "mixed-units" => [new InspectionRunMetric("Signed gap", MetricKind.Number, 2, "model", null), new InspectionRunMetric("Signed flush", MetricKind.Number, -1, "raw-height", null)],
                "error-unit" => [new InspectionRunMetric("Signed net volume", MetricKind.Number, double.NaN, "mm^3", ResultStatus.Error)],
                _ => [new InspectionRunMetric(test.Tool == "plane-flatness" ? "Reference-plane deviation range" : "Mean", MetricKind.Number, 2.5, test.Unit, ResultStatus.Pass)]
            };
            var fixture = sourceRecord with
            {
                Source = sourceRecord.Source with { Unit = test.Unit, FrameId = "frame.test", SensorId = "sensor.test", MeasurementEvidence = test.Evidence },
                SourceQualityEvidence = InspectionRunSourceQualityEvidence.Unavailable("Isolated presentation fixture"),
                Status = test.Id == "error-unit" ? ResultStatus.Error : ResultStatus.Pass,
                Steps = [sourceStep with
                {
                    ToolId = test.Tool,
                    ToolName = test.Tool,
                    Status = test.Id == "error-unit" ? ResultStatus.Error : ResultStatus.Pass,
                    Metrics = metrics,
                    AlgorithmEvidence = test.Version is null ? null : new InspectionRunAlgorithmEvidence(test.Version, "OpenVisionLab.Vision3D", "verification")
                }]
            };
            var path = Path.Combine(root, "measurement-meaning-" + test.Id + ".json");
            InspectionRunRecordJson.Write(path, fixture);
            var originalBytes = File.ReadAllBytes(path);
            using var shell = CreateShell(root, "measurement-meaning-" + test.Id, runRoot);
            var loaded = shell.LoadRunRecord(path, out var message);
            var summary = shell.ResultsValueValiditySummary;
            var definition = summary.Split(Environment.NewLine)[0];
            var unitBasis = summary.Split(Environment.NewLine).ElementAtOrDefault(1) ?? string.Empty;
            var correct = loaded
                && (summary.Contains(test.Token, StringComparison.Ordinal) || summary.Contains(test.Korean, StringComparison.Ordinal))
                && originalBytes.SequenceEqual(File.ReadAllBytes(path))
                && !shell.Workbench.HasOrderedRunResult;
            if (test.Id is "future" or "legacy")
                correct &= !definition.Contains("H-axis", StringComparison.Ordinal) && !definition.Contains("H축", StringComparison.Ordinal);
            if (test.Id == "flatness")
                correct &= definition.Contains("signed orthogonal", StringComparison.Ordinal) || definition.Contains("부호 있는 수직거리", StringComparison.Ordinal);
            if (test.Id is "declared" or "calibrated" or "unavailable")
                correct &= !unitBasis.Contains("raw-height", StringComparison.OrdinalIgnoreCase) && !unitBasis.Contains("원시", StringComparison.Ordinal);
            if (test.Id == "calibrated")
                correct &= unitBasis.Contains("calibration.recorded", StringComparison.Ordinal);
            if (test.Id == "error-unit")
                correct &= summary.Contains("Error", StringComparison.Ordinal) && !shell.ResultsOperatorDecisionSummary.Contains("NaN", StringComparison.Ordinal);
            passed &= correct;
            results.Add($"{test.Id}={correct}; load={message}; {definition}; {unitBasis}");
        }

        detail = string.Join(" | ", results);
        return passed;
    }

    private static bool VerifyMultipleMeasurementExplanation(string root, string runRoot, InspectionRunRecord? record, out string detail)
    {
        if (record?.Steps?.FirstOrDefault() is not { } template)
        {
            detail = "source record unavailable";
            return false;
        }
        var results = new List<string>();
        var passed = true;
        foreach (var scenario in new[] { (ResultStatus.Pass, false), (ResultStatus.Fail, false), (ResultStatus.Error, true), (ResultStatus.Pass, true) })
        {
            var (status, invalid) = scenario;
            var first = template with { Id = "recorded.first", Status = ResultStatus.Pass };
            var second = template with
            {
                Id = "recorded.second", Status = status,
                Metrics = [new InspectionRunMetric("Mean", MetricKind.Number, invalid ? double.PositiveInfinity : 12.5, "raw-height", status)],
                Overlays = [new InspectionRunOverlay("roi.reference", OverlayKind.Box, "Recorded reference ROI A", status, "input.old"),
                    new InspectionRunOverlay("roi.measurement", OverlayKind.Box, "Recorded measurement ROI B", status, "input.old")]
            };
            var fixture = record with { Status = status, Steps = [first, second] };
            var path = Path.Combine(root, $"multiple-measurement-{status}-{invalid}.json");
            InspectionRunRecordJson.Write(path, fixture);
            var bytes = File.ReadAllBytes(path);
            using var shell = CreateShell(root, "multiple-measurement-" + status, runRoot);
            var loaded = shell.LoadRunRecord(path, out var message);
            var summary = shell.ResultsOperatorDecisionSummary;
            var validity = shell.ResultsValueValiditySummary;
            var expectedId = status == ResultStatus.Pass && !invalid ? first.Id : second.Id;
            var correct = loaded && summary.Contains(expectedId, StringComparison.Ordinal)
                && validity.Contains(expectedId, StringComparison.Ordinal)
                && bytes.SequenceEqual(File.ReadAllBytes(path)) && !shell.Workbench.HasOrderedRunResult;
            if (status != ResultStatus.Pass || invalid)
                correct &= validity.Contains("Recorded reference ROI A", StringComparison.Ordinal)
                    && validity.Contains("Recorded measurement ROI B", StringComparison.Ordinal);
            if (invalid)
                correct &= summary.Contains("Error", StringComparison.Ordinal) && !summary.Contains("Infinity", StringComparison.Ordinal) && !summary.Contains("Mean=0", StringComparison.Ordinal)
                    && (summary.Contains("Unavailable", StringComparison.Ordinal) || summary.Contains("계산 불가", StringComparison.Ordinal));
            passed &= correct;
            results.Add($"{status}={correct}; load={message}; {summary.Replace(Environment.NewLine, " | ")}");
        }
        detail = string.Join("; ", results);
        return passed;
    }

    private static InspectionRunRecord? ReadRecord(string? path) =>
        string.IsNullOrWhiteSpace(path) || !File.Exists(path)
            ? null
            : JsonSerializer.Deserialize<InspectionRunRecord>(
                File.ReadAllText(path),
                JsonOptions);

    private static string FormatRecordedMetric(InspectionRunMetric metric) =>
        string.IsNullOrWhiteSpace(metric.Unit)
            ? $"{metric.Name}={metric.Value:G6}"
            : $"{metric.Name}={metric.Value:G6} {metric.Unit}";
}
