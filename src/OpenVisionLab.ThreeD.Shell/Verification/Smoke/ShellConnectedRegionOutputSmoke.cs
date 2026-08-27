using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Docking.Controls;
using OpenVisionLab.ThreeD.Shell.Coordination;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Shell.Views.Workbench;
using OpenVisionLab.ThreeD.Tools;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.Verification.Smoke;

internal static class ShellConnectedRegionOutputSmoke
{
    public static async Task<bool> RunAsync(
        Window window,
        ShellMainWindowViewModel shell,
        OpenVisionThreeDViewerControl viewer,
        ToolRecipeWorkbenchView workbenchView,
        ShellWorkbenchLifecycleController lifecycle,
        WorkbenchViewerTeachingCoordinator teaching,
        Dispatcher dispatcher,
        string sourcePath,
        string? reportPath,
        string? screenshotPath,
        string? screenshotQualityReportPath)
    {
        var lines = new List<string>
        {
            "OpenVisionLab 3D actual EXE Connected Region output smoke"
        };
        var passed = true;
        var checks = new List<string>();

        void Check(string name, bool condition, string detail)
        {
            passed &= condition;
            checks.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
        }

        try
        {
            var loaded = await lifecycle.LoadWorkbenchC3DSourceAsync(
                sourcePath,
                showFailureDialog: false);
            Check(
                "load source through the existing Viewer → Workbench lifecycle",
                loaded
                && viewer.CurrentC3DSourcePath is not null
                && workbenchView.DataContext is ShellMainWindowViewModel,
                $"loaded={loaded};viewer={viewer.CurrentC3DSourcePath};workbenchSource={shell.Workbench.Source.Path}");
            if (!loaded)
            {
                return Finish(
                    lines,
                    checks,
                    passed,
                    reportPath,
                    screenshotPath,
                    screenshotQualityReportPath,
                    "Source load failed.");
            }

            var workbench = shell.Workbench;
            var source = C3DHeightFieldSnapshot.LoadIdentified(
                sourcePath,
                workbench.Source.Id,
                workbench.Source.Unit,
                workbench.Source.FrameId);
            var mask = CreateTwoPointMask(source);
            var evaluation = await Task.Run(() => C3DConnectedRegionRule.Evaluate(
                new C3DConnectedRegionInput(
                    "derived.connected-region.smoke",
                    source.EntityId,
                    source,
                    mask,
                    C3DConnectedRegionConnectivity.Four)));
            var stepIdsBefore = workbench.PipelineSteps.Select(step => step.Id).ToArray();
            var recipeNameBefore = workbench.RecipeName;
            Check(
                "G-11 evaluation produces typed source-bound output",
                evaluation.Result.Status == ResultStatus.Pass
                && evaluation.Output is { RegionCount: 2 },
                evaluation.Result.Message);
            Check(
                "Workbench consumes evaluation without starting a recipe run",
                workbench.SetConnectedRegionPreview(evaluation, out var setMessage)
                && workbench.HasConnectedRegionOutput
                && workbench.CurrentConnectedRegionOutput is { RegionCount: 2 },
                setMessage);

            teaching.SyncAppliedSelections();
            await dispatcher.InvokeAsync(() => { }, DispatcherPriority.DataBind);
            await WaitForAsync(
                () => workbench.HeightImageViewer.HasImage,
                TimeSpan.FromSeconds(15));

            var selected = workbench.ConnectedRegionReviewItems.LastOrDefault();
            if (selected is null || workbench.CurrentConnectedRegionOutput is not { } output)
            {
                Check("review item and output are available", false, "no typed review item was created");
                return Finish(
                    lines,
                    checks,
                    passed,
                    reportPath,
                    screenshotPath,
                    screenshotQualityReportPath,
                    "No connected-region review item was created.");
            }

            workbench.SelectConnectedRegionCommand.Execute(selected);
            Check(
                "selected region state is stable across the Workbench review list",
                workbench.SelectedConnectedRegionId == selected.RegionId
                && selected.IsSelected
                && workbench.ConnectedRegionReviewItems.Count(item => item.IsSelected) == 1,
                workbench.SelectedConnectedRegionSummary);

            shell.SelectWorkspaceCommand.Execute(ShellWorkspaceMode.Teach);
            await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Loaded);
            var dock = FindVisualDescendants<OpenVisionDockWorkspaceView>(workbenchView)
                .FirstOrDefault();
            dock?.ActivateDisplayedOutputsPane();
            workbench.ShowConnectedRegionOutputCommand.Execute(selected);
            await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
            workbenchView.UpdateLayout();

            var connectedRegionItem = workbench.DisplayedOutputs.SingleOrDefault(item =>
                item.NodeKind == "ConnectedRegionOutput");
            var reviewPanel = FindVisualDescendants<FrameworkElement>(workbenchView)
                .FirstOrDefault(element =>
                    AutomationProperties.GetAutomationId(element) == "ConnectedRegionReview");
            var heightImageOverlay = FindVisualDescendants<Canvas>(workbenchView)
                .FirstOrDefault(canvas =>
                    AutomationProperties.GetAutomationId(canvas) == "HeightImageRoiOverlay");
            Check(
                "Viewer receives the same typed output and selected region",
                ReferenceEquals(viewer.ViewModel.ConnectedRegionOutput, output)
                && viewer.ViewModel.SelectedConnectedRegionId == selected.RegionId,
                $"viewerOutput={viewer.ViewModel.ConnectedRegionOutput?.OutputEntityId};viewerSelected={viewer.ViewModel.SelectedConnectedRegionId}");
            Check(
                "Height Image receives the same typed output and selected region",
                ReferenceEquals(workbench.HeightImageViewer.ConnectedRegionOutput, output)
                && workbench.HeightImageViewer.SelectedConnectedRegionId == selected.RegionId,
                $"heightOutput={workbench.HeightImageViewer.ConnectedRegionOutput?.OutputEntityId};heightSelected={workbench.HeightImageViewer.SelectedConnectedRegionId}");
            Check(
                "Displayed Outputs and review panel expose the overlay action",
                connectedRegionItem is { CanShowInViewer: true, IsShownInViewer: true }
                && reviewPanel?.Visibility == Visibility.Visible
                && workbenchView.IsDisplayedOutputsPaneSelected,
                $"itemShown={connectedRegionItem?.IsShownInViewer};review={reviewPanel?.Visibility};pane={workbenchView.IsDisplayedOutputsPaneSelected}");

            // Displayed Outputs and the viewer are separate primary-pane tabs.
            // Return to Teach before opening the real auxiliary Height Image
            // slot so the 2D canvas is materialized and can be checked.
            shell.SelectWorkspaceCommand.Execute(ShellWorkspaceMode.Review);
            shell.SelectWorkspaceCommand.Execute(ShellWorkspaceMode.Teach);
            await dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
            workbench.OpenHeightImageCommand.Execute(null);
            await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Loaded);
            await WaitForAsync(
                () => workbench.ViewerWorkspace.IsInlineSplit
                      && string.Equals(
                          workbench.ViewerWorkspace.AuxiliaryContentId,
                          ToolWorkbenchViewModel.HeightImageViewerContentId,
                          StringComparison.OrdinalIgnoreCase)
                      && workbench.HeightImageViewer.HasImage,
                TimeSpan.FromSeconds(15));
            await dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
            await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
            workbenchView.UpdateLayout();
            await WaitForAsync(
                () => FindVisualDescendants<HeightImageViewerView>(workbenchView)
                    .Any(view => view.IsVisible && view.HasNativeCoordinateImage),
                TimeSpan.FromSeconds(5));
            var heightImageViewer = FindVisualDescendants<HeightImageViewerView>(workbenchView)
                .FirstOrDefault(view => view.IsVisible);
            heightImageOverlay = heightImageViewer is null
                ? null
                : FindVisualDescendants<Canvas>(heightImageViewer)
                    .FirstOrDefault(canvas =>
                        AutomationProperties.GetAutomationId(canvas) == "HeightImageRoiOverlay");
            Check(
                "Height Image renders exact region cells",
                heightImageViewer is not null
                && heightImageViewer.HasNativeCoordinateImage
                && heightImageOverlay is not null
                && heightImageOverlay.Children.Count >= output.ForegroundCellCount,
                $"mode={shell.SelectedWorkspaceMode};operatorStage={workbenchView.OperatorStage};layout={workbench.ViewerWorkspace.Layout};auxiliary={workbench.ViewerWorkspace.AuxiliaryContentId};heightVisible={heightImageViewer?.IsVisible};nativeImage={heightImageViewer?.HasNativeCoordinateImage};overlayChildren={heightImageOverlay?.Children.Count};foregroundCells={output.ForegroundCellCount}");
            Check(
                "overlay workflow leaves recipe structure unchanged",
                workbench.PipelineSteps.Select(step => step.Id).SequenceEqual(stepIdsBefore)
                && workbench.RecipeName == recipeNameBefore
                && !workbench.HasOrderedRunResult,
                $"steps={workbench.PipelineSteps.Count};recipe={workbench.RecipeName};runResult={workbench.HasOrderedRunResult}");

