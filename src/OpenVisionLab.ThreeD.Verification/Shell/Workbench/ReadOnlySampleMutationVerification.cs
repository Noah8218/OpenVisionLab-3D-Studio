using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using OpenVisionLab.ThreeD.Shell;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Verification.Shell.Workbench;

internal static class ReadOnlySampleMutationVerification
{
    // Uses an existing geometry recipe with real upstream inputs; creates no Views or synthetic outputs.
    public static bool Verify(string recipePath, string reportPath, out string summary)
    {
        var checks = new List<(string Name, bool Passed, string Detail)>();
        var root = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(reportPath))!, $"readonly-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var recipeBytes = File.ReadAllBytes(recipePath);
        var thread = new Thread(() =>
        {
            var previousContext = SynchronizationContext.Current;
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            void Check(string name, bool passed, string detail) => checks.Add((name, passed, detail));
            void Await(Task task)
            {
                var frame = new DispatcherFrame();
                _ = task.ContinueWith(_ => dispatcher.BeginInvoke(new Action(() => frame.Continue = false)), TaskScheduler.Default);
                Dispatcher.PushFrame(frame);
                task.GetAwaiter().GetResult();
            }
            void WaitWhile(Func<bool> running)
            {
                Await(Task.Run(async () =>
                {
                    var deadline = DateTime.UtcNow.AddSeconds(30);
                    while (running())
                    {
                        if (DateTime.UtcNow >= deadline) throw new TimeoutException("The explicit command did not complete within 30 seconds.");
                        await Task.Delay(10);
                    }
                }));
                dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
            }
            try
            {
                foreach (var mode in new[] { ShellWorkspaceMode.Inspect, ShellWorkspaceMode.Validate })
                {
                    var modeRoot = Path.Combine(root, mode.ToString());
                    Directory.CreateDirectory(modeRoot);
                    using var shell = new ShellMainWindowViewModel(
                        recentRunRecordsPath: Path.Combine(modeRoot, "recent-runs.json"),
                        recentRecipesPath: Path.Combine(modeRoot, "recent-recipes.json"),
                        orderedRunRecordRoot: Path.Combine(modeRoot, "runs"),
                        integrationSettingsPath: Path.Combine(modeRoot, "integration.json"));
                    var workbench = shell.Workbench;
                    void Open()
                    {
                        if (!workbench.TryOpenTeachingRecipe(recipePath, out var message)) throw new InvalidOperationException(message);
                    }
                    void SelectMode(ShellWorkspaceMode target)
                    {
                        if (!shell.SelectWorkspaceCommand.CanExecute(target)) throw new InvalidOperationException($"Cannot select {target}.");
                        shell.SelectWorkspaceCommand.Execute(target);
                        if (shell.SelectedWorkspaceMode != target) throw new InvalidOperationException($"Selection did not reach {target}.");
                    }
                    Open();
                    SelectMode(ShellWorkspaceMode.Teach);
                    if (!workbench.SelectPipelineStep("step.landmark")) throw new InvalidOperationException("Fixture requires step.landmark.");
                    var originalRevision = workbench.SelectedStepTeachingSelection!.CorrespondenceDescriptor!.ReferenceRevision;
                    SelectMode(mode);
                    Check($"{mode}: public navigation is read-only", workbench.IsReadOnlySampleView, shell.SelectedWorkspaceMode.ToString());
                    workbench.CorrespondenceReferenceRevision = "readonly-must-not-commit";
                    var revision = workbench.SelectedStepTeachingSelection!.CorrespondenceDescriptor!.ReferenceRevision;
                    Check($"{mode}: revision setter preserves committed selection", revision == originalRevision && workbench.CorrespondenceReferenceRevision == originalRevision,
                        $"before={originalRevision}; after={revision}; editor={workbench.CorrespondenceReferenceRevision}");
                    SelectMode(ShellWorkspaceMode.Teach);
                    workbench.CorrespondenceReferenceRevision = "teach-revision";
                    Check($"{mode}: revision editing resumes in Teach", workbench.SelectedStepTeachingSelection!.CorrespondenceDescriptor!.ReferenceRevision == "teach-revision", workbench.CorrespondenceReferenceRevision);

                    Open();
                    if (!workbench.SelectPipelineStep("step.solve")) throw new InvalidOperationException("Fixture requires step.solve.");
                    string Parameter() => workbench.SelectedPipelineStep!.Parameters.Single(item => item.Name == "MaximumConditionEstimate").Value;
                    var originalParameter = Parameter();
                    var applyStateEvents = 0;
                    workbench.ApplySelectedStepParameterDraftCommand.CanExecuteChanged += (_, _) => applyStateEvents++;
                    SelectMode(mode);
                    Check($"{mode}: mode change refreshes Apply state", applyStateEvents > 0, applyStateEvents.ToString());
                    var draft = (XYZAffineSolveStepProperties)workbench.SelectedStepPropertyDraft!;
                    draft.MaximumConditionEstimate /= 2;
                    workbench.MarkSelectedStepParameterDraftDirty();
                    var canApply = workbench.ApplySelectedStepParameterDraftCommand.CanExecute(null);
                    workbench.ApplySelectedStepParameterDraftCommand.Execute(null);
                    Check($"{mode}: Apply command rejects a pending draft", !canApply && Parameter() == originalParameter,
                        $"canApply={canApply}; before={originalParameter}; after={Parameter()}");
                    draft = (XYZAffineSolveStepProperties)workbench.SelectedStepPropertyDraft!;
                    draft.MaximumConditionEstimate /= 2;
                    workbench.MarkSelectedStepParameterDraftDirty();
                    var accepted = workbench.TryApplySelectedStepParameterDraft(out var applyMessage);
                    Check($"{mode}: direct Apply rejects a pending draft", !accepted && Parameter() == originalParameter,
                        $"accepted={accepted}; after={Parameter()}; message={applyMessage}");
                    Check($"{mode}: Discard remains available", workbench.DiscardSelectedStepParameterDraftCommand.CanExecute(null), "Pending parameter draft can be discarded.");
                    workbench.DiscardSelectedStepParameterDraftCommand.Execute(null);
                    SelectMode(ShellWorkspaceMode.Teach);
                    draft = (XYZAffineSolveStepProperties)workbench.SelectedStepPropertyDraft!;
                    draft.MaximumConditionEstimate /= 2;
                    workbench.MarkSelectedStepParameterDraftDirty();
                    Check($"{mode}: Apply resumes in Teach", workbench.ApplySelectedStepParameterDraftCommand.CanExecute(null) && workbench.TryApplySelectedStepParameterDraft(out _) && Parameter() != originalParameter,
                        $"after={Parameter()}; command-state notifications={applyStateEvents}");

                    Open();
                    foreach (var step in workbench.PipelineSteps.TakeWhile(item => item.Id != "step.landmark").ToArray())
                    {
                        workbench.SelectPipelineStep(step.Id);
                        if (!workbench.RunSelectedStepCommand.CanExecute(null)) throw new InvalidOperationException($"Cannot run upstream {step.Id}.");
                        workbench.RunSelectedStepCommand.Execute(null);
                        WaitWhile(() => workbench.IsSelectedStepPreviewRunning);
                    }
                    workbench.SelectPipelineStep("step.landmark");
                    if (!workbench.PreviewSelectedStepCommand.CanExecute(null)) throw new InvalidOperationException("Landmark Preview is unavailable after real upstream execution.");
                    workbench.PreviewSelectedStepCommand.Execute(null);
                    WaitWhile(() => workbench.IsSelectedStepPreviewRunning);
                    Check($"{mode}: real Preview is ready for Publish in Teach", workbench.HasCurrentLandmarkCorrespondencePreview && !workbench.IsLandmarkCorrespondencePreviewPublished && workbench.PublishSelectedStepCommand.CanExecute(null), workbench.LandmarkCorrespondenceExecutionSummary);
                    SelectMode(mode);
                    Check($"{mode}: mode selection does not run or publish", !workbench.IsSelectedStepPreviewRunning && !workbench.IsLandmarkCorrespondencePreviewPublished, "Mode selected through the public navigation command.");
                    Check($"{mode}: Preview and selected Run remain blocked", !workbench.PreviewSelectedStepCommand.CanExecute(null) && !workbench.RunSelectedStepCommand.CanExecute(null), "Existing execution boundary.");
                    var canPublish = workbench.PublishSelectedStepCommand.CanExecute(null);
                    workbench.PublishSelectedStepCommand.Execute(null);
                    Check($"{mode}: Publish preserves the pending result", !canPublish && !workbench.IsLandmarkCorrespondencePreviewPublished,
                        $"canPublish={canPublish}; published={workbench.IsLandmarkCorrespondencePreviewPublished}");
                    SelectMode(ShellWorkspaceMode.Teach);
                    var restoredPublish = workbench.PublishSelectedStepCommand.CanExecute(null);
                    workbench.PublishSelectedStepCommand.Execute(null);
                    Check($"{mode}: explicit Publish resumes in Teach", restoredPublish && workbench.IsLandmarkCorrespondencePreviewPublished,
                        $"canPublish={restoredPublish}; published={workbench.IsLandmarkCorrespondencePreviewPublished}");

                    Open();
                    var sourcePath = System.Text.Json.Nodes.JsonNode.Parse(recipeBytes)!["source"]!["path"]!.GetValue<string>();
                    if (mode == ShellWorkspaceMode.Validate) workbench.SetValidationSetSources([sourcePath]);
                    else workbench.InspectionQueue.AddSources([sourcePath]);
                    SelectMode(mode);
                    var run = workbench.RunCurrentWorkspaceCommand;
                    var canRun = run.CanExecute(null);
                    if (canRun) run.Execute(null);
                    WaitWhile(() => workbench.IsValidationSetRunning || workbench.InspectionQueue.IsRunning);
                    var completed = mode == ShellWorkspaceMode.Validate
                        ? workbench.ValidationSetSamples.Count == 1 && workbench.ValidationSetErrorCount == 0 && workbench.ValidationSetPassCount + workbench.ValidationSetFailCount > 0
                        : workbench.InspectionQueue.Samples.Count == 1 && workbench.InspectionQueue.Samples[0].Record is not null;
                    Check($"{mode}: explicit workspace run is preserved", canRun && completed,
                        $"canRun={canRun}; completed={completed}; validationErrors={workbench.ValidationSetErrorCount}; inspectionErrors={workbench.InspectionQueue.ErrorCount}");
                }
            }
            catch (Exception exception)
            {
                Check("Harness execution", false, exception.ToString());
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previousContext);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        checks.AddRange(CheckPublishGuard());
        checks.Add(("Input recipe unchanged", recipeBytes.SequenceEqual(File.ReadAllBytes(recipePath)), recipePath));
        summary = $"Read-only sample mutation: {checks.Count(item => item.Passed)}/{checks.Count} passed.";
        File.WriteAllLines(reportPath, new[] { summary, $"Assembly: {typeof(ToolWorkbenchViewModel).Assembly.Location}", $"Evidence: {root}" }
            .Concat(checks.Select(item => $"{(item.Passed ? "PASS" : "FAIL")} | {item.Name} | {item.Detail}")));
        return checks.All(item => item.Passed);
    }

    private static List<(string Name, bool Passed, string Detail)> CheckPublishGuard()
    {
        var checks = new List<(string Name, bool Passed, string Detail)>();
        void Check(string name, bool passed, string detail) => checks.Add((name, passed, detail));
        var step = new ToolWorkbenchPipelineStepItem("step.guard",
            new ToolWorkbenchToolItem("Verification", "Guard", "guard", 1, "Input", "Output", "Publish permission boundary.", []),
            "input.guard", "output.guard");
        var readOnly = true;
        var publications = 0;
        var previews = 0;
        var outputReady = true;
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        ToolWorkbenchSelectedStepExecutionOwner? owner = null;
        var route = new ToolWorkbenchSelectedStepExecutionRoute(
            () => { previews++; return completion.Task; }, () => true,
            () => publications++, () => outputReady, () => { }, () => false, () => { });
        using (owner = new ToolWorkbenchSelectedStepExecutionOwner(() => step,
            new Dictionary<string, ToolWorkbenchSelectedStepExecutionRoute> { ["guard"] = route },
            (_, _) => { }, canStartExecution: () => !readOnly && owner?.IsRunning != true))
        {
            Check("Publish guard: read-only command disabled", !owner.PublishCommand.CanExecute(null), "A pending output alone is not permission to publish.");
            owner.PublishCommand.Execute(null);
            owner.Publish();
            Check("Publish guard: command and direct entry preserve output", publications == 0, $"publications={publications}");
            owner.RunCommand.Execute(null);
            owner.PreviewCommand.Execute(null);
            Check("Publish guard: read-only execution remains blocked", previews == 0, $"previews={previews}");
            readOnly = false;
            owner.PublishCommand.Execute(null);
            Check("Publish guard: Teach can publish explicitly", publications == 1, $"publications={publications}");
            outputReady = false;
            Check("Publish guard: route readiness is preserved", !owner.PublishCommand.CanExecute(null), "No ready output.");
            outputReady = true;
            publications = 0;
            owner.RunCommand.Execute(null);
            Check("Publish guard: selected Run retains its in-flight guard", owner.IsRunning && !owner.PublishCommand.CanExecute(null), $"previews={previews}");
            completion.SetResult(true);
            var finished = SpinWait.SpinUntil(() => !owner.IsRunning, TimeSpan.FromSeconds(5));
            Check("Publish guard: accepted Run still publishes its result", finished && publications == 1 && previews == 1, $"finished={finished}; publications={publications}; previews={previews}");
            owner.Dispose();
            Check("Publish guard: disposal remains closed", !owner.PublishCommand.CanExecute(null), owner.IsDisposed.ToString());
        }
        return checks;
    }

#if N8_PROBE
    private static int Main(string[] args)
    {
        System.Runtime.Loader.AssemblyLoadContext.Default.Resolving += (_, name) =>
        {
            var path = Path.Combine(args[0], name.Name + ".dll");
            return File.Exists(path) ? System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromAssemblyPath(path) : null;
        };
        return RunProbe(args[1], args[2]);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static int RunProbe(string recipePath, string reportPath)
    {
        if (recipePath == "--owner")
        {
            var checks = CheckPublishGuard();
            File.WriteAllLines(reportPath, checks.Select(item => $"{(item.Passed ? "PASS" : "FAIL")} | {item.Name} | {item.Detail}"));
            Console.WriteLine($"Publish owner: {checks.Count(item => item.Passed)}/{checks.Count} passed.");
            return checks.All(item => item.Passed) ? 0 : 1;
        }
        var passed = Verify(recipePath, reportPath, out var summary);
        Console.WriteLine(summary);
        return passed ? 0 : 1;
    }
#endif
}
