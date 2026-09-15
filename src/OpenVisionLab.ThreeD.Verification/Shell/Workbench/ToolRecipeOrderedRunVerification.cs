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
            var passDocument = CreateThicknessDocument(sourcePath, 4.5, 5.5);
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
            var fullRunCount = 0;
            shell.Workbench.OrderedRunCompleted += (_, _) => fullRunCount++;
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
            var valueValiditySummary = reopenedShell.ResultsValueValiditySummary;
            var validSampleMetric = record?.Steps?.SingleOrDefault()?.Metrics.SingleOrDefault(metric =>
                metric.Name.Equals("ValidSampleCount", StringComparison.OrdinalIgnoreCase));
            Check(
                "Results explains stored definition, raw-height unit basis, ROI, valid count, and unavailable residual without recomputation",
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
                    || valueValiditySummary.Contains("물리 보정", StringComparison.Ordinal))
                && (valueValiditySummary.Contains("Fit residual", StringComparison.Ordinal)
                    || valueValiditySummary.Contains("Unknown", StringComparison.Ordinal)),
                valueValiditySummary.Replace(Environment.NewLine, " | "));
            var valueValidityScenarios = VerifyResultsValueValidityScenarios(
                root,
                runRoot,
                record,
                out var valueValidityScenarioDetail);
            Check(
                "Results keeps Warpage residuals, unknown units, and failure reasons distinguishable",
                valueValidityScenarios,
                valueValidityScenarioDetail);
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
            () => { });
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
            && cancelCommandWasEnabled;

        using var commandFailureObserved = new ManualResetEventSlim();
        var commandFailureOwner = new ToolWorkbenchOrderedRunExecutionOwner(
            () => document,
            _ => (true, "ready"),
            () => recipePath,
            () => string.Empty,
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
            () => { });
        var commandWasEnabled = commandFailureOwner.RunCommand.CanExecute(null);
        commandFailureOwner.RunCommand.Execute(null);
        var commandFailureWasObserved = commandFailureObserved.Wait(TimeSpan.FromSeconds(5))
            && !commandFailureOwner.IsRunning
            && commandFailureOwner.Summary.Contains("failed", StringComparison.OrdinalIgnoreCase);
        commandFailureOwner.Dispose();

        detail = $"singleFlight={singleFlight};capabilityCalls={capabilityCalls};"
            + $"cancellation={cancellationWorked};commandEnabled={commandWasEnabled};"
            + $"commandFailureObserved={commandFailureWasObserved};completionCount={completionCount};"
            + $"summary={cancellationOwner.Summary}";
        return singleFlight && cancellationWorked && commandWasEnabled && commandFailureWasObserved;
    }

    private static ShellMainWindowViewModel CreateShell(
        string root,
        string name,
        string runRoot) =>
        new(
            recentRunRecordsPath: Path.Combine(root, $"recent-runs-{name}.json"),
            recentRecipesPath: Path.Combine(root, $"recent-recipes-{name}.json"),
            orderedRunRecordRoot: runRoot);

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
        detail = $"opened=1;crop={cropOutput.Width}x{cropOutput.Height}@{cropOutput.GridOriginColumn},{cropOutput.GridOriginRow};"
            + $"runCompleted={runCompleted};uiRecord={recordPath};headless={headless.Status};{parityDetail}";
        return runCompleted && parity;
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
        var passed = runCompleted
            && headless.Status == ResultStatus.Fail
            && record?.Status == ResultStatus.Fail
            && headlessStep?.Result.Status == ResultStatus.Fail
            && uiStep?.Status == ResultStatus.Fail
            && headless.Status is not ResultStatus.Error
            && record.Status is not ResultStatus.Error;
        detail = $"quality={sourceQuality is not null};runCompleted={runCompleted};"
            + $"ui={record?.Status};headless={headless.Status};uiStep={uiStep?.Status};"
            + $"headlessStep={headlessStep?.Result.Status};message={headless.Message}";
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
        var failedToPersist = shell.Workbench.CurrentOrderedRunRecordPath is null
            && shell.Workbench.HasOrderedRunResult
            && shell.StatusText.Contains("Run Record", StringComparison.OrdinalIgnoreCase)
            && shell.ResultsValueValiditySummary.Contains(
                "Run Record",
                StringComparison.OrdinalIgnoreCase)
            && (shell.ResultsValueValiditySummary.Contains("Error", StringComparison.Ordinal)
                || shell.ResultsValueValiditySummary.Contains("오류", StringComparison.Ordinal));
        detail = $"runCompleted={runCompleted};record={(shell.Workbench.CurrentOrderedRunRecordPath ?? "(none)")};"
            + $"result={shell.Workbench.HasOrderedRunResult};status={shell.StatusText}";
        return runCompleted && failedToPersist;
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
        var warpagePass = warpageLoaded
            && warpageSummary.Contains("PeakToValley=0.3", StringComparison.Ordinal)
            && warpageSummary.Contains("Rms=0.1", StringComparison.Ordinal)
            && warpageSummary.Contains("ValidSampleCount=8", StringComparison.Ordinal)
            && warpageSummary.Contains("best-fit inspection ROI", StringComparison.OrdinalIgnoreCase)
            && warpageSummary.Contains("Pass", StringComparison.Ordinal);

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
        var invalidPass = invalidLoaded
            && invalidSummary.Contains("Unknown", StringComparison.Ordinal)
            && invalidSummary.Contains("Fail (NG)", StringComparison.Ordinal)
            && invalidSummary.Contains("Bad reference plane", StringComparison.Ordinal)
            && invalidSummary.Contains("Insufficient valid ROI samples", StringComparison.Ordinal);

        detail = $"warpageLoaded={warpageLoaded};warpagePass={warpagePass};warpageMessage={warpageMessage};"
            + $"invalidLoaded={invalidLoaded};invalidPass={invalidPass};invalidMessage={invalidMessage};"
            + $"warpage={warpageSummary.Replace(Environment.NewLine, " | ")};"
            + $"invalid={invalidSummary.Replace(Environment.NewLine, " | ")}";
        return warpagePass && invalidPass;
    }

    private static InspectionRunRecord? ReadRecord(string? path) =>
        string.IsNullOrWhiteSpace(path) || !File.Exists(path)
            ? null
            : JsonSerializer.Deserialize<InspectionRunRecord>(
                File.ReadAllText(path),
                JsonOptions);
}
