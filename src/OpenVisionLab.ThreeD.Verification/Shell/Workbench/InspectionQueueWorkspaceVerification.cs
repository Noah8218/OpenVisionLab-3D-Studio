using System.IO;
using System.Security.Cryptography;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Verification.Shell.Workbench;

internal static class InspectionQueueWorkspaceVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var fullReportPath = Path.GetFullPath(reportPath);
        var reportDirectory = Path.GetDirectoryName(fullReportPath)
            ?? throw new InvalidOperationException("Inspection Queue verification report has no directory.");
        var lines = new List<string>
        {
            "OpenVisionLab 3D inspection queue state-transition verification",
            $"Generated: {DateTimeOffset.Now:O}"
        };
        var passed = 0;
        var total = 0;
        void Check(string name, bool condition, string detail)
        {
            total++;
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
            if (condition) passed++;
        }

        try
        {
            Directory.CreateDirectory(reportDirectory);
            var root = Path.Combine(reportDirectory, "inspection-queue-fixture", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var recipePath = Path.Combine(root, "saved-recipe.ov3d-recipe.json");
            var document = CreateDocument(Path.Combine(root, "teaching-source.C3D"));
            ToolRecipeDocumentStore.Save(recipePath, document);
            var sourceA = CreateInput(root, "input-a.C3D");
            var sourceB = CreateInput(root, "input-b.c3d");

            using (var missingRecipeQueue = new InspectionQueueWorkspaceViewModel(
                () => document,
                () => null,
                _ => (true, "ready"),
                (_, _) => (true, "compatible"),
                (_, _, _) => Task.FromResult(new InspectionQueueRunOutcome(false, null, null, "unexpected run")),
                () => { },
                Localize))
            {
                missingRecipeQueue.AddSources([sourceA]);
                missingRecipeQueue.SelectedSample = missingRecipeQueue.Samples[0];
                Check(
                    "saved recipe required for run readiness",
                    !missingRecipeQueue.RunSelectedCommand.CanExecute(null)
                    && !missingRecipeQueue.RunAllCommand.CanExecute(null)
                    && missingRecipeQueue.SavedRecipeText == "저장된 레시피 없음",
                    $"selected={missingRecipeQueue.RunSelectedCommand.CanExecute(null)};all={missingRecipeQueue.RunAllCommand.CanExecute(null)};recipe={missingRecipeQueue.SavedRecipeText}");
            }

            var runCount = 0;
            var recordIndex = 0;
            var failNextRun = false;
            TaskCompletionSource<InspectionQueueRunOutcome>? pendingRun = null;
            TaskCompletionSource<bool>? runEntered = null;
            string? pendingInputPath = null;
            using var queue = new InspectionQueueWorkspaceViewModel(
                () => document,
                () => recipePath,
                _ => (true, "ready"),
                (_, _) => (true, "compatible"),
                async (_, selectedRecipePath, inputPath) =>
                {
                    Interlocked.Increment(ref runCount);
                    if (pendingRun is not null && (pendingInputPath is null || inputPath == pendingInputPath))
                    {
                        runEntered?.TrySetResult(true);
                        return await pendingRun.Task;
                    }
                    if (failNextRun)
                    {
                        failNextRun = false;
                        throw new InvalidOperationException("Injected isolated verification failure.");
                    }

                    var outcome = CreateOutcome(root, document, selectedRecipePath, inputPath, Interlocked.Increment(ref recordIndex));
                    return outcome;
                },
                () => pendingRun?.TrySetResult(new InspectionQueueRunOutcome(true, null, null, "cancelled")),
                Localize);

            queue.AddSources([sourceA, sourceB, sourceA]);
            queue.SelectedSample = queue.Samples[0];
            queue.SelectedSample.IsChecked = false;
            queue.Samples[1].IsChecked = true;
            Check(
                "selection, check state, and duplicate-path handling are independent",
                queue.Samples.Count == 2
                && queue.SelectedSample.SourcePath == sourceA
                && !queue.SelectedSample.IsChecked
                && queue.CheckedCount == 1
                && queue.Samples[1].IsChecked
                && runCount == 0,
                $"items={queue.Samples.Count};selected={queue.SelectedSample.FileName};selectedChecked={queue.SelectedSample.IsChecked};checked={queue.CheckedCount};runs={runCount};summary={queue.SummaryText}");

            Check("checked command label names its exact scope", queue.RunCheckedLabel == "체크한 1개 검사", queue.RunCheckedLabel);
            queue.RunSelectedCommand.Execute(null);
            awaitIdle(queue);
            var selectedInputRecord = queue.Samples[0].Record;
            Check(
                "selected unchecked input runs only after explicit selected Run",
                runCount == 1
                && queue.Samples[0].State == InspectionQueueItemState.Pass
                && selectedInputRecord?.Source.Path == sourceA
                && queue.Samples[1].State == InspectionQueueItemState.Untested,
                $"runs={runCount};A={queue.Samples[0].State};recordSource={selectedInputRecord?.Source.Path};B={queue.Samples[1].State}");

            pendingRun = new TaskCompletionSource<InspectionQueueRunOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
            runEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            queue.RunCheckedCommand.Execute(null);
            runEntered.Task.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            queue.RunCheckedCommand.Execute(null);
            var duplicateBlocked = Volatile.Read(ref runCount) == 2 && queue.IsRunning;
            queue.StopCommand.Execute(null);
            awaitIdle(queue);
            pendingRun = null;
            runEntered = null;
            Check(
                "repeated run click starts one input and cancellation leaves it retryable",
                duplicateBlocked
                && queue.Samples[1].State == InspectionQueueItemState.Cancelled
                && queue.RunRemainingCommand.CanExecute(null)
                && Volatile.Read(ref runCount) == 2,
                $"duplicateBlocked={duplicateBlocked};B={queue.Samples[1].State};remainingEnabled={queue.RunRemainingCommand.CanExecute(null)};runs={runCount};summary={queue.SummaryText}");

            queue.RunRemainingCommand.Execute(null);
            awaitIdle(queue);
            var recoveredInput = queue.Samples[1];
            Check(
                "cancelled input retries through the existing saved-recipe runner",
                Volatile.Read(ref runCount) == 3
                && recoveredInput.State == InspectionQueueItemState.Pass
                && recoveredInput.Record?.Source.Path == sourceB
                && recoveredInput.Steps.Count == 1
                && recoveredInput.CanOpenCurrentRunRecord,
                $"runs={runCount};B={recoveredInput.State};recordSource={recoveredInput.Record?.Source.Path};steps={recoveredInput.Steps.Count};recordExists={recoveredInput.CanOpenCurrentRunRecord}");

            failNextRun = true;
            queue.SelectedSample = queue.Samples[0];
            queue.RunSelectedCommand.Execute(null);
            awaitIdle(queue);
            var failedCanRetry = queue.Samples[0].State == InspectionQueueItemState.Error
                && queue.RunSelectedCommand.CanExecute(null);
            queue.RunSelectedCommand.Execute(null);
            awaitIdle(queue);
            Check(
                "failed input remains distinguishable and can recover on retry",
                failedCanRetry
                && Volatile.Read(ref runCount) == 5
                && queue.Samples[0].State == InspectionQueueItemState.Pass
                && queue.Samples[0].Record?.Source.Path == sourceA,
                $"failedCanRetry={failedCanRetry};runs={runCount};A={queue.Samples[0].State};message={queue.Samples[0].Message}");

            var filterRecord = queue.SelectedSample!.Record!;
            var failStep = filterRecord.Steps![0] with { Id = "failed-step", Status = ResultStatus.Fail };
            queue.SelectedSample.SetRecord(filterRecord with { Steps = [filterRecord.Steps[0], failStep] }, queue.SelectedSample.RunRecordPath!);
            queue.ShowOnlyFailedSteps = true;
            Check("NG item filter keeps file selection and executes nothing",
                queue.VisibleStepResults.Count == 1 && queue.VisibleStepResults[0].StepId == "failed-step"
                && queue.SelectedSample.SourcePath == sourceA && runCount == 5,
                "measurement result filter is independent from file filter");
            queue.ShowOnlyFailedSteps = false;
            Check("clearing NG filter restores all recorded steps", queue.VisibleStepResults.Count == 2, "same record, no rerun");

            var retainedA = queue.Samples[0].Record;
            foreach (var item in queue.Samples) item.IsChecked = false;
            queue.RunCheckedCommand.Execute(null);
            Check("zero checked inputs refuse Run without replacing completed evidence",
                !queue.RunCheckedCommand.CanExecute(null) && queue.RunCheckedLabel == "체크한 0개 검사"
                && !queue.IsRunning && runCount == 5 && ReferenceEquals(queue.Samples[0].Record, retainedA),
                $"runs={runCount};label={queue.RunCheckedLabel};running={queue.IsRunning}");

            queue.Samples[1].IsChecked = true;
            queue.SearchText = "input-a";
            queue.SelectedSample = queue.Samples[0];
            var previousB = queue.Samples[1].RunRecordPath;
            queue.RunCheckedCommand.Execute(null);
            awaitIdle(queue);
            Check("checked scope reruns a completed hidden input independently of the visible selected row",
                queue.VisibleSamples.Count == 1 && queue.VisibleSamples[0].SourcePath == sourceA
                && queue.CheckedCount == 1 && runCount == 6 && queue.Samples[1].Record?.Source.Path == sourceB
                && queue.Samples[1].PreviousRunRecordPath == previousB
                && queue.Samples[1].RunRecordPath != previousB && ReferenceEquals(queue.Samples[0].Record, retainedA),
                $"runs={runCount};visible={queue.VisibleSamples.Count};checked={queue.CheckedCount};B={queue.Samples[1].State}");

            queue.SearchText = string.Empty;
            foreach (var item in queue.Samples) item.IsChecked = true;
            pendingInputPath = sourceB;
            pendingRun = new TaskCompletionSource<InspectionQueueRunOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
            runEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            queue.RunCheckedCommand.Execute(null);
            runEntered.Task.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            var completedBeforeStop = queue.Samples[0].Record;
            queue.StopCommand.Execute(null);
            awaitIdle(queue);
            pendingRun = null;
            pendingInputPath = null;
            runEntered = null;
            Check("stopping a later input preserves the completed input from the same batch",
                runCount == 8 && completedBeforeStop?.Source.Path == sourceA
                && ReferenceEquals(queue.Samples[0].Record, completedBeforeStop)
                && queue.Samples[0].State == InspectionQueueItemState.Pass
                && queue.Samples[1].State == InspectionQueueItemState.Cancelled,
                $"runs={runCount};A={queue.Samples[0].State};B={queue.Samples[1].State}");

            queue.RunRemainingCommand.Execute(null);
            awaitIdle(queue);
            Check("remaining scope recovers only the cancelled input and leaves completed evidence untouched",
                runCount == 9 && queue.Samples[1].State == InspectionQueueItemState.Pass
                && queue.Samples[1].Record?.Source.Path == sourceB
                && ReferenceEquals(queue.Samples[0].Record, completedBeforeStop),
                $"runs={runCount};A={queue.Samples[0].State};B={queue.Samples[1].State}");

            var recordedStep = completedBeforeStop!.Steps![0] with
            {
                RecipeIndex = 1,
                ToolId = "thickness",
                ToolName = "C3D Thickness",
                Status = ResultStatus.Fail,
                Message = "Thickness is outside the configured limits.",
                Metrics =
                [
                    new InspectionRunMetric("Count", MetricKind.Count, 100, "cells", null),
                    new InspectionRunMetric("LowerLimit", MetricKind.Length, 7.75, "raw-height", null),
                    new InspectionRunMetric("UpperLimit", MetricKind.Length, 8.25, "raw-height", null),
                    new InspectionRunMetric("Mean", MetricKind.Length, 8.250000000000002, "raw-height", ResultStatus.Fail)
                ]
            };
            var authoredStep = new ToolRecipeStep(recordedStep.Id, "thickness", "Pad 2", 1, ["source"], recordedStep.OutputEntityId, []);
            var row = InspectionQueueStepResultItem.FromRecord(recordedStep, authoredStep);
            Check("inspection row identifies the authored step and preserves near-limit NG precision and unit",
                row.ToolName == "Pad 2" && row.DisplayOrder == 2 && row.Status == "Fail"
                && row.Value == $"Mean{Environment.NewLine}8.250000000000002" && row.Unit == "raw-height"
                && row.Limits == "7.75–8.25 raw-height" && row.Metrics.Contains("Count: 100 cells", StringComparison.Ordinal)
                && recordedStep.ToolName == "C3D Thickness",
                $"name={row.ToolName};value={row.Value.Replace(Environment.NewLine, " ")};unit={row.Unit};limits={row.Limits}");

            var snapshot = document with { Steps = [authoredStep] };
            queue.Samples[0].SetRecord(completedBeforeStop with { Steps = [recordedStep] }, queue.Samples[0].RunRecordPath!, snapshot);
            snapshot = snapshot with { Steps = [authoredStep with { ToolName = "Edited after execution" }] };
            Check("completed inspection row keeps its execution-time name after recipe editing",
                queue.Samples[0].Steps[0].ToolName == "Pad 2" && snapshot.Steps[0].ToolName == "Edited after execution"
                && queue.Samples[0].Record!.Steps![0].ToolName == "C3D Thickness",
                $"recorded={queue.Samples[0].Steps[0].ToolName};edited={snapshot.Steps[0].ToolName}");

            queue.SelectedSample = queue.Samples[0];
            queue.SelectedStepResult = queue.Samples[0].Steps[0];
            Check("result-row selection retains the selected input and does not execute",
                queue.SelectedStepResult?.StepId == recordedStep.Id && ReferenceEquals(queue.SelectedSample, queue.Samples[0])
                && !queue.IsRunning && runCount == 9,
                $"step={queue.SelectedStepResult?.StepId};input={queue.SelectedSample?.FileName};runs={runCount}");
            queue.SelectedSample = queue.Samples[1];
            queue.SelectedStepResult = queue.Samples[0].Steps[0];
            Check("switching inputs clears result selection and rejects a row from another input",
                queue.SelectedStepResult is null && ReferenceEquals(queue.SelectedSample, queue.Samples[1]) && runCount == 9,
                $"selectedResult={queue.SelectedStepResult?.StepId ?? "none"};input={queue.SelectedSample?.FileName};runs={runCount}");

            var legacyRow = InspectionQueueStepResultItem.FromRecord(recordedStep);
            Check("legacy inspection record falls back to SDK name while retaining recorded unit and limits",
                legacyRow.ToolName == "C3D Thickness" && legacyRow.Unit == "raw-height"
                && legacyRow.Limits == "7.75–8.25 raw-height" && legacyRow.Value == row.Value,
                $"name={legacyRow.ToolName};unit={legacyRow.Unit};limits={legacyRow.Limits}");

            ToolRecipeParameter[] volumeParameters = [new("ExpectedNetVolume", "12"), new("VolumeTolerance", "0.25")];
            var volumeStep = recordedStep with
            {
                ToolId = "volume",
                Metrics = [new("Count", MetricKind.Count, 100, "cells", null), new("Signed net volume", MetricKind.Volume, 10.75, "model^3", ResultStatus.Fail)]
            };
            var volumeRow = InspectionQueueStepResultItem.FromRecord(volumeStep, authoredStep with { ToolId = "volume", Parameters = volumeParameters });
            volumeParameters[0] = new("ExpectedNetVolume", "999");
            Check("non-length inspection row selects the volume metric and freezes execution limits",
                volumeRow.Value == $"Signed net volume{Environment.NewLine}10.75" && volumeRow.Unit == "model^3"
                && volumeRow.Limits == "ExpectedNetVolume: 12; VolumeTolerance: 0.25",
                $"value={volumeRow.Value.Replace(Environment.NewLine, " ")};unit={volumeRow.Unit};limits={volumeRow.Limits}");

            var unjudgedRow = InspectionQueueStepResultItem.FromRecord(recordedStep with
            {
                Status = ResultStatus.NotRun,
                Message = ToolRecipeStep.UnconfiguredAcceptanceMessage
            }, authoredStep);
            Check("unconfigured acceptance stays unjudged in the inspection row",
                unjudgedRow.Status == "NotRun" && unjudgedRow.StatusLabel is "미판정" or "Unjudged"
                && unjudgedRow.Limits is "규격 미설정" or "Limits not configured"
                && unjudgedRow.Unit == "raw-height" && unjudgedRow.Value == row.Value,
                $"status={unjudgedRow.StatusLabel};limits={unjudgedRow.Limits}");

            var englishPresentation = false;
            using var languageQueue = new InspectionQueueWorkspaceViewModel(
                () => document, () => recipePath, _ => (true, "ready"), (_, _) => (true, "compatible"),
                (_, _, _) => throw new InvalidOperationException("Language restoration must not execute inspection."),
                () => { }, (korean, english) => englishPresentation ? english : korean);
            languageQueue.AddSources([sourceA]);
            languageQueue.SelectedSample = languageQueue.Samples[0];
            var languageInput = languageQueue.SelectedSample;
            var executionNotifications = 0;
            languageQueue.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(InspectionQueueWorkspaceViewModel.IsRunning)) executionNotifications++;
            };
            englishPresentation = true;
            languageQueue.RefreshResultPresentation();
            var restoredEnglish = languageQueue.RunCheckedLabel == "Inspect 1 checked"
                && languageQueue.SummaryText.StartsWith("Added 1 file(s)", StringComparison.Ordinal)
                && languageQueue.SelectedSample!.StateText == "Untested";
            englishPresentation = false;
            languageQueue.RefreshResultPresentation();
            Check("language round trip refreshes queue labels and summary without execution or input mutation",
                restoredEnglish && languageQueue.RunCheckedLabel == "체크한 1개 검사"
                && languageQueue.SummaryText.StartsWith("파일 1개 추가", StringComparison.Ordinal)
                && ReferenceEquals(languageInput, languageQueue.SelectedSample) && !languageQueue.IsRunning
                && executionNotifications == 0,
                $"English={restoredEnglish};label={languageQueue.RunCheckedLabel};executionNotifications={executionNotifications}");
            var completedOutcome = CreateOutcome(root, document, recipePath, sourceA, 999);
            var completedRecord = completedOutcome.Record! with { Message = "Executed 1 authored typed step(s) in recipe order." };
            languageInput!.SetRecord(completedRecord, completedOutcome.RecordPath!, document);
            var koreanStatus = languageInput.StatusSummary;
            executionNotifications = 0;
            englishPresentation = true;
            languageQueue.RefreshResultPresentation();
            Check("normal completion summary localizes without rewriting the SDK record or executing",
                koreanStatus == "통과 · 1단계 검사 완료" && languageInput.StatusSummary == "Pass · 1 step inspected"
                && completedRecord.Message == "Executed 1 authored typed step(s) in recipe order." && !languageQueue.IsRunning && executionNotifications == 0,
                $"Korean={koreanStatus};English={languageInput.StatusSummary};raw={completedRecord.Message}");

            var sourceC = CreateInput(root, "input-c.c3d");
            var batchRuns = 0;
            var saveAttempts = 0;
            InspectionQueueRunOutcome? pendingSave = null;
            using var saveQueue = new InspectionQueueWorkspaceViewModel(
                () => document, () => recipePath, _ => (true, "ready"), (_, _) => (true, "compatible"),
                (_, savedPath, inputPath) =>
                {
                    batchRuns++;
                    var calculated = CreateOutcome(root, document, savedPath, inputPath, 1000 + batchRuns);
                    if (inputPath == sourceB && pendingSave is null)
                    {
                        pendingSave = calculated;
                        return Task.FromResult(calculated with { RecordPath = null, Message = "injected record save failure", RecordSaveFailed = true });
                    }
                    return Task.FromResult(calculated);
                }, () => { }, Localize,
                () => ++saveAttempts == 1
                    ? pendingSave! with { RecordPath = null, Message = "save retry still blocked", RecordSaveFailed = true }
                    : pendingSave!);
            saveQueue.AddSources([sourceA, sourceB, sourceC]);
            saveQueue.RunAllCommand.Execute(null);
            awaitIdle(saveQueue);
            var keptA = saveQueue.Samples[0].Record;
            var pendingB = saveQueue.Samples[1].Record;
            Check("record save failure stops the batch and preserves calculated steps",
                batchRuns == 2 && saveQueue.HasPendingRecordSave && pendingB is not null
                && saveQueue.Samples[1].Steps.Count == 1 && saveQueue.Samples[1].State == InspectionQueueItemState.Error
                && saveQueue.Samples[2].State == InspectionQueueItemState.Untested,
                $"runs={batchRuns};pending={saveQueue.HasPendingRecordSave};steps={saveQueue.Samples[1].Steps.Count};C={saveQueue.Samples[2].State}");
            foreach (var command in new[] { saveQueue.RunSelectedCommand, saveQueue.RunAllCommand, saveQueue.RunCheckedCommand, saveQueue.RunRemainingCommand, saveQueue.ClearQueueCommand, saveQueue.RemoveSelectedCommand, saveQueue.NewInspectionContextCommand })
            {
                command.Execute(null);
            }
            Check("pending save rejects all run and destructive queue entry points",
                batchRuns == 2 && saveQueue.Samples.Count == 3 && ReferenceEquals(pendingB, saveQueue.Samples[1].Record)
                && !saveQueue.RunRemainingCommand.CanExecute(null) && !saveQueue.ClearQueueCommand.CanExecute(null),
                $"runs={batchRuns};items={saveQueue.Samples.Count};pending={saveQueue.HasPendingRecordSave}");
            saveQueue.RetryRecordSaveCommand.Execute(null);
            Check("failed save retry preserves the same calculation without execution",
                saveAttempts == 1 && batchRuns == 2 && saveQueue.HasPendingRecordSave && ReferenceEquals(pendingB, saveQueue.Samples[1].Record),
                $"saveAttempts={saveAttempts};runs={batchRuns};runId={saveQueue.Samples[1].Record?.RunId}");
            saveQueue.RetryRecordSaveCommand.Execute(null);
            Check("successful save retry retains identity and never resumes inspection implicitly",
                saveAttempts == 2 && batchRuns == 2 && !saveQueue.HasPendingRecordSave
                && ReferenceEquals(pendingB, saveQueue.Samples[1].Record) && saveQueue.Samples[1].CanOpenCurrentRunRecord
                && saveQueue.Samples[2].State == InspectionQueueItemState.Untested,
                $"saveAttempts={saveAttempts};runs={batchRuns};C={saveQueue.Samples[2].State}");
            saveQueue.RunRemainingCommand.Execute(null);
            awaitIdle(saveQueue);
            Check("explicit remaining run skips saved results after record recovery",
                batchRuns == 3 && ReferenceEquals(keptA, saveQueue.Samples[0].Record)
                && ReferenceEquals(pendingB, saveQueue.Samples[1].Record) && saveQueue.Samples[2].State == InspectionQueueItemState.Pass,
                $"runs={batchRuns};A={saveQueue.Samples[0].Record?.RunId};B={saveQueue.Samples[1].Record?.RunId};C={saveQueue.Samples[2].State}");

            var originalRecipeBytes = File.ReadAllBytes(recipePath);
            File.AppendAllText(recipePath, " ");
            saveQueue.RefreshCommands();
            foreach (var command in new[] { saveQueue.RunSelectedCommand, saveQueue.RunAllCommand, saveQueue.RunCheckedCommand, saveQueue.RunRemainingCommand }) command.Execute(null);
            Check("changed saved recipe rejects all run scopes without altering previous evidence",
                batchRuns == 3 && !saveQueue.RunAllCommand.CanExecute(null) && ReferenceEquals(keptA, saveQueue.Samples[0].Record)
                && saveQueue.RecipeReadinessText.Contains("바뀌었습니다", StringComparison.Ordinal),
                $"runs={batchRuns};ready={saveQueue.RecipeReadinessText}");
            File.WriteAllBytes(recipePath, originalRecipeBytes);
            Check("byte-identical recipe restoration permits the same inspection context",
                saveQueue.RunAllCommand.CanExecute(null) && batchRuns == 3,
                $"enabled={saveQueue.RunAllCommand.CanExecute(null)};runs={batchRuns}");
            File.AppendAllText(recipePath, " ");
            var previousAPath = saveQueue.Samples[0].RunRecordPath;
            saveQueue.NewInspectionContextCommand.Execute(null);
            Check("explicit new inspection context retains previous records and executes nothing",
                batchRuns == 3 && saveQueue.Samples.All(item => item.State == InspectionQueueItemState.Untested)
                && saveQueue.Samples[0].PreviousRunRecordPath == previousAPath && File.Exists(previousAPath)
                && saveQueue.RunAllCommand.CanExecute(null),
                $"runs={batchRuns};previous={saveQueue.Samples[0].PreviousRunRecordPath};ready={saveQueue.RunAllCommand.CanExecute(null)}");
            File.WriteAllBytes(recipePath, originalRecipeBytes);
        }
        catch (Exception exception)
        {
            Check("verification execution", false, exception.GetBaseException().Message);
        }

        lines.Add($"Result={(passed == total ? "PASS" : "FAIL")}|{passed}/{total}");
        File.WriteAllLines(fullReportPath, lines);
        summary = lines[^1];
        foreach (var line in lines) Console.WriteLine(line);
        return passed == total;
    }

    private static ToolRecipeDocument CreateDocument(string sourcePath) => new(
        ToolRecipeDocument.CurrentSchemaVersion,
        "Queue verification recipe",
        new ToolRecipeSource("source", "Source", "C3D", "mm", "grid", sourcePath, GridWidth: 4, GridHeight: 4),
        [],
        []);

    private static string CreateInput(string root, string name)
    {
        var path = Path.Combine(root, name);
        File.WriteAllText(path, name);
        return path;
    }

    private static InspectionQueueRunOutcome CreateOutcome(
        string root,
        ToolRecipeDocument document,
        string recipePath,
        string inputPath,
        int recordIndex)
    {
        var recipeHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(recipePath))).ToLowerInvariant();
        var inputBytes = File.ReadAllBytes(inputPath);
        var inputHash = Convert.ToHexString(SHA256.HashData(inputBytes)).ToLowerInvariant();
        var recordPath = Path.Combine(root, $"run-{recordIndex}.json");
        File.WriteAllText(recordPath, "verification-only record artifact");
        var record = new InspectionRunRecord(
            InspectionRunRecord.CurrentSchemaVersion,
            $"verification-run-{recordIndex}",
            DateTimeOffset.UtcNow,
            new InspectionRunRecipe("OpenVisionLab.ThreeD", document.SchemaVersion, recipePath, recipeHash),
            new InspectionRunSource("source", inputPath, inputHash, inputBytes.Length, "mm"),
            "Thickness",
            ResultStatus.Pass,
            "passed",
            3.5,
            [],
            [],
            "matched",
            new InspectionRunArtifacts("", null, null, recordPath, null, null))
        {
            Steps =
            [
                new InspectionRunStepResult(
                    0,
                    "thickness-1",
                    "thickness-1",
                    "Thickness",
                    ["source"],
                    "measurement-1",
                    ResultStatus.Pass,
                    "passed",
                    3.5,
                    [],
                    [])
            ]
        };
        return new InspectionQueueRunOutcome(false, record, recordPath, "passed");
    }

    private static void awaitIdle(InspectionQueueWorkspaceViewModel queue)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (queue.IsRunning && DateTimeOffset.UtcNow < deadline) Thread.Sleep(10);
        if (queue.IsRunning) throw new TimeoutException("Inspection queue did not return to idle.");
    }

    private static string Localize(string korean, string english) => korean;
}
