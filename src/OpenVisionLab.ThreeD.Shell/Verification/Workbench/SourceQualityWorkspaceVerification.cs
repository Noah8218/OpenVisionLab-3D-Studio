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
        var passed = result.Passed + (errorViewRuntime.Passed ? 1 : 0);
        var total = result.Total + 1;
        result.Lines.Add(
            $"{(errorViewRuntime.Passed ? "PASS" : "FAIL")} | actual-wpf-error-row-resolves-semantic-trigger-and-long-binding | {errorViewRuntime.Detail}");
        var passedAll = passed == total;
        result.Lines.Add($"Result={(passedAll ? "PASS" : "FAIL")}|{passed}/{total}");
        var fullReportPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(
            Path.GetDirectoryName(fullReportPath) ?? Environment.CurrentDirectory);
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

            var sourceSession = new ToolWorkbenchSourceSession();
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

            var viewModel = new SourceQualityWorkspaceViewModel(
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

            var workbench = new ToolWorkbenchViewModel();
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
                Path.GetDirectoryName(Path.GetFullPath(reportPath))
                    ?? Environment.CurrentDirectory,
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

    private sealed record ErrorDiagnosticViewRuntimeResult(bool Passed, string Detail);
}
