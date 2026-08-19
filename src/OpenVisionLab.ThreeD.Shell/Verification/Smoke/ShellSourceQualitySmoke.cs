using System.Diagnostics;
using System.IO;
using System.Windows.Threading;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Shell.Verification.Smoke;

internal static class ShellSourceQualitySmoke
{
    public static async Task<bool> RunAsync(
        ToolWorkbenchViewModel workbench,
        Dispatcher dispatcher,
        string? reportPath)
    {
        var quality = workbench.SourceQuality;
        var timeout = Stopwatch.StartNew();
        while (!quality.HasReport
               && !quality.HasError
               && timeout.Elapsed < TimeSpan.FromSeconds(15))
        {
            await Task.Delay(25);
        }

        var beforeDirty = workbench.IsDirty;
        var beforeStepCount = workbench.PipelineSteps.Count;
        var beforeSelectionCount = workbench.Selections.Count;
        var beforeLogCount = workbench.RunLog.Count;
        var beforePreviewRunning = workbench.IsSelectedStepPreviewRunning;
        workbench.SelectSourceQualityCommand.Execute(null);
        await dispatcher.InvokeAsync(() => { });

        var expectedGlobalKind = quality.Report?.Coverage.MissingSampleCount > 0
            ? "Warning"
            : "Pass";
        var passed = quality.HasReport
                      && !quality.IsLoading
                      && !quality.HasError
                      && workbench.IsCurrentSourceQualityStatusVisible
                      && string.Equals(
                          workbench.CurrentSourceQualityStatusKind,
                          expectedGlobalKind,
                          StringComparison.Ordinal)
                      && !string.IsNullOrWhiteSpace(workbench.CurrentSourceQualitySummary)
                      && !string.IsNullOrWhiteSpace(workbench.CurrentSourceQualityDetail)
                      && workbench.IsSourceQualityWorkspaceVisible
                      && !workbench.HasSelectedPipelineStep
                      && workbench.IsDirty == beforeDirty
                      && workbench.PipelineSteps.Count == beforeStepCount
                      && workbench.Selections.Count == beforeSelectionCount
                      && workbench.RunLog.Count == beforeLogCount
                      && workbench.IsSelectedStepPreviewRunning == beforePreviewRunning;

        if (!string.IsNullOrWhiteSpace(reportPath))
        {
            var sourceReport = quality.Report;
            var fullReportPath = Path.GetFullPath(reportPath);
            Directory.CreateDirectory(
                Path.GetDirectoryName(fullReportPath) ?? Environment.CurrentDirectory);
            File.WriteAllLines(
                fullReportPath,
                [
                    $"SourceQualityWorkspaceSmoke|{(passed ? "Pass" : "Fail")}|viewOnly=true|recipeChanged=false|inspectionRun=false",
                    $"State|loading={quality.IsLoading}|hasReport={quality.HasReport}|hasError={quality.HasError}|visible={workbench.IsSourceQualityWorkspaceVisible}|selectedStep={workbench.SelectedPipelineStep?.Id ?? "(none)"}",
                    $"GlobalStatus|visible={workbench.IsCurrentSourceQualityStatusVisible}|kind={workbench.CurrentSourceQualityStatusKind}|summary={workbench.CurrentSourceQualitySummary}|detail={workbench.CurrentSourceQualityDetail.Replace(Environment.NewLine, " | ")}",
                    $"Source|name={quality.SourceName}|grid={sourceReport?.Grid.Width ?? 0}x{sourceReport?.Grid.Height ?? 0}|cells={sourceReport?.Grid.CellCount ?? 0}|valid={sourceReport?.Coverage.ValidSampleCount ?? 0}|validRatio={sourceReport?.Coverage.ValidRatio ?? 0:R}|missing={sourceReport?.Coverage.MissingSampleCount ?? 0}|missingRatio={sourceReport?.Coverage.MissingRatio ?? 0:R}",
                    $"Height|min={sourceReport?.Height.Minimum?.ToString("R") ?? "null"}|max={sourceReport?.Height.Maximum?.ToString("R") ?? "null"}|mean={sourceReport?.Height.Mean?.ToString("R") ?? "null"}|bins={sourceReport?.Height.Distribution?.BinCount ?? 0}|peak={sourceReport?.Height.Distribution?.PeakBinIndex ?? -1}",
                    $"Mask|bytes={sourceReport?.Coverage.InvalidCellMask.ByteLength ?? 0}|sha256={quality.MaskSha256}",
                    $"Channels|count={quality.Channels.Count}|available={string.Join(',', quality.Channels.Where(channel => channel.IsAvailable).Select(channel => channel.Name))}",
                    $"Boundary|dirty={beforeDirty}->{workbench.IsDirty}|steps={beforeStepCount}->{workbench.PipelineSteps.Count}|selections={beforeSelectionCount}->{workbench.Selections.Count}|logs={beforeLogCount}->{workbench.RunLog.Count}|previewRunning={beforePreviewRunning}->{workbench.IsSelectedStepPreviewRunning}",
                    $"Error|{quality.Error}"
                ]);
        }

        return passed;
    }
}