            if (screenshotPath is not null)
            {
                var captured = await ShellSmokeArtifacts.CaptureWindowWithRetryAsync(
                    window,
                    screenshotPath,
                    screenshotQualityReportPath,
                    "ConnectedRegionOutput");
                Check(
                    "runtime screenshot is accepted",
                    captured,
                    screenshotPath);
            }

            lines.Add($"Source|path={Path.GetFullPath(sourcePath)}|sha256={source.ContentSha256}|grid={source.Width}x{source.Height}");
            lines.Add($"Output|entity={output.OutputEntityId}|sha256={output.ContentSha256}|regions={output.RegionCount}|foregroundCells={output.ForegroundCellCount}");
            lines.Add($"Selection|region={selected.RegionId}|cells={selected.CellCount}|selected={selected.IsSelected}");
            lines.Add($"Viewer|connectedRegionOutput={viewer.ViewModel.ConnectedRegionOutput?.OutputEntityId}|selected={viewer.ViewModel.SelectedConnectedRegionId}");
            lines.Add($"HeightImage|connectedRegionOutput={workbench.HeightImageViewer.ConnectedRegionOutput?.OutputEntityId}|selected={workbench.HeightImageViewer.SelectedConnectedRegionId}|overlayChildren={heightImageOverlay?.Children.Count ?? 0}");
            lines.Add($"Window|left={window.Left.ToString("R", CultureInfo.InvariantCulture)}|top={window.Top.ToString("R", CultureInfo.InvariantCulture)}|width={window.ActualWidth.ToString("R", CultureInfo.InvariantCulture)}|height={window.ActualHeight.ToString("R", CultureInfo.InvariantCulture)}");
        }
        catch (Exception exception)
        {
            passed = false;
            checks.Add($"FAIL | unexpected exception | {exception}");
        }

        return Finish(
            lines,
            checks,
            passed,
            reportPath,
            screenshotPath,
            screenshotQualityReportPath,
            passed ? null : "One or more Connected Region runtime checks failed.");
    }

    private static async Task<bool> WaitForAsync(
        Func<bool> predicate,
        TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (!predicate() && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(50);
        }

        return predicate();
    }

    private static C3DConnectedRegionMask CreateTwoPointMask(
        C3DHeightFieldSnapshot source)
    {
        var finiteCells = new List<C3DConnectedRegionCell>();
        var values = source.Values.Span;
        for (var row = 0; row < source.Height; row++)
        {
            for (var column = 0; column < source.Width; column++)
            {
                if (double.IsFinite(values[row * source.Width + column]))
                {
                    finiteCells.Add(new C3DConnectedRegionCell(row, column));
                }
            }
        }

        if (finiteCells.Count < 2)
        {
            throw new InvalidDataException("Connected Region smoke requires at least two finite C3D cells.");
        }

        var first = finiteCells[0];
        var second = finiteCells
            .OrderByDescending(cell =>
                Math.Abs(cell.Row - first.Row) + Math.Abs(cell.Column - first.Column))
            .First();
        if (first.Row == second.Row && first.Column == second.Column
            || Math.Abs(first.Row - second.Row) + Math.Abs(first.Column - second.Column) <= 1)
        {
            throw new InvalidDataException("Connected Region smoke could not choose two separated finite cells.");
        }

        var foreground = new bool[checked(source.Width * source.Height)];
        foreground[first.Row * source.Width + first.Column] = true;
        foreground[second.Row * source.Width + second.Column] = true;
        return new C3DConnectedRegionMask(
            "mask.connected-region-smoke",
            source.EntityId,
            source.ContentSha256,
            source.Width,
            source.Height,
            foreground);
    }

    private static bool Finish(
        List<string> lines,
        List<string> checks,
        bool passed,
        string? reportPath,
        string? screenshotPath,
        string? screenshotQualityReportPath,
        string? failure)
    {
        lines.Insert(1, $"Result: {(passed ? "Pass" : "Fail")}");
        lines.AddRange(checks);
        lines.Add($"Screenshot={screenshotPath ?? "n/a"}");
        lines.Add($"ScreenshotQualityReport={screenshotQualityReportPath ?? "n/a"}");
        if (failure is not null)
        {
            lines.Add($"Failure={failure}");
        }

        if (!string.IsNullOrWhiteSpace(reportPath))
        {
            ShellSmokeArtifacts.WriteTextReport(reportPath, lines);
        }

        return passed;
    }

    private static IEnumerable<T> FindVisualDescendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < count; index++)
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
}
