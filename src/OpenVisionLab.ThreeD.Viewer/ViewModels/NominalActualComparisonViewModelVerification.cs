using System.Globalization;
using System.IO;
using System.Numerics;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Viewer.ViewModels;

internal static class NominalActualComparisonViewModelVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var lines = new List<string>
        {
            "OpenVisionLab 3D nominal/actual ViewModel verification",
            $"Generated: {DateTimeOffset.Now:O}"
        };
        var passed = 0;

        try
        {
            var viewModel = new NominalActualComparisonViewModel();
            var propertyChanges = 0;
            viewModel.PropertyChanged += (_, _) => propertyChanges++;

            Check("initial state", viewModel.State == NominalActualComparisonState.NoInputs, viewModel.State.ToString());
            Check("initial inputs", !viewModel.InputsReady, viewModel.InputsReady.ToString());
            Check("initial preview disabled", !viewModel.CanPreview && !viewModel.PreviewCommand.CanExecute(null), viewModel.StateSummary);
            Check("initial cancel disabled", !viewModel.CanCancel && !viewModel.CancelCommand.CanExecute(null), viewModel.StateSummary);
            Check("initial publish disabled", !viewModel.CanPublish && !viewModel.PublishCommand.CanExecute(null), viewModel.StateSummary);
            Check("initial evidence", viewModel.EvidenceSummary == "No comparison evidence.", viewModel.EvidenceSummary);
            Check("initial selected deviation", !viewModel.HasSelectedDeviation && viewModel.SelectedDeviation is null, viewModel.SelectedDeviationSummary);
            Check(
                "initial display density state",
                viewModel.CurrentDisplayDensity == "(none)"
                && viewModel.NextPreviewDisplayDensity == "Balanced"
                && viewModel.NextPreviewDisplaySampleBudget == 60000
                && !viewModel.DisplaySamplingChangePending,
                viewModel.NextPreviewSamplingSummary);

            var rootViewModel = new MainWindowViewModel
            {
                GlbSampleVisible = true
            };
            var rootInput = CreateInput();
            rootViewModel.ConfigureNominalActualComparison(rootInput);
            Check(
                "root owns comparison input",
                ReferenceEquals(rootViewModel.NominalActualInput, rootInput),
                rootViewModel.NominalActualInput?.SourceFingerprint ?? "(none)");
            Check(
                "root exposes comparison source entities",
                rootViewModel.SourceEntities.Count(entity =>
                    entity.Id == rootInput.ActualSource.Id
                    || entity.Id == rootInput.NominalSource.Id
                    || entity.Id == rootInput.QuerySource.Id) == 3,
                $"sources={rootViewModel.SourceEntities.Count}");
            Check(
                "nominal visibility hides imported mesh",
                !rootViewModel.GlbSampleVisible && !rootViewModel.NominalActual.NominalVisible,
                $"mesh={rootViewModel.GlbSampleVisible}|nominal={rootViewModel.NominalActual.NominalVisible}");
            Check(
                "comparison unit updates camera status",
                rootViewModel.BottomStatus.StartsWith("Model units: mm |", StringComparison.Ordinal),
                rootViewModel.BottomStatus);
            rootViewModel.NominalActual.NominalVisible = true;
            Check(
                "nominal visibility shows imported mesh",
                rootViewModel.GlbSampleVisible,
                $"mesh={rootViewModel.GlbSampleVisible}|nominal={rootViewModel.NominalActual.NominalVisible}");
            rootViewModel.GlbSampleVisible = false;
            Check(
                "imported mesh visibility updates nominal",
                !rootViewModel.NominalActual.NominalVisible,
                $"mesh={rootViewModel.GlbSampleVisible}|nominal={rootViewModel.NominalActual.NominalVisible}");

            NominalActualPreviewRequestedEventArgs? rootRequest = null;
            rootViewModel.NominalActual.PreviewRequested += (_, args) => rootRequest = args;
            rootViewModel.SelectedRenderDensity = "Fast";
            rootViewModel.NominalActual.PreviewCommand.Execute(null);
            Check(
                "root preview snapshots display density",
                rootRequest?.DisplayDensity == "Fast" && rootRequest.MaximumDisplaySamples == 25000,
                rootRequest is null
                    ? "request missing"
                    : $"{rootRequest.DisplayDensity}|{rootRequest.MaximumDisplaySamples}");
            var rootResult = CreateResult(rootInput);
            Check(
                "root preview completion",
                rootViewModel.NominalActual.CompletePreview(rootRequest!.RequestId, rootResult),
                rootViewModel.NominalActual.State.ToString());
            Check(
                "root publishes separate result entity",
                rootViewModel.PublishNominalActualComparison(rootResult)
                && rootViewModel.NominalActual.ConfirmPublished("Evidence: root verification publish recorded")
                && rootViewModel.ResultEntities.Single().Id == NominalActualComparisonContract.ResultEntityId
                && rootViewModel.ResultEntities.Single().SourceEntityId == rootInput.ActualSource.Id,
                rootViewModel.PublishedResultSummary);
            var publishedRootResult = rootViewModel.NominalActual.PreviewResult;
            rootViewModel.SelectedRenderDensity = "Detailed";
            Check(
                "density change preserves completed result",
                rootViewModel.NominalActual.State == NominalActualComparisonState.Published
                && ReferenceEquals(rootViewModel.NominalActual.PreviewResult, publishedRootResult)
                && rootViewModel.NominalActual.CurrentDisplayDensity == "Fast"
                && rootViewModel.NominalActual.CurrentDisplaySampleBudget == 25000,
                rootViewModel.NominalActual.CurrentDisplaySamplingSummary);
            Check(
                "density change is next Preview only",
                rootViewModel.NominalActual.DisplaySamplingChangePending
                && rootViewModel.NominalActual.NextPreviewDisplayDensity == "Detailed"
                && rootViewModel.NominalActual.NextPreviewDisplaySampleBudget == 150000
                && rootViewModel.NominalActual.NextPreviewSamplingSummary.Contains("run Preview to apply", StringComparison.Ordinal),
                rootViewModel.NominalActual.NextPreviewSamplingSummary);

            var unwiredViewModel = new NominalActualComparisonViewModel();
            ApplyReadyInputs(unwiredViewModel);
            unwiredViewModel.PreviewCommand.Execute(null);
            Check("missing executor state", unwiredViewModel.State == NominalActualComparisonState.Failed, unwiredViewModel.State.ToString());
            Check("missing executor cause", unwiredViewModel.ResultSummary.Contains("not connected", StringComparison.Ordinal), unwiredViewModel.ResultSummary);

            var readyInput = ApplyReadyInputs(viewModel);
            Check("ready state", viewModel.State == NominalActualComparisonState.InputsReady, viewModel.State.ToString());
            Check("ready inputs", viewModel.InputsReady, viewModel.ValidationSummary);
            Check("ready preview", viewModel.CanPreview && viewModel.PreviewCommand.CanExecute(null), viewModel.StateSummary);
            Check("ready edit", viewModel.CanEdit, viewModel.CanEdit.ToString());
            Check("default visibility", viewModel.ActualVisible && !viewModel.NominalVisible, $"actual={viewModel.ActualVisible}|nominal={viewModel.NominalVisible}");
            Check("explicit unit", viewModel.Unit == "mm", viewModel.Unit);
            Check("query identity", viewModel.QuerySourceSummary.Contains("validation-query.ply", StringComparison.Ordinal), viewModel.QuerySourceSummary);

            viewModel.LowerTolerance = 0.100;
            Check("invalid tolerance", !viewModel.ToleranceIsValid, viewModel.ValidationSummary);
            Check("invalid tolerance blocks preview", !viewModel.CanPreview && !viewModel.PreviewCommand.CanExecute(null), viewModel.StateSummary);
            Check("invalid tolerance visible", viewModel.StateSummary.Contains("invalid tolerance", StringComparison.Ordinal), viewModel.StateSummary);
            viewModel.LowerTolerance = -0.300;
            Check("restored tolerance", viewModel.ToleranceIsValid && viewModel.CanPreview, viewModel.ValidationSummary);

            NominalActualPreviewRequestedEventArgs? request = null;
            viewModel.PreviewRequested += (_, args) => request = args;
            viewModel.PreviewCommand.Execute(null);
            Check("preview request", request is not null, "event raised");
            Check(
                "preview request density contract",
                request?.DisplayDensity == "Balanced" && request.MaximumDisplaySamples == 60000,
                request is null ? "request missing" : $"{request.DisplayDensity}|{request.MaximumDisplaySamples}");
            Check("running state", viewModel.State == NominalActualComparisonState.PreviewRunning, viewModel.State.ToString());
            Check("running commands", viewModel.CanCancel && !viewModel.CanPreview, $"cancel={viewModel.CanCancel}|preview={viewModel.CanPreview}");
            Check("progress accepted", viewModel.ReportPreviewProgress(request!.RequestId, 25, 100, TimeSpan.FromSeconds(1.25)), viewModel.ProgressSummary);
            Check("progress percent", Math.Abs(viewModel.ProgressPercent - 25.0) < 1e-12, viewModel.ProgressPercent.ToString("R", CultureInfo.InvariantCulture));
            Check("progress summary", viewModel.ProgressSummary.Contains("25.0%", StringComparison.Ordinal), viewModel.ProgressSummary);

            var cancelledRequestId = request.RequestId;
            var cancelledToken = request.CancellationToken;
            viewModel.CancelCommand.Execute(null);
            Check("cancellation token", cancelledToken.IsCancellationRequested, cancelledToken.IsCancellationRequested.ToString());
            Check("cancelled state", viewModel.State == NominalActualComparisonState.InputsReady, viewModel.State.ToString());
            Check("cancelled result isolation", !viewModel.LegendVisible && !viewModel.DistributionVisible, viewModel.ResultSummary);
            Check(
                "cancelled completion rejected",
                !viewModel.CompletePreview(cancelledRequestId, CreateResult(readyInput)),
                $"request={cancelledRequestId}");

            request = null;
            viewModel.PreviewCommand.Execute(null);
            Check("second preview request", request is not null, "event raised");
            var previewResult = CreateResult(readyInput);
            Check("preview completion", viewModel.CompletePreview(request!.RequestId, previewResult), viewModel.ResultSummary);
            Check("preview-ready state", viewModel.State == NominalActualComparisonState.PreviewReady, viewModel.State.ToString());
            Check("preview publish enabled", viewModel.CanPublish && viewModel.PublishCommand.CanExecute(null), viewModel.StateSummary);
            Check("preview visual state", viewModel.LegendVisible && viewModel.DistributionVisible && viewModel.HudVisible, viewModel.LegendSummary);
            Check("typed preview result", ReferenceEquals(viewModel.PreviewResult, previewResult), viewModel.ResultSummary);
            Check(
                "completed display density state",
                viewModel.CurrentDisplayDensity == "Balanced"
                && viewModel.CurrentDisplaySampleBudget == 60000
                && !viewModel.DisplaySamplingChangePending
                && viewModel.CurrentDisplaySamplingSummary.Contains("1/100 points", StringComparison.Ordinal)
                && viewModel.NextPreviewSamplingSummary.Contains("matches current display", StringComparison.Ordinal),
                $"{viewModel.CurrentDisplaySamplingSummary}|{viewModel.NextPreviewSamplingSummary}");
            Check(
                "preview fingerprint",
                viewModel.CompletedPreviewFingerprint == previewResult.Input.ExecutionFingerprint,
                viewModel.CompletedPreviewFingerprint);
            var selectedSample = previewResult.DisplaySamples.Single();
            Check(
                "selected deviation accepted",
                viewModel.SelectDeviation(selectedSample)
                && viewModel.HasSelectedDeviation
                && viewModel.SelectedDeviation == selectedSample,
                viewModel.SelectedDeviationSummary);
            Check(
                "selected deviation provenance",
                viewModel.SelectedDeviationSummary.Contains("#7", StringComparison.Ordinal)
                && viewModel.SelectedDeviationToleranceStatus == "Within tolerance"
                && viewModel.SelectedDeviationDetails.Contains("unsigned 0.25 mm", StringComparison.Ordinal)
                && viewModel.SelectedDeviationDetails.Contains("triangle #42", StringComparison.Ordinal)
                && viewModel.SelectedDeviationDetails.Contains("query=query.actual", StringComparison.Ordinal),
                viewModel.SelectedDeviationDetails);
            Check(
                "foreign display sample rejected",
                !viewModel.SelectDeviation(selectedSample with { QueryPointIndex = 8 }),
                viewModel.SelectedDeviationSummary);

            viewModel.UpperTolerance = 0.400;
            Check("stale tolerance state", viewModel.State == NominalActualComparisonState.PreviewStale, viewModel.State.ToString());
            Check("stale blocks publish", !viewModel.CanPublish, viewModel.StateSummary);
            Check("stale preserves review", viewModel.LegendVisible && viewModel.DistributionVisible, viewModel.LegendSummary);
            Check("stale clears selected deviation", !viewModel.HasSelectedDeviation, viewModel.SelectedDeviationSummary);
            viewModel.UpperTolerance = 0.300;
            Check("fingerprint restoration", viewModel.State == NominalActualComparisonState.PreviewReady, viewModel.State.ToString());

            var publishRaised = false;
            viewModel.PublishRequested += (_, args) =>
            {
                publishRaised = args.Fingerprint == viewModel.CompletedPreviewFingerprint;
                viewModel.ConfirmPublished("Evidence: verification publish recorded");
            };
            viewModel.PublishCommand.Execute(null);
            Check("publish request", publishRaised, "event raised with current fingerprint");
            Check("published state", viewModel.State == NominalActualComparisonState.Published, viewModel.State.ToString());
            Check("published fingerprint", viewModel.PublishedPreviewFingerprint == viewModel.CompletedPreviewFingerprint, viewModel.PublishedPreviewFingerprint);
            Check("published evidence", viewModel.EvidenceSummary.Contains("verification publish", StringComparison.Ordinal), viewModel.EvidenceSummary);

            var failureViewModel = new NominalActualComparisonViewModel();
            NominalActualPreviewRequestedEventArgs? failureRequest = null;
            failureViewModel.PreviewRequested += (_, args) => failureRequest = args;
            ApplyReadyInputs(failureViewModel);
            failureViewModel.PreviewCommand.Execute(null);
            Check("failure request", failureRequest is not null, "event raised");
            Check("controlled failure", failureViewModel.FailPreview(failureRequest!.RequestId, "controlled verification failure"), failureViewModel.ResultSummary);
            Check("failed state", failureViewModel.State == NominalActualComparisonState.Failed, failureViewModel.State.ToString());
            Check("failed partial result", !failureViewModel.LegendVisible && !failureViewModel.CanPublish, failureViewModel.ResultSummary);

            failureViewModel.ApplyInputValidation(
                "Actual: measured.stl",
                "Nominal: measured.stl",
                "Validation query: validation-query.ply",
                "Frame: NIST 3-2-1 | Units: mm",
                "Alignment: identity",
                "mm",
                "same-file",
                "Actual and nominal sources must be distinct.");
            Check("invalid input state", failureViewModel.State == NominalActualComparisonState.NoInputs, failureViewModel.State.ToString());
            Check("invalid input cause", failureViewModel.ValidationSummary.Contains("distinct", StringComparison.Ordinal), failureViewModel.ValidationSummary);
            Check("invalid input commands", !failureViewModel.CanPreview && !failureViewModel.CanPublish, failureViewModel.StateSummary);
            Check("property notifications", propertyChanges > 0, propertyChanges.ToString(CultureInfo.InvariantCulture));

            summary = $"Nominal/actual ViewModel verification: Pass ({passed} checks)";
            lines.Add(summary);
            WriteReport(reportPath, lines);
            return true;
        }
        catch (Exception ex)
        {
            summary = $"Nominal/actual ViewModel verification: Fail after {passed} checks: {ex.Message}";
            lines.Add(summary);
            WriteReport(reportPath, lines);
            return false;
        }

        void Check(string name, bool condition, string detail)
        {
            if (!condition)
            {
                throw new InvalidOperationException($"{name}: {detail}");
            }

            passed++;
            lines.Add($"PASS|{name}|{detail}");
        }
    }

    private static NominalActualComparisonInput ApplyReadyInputs(NominalActualComparisonViewModel viewModel)
    {
        var input = CreateInput();
        viewModel.ApplyInputValidation(
            "Actual: measured.stl",
            "Nominal: nominal.stl",
            "Validation query: validation-query.ply",
            "Frame: NIST 3-2-1 | Units: mm",
            "Alignment: Identity / source-provided",
            "mm",
            input.SourceFingerprint);
        return input;
    }

    private static NominalActualComparisonInput CreateInput() => new(
        "step.verification-surface-deviation",
        new NominalActualFileIdentity("source.actual", "Actual", "measured.stl", 100, new string('A', 64)),
        new NominalActualFileIdentity("source.nominal", "Nominal", "nominal.stl", 200, new string('B', 64)),
        new NominalActualFileIdentity("query.actual", "Query", "validation-query.ply", 300, new string('C', 64)),
        "mm",
        "frame.nist-321",
        "alignment.identity",
        -0.300,
        0.300);

    private static NominalActualComparisonResult CreateResult(NominalActualComparisonInput input) => new(
        input,
        ResultStatus.Pass,
        "Pass: 0 of 100 points outside [-0.3, 0.3] mm.",
        100,
        new NominalActualDeviationStatistics(100, 0, 0.25, 0.1, 0.05, 0.1118),
        new NominalActualDeviationStatistics(100, -0.2, 0.25, 0.01, 0.08, 0.0806),
        0,
        100,
        0,
        98,
        2,
        10,
        [new NominalActualDeviationSample(
            7,
            new Vector3(1.0f, 2.0f, 3.0f),
            new Vector3(1.0f, 2.0f, 2.75f),
            42,
            0.25,
            0.25,
            false)],
        TimeSpan.FromMilliseconds(1),
        TimeSpan.FromMilliseconds(10),
        TimeSpan.FromMilliseconds(12));

    private static void WriteReport(string reportPath, IEnumerable<string> lines)
    {
        var fullPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllLines(fullPath, lines);
    }
}
