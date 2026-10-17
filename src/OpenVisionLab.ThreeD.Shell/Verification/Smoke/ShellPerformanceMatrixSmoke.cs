using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Shell.Automation;
using OpenVisionLab.ThreeD.Shell.PropertyGrid;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Shell.Views.Workbench;
using OpenVisionLab.ThreeD.Viewer.Rendering;

namespace OpenVisionLab.ThreeD.Shell.Verification.Smoke;

internal static class ShellPerformanceMatrixSmoke
{
    public static async Task<bool> RunAsync(ShellSmokeScenarioContext context, string reportPath, CancellationToken cancellationToken)
    {
        var root = Path.GetDirectoryName(Path.GetFullPath(reportPath))!;
        Directory.CreateDirectory(root);
        using var config = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "performance-inputs.json")));
        var input = config.RootElement;
        string PathFor(string name) => input.GetProperty(name).GetString()!;
        var selectedGroups = input.TryGetProperty("groups", out var groupList)
            ? groupList.EnumerateArray().Select(item => item.GetString()!).ToHashSet(StringComparer.Ordinal) : null;
        bool Includes(string id) => selectedGroups is null || selectedGroups.Contains(id);
        int Repetitions(string name, int fallback) => input.TryGetProperty("repetitions", out var repeats)
            && repeats.TryGetProperty(name, out var count) ? count.GetInt32() : fallback;
        var shell = context.ViewModel;
        var workbench = shell.Workbench;
        var dispatcher = context.Window.Dispatcher;
        var rows = new List<object>();
        var heartbeatGaps = new List<double>();
        var lastTick = Stopwatch.GetTimestamp();
        EventHandler heartbeat = (_, _) =>
        {
            var now = Stopwatch.GetTimestamp();
            heartbeatGaps.Add(Stopwatch.GetElapsedTime(lastTick, now).TotalMilliseconds);
            lastTick = now;
        };
        var timer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Input, heartbeat, dispatcher);
        var failures = 0;
        var rawPath = Path.Combine(root, "samples.jsonl");
        using var raw = new StreamWriter(rawPath, append: false) { AutoFlush = true };
        void Write(object row) { rows.Add(row); raw.WriteLine(JsonSerializer.Serialize(row)); }
        async Task Settle()
        {
            await dispatcher.InvokeAsync(context.Window.UpdateLayout, DispatcherPriority.Loaded, cancellationToken);
            var frame = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            EventHandler rendered = (_, _) => frame.TrySetResult();
            // Continuous pane rendering can starve ContextIdle even while input is responsive.
            CompositionTarget.Rendering += rendered;
            try
            {
                context.Window.InvalidateVisual();
                await frame.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            }
            finally { CompositionTarget.Rendering -= rendered; }
        }
        string State() => JsonSerializer.Serialize(new
        {
            workbench.RecipePath, workbench.Source.Path, workbench.IsDirty, workbench.HasPendingStepParameterChanges,
            workbench.CurrentOrderedRunRecordPath,
            executions = workbench.RunLog.Count(item => item.Category is "Preview" or "Publish" or "Run")
        });
        async Task UntilIdle(Func<bool> running)
        {
            var deadline = Stopwatch.StartNew();
            while (running())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (deadline.Elapsed > TimeSpan.FromSeconds(60)) throw new TimeoutException("Operation did not return to idle within the smoke safety timeout.");
                await Task.Delay(10, cancellationToken);
            }
        }
        async Task Measure(string id, string action, int iteration, Func<Task> run, Func<bool> expected, Func<object>? detail = null)
        {
            await Settle();
            using var process = Process.GetCurrentProcess();
            var cpuBefore = process.TotalProcessorTime;
            heartbeatGaps.Clear();lastTick = Stopwatch.GetTimestamp();
            var started = Stopwatch.StartNew();
            double? operationMilliseconds = null;
            string? error = null;
            bool passed;
            try
            {
                await run();operationMilliseconds = started.Elapsed.TotalMilliseconds;
                if (id == "PF03")
                    await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background, cancellationToken).Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
                await Settle();passed = expected();
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                error = exception.ToString();passed = false;
            }
            started.Stop();
            var tailGap = Stopwatch.GetElapsedTime(lastTick).TotalMilliseconds;
            process.Refresh();
            if (!passed) failures++;
            Write(new
            {
                id, action, iteration, passed, elapsedMs = started.Elapsed.TotalMilliseconds,
                operationMs = operationMilliseconds, presentationMs = operationMilliseconds is { } completed ? started.Elapsed.TotalMilliseconds - completed : (double?)null,
                maximumDispatcherGapMs = Math.Max(tailGap, heartbeatGaps.Count == 0 ? 0 : heartbeatGaps.Max()),
                dispatcherTicks = heartbeatGaps.Count, cpuMs = (process.TotalProcessorTime - cpuBefore).TotalMilliseconds,
                privateBytes = process.PrivateMemorySize64, workingSetBytes = process.WorkingSet64,
                managedBytes = GC.GetTotalMemory(false), handles = process.HandleCount, threads = process.Threads.Count,
                workspace = shell.SelectedWorkspaceMode.ToString(), source = context.Viewer.CurrentC3DSourcePath,
                workbenchSource = workbench.Source.Path, viewerStatus = context.Viewer.HostState.ViewerStatus,
                loading = workbench.IsC3DSourceLoading, recipeMutationBlocked = workbench.IsRecipeMutationBlocked,
                workbench.LastStepSelectionMilliseconds, workbench.LastToolSelectionMilliseconds, workbench.LastRecipeRefreshMilliseconds,
                detail = detail?.Invoke(), error
            });
        }
        Task Action(Action action) { action();return Task.CompletedTask; }
        void Execute(System.Windows.Input.ICommand command, object? parameter = null)
        {
            if (!command.CanExecute(parameter)) throw new InvalidOperationException("The requested command is disabled in the current starting state.");
            command.Execute(parameter);
        }
        async Task Open(string path)
        {
            Execute(shell.SelectWorkspaceCommand, ShellWorkspaceMode.Teach);
            if (!workbench.TryOpenTeachingRecipe(path, out var error)) throw new InvalidOperationException(error);
            if (workbench.PipelineSteps.FirstOrDefault() is { } firstStep) Execute(workbench.SelectPipelineStepCommand, firstStep.Id);
            if (!await context.WorkbenchLifecycle.LoadWorkbenchC3DSourceAsync(PathFor("medium"), showFailureDialog: false, cancellationToken: cancellationToken))
                throw new InvalidOperationException("The owned medium fixture could not be loaded.");
            await Settle();
        }
        async Task NavigateWorkspaces(string state)
        {
            if (!Includes("PF01") && !(state == "with-results" && Includes("PF10"))) return;
            var before = State();
            for (var iteration = 0; iteration < 8; iteration++)
            foreach (var mode in new[] { ShellWorkspaceMode.Teach, ShellWorkspaceMode.Inspect, ShellWorkspaceMode.Validate, ShellWorkspaceMode.Review })
                await Measure("PF01", state + "/" + mode, iteration, () => Action(() => Execute(shell.SelectWorkspaceCommand, mode)),
                    () => shell.SelectedWorkspaceMode == mode && State() == before);
            Execute(shell.SelectWorkspaceCommand, ShellWorkspaceMode.Teach);
        }
        timer.Start();
        try
        {
            ShellWindowNativeInterop.AppendWindowMonitorEvidence(context.Window, Path.Combine(root, "window-monitors.txt"));
            Write(new
            {
                kind = "boundary", sourceCommit = typeof(ShellPerformanceMatrixSmoke).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                    .FirstOrDefault(item => item.Key == "OpenVisionSourceCommit")?.Value,
                method = "Actual Shell EXE, scripted bound commands/WPF controls; command-to-layout-and-next-WPF-frame, not Computer Use or photon latency.",
                timingPolicy = "Descriptive timing; no new performance pass threshold. Input-priority16ms dispatcher heartbeat includes OS, GC and renderer work.",
                dpi = VisualTreeHelper.GetDpi(context.Window).DpiScaleX, width = context.Window.ActualWidth, height = context.Window.ActualHeight,
                initialSource = context.Viewer.CurrentC3DSourcePath
            });
            await NavigateWorkspaces("empty");
            await Measure("PF09", "open-initial-recipe", 0, () => Open(PathFor("recipe")), () => workbench.PipelineSteps.Count > 0);
            if (Includes("PF09"))
            {
                await Measure("PF09", "recipe-center-open", 0, () => Action(context.WorkbenchLifecycle.ShowRecipeManagerWindow),
                    () => context.WorkbenchLifecycle.IsRecipeManagerVisible);
                if (context.WorkbenchLifecycle.RecipeManagerWindow is { } recipeCenter)
                    ShellWindowNativeInterop.AppendWindowMonitorEvidence(recipeCenter, Path.Combine(root, "recipe-center-monitors.txt"));
                await Measure("PF09", "recipe-center-close", 0, () => Action(context.WorkbenchLifecycle.CloseRecipeManager),
                    () => !context.WorkbenchLifecycle.IsRecipeManagerVisible);
            }
            await NavigateWorkspaces("loaded");

            var navigationState = State();
            if (Includes("PF02"))
            {
                for (var iteration = 0; iteration < Repetitions("viewer", 8); iteration++)
                {
                    foreach (var kind in new[] { ViewerPaneKind.Height, ViewerPaneKind.Section, ViewerPaneKind.ThreeD })
                        await Measure("PF02", "main-kind/" + kind, iteration, async () =>
                        {
                            Execute(workbench.MainViewerPane.SetKindCommand, kind);
                            if (kind == ViewerPaneKind.Height) await workbench.MainViewerPane.HeightImage.ObservedLoadTask;
                        }, () => workbench.MainViewerPane.Kind == kind && State() == navigationState);
                    foreach (var layout in new[] { "split", "four", "single" })
                        await Measure("PF02", "layout/" + layout, iteration, () => Action(() => Execute(layout switch
                        {
                            "split" => workbench.SplitViewerVerticallyCommand,
                            "four" => workbench.SetFourViewerLayoutCommand,
                            _ => workbench.SetSingleViewerLayoutCommand
                        })), () => State() == navigationState && (layout switch
                        {
                            "split" => workbench.IsSplitVerticalViewerLayout,
                            "four" => workbench.IsFourViewerLayout,
                            _ => workbench.IsSingleViewerLayout
                        }));
                }
            }
            if (Includes("PF03") || Includes("PF03-selection"))
            {
                for (var iteration = 0; iteration < Repetitions("selection", 8); iteration++)
                foreach (var step in workbench.PipelineSteps.ToArray())
                    await Measure("PF03", "step/" + step.Id, iteration, () => Action(() => Execute(workbench.SelectPipelineStepCommand, step.Id)),
                        () => ReferenceEquals(workbench.SelectedPipelineStep, step) && State() == navigationState);
            }

            if (Includes("PF03") || Includes("PF03-main"))
            {
                var measurementSettings = Descendants<SelectedToolWorkspaceView>(context.Workbench).Single(view => view.IsVisible);
                var measurementState = State();
                for (var iteration = 0; iteration < 3; iteration++)
                foreach (var name in new[] { "EditorRegionTab", "EditorAllTab", "EditorJudgmentTab", "EditorDetailTab" })
                    await Measure("PF03", "measurement-settings/" + name, iteration, () => Action(() =>
                    {
                        var button = (ButtonBase)measurementSettings.FindName(name);
                        if (!button.IsVisible || !button.IsEnabled) throw new InvalidOperationException("Requested measurement settings tab is not reachable.");
                        typeof(ButtonBase).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(button, null);
                    }), () => ((ToggleButton)measurementSettings.FindName(name)).IsChecked == true && State() == measurementState);
            }
            if (Includes("PF03") || Includes("PF03-range"))
            {
                await Open(PathFor("loadRecipe"));
                var settings = Descendants<SelectedToolWorkspaceView>(context.Workbench).Single(view => view.IsVisible);
                var settingsState = State();
                for (var iteration = 0; iteration < (Includes("PF03") ? 8 : 1); iteration++)
                foreach (var name in new[] { "EditorAllTab", "EditorDetailTab", "DetailIoTab", "DetailProcessingTab", "DetailHelpTab", "DetailParametersTab" })
                    await Measure("PF03", "settings/" + name, iteration, () => Action(() =>
                    {
                        var button = (ButtonBase)settings.FindName(name);
                        if (!button.IsVisible || !button.IsEnabled) throw new InvalidOperationException("Requested settings tab is not reachable.");
                        typeof(ButtonBase).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(button, null);
                    }), () => ((ToggleButton)settings.FindName(name)).IsChecked == true && State() == settingsState);
                var grid = (RecipeStepPropertyGridHost)settings.FindName("StepPropertyGrid");
                var page = (ComboBox)grid.FindName("ParameterPageSelector");
                await Settle();await Settle();
                await Measure("PF03", "parameter-range-ready", 0, () => Task.CompletedTask,
                    () => workbench.SelectedPipelineStep?.ToolId == "remove-outlier-pixels" && !grid.PrimaryParametersOnly && page.Items.Count > 1,
                    () => new { tool = workbench.SelectedPipelineStep?.ToolId, grid.PrimaryParametersOnly, grid.VisiblePropertyCount, pages = page.Items.Count,
                        selectedObject = grid.SelectedObject?.GetType().FullName, detailChecked = ((RadioButton)settings.FindName("EditorDetailTab")).IsChecked });
                for (var iteration = 0; iteration < 8; iteration++)
                for (var index = 0; index < page.Items.Count; index++)
                {
                    var expectedIndex = index;
                    await Measure("PF03", "parameter-range/" + index, iteration,
                        () => Action(() => page.SetCurrentValue(Selector.SelectedIndexProperty, expectedIndex)),
                        () => page.SelectedIndex == expectedIndex && State() == settingsState);
                }
            }

            if (Includes("PF04"))
            {
                await Open(PathFor("loadRecipe"));
                for (var iteration = 0; iteration < Repetitions("load", 3); iteration++)
                foreach (var source in input.GetProperty("sources").EnumerateArray())
                {
                    var path = source.GetProperty("path").GetString()!;
                    var loaded = false;
                    string? immediatelyAfterLoad = null;
                    var alreadyActive = string.Equals(context.Viewer.CurrentC3DSourcePath, path, StringComparison.OrdinalIgnoreCase);
                    await Measure("PF04", source.GetProperty("id").GetString()!, iteration,
                        async () =>
                        {
                            loaded = await context.WorkbenchLifecycle.LoadWorkbenchC3DSourceAsync(path, showFailureDialog: false, cancellationToken: cancellationToken);
                            immediatelyAfterLoad = context.Viewer.CurrentC3DSourcePath;
                        },
                        () => loaded && !workbench.IsC3DSourceLoading && string.Equals(context.Viewer.CurrentC3DSourcePath, path, StringComparison.OrdinalIgnoreCase),
                        () => new { bytes = source.GetProperty("bytes").GetInt64(), loaded, alreadyActive, immediatelyAfterLoad,
                            performance = alreadyActive || !loaded ? null : context.Viewer.LastC3DSourceLoadPerformance,
                            sourceState = workbench.LastC3DSourceStatePerformance });
                    if (source.TryGetProperty("texture", out var texture))
                    {
                        var attached = false;
                        await Measure("PF04", source.GetProperty("id").GetString()! + "/texture", iteration,
                            () => Action(() => attached = context.Viewer.TryLoadC3DTexture(texture.GetString()!)),
                            () => attached && context.Viewer.HostState.Presentation.HasC3DTexture
                                && string.Equals(context.Viewer.CurrentC3DSourcePath, path, StringComparison.OrdinalIgnoreCase));
                    }
                }
            }

            bool returned = true;
            if (Includes("PF05"))
            {
                await Open(PathFor("recipe"));
                var previous = context.Viewer.CurrentC3DSourcePath;
                var canceled = false;
                await Measure("PF05", "cancel-large-retain-previous", 0, async () =>
                {
                    var load = context.WorkbenchLifecycle.LoadWorkbenchC3DSourceAsync(PathFor("large"), showFailureDialog: false, cancellationToken: cancellationToken);
                    await Task.Delay(20, cancellationToken);
                    canceled = workbench.IsC3DSourceLoading && workbench.CancelC3DSourceLoadCommand.CanExecute(null);
                    if (canceled) workbench.CancelC3DSourceLoadCommand.Execute(null);
                    returned = await load;
                }, () => canceled && !returned && !workbench.IsC3DSourceLoading && context.Viewer.CurrentC3DSourcePath == previous);
                await Measure("PF05", "replace-pending-large-with-medium", 0, async () =>
                {
                    var first = context.WorkbenchLifecycle.LoadWorkbenchC3DSourceAsync(PathFor("large"), showFailureDialog: false, cancellationToken: cancellationToken);
                    await Task.Delay(20, cancellationToken);
                    var next = context.WorkbenchLifecycle.LoadWorkbenchC3DSourceAsync(PathFor("medium"), showFailureDialog: false, cancellationToken: cancellationToken);
                    await Task.WhenAll(first, next);
                }, () => !workbench.IsC3DSourceLoading && context.Viewer.CurrentC3DSourcePath == PathFor("medium"));
                previous = context.Viewer.CurrentC3DSourcePath;
                await Measure("PF05", "truncated-load-retain-previous", 0,
                    async () => returned = await context.WorkbenchLifecycle.LoadWorkbenchC3DSourceAsync(PathFor("malformed"), showFailureDialog: false, cancellationToken: cancellationToken),
                    () => !returned && !workbench.IsC3DSourceLoading && context.Viewer.CurrentC3DSourcePath == previous);
                await Measure("PF05", "valid-retry-after-load-failure", 0,
                    async () => returned = await context.WorkbenchLifecycle.LoadWorkbenchC3DSourceAsync(PathFor("medium"), showFailureDialog: false, cancellationToken: cancellationToken),
                    () => returned && !workbench.IsC3DSourceLoading && context.Viewer.CurrentC3DSourcePath == PathFor("medium"));
            }

            if (Includes("PF06"))
            {
                foreach (var name in new[] { "recipe", "operationRecipe" })
                {
                    await Open(PathFor(name));
                    for (var iteration = 0; iteration < Repetitions("run", 3); iteration++)
                    {
                        var duplicateRejected = false;
                        await Measure("PF06", "explicit-run/" + name, iteration, async () =>
                        {
                            Execute(workbench.RunTeachingRecipeCommand);
                            duplicateRejected = !workbench.RunTeachingRecipeCommand.CanExecute(null);
                            await UntilIdle(() => workbench.IsOrderedRunRunning);
                        }, () => duplicateRejected && workbench.CurrentOrderedRunRecord is { Status: not ResultStatus.Error } && !workbench.IsDirty,
                            () => new { duplicateRejected, status = workbench.CurrentOrderedRunRecord?.Status.ToString(), rows = workbench.TeachingResults.Count, record = workbench.CurrentOrderedRunRecordPath });
                    }
                }
            }
            if (Includes("PF07"))
            {
                await Open(PathFor("recipe"));
                Execute(shell.SelectWorkspaceCommand, ShellWorkspaceMode.Inspect);
                workbench.InspectionQueue.AddSources(input.GetProperty("batchSources").EnumerateArray().Select(item => item.GetString()!));
                await Measure("PF07", "queue-stop", 0, async () =>
                {
                    Execute(workbench.InspectionQueue.RunAllCommand);
                    await Task.Delay(25, cancellationToken);
                    Execute(workbench.InspectionQueue.StopCommand);
                    await UntilIdle(() => workbench.InspectionQueue.IsRunning);
                }, () => workbench.InspectionQueue.IsIdle && workbench.InspectionQueue.Samples.Any(item => item.State == InspectionQueueItemState.Cancelled));
                await Measure("PF07", "queue-valid-and-truncated-retry", 0, async () =>
                {
                    Execute(workbench.InspectionQueue.RunAllCommand);
                    await UntilIdle(() => workbench.InspectionQueue.IsRunning);
                }, () => workbench.InspectionQueue.IsIdle && workbench.InspectionQueue.Samples.Count == 5
                    && workbench.InspectionQueue.Samples.Count(item => item.State == InspectionQueueItemState.Error) == 1
                    && workbench.InspectionQueue.Samples.All(item => item.State is InspectionQueueItemState.Pass or InspectionQueueItemState.Fail or InspectionQueueItemState.Error),
                    () => workbench.InspectionQueue.Samples.Select(item => new { item.FileName, state = item.State.ToString(), item.RunRecordPath }).ToArray());
                for (var iteration = 0; iteration < 3; iteration++)
                foreach (var sample in workbench.InspectionQueue.Samples)
                    await Measure("PF07", "queue-select/" + sample.FileName, iteration, () => Action(() => workbench.InspectionQueue.SelectedSample = sample),
                        () => ReferenceEquals(workbench.InspectionQueue.SelectedSample, sample) && !workbench.InspectionQueue.IsRunning);
            }

            if (Includes("PF08"))
            {
                Execute(shell.SelectWorkspaceCommand, ShellWorkspaceMode.Teach);
                await Open(PathFor("recipe"));
                workbench.SetValidationSetSources(input.GetProperty("batchSources").EnumerateArray().Select(item => item.GetString()!));
                Execute(shell.SelectWorkspaceCommand, ShellWorkspaceMode.Validate);
                await Measure("PF08", "validation-stop", 0, async () =>
                {
                    Execute(workbench.RunValidationSetCommand);
                    await Task.Delay(25, cancellationToken);
                    Execute(workbench.CancelValidationSetCommand);
                    await UntilIdle(() => workbench.IsValidationSetRunning);
                }, () => !workbench.IsValidationSetRunning && workbench.RunValidationSetCommand.CanExecute(null));
                for (var iteration = 0; iteration < 2; iteration++)
                    await Measure("PF08", "validation-retry", iteration, async () =>
                    {
                        Execute(workbench.RunValidationSetCommand);
                        if (workbench.RunValidationSetCommand.CanExecute(null)) throw new InvalidOperationException("Validation accepted an additional run while running.");
                        await UntilIdle(() => workbench.IsValidationSetRunning);
                    }, () => !workbench.IsValidationSetRunning && workbench.HasValidationResults && workbench.ValidationSetSamples.Count == 5);
            }

            if (Includes("PF09"))
            {
                await Open(PathFor("recipe"));
                for (var iteration = 0; iteration < Repetitions("save", 5); iteration++)
                {
                    var path = Path.Combine(root, "saved.ov3d-recipe.json");
                    await Measure("PF09", "save", iteration, () => Action(() =>
                    {
                        if (!workbench.TrySaveTeachingRecipe(path, out var error)) throw new IOException(error);
                    }), () => !workbench.IsDirty && File.Exists(path));
                    var executions = workbench.RunLog.Count(item => item.Category is "Preview" or "Publish" or "Run");
                    await Measure("PF09", "reopen", iteration, () => Action(() =>
                    {
                        if (!workbench.TryOpenTeachingRecipe(path, out var error)) throw new IOException(error);
                    }), () => !workbench.IsDirty && !workbench.IsOrderedRunRunning
                        && workbench.RunLog.Count(item => item.Category is "Preview" or "Publish" or "Run") == executions);
                }
                var retained = State();
                await Measure("PF09", "malformed-recipe-retains-work", 0,
                    () => Action(() => returned = workbench.TryOpenTeachingRecipe(PathFor("malformedRecipe"), out _)), () => !returned && State() == retained);
                var directoryPath = Path.Combine(root, "save-target-is-directory.ov3d-recipe.json");
                Directory.CreateDirectory(directoryPath);
                await Measure("PF09", "save-failure-retains-work", 0,
                    () => Action(() => returned = workbench.TrySaveTeachingRecipe(directoryPath, out _)), () => !returned && State() == retained);
                await Measure("PF09", "recover-save-after-failure", 0,
                    () => Action(() => returned = workbench.TrySaveTeachingRecipe(Path.Combine(root, "recovered.ov3d-recipe.json"), out _)), () => returned && !workbench.IsDirty);
            }

            if (Includes("PF10"))
            {
                await Open(PathFor("recipe"));
                Execute(workbench.RunTeachingRecipeCommand);await UntilIdle(() => workbench.IsOrderedRunRunning);
                var resultState = State();
                for (var iteration = 0; iteration < 20; iteration++)
                foreach (var section in new[] { ResultsWorkspaceSection.RunRecord, ResultsWorkspaceSection.Section, ResultsWorkspaceSection.Log })
                    await Measure("PF10", "result-section/" + section, iteration,
                        () => Action(() => Execute(shell.ResultsWorkspace.SelectSectionCommand, section.ToString())),
                        () => shell.ResultsWorkspace.ActiveSection == section && State() == resultState);
                var recordPath = workbench.CurrentOrderedRunRecordPath!;
                Execute(shell.SelectWorkspaceCommand, ShellWorkspaceMode.Review);
                for (var iteration = 0; iteration < 3; iteration++)
                {
                    await Measure("PF10", "open-record", iteration, () => Action(() =>
                    {
                        if (!shell.LoadRunRecord(recordPath, out var error)) throw new IOException(error);
                    }), () => shell.ResultsWorkspace.HasOpenedRecord && State() == resultState);
                    foreach (var name in new[] { "Trend", "Failures", "Report" })
                        await Measure("PF10", "history/" + name, iteration,
                            () => Action(() => Execute(shell.ResultsWorkspace.SelectHistorySectionCommand, name)),
                            () => shell.ResultsWorkspace.HistorySection.ToString() == name && State() == resultState);
                }
                await NavigateWorkspaces("with-results");
            }
            Execute(shell.SelectWorkspaceCommand, ShellWorkspaceMode.Teach);
            Execute(shell.ResultsWorkspace.SelectSectionCommand, "RunRecord");await Settle();
            WpfScreenshotCapture.Save(WpfScreenshotCapture.Capture(context.Window).Bitmap, Path.Combine(root, "final-context.png"));
        }
        catch (Exception exception)
        {
            failures++;Write(new { kind = "matrix-aborted", error = exception.ToString() });
        }
        finally
        {
            timer.Stop();timer.Tick -= heartbeat;
            File.WriteAllText(reportPath, JsonSerializer.Serialize(new { passed = failures == 0, failures, selectedGroups, rows, boundary = "Bounded local scripted performance investigation; no SDK qualification, whole-View acceptance, physical latency or long-duration leak guarantee." }, new JsonSerializerOptions { WriteIndented = true }));
        }
        return failures == 0;
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject owner) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(owner); index++)
        {
            var child = VisualTreeHelper.GetChild(owner, index);
            if (child is T match) yield return match;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }
}
