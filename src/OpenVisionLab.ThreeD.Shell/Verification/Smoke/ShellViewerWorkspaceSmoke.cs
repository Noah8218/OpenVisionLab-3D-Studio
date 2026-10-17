using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Text.Json;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Shell.Views.Workbench;
using OpenVisionLab.ThreeD.Viewer.Hosting;

namespace OpenVisionLab.ThreeD.Shell.Verification.Smoke;

/// <summary>
/// Owns command-line Viewer workspace presentation/layout Smoke orchestration.
/// MainWindow retains scenario selection and failure/shutdown policy; the
/// Workbench remains the owner of ViewerWorkspace state and transitions; the
/// View supplies its existing explicit smoke adapters.
/// </summary>
internal sealed class ShellViewerWorkspaceSmoke
{
    private readonly ToolRecipeWorkbenchView workbenchView;
    private readonly Dispatcher dispatcher;

    public ShellViewerWorkspaceSmoke(
        ToolRecipeWorkbenchView workbenchView,
        Dispatcher dispatcher)
    {
        this.workbenchView = workbenchView ?? throw new ArgumentNullException(nameof(workbenchView));
        this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        AttachSectionPointerEvidence();
        AttachSectionConnectionEvidence();
    }

    public async Task<ShellViewerWorkspaceSmokeResult> RunAsync(
        bool presentationSmoke,
        string? layoutSmoke,
        string? screenshotQualityReportPath)
    {
        if (presentationSmoke)
        {
            if (!workbenchView.ConfigureViewerWorkspacePresentationForSmoke())
            {
                return Failure(
                    "Viewer workspace presentation smoke could not activate two real linked Viewers.");
            }

            await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
            await Task.Delay(600);
            if (!workbenchView.VerifyViewerWorkspaceCameraLinkForSmoke(
                    out var cameraLinkSmokeSummary))
            {
                ShellSmokeArtifacts.WriteTextReport(
                    screenshotQualityReportPath,
                    [cameraLinkSmokeSummary]);
                return Failure(
                    "Viewer workspace camera-link propagation smoke failed.");
            }

            return Success(cameraLinkSmokeSummary);
        }

        if (layoutSmoke is not null)
        {
            var section = string.Equals(layoutSmoke, "section", StringComparison.OrdinalIgnoreCase);
            var mainHeight = string.Equals(layoutSmoke, "main-height", StringComparison.OrdinalIgnoreCase);
            var teachingResults = layoutSmoke is "results" or "gallery";
            if (!workbenchView.ConfigureViewerWorkspaceLayoutForSmoke(section || mainHeight || teachingResults ? "single" : layoutSmoke))
            {
                return Failure(
                    $"Viewer workspace layout smoke could not activate '{layoutSmoke}'.");
            }

            await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
            if (!teachingResults && workbenchView.ViewerContent is IOpenVisionThreeDViewerHost fitHost)
                fitHost.FitAll();
            if (teachingResults)
            {
                if (workbenchView.DataContext is not ShellMainWindowViewModel resultShell
                    || !resultShell.IsAuthoringWorkspaceSelected || !resultShell.Workbench.RunTeachingRecipeCommand.CanExecute(null))
                    return Failure("Teaching results fixture requires a ready recipe in Teach.");
                var dirty = resultShell.Workbench.IsDirty;
                resultShell.Workbench.RunTeachingRecipeCommand.Execute(null);
                var deadline = DateTime.UtcNow.AddSeconds(30);
                while (resultShell.Workbench.IsOrderedRunRunning && DateTime.UtcNow < deadline) await Task.Delay(50);
                if (resultShell.Workbench.IsOrderedRunRunning || !resultShell.Workbench.HasTeachingResults || resultShell.Workbench.IsDirty != dirty)
                    return Failure("Teaching results fixture did not finish or changed the recipe.");
                resultShell.ResultsWorkspace.SelectSectionCommand.Execute("RunRecord");
                workbenchView.IsTeachingDataExpanded = layoutSmoke == "gallery";
                if (!string.IsNullOrWhiteSpace(screenshotQualityReportPath))
                    ShellSmokeArtifacts.WriteTextReport(Path.ChangeExtension(screenshotQualityReportPath, ".results-state.txt"),
                        [$"Actual Shell CLI fixture, not Computer Use|explicitRunRequests=1|rows={resultShell.Workbench.TeachingResults.Count}|gallery={workbenchView.IsTeachingDataExpanded}|recipeDirtyUnchanged=True|source={resultShell.Workbench.HeightImageViewer.Frame?.SourceContentSha256}"]);
            }
            if (section || mainHeight)
            {
                if (workbenchView.DataContext is not ShellMainWindowViewModel shell
                    || workbenchView.ViewerContent is not IOpenVisionThreeDViewerHost host)
                    return Failure("Section fixture requires the real Shell C3D input and Viewer host.");
                shell.Workbench.MainViewerPane.SetKindCommand.Execute(ViewerPaneKind.Height);
                await shell.Workbench.MainViewerPane.HeightImage.ObservedLoadTask;
                if (shell.Workbench.MainViewerPane.HeightImage.Frame is not { } frame)
                    return Failure("Section fixture requires a loaded native height frame.");
                var inspection = host.HostState.Inspection;
                var dirty = shell.Workbench.IsDirty;
                if (mainHeight)
                {
                    if (!host.TrySetSelectionMode("Move")) return Failure("Main Height comparison could not select the existing Move command.");
                    if (!string.IsNullOrWhiteSpace(screenshotQualityReportPath))
                        ShellSmokeArtifacts.WriteTextReport(Path.ChangeExtension(screenshotQualityReportPath, ".height-state.txt"),
                            [$"Actual Shell Main Height CLI fixture, not Computer Use|source={frame.SourceContentSha256}|size={frame.Width}x{frame.Height}|inspectionUnchanged=True|dirtyUnchanged=True"]);
                    await Task.Delay(600);
                    return Success(null);
                }
                var htmlComparison = Environment.GetEnvironmentVariable("OVL3D_SECTION_PARITY_CAPTURE") == "html-v3";
                if (htmlComparison && (frame.Width != 1280 || frame.Height != 840))
                    return Failure("HTML Section comparison requires the owned 1280x840 coupon input.");
                if (!host.TryRequestHeightProfile(ViewerHeightProfileAction.Begin)) return Failure("Section fixture could not begin the existing profile command.");
                var row = htmlComparison ? 228 : Math.Clamp(frame.Height / 3, 0, frame.Height - 1);
                var first = htmlComparison ? 694 : Math.Clamp(frame.Width / 20, 0, frame.Width - 1);
                var second = htmlComparison ? 892 : Math.Clamp(frame.Width * 19 / 20, 0, frame.Width - 1);
                var source = host.HostState.Profile.SourceSha256;
                if (!host.TryPickHeightProfileCell(new ViewerHeightProfileCell(source, row, first))
                    || !host.TryPickHeightProfileCell(new ViewerHeightProfileCell(source, row, second)))
                    return Failure("Section fixture could not pick two valid distinct cells from the actual input.");
                shell.Workbench.MainViewerPane.SetKindCommand.Execute(htmlComparison ? ViewerPaneKind.ThreeD : ViewerPaneKind.Section);
                if (htmlComparison)
                {
                    var layout = Window.GetWindow(workbenchView)?.ActualWidth >= 1500 ? "vertical" : "single";
                    if (!workbenchView.ConfigureViewerWorkspaceLayoutForSmoke(layout)) return Failure("HTML Section comparison could not match the Viewer layout.");
                    workbenchView.IsTeachingDataExpanded = true;
                }
                shell.ResultsWorkspace.SelectSectionCommand.Execute("Section");
                if (!host.HostState.Profile.HasEndpoints || host.HostState.Inspection != inspection || shell.Workbench.IsDirty != dirty)
                    return Failure("Section fixture changed inspection/recipe state or did not retain its endpoints.");
                if (!string.IsNullOrWhiteSpace(screenshotQualityReportPath))
                    ShellSmokeArtifacts.WriteTextReport(Path.ChangeExtension(screenshotQualityReportPath, ".section-state.txt"),
                        [$"Actual Shell CLI fixture, not Computer Use|source={source}|A={row},{first}|B={row},{second}|distance={host.HostState.Profile.GridDistanceCells:R}|min={host.HostState.Profile.MinimumRaw:R}|max={host.HostState.Profile.MaximumRaw:R}|valid={host.HostState.Profile.ValidSampleCount}|missing={host.HostState.Profile.MissingSampleCount}|htmlComparison={htmlComparison}|mainKind={shell.Workbench.MainViewerPane.Kind}|dataExpanded={workbenchView.IsTeachingDataExpanded}|inspectionUnchanged=True|dirtyUnchanged=True"]);
                await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
            }
            await Task.Delay(600);
            if (section && Environment.GetEnvironmentVariable("OVL3D_SECTION_PARITY_CAPTURE") == "html-v3")
            {
                if (workbenchView.DataContext is not ShellMainWindowViewModel shell
                    || workbenchView.ViewerContent is not IOpenVisionThreeDViewerHost host)
                    return Failure("Section layout diagnostics require the actual Shell and Viewer host.");
                var profileBefore = host.HostState.Profile;
                var inspectionBefore = host.HostState.Inspection;
                var dirtyBefore = shell.Workbench.IsDirty;
                var inline = ShellSmokeArtifacts.FindVisualDescendants<HeightProfileView>(workbenchView).Single(view => view.Name == "InlineHeightProfile");
                var results = ShellSmokeArtifacts.FindVisualDescendants<ResultsWorkspaceView>(workbenchView).Single();
                var drawer = (HeightProfileView)results.FindName("DrawerHeightProfile");
                if (inline.IsVisible || !drawer.IsVisible) return Failure("Section drawer must own the visible profile while it is selected.");
                shell.ResultsWorkspace.SelectSectionCommand.Execute("RunRecord");
                await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
                if (!inline.IsVisible || results.MinHeight != 0) return Failure("Leaving Section did not restore the existing inline profile and Results height.");
                shell.ResultsWorkspace.SelectSectionCommand.Execute("Section");
                await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
                if (inline.IsVisible || !drawer.IsVisible || host.HostState.Profile.EndpointSummary != profileBefore.EndpointSummary
                    || host.HostState.Inspection != inspectionBefore || shell.Workbench.IsDirty != dirtyBefore)
                    return Failure("Section visibility round trip changed measurement, inspection or recipe state.");
                var nativePane = ShellSmokeArtifacts.FindVisualDescendants<AvalonDock.Controls.LayoutAnchorablePaneControl>(workbenchView)
                    .Single(pane => ShellSmokeArtifacts.FindVisualDescendants<ResultsWorkspaceView>(pane).Contains(results));
                var resultBounds = results.TransformToAncestor(nativePane).TransformBounds(new Rect(results.RenderSize));
                var workbenchBounds = results.TransformToAncestor(workbenchView).TransformBounds(new Rect(results.RenderSize));
                if (resultBounds.Bottom > nativePane.ActualHeight + 1 || resultBounds.Top < -1
                    || workbenchBounds.Bottom > workbenchView.ActualHeight + 1)
                    return Failure($"Section is clipped by its actual parent: results={resultBounds};parent={nativePane.ActualHeight:R};workbench={workbenchBounds}/{workbenchView.ActualHeight:R}.");
                if (!string.IsNullOrWhiteSpace(screenshotQualityReportPath))
                {
                    var header = (Border)results.FindName("ResultsSectionNavigation");
                    var canvas = (Canvas)drawer.FindName("ProfileChartCanvas");
                    var toolbar = (StackPanel)results.FindName("DrawerProfileToolbar");
                    var lines = new List<string> { $"Actual Shell CLI fixture; not Computer Use|results={results.ActualWidth:R}x{results.ActualHeight:R}|minimum={results.MinHeight}|header={header.ActualHeight:R}|drawer={drawer.ActualWidth:R}x{drawer.ActualHeight:R}|canvas={canvas.ActualWidth:R}x{canvas.ActualHeight:R}|nativeParent={nativePane.ActualWidth:R}x{nativePane.ActualHeight:R}|parentBounds={resultBounds}|workbenchBounds={workbenchBounds}|workbenchHeight={workbenchView.ActualHeight:R}|fullContentFits=True|inlineVisible={inline.IsVisible}|tabRoundTrip=True|inspectionUnchanged=True|dirtyUnchanged=True" };
                    lines.AddRange(toolbar.Children.OfType<Button>().Select(button => $"button={System.Windows.Automation.AutomationProperties.GetAutomationId(button)}|size={button.ActualWidth:R}x{button.ActualHeight:R}|font={button.FontFamily.Source}/{button.FontSize:R}/{button.FontWeight}|enabled={button.IsEnabled}|canExecute={button.Command?.CanExecute(button.CommandParameter)}"));
                    ShellSmokeArtifacts.WriteTextReport(Path.ChangeExtension(screenshotQualityReportPath, ".section-layout.txt"), lines);
                }
            }
            if (teachingResults && !string.IsNullOrWhiteSpace(screenshotQualityReportPath))
            {
                if (workbenchView.ViewerContent is IOpenVisionThreeDViewerHost finalFitHost) finalFitHost.FitAll();
                await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
                var table = ShellSmokeArtifacts.FindVisualDescendants<DataGrid>(workbenchView).First(grid => grid.Name == "TeachingResultsTable");
                var rows = ShellSmokeArtifacts.FindVisualDescendants<DataGridRow>(table).Take(2).ToArray();
                var scroll = ShellSmokeArtifacts.FindVisualDescendants<ScrollViewer>(table).First();
                var lines = new List<string> { $"Actual Shell layout diagnostics|table={table.ActualWidth:R}x{table.ActualHeight:R}|columns={string.Join(",", table.Columns.Select(column => column.ActualWidth.ToString("R")))}|offset={scroll.HorizontalOffset:R},{scroll.VerticalOffset:R}" };
                lines.AddRange(rows.Select(row => $"row={row.GetIndex()}|height={row.ActualHeight:R}|top={row.TransformToAncestor(workbenchView).Transform(new Point()).Y:R}|cells={string.Join(",", ShellSmokeArtifacts.FindVisualDescendants<DataGridCell>(row).Select(cell => $"{cell.ActualWidth:R}x{cell.ActualHeight:R}"))}"));
                var results = ShellSmokeArtifacts.FindVisualDescendants<ResultsWorkspaceView>(workbenchView).First();
                var drawerHeader = (Border)results.FindName("ResultsSectionNavigation");
                var grip = (AvalonDock.Controls.AnchorablePaneTitle)results.FindName("TeachingResultsGrip");
                var pin = (Button)results.FindName("TeachingResultsAutoHide");
                var menu = (MenuItem)results.FindName("TeachingResultsDockMenu");
                var dockItem = results.DockPaneCommands as AvalonDock.Controls.LayoutAnchorableItem;
                lines.Add($"Results header|height={drawerHeader.ActualHeight:R}|grip={grip.ActualWidth:R}x{grip.ActualHeight:R}|nativeModel={ReferenceEquals(grip.Model, dockItem?.LayoutElement)}|pin={pin.ActualWidth:R}x{pin.ActualHeight:R}|pinEnabled={pin.IsEnabled}|autoHideBinding={ReferenceEquals(pin.Command, dockItem?.AutoHideCommand)}|menu={menu.ActualWidth:R}x{menu.ActualHeight:R}|canFloat={grip.Model?.CanFloat}|canClose={grip.Model?.CanClose}");
                lines.AddRange(ShellSmokeArtifacts.FindVisualDescendants<System.Windows.Controls.Primitives.DataGridColumnHeader>(table).Where(header => header.Column is not null).Select(header => $"header={header.Column.DisplayIndex}|align={header.HorizontalContentAlignment}|width={header.ActualWidth:R}"));
                ShellSmokeArtifacts.WriteTextReport(Path.ChangeExtension(screenshotQualityReportPath, ".results-layout.txt"), lines);
            }
        }

        return Success(null);
    }

