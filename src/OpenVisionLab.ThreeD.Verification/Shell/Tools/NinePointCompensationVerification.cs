using System.Globalization;
using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Reporting.RunRecords;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Tools;
using OpenVisionLab.ThreeD.Viewer.Hosting;
using OpenVisionLab.ThreeD.Viewer.Teaching;
using OpenVisionLab.Vision3D.Geometry;

namespace OpenVisionLab.ThreeD.Verification.Shell.Tools;

internal static class NinePointCompensationVerification
{
    public static bool VerifyLinkedWarpWorkbench(string fixturePath, string reportPath, out string summary)
    {
        var lines = new List<string> { "Linked warped-map Workbench regression; no Window, GUI input or Viewer created." };
        var passed = 0;
        var total = 0;
        void Check(string name, bool ok, string detail = "") { total++; if (ok) passed++; lines.Add($"{(ok ? "PASS" : "FAIL")} | {name} | {detail}"); }
        var root = Path.GetDirectoryName(Path.GetFullPath(reportPath))!;
        Directory.CreateDirectory(root);
        var previousRoot = Environment.GetEnvironmentVariable("OPENVISIONLAB_3D_TEST_ARTIFACT_ROOT");
        Environment.SetEnvironmentVariable("OPENVISIONLAB_3D_TEST_ARTIFACT_ROOT", root);
        try
        {
            using var workbench = new ToolWorkbenchViewModel(Path.Combine(root, "linked-recent.json"));
            if (!workbench.TryOpenTeachingRecipe(fixturePath, out var message)) throw new InvalidDataException(message);
            Check("opening a linked recipe does not execute", workbench.CurrentOrderedRunResult is null
                && !workbench.TryGetPublishedTeachingHeightField("output.flat-coupons", out _, out _));
            var compareRefreshes = 0;
            workbench.CompareCandidates.CollectionChanged += (_, _) =>
                workbench.UpdateTeachingSelectionCaptureState(false, 0, 0, false, string.Empty);
            workbench.CompareCandidates.CollectionChanged += (_, _) => compareRefreshes++;
            var sourceId = workbench.Source.Id;
            var sourcePath = workbench.Source.Path;
            var ran = workbench.RunTeachingRecipeAsync().GetAwaiter().GetResult();
            Check("one full run executes all twelve linked steps", ran && workbench.CurrentOrderedRunResult?.Steps.Count == 12,
                workbench.OrderedRunCapabilitySummary);
            Check("full run publishes the actual compensation without producer rerun", workbench.TryGetPublishedTeachingHeightField(
                "output.flat-coupons", out var output, out var outputPath) && output is not null && File.Exists(outputPath),
                workbench.ArtifactRegistry.FirstOrDefault(item => item.Id == "output.flat-coupons")?.State ?? "missing");
            if (output is not null && outputPath is not null)
            {
                var loaded = C3DHeightFieldSnapshot.LoadIdentified(outputPath, output.EntityId, output.Unit, output.FrameId);
                var executed = workbench.CurrentOrderedRunResult!.Steps.Single(step => step.StepId == "step.flatten");
                Check("published bytes are exactly the ordered SDK output", loaded.ContentSha256 == executed.OutputContentSha256
                    && output.ContentSha256 == executed.OutputContentSha256 && output.RootSourceSha256 == workbench.CurrentOrderedRunResult.SourceContentSha256);
            }
            Check("Viewer capture callbacks do not reenter compare collection updates", compareRefreshes > 0);
            Check("completed output refreshes Save and Save As readiness", workbench.CanSaveTeachingRecipe
                && workbench.SaveTeachingRecipeCommand.CanExecute(null) && workbench.SaveTeachingRecipeAsCommand.CanExecute(null), workbench.ValidationSummary);
            Check("completed output removes downstream waiting and stale ROI diagnostics", !workbench.FlowPortDiagnostics.Any(item => item.Port == "Input"),
                string.Join(" | ", workbench.FlowPortDiagnostics.Select(item => item.Detail)));
            var firstMetrics = workbench.CurrentOrderedRunResult!.Steps.SelectMany(step => step.Result.Metrics).ToArray();
            workbench.SelectPipelineStep("step.warp.after");
            Check("downstream measurement is ready immediately after full run", workbench.RunSelectedStepCommand.CanExecute(null));
            var measured = workbench.PreviewSelectedMeasurementAsync().GetAwaiter().GetResult();
            Check("downstream preview consumes the same compensated map", measured && workbench.CurrentMeasurementOutput?.InputEntityId == "output.flat-coupons",
                workbench.MeasurementExecutionSummary);
            Check("linked execution preserves the raw input", workbench.Source.Id == sourceId && workbench.Source.Path == sourcePath);
            var notifiedRows = new List<int>();
            System.ComponentModel.PropertyChangedEventHandler rowsChanged = (_, args) =>
            {
                if (args.PropertyName == nameof(workbench.NinePointCompensationRows)) notifiedRows.Add(workbench.NinePointCompensationRows.Count);
            };
            workbench.PropertyChanged += rowsChanged;
            workbench.SelectPipelineStep("step.flatten");
            workbench.PropertyChanged -= rowsChanged;
            Check("selection notifies bindings after restoring ordered compensation", notifiedRows.Count > 0 && notifiedRows[^1] == 9,
                string.Join(",", notifiedRows));
            Check("ordered compensation exposes nine reference rows without execution", workbench.NinePointCompensationRows.Count == 9
                && workbench.NinePointPreviewFrame is { Width: 641, Height: 481 });
            var saved = Path.Combine(root, "linked-saved.ov3d-recipe.json");
            bool? saveRunReadiness = null;
            workbench.RunTeachingRecipeCommand.CanExecuteChanged += (_, _) => saveRunReadiness = workbench.RunTeachingRecipeCommand.CanExecute(null);
            Check("save preserves the linked recipe", workbench.TrySaveTeachingRecipe(saved, out message), message);
            Check("Save As notifies the actual full-run button after saving ends", saveRunReadiness == true && workbench.RunTeachingRecipeCommand.CanExecute(null));
            using (var reopened = new ToolWorkbenchViewModel(Path.Combine(root, "reopened-recent.json")))
            {
                bool? openRunReadiness = null;
                reopened.RunTeachingRecipeCommand.CanExecuteChanged += (_, _) => openRunReadiness = reopened.RunTeachingRecipeCommand.CanExecute(null);
                if (!reopened.TryOpenTeachingRecipe(saved, out message)) throw new InvalidDataException(message);
                Check("Open notifies the full-run button after accepting the saved identity", openRunReadiness == true && reopened.RunTeachingRecipeCommand.CanExecute(null));
                Check("reopen restores wiring without running", reopened.CurrentOrderedRunResult is null
                    && !reopened.TryGetPublishedTeachingHeightField("output.flat-coupons", out _, out _));
                var replayed = reopened.RunTeachingRecipeAsync().GetAwaiter().GetResult();
                Check("one explicit replay restores output and all measured values", replayed
                    && reopened.TryGetPublishedTeachingHeightField("output.flat-coupons", out var restored, out _)
                    && restored?.ContentSha256 == output?.ContentSha256
                    && reopened.CurrentOrderedRunResult!.Steps.SelectMany(step => step.Result.Metrics)
                        .SequenceEqual(firstMetrics));
            }
            using (var context = new CompletionQueue())
            {
                var previous = SynchronizationContext.Current;
                SynchronizationContext.SetSynchronizationContext(context);
                try
                {
                    var canceled = workbench.RunTeachingRecipeAsync();
                    Check("a new full run withdraws the preceding published map", workbench.IsOrderedRunRunning
                        && !workbench.TryGetPublishedTeachingHeightField("output.flat-coupons", out _, out _));
                    workbench.CancelTeachingRecipeCommand.Execute(null);
                    var settled = context.DrainUntil(() => canceled.IsCompleted);
                    Check("cancel rejects late outputs and leaves no run record", settled && !canceled.GetAwaiter().GetResult()
                        && workbench.CurrentOrderedRunResult is null && workbench.CurrentOrderedRunRecord is null
                        && !workbench.TryGetPublishedTeachingHeightField("output.flat-coupons", out _, out _));
                }
                finally { SynchronizationContext.SetSynchronizationContext(previous); }
            }
            Check("valid retry recovers the complete linked workflow", workbench.RunTeachingRecipeAsync().GetAwaiter().GetResult()
                && workbench.TryGetPublishedTeachingHeightField("output.flat-coupons", out var recovered, out _)
                && recovered?.ContentSha256 == output?.ContentSha256);
            var invalid = ToolRecipeDocumentStore.Load(saved);
            invalid = invalid with { Steps = invalid.Steps.Select(step => step.Id != "step.flatten" ? step : step with
            {
                Parameters = step.Parameters!.Select(parameter => parameter.Name != "MiddleRow" ? parameter : parameter with { Value = "0" }).ToArray()
            }).ToArray() };
            var invalidPath = Path.Combine(root, "invalid-reference.ov3d-recipe.json");
            ToolRecipeDocumentStore.Save(invalidPath, invalid);
            if (!workbench.TryOpenTeachingRecipe(invalidPath, out message)) throw new InvalidDataException(message);
            var invalidRan = workbench.RunTeachingRecipeAsync().GetAwaiter().GetResult();
            Check("invalid grid stops before downstream inspection and exposes no stale map", !invalidRan
                && workbench.CurrentOrderedRunResult?.Status == ResultStatus.Error
                && workbench.CurrentOrderedRunResult.Steps.Count == 2
                && !workbench.TryGetPublishedTeachingHeightField("output.flat-coupons", out _, out _), workbench.OrderedRunCapabilitySummary);
            if (!workbench.TryOpenTeachingRecipe(saved, out message)) throw new InvalidDataException(message);
            Check("valid reopen recovers after the controlled error", workbench.RunTeachingRecipeAsync().GetAwaiter().GetResult()
                && workbench.TryGetPublishedTeachingHeightField("output.flat-coupons", out _, out _));
        }
        catch (Exception exception) { lines.Add(exception.ToString()); Check("linked Workbench flow", false); }
        finally { Environment.SetEnvironmentVariable("OPENVISIONLAB_3D_TEST_ARTIFACT_ROOT", previousRoot); }
        summary = $"Linked warp Workbench: {passed}/{total}";
        lines.Add(summary);
        File.WriteAllLines(reportPath, lines);
        return passed == total;
    }

