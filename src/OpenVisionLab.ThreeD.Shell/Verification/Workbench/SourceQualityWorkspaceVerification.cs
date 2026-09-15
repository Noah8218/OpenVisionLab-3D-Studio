using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Shell.Views.Workbench;
using OpenVisionLab.ThreeD.Viewer.Rendering;
using SymbolIcon = Wpf.Ui.Controls.SymbolIcon;
using SymbolRegular = Wpf.Ui.Controls.SymbolRegular;

namespace OpenVisionLab.ThreeD.Shell;

internal static class SourceQualityWorkspaceVerification
{
    private const string ExpectedMaskSha256 =
        "E55705189A5D08B23D9037386E93CAA3C6A723A3E29A83A993AEAD9908A1D68B";
    private const string LongErrorEvidence =
        "Grid has a non-finite coordinate component at the first deterministic location. "
        + "This deliberately long persisted diagnostic evidence verifies that the bound error row retains complete content for wrapping in Compact layout without trimming or a tooltip-only dependency.";

    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var result = Task.Run(VerifyAsync)
            .GetAwaiter()
            .GetResult();
        var errorViewRuntime = VerifyErrorDiagnosticViewRuntime(reportPath);
        var acquisitionFlagsRuntime = VerifyAcquisitionFlagsViewRuntime(reportPath);
        var measurementEvidenceRuntime = VerifyMeasurementEvidenceViewRuntime(reportPath);
        var passed = result.Passed
            + (errorViewRuntime.Passed ? 1 : 0)
            + (acquisitionFlagsRuntime.Passed ? 1 : 0)
            + (measurementEvidenceRuntime.Passed ? 1 : 0);
        var total = result.Total + 3;
        result.Lines.Add(
            $"{(errorViewRuntime.Passed ? "PASS" : "FAIL")} | actual-wpf-error-row-resolves-semantic-trigger-and-long-binding | {errorViewRuntime.Detail}");
        result.Lines.Add(
            $"{(acquisitionFlagsRuntime.Passed ? "PASS" : "FAIL")} | actual-wpf-acquisition-flags-render-and-two-way-bind | {acquisitionFlagsRuntime.Detail}");
        result.Lines.Add(
            $"{(measurementEvidenceRuntime.Passed ? "PASS" : "FAIL")} | actual-wpf-measurement-evidence-state-and-calibration-render | {measurementEvidenceRuntime.Detail}");
        var passedAll = passed == total;
        result.Lines.Add($"Result={(passedAll ? "PASS" : "FAIL")}|{passed}/{total}");
        var fullReportPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(
            GetReportDirectory(reportPath) ?? Environment.CurrentDirectory);
        File.WriteAllLines(fullReportPath, result.Lines);
        summary = result.Lines[^1];
        return passedAll;
    }

    private static async Task<(int Passed, int Total, List<string> Lines)> VerifyAsync()
    {
        var lines = new List<string>
        {
            "OpenVisionLab 3D Source Quality workspace verification",
            $"Generated: {DateTimeOffset.Now:O}"
        };
        var passed = 0;
        var total = 0;
        var root = Path.Combine(
            Path.GetTempPath(),
            "OpenVisionLab.ThreeD",
            nameof(SourceQualityWorkspaceVerification),
            Guid.NewGuid().ToString("N"));
        var sourcePath = Path.Combine(root, "source-quality-fixture.c3d");
        var originalLanguage = OpenVisionLanguageService.CurrentLanguage;

        void Check(string name, bool condition, string detail)
        {
            total++;
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
            if (condition)
            {
                passed++;
            }
        }

        try
        {
            Directory.CreateDirectory(root);
            var source = C3DHeightFieldSnapshot.CreateForVerification(
                "source.quality-workspace",
                4,
                3,
                [
                    1.0, 2.0, 0.0, 4.0,
                    double.NaN, 6.0, 7.0, 8.0,
                    9.0, 10.0, 11.0, 12.0
                ]);
            source.SaveC3D(sourcePath);
            await VerifyCancellationSourceLifetimeAsync(source, sourcePath, Check);
            await VerifyObservedLoadLifetimeAsync(Check);

            var streamedSnapshot = C3DHeightFieldSnapshot.LoadIdentified(
                sourcePath,
                source.EntityId,
                source.Unit,
                source.FrameId);
            Check(
                "streamed-snapshot-preserves-byte-and-grid-identity",
                streamedSnapshot.ByteLength == new FileInfo(sourcePath).Length
                && streamedSnapshot.ContentSha256 == source.ContentSha256
                && streamedSnapshot.Width == source.Width
                && streamedSnapshot.Height == source.Height,
                $"bytes={streamedSnapshot.ByteLength},sha256={streamedSnapshot.ContentSha256},grid={streamedSnapshot.Width}x{streamedSnapshot.Height}");
            Check(
                "streamed-snapshot-preserves-raw-height-values",
                streamedSnapshot.Values.Span.SequenceEqual(source.Values.Span),
                $"values={streamedSnapshot.Values.Length},valid={streamedSnapshot.ValidCount},missing={streamedSnapshot.MissingCount}");

            using var parserCancellation = new CancellationTokenSource();
            parserCancellation.Cancel();
            var parserCancelled = false;
            try
            {
                C3DHeightFieldSnapshot.LoadIdentified(
                    sourcePath,
                    source.EntityId,
                    source.Unit,
                    source.FrameId,
                    parserCancellation.Token);
            }
            catch (OperationCanceledException)
            {
                parserCancelled = true;
            }
            Check(
                "c3d-snapshot-parser-honors-pre-cancel",
                parserCancelled,
                $"cancelled={parserCancelled}");

            using var sourceSession = new ToolWorkbenchSourceSession();
            var firstSnapshotTask = sourceSession.GetOrLoadDecodedSourceAsync(
                sourcePath,
                source.EntityId,
                source.Unit,
                source.FrameId,
                CancellationToken.None);
            var secondSnapshotTask = sourceSession.GetOrLoadDecodedSourceAsync(
                sourcePath,
                source.EntityId,
                source.Unit,
                source.FrameId,
                CancellationToken.None);
            var sharedSnapshots = await Task.WhenAll(firstSnapshotTask, secondSnapshotTask);
            Check(
                "source-session-shares-one-concurrent-decoded-snapshot",
                ReferenceEquals(sharedSnapshots[0], sharedSnapshots[1]),
                $"sameReference={ReferenceEquals(sharedSnapshots[0], sharedSnapshots[1])},sha256={sharedSnapshots[0].ContentSha256}");
            sourceSession.ClearDecodedSource();
            var replacedSnapshot = await sourceSession.GetOrLoadDecodedSourceAsync(
                sourcePath,
                source.EntityId,
                source.Unit,
                source.FrameId,
                CancellationToken.None);
            Check(
                "source-session-clear-replaces-decoded-snapshot",
                !ReferenceEquals(sharedSnapshots[0], replacedSnapshot)
                && replacedSnapshot.ContentSha256 == sharedSnapshots[0].ContentSha256,
                $"sameReference={ReferenceEquals(sharedSnapshots[0], replacedSnapshot)},sha256={replacedSnapshot.ContentSha256}");

            sourceSession.SetSourceBinding(new ToolRecipeSelectionSourceBinding(
                "C3D",
                new string('0', 64),
                source.Width,
                source.Height));
            var staleBindingRejected = false;
            try
            {
                await sourceSession.GetOrLoadDecodedSourceAsync(
                    sourcePath,
                    source.EntityId,
                    source.Unit,
                    source.FrameId,
                    CancellationToken.None);
            }
            catch (InvalidDataException)
            {
                staleBindingRejected = true;
            }
            Check(
                "source-session-rejects-stale-binding-before-sharing",
                staleBindingRejected,
                $"rejected={staleBindingRejected}");
            await VerifySourceSessionCancellationRecoveryAsync(source, sourcePath, Check);
            await VerifySourceSessionDisposalAsync(source, sourcePath, Check);

            using var viewModel = new SourceQualityWorkspaceViewModel(
                ThreeDLocalization.Shared);
            Check(
                "initial-state-is-unavailable",
                !viewModel.HasReport
                && !viewModel.IsLoading
                && !viewModel.HasError,
                $"state={viewModel.State}");

            var propertyChanges = new List<string>();
            viewModel.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName is { } propertyName)
                {
                    propertyChanges.Add(propertyName);
                }
            };
            await viewModel.EnsureSourceAsync(
                sourcePath,
                source.EntityId,
                source.Unit,
                source.FrameId);

            Check(
                "report-becomes-ready",
                viewModel.HasReport
                && !viewModel.IsLoading
                && !viewModel.HasError
                && viewModel.IsAvailableOrLoading,
                $"state={viewModel.State},source={viewModel.SourceName}");
            Check(
                "native-grid-and-cell-count",
                viewModel.Report?.Grid.Width == 4
                && viewModel.Report.Grid.Height == 3
                && viewModel.Report.Grid.CellCount == 12
                && viewModel.GridValue == "4 \u00d7 3"
                && viewModel.CellCountValue == "12",
                $"grid={viewModel.GridValue},cells={viewModel.CellCountValue}");
            Check(
                "valid-and-missing-coverage",
                viewModel.Report?.Coverage.ValidSampleCount == 10
                && viewModel.Report.Coverage.MissingSampleCount == 2
                && Math.Abs(viewModel.ValidPercent - (10.0 / 12.0 * 100.0)) < 1e-9,
                $"valid={viewModel.ValidValue},missing={viewModel.MissingValue},percent={viewModel.ValidPercent:R}");
            Check(
                "height-statistics-and-distribution",
                viewModel.Report?.Height.Minimum == 1.0
                && viewModel.Report.Height.Maximum == 12.0
                && viewModel.Report.Height.Mean == 7.0
                && viewModel.DistributionBins.Count
                    == viewModel.Report.Height.Distribution?.BinCount
                && viewModel.DistributionBins.Count > 0,
                $"range={viewModel.HeightRangeValue},mean={viewModel.HeightMeanValue},distribution={viewModel.DistributionSummary}");
            Check(
                "invalid-mask-identity-visible",
                viewModel.Report?.Coverage.InvalidCellMask.ByteLength == 2
                && string.Equals(
                    viewModel.MaskSha256,
                    ExpectedMaskSha256,
                    StringComparison.OrdinalIgnoreCase)
                && viewModel.MaskSummary.Contains("2 bytes", StringComparison.Ordinal),
                $"mask={viewModel.MaskSummary},sha256={viewModel.MaskSha256}");
            Check(
                "frame-unit-and-coordinate-convention",
                viewModel.CoordinateSummary.Contains(source.FrameId, StringComparison.Ordinal)
                && viewModel.CoordinateSummary.Contains(source.Unit, StringComparison.Ordinal)
                && viewModel.CoordinateConvention == "column-rawHeight-row",
                $"coordinates={viewModel.CoordinateSummary},convention={viewModel.CoordinateConvention}");
            Check(
                "missing-measurement-evidence-is-unavailable",
                viewModel.MeasurementState == nameof(HeightMeasurementEvidenceState.Unavailable)
                && viewModel.MeasurementEvidenceSummary.Contains("No explicit", StringComparison.Ordinal)
                && viewModel.MeasurementCalibrationSummary.Contains("not asserted", StringComparison.Ordinal),
                $"state={viewModel.MeasurementState},evidence={viewModel.MeasurementEvidenceSummary},calibration={viewModel.MeasurementCalibrationSummary}");
            await viewModel.EnsureSourceAsync(
                sourcePath,
                source.EntityId,
                source.Unit,
                source.FrameId,
                HeightMeasurementEvidence.RawHeight());
            Check(
                "raw-height-measurement-state-is-visible",
                viewModel.MeasurementState == nameof(HeightMeasurementEvidenceState.RawHeight)
                && viewModel.MeasurementEvidenceSummary.Contains("raw height", StringComparison.OrdinalIgnoreCase)
                && viewModel.MeasurementCalibrationSummary.Contains("not asserted", StringComparison.Ordinal),
                $"state={viewModel.MeasurementState},evidence={viewModel.MeasurementEvidenceSummary},calibration={viewModel.MeasurementCalibrationSummary}");
            var declaredSnapshot = C3DHeightFieldSnapshot.CreateForVerification(
                "source.quality-workspace-declared",
                source.Width,
                source.Height,
                source.Values.ToArray(),
                unit: "mm",
                frameId: "frame.declared-mm");
            var declaredEvidence = HeightMeasurementEvidence.DeclaredUnit(
                "Source declares millimetres; physical calibration evidence was not supplied.");
            var declaredReport = C3DSourceQualityAnalyzer.Create(
                declaredSnapshot,
                measurementEvidence: declaredEvidence);
            viewModel.SetReportForVerification(declaredReport);
            Check(
                "declared-unit-measurement-state-is-visible",
                viewModel.MeasurementState == nameof(HeightMeasurementEvidenceState.DeclaredUnit)
                && viewModel.MeasurementEvidenceSummary == declaredEvidence.Evidence
                && viewModel.MeasurementCalibrationSummary.Contains("not asserted", StringComparison.Ordinal),
                $"state={viewModel.MeasurementState},evidence={viewModel.MeasurementEvidenceSummary},calibration={viewModel.MeasurementCalibrationSummary}");
            var calibratedEvidence = HeightMeasurementEvidence.CalibratedPhysical(
                "sensor-workspace",
                "calibration-workspace",
                declaredSnapshot.FrameId,
                "Operator supplied calibration record.",
                DateTimeOffset.UtcNow.AddHours(1));
            var calibratedReport = C3DSourceQualityAnalyzer.Create(
                declaredSnapshot,
                measurementEvidence: calibratedEvidence,
                sourceSensorId: "sensor-workspace");
            viewModel.SetReportForVerification(calibratedReport);
            Check(
                "calibrated-measurement-state-is-visible",
                viewModel.MeasurementState == nameof(HeightMeasurementEvidenceState.CalibratedPhysical)
                && viewModel.MeasurementCalibrationSummary.Contains("sensor-workspace", StringComparison.Ordinal)
                && viewModel.MeasurementCalibrationSummary.Contains("calibration-workspace", StringComparison.Ordinal)
                && viewModel.MeasurementCalibrationSummary.Contains("frame.declared-mm", StringComparison.Ordinal),
                $"state={viewModel.MeasurementState},evidence={viewModel.MeasurementEvidenceSummary},calibration={viewModel.MeasurementCalibrationSummary}");
            Check(
                "only-real-height-channel-available",
                viewModel.Channels.Count == 7
                && viewModel.Channels.Count(channel => channel.IsAvailable) == 1
                && viewModel.Channels.Single(channel => channel.IsAvailable).Name == "Height",
                $"channels={viewModel.Channels.Count},available={string.Join(',', viewModel.Channels.Where(channel => channel.IsAvailable).Select(channel => channel.Name))}");
            Check(
                "unsupported-channel-reasons-remain-visible",
                viewModel.Channels
                    .Where(channel => !channel.IsAvailable)
                    .All(channel => !string.IsNullOrWhiteSpace(channel.Evidence)),
                string.Join(';', viewModel.Channels.Where(channel => !channel.IsAvailable).Select(channel => channel.Name)));
            Check(
                "implicit-grid-diagnostics-are-visible-and-passing",
                viewModel.GridDiagnostics.Count == 4
                && viewModel.GridDiagnostics.All(item => item.IsPass && !item.IsError)
                && !viewModel.HasGridDiagnosticError,
                $"count={viewModel.GridDiagnostics.Count},state={viewModel.GridDiagnosticsState},codes={string.Join(',', viewModel.GridDiagnostics.Select(item => item.Code))}");
            Check(
                "report-properties-notified",
                propertyChanges.Contains(nameof(SourceQualityWorkspaceViewModel.HasReport))
                && propertyChanges.Contains(nameof(SourceQualityWorkspaceViewModel.GridValue))
                && propertyChanges.Contains(nameof(SourceQualityWorkspaceViewModel.MaskSha256))
                && propertyChanges.Contains(nameof(SourceQualityWorkspaceViewModel.MeasurementState))
                && propertyChanges.Contains(nameof(SourceQualityWorkspaceViewModel.MeasurementEvidenceSummary))
                && propertyChanges.Contains(nameof(SourceQualityWorkspaceViewModel.MeasurementCalibrationSummary))
                && propertyChanges.Contains(nameof(SourceQualityWorkspaceViewModel.GridDiagnosticsSummary)),
                $"notifications={propertyChanges.Distinct().Count()}");

            var explicitDiagnostics = CreateExplicitErrorDiagnostics();
            var errorReport = viewModel.Report! with { GridDiagnostics = explicitDiagnostics };
            OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
            viewModel.SetReportForVerification(errorReport);
            var englishError = viewModel.GridDiagnostics.Single(item =>
                item.Code == nameof(SourceQualityGridDiagnosticCode.CoordinateFiniteness));
            var englishTitle = englishError.Title;
            OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
            var koreanError = viewModel.GridDiagnostics.Single(item =>
                item.Code == nameof(SourceQualityGridDiagnosticCode.CoordinateFiniteness));
            Check(
                "explicit-error-diagnostic-is-localized-and-retains-long-evidence",
                viewModel.HasGridDiagnosticError
                && koreanError.IsError
                && !koreanError.IsPass
                && englishTitle == "Finite valid-cell coordinates"
                && koreanError.Title == "유한 유효 셀 좌표"
                && koreanError.State == "오류"
                && koreanError.Detail.Contains("샘플 5", StringComparison.Ordinal)
                && koreanError.Evidence.StartsWith("진단 근거:", StringComparison.Ordinal)
                && koreanError.Evidence.EndsWith(LongErrorEvidence, StringComparison.Ordinal)
                && koreanError.HasEvidence,
                $"aggregate={viewModel.GridDiagnosticsState},englishTitle={englishTitle},koreanTitle={koreanError.Title},state={koreanError.State},detail={koreanError.Detail},evidenceLength={koreanError.Evidence.Length}");
            OpenVisionLanguageService.SetLanguage(originalLanguage, save: false);

            using var workbench = new ToolWorkbenchViewModel();
            workbench.SetC3DSource(sourcePath, markDirty: false);
            await workbench.SourceQuality.EnsureSourceAsync(
                sourcePath,
                workbench.Source.Id,
                workbench.Source.Unit,
                workbench.Source.FrameId,
                cancellationToken => workbench.SourceSession.GetOrLoadDecodedSourceAsync(
                    workbench.Source.Path,
                    workbench.Source.Id,
                    workbench.Source.Unit,
                    workbench.Source.FrameId,
                    cancellationToken));
            var workbenchSnapshot = await workbench.SourceSession.GetOrLoadDecodedSourceAsync(
                workbench.Source.Path,
                workbench.Source.Id,
                workbench.Source.Unit,
                workbench.Source.FrameId,
                CancellationToken.None);
            await workbench.EnsureHeightImageSourceAsync();
            var reusedWorkbenchSnapshot = await workbench.SourceSession.GetOrLoadDecodedSourceAsync(
                workbench.Source.Path,
                workbench.Source.Id,
                workbench.Source.Unit,
                workbench.Source.FrameId,
                CancellationToken.None);
            Check(
                "workbench-quality-and-height-image-share-session-snapshot",
                ReferenceEquals(workbenchSnapshot, reusedWorkbenchSnapshot)
                && workbench.SourceQuality.Report?.Source.ContentSha256 == workbenchSnapshot.ContentSha256
                && workbench.HeightImageViewer.Frame?.SourceContentSha256 == workbenchSnapshot.ContentSha256,
                $"sameReference={ReferenceEquals(workbenchSnapshot, reusedWorkbenchSnapshot)},qualitySha={workbench.SourceQuality.Report?.Source.ContentSha256},heightImageSha={workbench.HeightImageViewer.Frame?.SourceContentSha256}");
            workbench.AddSelectedToolCommand.Execute(workbench.SelectedTool);
            var beforeDirty = workbench.IsDirty;
            var beforeSteps = workbench.PipelineSteps.Count;
            var beforeSelections = workbench.Selections.Count;
            var beforeLogs = workbench.RunLog.Count;
            var beforePreview = workbench.IsSelectedStepPreviewRunning;
            var sourceQualityWorkspaceRequestCount = 0;
            workbench.SourceQualityWorkspaceRequested += (_, _) =>
                sourceQualityWorkspaceRequestCount++;

            Check(
                "tool-step-selected-before-quality-navigation",
                workbench.HasSelectedPipelineStep
                && !workbench.IsSourceQualityWorkspaceVisible,
                $"step={workbench.SelectedPipelineStep?.Id},visible={workbench.IsSourceQualityWorkspaceVisible}");
            Check(
                "source-card-navigation-is-enabled",
                workbench.SelectSourceQualityCommand.CanExecute(null),
                $"canExecute={workbench.SelectSourceQualityCommand.CanExecute(null)}");
            workbench.SelectSourceQualityCommand.Execute(null);
            Check(
                "source-card-opens-quality-workspace",
                workbench.IsSourceQualityWorkspaceVisible
                && !workbench.HasSelectedPipelineStep
                && workbench.SelectedWorkspaceTitle == workbench.Localization.SourceQuality
                && sourceQualityWorkspaceRequestCount == 1,
                $"visible={workbench.IsSourceQualityWorkspaceVisible},title={workbench.SelectedWorkspaceTitle},requests={sourceQualityWorkspaceRequestCount}");
            Check(
                "quality-navigation-does-not-edit-recipe",
                workbench.IsDirty == beforeDirty
                && workbench.PipelineSteps.Count == beforeSteps
                && workbench.Selections.Count == beforeSelections,
                $"dirty={beforeDirty}->{workbench.IsDirty},steps={beforeSteps}->{workbench.PipelineSteps.Count},selections={beforeSelections}->{workbench.Selections.Count}");
            Check(
                "quality-navigation-does-not-execute",
                workbench.RunLog.Count == beforeLogs
                && workbench.IsSelectedStepPreviewRunning == beforePreview,
                $"logs={beforeLogs}->{workbench.RunLog.Count},preview={beforePreview}->{workbench.IsSelectedStepPreviewRunning}");
            workbench.SourceQuality.SetReportForVerification(
                workbench.SourceQuality.Report! with { GridDiagnostics = explicitDiagnostics });
            Check(
                "grid-diagnostic-error-controls-global-status",
                workbench.CurrentSourceQualityStatusKind == "Error"
                && workbench.CurrentSourceQualitySummary.Contains(
                    workbench.SourceQuality.GridDiagnosticsStatus,
                    StringComparison.Ordinal)
                && workbench.CurrentSourceQualityDetail.Contains(
                    workbench.SourceQuality.GridDiagnosticsSummary,
                    StringComparison.Ordinal),
                $"kind={workbench.CurrentSourceQualityStatusKind},summary={workbench.CurrentSourceQualitySummary},detail={workbench.CurrentSourceQualityDetail.Replace(Environment.NewLine, " | ")}");

            viewModel.Clear();
            Check(
                "clear-removes-stale-report",
                !viewModel.HasReport
                && !viewModel.IsLoading
                && !viewModel.HasError
                && viewModel.Channels.Count == 0
                && viewModel.DistributionBins.Count == 0
                && viewModel.GridDiagnostics.Count == 0,
                $"report={viewModel.HasReport},channels={viewModel.Channels.Count},bins={viewModel.DistributionBins.Count},diagnostics={viewModel.GridDiagnostics.Count}");

            await viewModel.EnsureSourceAsync(
                Path.Combine(root, "missing.c3d"),
                source.EntityId,
                source.Unit,
                source.FrameId);
            Check(
                "missing-source-fails-closed",
                !viewModel.HasReport
                && !viewModel.IsLoading
                && viewModel.HasError,
                $"state={viewModel.State},error={viewModel.Error}");

            workbench.CreateNewTeachingRecipe();
            Check(
                "new-recipe-clears-source-quality",
                string.IsNullOrWhiteSpace(workbench.Source.Path)
                && !workbench.SourceQuality.HasReport
                && !workbench.IsSourceQualityWorkspaceVisible,
                $"source={workbench.Source.Path},report={workbench.SourceQuality.HasReport},visible={workbench.IsSourceQualityWorkspaceVisible}");
        }
        catch (Exception exception)
        {
            Check(
                "unexpected-exception",
                false,
                $"{exception.GetType().Name}: {exception.Message}");
        }
        finally
        {
            OpenVisionLanguageService.SetLanguage(originalLanguage, save: false);
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }

        return (passed, total, lines);
    }

    private static async Task VerifyCancellationSourceLifetimeAsync(
        C3DHeightFieldSnapshot source,
        string sourcePath,
        Action<string, bool, string> check)
    {
        using var sourceQuality = new SourceQualityWorkspaceViewModel(
            ThreeDLocalization.Shared);
        var sourceQualityEntered = new TaskCompletionSource<CancellationToken>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var sourceQualityRelease = new TaskCompletionSource<C3DHeightFieldSnapshot>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var sourceQualityLoad = sourceQuality.EnsureSourceAsync(
            sourcePath,
            source.EntityId,
            source.Unit,
            source.FrameId,
            cancellationToken =>
            {
                sourceQualityEntered.TrySetResult(cancellationToken);
                return sourceQualityRelease.Task;
            });
        var sourceQualityToken = await sourceQualityEntered.Task;
        sourceQuality.Clear();
        var sourceQualityTokenAliveAfterCancel = IsCancellationWaitHandleAvailable(
            sourceQualityToken);
        sourceQualityRelease.TrySetResult(source);
        await sourceQualityLoad;
        var sourceQualityTokenDisposedAfterCompletion =
            !IsCancellationWaitHandleAvailable(sourceQualityToken);
        check(
            "source-quality cancellation keeps the token source alive until async completion",
            sourceQualityToken.IsCancellationRequested
            && sourceQualityTokenAliveAfterCancel
            && sourceQualityTokenDisposedAfterCompletion,
            $"cancelled={sourceQualityToken.IsCancellationRequested};aliveAfterCancel={sourceQualityTokenAliveAfterCancel};disposedAfterCompletion={sourceQualityTokenDisposedAfterCompletion}");

        using var heightImage = new HeightImageViewerViewModel(
            ThreeDLocalization.Shared,
            new SharedHeightCursorSession());
        var heightImageEntered = new TaskCompletionSource<CancellationToken>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var heightImageRelease = new TaskCompletionSource<C3DHeightFieldSnapshot>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var heightImageLoad = heightImage.EnsureSourceAsync(
            sourcePath,
            source.EntityId,
            source.Unit,
            source.FrameId,
            cancellationToken =>
            {
                heightImageEntered.TrySetResult(cancellationToken);
                return heightImageRelease.Task;
            });
        var heightImageToken = await heightImageEntered.Task;
        heightImage.ClearSource();
        var heightImageTokenAliveAfterCancel = IsCancellationWaitHandleAvailable(
            heightImageToken);
        heightImageRelease.TrySetResult(source);
        await heightImageLoad;
        var heightImageTokenDisposedAfterCompletion =
            !IsCancellationWaitHandleAvailable(heightImageToken);
        check(
            "height-image cancellation keeps the token source alive until async completion",
            heightImageToken.IsCancellationRequested
            && heightImageTokenAliveAfterCancel
            && heightImageTokenDisposedAfterCompletion,
            $"cancelled={heightImageToken.IsCancellationRequested};aliveAfterCancel={heightImageTokenAliveAfterCancel};disposedAfterCompletion={heightImageTokenDisposedAfterCompletion}");
    }

    private static async Task VerifyObservedLoadLifetimeAsync(
        Action<string, bool, string> check)
    {
        using var disposedWorkspace = new SourceQualityWorkspaceViewModel(
            ThreeDLocalization.Shared);
        var pendingFailure = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var lateFailure = new TaskCompletionSource<Exception>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var accepted = disposedWorkspace.StartObservedLoad(
            () => pendingFailure.Task,
            exception => lateFailure.TrySetResult(exception));
        var retainedBeforeDispose = disposedWorkspace.IsObservedLoadRunning;
        disposedWorkspace.Dispose();
        pendingFailure.TrySetException(
            new InvalidOperationException("late source-quality failure"));
        try
        {
            await pendingFailure.Task;
        }
        catch (InvalidOperationException)
        {
        }

        var lateFailureSuppressed = await Task.WhenAny(
                lateFailure.Task,
                Task.Delay(TimeSpan.FromMilliseconds(100)))
            != lateFailure.Task;
        check(
            "source-quality observed load suppresses late failure after disposal",
            accepted
            && retainedBeforeDispose
            && disposedWorkspace.IsDisposed
            && !disposedWorkspace.IsObservedLoadRunning
            && lateFailureSuppressed,
            $"accepted={accepted};retained={retainedBeforeDispose};disposed={disposedWorkspace.IsDisposed};lateFailureSuppressed={lateFailureSuppressed}");

        using var reusableWorkspace = new SourceQualityWorkspaceViewModel(
            ThreeDLocalization.Shared);
        var firstCompletion = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var firstAccepted = reusableWorkspace.StartObservedLoad(
            () => firstCompletion.Task,
            _ => { });
        var retainedFirstLoad = reusableWorkspace.IsObservedLoadRunning;
        firstCompletion.TrySetResult(null);
        await firstCompletion.Task;
        var firstReleased = await WaitUntilAsync(
            () => !reusableWorkspace.IsObservedLoadRunning,
            TimeSpan.FromSeconds(5));
        var secondCompletion = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var secondAccepted = reusableWorkspace.StartObservedLoad(
            () => secondCompletion.Task,
            _ => { });
        var retainedSecondLoad = reusableWorkspace.IsObservedLoadRunning;
        secondCompletion.TrySetResult(null);
        await secondCompletion.Task;
        var secondReleased = await WaitUntilAsync(
            () => !reusableWorkspace.IsObservedLoadRunning,
            TimeSpan.FromSeconds(5));
        check(
            "source-quality observed load releases and reuses the owner slot",
            firstAccepted
            && retainedFirstLoad
            && firstReleased
            && secondAccepted
            && retainedSecondLoad
            && secondReleased,
            $"firstAccepted={firstAccepted};firstRetained={retainedFirstLoad};firstReleased={firstReleased};secondAccepted={secondAccepted};secondRetained={retainedSecondLoad};secondReleased={secondReleased}");
    }

    private static async Task<bool> WaitUntilAsync(
        Func<bool> predicate,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (predicate())
            {
                return true;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(10));
        }

        return predicate();
    }

    private static async Task VerifySourceSessionDisposalAsync(
        C3DHeightFieldSnapshot source,
        string sourcePath,
        Action<string, bool, string> check)
    {
        using var sourceSession = new ToolWorkbenchSourceSession();
        var entered = new TaskCompletionSource<CancellationToken>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var pendingLoad = sourceSession.GetOrLoadDecodedSourceAsync(
            sourcePath,
            source.EntityId,
            source.Unit,
            source.FrameId,
            CancellationToken.None,
            cancellationToken =>
            {
                entered.TrySetResult(cancellationToken);
                return Task.Delay(Timeout.Infinite, cancellationToken)
                    .ContinueWith(
                        _ => source,
                        CancellationToken.None,
                        TaskContinuationOptions.OnlyOnRanToCompletion,
                        TaskScheduler.Default);
            });
        var sessionToken = await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        sourceSession.Dispose();

        var cancelled = false;
        try
        {
            await pendingLoad;
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }

        check(
            "source-session-dispose-cancels-pending-decode-and-releases-token",
            cancelled
            && sessionToken.IsCancellationRequested
            && !IsCancellationWaitHandleAvailable(sessionToken),
            $"cancelled={cancelled};requested={sessionToken.IsCancellationRequested};disposed={!IsCancellationWaitHandleAvailable(sessionToken)}");

        var rejectedAfterDispose = false;
        try
        {
            await sourceSession.GetOrLoadDecodedSourceAsync(
                sourcePath,
                source.EntityId,
                source.Unit,
                source.FrameId,
                CancellationToken.None);
        }
        catch (ObjectDisposedException)
        {
            rejectedAfterDispose = true;
        }

        check(
            "source-session-rejects-new-decode-after-dispose",
            rejectedAfterDispose,
            $"rejected={rejectedAfterDispose}");
    }

    private static async Task VerifySourceSessionCancellationRecoveryAsync(
        C3DHeightFieldSnapshot source,
        string sourcePath,
        Action<string, bool, string> check)
    {
        using var sourceSession = new ToolWorkbenchSourceSession();
        var loadCount = 0;
        Task<C3DHeightFieldSnapshot> LoadWithOneCancelledAttempt(CancellationToken _)
        {
            if (Interlocked.Increment(ref loadCount) == 1)
            {
                return Task.FromCanceled<C3DHeightFieldSnapshot>(new CancellationToken(true));
            }

            return Task.FromResult(source);
        }

        var firstAttemptCancelled = false;
        try
        {
            await sourceSession.GetOrLoadDecodedSourceAsync(
                sourcePath,
                source.EntityId,
                source.Unit,
                source.FrameId,
                CancellationToken.None,
                LoadWithOneCancelledAttempt);
        }
        catch (OperationCanceledException)
        {
            firstAttemptCancelled = true;
        }

        var recovered = await sourceSession.GetOrLoadDecodedSourceAsync(
            sourcePath,
            source.EntityId,
            source.Unit,
            source.FrameId,
            CancellationToken.None,
            LoadWithOneCancelledAttempt);
        check(
            "source-session-recovers-after-cancelled-decode",
            firstAttemptCancelled
            && loadCount == 2
            && ReferenceEquals(recovered, source),
            $"firstCancelled={firstAttemptCancelled};loadCount={loadCount};sameReference={ReferenceEquals(recovered, source)}");
    }

    private static bool IsCancellationWaitHandleAvailable(CancellationToken token)
    {
        try
        {
            return !token.WaitHandle.SafeWaitHandle.IsClosed;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }

    private static SourceQualityGridDiagnostics CreateExplicitErrorDiagnostics()
    {
        var diagnostics = SourceQualityGridDiagnosticsAnalyzer.AnalyzeExplicit(
            4,
            3,
            [
                new(0, 0, 0, 0, 1), new(0, 1, 1, 0, 2),
                new(0, 2, 2, 0, 3), new(0, 3, 3, 0, 4),
                new(1, 1, 1, 1, 5), new(1, 1, double.NaN, 1, 6),
                new(1, 0, 0, 1, 7), new(1, 3, 3, 1, 8),
                new(2, 0, 0, 2, 9), new(2, 1, 1, 2, 10),
                new(2, 2, 2, 2, 11), new(2, 3, 3, 2, 12)
            ]);
        return diagnostics with
        {
            Checks = diagnostics.Checks.Select(check =>
                check.Code == SourceQualityGridDiagnosticCode.CoordinateFiniteness
                    ? check with { Message = LongErrorEvidence }
                    : check).ToArray()
        };
    }

    private static ErrorDiagnosticViewRuntimeResult VerifyErrorDiagnosticViewRuntime(
        string reportPath)
    {
        if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
        {
            return new(
                false,
                $"apartment={Thread.CurrentThread.GetApartmentState()}|expected=STA");
        }

        var originalLanguage = OpenVisionLanguageService.CurrentLanguage;
        var application = Application.Current;
        if (application is null)
        {
            return new(false, "application=null");
        }

        var originalShutdownMode = application.ShutdownMode;
        Window? host = null;
        try
        {
            application.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
            var snapshot = C3DHeightFieldSnapshot.CreateForVerification(
                "source.quality-error-runtime",
                4,
                3,
                Enumerable.Range(1, 12).Select(value => (double)value).ToArray());
            var errorReport = C3DSourceQualityAnalyzer.Create(snapshot) with
            {
                GridDiagnostics = CreateExplicitErrorDiagnostics()
            };
            var viewModel = new SourceQualityWorkspaceViewModel(ThreeDLocalization.Shared);
            viewModel.SetReportForVerification(errorReport);
            var view = new SourceQualityWorkspaceView
            {
                Width = 280,
                Height = 760,
                DataContext = viewModel
            };
            host = new Window
            {
                Width = 280,
                Height = 760,
                Left = SystemParameters.VirtualScreenLeft,
                Top = SystemParameters.VirtualScreenTop,
                Content = view,
                Opacity = 0.01,
                ShowActivated = false,
                ShowInTaskbar = false,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.None
            };
            host.Show();
            host.UpdateLayout();
            view.UpdateLayout();

            var errorRow = FindVisualDescendants<Border>(view).SingleOrDefault(border =>
                AutomationProperties.GetAutomationId(border)
                == "SourceQualityGridDiagnostic.CoordinateFiniteness");
            if (errorRow is null)
            {
                return new(false, "errorRow=null");
            }

            var texts = FindVisualDescendants<TextBlock>(errorRow).ToArray();
            var icon = FindVisualDescendants<SymbolIcon>(errorRow).SingleOrDefault();
            var stateText = texts.SingleOrDefault(text => text.Text == "오류");
            var evidenceText = texts.SingleOrDefault(text =>
                text.Text.EndsWith(LongErrorEvidence, StringComparison.Ordinal));
            var expectedSurface = view.FindResource("ThreeD.FailSurfaceBrush") as Brush;
            var expectedFail = view.FindResource("ThreeD.FailBrush") as Brush;
            var screenshotPath = Path.Combine(
                GetReportDirectory(reportPath) ?? Environment.CurrentDirectory,
                "error-state",
                "coordinate-finiteness-error.png");
            var capture = WpfScreenshotCapture.Capture(errorRow);
            WpfScreenshotCapture.Save(capture.Bitmap, screenshotPath);

            var passed = errorRow.IsVisible
                && errorRow.ActualWidth > 0
                && errorRow.ActualHeight > 0
                && BrushesMatch(errorRow.Background, expectedSurface)
                && BrushesMatch(errorRow.BorderBrush, expectedFail)
                && icon is
                {
                    IsVisible: true,
                    Symbol: SymbolRegular.ErrorCircle24
                }
                && BrushesMatch(icon.Foreground, expectedFail)
                && stateText is { IsVisible: true }
                && BrushesMatch(stateText.Foreground, expectedFail)
                && evidenceText is
                {
                    IsVisible: true,
                    TextWrapping: TextWrapping.Wrap
                }
                && evidenceText.Text.StartsWith("진단 근거:", StringComparison.Ordinal)
                && evidenceText.ActualWidth > 0
                && evidenceText.ActualHeight > 0;
            return new(
                passed,
                $"apartment=STA|visible={errorRow.IsVisible}|size={errorRow.ActualWidth:0.###}x{errorRow.ActualHeight:0.###}|background={DescribeBrush(errorRow.Background)}|border={DescribeBrush(errorRow.BorderBrush)}|icon={icon?.Symbol}|iconBrush={DescribeBrush(icon?.Foreground)}|state={stateText?.Text}|stateBrush={DescribeBrush(stateText?.Foreground)}|evidenceLength={evidenceText?.Text.Length ?? 0}|wrap={evidenceText?.TextWrapping}|evidenceSize={evidenceText?.ActualWidth:0.###}x{evidenceText?.ActualHeight:0.###}|screenshot={screenshotPath}");
        }
        catch (Exception exception)
        {
            return new(false, $"{exception.GetType().Name}: {exception.Message}");
        }
        finally
        {
            host?.Close();
            application.ShutdownMode = originalShutdownMode;
            OpenVisionLanguageService.SetLanguage(originalLanguage, save: false);
        }
    }

    private static ErrorDiagnosticViewRuntimeResult VerifyAcquisitionFlagsViewRuntime(
        string reportPath)
    {
        if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
        {
            return new(
                false,
                $"apartment={Thread.CurrentThread.GetApartmentState()}|expected=STA");
        }

        var originalLanguage = OpenVisionLanguageService.CurrentLanguage;
        var application = Application.Current;
        if (application is null)
        {
            return new(false, "application=null");
        }

        var originalShutdownMode = application.ShutdownMode;
        Window? host = null;
        try
        {
            application.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
            var viewModel = new SourceQualityWorkspaceViewModel(ThreeDLocalization.Shared);
            viewModel.LoadAcquisitionProvenance(
                new ToolRecipeAcquisitionProvenance(
                    ToolRecipeAcquisitionProvenanceState.Available,
                    "Operator-confirmed reflective source with explicit coverage limitation.",
                    "Reflective surface; low coverage is retained as an operator limitation.",
                    ToolRecipeAcquisitionDirection.CreateUnavailable("frame.c3d-grid-index"),
                    [
                        new(
                            ToolRecipeAcquisitionLimitationKind.Reflective,
                            ToolRecipeAcquisitionLimitationOrigin.OperatorAuthored),
                        new(
                            ToolRecipeAcquisitionLimitationKind.LowCoverage,
                            ToolRecipeAcquisitionLimitationOrigin.Imported)
                    ]),
                "frame.c3d-grid-index");
            var view = new SourceQualityWorkspaceView
            {
                Width = 520,
                Height = 900,
                DataContext = viewModel
            };
            host = new Window
            {
                Width = 520,
                Height = 900,
                Left = SystemParameters.VirtualScreenLeft,
                Top = SystemParameters.VirtualScreenTop,
                Content = view,
                Opacity = 0.01,
                ShowActivated = false,
                ShowInTaskbar = false,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.None
            };
            host.Show();
            host.UpdateLayout();
            view.UpdateLayout();

            var checkBoxes = FindVisualDescendants<CheckBox>(view)
                .Where(checkBox => !string.IsNullOrWhiteSpace(
                    AutomationProperties.GetAutomationId(checkBox)))
                .ToDictionary(
                    checkBox => AutomationProperties.GetAutomationId(checkBox),
                    StringComparer.Ordinal);
            var flagIds = new[]
            {
                "SourceAcquisitionFlagReflective",
                "SourceAcquisitionFlagTransparent",
                "SourceAcquisitionFlagTextureless",
                "SourceAcquisitionFlagClipped",
                "SourceAcquisitionFlagLowCoverage"
            };
            var allRendered = flagIds.All(checkBoxes.ContainsKey)
                && checkBoxes.Values.All(checkBox =>
                    checkBox.IsVisible
                    && checkBox.ActualWidth > 0
                    && checkBox.ActualHeight > 0);
            var initialBinding = checkBoxes["SourceAcquisitionFlagReflective"].IsChecked == true
                && checkBoxes["SourceAcquisitionFlagLowCoverage"].IsChecked == true
                && checkBoxes["SourceAcquisitionFlagTransparent"].IsChecked != true
                && checkBoxes["SourceAcquisitionFlagTextureless"].IsChecked != true
                && checkBoxes["SourceAcquisitionFlagClipped"].IsChecked != true;

            checkBoxes["SourceAcquisitionFlagReflective"].IsChecked = false;
            checkBoxes["SourceAcquisitionFlagTransparent"].IsChecked = true;
            checkBoxes["SourceAcquisitionFlagLowCoverage"].IsChecked = false;
            view.UpdateLayout();
            var twoWayBinding = !viewModel.IsAcquisitionReflectiveFlagDraft
                && viewModel.IsAcquisitionTransparentFlagDraft
                && !viewModel.IsAcquisitionLowCoverageFlagDraft
                && viewModel.HasPendingAcquisitionProvenanceChanges;

            var flagsPanel = FindVisualDescendants<WrapPanel>(view).SingleOrDefault(panel =>
                AutomationProperties.GetAutomationId(panel)
                == "SourceAcquisitionLimitationFlags");
            var provenanceEditor = FindVisualDescendants<Border>(view).SingleOrDefault(border =>
                AutomationProperties.GetAutomationId(border)
                == "SourceAcquisitionProvenanceEditor");
            var screenshotPath = Path.Combine(
                GetReportDirectory(reportPath) ?? Environment.CurrentDirectory,
                "acquisition-flags",
                "source-acquisition-flags.png");
            if (provenanceEditor is not null)
            {
                WpfScreenshotCapture.Save(
                    WpfScreenshotCapture.Capture(provenanceEditor).Bitmap,
                    screenshotPath);
            }

            var passed = allRendered
                && initialBinding
                && twoWayBinding
                && flagsPanel is not null
                && provenanceEditor is not null;
            return new(
                passed,
                $"apartment=STA|rendered={allRendered}|initial={initialBinding}|twoWay={twoWayBinding}|panel={flagsPanel is not null}|editor={provenanceEditor is not null}|screenshot={screenshotPath}");
        }
        catch (Exception exception)
        {
            return new(false, $"{exception.GetType().Name}: {exception.Message}");
        }
        finally
        {
            host?.Close();
            application.ShutdownMode = originalShutdownMode;
            OpenVisionLanguageService.SetLanguage(originalLanguage, save: false);
        }
    }

    private static ErrorDiagnosticViewRuntimeResult VerifyMeasurementEvidenceViewRuntime(
        string reportPath)
    {
        if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
        {
            return new(
                false,
                $"apartment={Thread.CurrentThread.GetApartmentState()}|expected=STA");
        }

        var originalLanguage = OpenVisionLanguageService.CurrentLanguage;
        var application = Application.Current;
        if (application is null)
        {
            return new(false, "application=null");
        }

        var originalShutdownMode = application.ShutdownMode;
        Window? host = null;
        try
        {
            application.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
            var snapshot = C3DHeightFieldSnapshot.CreateForVerification(
                "source.quality-measurement-runtime",
                2,
                2,
                [1.0, 2.0, 3.0, 4.0],
                unit: "mm",
                frameId: "frame.runtime-calibrated");
            var evidence = HeightMeasurementEvidence.CalibratedPhysical(
                "sensor-runtime",
                "calibration-runtime",
                snapshot.FrameId,
                "Runtime calibration evidence is explicitly supplied.",
                DateTimeOffset.UtcNow.AddHours(1));
            var report = C3DSourceQualityAnalyzer.Create(
                snapshot,
                measurementEvidence: evidence,
                sourceSensorId: "sensor-runtime");
            var viewModel = new SourceQualityWorkspaceViewModel(ThreeDLocalization.Shared);
            viewModel.SetReportForVerification(report);
            var view = new SourceQualityWorkspaceView
            {
                Width = 520,
                Height = 900,
                DataContext = viewModel
            };
            host = new Window
            {
                Width = 520,
                Height = 900,
                Left = SystemParameters.VirtualScreenLeft,
                Top = SystemParameters.VirtualScreenTop,
                Content = view,
                Opacity = 0.01,
                ShowActivated = false,
                ShowInTaskbar = false,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.None
            };
            host.Show();
            host.UpdateLayout();
            foreach (var expander in FindVisualDescendants<Expander>(view))
            {
                expander.IsExpanded = true;
                expander.ApplyTemplate();
                expander.UpdateLayout();
            }
            view.UpdateLayout();

            var state = FindVisualDescendants<TextBlock>(view).SingleOrDefault(text =>
                AutomationProperties.GetAutomationId(text) == "SourceQualityMeasurementState");
            var measurement = FindVisualDescendants<TextBlock>(view).SingleOrDefault(text =>
                AutomationProperties.GetAutomationId(text) == "SourceQualityMeasurementEvidence");
            var calibration = FindVisualDescendants<TextBlock>(view).SingleOrDefault(text =>
                AutomationProperties.GetAutomationId(text) == "SourceQualityMeasurementCalibration");
            var screenshotPath = Path.Combine(
                GetReportDirectory(reportPath) ?? Environment.CurrentDirectory,
                "measurement-evidence",
                "calibrated-state.png");
            if (calibration is not null)
            {
                WpfScreenshotCapture.Save(
                    WpfScreenshotCapture.Capture(calibration).Bitmap,
                    screenshotPath);
            }

            var passed = state is
                {
                    IsVisible: true,
                    ActualWidth: > 0,
                    ActualHeight: > 0,
                    Text: nameof(HeightMeasurementEvidenceState.CalibratedPhysical)
                }
                && measurement is
                {
                    IsVisible: true,
                    ActualWidth: > 0,
                    ActualHeight: > 0,
                    TextWrapping: TextWrapping.Wrap
                }
                && measurement.Text.Contains("explicitly supplied", StringComparison.Ordinal)
                && calibration is
                {
                    IsVisible: true,
                    ActualWidth: > 0,
                    ActualHeight: > 0,
                    TextWrapping: TextWrapping.Wrap
                }
                && calibration.Text.Contains("sensor-runtime", StringComparison.Ordinal)
                && calibration.Text.Contains("calibration-runtime", StringComparison.Ordinal)
                && calibration.Text.Contains("frame.runtime-calibrated", StringComparison.Ordinal);
            return new(
                passed,
                $"apartment=STA|state={state?.Text}|stateSize={state?.ActualWidth:0.###}x{state?.ActualHeight:0.###}|evidenceVisible={measurement?.IsVisible}|evidenceWrap={measurement?.TextWrapping}|calibrationVisible={calibration?.IsVisible}|calibrationWrap={calibration?.TextWrapping}|calibrationSize={calibration?.ActualWidth:0.###}x{calibration?.ActualHeight:0.###}|screenshot={screenshotPath}");
        }
        catch (Exception exception)
        {
            return new(false, $"{exception.GetType().Name}: {exception.Message}");
        }
        finally
        {
            host?.Close();
            application.ShutdownMode = originalShutdownMode;
            OpenVisionLanguageService.SetLanguage(originalLanguage, save: false);
        }
    }

    private static bool BrushesMatch(Brush? actual, Brush? expected) =>
        actual is SolidColorBrush actualSolid
        && expected is SolidColorBrush expectedSolid
        && actualSolid.Color == expectedSolid.Color;

    private static string DescribeBrush(Brush? brush) =>
        brush is SolidColorBrush solid ? solid.Color.ToString() : brush?.ToString() ?? "null";

    private static IEnumerable<T> FindVisualDescendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
            {
                yield return match;
            }

            foreach (var descendant in FindVisualDescendants<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private static string? GetReportDirectory(string reportPath) =>
        Path.GetDirectoryName(Path.GetFullPath(reportPath));

    private sealed record ErrorDiagnosticViewRuntimeResult(bool Passed, string Detail);
}