    internal static string? ResolveEvidenceDirectory(string variable)
    {
        var directory = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrWhiteSpace(directory)) return null;
        var configuredRoot = Environment.GetEnvironmentVariable("OPENVISIONLAB_3D_TEST_ARTIFACT_ROOT");
        if (string.IsNullOrWhiteSpace(configuredRoot))
            throw new InvalidOperationException("Evidence capture requires an explicit test artifact root.");
        directory = Path.GetFullPath(directory);
        var root = Path.GetFullPath(configuredRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!directory.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Evidence capture must stay below the configured test artifact root.");
        return directory;
    }

    private void AttachSectionConnectionEvidence()
    {
        var directory = ResolveEvidenceDirectory("OVL3D_SECTION_CONNECTION_EVIDENCE");
        if (directory is null) return;
        Directory.CreateDirectory(directory);
        var sequence = 0;
        var attached = false;
        var notifications = new Queue<object>();
        var previousCursorVisible = false;
        IOpenVisionThreeDViewerHost? observedHost = null;
        double? Finite(double? value) => value is { } number && double.IsFinite(number) ? number : null;
        void Record(string action)
        {
            var host = workbenchView.ViewerContent as IOpenVisionThreeDViewerHost;
            var profile = host?.HostState.Profile;
            var workspace = ShellSmokeArtifacts.FindVisualDescendants<ViewerWorkspaceView>(workbenchView).FirstOrDefault();
            var bar = workspace?.FindName("MainViewerPresentationBar") as ViewerPresentationBar;
            var results = ShellSmokeArtifacts.FindVisualDescendants<ResultsWorkspaceView>(workbenchView).FirstOrDefault();
            var drawer = results?.FindName("DrawerHeightProfile") as HeightProfileView;
            var chart = drawer?.FindName("ProfileChartCanvas") as Canvas;
            var readout = chart?.Children.OfType<Border>().FirstOrDefault(child => System.Windows.Automation.AutomationProperties.GetAutomationId(child) == "ProfileCursorReadout");
            File.WriteAllText(Path.Combine(directory, $"{++sequence:D3}-connection.json"), JsonSerializer.Serialize(new
            {
                at = DateTime.UtcNow, action, workspaceMode = (workbenchView.DataContext as ShellMainWindowViewModel)?.SelectedWorkspaceMode.ToString(),
                rootHostPresent = host is not null, rootInputVisible = host?.HostState.C3DSampleVisible, rootProfile = profile?.EndpointSummary,
                rootCursorVisible = profile?.LinkedCursorVisible, rootCursorRaw = Finite(profile?.LinkedCursorRawHeight), rootCursorDistance = Finite(profile?.LinkedCursorDistanceCells), rootCursorSummary = profile?.LinkedCursorSummary,
                rootLinkedCursor = host is OpenVisionLab.ThreeD.Viewer.OpenVisionThreeDViewerControl native && native.LinkedHeightCursor is { } cursor ? new { cursor.Row, cursor.Column, raw = Finite(cursor.RawHeight), cursor.IsValid } : null,
                workspacePresent = workspace is not null, mainHostPresent = workspace?.MainViewerContent is not null, mainHostMatchesRoot = host is not null && ReferenceEquals(workspace?.MainViewerContent, host),
                mainBindingStatus = workspace is null ? null : System.Windows.Data.BindingOperations.GetBindingExpression(workspace, ViewerWorkspaceView.MainViewerContentProperty)?.Status.ToString(),
                barHostPresent = bar?.ViewerHost is not null, barHostMatchesRoot = host is not null && ReferenceEquals(bar?.ViewerHost, host),
                measureCanExecute = bar?.Presentation?.MeasureCommand.CanExecute(null), barProfile = bar?.Presentation?.ProfileState.EndpointSummary,
                drawerPresent = drawer is not null, drawerHostPresent = drawer?.ViewerHost is not null, drawerHostMatchesRoot = host is not null && ReferenceEquals(drawer?.ViewerHost, host),
                drawerBindingStatus = drawer is null ? null : System.Windows.Data.BindingOperations.GetBindingExpression(drawer, HeightProfileView.ViewerHostProperty)?.Status.ToString(),
                drawerHasInput = (drawer?.DataContext as HeightProfileViewModel)?.ProfileHasInput, drawerProfile = drawer?.ProfileState.EndpointSummary,
                drawerCursorVisible = drawer?.ProfileState.LinkedCursorVisible, drawerCursorRaw = Finite(drawer?.ProfileState.LinkedCursorRawHeight), drawerCursorDistance = Finite(drawer?.ProfileState.LinkedCursorDistanceCells), drawerCursorSummary = drawer?.DrawerCursorSummary,
                drawerTraceCommandReady = drawer?.TraceCursorCommand?.CanExecute(0.5), chartChildren = chart?.Children.Count, chartReadout = (readout?.Child as TextBlock)?.Text,
                eventHostMatchesRoot = ReferenceEquals(observedHost, host), hostNotifications = notifications.ToArray(),
                recipeDirty = (workbenchView.DataContext as ShellMainWindowViewModel)?.Workbench.IsDirty,
                capture = "Read-only public host, binding and rendered View state after actual input; bounded public notification trace; no command invocation or state mutation."
            }, new JsonSerializerOptions { WriteIndented = true }));
            notifications.Clear();
        }
        EventHandler<ViewerHostStateChangedEventArgs> recordHost = (_, args) =>
        {
            if (args.PropertyName != nameof(ViewerHostState.Profile)) return;
            var profile = args.State.Profile;
            if (notifications.Count >= 120) notifications.Dequeue();
            notifications.Enqueue(new
            {
                at = DateTime.UtcNow, visible = profile.LinkedCursorVisible, raw = Finite(profile.LinkedCursorRawHeight), distance = Finite(profile.LinkedCursorDistanceCells), profile.LinkedCursorSummary,
                clearedStack = previousCursorVisible && !profile.LinkedCursorVisible ? new System.Diagnostics.StackTrace().ToString() : null
            });
            previousCursorVisible = profile.LinkedCursorVisible;
        };
        MouseButtonEventHandler record = (_, args) =>
        {
            if (args.ChangedButton != MouseButton.Left) return;
            var action = args.OriginalSource is FrameworkElement element ? element.Name : args.OriginalSource?.GetType().Name ?? "unknown";
            dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() => Record(action)));
        };
        RoutedEventHandler? attachOwner = null;
        attachOwner = (_, _) =>
        {
            if (attached || Window.GetWindow(workbenchView) is not { } window) return;
            attached = true;
            workbenchView.Loaded -= attachOwner;
            observedHost = workbenchView.ViewerContent as IOpenVisionThreeDViewerHost;
            if (observedHost is not null) observedHost.HostStateChanged += recordHost;
            window.AddHandler(UIElement.PreviewMouseDownEvent, record, true);
            window.Closed += (_, _) =>
            {
                window.RemoveHandler(UIElement.PreviewMouseDownEvent, record);
                if (observedHost is not null) observedHost.HostStateChanged -= recordHost;
            };
            dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() => Record("loaded")));
        };
        workbenchView.Loaded += attachOwner;
    }

    private void AttachSectionPointerEvidence()
    {
        var directory = ResolveEvidenceDirectory("OVL3D_SECTION_POINTER_EVIDENCE");
        if (directory is null) return;
        Directory.CreateDirectory(directory);
        var sequence = 0;
        MouseButtonEventHandler record = (_, args) =>
        {
            if (args.ChangedButton != MouseButton.Left) return;
            DependencyObject? current = args.OriginalSource as DependencyObject;
            while (current is not null && current is not ButtonBase)
                current = current is Visual ? VisualTreeHelper.GetParent(current) : LogicalTreeHelper.GetParent(current);
            if (current is not ButtonBase button) return;
            var id = System.Windows.Automation.AutomationProperties.GetAutomationId(button);
            if (id is not ("DrawerProfileSwap" or "DrawerProfileClear")) return;
            var stem = $"{++sequence:D3}-{id}-actual-down";
            dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
            {
                var pressedBefore = button.IsPressed && Mouse.LeftButton == MouseButtonState.Pressed;
                if (!pressedBefore || !button.IsVisible || button.ActualWidth <= 0 || button.ActualHeight <= 0)
                {
                    File.WriteAllText(Path.Combine(directory, stem + ".json"), JsonSerializer.Serialize(new { validHeldFrame = false, reason = "Actual pointer was released before the render callback." }));
                    return;
                }
                button.UpdateLayout();
                var dpi = VisualTreeHelper.GetDpi(button);
                var drawing = new DrawingVisual();
                using (var context = drawing.RenderOpen())
                    context.DrawRectangle(new VisualBrush(button), null, new Rect(0, 0, button.ActualWidth, button.ActualHeight));
                var bitmap = new RenderTargetBitmap((int)Math.Ceiling(button.ActualWidth * dpi.DpiScaleX), (int)Math.Ceiling(button.ActualHeight * dpi.DpiScaleY), dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
                bitmap.Render(drawing);
                var pressedAfter = button.IsPressed && Mouse.LeftButton == MouseButtonState.Pressed;
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using (var stream = File.Create(Path.Combine(directory, stem + ".png"))) encoder.Save(stream);
                var surface = button.Template.FindName("Surface", button) as Border;
                var shell = workbenchView.DataContext as ShellMainWindowViewModel;
                var profile = workbenchView.ViewerContent is IOpenVisionThreeDViewerHost host ? host.HostState.Profile : null;
                File.WriteAllText(Path.Combine(directory, stem + ".json"), JsonSerializer.Serialize(new
                {
                    at = DateTime.UtcNow, validHeldFrame = pressedBefore && pressedAfter, pointerBeforeRender = pressedBefore, pointerAfterRender = pressedAfter,
                    window = Window.GetWindow(button)?.Title, button = id, button.IsPressed, button.IsMouseOver, button.IsKeyboardFocused, button.IsEnabled,
                    width = button.ActualWidth, height = button.ActualHeight, dpi = dpi.PixelsPerInchX, background = surface?.Background.ToString(), foreground = button.Foreground.ToString(),
                    recipeDirty = shell?.Workbench.IsDirty, endpoints = profile?.EndpointSummary, distance = profile?.GridDistanceCells,
                    capture = "In-process rendering during real pointer down; no forced style or synthetic input; separate from the Computer Use full-window screenshot."
                }, new JsonSerializerOptions { WriteIndented = true }));
            }));
        };
        workbenchView.AddHandler(UIElement.PreviewMouseDownEvent, record, true);
        RoutedEventHandler? attachOwner = null;
        attachOwner = (_, _) =>
        {
            if (Window.GetWindow(workbenchView) is not { } window) return;
            workbenchView.Loaded -= attachOwner;
            window.Closed += (_, _) => workbenchView.RemoveHandler(UIElement.PreviewMouseDownEvent, record);
        };
        workbenchView.Loaded += attachOwner;
    }

    private static ShellViewerWorkspaceSmokeResult Success(
        string? cameraLinkSummary) =>
        new(null, cameraLinkSummary);

    private static ShellViewerWorkspaceSmokeResult Failure(string message) =>
        new(message, null);
}

internal sealed record ShellViewerWorkspaceSmokeResult(
    string? Failure,
    string? CameraLinkSummary)
{
    public bool Succeeded => Failure is null;
}
