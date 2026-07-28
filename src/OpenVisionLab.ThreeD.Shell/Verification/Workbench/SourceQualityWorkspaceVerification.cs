using System.IO;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Shell;

internal static class SourceQualityWorkspaceVerification
{
    private const string ExpectedMaskSha256 =
        "E55705189A5D08B23D9037386E93CAA3C6A723A3E29A83A993AEAD9908A1D68B";

    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var result = Task.Run(() => VerifyAsync(reportPath)).GetAwaiter().GetResult();
        summary = result.Summary;
        return result.Passed;
    }

    private static async Task<(bool Passed, string Summary)> VerifyAsync(
        string reportPath)
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
                "report-properties-notified",
                propertyChanges.Contains(nameof(SourceQualityWorkspaceViewModel.HasReport))
                && propertyChanges.Contains(nameof(SourceQualityWorkspaceViewModel.GridValue))
                && propertyChanges.Contains(nameof(SourceQualityWorkspaceViewModel.MaskSha256)),
                $"notifications={propertyChanges.Distinct().Count()}");

            var workbench = new ToolWorkbenchViewModel();
            workbench.SetC3DSource(sourcePath, markDirty: false);
            await workbench.SourceQuality.EnsureSourceAsync(
                sourcePath,
                workbench.Source.Id,
                workbench.Source.Unit,
                workbench.Source.FrameId);
            workbench.AddSelectedToolCommand.Execute(workbench.SelectedTool);
            var beforeDirty = workbench.IsDirty;
            var beforeSteps = workbench.PipelineSteps.Count;
            var beforeSelections = workbench.Selections.Count;
            var beforeLogs = workbench.RunLog.Count;
            var beforePreview = workbench.IsSelectedStepPreviewRunning;

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
                && workbench.SelectedWorkspaceTitle == workbench.Localization.SourceQuality,
                $"visible={workbench.IsSourceQualityWorkspaceVisible},title={workbench.SelectedWorkspaceTitle}");
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

            viewModel.Clear();
            Check(
                "clear-removes-stale-report",
                !viewModel.HasReport
                && !viewModel.IsLoading
                && !viewModel.HasError
                && viewModel.Channels.Count == 0
                && viewModel.DistributionBins.Count == 0,
                $"report={viewModel.HasReport},channels={viewModel.Channels.Count},bins={viewModel.DistributionBins.Count}");

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
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }

        var passedAll = passed == total;
        lines.Add($"Result={(passedAll ? "PASS" : "FAIL")}|{passed}/{total}");
        var fullReportPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(
            Path.GetDirectoryName(fullReportPath) ?? Environment.CurrentDirectory);
        File.WriteAllLines(fullReportPath, lines);
        return (passedAll, lines[^1]);
    }
}
