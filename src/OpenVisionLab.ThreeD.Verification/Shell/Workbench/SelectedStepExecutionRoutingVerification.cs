using System.IO;
using System.Linq;
using System.Threading;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Verification.Shell.Workbench;

internal static class SelectedStepExecutionRoutingVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        var lines = new List<string>();
        var total = 0;
        var passed = 0;

        void Check(string name, bool condition, string detail)
        {
            total++;
            if (condition)
            {
                passed++;
            }

            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
        }

        var firstStep = CreateStep("first", outputEnabled: true);
        ToolWorkbenchPipelineStepItem? selectedStep = firstStep;
        var firstPreview = 0;
        var firstPublish = 0;
        var firstCancel = 0;
        var firstRefresh = 0;
        var firstRunning = false;
        var firstCanPreview = true;
        var firstCanPublish = true;
        var secondPreview = 0;
        var secondRefresh = 0;
        var failurePreview = 0;
        var commandLog = new List<string>();

        var routes = new Dictionary<string, ToolWorkbenchSelectedStepExecutionRoute>(
            StringComparer.Ordinal)
        {
            ["first"] = new(
                () =>
                {
                    firstPreview++;
                    return Task.FromResult(true);
                },
                () => firstCanPreview,
                () => firstPublish++,
                () => firstCanPublish,
                () => firstCancel++,
                () => firstRunning,
                () => firstRefresh++),
            ["second"] = new(
                () =>
                {
                    secondPreview++;
                    return Task.FromResult(true);
                },
                () => true,
                () => { },
                () => true,
                () => { },
                () => false,
                () => secondRefresh++),
            ["failure"] = new(
                () =>
                {
                    failurePreview++;
                    return Task.FromException<bool>(
                        new InvalidOperationException("verification preview failure"));
                },
                () => true,
                () => { },
                () => true,
                () => { },
                () => false,
                () => { })
        };
        var owner = new ToolWorkbenchSelectedStepExecutionOwner(
            () => selectedStep,
            routes,
            (category, message) => commandLog.Add($"{category}:{message}"));

        var previewed = owner.PreviewAsync().GetAwaiter().GetResult();
        Check(
            "selected Preview dispatches exactly once",
            previewed && firstPreview == 1 && secondPreview == 0,
            $"result={previewed};first={firstPreview};second={secondPreview}");

        owner.Publish();
        owner.Cancel();
        Check(
            "Publish and Cancel dispatch only to the selected route",
            firstPublish == 1 && firstCancel == 1,
            $"publish={firstPublish};cancel={firstCancel}");

        firstRunning = true;
        owner.RefreshCommandStates();
        Check(
            "running state drives the selected Cancel command",
            owner.IsRunning && owner.CancelCommand.CanExecute(null),
            $"running={owner.IsRunning};canCancel={owner.CancelCommand.CanExecute(null)}");

        firstCanPreview = false;
        firstCanPublish = false;
        owner.RefreshCommandStates();
        Check(
            "route predicates drive Preview and Publish commands",
            !owner.PreviewCommand.CanExecute(null)
            && !owner.PublishCommand.CanExecute(null),
            $"preview={owner.PreviewCommand.CanExecute(null)};publish={owner.PublishCommand.CanExecute(null)}");

        owner.RefreshSelectedStepState();
        Check(
            "selected state refresh dispatches exactly once",
            firstRefresh == 1 && secondRefresh == 0,
            $"first={firstRefresh};second={secondRefresh}");

        selectedStep = CreateStep("second", outputEnabled: false);
        owner.RefreshSelectedStepState();
        Check(
            "output-disabled step fails closed without route dispatch",
            selectedStep.State == "Disabled"
            && secondRefresh == 0
            && !owner.CanPreview()
            && !owner.CanPublish(),
            $"state={selectedStep.State};refresh={secondRefresh};preview={owner.CanPreview()};publish={owner.CanPublish()}");

        selectedStep = CreateStep("unknown", outputEnabled: true);
        var unknownPreview = owner.PreviewAsync().GetAwaiter().GetResult();
        owner.Publish();
        owner.Cancel();
        owner.RefreshSelectedStepState();
        Check(
            "unknown ToolId performs no fallback execution",
            !unknownPreview
            && firstPreview == 1
            && firstPublish == 1
            && firstCancel == 1
            && firstRefresh == 1
            && secondPreview == 0
            && secondRefresh == 0,
            $"preview={unknownPreview};first={firstPreview}/{firstPublish}/{firstCancel}/{firstRefresh};second={secondPreview}/{secondRefresh}");

        selectedStep = null;
        Check(
            "no selection disables all selected-step commands",
            !owner.PreviewCommand.CanExecute(null)
            && !owner.PublishCommand.CanExecute(null)
            && !owner.CancelCommand.CanExecute(null),
            $"preview={owner.PreviewCommand.CanExecute(null)};publish={owner.PublishCommand.CanExecute(null)};cancel={owner.CancelCommand.CanExecute(null)}");

        selectedStep = CreateStep("failure", outputEnabled: true);
        owner.PreviewCommand.Execute(null);
        Check(
            "Preview command observes route failures",
            failurePreview == 1
            && commandLog.Any(log => log.Contains(
                "Selected step Preview failed",
                StringComparison.Ordinal)),
            $"preview={failurePreview};logCount={commandLog.Count}");

        var commandPreviewStep = CreateStep("command-preview", outputEnabled: true);
        var commandPreviewCalls = 0;
        var commandPreviewRelease = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var commandPreviewOwner = new ToolWorkbenchSelectedStepExecutionOwner(
            () => commandPreviewStep,
            new Dictionary<string, ToolWorkbenchSelectedStepExecutionRoute>(StringComparer.Ordinal)
            {
                ["command-preview"] = new(
                    async () =>
                    {
                        Interlocked.Increment(ref commandPreviewCalls);
                        return await commandPreviewRelease.Task;
                    },
                    () => true,
                    () => { },
                    () => true,
                    () => { },
                    () => false,
                    () => { })
            },
            (_, _) => { });
        commandPreviewOwner.PreviewCommand.Execute(null);
        commandPreviewOwner.PreviewCommand.Execute(null);
        var commandPreviewAdmitted = SpinWait.SpinUntil(
            () => Volatile.Read(ref commandPreviewCalls) == 1,
            TimeSpan.FromSeconds(1));
        Check(
            "Preview command is single-flight while its Task is pending",
            commandPreviewAdmitted
            && Volatile.Read(ref commandPreviewCalls) == 1
            && !commandPreviewOwner.PreviewCommand.CanExecute(null),
            $"admitted={commandPreviewAdmitted};calls={commandPreviewCalls};canExecute={commandPreviewOwner.PreviewCommand.CanExecute(null)}");

        commandPreviewRelease.SetResult(true);
        var commandPreviewReenabled = SpinWait.SpinUntil(
            () => commandPreviewOwner.PreviewCommand.CanExecute(null),
            TimeSpan.FromSeconds(1));
        commandPreviewOwner.PreviewCommand.Execute(null);
        var commandPreviewReused = SpinWait.SpinUntil(
            () => Volatile.Read(ref commandPreviewCalls) == 2,
            TimeSpan.FromSeconds(1));
        Check(
            "Preview command releases its admission after completion",
            commandPreviewReenabled && commandPreviewReused,
            $"reenabled={commandPreviewReenabled};reused={commandPreviewReused};calls={commandPreviewCalls}");

        commandPreviewRelease = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        commandPreviewOwner.PreviewCommand.Execute(null);
        var commandPreviewCancellationAdmitted = SpinWait.SpinUntil(
            () => Volatile.Read(ref commandPreviewCalls) == 3,
            TimeSpan.FromSeconds(1));
        commandPreviewRelease.SetCanceled();
        var commandPreviewCancellationReleased = SpinWait.SpinUntil(
            () => commandPreviewOwner.PreviewCommand.CanExecute(null),
            TimeSpan.FromSeconds(1));
        Check(
            "Preview command releases its admission after cancellation",
            commandPreviewCancellationAdmitted && commandPreviewCancellationReleased,
            $"admitted={commandPreviewCancellationAdmitted};released={commandPreviewCancellationReleased};calls={commandPreviewCalls}");
        commandPreviewOwner.Dispose();

        var runStep = CreateStep("run", outputEnabled: true);
        var otherStep = CreateStep("other", outputEnabled: true);
        var currentStep = runStep;
        var context = "input-A/conditions-1";
        var runCalls = 0;
        var publishCalls = 0;
        var cancelCalls = 0;
        var completedCalls = 0;
        var canPublish = true;
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var runOwner = new ToolWorkbenchSelectedStepExecutionOwner(
            () => currentStep,
            new Dictionary<string, ToolWorkbenchSelectedStepExecutionRoute>
            {
                ["run"] = new(
                    () => { Interlocked.Increment(ref runCalls); return release.Task; },
                    () => true,
                    () => Interlocked.Increment(ref publishCalls),
                    () => canPublish,
                    () => Interlocked.Increment(ref cancelCalls),
                    () => false,
                    () => { })
            },
            (category, message) => commandLog.Add($"{category}:{message}"),
            _ => context);
        runOwner.RunCompleted += (_, _) => Interlocked.Increment(ref completedCalls);
        bool WaitReleased() => SpinWait.SpinUntil(() => !runOwner.IsCommandRunning, TimeSpan.FromSeconds(2));
        void BeginRun()
        {
            currentStep = runStep;
            release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            runOwner.RunCommand.Execute(null);
        }

        BeginRun();
        runOwner.RunCommand.Execute(null);
        Check("W01 duplicate Run shares one admission", runCalls == 1 && runOwner.IsRunning
            && !runOwner.RunCommand.CanExecute(null) && runOwner.CancelCommand.CanExecute(null), $"calls={runCalls}");
        release.SetResult(true);
        Check("W01 success publishes once and reveals results", WaitReleased() && publishCalls == 1 && completedCalls == 1,
            $"published={publishCalls};revealed={completedCalls}");

        BeginRun();
        currentStep = otherStep;
        runOwner.Cancel();
        release.SetResult(true);
        Check("W01 cancel targets captured route after selection change; late success cannot publish",
            WaitReleased() && cancelCalls == 1 && publishCalls == 1, $"canceled={cancelCalls};published={publishCalls}");

        BeginRun();
        currentStep = otherStep;
        release.SetResult(true);
        Check("W01 late completion cannot publish to a different step", WaitReleased() && publishCalls == 1,
            $"published={publishCalls}");

        foreach (var changedContext in new[] { "input-B/conditions-1", "input-B/conditions-2" })
        {
            BeginRun();
            context = changedContext;
            release.SetResult(true);
            Check("W01 input/condition change rejects late publication", WaitReleased() && publishCalls == 1, context);
        }

        BeginRun();
        release.SetException(new InvalidOperationException("W01 controlled failure"));
        Check("W01 failure releases admission without publishing", WaitReleased() && publishCalls == 1
            && commandLog.Any(log => log.Contains("W01 controlled failure", StringComparison.Ordinal)), $"published={publishCalls}");
        BeginRun();
        release.SetCanceled();
        Check("W01 canceled Task releases admission", WaitReleased() && publishCalls == 1, $"published={publishCalls}");
        BeginRun();
        canPublish = false;
        release.SetResult(true);
        Check("W01 route CanPublish remains authoritative", WaitReleased() && publishCalls == 1, $"published={publishCalls}");
        canPublish = true;
        BeginRun();
        release.SetResult(true);
        Check("W01 recovers with explicit Run after failure/cancel/change", WaitReleased() && publishCalls == 2 && completedCalls == 2,
            $"published={publishCalls};revealed={completedCalls}");
        runOwner.RefreshSelectedStepState();
        context = "restored-input/conditions";
        Check("W01 refresh and restored conditions do not execute", runCalls == 9, $"calls={runCalls}");
        BeginRun();
        runOwner.Dispose();
        release.SetResult(true);
        Check("W01 disposal cancels captured work and disables Run", !runOwner.RunCommand.CanExecute(null)
            && publishCalls == 2 && cancelCalls == 2, $"published={publishCalls};canceled={cancelCalls}");

        var filterTool = ToolWorkbenchToolCatalog.Create().Single(tool => tool.Id == "filter");
        var filterStep = new ToolWorkbenchPipelineStepItem("step.filter", filterTool, "source.filter", "output.filter",
            [new("Method", "Median"), new("KernelSize", "3"), new("MissingValuePolicy", "PreserveMask"), new("BoundaryPolicy", "AvailableNeighbors")]);
        var filterDocument = new ToolRecipeDocument(ToolRecipeDocument.CurrentSchemaVersion, "W01 cancellation",
            new ToolRecipeSource("source.filter", "Cancellation fixture", "C3D", "raw-height", "frame.raw",
                Path.Combine(Path.GetDirectoryName(Path.GetFullPath(reportPath))!, "not-opened.c3d"), 64, new string('A', 64), 4, 4),
            [], [new ToolRecipeStep(filterStep.Id, "filter", "Filter", 1, ["source.filter"], "output.filter",
                [new("Method", "Median"), new("KernelSize", "3"), new("MissingValuePolicy", "PreserveMask"), new("BoundaryPolicy", "AvailableNeighbors")])], []);
        ToolWorkbenchFilterExecutionOwner? filterOwner = null;
        filterOwner = new ToolWorkbenchFilterExecutionOwner(
            () => false, () => { }, () => { }, () => false, () => { }, () => false, () => { }, () => false,
            () => true, () => filterStep, () => true, () => false, () => false, () => filterDocument,
            () => reportPath, (_, _) => { }, _ => { }, _ => { },
            () => { if (filterOwner?.IsFilterPreviewRunning == true) filterOwner.CancelPreview(); });
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var admitted = filterOwner.CanPreview();
            var result = filterOwner.PreviewAsync().GetAwaiter().GetResult();
            Check("W01 Filter immediate cancellation releases state for another explicit attempt",
                admitted && !result && !filterOwner.IsFilterPreviewRunning && filterOwner.CanPreview()
                && filterOwner.FilterExecutionSummary.Contains("canceled", StringComparison.Ordinal),
                $"attempt={attempt};{filterOwner.FilterExecutionSummary}");
        }
        filterOwner.Dispose();

        firstRunning = false;
        firstCanPreview = firstCanPublish = true;
        var resultContext = "input-A/conditions-A";
        selectedStep = firstStep;
        using var resultOwner = new ToolWorkbenchSelectedStepExecutionOwner(() => selectedStep, routes, (_, _) => { },
            _ => resultContext, captureOutput: () => new SelectedToolOutputEvidence("Output", selectedStep!.ToolId, "count", "Calculated"));
        resultOwner.RunCommand.Execute(null);
        var secondStep = CreateStep("second", outputEnabled: true);
        selectedStep = secondStep;
        resultOwner.RunCommand.Execute(null);
        Check("R19 separate non-measurement results survive selection",
            resultOwner.TryGetCompletedResult(firstStep, out var stored, out var current) && current && stored.Value == "first"
            && resultOwner.TryGetCompletedResult(secondStep, out var second, out var secondCurrent) && secondCurrent && second.Value == "second", "two retained results");
        var callsBeforeSelection = firstPreview + secondPreview;
        selectedStep = firstStep;
        resultOwner.RefreshSelectedStepState();
        Check("R19 selection restores evidence without execution",
            resultOwner.TryGetCompletedResult(firstStep, out stored, out current) && current && stored.Value == "first"
            && firstPreview + secondPreview == callsBeforeSelection, "selection only");
        resultContext = "input-B/conditions-B";
        Check("R19 changed context marks retained result previous",
            resultOwner.TryGetCompletedResult(firstStep, out stored, out current) && !current && stored.Value == "first", resultContext);
        firstCanPublish = false;
        resultOwner.RunCommand.Execute(null);
        Check("R19 failed publication preserves previous result",
            resultOwner.TryGetCompletedResult(firstStep, out stored, out current) && !current && stored.Value == "first", "publish blocked");
        firstCanPublish = true;
        resultOwner.RunCommand.Execute(null);
        Check("R19 explicit retry restores current result", resultOwner.TryGetCompletedResult(firstStep, out _, out current) && current, resultContext);
        resultOwner.ClearCompletedResults();
        Check("R19 replacement releases retained results",
            !resultOwner.TryGetCompletedResult(firstStep, out _, out _) && !resultOwner.TryGetCompletedResult(secondStep, out _, out _), "cleared");

        var activityRecords = new System.Collections.Concurrent.ConcurrentQueue<(bool Run, ResultStatus? Status, string Reason, bool Canceled)>();
        var activityStatus = (ResultStatus?)ResultStatus.Fail;
        var activityMode = "success";
        var activityPublications = 0;
        using var activityOwner = new ToolWorkbenchSelectedStepExecutionOwner(() => firstStep,
            new Dictionary<string, ToolWorkbenchSelectedStepExecutionRoute>
            {
                [firstStep.ToolId] = new(() => activityMode switch
                {
                    "error" => Task.FromException<bool>(new InvalidOperationException("explicit execution failure")),
                    "cancel" => Task.FromCanceled<bool>(new CancellationToken(canceled: true)),
                    "blocked" => Task.FromResult(false),
                    _ => Task.FromResult(true)
                }, () => true, () => activityPublications++, () => true, () => { }, () => false, () => { })
            }, (category, message) => commandLog.Add($"{category}:{message}"),
            captureOutput: () => new SelectedToolOutputEvidence("Value", "5", "raw-height", "Pass", Reason: "original producer reason") { ExecutionStatus = activityStatus },
            recordExecution: (_, run, status, _, reason, canceled) => activityRecords.Enqueue((run, status, reason, canceled)));
        foreach (var status in new ResultStatus?[] { ResultStatus.Pass, ResultStatus.Fail, ResultStatus.Warning, ResultStatus.Error, null })
        {
            activityStatus = status;
            activityOwner.RunCommand.Execute(null);
            Check($"Activity uses the typed {status?.ToString() ?? "calculated"} result", activityRecords.Last().Status == status && activityRecords.Last().Run
                && !activityRecords.Last().Canceled && activityRecords.Last().Reason == "original producer reason", "Typed status and original reason are independent from the conflicting 'Pass' label.");
        }
        activityOwner.PreviewCommand.Execute(null);
        Check("Explicit Preview is recorded without publication", !activityRecords.Last().Run && activityPublications == 5, $"publications={activityPublications}");
        foreach (var mode in new[] { "error", "cancel", "blocked" })
        {
            activityMode = mode;
            activityOwner.RunCommand.Execute(null);
            Check($"Activity records {mode} without a successful publication", activityPublications == 5
                && (mode != "error" || activityRecords.Last().Reason == "explicit execution failure")
                && (mode == "cancel" ? activityRecords.Last().Canceled && activityRecords.Last().Status is null
                    : activityRecords.Last().Status == (mode == "error" ? ResultStatus.Error : ResultStatus.NotRun)), $"records={activityRecords.Count}");
        }
        activityMode = "success";
        activityOwner.RunCommand.Execute(null);
        Check("Explicit retry recovers one execution record and publication", activityPublications == 6 && activityRecords.Count == 10, $"publications={activityPublications};records={activityRecords.Count}");
        activityOwner.Dispose();
        activityOwner.RunCommand.Execute(null);
        Check("Disposed execution owner cannot append Activity", activityRecords.Count == 10 && activityPublications == 6, "No late execution or display mutation.");

        owner.Dispose();
        Check(
            "disposed owner fails closed for all selected-step commands",
            owner.IsDisposed
            && !owner.PreviewCommand.CanExecute(null)
            && !owner.PublishCommand.CanExecute(null)
            && !owner.CancelCommand.CanExecute(null),
            $"disposed={owner.IsDisposed};preview={owner.PreviewCommand.CanExecute(null)};publish={owner.PublishCommand.CanExecute(null)};cancel={owner.CancelCommand.CanExecute(null)}");

        var passedAll = total > 0 && passed == total;
        lines.Add($"SelectedStepExecutionRouting|{(passedAll ? "PASS" : "FAIL")}|checks={passed}/{total}");
        var fullReportPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(
            Path.GetDirectoryName(fullReportPath) ?? Environment.CurrentDirectory);
        File.WriteAllLines(fullReportPath, lines);
        summary = lines[^1];
        return passedAll;
    }

    private static ToolWorkbenchPipelineStepItem CreateStep(
        string toolId,
        bool outputEnabled)
    {
        var tool = new ToolWorkbenchToolItem(
            "Verification",
            toolId,
            toolId,
            1,
            "Input",
            "Output",
            "Selected-step routing fixture.",
            []);
        return new ToolWorkbenchPipelineStepItem(
            $"step.{toolId}",
            tool,
            "input.fixture",
            $"output.{toolId}",
            outputEnabled: outputEnabled);
    }
}
