using System.Collections.Concurrent;
using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Shell;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Verification.Shell.Workbench;

internal static class ValidationSetWorkspaceViewModelVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        var result = Task.Run(() => VerifyAsync(reportPath)).GetAwaiter().GetResult();
        summary = result.Summary;
        return result.Passed;
    }

    private static async Task<(bool Passed, string Summary)> VerifyAsync(string reportPath)
    {
        var fullReportPath = Path.GetFullPath(reportPath);
        var fixtureRoot = Path.Combine(Path.GetDirectoryName(fullReportPath)!, "validation-workspace-fixture");
        Directory.CreateDirectory(fixtureRoot);
        var lines = new List<string> { "Validation Set workspace independent feature verification" };
        var passed = 0;
        var total = 0;

        void Check(string name, bool condition, string detail)
        {
            total++;
            if (condition) passed++;
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
            Console.WriteLine($"ValidationWorkspace {total}: {(condition ? "PASS" : "FAIL")} | {name}");
        }

        try
        {
            var taughtPath = Path.Combine(fixtureRoot, "taught.C3D");
            var passPath = Path.Combine(fixtureRoot, "pass.C3D");
            var failPath = Path.Combine(fixtureRoot, "fail.C3D");
            C3DHeightFieldSnapshot.CreateForVerification(
                "source.validation-workspace", 4, 4,
                [10, 11, 12, 13, 11, 12, 13, 14, 12, 13, 14, 15, 13, 14, 15, 16]).SaveC3D(taughtPath);
            C3DHeightFieldSnapshot.CreateForVerification(
                "source.validation-workspace", 4, 4,
                [9, 10, 11, 12, 10, 11, 12, 13, 11, 12, 13, 14, 12, 13, 14, 15]).SaveC3D(passPath);
            C3DHeightFieldSnapshot.CreateForVerification(
                "source.validation-workspace", 4, 4,
                [10, 11, 12, 13, 11, 12, 13, 14, 32, 33, 34, 35, 33, 34, 35, 36]).SaveC3D(failPath);
            var document = CreateDocument(taughtPath);
            var sourceBefore = document.Source;
            var recipePath = Path.Combine(fixtureRoot, "workspace.ov3d-recipe.json");
            var english = true;
            var documentReads = 0;
            var failDocumentRead = false;
            var log = new List<(string Category, string Message)>();

            ValidationSetWorkspaceViewModel CreateWorkspace(Func<string, CancellationToken, Task<System.Windows.Media.Imaging.BitmapSource>>? loader = null) => new(
                () => { documentReads++; if (failDocumentRead) throw new InvalidDataException("isolated recipe read failure"); return document; },
                () => document.Name,
                () => recipePath,
                () => taughtPath,
                () => true,
                [],
                () => false,
                () => null,
                _ => false,
                new ToolWorkbenchStepPropertySession(),
                (category, message) => log.Add((category, message)),
                (korean, translated) => english ? translated : korean,
                status => english ? status.ToString() : status switch
                {
                    ResultStatus.Pass => "통과",
                    ResultStatus.Fail => "실패",
                    ResultStatus.Warning => "경고",
                    _ => "오류"
                }, previewLoader: loader);

            using var workspace = CreateWorkspace();
            var sourceRequests = 0;
            var catalogRequests = 0;
            var clearPinRequests = 0;
            var dirtyRequests = 0;
            var notificationNames = new ConcurrentQueue<string?>();
            var notificationSendersMatch = true;
            ValidationSetSampleRow? comparisonSample = null;
            workspace.PropertyChanged += (sender, args) =>
            {
                notificationSendersMatch &= ReferenceEquals(sender, workspace);
                notificationNames.Enqueue(args.PropertyName);
            };
            workspace.SelectValidationSetSourcesRequested += (_, _) => sourceRequests++;
            workspace.SamplesChanged += (_, _) => catalogRequests++;
            workspace.ComparePinsClearRequested += (_, _) => clearPinRequests++;
            workspace.DefinitionDirtyChanged += (_, _) => dirtyRequests++;
            workspace.ComparisonRequested += (_, sample) => comparisonSample = sample;
            Check("initial execution basis contains no invented timestamp", workspace.ValidationSetLastRunAt is null && !workspace.IsValidationSetStale, workspace.ValidationSetRunBasisText);
            Check("independent construction is idle and does not execute", documentReads == 1
                && workspace.ValidationSetAllCount == 0
                && !workspace.IsValidationSetRunning
                && log.Count == 0,
                $"documentReads={documentReads};samples={workspace.ValidationSetAllCount};logs={log.Count}");

            workspace.SetValidationSetSources([passPath, failPath, passPath.ToUpperInvariant()]);
            workspace.SelectedValidationSetSample = workspace.AllSamples[1];
            workspace.SetValidationSampleRoleCommand.Execute("Bad");
            var nonSelectedSample = workspace.AllSamples[0];
            workspace.SetValidationSampleRoleCommand.Execute(nonSelectedSample.HeldOutRoleChange);
            Check("row role command changes its explicit sample and preserves the other selected sample without execution",
                workspace.AllSamples[0].IsHeldOut && workspace.AllSamples[1].IsBad
                && workspace.SelectedValidationSetSample?.SourcePath == failPath && documentReads == 1
                && workspace.AllSamples.All(sample => sample.Status == "Pending") && !workspace.IsValidationSetRunning,
                $"target={workspace.AllSamples[0].Role};selected={workspace.SelectedValidationSetSample?.FileName};reads={documentReads}");
            Check("row role command rejects stale, missing and undefined targets",
                !workspace.SetValidationSampleRoleCommand.CanExecute(nonSelectedSample.BadRoleChange)
                && !workspace.SetValidationSampleRoleCommand.CanExecute(new ValidationSampleRoleChange(workspace.AllSamples[0], (ToolRecipeValidationSampleRole)999))
                && !workspace.SetValidationSampleRoleCommand.CanExecute(new ValidationSampleRoleChange(nonSelectedSample with { SourcePath = "absent.C3D" }, ToolRecipeValidationSampleRole.Bad)),
                "immutable replacement invalidates the old row; only canonical defined roles are accepted");
            workspace.SetValidationSampleRoleCommand.Execute(workspace.AllSamples[0].GoodRoleChange);
            Check("expected verdict changes preserve held-out use", workspace.AllSamples[0].IsGood && workspace.AllSamples[0].IsHeldOut, "expected OK; use remains held-out");
            workspace.SetValidationSampleRoleCommand.Execute(workspace.AllSamples[0].HeldOutRoleChange);
            Check("legacy selected-role command and independent use remain compatible",
                workspace.ValidationSetGoodCount == 1 && workspace.ValidationSetBadCount == 1 && workspace.ValidationSetHeldOutCount == 0
                && workspace.SelectedValidationSetSample?.SourcePath == failPath && !workspace.IsValidationSetRunning,
                $"good={workspace.ValidationSetGoodCount};bad={workspace.ValidationSetBadCount};held={workspace.ValidationSetHeldOutCount}");
            Check("row role command parameters do not recurse when WPF automation formats their owning record",
                workspace.AllSamples[0].ToString().Contains(workspace.AllSamples[0].FileName, StringComparison.Ordinal),
                "record formatting includes the source identity and finite command parameter type names");
            Check("staging and role changes stay inside the feature without execution", workspace.ValidationSetAllCount == 2
                && workspace.ValidationSetGoodCount == 1
                && workspace.ValidationSetBadCount == 1
                && workspace.AllSamples.All(sample => sample.Status == "Pending")
                && workspace.IsValidationSetDefinitionDirty
                && !workspace.IsValidationSetRunning
                && documentReads == 1
                && sourceRequests == 0
                && dirtyRequests > 0
                && catalogRequests > 0,
                $"samples={workspace.ValidationSetAllCount};good={workspace.ValidationSetGoodCount};bad={workspace.ValidationSetBadCount};reads={documentReads};catalog={catalogRequests}");

            workspace.SelectValidationSetSourcesCommand.Execute(null);
            Check("source selection is an explicit host request", sourceRequests == 1
                && workspace.AllSamples.All(sample => sample.Status == "Pending")
                && document.Source == sourceBefore,
                $"requests={sourceRequests};sourceSame={document.Source == sourceBefore}");

            workspace.SaveValidationSetDefinition(recipePath);
            using var reopened = CreateWorkspace();
            reopened.LoadValidationSetDefinition(recipePath, document);
            Check("definition save and reopen preserve ordered roles without execution", !workspace.IsValidationSetDefinitionDirty
                && !reopened.IsValidationSetDefinitionDirty
                && reopened.AllSamples.Select(sample => (sample.SourcePath, sample.Role))
                    .SequenceEqual(workspace.AllSamples.Select(sample => (sample.SourcePath, sample.Role)))
                && reopened.AllSamples.All(sample => sample.Status == "Pending")
                && !reopened.IsValidationSetRunning,
                $"reopened={reopened.ValidationSetAllCount};dirty={reopened.IsValidationSetDefinitionDirty}");

            await workspace.RunValidationSetAsync();
            Check("explicit run projects results and selects the first issue", workspace.ValidationSetPassCount == 1
                && workspace.ValidationSetFailCount == 1
                && workspace.SelectedValidationSetSample?.Status == "Fail"
                && workspace.SelectedValidationSetStep is { Metrics.Count: > 0 }
                && workspace.HasValidationEvidence
                && !workspace.IsValidationSetRunning
                && document.Source == sourceBefore,
                $"pass={workspace.ValidationSetPassCount};fail={workspace.ValidationSetFailCount};selected={workspace.SelectedValidationSetSample?.Status};summary={workspace.ValidationSetSummary}");

            var completedAt = workspace.ValidationSetLastRunAt;
            var completedBasis = workspace.ValidationSetRunBasisText;
            var completedRows = workspace.AllSamples.ToArray();
            Check("explicit run associates basis with projected results, without invented revision", completedAt is not null
                && completedBasis.Contains(document.Name) && !workspace.IsValidationSetStale && !completedBasis.Contains("rev "), completedBasis);
            var capturedDocument = document;
            document = document with { Steps = document.Steps.Select(step => step with { Parameters = step.Parameters.Select(parameter => parameter.Name == "MaximumThickness" ? parameter with { Value = "8" } : parameter).ToArray() }).ToArray() };
            workspace.RefreshValidationSetCapability();
            Check("numerical applied edit marks prior basis stale without changing execution evidence", workspace.IsValidationSetStale
                && workspace.ValidationSetLastRunAt == completedAt && workspace.AllSamples.SequenceEqual(completedRows) && !workspace.IsValidationSetRunning, workspace.ValidationSetRunBasisText);
            document = capturedDocument with { Name = "Descriptive rename", Steps = capturedDocument.Steps.Select(step => step with { ToolName = "Descriptive tool rename", Group = new ToolRecipeStepGroup("descriptive-group", "Descriptive group") }).ToArray() };
            workspace.RefreshValidationSetCapability();
            Check("restoring equal calculation clears stale despite names and group labels", !workspace.IsValidationSetStale
                && workspace.ValidationSetRunBasisText == completedBasis && workspace.ValidationSetLastRunAt == completedAt, "CreateStepExecutionContext excludes descriptive metadata");
            document = capturedDocument with { Source = capturedDocument.Source with { Id = "different-input-identity" } };
            workspace.RefreshValidationSetCapability();
            Check("input identity change marks conditions stale", workspace.IsValidationSetStale, "source identity is explicitly included");
            document = capturedDocument;
            workspace.RefreshValidationSetCapability();

            failDocumentRead = true;
            await workspace.RunValidationSetAsync();
            failDocumentRead = false;
            Check("failed rerun preserves prior result basis and exposes actual failure notice", workspace.ValidationSetLastRunAt == completedAt
                && workspace.AllSamples.SequenceEqual(completedRows) && workspace.ValidationSetRunNoticeText.Contains("isolated recipe read failure"), workspace.ValidationSetRunNoticeText);

            var matrixColumns = workspace.ValidationMatrixColumns;
            var observedValues = workspace.ValidationDistributionOutputs.SelectMany(output => output.Points.Select(point => (output.Column.StepId, output.Column.MetricName, output.Column.Unit, point.Sample.Order, point.Value))).ToArray();
            var expectedValues = workspace.AllSamples.SelectMany(sample => sample.Steps.SelectMany(step => step.Metrics.Where(metric => !string.IsNullOrWhiteSpace(metric.Status) && metric.NumericValue is { } value && double.IsFinite(value))
                .Select(metric => (step.StepId, metric.Name, metric.Unit, sample.Order, metric.NumericValue!.Value)))).ToArray();
            Check("validation distribution projects original numeric evidence, sample identity and units",
                observedValues.Length > 0 && observedValues.ToHashSet().SetEquals(expectedValues) && workspace.SelectedValidationDistributionOutput is not null
                && !workspace.IsValidationSetRunning && document.Source == sourceBefore,
                $"outputs={workspace.ValidationDistributionOutputs.Count};points={observedValues.Length};expected={expectedValues.Length}");

            ValidationSetSampleRow ReviewRow(int order, ToolRecipeValidationSampleRole role, string status, double value) => new(order, passPath, role, status, status, "actual evidence", "1 ms",
                [new ValidationSetStepRow(1, "step.test", "Test", status, status, "actual step", [new ValidationSetMetricRow("Mean", value.ToString(System.Globalization.CultureInfo.InvariantCulture), "mm", "Pass", "Pass", value),
                    new ValidationSetMetricRow("LowerLimit", "7.75", "mm", "", "", 7.75), new ValidationSetMetricRow("UpperLimit", "8.25", "mm", "", "", 8.25)], [])]);
            var reviewSamples = new System.Collections.ObjectModel.ObservableCollection<ValidationSetSampleRow>
            {
                ReviewRow(1, ToolRecipeValidationSampleRole.Good, "Pass", 8), ReviewRow(2, ToolRecipeValidationSampleRole.Bad, "Pass", 8.1),
                ReviewRow(3, ToolRecipeValidationSampleRole.Good, "Fail", 9), ReviewRow(4, ToolRecipeValidationSampleRole.Bad, "Fail", 10),
                ReviewRow(5, ToolRecipeValidationSampleRole.Good, "Error", 0), ReviewRow(6, ToolRecipeValidationSampleRole.HeldOut, "Pass", 8)
            };
            var comparisons = 0;
            var review = new ToolWorkbenchValidationSetReviewOwner(reviewSamples, () => false, () => true, () => comparisons++);
            review.RefreshSamples();
            Check("mismatch review includes false accepts, false rejects and errors while preserving expected Bad failures and held-out",
                review.ValidationMismatchSamples.Select(sample => sample.Order).SequenceEqual([2, 3, 5]) && review.HasValidationMismatches
                && reviewSamples[0].Agreement == "Match" && reviewSamples[3].Agreement == "Match" && reviewSamples[5].Agreement == "Unset",
                $"mismatches={string.Join(",", review.ValidationMismatchSamples.Select(sample => sample.Order))}");
            var mixed = reviewSamples[4] with { Steps = [reviewSamples[2].Steps[0], reviewSamples[4].Steps[0]] };
            Check("mismatch card prioritizes recorded Error over earlier failure", mixed.MismatchStep?.Status == "Error"
                && mixed.MismatchEvidenceText.Contains("7.75") && mixed.MismatchEvidenceText.Contains("8.25") && mixed.MismatchEvidenceText.Contains("Error"), mixed.MismatchEvidenceText);
            Check("missed defect card states pass facts without inferred cause", reviewSamples[1].MismatchStep is null
                && reviewSamples[1].HasReviewOutput && !reviewSamples[1].MismatchEvidenceText.Contains("mean", StringComparison.OrdinalIgnoreCase), reviewSamples[1].MismatchEvidenceText);
            var absent = reviewSamples[4] with { Steps = [] };
            Check("missing recorded issue is explicit and has no distribution output", absent.MismatchStep is null && !absent.HasReviewOutput
                && absent.MismatchEvidenceText.Contains("actual evidence"), absent.MismatchEvidenceText);
            var mixedMetric = reviewSamples[2] with { Steps = [reviewSamples[2].Steps[0] with { Metrics = [new ValidationSetMetricRow("Earlier pass", "8", "mm", "Pass", "Pass", 8),
                new ValidationSetMetricRow("Actual failure", "13", "mm", "Fail", "Fail", 13), new ValidationSetMetricRow("LowerLimit", "7.75", "mm", "", "", 7.75), new ValidationSetMetricRow("UpperLimit", "8.25", "mm", "", "", 8.25)] }] };
            Check("mismatch metric prioritizes actual failure over earlier passing output", mixedMetric.ReviewOutputMetric?.Name == "Actual failure"
                && mixedMetric.MismatchEvidenceText.Contains("13") && !mixedMetric.MismatchEvidenceText.Contains("Earlier pass"), mixedMetric.MismatchEvidenceText);
            Check("miss with no judged metrics does not claim recorded passes", (reviewSamples[1] with { Steps = [] }).MismatchEvidenceText.Contains("insufficient")
                || (reviewSamples[1] with { Steps = [] }).MismatchEvidenceText.Contains("충분하지"), "No invented passing evidence");
            review.SelectedValidationSetSample = mixed;
            Check("ordinary selection preserves first failure while card command alone prioritizes Error", review.SelectedValidationSetStep?.Status == "Fail", "Existing Results/Samples selection contract");
            var immutableEvidence = reviewSamples[2].MismatchEvidenceText;
            review.SelectedValidationSetSample = reviewSamples[0];
            review.SelectValidationMismatchDistributionCommand.Execute(reviewSamples[2]);
            Check("unselected mismatch distribution targets its own sample step metric and recorded limits", ReferenceEquals(review.SelectedValidationSetSample, reviewSamples[2])
                && review.SelectedValidationSetStep?.StepId == "step.test" && review.SelectedValidationDistributionOutput?.Column.MetricName == "Mean"
                && reviewSamples[2].MismatchEvidenceText == immutableEvidence && comparisons == 0, review.SelectedValidationDistributionReadout);
            var vanished = reviewSamples[2] with { SourcePath = Path.Combine(fixtureRoot, "missing-input.c3d") };
            Check("missing and detached card command guards reject stale row targets", !review.SelectValidationMismatchSampleCommand.CanExecute(vanished)
                && !review.OpenValidationSetComparisonCommand.CanExecute(vanished), "No selection/run side effects");
            using (var previewWorkspace = CreateWorkspace())
            {
                var previewBad = Path.Combine(fixtureRoot, "preview-corrupt.c3d");
                File.WriteAllText(previewBad, "not a c3d");
                previewWorkspace.SetValidationSetSources([passPath, failPath, previewBad, passPath.ToUpperInvariant()]);
                previewWorkspace.SetValidationSampleRoleCommand.Execute(previewWorkspace.AllSamples[1].GoodRoleChange);
                previewWorkspace.SelectedValidationSetSample = previewWorkspace.AllSamples[0];
                previewWorkspace.SetValidationSampleRoleCommand.Execute("Bad");
                await previewWorkspace.RunValidationSetAsync();
                await previewWorkspace.ValidationMismatchPreviewTask;
                Check("independent mismatch previews load unselected files and isolate corrupt input", previewWorkspace.ValidationMismatchPreviews.Count == 2
                    && previewWorkspace.ValidationMismatchPreviews.ContainsKey(passPath) && previewWorkspace.ValidationMismatchPreviews.ContainsKey(failPath)
                    && !previewWorkspace.ValidationMismatchPreviews.ContainsKey(previewBad) && previewWorkspace.AllSamples.Count == 3,
                    $"previews={previewWorkspace.ValidationMismatchPreviews.Count};canonical={previewWorkspace.AllSamples.Count}");
                Check("preview cache retains only small frozen display pixels", previewWorkspace.ValidationMismatchPreviews.Values.All(image => image.IsFrozen && image.PixelWidth <= 150 && image.PixelHeight <= 98), "No source snapshot retained in cache");
                var logsBeforePreview = log.Count;
                previewWorkspace.SelectedValidationSetSample = previewWorkspace.AllSamples[2];
                previewWorkspace.SelectValidationMismatchSampleCommand.Execute(previewWorkspace.AllSamples[0]);
                Check("preview and card selection cause no implicit execution or save", log.Count == logsBeforePreview && !previewWorkspace.IsValidationSetRunning, "Only preceding explicit Run produced results");
                previewWorkspace.SetValidationSetSources([passPath]);
                var staleTask = previewWorkspace.ValidationMismatchPreviewTask;
                previewWorkspace.ClearValidationSet();
                await staleTask;
                await previewWorkspace.ValidationMismatchPreviewTask;
                Check("replacement and clear ignore old preview completion", previewWorkspace.ValidationMismatchPreviews.Count == 0 && previewWorkspace.AllSamples.Count == 0, "generation invalidated");
                previewWorkspace.SetValidationSetSources([passPath, previewBad]);
                await previewWorkspace.RunValidationSetAsync();
                var pending = previewWorkspace.ValidationMismatchPreviewTask;
                previewWorkspace.Dispose();
                await pending;
                Check("workspace Dispose cancels preview publication and releases cache", previewWorkspace.ValidationMismatchPreviews.Count == 0 && previewWorkspace.IsDisposed, "Window0; no viewer frame mutation");
            }
            var headlessPreviewCompleted = await Task.Run(async () =>
            {
                _ = System.Windows.Threading.Dispatcher.CurrentDispatcher;
                using var headless = CreateWorkspace();
                headless.SetValidationSetSources([passPath]);
                headless.SelectedValidationSetSample = headless.AllSamples[0];
                headless.SetValidationSampleRoleCommand.Execute("Bad");
                await headless.RunValidationSetAsync();
                await headless.ValidationMismatchPreviewTask.WaitAsync(TimeSpan.FromSeconds(5));
                return Thread.CurrentThread.GetApartmentState() == ApartmentState.MTA
                    && headless.ValidationMismatchPreviews.ContainsKey(passPath);
            });
            Check("headless preview ignores an incidental non-pumping worker Dispatcher", headlessPreviewCompleted,
                "Actual C3D mismatch preview completes on an MTA worker without pumping its Dispatcher.");
            using (var reloadPreview = CreateWorkspace())
            {
                var replacePath = Path.Combine(fixtureRoot, "preview-replaced-input.c3d");
                File.Copy(passPath, replacePath, overwrite: true);
                reloadPreview.SetValidationSetSources([replacePath]);
                reloadPreview.SelectedValidationSetSample = reloadPreview.AllSamples[0];
                reloadPreview.SetValidationSampleRoleCommand.Execute("Bad");
                await reloadPreview.RunValidationSetAsync();
                await reloadPreview.ValidationMismatchPreviewTask;
                Check("current input preview starts from actual independent file", reloadPreview.ValidationMismatchPreviews.ContainsKey(replacePath), "Not selected Viewer frame");
                File.WriteAllText(replacePath, "replacement input is corrupt");
                await reloadPreview.RunValidationSetAsync();
                await reloadPreview.ValidationMismatchPreviewTask;
                Check("explicit rerun reloads changed file and does not retain earlier preview", reloadPreview.AllSamples[0].Status == "Error" && reloadPreview.ValidationMismatchPreviews.Count == 0, "Per-file failure is recoverable");
                File.Copy(passPath, replacePath, overwrite: true);
                await reloadPreview.RunValidationSetAsync();
                await reloadPreview.ValidationMismatchPreviewTask;
                Check("valid input recovery restores only its own preview", reloadPreview.AllSamples[0].Status == "Pass" && reloadPreview.ValidationMismatchPreviews.Count == 1
                    && reloadPreview.ValidationMismatchPreviews.ContainsKey(replacePath), "All three executions were explicit");
            }
            foreach (var dispose in new[] { false, true })
            {
                var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var released = new TaskCompletionSource<System.Windows.Media.Imaging.BitmapSource>(TaskCreationOptions.RunContinuationsAsynchronously);
                CancellationToken observedCancellation = default;
                using var controlled = CreateWorkspace((_, token) => { observedCancellation = token; started.TrySetResult(); return released.Task; });
                controlled.SetValidationSetSources([passPath]);
                controlled.SelectedValidationSetSample = controlled.AllSamples[0];
                controlled.SetValidationSampleRoleCommand.Execute("Bad");
                await controlled.RunValidationSetAsync();
                await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
                var pending = controlled.ValidationMismatchPreviewTask;
                var publications = 0;
                controlled.PropertyChanged += (_, args) => { if (args.PropertyName == nameof(controlled.ValidationMismatchPreviews)) publications++; };
                Check($"controlled preview really starts before {(dispose ? "Dispose" : "clear")}", !pending.IsCompleted && !observedCancellation.IsCancellationRequested, "TaskCompletionSource holds actual in-flight loader boundary");
                if (dispose) controlled.Dispose(); else controlled.ClearValidationSet();
                var notices = publications;
                var pixels = System.Windows.Media.Imaging.BitmapSource.Create(1, 1, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null, new byte[] { 0, 0, 0, 255 }, 4);
                pixels.Freeze();
                released.SetResult(pixels);
                await pending.WaitAsync(TimeSpan.FromSeconds(10));
                Check($"late preview after {(dispose ? "Dispose" : "clear")} is canceled and never published", observedCancellation.IsCancellationRequested
                    && controlled.ValidationMismatchPreviews.Count == 0 && publications == notices, "No late cache write or PropertyChanged callback");
            }
            Check("agreement summary partitions adjustment samples and keeps held-out outside Match",
                review.ValidationSetAdjustmentCount == 5 && review.ValidationSetMatchCount == 2
                && review.ValidationSetMismatchCount == 3 && review.ValidationSetFalseRejectCount == 1
                && review.ValidationSetFalseAcceptCount == 1 && review.ValidationSetAdjustmentErrorCount == 1
                && review.ValidationSetAdjustmentPendingCount == 0 && review.ValidationSetHeldOutCompletedCount == 1,
                "Expected: Good/Pass and Bad/Fail match; Bad/Pass is a miss; Good/Fail is an over-detection; errors do not match");
            foreach (var (filterName, expectedOrders) in new (string, int[])[]
            {
                ("Mismatch", [2, 3, 5]), ("FalseReject", [3]), ("FalseAccept", [2]), ("Error", [5]), ("HeldOut", [6]), ("All", [1, 2, 3, 4, 5, 6])
            })
            {
                review.SetValidationSetFilterCommand.Execute(filterName);
                Check($"agreement filter {filterName} preserves the canonical set and counts",
                    review.ValidationSetSamples.Select(sample => sample.Order).SequenceEqual(expectedOrders)
                    && reviewSamples.Count == 6 && review.ValidationSetMatchCount == 2 && comparisons == 0,
                    $"visible={string.Join(',', review.ValidationSetSamples.Select(sample => sample.Order))};canonical={reviewSamples.Count}");
            }
            review.SelectedValidationSetSample = reviewSamples[0];
            review.NextValidationSetIssueCommand.Execute(null);
            var firstMismatch = review.SelectedValidationSetSample;
            review.NextValidationSetIssueCommand.Execute(null);
            Check("issue navigation selects a false accept before a false reject without execution",
                firstMismatch?.Order == 2 && review.SelectedValidationSetSample?.Order == 3 && comparisons == 0,
                $"first={firstMismatch?.Order};next={review.SelectedValidationSetSample?.Order}");
            Check("distribution limits come from execution evidence with full numeric precision",
                review.ValidationDistributionOutputs.Single() is { Minimum: 7.75, Maximum: 8.25, Points.Count: 5 } output && output.Points[1].Value == 8.1
                && output.Points.All(point => point.Sample.HasExpectation),
                review.ValidationDistributionOutputs.Single().RangeText);
            review.OpenValidationSetComparisonCommand.Execute(reviewSamples[1]);
            Check("card comparison selects its actual sample before the explicit comparison request", review.SelectedValidationSetSample?.Order == 2 && comparisons == 1,
                $"selected={review.SelectedValidationSetSample?.Order};requests={comparisons}");
            reviewSamples.Add(ReviewRow(7, ToolRecipeValidationSampleRole.Good, "Pending", 0));
            reviewSamples.Add(ReviewRow(8, ToolRecipeValidationSampleRole.Good, "Warning", 0));
            reviewSamples.Add(ReviewRow(9, ToolRecipeValidationSampleRole.HeldOut, "Error", 0));
            reviewSamples.Add(ReviewRow(10, ToolRecipeValidationSampleRole.HeldOut, "Pending", 0));
            review.RefreshSamples();
            Check("pending, warning and held-out errors retain honest summary denominators",
                review.ValidationSetAdjustmentCount == 7 && review.ValidationSetMatchCount == 2
                && review.ValidationSetAdjustmentPendingCount == 2 && review.ValidationSetAdjustmentErrorCount == 1
                && review.ValidationSetHeldOutCompletedCount == 2 && review.ValidationSetHeldOutPendingCount == 1
                && review.ValidationSetMismatchCount == 3,
                "Held-out Error counts as a completed execution and an error, never as an adjustment Match");
            review.SetValidationSetFilterCommand.Execute("Error");
            Check("error filter includes held-out execution errors", review.ValidationSetSamples.Select(sample => sample.Order).SequenceEqual([5, 9]), "Both actual error rows remain reachable");
            reviewSamples.Clear();
            review.RefreshSamples();
            Check("empty summary resets counts without inventing successful comparisons",
                review.ValidationSetAdjustmentCount == 0 && review.ValidationSetMatchCount == 0
                && review.ValidationSetMismatchCount == 0 && review.ValidationSetHeldOutCompletedCount == 0,
                "Cleared canonical set yields zero counts");
            Check("clearing samples removes mismatch cards, distribution values and selection", !review.HasValidationMismatches && !review.HasValidationResults
                && review.ValidationDistributionOutputs.Count == 0 && review.SelectedValidationDistributionOutput is null,
                $"outputs={review.ValidationDistributionOutputs.Count};selected={review.SelectedValidationDistributionOutput?.Label ?? "none"}");

            var finalCommandStates = new System.Collections.Concurrent.ConcurrentQueue<bool>();
            var idlePublished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            ToolWorkbenchValidationSetExecutionOwner? commandOwner = null;
            commandOwner = new ToolWorkbenchValidationSetExecutionOwner(() =>
            {
                finalCommandStates.Enqueue(commandOwner!.CanStart);
                if (commandOwner.CanStart) idlePublished.TrySetResult();
            });
            var commandCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            commandOwner.TryStartCommand(() => commandCompletion.Task, _ => { });
            commandCompletion.SetResult();
            await idlePublished.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Check("command completion publishes restored availability after releasing the duplicate-execution slot", commandOwner.CanStart && finalCommandStates.LastOrDefault(),
                $"canStart={commandOwner.CanStart};notifications={string.Join(",", finalCommandStates)}");
            commandOwner.Dispose();
            var actualJudgedMetrics = workspace.AllSamples.SelectMany(sample => sample.Steps.SelectMany(step => step.Metrics
                .Where(metric => !string.IsNullOrWhiteSpace(metric.Status)).Select(metric => (step.StepId, metric.Name, metric.Unit)))).Distinct().ToArray();
            Check("validation matrix preserves judged metric identities and units without changing execution or selection",
                matrixColumns.Count > 0 && matrixColumns.Select(column => (column.StepId, column.MetricName, column.Unit)).ToHashSet().SetEquals(actualJudgedMetrics)
                && workspace.SelectedValidationSetSample?.Status == "Fail" && !workspace.IsValidationSetRunning && document.Source == sourceBefore,
                $"columns={matrixColumns.Count};metrics={actualJudgedMetrics.Length};selected={workspace.SelectedValidationSetSample?.FileName}");

            var cellConverter = new OpenVisionLab.ThreeD.Shell.Views.Workbench.ValidationMatrixValueConverter();
            var cellSample = new ValidationSetSampleRow(1, passPath, ToolRecipeValidationSampleRole.Good, "Pass", "Pass", string.Empty, string.Empty,
                [new ValidationSetStepRow(1, "step-a", "A", "Pass", "Pass", string.Empty, [new ValidationSetMetricRow("Mean", "8", "raw-height", "Pass", "Pass")], []),
                 new ValidationSetStepRow(2, "step-b", "B", "Pass", "Pass", string.Empty, [new ValidationSetMetricRow("Mean", "12", "mm", "Pass", "Pass")], [])]);
            var culture = System.Globalization.CultureInfo.InvariantCulture;
            Check("validation matrix cell rejects another step or unit and keeps missing outputs empty",
                cellConverter.Convert(cellSample, typeof(string), new ValidationMatrixColumn("step-a", "A", "Mean", "raw-height"), culture) is "Pass · 8 raw-height"
                && cellConverter.Convert(cellSample, typeof(string), new ValidationMatrixColumn("step-b", "B", "Mean", "mm"), culture) is "Pass · 12 mm"
                && cellConverter.Convert(cellSample, typeof(string), new ValidationMatrixColumn("step-a", "A", "Mean", "mm"), culture) is "—"
                && cellConverter.Convert(cellSample, typeof(string), new ValidationMatrixColumn("absent", "Absent", "Mean", "raw-height"), culture) is "—",
                "same metric name, different step and unit; missing output remains absent");

            Check("failure correction captures selected evidence", workspace.BeginValidationFailureCorrectionContext()
                && workspace.ActiveValidationFailureCorrectionContext is { } correction
                && correction.SourcePath == failPath
                && correction.StepId == document.Steps[0].Id
                && !string.IsNullOrWhiteSpace(correction.Reason),
                $"context={workspace.ActiveValidationFailureCorrectionContext}");

            workspace.SetValidationSetFilterCommand.Execute("Fail");
            workspace.OpenValidationSetComparisonCommand.Execute(null);
            Check("review filtering preserves all samples and requests explicit comparison", workspace.ValidationSetSamples.Count == 1
                && workspace.AllSamples.Count == 2
                && comparisonSample?.SourcePath == failPath
                && document.Source == sourceBefore,
                $"visible={workspace.ValidationSetSamples.Count};all={workspace.AllSamples.Count};comparison={comparisonSample?.FileName}");

            var logsBeforeLocalization = log.Count;
            english = false;
            workspace.RefreshValidationSetLocalization();
            Check("localization updates review state without execution", workspace.ValidationSetSamples[0].StatusText == "실패"
                && workspace.ValidationSetSummary.Contains("완료", StringComparison.Ordinal)
                && log.Count == logsBeforeLocalization
                && !workspace.IsValidationSetRunning
                && document.Source == sourceBefore,
                $"status={workspace.ValidationSetSamples[0].StatusText};logs={log.Count};summary={workspace.ValidationSetSummary}");

            workspace.ClearValidationSetCommand.Execute(null);
            Check("clearing the validation set releases previous matrix columns", workspace.ValidationMatrixColumns.Count == 0, $"columns={workspace.ValidationMatrixColumns.Count}");
            Check("clear owns review and evidence reset and requests pin cleanup", workspace.AllSamples.Count == 0
                && workspace.ValidationSetSamples.Count == 0
                && workspace.SelectedValidationSetSample is null
                && workspace.SelectedValidationSetStep is null
                && !workspace.HasValidationEvidence
                && !workspace.HasActiveValidationFailureCorrectionContext
                && clearPinRequests == 1,
                $"all={workspace.AllSamples.Count};evidence={workspace.HasValidationEvidence};pinRequests={clearPinRequests}");

            workspace.SetValidationSetSources([passPath]);
            Check("full source replacement clears basis when all visible rows become Pending", workspace.ValidationSetLastRunAt is null
                && workspace.AllSamples.All(row => row.Status == "Pending") && !workspace.IsValidationSetStale, workspace.ValidationSetRunBasisText);
            await workspace.RunValidationSetAsync();
            Check("replacement fixture explicit run establishes its own basis", workspace.ValidationSetLastRunAt is not null, workspace.ValidationSetRunBasisText);
            workspace.LoadValidationSetDefinition(recipePath, document);
            Check("recipe manifest load clears prior basis through existing definition callback", workspace.ValidationSetLastRunAt is null
                && workspace.AllSamples.All(row => row.Status == "Pending") && !workspace.IsValidationSetStale, workspace.ValidationSetRunBasisText);

            Check("feature notifications retain owning sender and count/selection names", notificationSendersMatch
                && notificationNames.Contains(nameof(ValidationSetWorkspaceViewModel.ValidationSetAllCount))
                && notificationNames.Contains(nameof(ValidationSetWorkspaceViewModel.SelectedValidationSetSample))
                && notificationNames.Contains(nameof(ValidationSetWorkspaceViewModel.IsValidationSetRunning)),
                $"ownerSender={notificationSendersMatch};notifications={notificationNames.Count}");

            workspace.Dispose();
            var notificationsBeforeDisposedRun = notificationNames.Count;
            await workspace.RunValidationSetAsync();
            workspace.Dispose();
            Check("dispose is idempotent and closes command and notification lifetime", workspace.IsDisposed
                && !workspace.RunValidationSetCommand.CanExecute(null)
                && !workspace.SelectValidationSetSourcesCommand.CanExecute(null)
                && !workspace.CancelValidationSetCommand.CanExecute(null)
                && notificationNames.Count == notificationsBeforeDisposedRun,
                $"disposed={workspace.IsDisposed};notifications={notificationNames.Count}");
        }
        catch (Exception exception)
        {
            Check("unexpected exception", false, exception.ToString());
        }

            var groupedRows = await VerifyGroupResultsAsync(fullReportPath, Check);
            var presentationThread = new Thread(() =>
        {
                try { VerifyResultsPresentation(fullReportPath, Check, groupedRows); VerifyExecutionBarPresentation(fullReportPath, Check); }
            catch (Exception exception) { Check("results mounted presentation exception", false, exception.ToString()); }
        });
        presentationThread.SetApartmentState(ApartmentState.STA);
        presentationThread.Start();
        presentationThread.Join();
        var success = total > 0 && passed == total;
        var summary = $"ValidationSetWorkspace|pass={success}|checks={passed}/{total}|report={fullReportPath}";
        lines.Insert(1, summary);
        File.WriteAllLines(fullReportPath, lines);
        return (success, summary);
    }

    private static async Task<ValidationSetSampleRow[]> VerifyGroupResultsAsync(string reportPath, Action<string, bool, string> check)
    {
        var root = Path.GetDirectoryName(reportPath)!;
        var fixtureRoot = Path.Combine(root, "native-group-fixture");
        var path = Path.Combine(fixtureRoot, "grouped.ov3d-recipe.json");
        using var workbench = new ToolWorkbenchViewModel(Path.Combine(root, "group-results-recent.json"));
        var opened = workbench.TryOpenTeachingRecipe(path, out var message);
        check("grouped fixture reopens idle through canonical owner", opened && workbench.ValidationSetAllCount == 3 && !workbench.IsValidationSetRunning,
            $"path={path};{message};fixture prepared by focused thickness authoring suite");
        if (!opened) return [];
        await workbench.RunValidationSetAsync();
        var rows = workbench.ValidationSetSamples.ToArray();
        var flat = workbench.ValidationMatrixColumns.ToArray();
        var table = workbench.ValidationTableColumns.ToArray();
        var converter = new OpenVisionLab.ThreeD.Shell.Views.Workbench.ValidationMatrixValueConverter();
        var cells = (ValidationGroupMemberCell[])converter.Convert(rows[1], typeof(object), table.Single(), System.Globalization.CultureInfo.InvariantCulture);
        check("real explicit group Run retains flat distributions and emits one active-member table column", table.Length == 1 && table[0].Members?.Count == 8
            && flat.Length > 8 && workbench.ValidationDistributionOutputs.Count == flat.Length && cells.Length == 8 && cells[0].Status == "Fail",
            $"table={table.Length};flat={flat.Length};members={cells.Length};statuses={string.Join(',', cells.Select(cell => cell.Status))};actual C3D execution");
        var readoutNotifications = 0;
        workbench.PropertyChanged += (_, args) => { if (args.PropertyName == nameof(ToolWorkbenchViewModel.SelectedValidationDistributionReadout)) readoutNotifications++; };
        var firstMean = workbench.ValidationDistributionOutputs.First(output => output.Column.StepId == rows[0].RecipeSteps![0].StepId && output.Column.MetricName == "Mean");
        var secondMean = workbench.ValidationDistributionOutputs.First(output => output.Column.StepId == rows[0].RecipeSteps![1].StepId && output.Column.MetricName == "Mean");
        workbench.SelectedValidationDistributionOutput = firstMean;
        workbench.SelectedValidationSetSample = rows[0];
        check("selected real coupon readout exposes executed value, unit, judgement and own limits without tooltip", workbench.SelectedValidationDistributionReadout.Contains("8 raw-height")
            && workbench.SelectedValidationDistributionReadout.Contains("✓ OK") && workbench.SelectedValidationDistributionReadout.Contains("7.75 – 8.25"), workbench.SelectedValidationDistributionReadout.Replace('\n', '|'));
        workbench.SelectedValidationSetSample = rows[1];
        check("selected real NG readout changes to its own failed value", workbench.SelectedValidationDistributionReadout.Contains("13 raw-height")
            && workbench.SelectedValidationDistributionReadout.Contains("× NG") && workbench.SelectedValidationDistributionReadout.Contains("7.75 – 8.25"), workbench.SelectedValidationDistributionReadout.Replace('\n', '|'));
        workbench.SelectedValidationSetSample = rows[2];
        check("selected real Error readout preserves NaN and lacks recorded limits", workbench.SelectedValidationDistributionReadout.Contains("NaN raw-height")
            && !workbench.SelectedValidationDistributionReadout.Contains("7.75")
            && (workbench.SelectedValidationDistributionReadout.Contains("기록된 허용 범위 없음") || workbench.SelectedValidationDistributionReadout.Contains("No recorded limits")), workbench.SelectedValidationDistributionReadout.Replace('\n', '|'));
        workbench.SelectedValidationDistributionOutput = secondMean;
        check("missing next step readout does not borrow another sample value or range", !workbench.SelectedValidationDistributionReadout.Contains("12.00")
            && !workbench.SelectedValidationDistributionReadout.Contains("11.75"), workbench.SelectedValidationDistributionReadout.Replace('\n', '|'));
        workbench.SelectedValidationSetSample = rows[0];
        var executedReadout = workbench.SelectedValidationDistributionReadout;
        check("valid sample and next output recovery restores its own execution evidence", executedReadout.Contains("12 raw-height") && executedReadout.Contains("11.75 – 12.25")
            && executedReadout.Contains("✓ OK") && readoutNotifications >= 4 && !workbench.IsValidationSetRunning, $"notifications={readoutNotifications};{executedReadout.Replace('\n', '|')}");
        var first = workbench.PipelineSteps[0];
        first.ToolName = "Live name after completed run";
        first.OutputEnabled = false;
        check("live recipe edits cannot alter completed member snapshot or flat evidence", workbench.ValidationTableColumns[0].Header == table[0].Header
            && workbench.ValidationTableColumns[0].Members?.Count == 8 && rows[0].RecipeSteps![0].ToolName != first.ToolName
            && workbench.ValidationMatrixColumns.SequenceEqual(flat) && workbench.SelectedValidationDistributionReadout == executedReadout, "immutable Run-start metadata/readout, not current recipe lookup");
        using var legacy = new ToolWorkbenchViewModel(Path.Combine(root, "legacy-group-results-recent.json"));
        legacy.TryOpenTeachingRecipe(Path.Combine(fixtureRoot, "legacy.ov3d-recipe.json"), out _);
        await legacy.RunValidationSetAsync();
        check("same legacy inputs produce the same real verdicts and flat columns", legacy.ValidationSetSamples.Select(row => row.Status).SequenceEqual(rows.Select(row => row.Status))
            && legacy.ValidationMatrixColumns.SequenceEqual(flat) && legacy.ValidationTableColumns.Count == flat.Length,
            "schema/group metadata changes presentation only");
        using var disabled = new ToolWorkbenchViewModel(Path.Combine(root, "disabled-group-results-recent.json"));
        disabled.TryOpenTeachingRecipe(Path.Combine(fixtureRoot, "disabled-member.ov3d-recipe.json"), out _);
        disabled.SetValidationSetSources([Path.Combine(fixtureRoot, "coupon.C3D")]);
        await disabled.RunValidationSetAsync();
        check("disabled member real Run exposes seven active bars without executing its output", disabled.ValidationTableColumns[0].Members?.Count == 7
            && disabled.ValidationSetSamples[0].Steps.Last().Metrics.Count == 0 && disabled.ValidationSetSamples[0].Steps.Last().Status == "Warning",
            "saved enabled policy; actual existing disabled-step result");
        var errorPath = Path.Combine(root, "group-all-error.C3D");
        File.WriteAllBytes(errorPath, [1, 2, 3]);
        workbench.SetValidationSetSources([errorPath]);
        await workbench.RunValidationSetAsync();
        check("all-error execution preserves snapshot group while missing member values stay unavailable", workbench.ValidationSetSamples[0].Status == "Error"
            && workbench.ValidationTableColumns.Single().Members?.Count == 7 && workbench.ValidationMatrixColumns.Count == 0
            && ((ValidationGroupMemberCell[])converter.Convert(workbench.ValidationSetSamples[0], typeof(object), workbench.ValidationTableColumns[0], System.Globalization.CultureInfo.InvariantCulture)).All(cell => cell.Status == "None"),
            "actual malformed C3D; no fabricated member error/value or live fallback");
        return rows;
    }

    private static void VerifyResultsPresentation(string reportPath, Action<string, bool, string> check, ValidationSetSampleRow[] groupedRows)
    {
        var app = System.Windows.Application.Current ?? new System.Windows.Application { ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add(new global::Wpf.Ui.Markup.ThemesDictionary { Theme = global::Wpf.Ui.Appearance.ApplicationTheme.Dark });
        app.Resources.MergedDictionaries.Add(new global::Wpf.Ui.Markup.ControlsDictionary());
        app.Resources.MergedDictionaries.Add(new System.Windows.ResourceDictionary { Source = new Uri("pack://application:,,,/OpenVisionLab.ThreeD.Shell;component/Themes/OpenVisionThreeDTheme.xaml") });
        var languageBefore = global::OpenVisionLab.OpenVisionLanguageService.CurrentLanguage;
        try
        {
            foreach (var language in new[] { global::OpenVisionLab.OpenVisionLanguage.Korean, global::OpenVisionLab.OpenVisionLanguage.English })
            {
                global::OpenVisionLab.OpenVisionLanguageService.SetLanguage(language, save: false);
                var presentationRoot = Path.GetDirectoryName(reportPath)!;
                using var shell = new ShellMainWindowViewModel(comparisonContractPath: Path.Combine(presentationRoot, "results-comparison.json"), comparisonReportPath: Path.Combine(presentationRoot, "results-comparison-report.json"),
                    shellScreenshotPath: Path.Combine(presentationRoot, "results-shell.png"), runRecordPath: Path.Combine(presentationRoot, "results-run.json"), htmlReportPath: Path.Combine(presentationRoot, "results-run.html"), csvReportPath: Path.Combine(presentationRoot, "results-run.csv"),
                    recentRunRecordsPath: Path.Combine(presentationRoot, "results-history-" + language + ".json"), recentRecipesPath: Path.Combine(presentationRoot, "results-recipes-" + language + ".json"), orderedRunRecordRoot: Path.Combine(presentationRoot, "results-records"), integrationSettingsPath: Path.Combine(presentationRoot, "results-settings.json"));
                var workbench = shell.Workbench;
                var paths = new[] { "pass.C3D", "fail.C3D" }.Select(name => Path.Combine(Path.GetDirectoryName(reportPath)!, "validation-workspace-fixture", name)).ToArray();
                workbench.SetValidationSetSources(paths);
                // Seed only this isolated canonical presentation owner; these rendered statuses are not an execution claim.
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                var workspaceOwner = (ValidationSetWorkspaceViewModel)typeof(ToolWorkbenchViewModel).GetField("validationSetWorkspace", flags)!.GetValue(workbench)!;
                var definitionOwner = (ToolWorkbenchValidationSetDefinitionOwner)typeof(ValidationSetWorkspaceViewModel).GetField("validationSetDefinitionOwner", flags)!.GetValue(workspaceOwner)!;
                var reviewOwner = (ToolWorkbenchValidationSetReviewOwner)typeof(ValidationSetWorkspaceViewModel).GetField("validationSetReviewOwner", flags)!.GetValue(workspaceOwner)!;
                var longPath = Path.Combine(Path.GetDirectoryName(paths[0])!, "검사 위치와 긴 입력 식별자를 확인해야 하는 validation sample long identity.C3D");
                File.Copy(paths[0], longPath, overwrite: true);
                ValidationSetSampleRow Row(int order, ToolRecipeValidationSampleRole role, string status, bool missing = false) => new(order, order == 3 ? longPath : paths[(order - 1) % 2], role,
                    status, status, status == "Error" ? "Actual isolated fixture read error; selected input is unavailable" : "Observed metric evidence for this isolated presentation fixture", "", status == "Error" ? [] :
                    [new ValidationSetStepRow(1, "step-a", "Long measurement tool caption with stable step identity", status, status, "Measured evidence", missing ? [] : [new ValidationSetMetricRow("Mean thickness", order == 3 ? "NaN" : order.ToString(), "mm", status, status)], [])]);
                definitionOwner.ReplaceExecutionResult([Row(1, ToolRecipeValidationSampleRole.Good, "Pass"), Row(2, ToolRecipeValidationSampleRole.Bad, "Pass"),
                    Row(3, ToolRecipeValidationSampleRole.Good, "Fail"), Row(4, ToolRecipeValidationSampleRole.Bad, "Fail", missing: true),
                    Row(5, ToolRecipeValidationSampleRole.HeldOut, "Pass"), Row(6, ToolRecipeValidationSampleRole.Good, "Error")]);
                typeof(ValidationSetWorkspaceViewModel).GetMethod("NotifyValidationSetCountsChanged", flags)!.Invoke(workspaceOwner, null);
                reviewOwner.RefreshSamples();
                var view = new OpenVisionLab.ThreeD.Shell.Views.Workbench.RecipePipelineReviewView { DataContext = workbench, RunRecordContext = shell, Foreground = (System.Windows.Media.Brush)app.FindResource("ThreeD.PrimaryTextBrush") };
                view.Measure(new System.Windows.Size(956, 330));
                view.Arrange(new System.Windows.Rect(0, 0, 956, 330));
                view.UpdateLayout();
                System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Background);
                view.UpdateLayout();
                var grid = (System.Windows.Controls.DataGrid)view.FindName("ValidationSamplesGrid");
                var standardColumns = grid.Columns.ToArray();
                var standardRowStyle = grid.RowStyle;
                var standardHeaderStyle = grid.ColumnHeaderStyle;
                var standardCellStyle = grid.CellStyle;
                var navigation = (System.Windows.FrameworkElement)view.FindName("ValidationWorkspaceNavigation");
                var standardNavigationHeight = navigation.Height;
                var tab = (System.Windows.Controls.RadioButton)view.FindName("ValidationResultsNavigation");
                var standardTabTemplate = tab.Template;
                var runningBefore = workbench.IsValidationSetRunning;
                var logBefore = workbench.RunLog.Count;
                var dirtyBefore = workbench.IsDirty;
                view.IsValidationSamplesExternal = true;
                view.SetPresentationMode(OpenVisionLab.ThreeD.Shell.Views.Workbench.RecipeReviewPresentationMode.Validation);
                view.ActivateValidationSet();
                var reviewTabs = (System.Windows.Controls.TabControl)view.FindName("ReviewTabs");
                check($"{language} formal validation activation selects the mounted validation content", view.IsValidationSetSelected
                    && ReferenceEquals(reviewTabs.SelectedContent, view.FindName("ValidationWorkspaceContent")),
                    $"selectedIndex={reviewTabs.SelectedIndex};initial DataBind settled before formal activation");
                var grip = (AvalonDock.Controls.AnchorablePaneTitle)view.FindName("ValidationHeaderGrip");
                var pin = (System.Windows.Controls.Button)view.FindName("ValidationMatrixPin");
                check($"{language} null docking owner disables validation grip and pin", grip.Model is null && !grip.IsEnabled && !pin.IsEnabled,
                    "mounted binding support only; no native drag or pointer proof");
                var dockModel = new AvalonDock.Layout.LayoutAnchorable { ContentId = "validation-results-presentation" };
                var dockPane = new AvalonDock.Layout.LayoutAnchorablePane();
                dockPane.Children.Add(dockModel);
                var dockPanel = new AvalonDock.Layout.LayoutPanel();
                dockPanel.Children.Add(dockPane);
                var dockManager = new AvalonDock.DockingManager { Layout = new AvalonDock.Layout.LayoutRoot { RootPanel = dockPanel } };
                view.DockPaneCommands = dockManager.GetLayoutItemFromModel(dockModel);
                foreach (var width in new[] { 956, 1600 })
                {
                    view.Width = width;
                    view.Height = 330;
                    view.Measure(new System.Windows.Size(width, 330));
                    view.Arrange(new System.Windows.Rect(0, 0, width, 330));
                    view.UpdateLayout();
                    System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Background);
                    view.UpdateLayout();
                    var header = (System.Windows.FrameworkElement)view.FindName("ValidationWorkspaceNavigation");
                    var actions = (System.Windows.FrameworkElement)view.FindName("ValidationMatrixHeaderActions");
                    var summaryBar = (System.Windows.FrameworkElement)view.FindName("ValidationMatrixSummary");
                    var summaryBounds = summaryBar.TransformToAncestor(header).TransformBounds(new System.Windows.Rect(summaryBar.RenderSize));
                    var actionBounds = actions.TransformToAncestor(header).TransformBounds(new System.Windows.Rect(actions.RenderSize));
                    check($"{language} {width}px validation-only matrix has compact header and separated columns", view.IsValidationSetSelected && view.IsDedicatedValidationWorkspace && header.ActualHeight == 34
                        && grid.Columns.Count == 5 + workbench.ValidationMatrixColumns.Count && grid.Columns[0] is System.Windows.Controls.DataGridTextColumn file
                        && file.Binding is System.Windows.Data.Binding fileBinding && fileBinding.Path.Path == nameof(ValidationSetSampleRow.FileName)
                        && grid.Columns[1] is System.Windows.Controls.DataGridTemplateColumn && grid.Columns[2] is System.Windows.Controls.DataGridTemplateColumn
                        && grid.Columns[3] is System.Windows.Controls.DataGridTemplateColumn && actions.Visibility == System.Windows.Visibility.Visible,
                        $"header={header.ActualWidth},{header.ActualHeight};columns={grid.Columns.Count};rows={grid.Items.Count};windows={app.Windows.Count};normal mounted only, no native input proof");
                    check($"{language} {width}px header summary and actions stay in the same row", summaryBounds.Top >= 0 && summaryBounds.Bottom <= header.ActualHeight + 0.1
                        && summaryBounds.Right <= actionBounds.Left && actionBounds.Right <= header.ActualWidth + 0.1,
                        $"summary={summaryBounds};actions={actionBounds};header={header.RenderSize}");
                    grid.SelectedItem = workbench.ValidationSetSamples[1];
                    check($"{language} {width}px matrix selection uses canonical owner without running", ReferenceEquals(workbench.SelectedValidationSetSample, grid.SelectedItem)
                        && workbench.IsValidationSetRunning == runningBefore && workbench.RunLog.Count == logBefore && workbench.IsDirty == dirtyBefore,
                        $"selected={workbench.SelectedValidationSetSample?.FileName};running={workbench.IsValidationSetRunning};logs={workbench.RunLog.Count}");
                    var numericLabels = VisualChildren<System.Windows.Controls.TextBlock>(grid).Where(text => text.FontFamily.Source.Contains("Cascadia", StringComparison.Ordinal)).ToArray();
                    var columnHeaders = VisualChildren<System.Windows.Controls.Primitives.DataGridColumnHeader>(grid).Where(item => item.Column is not null).ToArray();
                    var scroll = VisualChildren<System.Windows.Controls.ScrollViewer>(grid).First();
                    check($"{language} {width}px populated matrix keeps actual metric values, identities and headers accessible", numericLabels.Any(text => text.Text == "1 mm")
                        && numericLabels.Any(text => text.Text == "NaN mm") && numericLabels.Any(text => text.Text == "—")
                        && columnHeaders.All(item => item.ActualWidth + 0.1 >= item.DesiredSize.Width) && workbench.ValidationSetMatchCount == 2
                        && workbench.ValidationSetFalseAcceptCount == 1 && workbench.ValidationSetFalseRejectCount == 1
                        && ReferenceEquals(grip.Model, dockModel) && grip.IsEnabled && grip.ActualWidth == 10 && grip.ActualHeight == 16,
                        $"metricLabels={string.Join(',', numericLabels.Select(text => text.Text))};headerWidths={string.Join(',', columnHeaders.Select(item => item.ActualWidth))};extent={scroll.ExtentWidth};viewport={scroll.ViewportWidth};seeded canonical fixture only");
                    if (scroll.ScrollableWidth > 0)
                    {
                        scroll.ScrollToHorizontalOffset(scroll.ScrollableWidth);
                        view.UpdateLayout();
                        check($"{language} {width}px horizontal scrolling reaches cause without running", scroll.HorizontalOffset > 0 && workbench.RunLog.Count == logBefore,
                            $"offset={scroll.HorizontalOffset};source identity and metrics remain unchanged; no native wheel proof");
                        var causeText = VisualChildren<System.Windows.Controls.TextBlock>(grid).Where(text => text.Text.StartsWith("Actual isolated fixture read error", StringComparison.Ordinal)).FirstOrDefault();
                        check($"{language} {width}px cause is not replaced by tooltip-only access", causeText is not null && causeText.TextTrimming == System.Windows.TextTrimming.None
                            && causeText.ActualWidth + 0.1 >= causeText.DesiredSize.Width - causeText.Margin.Left - causeText.Margin.Right,
                            $"causeWidth={causeText?.ActualWidth};desired={causeText?.DesiredSize.Width};horizontal scroll access");
                        var causeCapture = new System.Windows.Media.Imaging.RenderTargetBitmap(width, 330, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                        causeCapture.Render(view);
                        var causeEncoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                        causeEncoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(causeCapture));
                        using (var causeOutput = File.Create(Path.Combine(Path.GetDirectoryName(reportPath)!, $"validation-results-{language}-{width}-cause.png"))) causeEncoder.Save(causeOutput);
                        scroll.ScrollToHorizontalOffset(0);
                        view.UpdateLayout();
                    }
                    var capture = new System.Windows.Media.Imaging.RenderTargetBitmap(width, 330, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                    capture.Render(view);
                    var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                    encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(capture));
                    using var output = File.Create(Path.Combine(Path.GetDirectoryName(reportPath)!, $"validation-results-{language}-{width}.png"));
                    encoder.Save(output);
                }
                var valueConverter = new OpenVisionLab.ThreeD.Shell.Views.Workbench.ValidationMatrixValueConverter { IncludeStatus = false };
                var measured = new ValidationSetSampleRow(1, paths[0], ToolRecipeValidationSampleRole.Bad, "Pass", "Pass", "actual evidence", "", [new ValidationSetStepRow(1, "step-a", "Very long actual tool caption", "Pass", "Pass", "actual evidence", [new ValidationSetMetricRow("Mean", "NaN", "mm", "Error", "Error")], [])]);
                check($"{language} matrix value-only mode preserves existing missing/nonfinite formatting", valueConverter.Convert(measured, typeof(string), new ValidationMatrixColumn("step-a", "A", "Mean", "mm"), System.Globalization.CultureInfo.InvariantCulture) is "NaN mm"
                    && valueConverter.Convert(measured, typeof(string), new ValidationMatrixColumn("step-a", "A", "Mean", "raw-height"), System.Globalization.CultureInfo.InvariantCulture) is "—"
                    && measured.ExpectedVerdictText == "NG" && measured.Agreement == "FalseAccept" && measured.Status == "Pass",
                    "Bad/Pass is NG expected, OK actual, FalseAccept comparison; no reinterpretation of metric value/status");
                if (groupedRows.Length > 0)
                {
                    var original = groupedRows[0];
                    var snapshots = original.RecipeSteps!;
                    var sharedName = snapshots[0].Group!.Name;
                    definitionOwner.ReplaceExecutionResult([original with { RecipeSteps = [
                        snapshots[0] with { OutputEnabled = false }, snapshots[1] with { Group = null }, snapshots[2]
                    ] }]);
                    reviewOwner.RefreshSamples();
                    check($"{language} disabled first member cannot move a group ahead of the first enabled output", workbench.ValidationTableColumns[0].Metric?.StepId == snapshots[1].StepId
                        && workbench.ValidationTableColumns.Last().Members?.Count == 1, "table ordering follows enabled snapshot; flat distribution unchanged");
                    definitionOwner.ReplaceExecutionResult([original with { RecipeSteps = [
                        snapshots[0], snapshots[1] with { Group = new ToolRecipeStepGroup("different-group", sharedName) }
                    ] }]);
                    reviewOwner.RefreshSamples();
                    check($"{language} same saved group name does not merge distinct identities", workbench.ValidationTableColumns.Count == 2
                        && workbench.ValidationTableColumns.All(column => column.Members?.Count == 1), "identity is stored ID, never displayed name");
                    var firstStep = original.Steps[0];
                    var judged = firstStep.Metrics.First(metric => !string.IsNullOrWhiteSpace(metric.Status));
                    var firstMetricSample = original with { Steps = [firstStep with { Status = "Fail", Metrics = [judged with { Status = "Pass", StatusText = "canonical first Pass" }] }] };
                    var firstMetricColumn = new ValidationTableColumn("group", null, [snapshots[0]]);
                    var firstCells = (ValidationGroupMemberCell[])valueConverter.Convert(firstMetricSample, typeof(object), firstMetricColumn, System.Globalization.CultureInfo.InvariantCulture);
                    var errorCells = (ValidationGroupMemberCell[])valueConverter.Convert(firstMetricSample with { Steps = [firstMetricSample.Steps[0] with { Status = "Error", StatusText = "execution error" }] }, typeof(object), firstMetricColumn, System.Globalization.CultureInfo.InvariantCulture);
                    var missingCells = (ValidationGroupMemberCell[])valueConverter.Convert(original with { Steps = [] }, typeof(object), firstMetricColumn, System.Globalization.CultureInfo.InvariantCulture);
                    check($"{language} group status and detail use canonical first judgement, error override and explicit missing", firstCells[0].Status == "Pass"
                        && errorCells[0].Status == "Error" && errorCells[0].Detail.Contains("execution error") && !errorCells[0].Detail.Contains("canonical first Pass")
                        && missingCells[0].Status == "None" && missingCells[0].Detail.Contains("—"), "isolated presentation boundary; no algorithm or native pointer proof");
                    definitionOwner.ReplaceExecutionResult(groupedRows);
                    typeof(ValidationSetWorkspaceViewModel).GetMethod("NotifyValidationSetCountsChanged", flags)!.Invoke(workspaceOwner, null);
                    reviewOwner.RefreshSamples();
                    foreach (var width in new[] { 956, 1600 })
                    {
                        view.Width = width;
                        view.Measure(new System.Windows.Size(width, 330));
                        view.Arrange(new System.Windows.Rect(0, 0, width, 330));
                        System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Background);
                        view.UpdateLayout();
                        var bars = VisualChildren<System.Windows.Controls.Border>(grid).Where(border => System.Windows.Automation.AutomationProperties.GetAutomationId(border) == "ValidationGroupMember").ToArray();
                        check($"{language} {width}px real-result group bars retain measured bounds and accessible evidence", grid.Columns.Count == 6 && bars.Length == 24
                            && bars.All(bar => bar.ActualWidth == 9 && bar.ActualHeight == 14 && bar.CornerRadius.TopLeft == 2
                                && !string.IsNullOrWhiteSpace(System.Windows.Automation.AutomationProperties.GetName(bar)))
                            && workbench.ValidationTableColumns[0].Header == "Pad 두께 ×8" && workbench.ValidationMatrixColumns.Count > 8
                            && bars.Length > 1 && Math.Abs(bars[1].TranslatePoint(new System.Windows.Point(0, 0), grid).X - bars[0].TranslatePoint(new System.Windows.Point(0, 0), grid).X - 11) < 0.01,
                            $"bars={bars.Length};header={workbench.ValidationTableColumns[0].Header};flat={workbench.ValidationMatrixColumns.Count};actual C3D result rows;no native pointer proof");
                        var render = new System.Windows.Media.Imaging.RenderTargetBitmap(width, 330, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                        render.Render(view);
                        var png = new System.Windows.Media.Imaging.PngBitmapEncoder();
                        png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(render));
                        using var stream = File.Create(Path.Combine(Path.GetDirectoryName(reportPath)!, $"validation-group-{language}-{width}.png"));
                        png.Save(stream);
                    }
                    view.SetValidationSection(ValidationWorkspaceSection.Thresholds);
                    var readout = (System.Windows.Controls.TextBlock)view.FindName("ValidationDistributionReadout");
                    var firstOutput = workbench.ValidationDistributionOutputs.First(output => output.Column.StepId == groupedRows[0].RecipeSteps![0].StepId && output.Column.MetricName == "Mean");
                    var secondOutput = workbench.ValidationDistributionOutputs.First(output => output.Column.StepId == groupedRows[0].RecipeSteps![1].StepId && output.Column.MetricName == "Mean");
                    foreach (var width in new[] { 956, 1600 })
                    {
                        view.Width = width;
                        foreach (var (name, sample, output, expected) in new[]
                        {
                            ("coupon", groupedRows[0], firstOutput, "8 raw-height"), ("ng", groupedRows[1], firstOutput, "13 raw-height"),
                            ("error", groupedRows[2], firstOutput, "NaN raw-height"), ("missing", groupedRows[2], secondOutput, language == global::OpenVisionLab.OpenVisionLanguage.Korean ? "값 없음" : "No value"),
                            ("recovery", groupedRows[0], secondOutput, "12 raw-height")
                        })
                        {
                            workbench.SelectedValidationDistributionOutput = output;
                            workbench.SelectedValidationSetSample = sample;
                            view.Measure(new System.Windows.Size(width, 330));
                            view.Arrange(new System.Windows.Rect(0, 0, width, 330));
                            System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Background);
                            view.UpdateLayout();
                            var origin = readout.TranslatePoint(new System.Windows.Point(), view);
                            check($"{language} {width}px {name} selected execution readout remains visible and updates its binding", readout.Text == workbench.SelectedValidationDistributionReadout
                                && readout.Text.Contains(expected) && readout.ToolTip is null && readout.TextWrapping == System.Windows.TextWrapping.Wrap
                                && readout.ActualWidth > 0 && origin.Y >= 34 && origin.Y + readout.ActualHeight <= view.ActualHeight,
                                $"bounds={origin}/{readout.ActualWidth}x{readout.ActualHeight};text={readout.Text.Replace('\n', '|')};isolated mounted binding, no pointer proof");
                            if (name is "ng" or "error" or "missing")
                            {
                                var render = new System.Windows.Media.Imaging.RenderTargetBitmap(width, 330, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                                render.Render(view);
                                var png = new System.Windows.Media.Imaging.PngBitmapEncoder();
                                png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(render));
                                using var stream = File.Create(Path.Combine(Path.GetDirectoryName(reportPath)!, $"validation-readout-{language}-{width}-{name}.png"));
                                png.Save(stream);
                            }
                        }
                    }
                    view.SetValidationSection(ValidationWorkspaceSection.Results);
                }
                foreach (var (status, role, expectedAgreement) in new[]
                {
                    ("Pending", ToolRecipeValidationSampleRole.Good, "Pending"), ("Error", ToolRecipeValidationSampleRole.Good, "Error"),
                    ("Warning", ToolRecipeValidationSampleRole.Good, "Warning"), ("Fail", ToolRecipeValidationSampleRole.Good, "FalseReject"),
                    ("Fail", ToolRecipeValidationSampleRole.Bad, "Match"), ("Pass", ToolRecipeValidationSampleRole.HeldOut, "Unset")
                })
                {
                    var sample = measured with { Status = status, StatusText = status, Role = role };
                    var pill = (System.Windows.Controls.Border)((System.Windows.DataTemplate)view.Resources["ValidationMatrixStatusTemplate"]).LoadContent();
                    pill.DataContext = sample;
                    pill.Measure(new System.Windows.Size(240, 40));
                    pill.Arrange(new System.Windows.Rect(0, 0, 240, 40));
                    pill.UpdateLayout();
                    var text = (System.Windows.Controls.TextBlock)pill.Child;
                    check($"{language} actual {status}/{role} pill remains separate from comparison", sample.Agreement == expectedAgreement
                        && text.Text == (status == "Fail" ? "× NG" : status == "Pass" ? "✓ OK" : status),
                        $"actual={text.Text};comparison={sample.Agreement};height={pill.ActualHeight}");
                }
                definitionOwner.ReplaceExecutionResult([Row(1, ToolRecipeValidationSampleRole.Good, "Pass")]);
                typeof(ValidationSetWorkspaceViewModel).GetMethod("NotifyValidationSetCountsChanged", flags)!.Invoke(workspaceOwner, null);
                reviewOwner.RefreshSamples();
                System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Background);
                view.UpdateLayout();
                var summaryChildren = ((System.Windows.Controls.StackPanel)view.FindName("ValidationMatrixSummary")).Children.Cast<System.Windows.Controls.Border>().ToArray();
                check($"{language} canonical zero mismatch/error counts hide only their summary badges", summaryChildren[0].Visibility == System.Windows.Visibility.Visible
                    && summaryChildren.Skip(1).All(border => border.Visibility == System.Windows.Visibility.Collapsed),
                    $"visibility={string.Join(',', summaryChildren.Select(border => border.Visibility))};match={workbench.ValidationSetMatchCount};reject={workbench.ValidationSetFalseRejectCount};accept={workbench.ValidationSetFalseAcceptCount};error={workbench.ValidationSetErrorCount};fixture-only refresh");
                view.DockPaneCommands = null;
                check($"{language} docking owner release clears grip model and disables pin", grip.Model is null && !grip.IsEnabled && !pin.IsEnabled,
                    "native owner binding lifetime support; actual drag remains unverified");
                var uiThread = Environment.CurrentManagedThreadId;
                var previewNotices = 0;
                var noticesOnUi = true;
                workspaceOwner.PropertyChanged += (_, args) =>
                {
                    if (args.PropertyName == nameof(workspaceOwner.ValidationMismatchPreviews)) { previewNotices++; noticesOnUi &= Environment.CurrentManagedThreadId == uiThread; }
                };
                definitionOwner.ReplaceExecutionResult([Row(1, ToolRecipeValidationSampleRole.Good, "Pass"), Row(2, ToolRecipeValidationSampleRole.Bad, "Pass"),
                    Row(3, ToolRecipeValidationSampleRole.Good, "Fail"), Row(4, ToolRecipeValidationSampleRole.Bad, "Fail", missing: true),
                    Row(5, ToolRecipeValidationSampleRole.HeldOut, "Pass"), Row(6, ToolRecipeValidationSampleRole.Good, "Error")]);
                workspaceOwner.RefreshValidationSetLocalization();
                var previewDeadline = DateTime.UtcNow.AddSeconds(20);
                while (!workspaceOwner.ValidationMismatchPreviewTask.IsCompleted && DateTime.UtcNow < previewDeadline)
                    System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Background);
                check($"{language} card preview preparation completes without a window", workspaceOwner.ValidationMismatchPreviewTask.IsCompleted,
                    $"cached={workbench.ValidationMismatchPreviews.Count};windows={app.Windows.Count}");
                check($"{language} preview notifications remain on captured UI dispatcher", previewNotices > 0 && noticesOnUi, $"notices={previewNotices};thread={uiThread}");
                view.SetValidationSection(ValidationWorkspaceSection.Failures);
                foreach (var width in new[] { 956, 1600 })
                {
                    view.Width = width;
                    view.Height = 650;
                    view.InvalidateMeasure();
                    view.InvalidateArrange();
                    view.Measure(new System.Windows.Size(width, 650));
                    view.Arrange(new System.Windows.Rect(0, 0, width, 650));
                    view.UpdateLayout();
                    System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Background);
                    check($"{language} mismatch capture has real requested View bounds at {width}", Math.Abs(view.ActualWidth - width) < 0.1 && Math.Abs(view.ActualHeight - 650) < 0.1, $"view={view.ActualWidth}x{view.ActualHeight};requested={width}x650");
                    var cards = (System.Windows.Controls.ListBox)view.FindName("ValidationMismatchCards");
                    check($"{language} mismatch cards retain canonical rows at {width}", cards.Items.Count == 3 && cards.Visibility == System.Windows.Visibility.Visible
                        && cards.ActualWidth > 700 && app.Windows.Count == 0, $"count={cards.Items.Count};width={cards.ActualWidth};Window0");
                    var cardLabels = VisualChildren<System.Windows.Controls.TextBlock>(cards).Select(text => text.Text).ToArray();
                    check($"{language} mismatch cards expose status guidance and recorded facts at {width}", cardLabels.Any(text => text.Contains("Long measurement"))
                        && cardLabels.Any(text => text.Contains(language == global::OpenVisionLab.OpenVisionLanguage.Korean ? "원인 도구" : "cause tool")), string.Join(" | ", cardLabels));
                    var actionButtons = VisualChildren<System.Windows.Controls.Button>(cards).Where(button => button.CommandParameter is ValidationSetSampleRow && button.Content is System.Windows.Controls.StackPanel).ToArray();
                    var actionStates = actionButtons.Select(button => (Button: button, Row: (ValidationSetSampleRow)button.CommandParameter, Template: button.Template, Enabled: button.IsEnabled, Foreground: button.Foreground)).ToArray();
                    foreach (var state in actionStates) state.Button.CommandParameter = state.Row with { SourcePath = Path.Combine(Path.GetDirectoryName(reportPath)!, "unavailable-card38.C3D") };
                    view.UpdateLayout();
                    var disabledBrush = (System.Windows.Media.Brush)view.FindResource("ThreeD.DisabledBrush");
                    check($"{language} {width}px command-disabled card Button and rendered caption/icon share semantic foreground", actionStates.Length == 12 && actionStates.All(state => !state.Button.IsEnabled
                        && state.Button.Foreground.ToString() == disabledBrush.ToString()
                        && VisualChildren<System.Windows.Controls.TextBlock>(state.Button).All(text => text.Foreground.ToString() == disabledBrush.ToString())
                        && VisualChildren<global::Wpf.Ui.Controls.SymbolIcon>(state.Button).All(icon => icon.Foreground.ToString() == disabledBrush.ToString())
                        && ReferenceEquals(state.Button.Template, state.Template)),
                        $"buttons={actionStates.Length};foreground={string.Join(',', actionStates.Select(state => state.Button.Foreground.ToString()))};disabled={disabledBrush};Window0; command guards unchanged");
                    var disabledCapture = new System.Windows.Media.Imaging.RenderTargetBitmap(width, 650, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                    disabledCapture.Render(view);
                    var disabledEncoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                    disabledEncoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(disabledCapture));
                    using (var disabledStream = File.Create(Path.Combine(Path.GetDirectoryName(reportPath)!, $"mismatch38-disabled-{language}-{width}.png"))) disabledEncoder.Save(disabledStream);
                    foreach (var state in actionStates) state.Button.CommandParameter = state.Row;
                    view.UpdateLayout();
                    check($"{language} {width}px canonical card commands restore enabled foreground and default template", actionStates.All(state => state.Button.IsEnabled == state.Enabled
                        && state.Button.Foreground.ToString() == state.Foreground.ToString() && ReferenceEquals(state.Button.Template, state.Template))
                        && workbench.RunLog.Count == logBefore && workbench.IsDirty == dirtyBefore && app.Windows.Count == 0,
                        $"enabled={string.Join(',', actionStates.Select(state => state.Button.IsEnabled))};Window0; no Run/recipe change; RunRecordContext=parent Shell; source/record guards preserved; no actual pointer proof");
                    var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(width, 650, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                    bitmap.Render(view);
                    var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                    encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                    using (var stream = File.Create(Path.Combine(Path.GetDirectoryName(reportPath)!, $"mismatch33-{language}-{width}.png"))) encoder.Save(stream);
                }
                foreach (var disposeDuringLoader in new[] { true, false })
                {
                    var loaderStartedOnUi = false;
                    var publicationStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    var releasePublication = new TaskCompletionSource<System.Windows.Media.Imaging.BitmapSource>(TaskCreationOptions.RunContinuationsAsynchronously);
                    using (var queuedWorkspace = new ValidationSetWorkspaceViewModel(() => CreateDocument(paths[0]), () => "Queued preview", () => null,
                        () => paths[0], () => true, [], () => false, () => null, _ => false, new ToolWorkbenchStepPropertySession(), (_, _) => { },
                        (_, englishText) => englishText, status => status.ToString(), previewLoader: (_, _) => { loaderStartedOnUi = Environment.CurrentManagedThreadId == uiThread && SynchronizationContext.Current is System.Windows.Threading.DispatcherSynchronizationContext; publicationStarted.TrySetResult(); return releasePublication.Task; }))
                    {
                        queuedWorkspace.SetValidationSetSources([paths[0]]);
                        queuedWorkspace.SelectedValidationSetSample = queuedWorkspace.AllSamples[0];
                        queuedWorkspace.SetValidationSampleRoleCommand.Execute("Bad");
                        var previous = SynchronizationContext.Current;
                        SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext(System.Windows.Threading.Dispatcher.CurrentDispatcher));
                        try
                        {
                            var queuedRun = queuedWorkspace.RunValidationSetAsync();
                            var queuedDeadline = DateTime.UtcNow.AddSeconds(15);
                            while (!queuedRun.IsCompleted && DateTime.UtcNow < queuedDeadline)
                                System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Background);
                            if (!queuedRun.IsCompleted) throw new TimeoutException("UI preview fixture explicit Run did not finish.");
                            queuedRun.GetAwaiter().GetResult();
                            publicationStarted.Task.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
                            check($"{language} controlled preview loader starts from actual UI context ({disposeDuringLoader})", loaderStartedOnUi
                                && !queuedWorkspace.ValidationMismatchPreviewTask.IsCompleted, "DispatcherSynchronizationContext; actual in-flight loader");
                            if (disposeDuringLoader)
                            {
                                var late = System.Windows.Media.Imaging.BitmapSource.Create(1, 1, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null, new byte[] { 0, 0, 0, 255 }, 4);
                                late.Freeze();
                                queuedWorkspace.Dispose();
                                releasePublication.SetResult(late);
                                queuedWorkspace.ValidationMismatchPreviewTask.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
                                check($"{language} UI-started loader Dispose finishes without pumping dispatcher", queuedWorkspace.ValidationMismatchPreviewTask.IsCompleted
                                    && queuedWorkspace.ValidationMismatchPreviews.Count == 0, "Loader continuation does not depend on UI dispatcher after disposal");
                                continue;
                            }
                            var posted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                            System.Windows.Threading.DispatcherHookEventHandler postedHandler = (_, args) =>
                            { if (args.Operation.Priority == System.Windows.Threading.DispatcherPriority.Normal) posted.TrySetResult(); };
                            System.Windows.Threading.Dispatcher.CurrentDispatcher.Hooks.OperationPosted += postedHandler;
                            try
                            {
                                var tiny = System.Windows.Media.Imaging.BitmapSource.Create(1, 1, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null, new byte[] { 0, 0, 0, 255 }, 4);
                                tiny.Freeze();
                                releasePublication.SetResult(tiny);
                                posted.Task.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
                                check($"{language} preview reaches queued UI publication boundary", !queuedWorkspace.ValidationMismatchPreviewTask.IsCompleted
                                    && queuedWorkspace.ValidationMismatchPreviews.Count == 0, "actual Dispatcher Post queued; UI callback not pumped yet");
                                var previewEvents = 0;
                                queuedWorkspace.PropertyChanged += (_, args) => { if (args.PropertyName == nameof(queuedWorkspace.ValidationMismatchPreviews)) previewEvents++; };
                                queuedWorkspace.Dispose();
                                queuedWorkspace.ValidationMismatchPreviewTask.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
                                System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Background);
                                check($"{language} Dispose terminates queued publication wait and late callback stays discarded", queuedWorkspace.ValidationMismatchPreviewTask.IsCompleted
                                    && queuedWorkspace.ValidationMismatchPreviews.Count == 0 && previewEvents == 0, "no dispatcher shutdown hang or post-disposal notification");
                            }
                            finally { System.Windows.Threading.Dispatcher.CurrentDispatcher.Hooks.OperationPosted -= postedHandler; }
                        }
                        finally { SynchronizationContext.SetSynchronizationContext(previous); }
                    }
                }
                var mismatchRow = workbench.ValidationMismatchSamples.First(sample => sample.Agreement == "FalseReject");
                var retainedRows = workspaceOwner.AllSamples.ToArray();
                var retainedLogs = workbench.RunLog.Count;
                workbench.ValidationWorkspace.OpenMismatchDistributionCommand.Execute(mismatchRow);
                check($"{language} card distribution selects exact sample and navigates existing section", ReferenceEquals(workbench.SelectedValidationSetSample, mismatchRow)
                    && workbench.ValidationWorkspace.Section == ValidationWorkspaceSection.Thresholds && workbench.SelectedValidationDistributionOutput?.Column.StepId == "step-a",
                    $"section={workbench.ValidationWorkspace.Section};sample={workbench.SelectedValidationSetSample?.FileName}");
                var sectionButton = new System.Windows.Controls.Button { Command = workbench.OpenValidationMismatchSectionCommand, CommandParameter = mismatchRow };
                var distributionButton = new System.Windows.Controls.Button { Command = workbench.ValidationWorkspace.OpenMismatchDistributionCommand, CommandParameter = mismatchRow };
                check($"{language} actual card wrapper Buttons are enabled for recorded target", sectionButton.IsEnabled && distributionButton.IsEnabled, "WPF command subscriptions are active");
                workbench.SetValidationSampleView(true);
                workbench.OpenValidationMismatchSectionCommand.Execute(mismatchRow);
                check($"{language} card section reuses visible main pane kind without changing layout", workbench.MainViewerPane.IsSection
                    && workspaceOwner.AllSamples.SequenceEqual(retainedRows) && workbench.RunLog.Count == retainedLogs && !workbench.IsValidationSetRunning,
                    $"kind={workbench.MainViewerPane.Kind};layout={workbench.EffectiveViewerLayout};input section, not cause ROI");
                workspaceOwner.ClearValidationSet();
                System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Background);
                check($"{language} clear re-queries actual wrapper Button disabled states", !sectionButton.IsEnabled && !distributionButton.IsEnabled, "CanExecuteChanged delivered by existing facade owner");
                definitionOwner.ReplaceExecutionResult(retainedRows);
                workspaceOwner.RefreshValidationSetLocalization();
                sectionButton.CommandParameter = workbench.ValidationMismatchSamples.First(sample => sample.Agreement == "FalseReject");
                distributionButton.CommandParameter = sectionButton.CommandParameter;
                check($"{language} restored card rows re-enable actual wrapper Buttons", sectionButton.IsEnabled && distributionButton.IsEnabled, "No CommandManager polling");
                workbench.SetValidationSampleView(false);
                workbench.SetValidationSampleView(true);
                workbench.OpenValidationMismatchSectionCommand.Execute(sectionButton.CommandParameter);
                // The real display coordinator refreshes this owner after sample selection. No native host is mounted here.
                workbench.RefreshValidationSampleView();
                var paneView = new OpenVisionLab.ThreeD.Shell.Views.Workbench.ViewerWorkspaceView { DataContext = workbench };
                paneView.Measure(new System.Windows.Size(956, 400));
                paneView.Arrange(new System.Windows.Rect(0, 0, 956, 400));
                paneView.UpdateLayout();
                check($"{language} card section is visible main pane content in Single", ((System.Windows.FrameworkElement)paneView.FindName("MainSlot")).Visibility == System.Windows.Visibility.Visible
                    && ((System.Windows.FrameworkElement)paneView.FindName("MainPaneProfile")).Visibility == System.Windows.Visibility.Visible
                    && ((System.Windows.FrameworkElement)paneView.FindName("MainPaneProfile")).ActualWidth > 0
                    && workbench.MainViewerPane.Source?.SourcePath == mismatchRow.SourcePath && workbench.EffectiveViewerLayout == ViewerWorkspaceLayout.Single, $"kind={workbench.MainViewerPane.Kind};main={((System.Windows.FrameworkElement)paneView.FindName("MainSlot")).Visibility};profile={((System.Windows.FrameworkElement)paneView.FindName("MainPaneProfile")).Visibility}/{((System.Windows.FrameworkElement)paneView.FindName("MainPaneProfile")).ActualWidth};path={workbench.MainViewerPane.Source?.SourcePath};expected={mismatchRow.SourcePath};layout={workbench.EffectiveViewerLayout};Window0;current section, not cause ROI");
                paneView.Dispose();
                var commandRecipePath = Path.Combine(Path.GetDirectoryName(reportPath)!, $"card-state-{language}.ov3d-recipe.json");
                ToolRecipeDocumentStore.Save(commandRecipePath, CreateDocument(paths[0]));
                check($"{language} card command-state fixture opens real source", workbench.TryOpenTeachingRecipe(commandRecipePath, out var commandOpenMessage), commandOpenMessage);
                workbench.SetValidationSetSources(paths);
                definitionOwner.ReplaceExecutionResult([Row(1, ToolRecipeValidationSampleRole.Bad, "Pass"), Row(2, ToolRecipeValidationSampleRole.Good, "Fail")]);
                workspaceOwner.RefreshValidationSetLocalization();
                sectionButton.CommandParameter = workbench.ValidationMismatchSamples.First(sample => sample.Agreement == "FalseReject");
                distributionButton.CommandParameter = sectionButton.CommandParameter;
                var roleRequest = workspaceOwner.AllSamples[0].BadRoleChange;
                var roleCanExecute = workbench.SetValidationSampleRoleCommand.CanExecute(roleRequest);
                workbench.SetValidationSampleRoleCommand.Execute(roleRequest);
                var beforeRunRows = string.Join(";", workspaceOwner.AllSamples.Select(row => $"{row.FileName}/{row.Role}/{row.Status}/{row.Agreement}"));
                check($"{language} execution transition starts with actual Buttons enabled", sectionButton.IsEnabled && distributionButton.IsEnabled, $"roleCanExecute={roleCanExecute};rolesAndRows={beforeRunRows}");
                var disabledWhileRunning = false;
                System.ComponentModel.PropertyChangedEventHandler runningHandler = (_, args) =>
                {
                    if (args.PropertyName == nameof(workspaceOwner.IsValidationSetRunning) && workspaceOwner.IsValidationSetRunning)
                        disabledWhileRunning |= !sectionButton.IsEnabled && !distributionButton.IsEnabled;
                };
                workspaceOwner.PropertyChanged += runningHandler;
                var commandContext = SynchronizationContext.Current;
                SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext(System.Windows.Threading.Dispatcher.CurrentDispatcher));
                try
                {
                    var commandRun = workbench.RunValidationSetAsync();
                    var deadline = DateTime.UtcNow.AddSeconds(20);
                    while (!commandRun.IsCompleted && DateTime.UtcNow < deadline)
                        System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Background);
                    check($"{language} actual wrapper Buttons disable during explicit execution", commandRun.IsCompleted && disabledWhileRunning, "Existing execution notification path; no CommandManager polling");
                    if (!commandRun.IsCompleted) throw new TimeoutException("Card command-state explicit run did not finish.");
                    commandRun.GetAwaiter().GetResult();
                    var latest = workbench.ValidationMismatchSamples.FirstOrDefault();
                    sectionButton.CommandParameter = latest;
                    distributionButton.CommandParameter = latest;
                    check($"{language} execution completion and new bound card restore Button guards", latest is not null && sectionButton.IsEnabled && distributionButton.IsEnabled
                        && !workbench.IsValidationSetRunning, $"latest={latest?.FileName};section={sectionButton.IsEnabled};distribution={distributionButton.IsEnabled};before={beforeRunRows};after={string.Join(";", workspaceOwner.AllSamples.Select(row => $"{row.FileName}/{row.Role}/{row.Status}/{row.Agreement}"))};summary={workbench.ValidationSetSummary};notice={workbench.ValidationSetRunNoticeText}");
                }
                finally
                {
                    workspaceOwner.PropertyChanged -= runningHandler;
                    SynchronizationContext.SetSynchronizationContext(commandContext);
                }

                view.IsValidationSamplesExternal = false;
                check($"{language} leaving external matrix restores standard columns and styles", grid.Columns.SequenceEqual(standardColumns)
                    && ReferenceEquals(grid.RowStyle, standardRowStyle) && ReferenceEquals(grid.ColumnHeaderStyle, standardHeaderStyle)
                    && ReferenceEquals(grid.CellStyle, standardCellStyle) && ((System.Windows.FrameworkElement)view.FindName("ValidationMatrixHeaderActions")).Visibility == System.Windows.Visibility.Collapsed,
                    $"columns={grid.Columns.Count};selected={workbench.SelectedValidationSetSample?.FileName};no shared Results/theme change");
                check($"{language} external header restores its standard template and height", ReferenceEquals(tab.Template, standardTabTemplate)
                    && double.IsNaN(navigation.Height) == double.IsNaN(standardNavigationHeight) && ((System.Windows.FrameworkElement)view.FindName("ValidationMatrixSummary")).Visibility == System.Windows.Visibility.Collapsed,
                    $"height={navigation.Height};templateRestored={ReferenceEquals(tab.Template, standardTabTemplate)}");
                view.IsValidationSamplesExternal = true;
                view.SetPresentationMode(OpenVisionLab.ThreeD.Shell.Views.Workbench.RecipeReviewPresentationMode.Results);
                check($"{language} leaving validation mode preserves standard table presentation", grid.Columns.SequenceEqual(standardColumns) && ReferenceEquals(grid.RowStyle, standardRowStyle)
                    && navigation.Visibility == System.Windows.Visibility.Collapsed && ((System.Windows.FrameworkElement)view.FindName("ValidationMatrixHeaderActions")).Visibility == System.Windows.Visibility.Collapsed,
                    "Results presentation mode uses the preserved standard path; external flag alone does not enable matrix");
            }
        }
        finally { global::OpenVisionLab.OpenVisionLanguageService.SetLanguage(languageBefore, save: false); }
    }

    private sealed class InlineValidationProgress(Action<OpenVisionLab.ThreeD.Tools.ToolRecipeValidationProgress> callback) : IProgress<OpenVisionLab.ThreeD.Tools.ToolRecipeValidationProgress>
    {
        public void Report(OpenVisionLab.ThreeD.Tools.ToolRecipeValidationProgress value) => callback(value);
    }

    private static void PumpUntil(Func<bool> condition)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        while (!condition() && clock.Elapsed < TimeSpan.FromSeconds(8))
        {
            System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Background);
            Thread.Sleep(10);
        }
        if (!condition()) throw new TimeoutException("Focused execution/context boundary did not complete.");
    }

    private static void VerifyExecutionBarPresentation(string reportPath, Action<string, bool, string> check)
    {
        var root = Path.GetDirectoryName(reportPath)!;
        var recipe = Path.Combine(root, "native-group-fixture", "grouped.ov3d-recipe.json");
        var doc = ToolRecipeDocumentStore.Load(recipe);
        var coupon = Path.Combine(root, "native-group-fixture", "coupon.C3D");
        var dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
        var previousContext = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext(dispatcher));
        try
        {
            using var reached = new ManualResetEventSlim();
            using var release = new ManualResetEventSlim();
            var notifications = new List<int>();
            var uiThread = Environment.CurrentManagedThreadId;
            using var owner = new ToolWorkbenchValidationSetExecutionOwner(() => notifications.Add(Environment.CurrentManagedThreadId));
            var progress = new InlineValidationProgress(value => { if (value.CompletedStatus is null) { reached.Set(); if (!release.Wait(TimeSpan.FromSeconds(8))) throw new TimeoutException("Progress release missing."); } });
            var task = owner.ExecuteAsync(doc, [new(coupon, ToolRecipeValidationSampleRole.Good)], progress);
            try
            {
                if (!reached.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("Actual execution did not reach the progress seam.");
                var callbackCount = notifications.Count;
                PumpUntil(() => owner.Elapsed.TotalSeconds >= 1.1 && notifications.Count > callbackCount);
                check("real active execution elapsed advances without completed samples on captured UI context", owner.IsRunning && owner.ElapsedText != "00:00"
                    && notifications.All(thread => thread == uiThread), $"elapsed={owner.ElapsedText};callbacks={notifications.Count};thread={uiThread};actual Tools paused at existing IProgress seam");
                var concurrent = owner.ExecuteAsync(doc, [new(coupon, ToolRecipeValidationSampleRole.Good)], progress);
                var rejected = false;
                try { concurrent.GetAwaiter().GetResult(); } catch (InvalidOperationException) { rejected = true; }
                check("timed execution retains duplicate gate and original cancellation owner", rejected && owner.IsRunning && !owner.CanStart, "second execution cannot replace CTS/timer");
                owner.Cancel();
            }
            finally { release.Set(); }
            PumpUntil(() => task.IsCompleted);
            var canceled = false;
            try { task.GetAwaiter().GetResult(); } catch (OperationCanceledException) { canceled = true; }
            var stoppedElapsed = owner.Elapsed;
            var stoppedCount = notifications.Count;
            var stopWait = System.Diagnostics.Stopwatch.StartNew();
            PumpUntil(() => stopWait.ElapsedMilliseconds >= 250);
            check("cancel ends elapsed clock and rejects queued context ticks", canceled && !owner.IsRunning && owner.CanStart
                && owner.Elapsed == stoppedElapsed && notifications.Count == stoppedCount, $"elapsed={stoppedElapsed};callbacks={stoppedCount};partial-result policy unchanged");
            reached.Reset(); release.Reset();
            var disposedTask = owner.ExecuteAsync(doc, [new(coupon, ToolRecipeValidationSampleRole.Good)], progress);
            try
            {
                if (!reached.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("Dispose execution seam missing.");
                Thread.Sleep(1100); // Leave a real timer post queued while the UI context is held.
                owner.Dispose();
            }
            finally { release.Set(); }
            PumpUntil(() => disposedTask.IsCompleted);
            try { disposedTask.GetAwaiter().GetResult(); } catch (OperationCanceledException) { }
            var disposedCount = notifications.Count;
            var disposedElapsed = owner.Elapsed;
            var disposeWait = System.Diagnostics.Stopwatch.StartNew();
            PumpUntil(() => disposeWait.ElapsedMilliseconds >= 250);
            check("dispose closes timer/context callbacks and owned cancellation deterministically", owner.IsDisposed && !owner.IsRunning
                && notifications.Count == disposedCount && owner.Elapsed == disposedElapsed, $"callbacks={disposedCount};elapsed={disposedElapsed};Windows={System.Windows.Application.Current.Windows.Count}");

            var languageBefore = global::OpenVisionLab.OpenVisionLanguageService.CurrentLanguage;
            try
            {
                foreach (var language in new[] { global::OpenVisionLab.OpenVisionLanguage.Korean, global::OpenVisionLab.OpenVisionLanguage.English })
                {
                    global::OpenVisionLab.OpenVisionLanguageService.SetLanguage(language, save: false);
                    using var shell = new ShellMainWindowViewModel(comparisonContractPath: Path.Combine(root, "bar-comparison.json"), comparisonReportPath: Path.Combine(root, "bar-comparison-report.json"),
                        shellScreenshotPath: Path.Combine(root, "bar-shell.png"), runRecordPath: Path.Combine(root, "bar-run.json"), htmlReportPath: Path.Combine(root, "bar-run.html"), csvReportPath: Path.Combine(root, "bar-run.csv"),
                        recentRunRecordsPath: Path.Combine(root, "bar-history-" + language + ".json"), recentRecipesPath: Path.Combine(root, "bar-recipes-" + language + ".json"), orderedRunRecordRoot: Path.Combine(root, "bar-records"), integrationSettingsPath: Path.Combine(root, "bar-settings.json"));
                    var workbench = shell.Workbench;
                    var opened = workbench.TryOpenTeachingRecipe(recipe, out var message);
                    shell.SelectWorkspaceCommand.Execute(ShellWorkspaceMode.Validate);
                    using var view = new OpenVisionLab.ThreeD.Shell.Views.Workbench.ToolRecipeWorkbenchView { DataContext = shell, Foreground = (System.Windows.Media.Brush)System.Windows.Application.Current.FindResource("ThreeD.PrimaryTextBrush") };
                    var dock = (OpenVisionLab.ThreeD.Docking.Controls.OpenVisionDockWorkspaceView)view.FindName("DockWorkspace");
                    var viewerContent = dock.ViewerContent as System.Windows.Controls.Grid ?? throw new InvalidOperationException("Canonical ViewerContent was not created.");
                    check($"{language} execution bar fixture follows real Validate stage injection", view.OperatorStage == OpenVisionLab.ThreeD.Docking.Controls.OpenVisionOperatorStage.Validate,
                        "DataContextChanged -> ApplyOperatorStage -> DockWorkspace.SetOperatorStage; Window0");
                    dock.ViewerContent = null;
                    view.Content = viewerContent; // Mount the actual existing ViewerContent, retaining Shell DataContext and View compact ancestor.
                    check($"{language} fresh runbar uses canonical Shell/Workbench context without prior result", opened && workbench.ValidationSetLastRunAt is null && !workbench.IsValidationSetStale, message);
                    var longInput = Path.Combine(root, "native-group-fixture", "Long coupon input for actual sequential validation progress and teaching conditions.C3D");
                    File.Copy(coupon, longInput, true);
                    workbench.SetValidationSetSources(new[] { longInput }.Concat(workbench.ValidationSetSamples.Where(sample => !string.Equals(sample.SourcePath, coupon, StringComparison.OrdinalIgnoreCase)).Select(sample => sample.SourcePath)).ToArray());
                    var capturedProgress = new HashSet<int>();
                    workbench.PropertyChanged += (_, args) =>
                    {
                        if (args.PropertyName != nameof(ToolWorkbenchViewModel.ValidationSetProgressText) || !workbench.IsValidationSetRunning
                            || !workbench.ValidationSetProgressText.Contains(Path.GetFileName(longInput))) return;
                        foreach (var width in new[] { 956, 1600 })
                        {
                            if (!capturedProgress.Add(width)) continue;
                            view.Width = width; view.Height = 760;
                            view.Measure(new System.Windows.Size(width, 760)); view.Arrange(new System.Windows.Rect(0, 0, width, 760));
                            var bar = VisualChildren<System.Windows.Controls.Border>(view).Single(control => System.Windows.Automation.AutomationProperties.GetAutomationId(control) == "ValidationExecutionBar");
                            foreach (var element in VisualChildren<System.Windows.FrameworkElement>(bar))
                            {
                                System.Windows.Data.BindingOperations.GetBindingExpression(element, System.Windows.UIElement.VisibilityProperty)?.UpdateTarget();
                                if (element is System.Windows.Controls.TextBlock text) System.Windows.Data.BindingOperations.GetBindingExpression(text, System.Windows.Controls.TextBlock.TextProperty)?.UpdateTarget();
                                if (element is System.Windows.Controls.ProgressBar progressBar) System.Windows.Data.BindingOperations.GetBindingExpression(progressBar, System.Windows.Controls.Primitives.RangeBase.ValueProperty)?.UpdateTarget();
                            }
                            view.Measure(new System.Windows.Size(width, 760)); view.Arrange(new System.Windows.Rect(0, 0, width, 760)); view.UpdateLayout();
                            var progressGrid = VisualChildren<System.Windows.Controls.Grid>(bar).Single(control => System.Windows.Automation.AutomationProperties.GetAutomationId(control) == "ValidationRunningProgress");
                            var progressText = VisualChildren<System.Windows.Controls.TextBlock>(bar).Single(control => System.Windows.Automation.AutomationProperties.GetAutomationId(control) == "ValidationProgressText");
                            var track = VisualChildren<System.Windows.Controls.ProgressBar>(bar).Single();
                            var clock = VisualChildren<System.Windows.Controls.TextBlock>(bar).Single(control => System.Windows.Automation.AutomationProperties.GetAutomationId(control) == "ValidationElapsed");
                            var stop = VisualChildren<System.Windows.Controls.Primitives.ButtonBase>(bar).Single(control => System.Windows.Automation.AutomationProperties.GetAutomationId(control) == "CancelValidationWorkspaceSet");
                            var textPoint = progressText.TranslatePoint(new System.Windows.Point(), bar);
                            var trackPoint = track.TranslatePoint(new System.Windows.Point(), bar);
                            var clockPoint = clock.TranslatePoint(new System.Windows.Point(), bar);
                            var stopPoint = stop.TranslatePoint(new System.Windows.Point(), bar);
                            check($"{language} {width}px actual long progress stays in right group with Stop and clock", bar.ActualWidth == width && bar.ActualHeight == 44
                                && progressGrid.Visibility == System.Windows.Visibility.Visible && progressText.Text == workbench.ValidationSetProgressText
                                && track.ActualHeight == 6 && Math.Abs(trackPoint.X - textPoint.X - progressText.ActualWidth - 8) < .1
                                && Math.Abs(clockPoint.X - trackPoint.X - track.ActualWidth - 8) < .1 && clockPoint.X + clock.ActualWidth + 8 <= stopPoint.X + .1
                                && stop.ActualHeight == 30 && stop.IsEnabled && ReferenceEquals(stop.Command, workbench.CancelValidationSetCommand),
                                $"bar={bar.ActualWidth}x{bar.ActualHeight};text={textPoint}/{progressText.ActualWidth};track={trackPoint}/{track.ActualWidth}x{track.ActualHeight};clock={clockPoint}/{clock.Text};Stop={stopPoint};actual IProgress notification;Window0");
                            var drawing = new System.Windows.Media.DrawingVisual();
                            using (var context = drawing.RenderOpen()) context.DrawRectangle(new System.Windows.Media.VisualBrush(bar) { ViewboxUnits = System.Windows.Media.BrushMappingMode.Absolute, Viewbox = new System.Windows.Rect(0, 0, width, 44), Stretch = System.Windows.Media.Stretch.Fill }, null, new System.Windows.Rect(0, 0, width, 44));
                            var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(width, 44, 96, 96, System.Windows.Media.PixelFormats.Pbgra32); bitmap.Render(drawing);
                            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                            using var stream = File.Create(Path.Combine(root, $"validation-runbar-{language}-{width}-running.png")); encoder.Save(stream);
                        }
                    };
                    var cancelFirst = true;
                    workbench.PropertyChanged += (_, args) => { if (cancelFirst && args.PropertyName == nameof(ToolWorkbenchViewModel.IsValidationSetRunning) && workbench.IsValidationSetRunning) workbench.CancelValidationSetCommand.Execute(null); };
                    var firstCancel = workbench.RunValidationSetAsync(); PumpUntil(() => firstCancel.IsCompleted); firstCancel.GetAwaiter().GetResult(); cancelFirst = false;
                    check($"{language} first cancellation never claims prior results", workbench.ValidationSetLastRunAt is null
                        && !workbench.ValidationSetRunNoticeText.Contains(language == global::OpenVisionLab.OpenVisionLanguage.Korean ? "이전 결과" : "previous results"), workbench.ValidationSetRunNoticeText);
                    workbench.RecipeName = "Long executed teaching condition identity / 긴 실행 티칭 조건 이름 / repeat grid coupon metrology basis";
                    var nameAtRun = workbench.RecipeName;
                    var originalEnabled = workbench.PipelineSteps[0].OutputEnabled;
                    var editOnRunning = true;
                    var cancelOnRunning = false;
                    workbench.PropertyChanged += (_, args) =>
                    {
                        if (args.PropertyName != nameof(ToolWorkbenchViewModel.IsValidationSetRunning) || !workbench.IsValidationSetRunning) return;
                        if (editOnRunning) { editOnRunning = false; workbench.PipelineSteps[0].OutputEnabled = !originalEnabled; }
                        if (cancelOnRunning) workbench.CancelValidationSetCommand.Execute(null);
                    };
                    var run = workbench.RunValidationSetAsync();
                    check($"{language} explicit run enters owned progress with actual command state", workbench.IsValidationSetRunning && !workbench.RunValidationSetCommand.CanExecute(null)
                        && workbench.CancelValidationSetCommand.CanExecute(null), $"elapsed={workbench.ValidationSetElapsedText};no simulated progress");
                    PumpUntil(() => run.IsCompleted);
                    run.GetAwaiter().GetResult();
                    check($"{language} live applied edit leaves completed captured basis stale", workbench.ValidationSetLastRunAt is not null
                        && workbench.IsValidationSetStale && workbench.ValidationSetRunBasisText.Contains(nameAtRun)
                        && workbench.ValidationSetRunBasisText.Contains(language == global::OpenVisionLab.OpenVisionLanguage.Korean ? "저장 전 변경" : "unsaved changes"), workbench.ValidationSetRunBasisText);
                    check($"{language} actual long progress renders both container widths", capturedProgress.SetEquals(new[] { 956, 1600 }), $"captured={string.Join(',', capturedProgress)};no simulated progress");
                    var priorTime = workbench.ValidationSetLastRunAt;
                    var priorRows = workbench.ValidationSetSamples.ToArray();
                    cancelOnRunning = true;
                    var canceledRun = workbench.RunValidationSetAsync();
                    cancelOnRunning = false;
                    PumpUntil(() => canceledRun.IsCompleted);
                    canceledRun.GetAwaiter().GetResult();
                    check($"{language} canceled rerun retains prior timestamp and rows", workbench.ValidationSetLastRunAt == priorTime && workbench.ValidationSetSamples.SequenceEqual(priorRows)
                        && !string.IsNullOrEmpty(workbench.ValidationSetRunNoticeText), $"basis={workbench.ValidationSetRunBasisText};notice={workbench.ValidationSetRunNoticeText}");
                    foreach (var width in new[] { 956, 1600 })
                    {
                        view.Width = width; view.Height = 760;
                        view.Measure(new System.Windows.Size(width, 760)); view.Arrange(new System.Windows.Rect(0, 0, width, 760));
                        dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Background); view.UpdateLayout();
                        var bar = VisualChildren<System.Windows.Controls.Border>(view).Single(control => System.Windows.Automation.AutomationProperties.GetAutomationId(control) == "ValidationExecutionBar");
                        var runButton = VisualChildren<System.Windows.Controls.Primitives.ButtonBase>(bar).Single(control => System.Windows.Automation.AutomationProperties.GetAutomationId(control) == "RunValidationWorkspaceSet");
                        var stale = VisualChildren<System.Windows.Controls.Border>(bar).Single(control => System.Windows.Automation.AutomationProperties.GetAutomationId(control) == "ValidationStaleBasis");
                        var basis = VisualChildren<System.Windows.Controls.TextBlock>(bar).Single(control => System.Windows.Automation.AutomationProperties.GetAutomationId(control) == "ValidationIdleSummary");
                        var notice = VisualChildren<System.Windows.Controls.TextBlock>(bar).Single(control => System.Windows.Automation.AutomationProperties.GetAutomationId(control) == "ValidationRunNotice");
                        var buttonOrigin = runButton.TranslatePoint(new System.Windows.Point(), bar);
                        var staleOrigin = stale.TranslatePoint(new System.Windows.Point(), bar);
                        var requiredPrefix = (language == global::OpenVisionLab.OpenVisionLanguage.Korean ? "마지막 검증 " : "Last validation ") + workbench.ValidationSetLastRunAt!.Value.ToString("yyyy-MM-dd HH:mm:ss");
                        var prefixWidth = new System.Windows.Media.FormattedText(requiredPrefix, System.Globalization.CultureInfo.CurrentCulture, System.Windows.FlowDirection.LeftToRight,
                            new System.Windows.Media.Typeface(basis.FontFamily, basis.FontStyle, basis.FontWeight, basis.FontStretch), basis.FontSize, basis.Foreground, 1).Width;
                        check($"{language} {width}px stale runbar retains warning, basis binding and reachable Run", bar.ActualHeight == 44 && runButton.ActualHeight == 30
                            && stale.Visibility == System.Windows.Visibility.Visible && basis.Text == workbench.ValidationSetRunBasisText && basis.ActualWidth >= prefixWidth
                            && staleOrigin.X + stale.ActualWidth <= buttonOrigin.X && buttonOrigin.X + runButton.ActualWidth <= bar.ActualWidth
                            && ReferenceEquals(runButton.Command, workbench.RunValidationSetCommand) && notice.ActualHeight > 0,
                            $"bar={bar.ActualWidth}x{bar.ActualHeight};basisWidth={basis.ActualWidth};requiredTimestamp={prefixWidth};stale={staleOrigin}/{stale.ActualWidth};Run={buttonOrigin}/{runButton.ActualWidth}x{runButton.ActualHeight};Window0;mounted support");
                        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)bar.ActualWidth, 44, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                        var drawing = new System.Windows.Media.DrawingVisual();
                        using (var context = drawing.RenderOpen()) context.DrawRectangle(new System.Windows.Media.VisualBrush(bar) { ViewboxUnits = System.Windows.Media.BrushMappingMode.Absolute, Viewbox = new System.Windows.Rect(0, 0, bar.ActualWidth, 44), Stretch = System.Windows.Media.Stretch.Fill }, null, new System.Windows.Rect(0, 0, bar.ActualWidth, 44));
                        bitmap.Render(drawing);
                        var png = new System.Windows.Media.Imaging.PngBitmapEncoder(); png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                        using var stream = File.Create(Path.Combine(root, $"validation-runbar-{language}-{width}-stale.png")); png.Save(stream);
                    }
                    workbench.PipelineSteps[0].OutputEnabled = originalEnabled;
                    check($"{language} restoring original condition clears stale without Run", !workbench.IsValidationSetStale && workbench.ValidationSetLastRunAt == priorTime, "existing recipe notification refresh path");
                    var recovery = workbench.RunValidationSetAsync(); PumpUntil(() => recovery.IsCompleted); recovery.GetAwaiter().GetResult();
                    check($"{language} valid explicit rerun recovers and adopts its own basis", !workbench.IsValidationSetStale && workbench.ValidationSetLastRunAt > priorTime
                        && workbench.ValidationSetSamples.Count == 3 && string.IsNullOrEmpty(workbench.ValidationSetRunNoticeText), workbench.ValidationSetRunBasisText);
                    var routeRows = workbench.ValidationSetSamples.ToArray();
                    var ng = routeRows.Single(sample => sample.Status == "Fail");
                    workbench.SelectedValidationSetSample = ng;
                    var sourcePathBefore = workbench.Source.Path;
                    var sourceHashBefore = workbench.SourceSession.SourceBinding?.ContentSha256;
                    var sourceFileHashBefore = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(sourcePathBefore)));
                    var recipeBefore = System.Text.Json.JsonSerializer.Serialize(new { workbench.RecipeName, workbench.RecipePath, workbench.RecipeSchemaVersion,
                        Steps = workbench.PipelineSteps.Select(step => new { step.Id, step.ToolId, step.ToolName, step.OutputEnabled, step.InputEntityIdsText, step.OutputEntityId, step.Group,
                            Parameters = step.Parameters.Select(parameter => new { parameter.Name, parameter.Value }) }), Selections = workbench.GetCurrentAppliedTeachingSelections() });
                    var dirtyBeforeRoute = workbench.IsDirty;
                    var routeLayout = workbench.EffectiveViewerLayout;
                    var routePins = (workbench.MainViewerContentId, workbench.AuxiliaryViewerContentId);
                    var routeTime = workbench.ValidationSetLastRunAt;
                    var logsBeforeRoute = workbench.RunLog.Count;
                    var executionTransitions = 0;
                    var executionNotifications = new List<string>();
                    workbench.PropertyChanged += (_, args) =>
                    {
                        var running = args.PropertyName switch
                        {
                            nameof(ToolWorkbenchViewModel.IsValidationSetRunning) => workbench.IsValidationSetRunning,
                            nameof(ToolWorkbenchViewModel.IsOrderedRunRunning) => workbench.IsOrderedRunRunning,
                            nameof(ToolWorkbenchViewModel.IsSelectedStepPreviewRunning) => workbench.IsSelectedStepPreviewRunning,
                            _ => (bool?)null,
                        };
                        if (running is { } active) { executionNotifications.Add($"{args.PropertyName}={active}"); if (active) executionTransitions++; }
                    };
                    workbench.MarkSelectedStepParameterDraftDirty();
                    check($"{language} canonical pending draft establishes navigation guard", workbench.HasPendingStepParameterChanges
                        && !shell.SelectWorkspaceCommand.CanExecute(ShellWorkspaceMode.Teach), "existing applied/draft owner; no new policy");
                    workbench.OpenValidationSetComparisonCommand.Execute(null);
                    check($"{language} guarded public comparison retains Validate and inactive display", shell.SelectedWorkspaceMode == ShellWorkspaceMode.Validate
                        && !workbench.IsOutputCompareViewerActive && shell.StatusText == shell.InspectionStageNavigationStatus, shell.StatusText);
                    workbench.DiscardSelectedStepParameterDraftCommand.Execute(null);
                    workbench.OpenValidationSetComparisonCommand.Execute(null);
                    var compareB = workbench.GetMainViewerCandidate(workbench.CompareSlotBArtifactId);
                    check($"{language} public NG comparison enters Teach with authored A and selected B", shell.SelectedWorkspaceMode == ShellWorkspaceMode.Teach
                        && workbench.IsOutputCompareViewerActive && workbench.EffectiveViewerLayout == ViewerWorkspaceLayout.SplitVertical
                        && workbench.CompareSlotAArtifactId == workbench.Source.Id && compareB?.SourcePath == ng.SourcePath
                        && workbench.EffectiveMainViewerContentId == workbench.CompareSlotAArtifactId && workbench.EffectiveAuxiliaryViewerContentId == workbench.CompareSlotBArtifactId,
                        $"workspace={shell.SelectedWorkspaceMode};A={workbench.EffectiveMainViewerContentId};B={compareB?.SourcePath};layout={workbench.EffectiveViewerLayout};Window0 VM support");
                    workbench.OpenValidationSetComparisonCommand.Execute(null);
                    check($"{language} repeated comparison never toggles off active A/B", shell.SelectedWorkspaceMode == ShellWorkspaceMode.Teach && workbench.IsOutputCompareViewerActive,
                        "existing toggle command skipped when already active");
                    shell.SelectWorkspaceCommand.Execute(ShellWorkspaceMode.Validate);
                    check($"{language} D90 public Validate return restores normal layout and hidden auxiliary focus", !workbench.IsOutputCompareViewerActive
                        && workbench.EffectiveViewerLayout == routeLayout && workbench.IsSingleViewerLayout
                        && workbench.ViewerWorkspace.Layout == ViewerWorkspaceLayout.SplitVertical
                        && !workbench.FocusViewerWorkspaceSlotCommand.CanExecute(ViewerWorkspaceSession.AuxiliarySlotId)
                        && routePins == (workbench.MainViewerContentId, workbench.AuxiliaryViewerContentId), $"effective={workbench.EffectiveViewerLayout};session={workbench.ViewerWorkspace.Layout};Window0 VM support");
                    shell.SelectWorkspaceCommand.Execute(ShellWorkspaceMode.Inspect);
                    check($"{language} D90 Inspect return retains normal layout without active comparison", !workbench.IsOutputCompareViewerActive
                        && workbench.EffectiveViewerLayout == routeLayout && workbench.IsSingleViewerLayout, $"workspace={shell.SelectedWorkspaceMode};layout={workbench.EffectiveViewerLayout}");
                    shell.SelectWorkspaceCommand.Execute(ShellWorkspaceMode.Teach);
                    check($"{language} D90 Teach reentry resumes the same A/B comparison", workbench.IsOutputCompareViewerActive
                        && workbench.EffectiveViewerLayout == ViewerWorkspaceLayout.SplitVertical
                        && workbench.EffectiveMainViewerContentId == workbench.CompareSlotAArtifactId
                        && workbench.GetMainViewerCandidate(workbench.EffectiveAuxiliaryViewerContentId)?.SourcePath == ng.SourcePath, "existing context refresh; no new compare execution");
                    shell.SelectWorkspaceCommand.Execute(ShellWorkspaceMode.Validate);
                    var nextRow = routeRows.Single(sample => sample.Status == "Error");
                    workbench.SelectedValidationSetSample = nextRow;
                    var nextSource = nextRow.SourcePath;
                    workbench.OpenValidationSetComparisonCommand.Execute(null);
                    check($"{language} return and next public comparison refresh selected B without losing display", shell.SelectedWorkspaceMode == ShellWorkspaceMode.Teach
                        && workbench.IsOutputCompareViewerActive && workbench.GetMainViewerCandidate(workbench.CompareSlotBArtifactId)?.SourcePath == nextSource, nextSource);
                    shell.SelectWorkspaceCommand.Execute(ShellWorkspaceMode.Validate);
                    var recipeAfter = System.Text.Json.JsonSerializer.Serialize(new { workbench.RecipeName, workbench.RecipePath, workbench.RecipeSchemaVersion,
                        Steps = workbench.PipelineSteps.Select(step => new { step.Id, step.ToolId, step.ToolName, step.OutputEnabled, step.InputEntityIdsText, step.OutputEntityId, step.Group,
                            Parameters = step.Parameters.Select(parameter => new { parameter.Name, parameter.Value }) }), Selections = workbench.GetCurrentAppliedTeachingSelections() });
                    check($"{language} comparison and return preserve authored source recipe results and explicit execution", sourcePathBefore == workbench.Source.Path
                        && sourceHashBefore == workbench.SourceSession.SourceBinding?.ContentSha256
                        && sourceFileHashBefore == Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(workbench.Source.Path)))
                        && recipeBefore == recipeAfter && dirtyBeforeRoute == workbench.IsDirty && routeRows.SequenceEqual(workbench.ValidationSetSamples)
                        && routePins == (workbench.MainViewerContentId, workbench.AuxiliaryViewerContentId) && workbench.EffectiveViewerLayout == routeLayout
                        && routeTime == workbench.ValidationSetLastRunAt && logsBeforeRoute == workbench.RunLog.Count && executionTransitions == 0
                        && workbench.SelectedValidationSetSample?.SourcePath == nextSource, $"rows={routeRows.Length};timestamp={routeTime};Run/Preview active transitions={executionTransitions};notifications={string.Join(',', executionNotifications)};RunLog={logsBeforeRoute}/{workbench.RunLog.Count};workspace={shell.SelectedWorkspaceMode}");
                    var roleBasis = workbench.ValidationSetLastRunAt;
                    var untouchedRow = workbench.ValidationSetSamples[1];
                    var editedRow = workbench.ValidationSetSamples[0];
                    workbench.SetValidationSampleRoleCommand.Execute(workbench.ValidationSetSamples[0].BadRoleChange);
                    check($"{language} partial role change preserves the other executed rows and basis", workbench.ValidationSetLastRunAt == roleBasis
                        && workbench.ValidationSetSamples[0].Status == editedRow.Status && ReferenceEquals(workbench.ValidationSetSamples[0].Steps, editedRow.Steps)
                        && ReferenceEquals(workbench.ValidationSetSamples[1], untouchedRow), workbench.ValidationSetRunBasisText);

                }
            }
            finally { global::OpenVisionLab.OpenVisionLanguageService.SetLanguage(languageBefore, save: false); }
        }
        finally { SynchronizationContext.SetSynchronizationContext(previousContext); }
    }

    private static IEnumerable<T> VisualChildren<T>(System.Windows.DependencyObject root) where T : System.Windows.DependencyObject
    {
        for (var index = 0; index < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, index);
            if (child is T match) yield return match;
            foreach (var descendant in VisualChildren<T>(child)) yield return descendant;
        }
    }

    private static ToolRecipeDocument CreateDocument(string sourcePath)
    {
        var binding = ToolRecipeSelectionSourceBindingVerifier.ReadIdentity(sourcePath);
        var source = new ToolRecipeSource(
            "source.validation-workspace", "Validation workspace source", "C3D", "model",
            "frame.c3d-grid-index", sourcePath, new FileInfo(sourcePath).Length,
            binding.ContentSha256, binding.GridWidth, binding.GridHeight);
        var reference = new ToolRecipeSelection(
            "selection.reference", "Reference ROI", ToolRecipeSelectionKinds.GridRectangle,
            source.Id, source.FrameId, binding, new ToolRecipeGridRectangle(0, 0, 2, 4), null, null);
        var measurement = new ToolRecipeSelection(
            "selection.measurement", "Measurement ROI", ToolRecipeSelectionKinds.GridRectangle,
            source.Id, source.FrameId, binding, new ToolRecipeGridRectangle(2, 0, 2, 4), null, null);
        var step = new ToolRecipeStep(
            "step.thickness", "thickness", "Dual-surface Thickness", 3,
            [source.Id, reference.Id, measurement.Id], "result.thickness",
            [
                new ToolRecipeParameter("MinimumThickness", "0"),
                new ToolRecipeParameter("MaximumThickness", "10"),
                new ToolRecipeParameter("MinimumValidSampleCount", "1")
            ]);
        return new ToolRecipeDocument(ToolRecipeDocument.CurrentSchemaVersion,
            "Validation workspace fixture", source, [], [step], [reference, measurement]);
    }
}