    public static bool Verify(string reportPath, out string summary)
    {
        var lines = new List<string> { "Nine-point Studio integration; no Application, Window, browser or Viewer created." };
        var passed = 0;
        var total = 0;
        var root = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(reportPath))!, "nine-point-fixtures");
        var previousRoot = Environment.GetEnvironmentVariable("OPENVISIONLAB_3D_TEST_ARTIFACT_ROOT");
        void Check(string name, bool condition, string detail)
        {
            total++;
            if (condition) passed++;
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
        }
        try
        {
            Directory.CreateDirectory(root);
            Environment.SetEnvironmentVariable("OPENVISIONLAB_3D_TEST_ARTIFACT_ROOT", root);
            var path = Path.Combine(root, "synthetic-substrate.c3d");
            var values = Enumerable.Range(0, 81).Select(index =>
            {
                var row = index / 9;
                var column = index % 9;
                return 100 + 2 * row + 3 * column + 0.125 * row * column + (row == 3 && column == 3 ? 5.25 : 0);
            }).ToArray();
            values[10] = double.NaN;
            C3DHeightFieldSnapshot.CreateForVerification("fixture", 9, 9, values).SaveC3D(path);
            var sourceBytes = File.ReadAllBytes(path);
            var source = C3DHeightFieldSnapshot.LoadIdentified(path, "source.nine", "raw-height", "frame.c3d-grid-index");
            var document = CreateDocument(source, path);
            var direct = ToolRecipeNinePointSurfaceCompensationExecution.Execute(document, "step.nine", source);
            var output = direct.Output ?? throw new InvalidDataException(direct.Result.Message);
            Check("adapter delegates compensation and preserves the local height", direct.Result.Status == ResultStatus.Pass
                && Math.Abs(output.Values.Span[30] - 25.25) < 1e-10 && Math.Abs(output.Values.Span[20] - 20) < 1e-10
                && double.IsNaN(output.Values.Span[10]) && direct.Numerical?.ControlPoints.Count == 9,
                $"local={output.Values.Span[30]:R};bare={output.Values.Span[20]:R};points={direct.Numerical?.ControlPoints.Count}");
            Check("typed output retains source identity and XY frame", output.EntityId == "output.nine"
                && output.FrameId == source.FrameId && output.RootSourceSha256 == source.ContentSha256
                && output.Unit == source.Unit && output.Width == 9 && output.Height == 9,
                $"id={output.EntityId};frame={output.FrameId};root={output.RootSourceSha256}");

            var recipePath = Path.Combine(root, "nine.ov3d-recipe.json");
            ToolRecipeDocumentStore.Save(recipePath, document);
            var loaded = ToolRecipeDocumentStore.Load(recipePath);
            Check("existing recipe schema retains four explicit parameters", loaded.SchemaVersion == document.SchemaVersion
                && loaded.Steps[0].Parameters!.SequenceEqual(document.Steps[0].Parameters!), loaded.SchemaVersion);
            var replay = ToolRecipeOrderedGraphExecution.Execute(loaded, path);
            Check("ordered recipe route consumes the committed SDK adapter", replay.Status == ResultStatus.Pass
                && replay.Steps.Count == 1 && replay.Steps[0].Result.Metrics.Count == 76
                && replay.Steps[0].SemanticIdentityEvidence?.AlgorithmDefinitionVersion == ToolRecipeNinePointSurfaceCompensationExecution.Semantics,
                $"{replay.Status};steps={replay.Steps.Count};{replay.Message}");

            var invalid = document with { Steps = [document.Steps[0] with
                { Parameters = Parameters(0, 20) }] };
            var rejected = ToolRecipeNinePointSurfaceCompensationExecution.Execute(invalid, "step.nine", source);
            var recovered = ToolRecipeNinePointSurfaceCompensationExecution.Execute(document, "step.nine", source);
            Check("invalid reference grid has no output and valid retry recovers", rejected.Result.Status == ResultStatus.Error
                && rejected.Output is null && recovered.Output?.ContentSha256 == output.ContentSha256,
                $"invalid={rejected.Result.Status};retry={recovered.Result.Status};{rejected.Result.Message}");
            var missingValues = source.Values.ToArray(); missingValues[0] = double.NaN;
            var missing = source.CreateDerived(source.EntityId, missingValues, "test-reference-missing");
            var missingResult = ToolRecipeNinePointSurfaceCompensationExecution.Execute(document, "step.nine", missing);
            Check("missing reference is controlled and never publishes stale output", missingResult.Result.Status == ResultStatus.Error
                && missingResult.Output is null && missingResult.Numerical is { Success: false }, missingResult.Result.Message);
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                var canceled = false;
                try { ToolRecipeNinePointSurfaceCompensationExecution.Execute(document, "step.nine", source, cancellationToken: cancellation.Token); }
                catch (OperationCanceledException) { canceled = true; }
                Check("cancellation propagates and a fresh run recovers", canceled
                    && ToolRecipeNinePointSurfaceCompensationExecution.Execute(document, "step.nine", source).Result.Status == ResultStatus.Pass,
                    $"canceled={canceled};source bytes checked at end");
            }

            var zeroFrame = C3DHeightImageFrame.CreateScalarMap(new HeightMap3D(3, 3, 2, 4, 1, 1,
                new double[] { 0, 1, 2, 3, double.NaN, 5, 6, 7, 8 }, "grid-index", "raw-height", source.FrameId, "background.zero"));
            Check("background evidence retains finite zero and native grid origin", zeroFrame.ValidCount == 8
                && zeroFrame.MissingCount == 1 && zeroFrame.DisplayValues.Span[0] == 0
                && zeroFrame.GridOriginColumn == 2 && zeroFrame.GridOriginRow == 4,
                $"valid={zeroFrame.ValidCount};missing={zeroFrame.MissingCount};origin={zeroFrame.GridOriginColumn},{zeroFrame.GridOriginRow}");

            using (var workbench = new ToolWorkbenchViewModel(Path.Combine(root, "recent.json")))
            {
                Check("open does not execute the new tool", workbench.TryOpenTeachingRecipe(recipePath, out var message)
                    && workbench.SelectPipelineStep("step.nine") && workbench.NinePointCompensationRows.Count == 0, message);
                Check("new tool uses the existing typed PropertyGrid", workbench.SelectedStepPropertyDraft is NinePointSurfaceCompensationStepProperties,
                    workbench.SelectedStepPropertyDraft?.GetType().Name ?? "null");
                var displayCount = 0;
                workbench.FilterDisplayRequested += (_, _) => displayCount++;
                var canRun = workbench.RunSelectedStepCommand.CanExecute(null);
                workbench.RunSelectedStepCommand.Execute(null);
                var settled = SpinWait.SpinUntil(() => !workbench.IsSelectedStepPreviewRunning, TimeSpan.FromSeconds(15));
                var published = workbench.ArtifactRegistry.FirstOrDefault(item => item.Id == "output.nine");
                Check("one explicit Run publishes one output and nine bound rows", canRun && settled && displayCount == 1
                    && published is { State: "Published", Contract: "HeightField" } && workbench.NinePointCompensationRows.Count == 9,
                    $"canRun={canRun};settled={settled};display={displayCount};state={published?.State};rows={workbench.NinePointCompensationRows.Count};{workbench.NinePointCompensationSummary}");
                foreach (var kind in new[] { "Source", "Background", "Corrected" })
                {
                    workbench.NinePointPreviewKind = kind;
                    Check($"{kind} map uses the same pixel grid", workbench.NinePointPreviewFrame is { Width: 9, Height: 9 }
                        && workbench.NinePointCompensationRows[8].PixelX == 8.5 && workbench.NinePointCompensationRows[8].PixelY == 8.5,
                        workbench.NinePointPreviewFrame?.PixelSha256 ?? "none");
                }
                var middle = workbench.SelectedPipelineStep!.Parameters.Single(parameter => parameter.Name == "MiddleRow");
                middle.Value = "0";
                workbench.RunSelectedStepCommand.Execute(null);
                SpinWait.SpinUntil(() => !workbench.IsSelectedStepPreviewRunning, TimeSpan.FromSeconds(15));
                Check("Workbench error clears the previous output", workbench.NinePointCompensationRows.Count == 0
                    && workbench.ArtifactRegistry.First(item => item.Id == "output.nine").State == "Declared"
                    && !string.IsNullOrEmpty(workbench.NinePointCompensationSummary), workbench.NinePointCompensationSummary);
                middle.Value = "4";
                var previousContext = SynchronizationContext.Current;
                using (var context = new CompletionQueue())
                {
                    SynchronizationContext.SetSynchronizationContext(context);
                    try
                    {
                        workbench.RunSelectedStepCommand.Execute(null);
                        var inFlight = workbench.IsSelectedStepPreviewRunning && !workbench.RunSelectedStepCommand.CanExecute(null);
                        workbench.RunSelectedStepCommand.Execute(null);
                        workbench.CancelSelectedPreviewCommand.Execute(null);
                        var canceled = context.DrainUntil(() => !workbench.IsSelectedStepPreviewRunning);
                        Check("in-flight cancel and repeat command publish no output", inFlight && canceled && displayCount == 1
                            && workbench.NinePointCompensationRows.Count == 0, $"inFlight={inFlight};settled={canceled};display={displayCount};{workbench.NinePointCompensationSummary}");
                        workbench.RunSelectedStepCommand.Execute(null);
                        var retried = context.DrainUntil(() => !workbench.IsSelectedStepPreviewRunning);
                        Check("valid retry after Workbench cancel and error recovers", retried && displayCount == 2
                            && workbench.NinePointCompensationRows.Count == 9, workbench.NinePointCompensationSummary);
                    }
                    finally { SynchronizationContext.SetSynchronizationContext(previousContext); }
                }
                Check("save retains the recipe and source selection", workbench.TrySaveTeachingRecipe(recipePath, out var saveMessage)
                    && workbench.Source.Id == source.EntityId && workbench.SelectedPipelineStep?.InputEntityIds[0] == source.EntityId, saveMessage);
            }
            using (var reopened = new ToolWorkbenchViewModel(Path.Combine(root, "reopened-recent.json")))
            {
                var events = 0;
                reopened.FilterDisplayRequested += (_, _) => events++;
                var opened = reopened.TryOpenTeachingRecipe(recipePath, out var message);
                reopened.SelectPipelineStep("step.nine");
                Check("reopen restores conditions without executing or publishing", opened && events == 0
                    && reopened.NinePointCompensationRows.Count == 0
                    && reopened.ArtifactRegistry.First(item => item.Id == "output.nine").State == "Declared", message);
                reopened.RunSelectedStepCommand.Execute(null);
                SpinWait.SpinUntil(() => !reopened.IsSelectedStepPreviewRunning, TimeSpan.FromSeconds(15));
                Check("explicit Run after reopen reproduces the output", events == 1
                    && reopened.ArtifactRegistry.First(item => item.Id == "output.nine").ContentSha256 == output.ContentSha256,
                    reopened.NinePointCompensationSummary);
            }
            var binding = ToolRecipeSelectionSourceBindingVerifier.FromHeightField(output);
            var reference = new ToolRecipeSelection("roi.reference", "Bare substrate", ToolRecipeSelectionKinds.GridRectangle,
                source.EntityId, output.FrameId, binding, new ToolRecipeGridRectangle(5, 5, 2, 2), null, null);
            var measurement = new ToolRecipeSelection("roi.local", "Local feature", ToolRecipeSelectionKinds.GridRectangle,
                source.EntityId, output.FrameId, binding, new ToolRecipeGridRectangle(3, 3, 1, 1), null, null);
            var heightDocument = document with
            {
                Steps = [document.Steps[0], new("step.height", "thickness", "Local height", 3,
                    [output.EntityId, reference.Id, measurement.Id], "result.height",
                    [new("MinimumThickness", "5.24"), new("MaximumThickness", "5.26"), new("MinimumValidSampleCount", "1")])],
                Selections = [reference, measurement]
            };
            var heightReplay = ToolRecipeOrderedGraphExecution.Execute(heightDocument, path);
            Check("ordered compensation feeds the existing height measurement", heightReplay.Status == ResultStatus.Pass
                && heightReplay.Steps.Count == 2 && heightReplay.Steps[1].Result.Metrics.Single(metric => metric.Name == "Mean").Value is double height
                && Math.Abs(height - 5.25) < 1e-10, $"{heightReplay.Status};{heightReplay.Message}");
            var heightPath = Path.Combine(root, "height.ov3d-recipe.json");
            ToolRecipeDocumentStore.Save(heightPath, heightDocument);
            using (var heightWorkbench = new ToolWorkbenchViewModel(Path.Combine(root, "height-recent.json")))
            {
                if (!heightWorkbench.TryOpenTeachingRecipe(heightPath, out var message)) throw new InvalidDataException(message);
                heightWorkbench.SelectPipelineStep("step.nine");
                heightWorkbench.RunSelectedStepCommand.Execute(null);
                SpinWait.SpinUntil(() => !heightWorkbench.IsSelectedStepPreviewRunning, TimeSpan.FromSeconds(15));
                heightWorkbench.SelectPipelineStep("step.height");
                var measured = heightWorkbench.PreviewSelectedMeasurementAsync().GetAwaiter().GetResult();
                Check("Workbench consumes the published compensated map without replacing the input", measured
                    && heightWorkbench.CurrentMeasurementOutput?.InputEntityId == output.EntityId
                    && heightWorkbench.CurrentMeasurementOutput.Result.Metrics.Single(metric => metric.Name == "Mean").Value is double measuredHeight
                    && Math.Abs(measuredHeight - 5.25) < 1e-10 && heightWorkbench.Source.Id == source.EntityId,
                    heightWorkbench.MeasurementExecutionSummary);
            }
            using (var closing = new ToolWorkbenchViewModel(Path.Combine(root, "closing-recent.json")))
            using (var context = new CompletionQueue())
            {
                if (!closing.TryOpenTeachingRecipe(recipePath, out var message)) throw new InvalidDataException(message);
                closing.SelectPipelineStep("step.nine");
                var step = closing.SelectedPipelineStep!;
                var displayed = 0;
                closing.FilterDisplayRequested += (_, _) => displayed++;
                var previous = SynchronizationContext.Current;
                SynchronizationContext.SetSynchronizationContext(context);
                try
                {
                    closing.RunSelectedStepCommand.Execute(null);
                    closing.Dispose();
                    var closedState = step.State;
                    var settled = context.DrainUntil(() => context.ProcessedCount > 0);
                    Check("closing during work releases its command and suppresses late state writes", settled
                        && !closing.IsSelectedStepPreviewRunning && displayed == 0 && step.State == closedState,
                        $"settled={settled};display={displayed};state={step.State};closedState={closedState}");
                }
                finally { SynchronizationContext.SetSynchronizationContext(previous); }
            }
            Check("source C3D bytes remain unchanged", sourceBytes.SequenceEqual(File.ReadAllBytes(path)), source.ContentSha256);
        }
        catch (Exception exception) { Check("unhandled integration error", false, exception.ToString()); }
        finally { Environment.SetEnvironmentVariable("OPENVISIONLAB_3D_TEST_ARTIFACT_ROOT", previousRoot); }
        summary = $"Nine-point Studio integration: {passed}/{total}";
        lines.Add(summary);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllLines(reportPath, lines);
        return total > 0 && passed == total;
    }

    public static bool VerifyConnectionBoundaries(string fixturePath, string reportPath, out string summary, bool demoIdentityOnly = false)
    {
        var lines = new List<string> { "D1–D4 connection boundaries only. Existing24-case suite not invoked; no Application/Window/Viewer." };
        var total = 0;
        var passed = 0;
        var root = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(reportPath))!, "connection-fixtures");
        var previousRoot = Environment.GetEnvironmentVariable("OPENVISIONLAB_3D_TEST_ARTIFACT_ROOT");
        void Check(string name, bool condition, string detail)
        {
            total++;
            if (condition) passed++;
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
        }
        void Run(ToolWorkbenchViewModel workbench, string stepId)
        {
            if (!workbench.SelectPipelineStep(stepId) || !workbench.RunSelectedStepCommand.CanExecute(null))
                throw new InvalidOperationException($"Cannot run boundary fixture step {stepId}.");
            workbench.RunSelectedStepCommand.Execute(null);
            if (!SpinWait.SpinUntil(() => !workbench.IsSelectedStepPreviewRunning, TimeSpan.FromSeconds(15)))
                throw new TimeoutException("Boundary fixture did not settle.");
        }
        try
        {
            Directory.CreateDirectory(root);
            Environment.SetEnvironmentVariable("OPENVISIONLAB_3D_TEST_ARTIFACT_ROOT", root);
            var document = ToolRecipeDocumentStore.Load(fixturePath);
            var step = document.Steps.Single(item => item.ToolId == ToolRecipeNinePointSurfaceCompensationExecution.ToolId);
            var direct = ToolRecipeNinePointSurfaceCompensationExecution.Execute(document, step.Id, recipeDirectory: Path.GetDirectoryName(fixturePath));
            var corrected = direct.Output ?? throw new InvalidDataException(direct.Result.Message);
            var numerical = direct.Numerical!;
            var expectedRows = new[] { 0, 96, 192 };
            var expectedColumns = new[] { 0, 128, 256 };
            var expectedBinding = document.Selections!.First().SourceBinding;
            Check("D1 exact257×193 recipe/control grid/output ROI identity", corrected is { Width: 257, Height: 193, MissingCount: 1 }
                && numerical.ControlPoints.Select(point => point.Row).Distinct().SequenceEqual(expectedRows)
                && numerical.ControlPoints.Select(point => point.Column).Distinct().SequenceEqual(expectedColumns)
                && corrected.ContentSha256 == expectedBinding.ContentSha256 && corrected.FrameId == document.Source.FrameId
                && string.Equals(corrected.RootSourceSha256, document.Source.ContentSha256, StringComparison.OrdinalIgnoreCase) && corrected.Unit == "raw-height",
                $"output={corrected.ContentSha256};frame={corrected.FrameId};missing={corrected.MissingCount};root={corrected.RootSourceSha256};authored={document.Source.ContentSha256}");
            if (demoIdentityOnly)
            {
                summary = $"Nine-point D1 demo identity: {passed}/{total}";
                lines.Add(summary);
                File.WriteAllLines(reportPath, lines);
                return passed == total;
            }
            var measurementStep = document.Steps.Single(item => item.ToolId == "thickness");
            var measurement = ToolRecipeHeightMeasurementExecution.Execute(document, measurementStep.Id, corrected, null);
            var mean = measurement.Result.Metrics.Single(metric => metric.Name == "Mean").Value;
            Check("D2 prepared local ROI consumes exact corrected map", measurement.Result.Status == ResultStatus.Pass
                && mean is double height && Math.Abs(height - 8.0) <= 3.814697265625e-6,
                $"local mean={mean:R} raw-height;reference121/measurement629 cells;synthetic only");

            var source = C3DHeightFieldSnapshot.LoadIdentified(document.Source.Path, document.Source.Id, document.Source.Unit, document.Source.FrameId);
            var twoSteps = document with { Steps = [step, step with { Id = "step.unexecuted", OutputEntityId = "output.unexecuted" }], Selections = [] };
            var twoPath = Path.Combine(root, "two-nine-point-steps.ov3d-recipe.json");
            ToolRecipeDocumentStore.Save(twoPath, twoSteps);
            using (var workbench = new ToolWorkbenchViewModel(Path.Combine(root, "recent.json")))
            {
                if (!workbench.TryOpenTeachingRecipe(twoPath, out var message)) throw new InvalidDataException(message);
                var displays = 0;
                workbench.FilterDisplayRequested += (_, _) => displays++;
                Run(workbench, step.Id);
                var label = workbench.SelectedToolWorkspace.CurrentOutput?.ValueLabel ?? string.Empty;
                Check("D4 new metric retains nine-point plane-deviation name", label.Contains('9') && label.Contains("P–V")
                    && workbench.NinePointCompensationSummary.Contains('9'), $"label={label};summary={workbench.NinePointCompensationSummary}");
                workbench.SelectPipelineStep("step.unexecuted");
                Check("D3 unexecuted step never displays another step summary", string.IsNullOrEmpty(workbench.NinePointCompensationSummary)
                    && workbench.NinePointCompensationRows.Count == 0 && workbench.NinePointPreviewFrame is null && displays == 1,
                    $"summary={workbench.NinePointCompensationSummary};rows={workbench.NinePointCompensationRows.Count};display={displays}");
                workbench.SelectPipelineStep(step.Id);
                workbench.SelectedPipelineStep!.Parameters.Single(item => item.Name == "TargetHeight").Value = "11";
                Check("D3 applied parameter invalidates map/publication and names stale result", workbench.NinePointCompensationRows.Count == 0
                    && workbench.NinePointPreviewFrame is null && !workbench.PublishSelectedStepCommand.CanExecute(null)
                    && !workbench.NinePointCompensationSummary.Contains("P–V")
                    && (workbench.NinePointCompensationSummary.Contains("바뀌") || workbench.NinePointCompensationSummary.Contains("changed"))
                    && displays == 1, workbench.NinePointCompensationSummary);
                var changedPath = Path.Combine(root, "replacement-source.c3d");
                File.Copy(document.Source.Path, changedPath, true);
                workbench.SetC3DSource(changedPath);
                Check("D3 source selection clears only the old nine-point presentation without execution", string.IsNullOrEmpty(workbench.NinePointCompensationSummary)
                    && workbench.NinePointCompensationRows.Count == 0 && workbench.NinePointPreviewFrame is null && displays == 1,
                    $"summary={workbench.NinePointCompensationSummary};display={displays}");
            }
            var missingValues = source.Values.ToArray();
            missingValues[0] = double.NaN;
            var missingPath = Path.Combine(root, "missing-control.c3d");
            source.CreateDerived("missing", missingValues, "owned-missing-control").SaveC3D(missingPath);
            var missingSource = C3DHeightFieldSnapshot.LoadIdentified(missingPath, document.Source.Id, source.Unit, source.FrameId);
            var missingDocument = document with
            {
                Source = document.Source with { Path = missingPath, ByteLength = missingSource.ByteLength, ContentSha256 = missingSource.ContentSha256 },
                Steps = [step], Selections = []
            };
            var missingRecipePath = Path.Combine(root, "missing-control.ov3d-recipe.json");
            ToolRecipeDocumentStore.Save(missingRecipePath, missingDocument);
            using (var missingWorkbench = new ToolWorkbenchViewModel(Path.Combine(root, "missing-recent.json")))
            {
                if (!missingWorkbench.TryOpenTeachingRecipe(missingRecipePath, out var message)) throw new InvalidDataException(message);
                Run(missingWorkbench, step.Id);
                var error = missingWorkbench.NinePointCompensationSummary;
                Check("D3 missing control reports its number/row/column without result", error.Contains('1')
                    && (error.Contains("행 0") || error.Contains("row 0")) && (error.Contains("열 0") || error.Contains("column 0"))
                    && missingWorkbench.NinePointCompensationRows.Count == 0 && missingWorkbench.NinePointPreviewFrame is null, error);
            }
        }
        catch (Exception exception) { Check("boundary fixture exception", false, exception.ToString()); }
        finally { Environment.SetEnvironmentVariable("OPENVISIONLAB_3D_TEST_ARTIFACT_ROOT", previousRoot); }
        summary = $"Nine-point D1–D4 boundaries: {passed}/{total}";
        lines.Add(summary);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllLines(reportPath, lines);
        return total > 0 && passed == total;
    }

    public static bool VerifyWarpCorrectedThickness(string fixturePath, string reportPath, out string summary)
    {
        var lines = new List<string> { "Same workpiece: raw warpage -> nine-point compensation -> corrected warpage -> thickness. No Window or old full regression suite." };
        var passed = 0;
        var total = 0;
        void Check(string name, bool ok) { total++; if (ok) passed++; lines.Add($"{(ok ? "PASS" : "FAIL")}|{name}"); }
        var root = Path.GetDirectoryName(Path.GetFullPath(fixturePath))!;
        try
        {
            var document = ToolRecipeDocumentStore.Load(fixturePath);
            var source = C3DHeightFieldSnapshot.LoadIdentified(document.Source.Path, document.Source.Id, document.Source.Unit, document.Source.FrameId);
            var beforeBytes = File.ReadAllBytes(document.Source.Path);
            var step = document.Steps.Single();
            var direct = ToolRecipeNinePointSurfaceCompensationExecution.Execute(document, step.Id, source);
            Check("declared micrometre input is accepted without unit conversion", direct.Result.Status == ResultStatus.Pass && direct.Output?.Unit == "µm");
            var output = direct.Output ?? throw new InvalidDataException(direct.Result.Message);
            output.SaveC3D(Path.Combine(root, "expected-compensated.C3D"));
            var wrongUnit = C3DHeightFieldSnapshot.LoadIdentified(document.Source.Path, document.Source.Id, "mm", document.Source.FrameId);
            var rejected = ToolRecipeNinePointSurfaceCompensationExecution.Execute(document, step.Id, wrongUnit);
            Check("different declared unit is rejected and valid retry recovers", rejected.Result.Status == ResultStatus.Error && rejected.Output is null
                && ToolRecipeNinePointSurfaceCompensationExecution.Execute(document, step.Id, source).Output?.ContentSha256 == output.ContentSha256);
            var legacy = C3DHeightFieldSnapshot.LoadIdentified(document.Source.Path, document.Source.Id, "raw-height", document.Source.FrameId);
            var legacyDocument = document with { Source = document.Source with { Unit = "raw-height" } };
            var legacyResult = ToolRecipeNinePointSurfaceCompensationExecution.Execute(legacyDocument, step.Id, legacy);
            Check("legacy raw-height numerical output remains identical", legacyResult.Result.Status == ResultStatus.Pass
                && legacyResult.Output?.Unit == "raw-height" && legacyResult.Output.Values.Span.SequenceEqual(output.Values.Span));
            using var truth = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "ground-truth.json")));
            var tolerance = truth.RootElement.GetProperty("scalarTolerance").GetDouble();
            var binding = ToolRecipeSelectionSourceBindingVerifier.FromHeightField(output);
            var selections = document.Selections!.Select(selection => selection.Id == "roi.warp.before" ? selection : selection with { SourceBinding = binding }).ToArray();
            ToolRecipeStep Warpage(string id, string name, string input, string roi, string limit) => new(id, "warpage", name, 2,
                [input, roi], "result." + id, [new("MaximumPeakToValley", limit), new("MaximumRms", limit), new("MinimumValidSampleCount", "1000")], AcceptanceLimitsConfigured: true);
            var steps = new List<ToolRecipeStep>
            {
                Warpage("step.warp.before", "보정 전 휨", source.EntityId, "roi.warp.before", "50"), step,
                Warpage("step.warp.after", "보정 후 휨", output.EntityId, "roi.warp.after", "0.01")
            };
            foreach (var item in truth.RootElement.GetProperty("measurements").EnumerateArray())
            {
                var index = item.GetProperty("index").GetInt32();
                var roiId = $"roi.thickness.{index}";
                steps.Add(new($"step.thickness.{index}", "thickness", $"보정 후 두께 {index}", 3,
                    [output.EntityId, "roi.reference", roiId], $"result.thickness.{index}",
                    [new("MinimumThickness", "1450"), new("MaximumThickness", "1550"), new("MinimumValidSampleCount", "1000")],
                    new("roi.reference", roiId), AcceptanceLimitsConfigured: true));
            }
            var flow = document with { Steps = steps, Selections = selections };
            var path = Path.Combine(root, "warp-flatten-thickness.ov3d-recipe.json");
            ToolRecipeDocumentStore.Save(path, flow);
            var replay = ToolRecipeOrderedGraphExecution.Execute(flow, document.Source.Path);
            Check("all twelve steps run in the authored order", replay.Steps.Count == 12
                && replay.Steps.Select(item => item.StepId).SequenceEqual(steps.Select(item => item.Id)) && replay.Status == ResultStatus.Fail);
            var raw = replay.Steps[0].Result.Metrics.Single(metric => metric.Name == "PeakToValley").Value;
            var flat = replay.Steps[2].Result.Metrics.Single(metric => metric.Name == "PeakToValley").Value;
            Check("raw warpage agrees with independent SVD oracle", Math.Abs(raw - truth.RootElement.GetProperty("expectedRawPeakToValley").GetDouble()) < tolerance);
            Check("same substrate region is flat after real SDK compensation", flat < 2 * tolerance && replay.Steps[2].Result.Status == ResultStatus.Pass);
            foreach (var item in truth.RootElement.GetProperty("measurements").EnumerateArray())
            {
                var index = item.GetProperty("index").GetInt32();
                var result = replay.Steps.Single(candidate => candidate.StepId == $"step.thickness.{index}").Result;
                var actual = result.Metrics.Single(metric => metric.Name == "Mean").Value;
                Check($"corrected thickness {index} keeps declared geometry and unit", Math.Abs(actual - item.GetProperty("expectedMean").GetDouble()) < tolerance
                    && result.Status.ToString() == item.GetProperty("expectedJudgment").GetString() && result.Metrics.Single(metric => metric.Name == "Mean").Unit == "µm");
                lines.Add($"MEASURE|{index}|{actual:R}|{result.Status}");
            }
            var reopened = ToolRecipeOrderedGraphExecution.Execute(ToolRecipeDocumentStore.Load(path), document.Source.Path);
            Check("saved recipe replays the same corrected output and measurements", reopened.Steps.Count == 12
                && reopened.Steps[1].OutputContentSha256 == replay.Steps[1].OutputContentSha256
                && reopened.Steps.SelectMany(item => item.Result.Metrics).SequenceEqual(replay.Steps.SelectMany(item => item.Result.Metrics)));
            Check("source bytes and root selection remain unchanged", beforeBytes.SequenceEqual(File.ReadAllBytes(document.Source.Path))
                && flow.Source.Id == source.EntityId && steps.Skip(2).All(item => item.InputEntityIds[0] == output.EntityId));
            lines.Add($"WARP|before={raw:R}|after={flat:R}|unit=µm|compensated={output.ContentSha256}|tolerance={tolerance:R}");
        }
        catch (Exception exception) { lines.Add(exception.ToString()); Check("corrected thickness workflow", false); }
        summary = $"Warp-corrected thickness: {passed}/{total}";
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllLines(reportPath, lines.Append(summary));
        return total > 0 && passed == total;
    }

    public static bool VerifyRuntimeRecord(string fixturePath, string reportPath, out string summary)
    {
        var lines = new List<string>();
        var passed = 0;
        var total = 0;
        void Check(string name, bool ok) { total++; if (ok) passed++; lines.Add($"{(ok ? "PASS" : "FAIL")}|{name}"); }
        var root = Path.GetDirectoryName(Path.GetFullPath(reportPath))!;
        Directory.CreateDirectory(root);
        var previousRoot = Environment.GetEnvironmentVariable("OPENVISIONLAB_3D_TEST_ARTIFACT_ROOT");
        Environment.SetEnvironmentVariable("OPENVISIONLAB_3D_TEST_ARTIFACT_ROOT", root);
        try
        {
            var document = ToolRecipeDocumentStore.Load(fixturePath);
            var execution = ToolRecipeOrderedGraphExecution.Execute(document, document.Source.Path!);
            var identity = OrderedRunRecordIdentity.Create(fixturePath, document.Source.Path!);
            var recordPath = Path.Combine(root, "inspection.json");
            InspectionRunRecord CreateRecord(ToolRecipeOrderedGraphExecutionResult result) => OrderedRunRecordFactory.Create(
                identity, result.ReboundDocument, result, "SameOrderedGraphEngine",
                new InspectionRunArtifacts(Path.Combine(root, "run.txt"), null, null, recordPath, null, null));
            var record = CreateRecord(execution);
            InspectionRunRecordJson.Write(recordPath, record);
            var reopened = InspectionRunRecordJson.Read(recordPath);
            Check("actual two-step inspection record saves and reopens with algorithm identity", execution.Status == ResultStatus.Pass
                && reopened.WorkflowKind == RunRecordWorkflowKind.Inspection && reopened.Steps is { Count: 2 }
                && reopened.Steps[0].SemanticFingerprint is { Length: 64 } && reopened.Steps[0].TryValidateEvidence(out _)
                && reopened.Steps[1].Metrics.Any(metric => Math.Abs(metric.Value - 8) < 0.00001));

            var badStep = document.Steps[0] with { Parameters = document.Steps[0].Parameters!
                .Select(parameter => parameter.Name == "MiddleRow" ? parameter with { Value = "0" } : parameter).ToArray() };
            var failed = ToolRecipeOrderedGraphExecution.Execute(document with { Steps = [badStep] }, document.Source.Path!);
            var failedPath = Path.Combine(root, "controlled-error.json");
            InspectionRunRecordJson.Write(failedPath, CreateRecord(failed));
            var failedRecord = InspectionRunRecordJson.Read(failedPath);
            Check("controlled failure remains saveable without claiming a completed SDK output", failed.Status == ResultStatus.Error
                && failedRecord.Steps is { Count: 1 } && failedRecord.Steps[0].AlgorithmEvidence is null
                && failedRecord.Steps[0].SemanticFingerprint is null);

            var invalidPath = Path.Combine(root, "invalid-evidence.json");
            InspectionRunRecordJson.Write(invalidPath, record with { Steps = [record.Steps![0] with { SemanticFingerprint = null }] });
            var rejected = false;
            try { InspectionRunRecordJson.Read(invalidPath); } catch (InvalidDataException) { rejected = true; }
            Check("existing strict record validator still rejects evidence without fingerprint", rejected);

            using var workbench = new ToolWorkbenchViewModel(Path.Combine(root, "recent.json"));
            if (!workbench.TryOpenTeachingRecipe(fixturePath, out var message)) throw new InvalidDataException(message);
            workbench.SelectPipelineStep(document.Steps[0].Id);
            workbench.RunSelectedStepCommand.Execute(null);
            if (!SpinWait.SpinUntil(() => !workbench.IsSelectedStepPreviewRunning, TimeSpan.FromSeconds(15))) throw new TimeoutException();
            Check("ROIs on the compensated HeightField are current after explicit Run", document.Selections!.All(selection =>
                workbench.ArtifactRegistry.Any(artifact => artifact.Id == selection.Id && !artifact.State.StartsWith("Stale", StringComparison.OrdinalIgnoreCase)))
                && workbench.NinePointCompensationRows.Count == 9);
            var changed = new HashSet<string>();
            workbench.PropertyChanged += (_, args) => changed.Add(args.PropertyName ?? string.Empty);
            workbench.SelectPipelineStep(document.Steps[1].Id);
            Check("switching to height measurement updates and hides nine-point evidence", changed.Contains(nameof(workbench.IsSelectedStepNinePointCompensation))
                && changed.Contains(nameof(workbench.NinePointCompensationRows)) && !workbench.IsSelectedStepNinePointCompensation
                && workbench.NinePointCompensationRows.Count == 0 && string.IsNullOrEmpty(workbench.NinePointCompensationSummary));
        }
        catch (Exception exception) { lines.Add(exception.ToString()); Check("runtime record fixture", false); }
        finally { Environment.SetEnvironmentVariable("OPENVISIONLAB_3D_TEST_ARTIFACT_ROOT", previousRoot); }
        summary = $"Nine-point runtime record: {passed}/{total}";
        lines.Add(summary);
        File.WriteAllLines(reportPath, lines);
        return passed == total;
    }

    public static bool VerifySaveBinding(string fixturePath, string reportPath, out string summary)
    {
        var lines = new List<string>();
        var root = Path.GetDirectoryName(Path.GetFullPath(reportPath))!;
        Directory.CreateDirectory(root);
        var previousRoot = Environment.GetEnvironmentVariable("OPENVISIONLAB_3D_TEST_ARTIFACT_ROOT");
        Environment.SetEnvironmentVariable("OPENVISIONLAB_3D_TEST_ARTIFACT_ROOT", root);
        var passed = false;
        try
        {
            using var workbench = new ToolWorkbenchViewModel(Path.Combine(root, "save-recent.json"));
            if (!workbench.TryOpenTeachingRecipe(fixturePath, out var message)) throw new InvalidDataException(message);
            workbench.SelectPipelineStep("step.nine");
            workbench.RunSelectedStepCommand.Execute(null);
            if (!SpinWait.SpinUntil(() => !workbench.IsSelectedStepPreviewRunning, TimeSpan.FromSeconds(15))) throw new TimeoutException();
            passed = workbench.NinePointCompensationRows.Count == 9 && workbench.CanSaveTeachingRecipe
                && workbench.SaveTeachingRecipeAsCommand.CanExecute(null) && !workbench.IsRecipeSaveBlocked;
            lines.Add($"save={workbench.CanSaveTeachingRecipe};rows={workbench.NinePointCompensationRows.Count};{workbench.ValidationSummary};{workbench.RecipeStateSummary}");
        }
        catch (Exception exception) { lines.Add(exception.ToString()); }
        finally { Environment.SetEnvironmentVariable("OPENVISIONLAB_3D_TEST_ARTIFACT_ROOT", previousRoot); }
        summary = $"Nine-point save binding: {(passed ? "1/1" : "0/1")}";
        lines.Add(summary);
        File.WriteAllLines(reportPath, lines);
        return passed;
    }

    public static bool VerifyViewerLabels(string fixturePath, string reportPath, out string summary)
    {
        var lines = new List<string> { "Derived-map measurement labels only; no Application, Window or native input. Prior24 and persistence suites not invoked." };
        var root = Path.GetDirectoryName(Path.GetFullPath(reportPath))!;
        Directory.CreateDirectory(root);
        var previousRoot = Environment.GetEnvironmentVariable("OPENVISIONLAB_3D_TEST_ARTIFACT_ROOT");
        Environment.SetEnvironmentVariable("OPENVISIONLAB_3D_TEST_ARTIFACT_ROOT", root);
        var passed = 0;
        var total = 0;
        void Check(string name, bool condition) { total++; if (condition) passed++; lines.Add($"{(condition ? "PASS" : "FAIL")}|{name}"); }
        try
        {
            using var workbench = new ToolWorkbenchViewModel(Path.Combine(root, "label-recent.json"));
            using var workspace = new ResultsWorkspaceViewModel();
            if (!workbench.TryOpenTeachingRecipe(fixturePath, out var message)) throw new InvalidDataException(message);
            Check("opening does not create measurement labels or execute", workbench.TeachingResults.Count == 0 && workbench.NinePointCompensationRows.Count == 0);
            string? correctedPath = null;
            workbench.FilterDisplayRequested += (_, args) => { if (!args.IsSource) correctedPath = args.C3DPath; };
            Run("step.nine");
            Run("step.height");
            workspace.RefreshTeachingResultLabels(workbench.TeachingResults.ToArray(), workbench.GetCurrentAppliedTeachingSelections(), fixturePath);
            var label = workspace.TeachingResultLabels.Single();
            var corrected = C3DHeightFieldSnapshot.LoadIdentified(correctedPath ?? throw new InvalidDataException("No corrected-map export."),
                "synthetic-pcb-compensated", "raw-height", "synthetic-pcb-input-frame");
            var source = new TeachingCaptureSourceSnapshot(corrected.Width, corrected.Height, 1, corrected.Mean, corrected.ContentSha256);
            Check("executed height result retains ROI, raw value, unit and verdict", label.IsValid && label.IsCurrent && label.IsVisible
                && label.StepId == "step.height" && label.Anchor.SourceBinding.Format == "HeightField"
                && label.Anchor.GridRectangle == new ToolRecipeGridRectangle(54, 36, 17, 37)
                && label.Value is { } value && Math.Abs(value - 8) <= 3.814697265625e-6
                && label.Unit == "raw-height" && label.Status == ResultStatus.Pass);
            Check("byte-identical derived C3D permits the actual measurement label", TeachingSelectionSourcePolicy.IsCurrentC3DGrid(label.Anchor, source));
            Check("original input cannot display the corrected-map label", !TeachingSelectionSourcePolicy.IsCurrentC3DGrid(label.Anchor,
                source with { ContentSha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(workbench.Source.Path))) }));
            Check("changed bytes and either grid dimension reject the overlay", !TeachingSelectionSourcePolicy.IsCurrentC3DGrid(label.Anchor,
                source with { ContentSha256 = new string('B', 64) })
                && !TeachingSelectionSourcePolicy.IsCurrentC3DGrid(label.Anchor, source with { Width = source.Width - 1 })
                && !TeachingSelectionSourcePolicy.IsCurrentC3DGrid(label.Anchor, source with { Height = source.Height - 1 }));
            Check("transformed or unsupported bindings remain rejected", !TeachingSelectionSourcePolicy.IsCurrentC3DGrid(label.Anchor with
                { SourceBinding = label.Anchor.SourceBinding with { Format = "TransformedHeightField" } }, source)
                && !TeachingSelectionSourcePolicy.IsCurrentC3DGrid(label.Anchor with
                { SourceBinding = label.Anchor.SourceBinding with { Format = "MeasurementResult" } }, source));
            Check("caption exposes declared unit and OK without changing raw data", label.Caption == "8 raw-height OK"
                && label.Value == workbench.TeachingResults.Single(row => row.Step.Id == "step.height").Metrics.Single(metric => metric.Name == "Mean").Value);
            Check("NG, stale and unjudged captions are distinct", (label with { Status = ResultStatus.Fail }).Caption == "× 8 raw-height NG"
                && (label with { IsCurrent = false }).Caption == "↻ 8 raw-height"
                && (label with { Status = null }).Caption == "8 raw-height");
            Check("error and signed zero/negative values are not fabricated or hidden", (label with { Value = null, ValueText = "오류", Status = ResultStatus.Error }).Caption == "! 오류"
                && (label with { Value = -8, ValueText = "-8", Status = ResultStatus.Fail }).Caption == "× -8 raw-height NG"
                && (label with { Value = 0, ValueText = "0" }).Caption == "0 raw-height OK");
            var rowsBeforeHide = workbench.TeachingResults.ToArray();
            workspace.ToggleTeachingResultLabelCommand.Execute(rowsBeforeHide.Single(row => row.Step.Id == "step.height").ResultLabel);
            Check("existing hide control changes display only", !workspace.TeachingResultLabels.Single().IsVisible
                && workbench.TeachingResults.SequenceEqual(rowsBeforeHide) && workbench.CurrentMeasurementOutput?.Result.Status == ResultStatus.Pass);
            lines.Add($"caption={label.Caption};source={corrected.ContentSha256};anchor={label.Anchor.Id};raw={label.Value:R}");

            void Run(string stepId)
            {
                if (!workbench.SelectPipelineStep(stepId) || !workbench.RunSelectedStepCommand.CanExecute(null)) throw new InvalidDataException($"Cannot run {stepId}.");
                workbench.RunSelectedStepCommand.Execute(null);
                if (!SpinWait.SpinUntil(() => !workbench.IsSelectedStepPreviewRunning, TimeSpan.FromSeconds(15))) throw new TimeoutException(stepId);
            }
        }
        catch (Exception exception) { lines.Add(exception.ToString()); Check("derived-map label fixture", false); }
        finally { Environment.SetEnvironmentVariable("OPENVISIONLAB_3D_TEST_ARTIFACT_ROOT", previousRoot); }
        summary = $"Nine-point Viewer measurement labels: {passed}/{total}";
        lines.Add(summary);
        File.WriteAllLines(reportPath, lines);
        return passed == total;
    }

    public static bool VerifyNineRoiTeaching(string fixtureFolder, string reportPath, out string summary)
    {
        var lines = new List<string> { "Nine-ROI wiring/capture contract; existing SDK executes each region; no Window/GUI input." };
        var passed = 0;
        var total = 0;
        void Check(string name, bool ok) { total++; if (ok) passed++; lines.Add($"{(ok ? "PASS" : "FAIL")}|{name}"); }
        var root = Path.GetDirectoryName(Path.GetFullPath(reportPath))!;
        Directory.CreateDirectory(root);
        var previousRoot = Environment.GetEnvironmentVariable("OPENVISIONLAB_3D_TEST_ARTIFACT_ROOT");
        Environment.SetEnvironmentVariable("OPENVISIONLAB_3D_TEST_ARTIFACT_ROOT", root);
        try
        {
            foreach (var tool in new[] { "thickness", "warpage" })
            {
                var path = Path.Combine(fixtureFolder, tool + "-nine.ov3d-recipe.json");
                var document = ToolRecipeDocumentStore.Load(path);
                using var workbench = new ToolWorkbenchViewModel(Path.Combine(root, tool + "-recent.json"));
                using var workspace = new ResultsWorkspaceViewModel();
                if (!workbench.TryOpenTeachingRecipe(path, out var message)) throw new InvalidDataException(message);
                Check(tool + " open is not execution", workbench.TeachingResults.Count == 0);
                workbench.SelectPipelineStep($"step.{tool}.1");
                if (tool == "thickness")
                {
                    Check("unpublished compensation cannot start ROI capture", !workbench.BeginTeachingSelectionCaptureCommand.CanExecute(null)
                        && !workbench.TryGetPublishedTeachingHeightField("synthetic-pcb-compensated", out _, out _));
                    Run("step.nine");
                    workbench.SelectPipelineStep("step.thickness.1");
                    Check("Published compensation enables capture", workbench.BeginTeachingSelectionCaptureCommand.CanExecute(null));
                    ToolWorkbenchTeachingCaptureRequestEventArgs? captured = null;
                    workbench.BeginTeachingSelectionCaptureRequested += (_, args) => captured = args;
                    var before = workbench.GetCurrentAppliedTeachingSelections().ToArray();
                    var dirtyBeforeCapture = workbench.IsDirty;
                    var resultsBeforeCapture = workbench.TeachingResults.Count;
                    workbench.BeginTeachingSelectionCaptureCommand.Execute(null);
                    Check("capture carries the HeightField identity without format substitution", captured?.SourceBinding.Format == "HeightField"
                        && captured.SourceBinding.OwnerEntityId == "synthetic-pcb-compensated" && workbench.IsTeachingSelectionCaptureActive);
                    if (captured is null || !workbench.TryGetPublishedTeachingHeightField("synthetic-pcb-compensated", out var output, out var outputPath)
                        || output is null || outputPath is null) throw new InvalidDataException("No current Published capture output.");
                    var request = new OpenVisionLab.ThreeD.Viewer.Models.TeachingCaptureRequest(captured.SelectionId, captured.SelectionName,
                        captured.Kind, captured.RequiredPointCount, captured.RootSourceId, captured.FrameId, captured.SourceBinding)
                    { PublishedHeightField = output };
                    var grid = C3DHeightGrid.Load(outputPath, maxRenderedPoints: 0);
                    var visible = TeachingCaptureSourceSnapshot.From(grid);
                    bool Ready(OpenVisionLab.ThreeD.Viewer.Models.TeachingCaptureRequest candidate, bool shown = true,
                        TeachingCaptureSourceSnapshot? source = null) => TeachingCapturePreparation.Prepare(candidate, captured.ExistingSelection,
                            shown, source ?? visible, null).IsValid;
                    Check("exact Published owner and visible bytes prepare", Ready(request));
                    Check("HeightField without Published snapshot rejected", !Ready(request with { PublishedHeightField = null }));
                    var binding = request.SourceBinding;
                    foreach (var bad in new[] {
                        binding with { OwnerEntityId = "different-owner" }, binding with { ContentSha256 = new string('B', 64) },
                        binding with { RootSourceContentSha256 = new string('C', 64) }, binding with { GridWidth = binding.GridWidth - 1 },
                        binding with { GridHeight = binding.GridHeight - 1 }, binding with { Unit = "mm" }, binding with { FrameId = "different-frame" } })
                        Check("mismatched Published binding rejected: " + bad, !Ready(request with { SourceBinding = bad }));
                    Check("request frame mismatch rejected", !Ready(request with { FrameId = "different-frame" }));
                    Check("hidden or different visible bytes/grid rejected", !Ready(request, false)
                        && !Ready(request, source: visible with { ContentSha256 = new string('D', 64) })
                        && !Ready(request, source: visible with { Width = visible.Width - 1 })
                        && !Ready(request, source: visible with { Height = visible.Height - 1 }));
                    workbench.CancelTeachingSelectionCaptureCommand.Execute(null);
                    Check("cancel preserves all saved ROI geometry and executes nothing", !workbench.IsTeachingSelectionCaptureActive
                        && before.SequenceEqual(workbench.GetCurrentAppliedTeachingSelections()) && workbench.TeachingResults.Count == resultsBeforeCapture && workbench.IsDirty == dirtyBeforeCapture);
                }
                for (var index = 1; index <= 9; index++) Run($"step.{tool}.{index}");
                workspace.RefreshTeachingResultLabels(workbench.TeachingResults.ToArray(), workbench.GetCurrentAppliedTeachingSelections(), path);
                var labels = workspace.TeachingResultLabels;
                Check(tool + " has nine distinct actual result/ROI identities", labels.Count == 9 && labels.Select(label => label.StepId).Distinct().Count() == 9
                    && labels.Select(label => label.Anchor.Id).Distinct().Count() == 9 && labels.Select(label => label.Anchor.GridRectangle).Distinct().Count() == 9);
                for (var index = 1; index <= 9; index++)
                {
                    var stepId = $"step.{tool}.{index}";
                    var row = workbench.TeachingResults.Single(item => item.Step.Id == stepId);
                    var label = labels.Single(item => item.StepId == stepId);
                    Check(tool + $" #{index} value/unit/location/state comes from its own result", label.IsValid && label.IsCurrent && label.IsVisible
                        && label.Anchor.Id == $"roi.{tool}.{index}" && label.Anchor.Name == $"#{index}" && label.RegionName == label.Anchor.Name
                        && label.Caption.StartsWith($"#{index} · ", StringComparison.Ordinal) && label.Unit == "raw-height"
                        && label.Value == ToolWorkbenchViewModel.SelectPrimaryMeasurementMetric(tool, row.Metrics)?.Value && label.Status == row.ExecutionStatus);
                    if (tool == "thickness")
                    {
                        var roi = label.Anchor.GridRectangle!;
                        var expected = ExpectedSyntheticLocalHeight(roi);
                        Check($"thickness #{index} agrees with independently declared synthetic feature", label.Value is { } actual && Math.Abs(actual - expected) < .00001);
                    }
                    lines.Add($"RESULT|{stepId}|{label.Anchor.Id}|{label.Anchor.GridRectangle}|{label.Value:R}|{label.Unit}|{label.Status}|{label.Caption}");
                }
                Check(tool + " source and output stay distinct", workbench.Source.Id == document.Source.Id
                    && document.Steps.Where(step => step.ToolId == tool).All(step => step.InputEntityIds[0] == (tool == "thickness" ? "synthetic-pcb-compensated" : document.Source.Id)));
                var saved = Path.Combine(root, tool + "-saved.ov3d-recipe.json");
                Check(tool + " saves through the existing workbench", workbench.TrySaveTeachingRecipe(saved, out message));
                var reopened = ToolRecipeDocumentStore.Load(saved);
                Check(tool + " reopens nine step/selection identities", reopened.Steps.Count(step => step.ToolId == tool) == 9
                    && reopened.Selections!.Count == (tool == "thickness" ? 10 : 9)
                    && reopened.Selections.Select(selection => selection.Id).SequenceEqual(document.Selections!.Select(selection => selection.Id)));
                using var restored = new ToolWorkbenchViewModel(Path.Combine(root, tool + "-reload-recent.json"));
                Check(tool + " restoration does not execute", restored.TryOpenTeachingRecipe(saved, out message) && restored.TeachingResults.Count == 0);
                if (tool == "thickness")
                {
                    workbench.SelectPipelineStep("step.nine");
                    workbench.SelectedPipelineStep!.Parameters.Single(parameter => parameter.Name == "MiddleRow").Value = "95";
                    workbench.SelectPipelineStep("step.thickness.1");
                    Check("stale compensation cannot start or supply ROI capture", !workbench.TryGetPublishedTeachingHeightField("synthetic-pcb-compensated", out _, out _)
                        && !workbench.BeginTeachingSelectionCaptureCommand.CanExecute(null));
                }
                void Run(string stepId)
                {
                    if (!workbench.SelectPipelineStep(stepId) || !workbench.RunSelectedStepCommand.CanExecute(null)) throw new InvalidDataException("Cannot run " + stepId);
                    workbench.RunSelectedStepCommand.Execute(null);
                    if (!SpinWait.SpinUntil(() => !workbench.IsSelectedStepPreviewRunning, TimeSpan.FromSeconds(20))) throw new TimeoutException(stepId);
                }
            }
        }
        catch (Exception exception) { lines.Add(exception.ToString()); Check("nine-ROI fixture", false); }
        finally { Environment.SetEnvironmentVariable("OPENVISIONLAB_3D_TEST_ARTIFACT_ROOT", previousRoot); }
        summary = $"Nine-ROI teaching/capture: {passed}/{total}";
        File.WriteAllLines(reportPath, lines.Append(summary));
        return total > 0 && passed == total;
    }

    public static bool VerifyNineRoiCaptions(string fixtureFolder, string reportPath, out string summary)
    {
        var lines = new List<string> { "Caption projection only: declared test metrics; no SDK computation, Window or input." };
        var passed = 0;
        var total = 0;
        void Check(string name, bool ok) { total++; if (ok) passed++; lines.Add($"{(ok ? "PASS" : "FAIL")}|{name}"); }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        foreach (var tool in new[] { "thickness", "warpage" })
        {
            using var workbench = new ToolWorkbenchViewModel(Path.Combine(Path.GetDirectoryName(reportPath)!, tool + "-caption-recent.json"));
            using var workspace = new ResultsWorkspaceViewModel();
            var path = Path.Combine(fixtureFolder, tool + "-nine.ov3d-recipe.json");
            var document = ToolRecipeDocumentStore.Load(path);
            if (!workbench.TryOpenTeachingRecipe(path, out var message)) throw new InvalidDataException(message);
            foreach (var status in new[] { "미판정", "Unjudged" })
            {
                var rows = workbench.PipelineSteps.Where(step => step.ToolId == tool).Select((step, index) =>
                    new TeachingMeasurementResultItem(step, status, (index - 1.25).ToString(CultureInfo.InvariantCulture), "raw-height", "", "", true,
                        [new Metric(tool == "thickness" ? "Mean" : "PeakToValley", MetricKind.Length, index - 1.25, "raw-height", ResultStatus.Pass)])).ToArray();
                workspace.RefreshTeachingResultLabels(rows, document.Selections!, path);
                var labels = workspace.TeachingResultLabels;
                Check(tool + " nine independent metric/ROI captions: " + status, labels.Count == 9 && labels.All(label =>
                    label.IsValid && label.IsCurrent && label.StateText == status && label.Caption.EndsWith("\n" + status, StringComparison.Ordinal)
                    && label.Caption.StartsWith(label.Anchor.Name + " · ", StringComparison.Ordinal)
                    && label.Value == rows.Single(row => row.Step.Id == label.StepId).Metrics[0].Value));
                if (labels.Count == 0) continue;
                var sample = labels[0];
                var single = sample with { RegionName = null, StateText = null };
                Check(tool + " single-result caption unchanged: " + status, single.Caption == single.ValueText + " raw-height"
                    && (single with { Status = ResultStatus.Pass }).Caption.EndsWith(" OK", StringComparison.Ordinal)
                    && (single with { Status = ResultStatus.Fail }).Caption.StartsWith("× ", StringComparison.Ordinal));
                workspace.RefreshTeachingResultLabels(rows.Select(row => row with { IsCurrent = false }).ToArray(), document.Selections!, path);
                Check(tool + " stale regions show actual prior-result state: " + status, workspace.TeachingResultLabels.All(label =>
                    !label.IsCurrent && label.StateText == (rows[0] with { IsCurrent = false }).StatusText
                        && label.Caption.EndsWith("\n" + label.StateText, StringComparison.Ordinal)));
            }
        }
        summary = $"Nine-ROI caption states: {passed}/{total}";
        File.WriteAllLines(reportPath, lines.Append(summary));
        return passed == total;
    }

    public static bool VerifyReadOnlyCoordinateBindings(string fixturePath, string reportPath, out string summary)
    {
        var lines = new List<string> { "Actual WPF control/template/binding activation on an STA thread; no Window, desktop input or product-usability claim." };
        var passed = 0;
        var total = 0;
        void Check(string name, bool ok) { total++; if (ok) passed++; lines.Add($"{(ok ? "PASS" : "FAIL")}|{name}"); }
        var root = Path.GetDirectoryName(Path.GetFullPath(reportPath))!;
        Directory.CreateDirectory(root);
        var previousRoot = Environment.GetEnvironmentVariable("OPENVISIONLAB_3D_TEST_ARTIFACT_ROOT");
        Environment.SetEnvironmentVariable("OPENVISIONLAB_3D_TEST_ARTIFACT_ROOT", root);
        var thread = new Thread(() =>
        {
            System.Windows.Application? app = null;
            try
            {
                app = new System.Windows.Application { ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown };
                app.Resources.MergedDictionaries.Add(new global::Wpf.Ui.Markup.ThemesDictionary { Theme = global::Wpf.Ui.Appearance.ApplicationTheme.Dark });
                app.Resources.MergedDictionaries.Add(new global::Wpf.Ui.Markup.ControlsDictionary());
                app.Resources.MergedDictionaries.Add(new System.Windows.ResourceDictionary
                    { Source = new Uri("pack://application:,,,/OpenVisionLab.ThreeD.Shell;component/Themes/OpenVisionThreeDTheme.xaml") });
                using var workbench = new ToolWorkbenchViewModel(Path.Combine(root, "binding-recent.json"));
                if (!workbench.TryOpenTeachingRecipe(fixturePath, out var message)) throw new InvalidDataException(message);
                workbench.SelectPipelineStep("step.nine");
                var view = new OpenVisionLab.ThreeD.Shell.Views.Workbench.NinePointSurfaceCompensationEvidenceView { DataContext = workbench };
                void Layout()
                {
                    for (var i = 0; i < 3; i++)
                    {
                        view.Measure(new System.Windows.Size(320, double.PositiveInfinity));
                        view.Arrange(new System.Windows.Rect(0, 0, 320, view.DesiredSize.Height));
                        view.UpdateLayout();
                        var frame = new System.Windows.Threading.DispatcherFrame();
                        view.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
                        System.Windows.Threading.Dispatcher.PushFrame(frame);
                    }
                }
                Layout();
                var grid = Descendants(view).OfType<System.Windows.Controls.DataGrid>().Single();
                lines.Add($"INITIAL|step={workbench.SelectedPipelineStep?.Id}|vmRows={workbench.NinePointCompensationRows.Count}|gridRows={grid.Items.Count}|visibility={view.Visibility}|canRun={workbench.RunSelectedStepCommand.CanExecute(null)}|dataContext={ReferenceEquals(view.DataContext, workbench)}");
                Check("initial evidence has zero rows", grid.Items.Count == 0);
                workbench.RunSelectedStepCommand.Execute(null);
                var timer = System.Diagnostics.Stopwatch.StartNew();
                while (workbench.IsSelectedStepPreviewRunning && timer.Elapsed < TimeSpan.FromSeconds(20))
                {
                    view.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() => { }));
                    Thread.Sleep(1);
                }
                Layout();
                lines.Add($"AFTER-RUN|step={workbench.SelectedPipelineStep?.Id}|running={workbench.IsSelectedStepPreviewRunning}|vmRows={workbench.NinePointCompensationRows.Count}|gridRows={grid.Items.Count}|visibility={view.Visibility}|summary={workbench.NinePointCompensationSummary}|binding={System.Windows.Data.BindingOperations.GetBindingExpression(grid, System.Windows.Controls.ItemsControl.ItemsSourceProperty)?.Status}");
                lines.AddRange(workbench.RunLog.Select(item => $"RUNLOG|{item}"));
                Check("real compensation changes zero to nine WPF rows", !workbench.IsSelectedStepPreviewRunning && grid.Items.Count == 9);
                foreach (var kind in new[] { "Source", "Background", "Corrected" })
                {
                    workbench.NinePointPreviewKind = kind; Layout();
                    Check(kind + " preserves nine actual rows", grid.Items.Count == 9 && workbench.NinePointPreviewKind == kind);
                    for (var index = 0; index < grid.Items.Count; index++)
                    {
                        var cellContent = grid.Columns[1].GetCellContent(grid.Items[index]);
                        var content = cellContent is null ? null : Descendants(cellContent).OfType<System.Windows.Controls.TextBlock>().SingleOrDefault();
                        var runs = content?.Inlines.OfType<System.Windows.Documents.Run>()
                            .Where(run => System.Windows.Data.BindingOperations.IsDataBound(run, System.Windows.Documents.Run.TextProperty)).ToArray() ?? [];
                        lines.Add($"COORD|{kind}|row{index + 1}|cell={cellContent?.GetType().Name}|boundRuns={runs.Length}|text={content?.Text}");
                        Check($"{kind} row{index + 1} X/Y Active OneWay", runs.Length == 2 && runs.All(run =>
                        {
                            var binding = System.Windows.Data.BindingOperations.GetBindingExpression(run, System.Windows.Documents.Run.TextProperty);
                            return binding?.Status == System.Windows.Data.BindingStatus.Active && !binding.HasError
                                && binding.ParentBinding.Mode == System.Windows.Data.BindingMode.OneWay && !string.IsNullOrWhiteSpace(run.Text);
                        }));
                    }
                }
                workbench.SelectPipelineStep("step.thickness.1"); Layout();
                Check("different step clears coordinate rows", grid.Items.Count == 0 && view.Visibility == System.Windows.Visibility.Collapsed);
                workbench.SelectPipelineStep("step.nine"); Layout();
                Check("reselection restores nine rows without a setter exception", grid.Items.Count == 9 && view.Visibility == System.Windows.Visibility.Visible);
                Check("binding verification launched no Window", app.Windows.Count == 0);
            }
            catch (Exception exception) { lines.Add(exception.ToString()); Check("WPF coordinate binding activation", false); }
            finally { app?.Shutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();thread.Join();
        Environment.SetEnvironmentVariable("OPENVISIONLAB_3D_TEST_ARTIFACT_ROOT", previousRoot);
        summary = $"Nine-point X/Y WPF bindings: {passed}/{total}";
        File.WriteAllLines(reportPath, lines.Append(summary));
        return total > 0 && passed == total;

        static IEnumerable<System.Windows.DependencyObject> Descendants(System.Windows.DependencyObject parent)
        {
            yield return parent;
            for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
                foreach (var child in Descendants(System.Windows.Media.VisualTreeHelper.GetChild(parent, i))) yield return child;
        }
    }

    public static bool VerifyNineRoiFailureFollowup(string fixturePath, string reportPath, out string summary)
    {
        var lines = new List<string> { "Only the failed cancel/oracle boundaries plus the required normal ROI apply path; prior passing nine-result/rejection/save cases are reused." };
        var passed = 0; var total = 0;
        void Check(string name, bool ok) { total++; if (ok) passed++; lines.Add($"{(ok ? "PASS" : "FAIL")}|{name}"); }
        var root = Path.GetDirectoryName(Path.GetFullPath(reportPath))!;Directory.CreateDirectory(root);
        var previousRoot = Environment.GetEnvironmentVariable("OPENVISIONLAB_3D_TEST_ARTIFACT_ROOT");
        Environment.SetEnvironmentVariable("OPENVISIONLAB_3D_TEST_ARTIFACT_ROOT", root);
        try
        {
            using var workbench = new ToolWorkbenchViewModel(Path.Combine(root, "followup-recent.json"));
            if (!workbench.TryOpenTeachingRecipe(fixturePath, out var message)) throw new InvalidDataException(message);
            Run("step.nine");workbench.SelectPipelineStep("step.thickness.1");
            ToolWorkbenchTeachingCaptureRequestEventArgs? capture = null;
            workbench.BeginTeachingSelectionCaptureRequested += (_, args) => capture = args;
            var before = workbench.GetCurrentAppliedTeachingSelections().ToArray();
            var beforeJson = System.Text.Json.JsonSerializer.Serialize(before);
            var beforeDirty = workbench.IsDirty; var beforeResults = workbench.TeachingResults.Count;
            workbench.BeginTeachingSelectionCaptureCommand.Execute(null);
            var afterBegin = System.Text.Json.JsonSerializer.Serialize(workbench.GetCurrentAppliedTeachingSelections());
            workbench.CancelTeachingSelectionCaptureCommand.Execute(null);
            var after = workbench.GetCurrentAppliedTeachingSelections().ToArray();
            lines.Add($"CANCEL|beforeCount={before.Length}|afterCount={after.Length}|beforeDirty={beforeDirty}|afterDirty={workbench.IsDirty}|resultsBefore={beforeResults}|resultsAfter={workbench.TeachingResults.Count}|active={workbench.IsTeachingSelectionCaptureActive}|equal={before.SequenceEqual(after)}");
            File.WriteAllText(Path.Combine(root, "cancel-before.json"), beforeJson);
            File.WriteAllText(Path.Combine(root, "cancel-after-begin.json"), afterBegin);
            File.WriteAllText(Path.Combine(root, "cancel-after.json"), System.Text.Json.JsonSerializer.Serialize(after));
            Check("cancel preserves exact persisted ROI geometry", beforeJson == System.Text.Json.JsonSerializer.Serialize(after));
            Check("cancel preserves prior dirty/execution state", beforeDirty == workbench.IsDirty && beforeResults == workbench.TeachingResults.Count
                && !workbench.IsTeachingSelectionCaptureActive);
            workbench.BeginTeachingSelectionCaptureCommand.Execute(null);
            if (capture?.ExistingSelection is not { GridRectangle: { } rectangle } existing) throw new InvalidDataException("No existing ROI capture.");
            var replacement = existing with { GridRectangle = rectangle with { Column = rectangle.Column + 1 } };
            var applied = workbench.TryApplyCapturedTeachingSelection(replacement, out message);
            lines.Add("APPLY|" + message);
            Check("explicit apply replaces only the owning ROI without executing", applied && !workbench.IsTeachingSelectionCaptureActive
                && workbench.GetCurrentAppliedTeachingSelections().Single(selection => selection.Id == existing.Id).GridRectangle == replacement.GridRectangle
                && before.Where(selection => selection.Id != existing.Id).All(selection => workbench.GetCurrentAppliedTeachingSelections().Contains(selection))
                && workbench.TeachingResults.Count == beforeResults);
            foreach (var index in new[] { 3, 9 })
            {
                Run($"step.thickness.{index}");
                var row = workbench.TeachingResults.Single(item => item.Step.Id == $"step.thickness.{index}");
                var roi = workbench.GetCurrentAppliedTeachingSelections().Single(selection => selection.Id == $"roi.thickness.{index}").GridRectangle!;
                var actual = ToolWorkbenchViewModel.SelectPrimaryMeasurementMetric("thickness", row.Metrics)!.Value;
                var expected = ExpectedSyntheticLocalHeight(roi);
                lines.Add($"ORACLE|{index}|actual={actual:R}|expected={expected:R}|tolerance=0.00001|input=original producer's two pads and one recess");
                Check($"thickness #{index} agrees with full original fixture declaration", Math.Abs(actual - expected) < .00001);
            }
            var saved = Path.Combine(root, "applied-roi.ov3d-recipe.json");
            Check("applied ROI saves and reopens without changing its identity", workbench.TrySaveTeachingRecipe(saved, out message)
                && ToolRecipeDocumentStore.Load(saved).Selections!.Single(selection => selection.Id == replacement.Id).GridRectangle == replacement.GridRectangle);
            void Run(string stepId)
            {
                if (!workbench.SelectPipelineStep(stepId) || !workbench.RunSelectedStepCommand.CanExecute(null)) throw new InvalidDataException("Cannot run " + stepId);
                workbench.RunSelectedStepCommand.Execute(null);
                if (!SpinWait.SpinUntil(() => !workbench.IsSelectedStepPreviewRunning, TimeSpan.FromSeconds(20))) throw new TimeoutException(stepId);
            }
        }
        catch (Exception exception) { lines.Add(exception.ToString()); Check("failed-boundary followup", false); }
        finally { Environment.SetEnvironmentVariable("OPENVISIONLAB_3D_TEST_ARTIFACT_ROOT", previousRoot); }
        summary = $"Nine-ROI failed boundaries/apply: {passed}/{total}";
        File.WriteAllLines(reportPath, lines.Append(summary));return total > 0 && passed == total;
    }

    private static double ExpectedSyntheticLocalHeight(ToolRecipeGridRectangle roi)
    {
        // The original fixture producer declares two pads and one recess; do not infer an otherwise flat board.
        double Contribution(int row, int column, int rowCount, int columnCount, double height) => height
            * Math.Max(0, Math.Min(roi.Row + roi.RowCount, row + rowCount) - Math.Max(roi.Row, row))
            * Math.Max(0, Math.Min(roi.Column + roi.ColumnCount, column + columnCount) - Math.Max(roi.Column, column));
        return (Contribution(54, 36, 17, 37, 8) + Contribution(133, 187, 17, 29, 5.25) + Contribution(25, 210, 11, 11, -2.5))
            / (roi.RowCount * roi.ColumnCount);
    }

    private static IReadOnlyList<ToolRecipeParameter> Parameters(int middle, double target) =>
    [
        new("MiddleRow", middle.ToString(CultureInfo.InvariantCulture)), new("MiddleColumn", middle.ToString(CultureInfo.InvariantCulture)),
        new("TargetHeight", target.ToString("G17", CultureInfo.InvariantCulture)), new("Semantics", ToolRecipeNinePointSurfaceCompensationExecution.Semantics)
    ];

    private static ToolRecipeDocument CreateDocument(C3DHeightFieldSnapshot source, string path) => new(
        ToolRecipeDocument.CurrentSchemaVersion, "Nine-point synthetic substrate",
        new ToolRecipeSource(source.EntityId, "Synthetic substrate", "C3D", source.Unit, source.FrameId, path,
            source.ByteLength, source.ContentSha256, source.Width, source.Height), [],
        [new("step.nine", ToolRecipeNinePointSurfaceCompensationExecution.ToolId, "Nine-point surface compensation", 1,
            [source.EntityId], "output.nine", Parameters(4, 20))], []);

    // Hold async continuations until Cancel has been requested; no Window or dispatcher is needed.
    private sealed class CompletionQueue : SynchronizationContext, IDisposable
    {
        private readonly System.Collections.Concurrent.ConcurrentQueue<Action> callbacks = new();
        private readonly AutoResetEvent posted = new(false);
        public int ProcessedCount { get; private set; }
        public override void Post(SendOrPostCallback callback, object? state) { callbacks.Enqueue(() => callback(state)); posted.Set(); }
        public bool DrainUntil(Func<bool> done)
        {
            var timeout = System.Diagnostics.Stopwatch.StartNew();
            while (!done() && timeout.Elapsed < TimeSpan.FromSeconds(15))
                if (callbacks.TryDequeue(out var callback)) { callback(); ProcessedCount++; } else posted.WaitOne(20);
            return done();
        }
        public void Dispose() => posted.Dispose();
    }
}
