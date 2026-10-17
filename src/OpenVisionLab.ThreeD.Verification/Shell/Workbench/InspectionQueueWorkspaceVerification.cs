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

            var presentationRuns = 0;
            using (var listQueue = new InspectionQueueWorkspaceViewModel(
                () => document, () => recipePath, _ => (true, "ready"), (_, _) => (true, "compatible"),
                (_, _, _) => { presentationRuns++; throw new InvalidOperationException("Presentation must not run inspection."); }, () => { }, Localize))
            {
                Check("empty list and disabled displayed-check contract", listQueue.ProcessedCount == 0 && !listQueue.AreVisibleSamplesChecked
                    && !listQueue.SetVisibleCheckedCommand.CanExecute(true) && listQueue.EmptyQueueText == "검사할 파일이나 폴더를 추가하세요.", listQueue.EmptyQueueText);
                listQueue.AddSources([sourceA, sourceB]);
                listQueue.SearchText = "input-a";
                listQueue.SetVisibleCheckedCommand.Execute(false);
                Check("displayed-check bridge keeps hidden inclusion and selection independent", listQueue.VisibleSamples.Count == 1 && !listQueue.Samples[0].IsChecked
                    && listQueue.Samples[1].IsChecked && listQueue.CheckedCount == 1 && !listQueue.AreVisibleSamplesChecked && presentationRuns == 0, listQueue.ProcessingSummaryText);
                listQueue.SetVisibleCheckedCommand.Execute(true);
                listQueue.SetStatusFilterCommand.Execute("Error");
                Check("filter bridge keeps inclusion and distinguishes a filtered empty list", listQueue.StatusFilter == InspectionQueueStatusFilter.Error
                    && !listQueue.HasVisibleSamples && listQueue.HasSamples && listQueue.CheckedCount == 2 && listQueue.EmptyQueueText == "검색·상태 필터에 맞는 파일이 없습니다." && presentationRuns == 0, listQueue.EmptyQueueText);
                listQueue.SetStatusFilterCommand.Execute("invalid-filter");
                Check("invalid filter does not change the established filter", listQueue.StatusFilter == InspectionQueueStatusFilter.Error && presentationRuns == 0, listQueue.StatusFilter.ToString());
                listQueue.SetStatusFilterCommand.Execute("All");
                listQueue.SearchText = string.Empty;
                listQueue.Samples[1].IsChecked = false;
                Check("external inclusion writes refresh displayed-check projection", !listQueue.AreVisibleSamplesChecked && listQueue.CheckedCount == 1 && listQueue.ProcessedCount == 0 && presentationRuns == 0, listQueue.ProcessingSummaryText);
                var renderRecord = CreateOutcome(root, document, recipePath, sourceA, 3000).Record!;
                listQueue.Samples[0].SetRecord(renderRecord with { Status = ResultStatus.Warning }, Path.Combine(root, "warning-render.json"));
                listQueue.Samples[1].SetCancelled("cancelled");
                Check("processing count retains Warning and excludes cancellation without a new judgement policy", listQueue.ProcessedCount == 1 && listQueue.WarningCount == 1
                    && listQueue.CancelledCount == 1 && listQueue.PassCount == 0 && listQueue.ProcessingSummaryText == "처리 1/2 · 체크 1" && presentationRuns == 0, listQueue.ProcessingSummaryText);
                listQueue.Samples[1].SetError("error");
                Check("processing count reads Error as handled and never converts Warning to Pass", listQueue.ProcessedCount == 2 && listQueue.ErrorCount == 1 && listQueue.WarningCount == 1 && listQueue.PassCount == 0 && presentationRuns == 0, listQueue.ProcessingSummaryText);
                Check("D23 blank and error-only OK rate use the established processed denominator", listQueue.PassRateText == "0.0%"
                    && listQueue.RunningCount == 0 && listQueue.Samples[1].ElapsedText == "—" && presentationRuns == 0, listQueue.PassRateText);
                listQueue.Samples[0].SetRecord(renderRecord with { Status = ResultStatus.Pass, ElapsedMilliseconds = 12345.678 }, Path.Combine(root, "elapsed-render.json"));
                Check("D23 OK rate includes the existing Error denominator and reads recorded milliseconds as seconds", listQueue.PassRateText == "50.0%"
                    && listQueue.ProcessedCountText == "검사 완료 2 / 2" && listQueue.Samples[0].ElapsedMilliseconds == 12345.678
                    && listQueue.Samples[0].ElapsedText == "12.35 s" && presentationRuns == 0, listQueue.CountsText);
                listQueue.SearchText = "input-b";
                listQueue.SetStatusFilterCommand.Execute("Error");
                listQueue.Samples[0].IsChecked = false;
                Check("D23 whole queue rate is independent of visible filters, search and checked subset", listQueue.VisibleSamples.Count == 1
                    && listQueue.PassRateText == "50.0%" && listQueue.CheckedCount == 0 && listQueue.ProcessedCount == 2 && presentationRuns == 0, listQueue.PassRateText);
                listQueue.Samples[0].BeginRun();
                listQueue.Samples[0].SetRunning();
                Check("D23 new explicit run clears old timing and keeps Running separate from processed inputs", listQueue.Samples[0].ElapsedText == "—"
                    && listQueue.RunningCount == 1 && listQueue.ProcessedCount == 1 && listQueue.PassRateText == "0.0%" && presentationRuns == 0, listQueue.CountsText);
                listQueue.Samples[0].SetCancelled("cancelled");
                listQueue.Samples[1].BeginRun();
                Check("D23 untested and cancelled inputs leave the zero-denominator rate undefined", listQueue.PassRateText == "—%"
                    && listQueue.ProcessedCount == 0 && listQueue.UntestedCount == 1 && listQueue.CancelledCount == 1 && listQueue.RunningCount == 0
                    && listQueue.Samples.All(item => item.ElapsedMilliseconds is null && item.ElapsedText == "—") && presentationRuns == 0, listQueue.PassRateText);
                var timingSample = listQueue.Samples[0];
                timingSample.SetRecord(renderRecord with { ElapsedMilliseconds = 0 }, Path.Combine(root, "zero-elapsed.json"));
                Check("D23 an actual zero-duration record is distinct from no timing", timingSample.ElapsedText == "0.00 s" && timingSample.ElapsedMilliseconds == 0, timingSample.ElapsedText);
                foreach (var invalidElapsed in new[] { -1d, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
                {
                    timingSample.SetRecord(renderRecord with { ElapsedMilliseconds = invalidElapsed }, Path.Combine(root, "invalid-elapsed-render.json"));
                    Check($"D23 invalid recorded elapsed {invalidElapsed} is unknown without rewriting the Record", timingSample.ElapsedMilliseconds is null
                        && timingSample.ElapsedText == "—" && timingSample.Record!.ElapsedMilliseconds.Equals(invalidElapsed) && presentationRuns == 0, timingSample.ElapsedText);
                }
                var timeNotifications = new List<string?>();
                timingSample.PropertyChanged += (_, args) => timeNotifications.Add(args.PropertyName);
                timingSample.SetPendingRecord(renderRecord with { ElapsedMilliseconds = 98765.4321 }, "save failed", document);
                var beforeTimingRecovery = timingSample.Record;
                Check("D23 pending save retains calculated timing and Error state", timingSample.State == InspectionQueueItemState.Error
                    && timingSample.ElapsedText == "98.77 s" && listQueue.PassRateText == "0.0%"
                    && timeNotifications.Contains(nameof(InspectionQueueSampleItem.ElapsedText)), timingSample.ElapsedText);
                timingSample.SetRecord(beforeTimingRecovery!, Path.Combine(root, "timing-save-recovered.json"), document);
                Check("D23 save-only recovery retains the same time and stored verdict", ReferenceEquals(beforeTimingRecovery, timingSample.Record)
                    && timingSample.State == InspectionQueueItemState.Pass && timingSample.ElapsedText == "98.77 s" && listQueue.PassRateText == "100.0%" && presentationRuns == 0, timingSample.ElapsedText);
                listQueue.ClearQueueCommand.Execute(null);
                Check("D23 clearing the owned queue resets denominator and state counts without execution", listQueue.PassRateText == "—%" && listQueue.AllCount == 0
                    && listQueue.RunningCount == 0 && listQueue.ProcessedCount == 0 && presentationRuns == 0, listQueue.CountsText);
                listQueue.Dispose();
                Check("disposed list disables both presentation command bridges", !listQueue.SetStatusFilterCommand.CanExecute("All") && !listQueue.SetVisibleCheckedCommand.CanExecute(true), "Existing Queue lifetime remains the owner.");
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

            var scopedRuns = 0;
            using var scopedQueue = new InspectionQueueWorkspaceViewModel(() => document, () => recipePath,
                _ => (true, "Ready"), (_, _) => (true, "Compatible"), (_, savedPath, input) =>
                {
                    scopedRuns++;
                    return Task.FromResult(CreateOutcome(root, document, savedPath, input, 800 + scopedRuns));
                }, () => { }, Localize);
            var scopedPaths = Enumerable.Range(0, 6).Select(index => Path.Combine(root, $"scope-{index}.C3D")).ToArray();
            foreach (var path in scopedPaths) File.Copy(sourceA, path);
            scopedQueue.AddSources(scopedPaths);
            for (var index = 0; index < 4; index++)
            {
                var failed = CreateOutcome(root, document, recipePath, scopedPaths[index], 700 + index);
                scopedQueue.Samples[index].SetRecord(failed.Record! with { Status = ResultStatus.Fail }, failed.RecordPath!, document);
            }
            scopedQueue.Samples[4].SetError("Owned fixture read error");
            scopedQueue.Samples[5].SetError("Owned fixture input error");
            scopedQueue.SelectedRunScope = InspectionQueueRunScope.FailedAndError;
            Check("NG4/error2 scope selection does not execute", scopedRuns == 0 && scopedQueue.RunScopeTargetCount == 6
                && scopedQueue.RunScopeLabel.Contains("6", StringComparison.Ordinal) && scopedQueue.RunScopeCommand.CanExecute(null), scopedQueue.RunScopeLabel);
            scopedQueue.StatusFilter = InspectionQueueStatusFilter.Fail;
            Check("display filter is independent from NG/error run scope", scopedQueue.VisibleSamples.Count == 4 && scopedQueue.RunScopeTargetCount == 6, $"visible={scopedQueue.VisibleSamples.Count};targets={scopedQueue.RunScopeTargetCount}");
            scopedQueue.RunScopeCommand.Execute(null);
            awaitIdle(scopedQueue);
            Check("explicit NG/error run handles all six targets", scopedRuns == 6 && scopedQueue.RunScopeTargetCount == 0 && !scopedQueue.RunScopeCommand.CanExecute(null), $"runs={scopedRuns};targets={scopedQueue.RunScopeTargetCount}");
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
            var summaryRecordA = queue.Samples[0].Record!;
            var summaryRecordB = queue.Samples[1].Record!;
            var summaryRunCount = runCount;
            var summaryStep = summaryRecordA.Steps![0] with { SemanticFingerprint = "same-settings" };
            queue.Samples[0].SetRecord(summaryRecordA with { Steps = [summaryStep with
            {
                Metrics = [new InspectionRunMetric("thickness", MetricKind.Number, 8, "mm", ResultStatus.Pass)]
            }] }, queue.Samples[0].RunRecordPath!);
            queue.Samples[1].SetRecord(summaryRecordB with { Steps = [summaryStep with
            {
                Metrics = [new InspectionRunMetric("thickness", MetricKind.Number, 12, "mm", ResultStatus.Pass),
                    new InspectionRunMetric("thickness", MetricKind.Number, 12000, "µm", ResultStatus.Fail),
                    new InspectionRunMetric("non-finite", MetricKind.Number, double.PositiveInfinity, "mm", ResultStatus.Fail)]
            }, summaryStep with
            {
                SemanticFingerprint = "other-settings",
                Metrics = [new InspectionRunMetric("thickness", MetricKind.Number, 16, "mm", ResultStatus.Fail)]
            }] }, queue.Samples[1].RunRecordPath!);
            var measurementSummaries = queue.MeasurementSummaries;
            Check("inspection summary reads stored verdicts and keeps settings, units and non-finite values separate",
                measurementSummaries.Count == 4
                && measurementSummaries.Any(item => item.MetricName == "thickness" && item.Unit == "mm"
                    && item.Minimum == 8 && item.Mean == 10 && item.Maximum == 12 && item.PassCount == 2 && item.FailCount == 0)
                && measurementSummaries.Any(item => item.Unit == "µm" && item.Mean == 12000 && item.FailCount == 1)
                && measurementSummaries.Any(item => item.Unit == "mm" && item.Mean == 16 && item.FailCount == 1)
                && measurementSummaries.Any(item => item.MetricName == "non-finite" && item.Mean is null)
                && runCount == summaryRunCount,
                $"groups={measurementSummaries.Count};storedMean={measurementSummaries.FirstOrDefault()?.Mean};runs={summaryRunCount}->{runCount}");
            // Display-only record fixtures prove projection; the stored verdict remains authoritative.
            var d24Step = summaryStep with { ToolId = "thickness", ToolName = "Recorded thickness", Metrics =
            [
                new("Mean", MetricKind.Length, 8, "mm", ResultStatus.Pass),
                new("LowerLimit", MetricKind.Length, 7.75, "mm", null), new("UpperLimit", MetricKind.Length, 8.25, "mm", null),
                new("Secondary", MetricKind.Length, 9, "mm", ResultStatus.Fail)
            ] };
            var d24A = summaryRecordA with { Steps = [d24Step] };
            var d24B = summaryRecordB with { Steps = [d24Step with { Metrics =
            [
                new("Mean", MetricKind.Length, 12, "mm", ResultStatus.Pass),
                new("LowerLimit", MetricKind.Length, 7.75, "mm", null), new("UpperLimit", MetricKind.Length, 8.25, "mm", null)
            ] }] };
            queue.Samples[0].SetRecord(d24A, queue.Samples[0].RunRecordPath!);
            queue.Samples[1].SetRecord(d24B, queue.Samples[1].RunRecordPath!);
            var d24Mean = queue.MeasurementSummaries.Single(row => row.MetricName == "Mean");
            Check("D24 recorded file values keep source order, Run identity, kind, settings and units",
                d24Mean.StepId == d24Step.Id && d24Mean.Kind == MetricKind.Length && d24Mean.SettingsIdentity == "same-settings"
                && d24Mean.Points.Select(point => point.SourcePath).SequenceEqual(new[] { sourceA, sourceB })
                && d24Mean.Points.Select(point => point.RunId).SequenceEqual(new[] { d24A.RunId, d24B.RunId })
                && d24Mean.Points.All(point => point.Unit == "mm") && d24Mean.OutputLabel == "Mean (mm)", $"points={d24Mean.Points.Count};settings={d24Mean.SettingsIdentity}");
            Check("D24 stored Pass is never rejudged from its recorded range", d24Mean.PassCount == 2 && d24Mean.FailCount == 0
                && d24Mean.Points[1].Status == ResultStatus.Pass && d24Mean.Points[1].Value == 12 && d24Mean.RangeText == "7.75–8.25 mm"
                && d24Mean.Minimum == 8 && d24Mean.Mean == 10 && d24Mean.Maximum == 12 && runCount == summaryRunCount, d24Mean.RangeText);
            Check("D24 secondary output never inherits the primary output bounds", queue.MeasurementSummaries.Single(row => row.MetricName == "Secondary") is { RangeText: "—", Points.Count: 1 } secondary
                && secondary.Points[0].RangeText == "—" && secondary.Points[0].Status == ResultStatus.Fail, "secondary=9mm NG;range=unknown");
            queue.SearchText = "input-b";
            queue.SetStatusFilterCommand.Execute("Fail");
            queue.Samples[0].IsChecked = false;
            Check("D24 summary uses the whole queue after filter, search and check changes", queue.VisibleSamples.Count == 0
                && queue.MeasurementSummaries.Single(row => row.MetricName == "Mean").Points.Count == 2 && runCount == summaryRunCount, $"visible={queue.VisibleSamples.Count}");
            queue.SearchText = string.Empty;
            queue.SetStatusFilterCommand.Execute("All");
            queue.Samples[0].IsChecked = true;
            queue.Samples[1].SetRecord(d24B with { Steps = [d24B.Steps![0] with { Metrics = [new("Mean", MetricKind.Length, 12, "mm", ResultStatus.Fail)] }] }, queue.Samples[1].RunRecordPath!);
            Check("D24 missing bounds suppress a shared band but preserve measured values", queue.MeasurementSummaries.Single(row => row.MetricName == "Mean") is { RangeText: "—", Points.Count: 2 } missing
                && missing.Points[0].RangeText == "7.75–8.25 mm" && missing.Points[1].RangeText == "—" && missing.Points[1].Value == 12, "mixed known/unknown recorded bounds");
            queue.Samples[1].SetRecord(d24B with { Steps = [d24Step with { Metrics = [new("Mean", MetricKind.Length, double.PositiveInfinity, "mm", ResultStatus.Fail), new("LowerLimit", MetricKind.Length, 10, "mm", null), new("UpperLimit", MetricKind.Length, 9, "mm", null)] }] }, queue.Samples[1].RunRecordPath!);
            Check("D24 non-finite values and reversed bounds stay undefined without losing stored NG", queue.MeasurementSummaries.Single(row => row.MetricName == "Mean") is { Mean: 8, FailCount: 1, RangeText: "—" } invalid
                && invalid.Points[1].Value is null && invalid.Points[1].Status == ResultStatus.Fail && invalid.Points[1].RangeText == "—", "invalid value/range retained as —");
            queue.Samples[1].SetRecord(d24B with { Steps = [d24Step with { Status = ResultStatus.Error }] }, queue.Samples[1].RunRecordPath!);
            Check("D24 errored steps preserve file positions but never add numeric evidence", queue.MeasurementSummaries.Single(row => row.MetricName == "Mean") is { ErrorCount: 1, Mean: 8 } errored
                && errored.Points.Count == 2 && errored.Points[1].Value is null && errored.Points[1].Status == ResultStatus.Error, "error gap preserves two positions");
            queue.Samples[1].SetRecord(d24B with { Steps = [d24Step with { ToolId = "warpage", Metrics = [new("Rms", MetricKind.Deviation, .1, "raw-height", ResultStatus.Pass), new("MaximumRms", MetricKind.Deviation, .2, "raw-height", null)] }] }, queue.Samples[1].RunRecordPath!);
            Check("D24 one-sided recorded RMS upper limit does not invent a lower bound", queue.MeasurementSummaries.Single(row => row.MetricName == "Rms") is { LowerLimit: null, UpperLimit: .2, RangeText: "≤ 0.2 raw-height" }, "recorded Rms/MaximumRms;no fabricated zero");
            queue.Samples[1].SetRecord(d24B with { Steps = [d24Step with { SemanticFingerprint = "other-settings", Metrics = [new("Mean", MetricKind.Length, 16, "mm", ResultStatus.Warning)] }, d24Step with { Metrics = [new("Mean", MetricKind.Number, 24, "mm", ResultStatus.Pass), new("Mean", MetricKind.Length, 24000, "µm", ResultStatus.Fail)] }] }, queue.Samples[1].RunRecordPath!);
            Check("D24 kind, units and settings remain separate and Warning is never counted as Pass", queue.MeasurementSummaries.Count(row => row.MetricName == "Mean") == 4
                && queue.MeasurementSummaries.Where(row => row.MetricName == "Mean" && row.Kind == MetricKind.Length && row.Unit == "mm").All(row => row.HasMultipleSettings)
                && queue.MeasurementSummaries.Single(row => row.SettingsIdentity == "other-settings") is { PassCount: 0, FailCount: 0, Points.Count: 1 } warning
                && warning.Points[0].Status == ResultStatus.Warning && warning.Points[0].Value == 16, "four groups;Warning retained");
            queue.Samples[1].SetRecord(d24B with { Steps = [d24Step with { Metrics = [new("Mean", MetricKind.Length, 12, "mm", ResultStatus.Pass), new("LowerLimit", MetricKind.Length, 7.75, "mm", null), new("UpperLimit", MetricKind.Length, 8.5, "mm", null)] }] }, queue.Samples[1].RunRecordPath!);
            Check("D24 conflicting recorded ranges retain individual evidence and suppress a shared band", queue.MeasurementSummaries.Single(row => row.MetricName == "Mean") is { RangeText: "—", Points.Count: 2 } conflict
                && conflict.Points[0].RangeText == "7.75–8.25 mm" && conflict.Points[1].RangeText == "7.75–8.5 mm", "conflicting ranges;no current-editor fallback");
            queue.Samples[1].SetRecord(d24B with { Steps = [d24Step with { Metrics = [new("Mean", MetricKind.Length, 12, "mm", ResultStatus.Pass), new("LowerLimit", MetricKind.Number, 7.75, "mm", null), new("UpperLimit", MetricKind.Length, 8250, "µm", null)] }] }, queue.Samples[1].RunRecordPath!);
            Check("D24 bounds with a different kind or unit never become the measured output range", queue.MeasurementSummaries.Single(row => row.MetricName == "Mean").Points[1].RangeText == "—"
                && queue.MeasurementSummaries.Single(row => row.MetricName == "Mean").Points[1].Value == 12, "kind/unit mismatch retains value but no inferred bound");
            queue.Samples[1].SetRecord(d24B with { Steps = [d24Step with { Message = ToolRecipeStep.UnconfiguredAcceptanceMessage }] }, queue.Samples[1].RunRecordPath!);
            Check("D24 unconfigured acceptance never displays stale recorded bounds", queue.MeasurementSummaries.Single(row => row.MetricName == "Mean").RangeText == "—"
                && queue.MeasurementSummaries.Single(row => row.MetricName == "Mean").Points[1].RangeText == "—" && runCount == summaryRunCount, "unjudged bound suppression");
            queue.Samples[1].SetRecord(d24B, queue.Samples[1].RunRecordPath!);
            Check("D24 valid record recovery restores the same range and statistics without executing", queue.MeasurementSummaries.Single(row => row.MetricName == "Mean") is { RangeText: "7.75–8.25 mm", Mean: 10, Points.Count: 2 }
                && runCount == summaryRunCount && !queue.IsRunning, "display recovery;no new Run");
            queue.Samples[0].SetRecord(summaryRecordA, queue.Samples[0].RunRecordPath!);
            queue.Samples[1].SetRecord(summaryRecordB, queue.Samples[1].RunRecordPath!);
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

            Check("output projection separates measurement name, exact value, unit and recorded verdict",
                row.Outputs.Count == 1 && row.Outputs[0].OutputName == "Mean" && row.Outputs[0].Value == "8.250000000000002"
                && row.Outputs[0].Unit == "raw-height" && row.Outputs[0].Status == "Fail" && row.Outputs[0].Limits == "7.75–8.25 raw-height",
                $"outputs={row.Outputs.Count};value={row.Outputs[0].ValueWithUnit}");
            var multiOutputStep = recordedStep with
            {
                Metrics = recordedStep.Metrics.Concat([new InspectionRunMetric("Secondary ratio", MetricKind.Number, 0.12345678901234567, "ratio", ResultStatus.Pass)]).ToArray()
            };
            var multiOutputRow = InspectionQueueStepResultItem.FromRecord(multiOutputStep, authoredStep);
            Check("multiple judged outputs retain independent recorded states and do not copy the primary limits",
                multiOutputRow.Outputs.Count == 2 && multiOutputRow.Outputs[0].Status == "Fail" && multiOutputRow.Outputs[1].Status == "Pass"
                && multiOutputRow.Outputs[1].Unit == "ratio" && multiOutputRow.Outputs[1].Limits == "—"
                && multiOutputRow.Outputs[1].Value == "0.12345678901234566",
                $"outputs={multiOutputRow.Outputs.Count};secondary={multiOutputRow.Outputs[1].ValueWithUnit};limits={multiOutputRow.Outputs[1].Limits}");
            var d25Step = recordedStep with
            {
                ToolId = "warpage", Metrics = [new("PeakToValley", MetricKind.Length, .15, "mm", ResultStatus.Pass),
                    new("Rms", MetricKind.Length, .8, "mm", ResultStatus.Pass), new("MaximumPeakToValley", MetricKind.Length, 1, "mm", null),
                    new("MaximumRms", MetricKind.Length, .2, "mm", null)]
            };
            var d25Authored = authoredStep with { ToolId = "warpage", Parameters = [new("MaximumRms", "999")] };
            var d25Row = InspectionQueueStepResultItem.FromRecord(d25Step, d25Authored);
            Check("D25 secondary range reads its recorded output limit without rejudging or changing primary limits", d25Row.Outputs[1] is { Limits: "≤ 0.2 mm", Status: "Pass", Value: "0.8" }
                && d25Row.Outputs[0].Limits == d25Row.Limits && d25Row.Outputs[0].DisplayLimits == "≤ 1 mm" && d25Row.Outputs[0].OrderLabel == "02" && d25Row.Outputs[1].OrderLabel == string.Empty
                && d25Row.Outputs[1].ToolName == d25Row.ToolName && d25Row.Outputs[1].ToolLabel == string.Empty, d25Row.Outputs[1].Limits);
            foreach (var d25Invalid in new[]
            {
                ("missing", Array.Empty<InspectionRunMetric>()),
                ("unit mismatch", new[] { new InspectionRunMetric("MaximumRms", MetricKind.Length, .2, "µm", null) }),
                ("kind mismatch", new[] { new InspectionRunMetric("MaximumRms", MetricKind.Number, .2, "mm", null) }),
                ("nonfinite", new[] { new InspectionRunMetric("MaximumRms", MetricKind.Length, double.NaN, "mm", null) }),
                ("conflicting", new[] { new InspectionRunMetric("MaximumRms", MetricKind.Length, .2, "mm", null), new("MaximumRms", MetricKind.Length, .3, "mm", null) })
            })
            {
                var invalidRow = InspectionQueueStepResultItem.FromRecord(d25Step with { Metrics = d25Step.Metrics.Where(metric => metric.Name != "MaximumRms").Concat(d25Invalid.Item2).ToArray() }, d25Authored);
                Check($"D25 {d25Invalid.Item1} never copies primary/editor limits to the secondary output", invalidRow.Outputs[1].Limits == "—"
                    && invalidRow.Outputs[1].Status == "Pass" && invalidRow.Outputs[1].Value == "0.8", invalidRow.Outputs[1].Limits);
            }
            var d25Unjudged = InspectionQueueStepResultItem.FromRecord(d25Step with { Status = ResultStatus.NotRun, Message = ToolRecipeStep.UnconfiguredAcceptanceMessage }, d25Authored);
            Check("D25 unjudged outputs preserve the existing unconfigured contract", d25Unjudged.Outputs.All(output => output.Limits == d25Unjudged.Limits && output.Status == "NotRun"), d25Unjudged.Limits);
            var d25Mutable = d25Step.Metrics.ToArray();
            var d25Snapshot = InspectionQueueStepResultItem.FromRecord(d25Step with { Metrics = d25Mutable }, d25Authored);
            d25Mutable[^1] = new("MaximumRms", MetricKind.Length, 999, "mm", null);
            Check("D25 range projection is frozen after admission and valid recovery does not execute", d25Snapshot.Outputs[1].Limits == d25Row.Outputs[1].Limits
                && InspectionQueueStepResultItem.FromRecord(d25Step, d25Authored).Outputs[1].Limits == d25Row.Outputs[1].Limits && runCount == 9 && !queue.IsRunning, d25Snapshot.Outputs[1].Limits);
            queue.Samples[0].SetRecord(completedBeforeStop with { Steps = [multiOutputStep] }, queue.Samples[0].RunRecordPath!, document with { Steps = [authoredStep] });
            queue.SelectedSample = queue.Samples[0];
            queue.SelectedOutputResult = queue.VisibleOutputResults[1];
            var selectedSecondary = queue.SelectedOutputResult;
            Check("output selection bridges the canonical step and preserves secondary output without running",
                queue.SelectedOutputResult?.OutputName == "Secondary ratio" && ReferenceEquals(queue.SelectedOutputResult.Step, queue.SelectedStepResult)
                && queue.SelectedSample == queue.Samples[0] && runCount == 9 && !queue.IsRunning,
                $"selected={queue.SelectedOutputResult?.OutputName};step={queue.SelectedStepResult?.StepId};runs={runCount}");
            queue.ShowOnlyFailedSteps = true;
            Check("NG filter keeps all outputs of the existing failed-step contract without rejudging a passing metric",
                queue.VisibleOutputResults.Count == 2 && queue.VisibleOutputResults[1].Status == "Pass"
                && ReferenceEquals(queue.SelectedOutputResult, selectedSecondary) && runCount == 9,
                $"filtered={queue.VisibleOutputResults.Count};secondary={queue.VisibleOutputResults[1].Status}");
            queue.SelectedOutputResult = null;
            Check("clearing only the rendered output selection retains canonical viewer step context",
                queue.SelectedOutputResult is null && queue.SelectedStepResult?.StepId == recordedStep.Id && runCount == 9,
                $"step={queue.SelectedStepResult?.StepId};runs={runCount}");
            queue.SelectedSample = queue.Samples[1];
            queue.SelectedOutputResult = selectedSecondary;
            Check("file switch clears both selections and rejects an output from the previous file",
                queue.SelectedOutputResult is null && queue.SelectedStepResult is null && runCount == 9,
                $"output={queue.SelectedOutputResult?.OutputName ?? "none"};step={queue.SelectedStepResult?.StepId ?? "none"}");
            queue.ShowOnlyFailedSteps = false;
            var erroredOutputs = InspectionQueueStepResultItem.FromRecord(multiOutputStep with { Status = ResultStatus.Error, Message = "Owned SDK error." }, authoredStep).Outputs;
            Check("error step hides otherwise finite stored values and overrides metric verdicts",
                erroredOutputs.All(output => output.Value == "—" && output.Status == "Error" && output.DisplayMessage == "Owned SDK error."),
                string.Join(';', erroredOutputs.Select(output => $"{output.OutputName}:{output.Value}:{output.Status}")));
            var nonFiniteOutput = InspectionQueueStepResultItem.FromRecord(recordedStep with
            {
                Metrics = [new("Mean", MetricKind.Length, double.NaN, "raw-height", ResultStatus.Fail)]
            }, authoredStep).Outputs[0];
            Check("nonfinite output never becomes a numeric value or invented margin", nonFiniteOutput.Value == "—" && nonFiniteOutput.Unit == "raw-height" && nonFiniteOutput.Status == "Fail", nonFiniteOutput.ValueWithUnit);
            var noMeasurement = InspectionQueueStepResultItem.FromRecord(recordedStep with { Metrics = [] }, authoredStep);
            Check("a step without metrics remains one visible result with its recorded reason", noMeasurement.Outputs.Count == 1 && noMeasurement.Outputs[0].Value == "—"
                && noMeasurement.Outputs[0].DisplayMessage == noMeasurement.DisplayMessage, noMeasurement.Outputs[0].DisplayMessage);
            Check("unconfigured output does not inherit a stored metric Pass or Fail verdict", unjudgedRow.Outputs.All(output => output.Status == "NotRun" && output.StatusLabel == unjudgedRow.StatusLabel
                && output.Limits == unjudgedRow.Limits), string.Join(';', unjudgedRow.Outputs.Select(output => output.StatusLabel)));
            queue.Samples[0].SetRecord(completedBeforeStop with { Steps = [recordedStep] }, queue.Samples[0].RunRecordPath!, document with { Steps = [authoredStep] });
            queue.SelectedSample = queue.Samples[0];
            Check("restoring a valid recorded file restores the output table without execution", queue.VisibleOutputResults.Count == 1 && queue.SelectedResultNotice == string.Empty && queue.SelectedStepResult is null
                && queue.SelectedOutputResult is null && runCount == 9, $"outputs={queue.VisibleOutputResults.Count};runs={runCount}");

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
                && languageQueue.SelectedSample!.StateText == "Untested"
                && languageQueue.SelectedSample.StatusSummary == "Untested · Waiting for explicit Run."
                && languageQueue.SelectedSample.Message == "실행을 기다립니다.";
            englishPresentation = false;
            languageQueue.RefreshResultPresentation();
            Check("language round trip refreshes queue labels and summary without execution or input mutation",
                restoredEnglish && languageQueue.RunCheckedLabel == "체크한 1개 검사"
                && languageQueue.SelectedSample!.StatusSummary == "미검사 · 실행을 기다립니다."
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

            const string originalDiagnostic = "원본 진단 / SDK error 123";
            languageInput.SetError(originalDiagnostic);
            executionNotifications = 0;
            languageQueue.RefreshResultPresentation();
            Check("localizing initial waiting guidance does not rewrite unrecognized input diagnostics", languageInput.Message == originalDiagnostic
                && languageInput.StatusSummary == "Error · " + originalDiagnostic && !languageQueue.IsRunning && executionNotifications == 0, languageInput.StatusSummary);

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

            var displayedRecipePath = recipePath;
            using var projectionQueue = new InspectionQueueWorkspaceViewModel(
                () => document, () => displayedRecipePath, _ => (true, "ready"), (_, _) => (true, "compatible"),
                (_, savedPath, inputPath) => Task.FromResult(CreateOutcome(root, document, savedPath, inputPath, 2000)), () => { }, Localize);
            projectionQueue.AddSources([sourceA, sourceB]);
            projectionQueue.Samples[1].IsChecked = false;
            Check("central remaining count uses the existing scope independently of file checks", projectionQueue.CheckedCount == 1
                && projectionQueue.RemainingCount == 2 && projectionQueue.RunRemainingLabel == "남은 파일 2" && projectionQueue.InspectionBasisText == Path.GetFileName(recipePath), projectionQueue.RunRemainingLabel);
            projectionQueue.RunCheckedCommand.Execute(null);
            awaitIdle(projectionQueue);
            Check("completed checked input leaves the actual remaining count", projectionQueue.PassCount == 1 && projectionQueue.RemainingCount == 1
                && projectionQueue.RunRemainingLabel == "남은 파일 1", projectionQueue.RunRemainingLabel);
            displayedRecipePath = Path.Combine(root, "다른-저장-레시피.ov3d-recipe.json");
            File.Copy(recipePath, displayedRecipePath);
            projectionQueue.RefreshCommands();
            Check("central basis retains the admitted saved path when current recipe changes", projectionQueue.SavedRecipeText == Path.GetFileName(displayedRecipePath)
                && projectionQueue.InspectionBasisText == Path.GetFileName(recipePath) && !projectionQueue.RunCheckedCommand.CanExecute(null), projectionQueue.InspectionBasisText);
            projectionQueue.NewInspectionContextCommand.Execute(null);
            Check("explicit new context restores current saved basis without execution", projectionQueue.InspectionBasisText == Path.GetFileName(displayedRecipePath)
                && projectionQueue.RemainingCount == 2 && projectionQueue.Samples.All(item => item.State == InspectionQueueItemState.Untested) && !projectionQueue.IsRunning, projectionQueue.InspectionBasisText);
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


    public static bool VerifyInputFailureRecovery(string reportPath, out string summary)
    {
        var root = Path.GetDirectoryName(Path.GetFullPath(reportPath))!;
        Directory.CreateDirectory(root);
        var lines = new List<string> { "Boundary|Queue production commands with controlled IO failures and stored-record fixtures; no SDK arithmetic or real CU." };
        var total = 0;
        var passed = 0;
        void Check(string name, bool condition, string detail)
        {
            total++;
            if (condition) passed++;
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
        }
        try
        {
            var sourceA = CreateInput(root, "input-a.C3D");
            var sourceB = CreateInput(root, "input-b.C3D");
            var recipePath = Path.Combine(root, "recipe.ov3d-recipe.json");
            var document = CreateDocument(sourceA);
            ToolRecipeDocumentStore.Save(recipePath, document);
            var recordIndex = 0;
            foreach (var error in new Exception[] { new IOException("owned read failed"), new UnauthorizedAccessException("owned access failed"), new InvalidDataException("owned truncated input") })
            {
                var failInput = false;
                var runs = 0;
                using var queue = new InspectionQueueWorkspaceViewModel(() => document, () => recipePath, _ => (true, "ready"),
                    (_, path) => failInput && path == sourceA ? throw error : (true, "compatible"),
                    (snapshot, path, input) => { runs++; return Task.FromResult(CreateOutcome(root, snapshot, path, input, ++recordIndex)); }, () => { }, Localize);
                queue.AddSources([sourceA, sourceB]);
                queue.RunAllCommand.Execute(null);
                awaitIdle(queue);
                var prior = queue.Samples.Select(item => item.RunRecordPath!).ToArray();
                var priorBytes = prior.Select(File.ReadAllBytes).ToArray();
                failInput = true;
                queue.RunAllCommand.Execute(null);
                awaitIdle(queue);
                Check(error.GetType().Name + " is a per-file Error and later input completes", queue.Samples[0].State == InspectionQueueItemState.Error
                    && queue.Samples[0].Message == error.Message && queue.Samples[1].State == InspectionQueueItemState.Pass && runs == 3 && queue.ProgressPercent == 100,
                    $"states={string.Join(',', queue.Samples.Select(item => item.State))};runs={runs};progress={queue.ProgressPercent}");
                Check(error.GetType().Name + " retains historical record links and bytes", queue.Samples.Select(item => item.PreviousRunRecordPath).SequenceEqual(prior)
                    && prior.Select((path, index) => File.ReadAllBytes(path).SequenceEqual(priorBytes[index])).All(value => value), "no stale current result for failed input");
                failInput = false;
                queue.RunRemainingCommand.Execute(null);
                awaitIdle(queue);
                Check(error.GetType().Name + " explicit retry recovers only the failed input", queue.Samples.All(item => item.State == InspectionQueueItemState.Pass)
                    && runs == 4 && queue.Samples[0].PreviousRunRecordPath == prior[0], $"runs={runs}");
            }

            var cancel = true;
            var cancelRuns = 0;
            using (var queue = new InspectionQueueWorkspaceViewModel(() => document, () => recipePath, _ => (true, "ready"), (_, _) => (true, "compatible"),
                (snapshot, path, input) => { cancelRuns++; return cancel ? Task.FromException<InspectionQueueRunOutcome>(new OperationCanceledException())
                    : Task.FromResult(CreateOutcome(root, snapshot, path, input, ++recordIndex)); }, () => { }, Localize))
            {
                queue.AddSources([sourceA, sourceB]);
                queue.RunAllCommand.Execute(null);
                awaitIdle(queue);
                Check("cancelled input leaves later input untested and commands recover", queue.Samples[0].State == InspectionQueueItemState.Cancelled
                    && queue.Samples[1].State == InspectionQueueItemState.Untested && cancelRuns == 1 && queue.RunRemainingCommand.CanExecute(null), queue.SummaryText);
                cancel = false;
                queue.RunRemainingCommand.Execute(null);
                awaitIdle(queue);
                Check("explicit retry after cancellation completes both inputs", queue.Samples.All(item => item.State == InspectionQueueItemState.Pass) && cancelRuns == 3, queue.SummaryText);
            }

            var saved = CreateOutcome(root, document, recipePath, sourceA, ++recordIndex);
            var retryFails = true;
            var pendingRuns = 0;
            using (var queue = new InspectionQueueWorkspaceViewModel(() => document, () => recipePath, _ => (true, "ready"), (_, _) => (true, "compatible"),
                (_, _, _) => { pendingRuns++; return Task.FromResult(saved with { RecordPath = null, RecordSaveFailed = true, Message = "owned record save failure" }); }, () => { }, Localize,
                () => retryFails ? throw new IOException("owned retry save failed") : saved))
            {
                queue.AddSources([sourceA, sourceB]);
                queue.RunAllCommand.Execute(null);
                awaitIdle(queue);
                Exception? escaped = null;
                try { queue.RetryRecordSaveCommand.Execute(null); } catch (Exception exception) { escaped = exception; }
                Check("record save retry contains IO failure and retains the exact calculation", escaped is null && queue.HasPendingRecordSave
                    && ReferenceEquals(queue.Samples[0].Record, saved.Record) && queue.Samples[0].Message == "owned retry save failed"
                    && queue.RetryRecordSaveCommand.CanExecute(null) && !queue.RunAllCommand.CanExecute(null), escaped?.ToString() ?? queue.SummaryText);
                retryFails = false;
                queue.RetryRecordSaveCommand.Execute(null);
                Check("save-only recovery links the same record and never reruns or advances another input", !queue.HasPendingRecordSave && pendingRuns == 1
                    && ReferenceEquals(queue.Samples[0].Record, saved.Record) && queue.Samples[0].CanOpenCurrentRunRecord
                    && queue.Samples[1].State == InspectionQueueItemState.Untested && queue.RunRemainingCommand.CanExecute(null), queue.SummaryText);
                string? opened = null;
                queue.OpenRunRecordRequested += (_, args) => opened = args.Path;
                queue.SelectedSample = queue.Samples[0];
                queue.OpenRunRecordCommand.Execute(null);
                Check("completed row opens its own saved record", opened == saved.RecordPath, opened ?? "no path");
            }
        }
        catch (Exception exception) { Check("unexpected exception", false, exception.ToString()); }
        summary = $"InspectionInputFailureRecovery|pass={passed == total}|checks={passed}/{total}";
        File.WriteAllLines(reportPath, lines.Append(summary));
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
