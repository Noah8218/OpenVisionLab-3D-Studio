using System.IO;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Threading;
using OpenVisionLab;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Reporting.RunRecords;
using OpenVisionLab.ThreeD.Shell;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Verification.Shell.Workbench;

internal static class RunRecordWorkflowKindVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        var checks = new List<(string Name, bool Passed)>();
        var thread = new Thread(() =>
        {
            var previousLanguage = OpenVisionLanguageService.CurrentLanguage;
            var previousContext = SynchronizationContext.Current;
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            void Await(Task task)
            {
                var frame = new DispatcherFrame();
                _ = task.ContinueWith(_ => dispatcher.BeginInvoke(new Action(() => frame.Continue = false)), TaskScheduler.Default);
                Dispatcher.PushFrame(frame);
                task.GetAwaiter().GetResult();
            }
            void Check(string name, bool pass) => checks.Add((name, pass));
            try
            {
                var root = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(reportPath))!, $"workflow-kind-fixture-{Guid.NewGuid():N}");
                Directory.CreateDirectory(root);
                var sourcePath = Path.Combine(root, "input.C3D");
                C3DHeightFieldSnapshot.CreateForVerification("source.kind", 4, 4,
                    [10, 11, 12, 13, 11, 12, 13, 14, 12, 13, 14, 15, 13, 14, 15, 16]).SaveC3D(sourcePath);
                var binding = ToolRecipeSelectionSourceBindingVerifier.ReadIdentity(sourcePath);
                var source = new ToolRecipeSource("source.kind", "Kind fixture", "C3D", "model", "frame.c3d-grid-index",
                    sourcePath, new FileInfo(sourcePath).Length, binding.ContentSha256, binding.GridWidth, binding.GridHeight);
                var first = new ToolRecipeSelection("reference", "Reference", ToolRecipeSelectionKinds.GridRectangle, source.Id, source.FrameId, binding, new(0, 0, 2, 4), null, null);
                var second = new ToolRecipeSelection("measurement", "Measurement", ToolRecipeSelectionKinds.GridRectangle, source.Id, source.FrameId, binding, new(2, 0, 2, 4), null, null);
                var step = new ToolRecipeStep("step.thickness", "thickness", "Thickness", 3, [source.Id, first.Id, second.Id], "result.thickness",
                    [new("MinimumThickness", "0"), new("MaximumThickness", "10"), new("MinimumValidSampleCount", "1")]);
                var document = new ToolRecipeDocument(ToolRecipeDocument.CurrentSchemaVersion, "Kind fixture", source, [], [step], [first, second]);
                var recipePath = Path.Combine(root, "input.ov3d-recipe.json");
                ToolRecipeDocumentStore.SaveNew(recipePath, document);
                var originalRecipe = File.ReadAllBytes(recipePath);
                var originalSource = File.ReadAllBytes(sourcePath);
                var execution = ToolRecipeOrderedGraphExecution.Execute(document, sourcePath);
                var runsRoot = Path.Combine(root, "runs");
                var inspected = ShellOrderedRunRecordWriter.Write(recipePath, document, sourcePath, execution, runsRoot);
                Check("new inspection kind and schema", inspected.Record.WorkflowKind == RunRecordWorkflowKind.Inspection
                    && InspectionRunRecordJson.Read(inspected.JsonPath).WorkflowKind == RunRecordWorkflowKind.Inspection && inspected.Record.SchemaVersion == "1.11");
                var legacyPaths = new List<string>();
                var legacyBytes = new Dictionary<string, byte[]>();
                for (var minor = 0; minor <= 10; minor++)
                {
                    var json = JsonNode.Parse(File.ReadAllText(inspected.JsonPath))!;
                    json.AsObject().Remove("WorkflowKind");
                    json["SchemaVersion"] = $"1.{minor}";
                    var path = Path.Combine(root, $"legacy-{minor}.json");
                    File.WriteAllText(path, json.ToJsonString());
                    legacyPaths.Add(path);
                    legacyBytes[path] = File.ReadAllBytes(path);
                    Check($"legacy1.{minor} missing kind stays Unknown", InspectionRunRecordJson.Read(path).WorkflowKind == RunRecordWorkflowKind.Unknown);
                }
                var bad = JsonNode.Parse(File.ReadAllText(inspected.JsonPath))!;
                bad["WorkflowKind"] = 99;
                var invalid = Path.Combine(root, "invalid-kind.json");
                File.WriteAllText(invalid, bad.ToJsonString());
                try { InspectionRunRecordJson.Read(invalid); Check("invalid kind rejected", false); }
                catch (InvalidDataException) { Check("invalid kind rejected", true); }
                var sample = ToolRecipeValidationSetExecution.Execute(document,
                    [new ToolRecipeValidationSampleInput(sourcePath, ToolRecipeValidationSampleRole.Good) { Expectation = ToolRecipeValidationExpectation.Unset }]).Samples.Single();
                var validated = ShellOrderedRunRecordWriter.WriteValidationSample(sample, runsRoot);
                Check("new validation kind roundtrip", InspectionRunRecordJson.Read(validated.JsonPath).WorkflowKind == RunRecordWorkflowKind.Validation);
                Check("snapshot records executed source and unchanged decision", ToolRecipeDocumentStore.Load(validated.Record.Recipe.Path).Source.ContentSha256 == sample.SourceContentSha256
                    && validated.Record.Status == sample.Status && validated.Record.Message.Contains("Expected: Unset"));
                Check("snapshot recipe hash matches recorded evidence", Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(validated.Record.Recipe.Path))) == validated.Record.Recipe.Sha256);
                Check("history query carries explicit kind", RunRecordHistoryQuery.Read(runsRoot).Records.Select(row => row.WorkflowKind).Order().SequenceEqual(new[] { RunRecordWorkflowKind.Inspection, RunRecordWorkflowKind.Validation }));
                var recentPath = Path.Combine(root, "recent.json");
                RecipeRecentFileStore.Save(recentPath, legacyPaths.Take(1).Append(inspected.JsonPath).Append(validated.JsonPath));
                ShellMainWindowViewModel CreateShell(string name, string? output = null) => new(recentRunRecordsPath: recentPath,
                    recentRecipesPath: Path.Combine(root, name + "-recipes.json"), orderedRunRecordRoot: output ?? runsRoot, integrationSettingsPath: Path.Combine(root, name + "-integration.json"));
                using var shell = CreateShell("filter");
                var count = new TextBlock { DataContext = shell };
                count.SetBinding(TextBlock.TextProperty, new Binding("RunRecordHistory.Count"));
                var historyView = new OpenVisionLab.ThreeD.Shell.Views.Workbench.ResultsHistoryView { DataContext = shell };
                var list = (ListBox)historyView.FindName("StoredHistoryList");
                historyView.Measure(new Size(1280, 760));
                historyView.Arrange(new Rect(0, 0, 1280, 760));
                historyView.UpdateLayout();
                Check("compiled history view owns selection explicitly", list.IsSynchronizedWithCurrentItem == false);
                void Drain() => dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
                Drain();
                Check("All includes legacy Unknown", list.Items.Count == 3 && shell.IsAllRunRecordsSelected);
                var selected = shell.RecentRunRecords.Single(row => row.WorkflowKind == RunRecordWorkflowKind.Inspection);
                list.SelectedItem = selected;
                Drain();
                shell.SelectRunRecordKindCommand.Execute("Inspection");
                Drain();
                Check("Inspection filter and matching selection", list.Items.Count == 1 && ReferenceEquals(list.SelectedItem, selected) && ReferenceEquals(shell.SelectedRecentRunRecord, selected));
                Check("count and trend use the selected kind", count.Text == "1" && shell.ResultsWorkspace.TrendPoints.Count > 0
                    && shell.ResultsWorkspace.TrendPoints.All(point => point.Record.WorkflowKind == RunRecordWorkflowKind.Inspection));
                shell.SelectRunRecordKindCommand.Execute("Inspection");
                Check("repeat selection remains checked", shell.IsInspectionRunRecordsSelected);
                shell.SelectRunRecordKindCommand.Execute("Validation");
                Drain();
                Check("Validation filter clears hidden selection", list.Items.Count == 1 && shell.SelectedRecentRunRecord is null && list.SelectedItem is null
                    && shell.RunRecordHistory.Cast<RunRecordRecentItem>().Single().WorkflowKind == RunRecordWorkflowKind.Validation);
                shell.RunRecordHistory.MoveCurrentToFirst();
                Drain();
                Check("collection cursor cannot select another kind", shell.SelectedRecentRunRecord is null && list.SelectedItem is null);
                foreach (var language in new[] { OpenVisionLanguage.Korean, OpenVisionLanguage.English })
                {
                    OpenVisionLanguageService.SetLanguage(language, save: false);
                    Drain();
                    Check($"language preserves filter {language}", shell.IsValidationRunRecordsSelected && list.Items.Count == 1 && !shell.Workbench.HasOrderedRunResult);
                }
                shell.SelectRunRecordKindCommand.Execute("All");
                Drain();
                Check("All restore and no implicit execution", list.Items.Count == 3 && !shell.Workbench.HasOrderedRunResult);
                Check("All restores visible record count", count.Text == "3");
                Check("recipe opens for explicit validation", shell.Workbench.TryOpenTeachingRecipe(recipePath, out _));
                shell.Workbench.SetValidationSetSources([sourcePath]);
                Await(shell.Workbench.RunValidationSetAsync());
                Check("actual validation completion persists and retains result", shell.RecentRunRecords.Count(row => row.WorkflowKind == RunRecordWorkflowKind.Validation) == 2
                    && shell.Workbench.ValidationSetSamples.Single().Status is "Pass" or "Warning" && !shell.Workbench.HasOrderedRunResult);
                var countAfterRun = Directory.GetFiles(runsRoot, "run-record.json", SearchOption.AllDirectories).Length;
                using (var canceled = new CancellationTokenSource())
                {
                    canceled.Cancel();
                    try { ToolRecipeValidationSetExecution.Execute(document, [sourcePath], canceled.Token); Check("cancel keeps records unchanged", false); }
                    catch (OperationCanceledException) { Check("cancel keeps records unchanged", Directory.GetFiles(runsRoot, "run-record.json", SearchOption.AllDirectories).Length == countAfterRun); }
                }
                var deniedRoot = Path.Combine(root, "file-as-output-root");
                File.WriteAllText(deniedRoot, "owned failure fixture");
                using (var failing = CreateShell("failure", deniedRoot))
                {
                    Check("failure fixture recipe opens", failing.Workbench.TryOpenTeachingRecipe(recipePath, out _));
                    failing.Workbench.SetValidationSetSources([sourcePath]);
                    Await(failing.Workbench.RunValidationSetAsync());
                    Check("save failure retains results with visible notice", failing.Workbench.ValidationSetSamples.Single().Status is "Pass" or "Warning"
                        && !string.IsNullOrWhiteSpace(failing.Workbench.ValidationSetRunNoticeText) && failing.StatusText.Contains("0/1"));
                    File.Delete(deniedRoot);
                    Await(failing.Workbench.RunValidationSetAsync());
                    Check("valid retry recovers persistence and notice", failing.Workbench.ValidationSetRunNoticeText.Length == 0
                        && Directory.GetFiles(deniedRoot, "run-record.json", SearchOption.AllDirectories).Length == 1);
                }
                Check("legacy bytes preserved", legacyBytes.All(pair => File.ReadAllBytes(pair.Key).SequenceEqual(pair.Value)));
                using (var reopened = CreateShell("reopen"))
                    Check("reopen restores explicit kinds without execution", reopened.RecentRunRecords.Any(row => row.WorkflowKind == RunRecordWorkflowKind.Unknown)
                        && reopened.RecentRunRecords.Any(row => row.WorkflowKind == RunRecordWorkflowKind.Inspection)
                        && reopened.RecentRunRecords.Any(row => row.WorkflowKind == RunRecordWorkflowKind.Validation) && !reopened.Workbench.HasOrderedRunResult);
                var partialRoot = Path.Combine(root, "partial-runs");
                using (var partial = CreateShell("partial", partialRoot))
                {
                    partial.Workbench.TryOpenTeachingRecipe(recipePath, out _);
                    partial.Workbench.SetValidationSetSources([sourcePath, Path.Combine(root, "missing.C3D")]);
                    Await(partial.Workbench.RunValidationSetAsync());
                    Check("partial failure keeps all sample outcomes and reports unsaved evidence", partial.Workbench.ValidationSetSamples.Count == 2
                        && partial.Workbench.ValidationSetSamples.Any(row => row.Status == "Error") && partial.StatusText.Contains("1/2")
                        && !string.IsNullOrWhiteSpace(partial.Workbench.ValidationSetRunNoticeText)
                        && Directory.GetFiles(partialRoot, "run-record.json", SearchOption.AllDirectories).Length == 1);
                }
                Check("authored recipe and source bytes preserved", File.ReadAllBytes(recipePath).SequenceEqual(originalRecipe) && File.ReadAllBytes(sourcePath).SequenceEqual(originalSource));
                Check("no temporary recipe snapshots left", !Directory.EnumerateFiles(runsRoot).Any(path => Path.GetFileName(path).Contains(".tmp.")));
                BindingOperations.ClearAllBindings(list);
                BindingOperations.ClearAllBindings(count);
                Check("no desktop Window created", (Application.Current?.Windows.Count ?? 0) == 0);
            }
            catch (Exception exception)
            {
                checks.Add(("Unexpected: " + exception, false));
            }
            finally
            {
                OpenVisionLanguageService.SetLanguage(previousLanguage, save: false);
                SynchronizationContext.SetSynchronizationContext(previousContext);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        summary = $"Run Record workflow kind: {(checks.All(item => item.Passed) ? "PASS" : "FAIL")} ({checks.Count(item => item.Passed)}/{checks.Count})";
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllLines(reportPath, checks.Select(item => $"{(item.Passed ? "PASS" : "FAIL")} | {item.Name}").Prepend(summary));
        return checks.All(item => item.Passed);
    }

    public static bool VerifyTrendSelection(string recentRecordsPath, string reportPath, out string summary)
    {
        var checks = new List<(string Name, bool Passed)>();
        var thread = new Thread(() =>
        {
            try
            {
                var points = RecipeRecentFileStore.Load(recentRecordsPath).Select(path =>
                {
                    var record = InspectionRunRecordJson.Read(path);
                    var snapshot = ResultsRunSnapshot.FromRecord(record)!;
                    var item = new RunRecordRecentItem(path, record.RunId, snapshot.RecordedAtText, record.Status.ToString(), snapshot.StepCount, true) { Snapshot = snapshot, WorkflowKind = record.WorkflowKind };
                    return new ResultsTrendPoint(item, snapshot, snapshot.Metrics.First(metric => metric.IsFinite));
                }).ToArray();
                if (points.Length < 2) throw new InvalidDataException("Two real stored records are required.");
                var plot = new OpenVisionLab.ThreeD.Shell.Views.Workbench.ResultsTrendPlot { Points = points };
                var field = plot.GetType().GetField("selectedIndex", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
                field.SetValue(plot, points.Length - 1);
                var selectedPath = points[^1].Record.Path;
                plot.Points = points.Select(point => point with { Record = point.Record with { } }).ToArray();
                checks.Add(("rebuilt point objects preserve the selected record", plot.Points[(int)field.GetValue(plot)!].Record.Path == selectedPath));
                plot.Points = plot.Points.Reverse().ToArray();
                checks.Add(("reordering preserves record and metric identity", (int)field.GetValue(plot)! == 0 && plot.Points[0].Record.Path == selectedPath));
                plot.Points = plot.Points.Skip(1).ToArray();
                checks.Add(("removed selection falls back to a visible point", (int)field.GetValue(plot)! == 0 && plot.Points[0].Record.Path != selectedPath));
                plot.Points = [];
                checks.Add(("empty history clears selection safely", (int)field.GetValue(plot)! == 0));
                checks.Add(("no desktop Window created", (Application.Current?.Windows.Count ?? 0) == 0));
            }
            catch (Exception exception) { checks.Add(("Unexpected: " + exception, false)); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        summary = $"Stored trend selection: {(checks.All(item => item.Passed) ? "PASS" : "FAIL")} ({checks.Count(item => item.Passed)}/{checks.Count})";
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllLines(reportPath, checks.Select(item => $"{(item.Passed ? "PASS" : "FAIL")} | {item.Name}").Prepend(summary));
        return checks.All(item => item.Passed);
    }
}
