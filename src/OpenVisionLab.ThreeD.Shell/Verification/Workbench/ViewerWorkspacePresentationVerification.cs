using System.IO;
using System.Globalization;
using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Shell.PropertyGrid;
using OpenVisionLab.ThreeD.Shell.Views.Workbench;
using OpenVisionLab.ThreeD.Viewer;
using OpenVisionLab.ThreeD.Viewer.Models;
using OpenVisionLab.ThreeD.Presentation.Controls;

namespace OpenVisionLab.ThreeD.Shell;

internal static class ViewerWorkspacePresentationVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var lines = new List<string>
        {
            "OpenVisionLab 3D independent Viewer presentation verification",
            $"Generated: {DateTimeOffset.Now:O}",
            "Boundary|Presentation state is per real Viewer instance; camera link is session-only Main -> Auxiliary; no recipe or inspection execution."
        };
        var passed = 0;
        var progressPath = Path.ChangeExtension(reportPath, ".progress.txt");
        File.WriteAllText(progressPath, $"Started: {DateTimeOffset.Now:O}{Environment.NewLine}");
        void Progress(string message) => File.AppendAllText(progressPath, $"{DateTimeOffset.Now:O} | {message}{Environment.NewLine}");
        Window? layoutTestWindow = null;
        Window? chartTestWindow = null;

        void Check(string name, bool condition, string detail)
        {
            if (!condition)
            {
                throw new InvalidOperationException($"{name}: {detail}");
            }

            passed++;
            lines.Add($"PASS | {name} | {detail}");
            Progress($"PASS {passed} | {name}");
        }

        try
        {
            var session = new ViewerWorkspaceSession();
            Check(
                "Authoring starts with a single large 3D Viewer",
                session.Layout == ViewerWorkspaceLayout.Single
                && !session.HasAuxiliarySlot,
                $"layout={session.Layout};auxiliary={session.HasAuxiliarySlot}");
            Check(
                "camera link starts disabled",
                !session.IsCameraLinked,
                $"linked={session.IsCameraLinked}");
            Check(
                "split session retains two explicit Viewer slots",
                session.TrySetLayout(ViewerWorkspaceLayout.SplitVertical, ["main", "aux"], "aux")
                && session.MainContentId.Length == 0
                && session.AuxiliaryContentId == "aux",
                $"layout={session.Layout};main={session.MainContentId};aux={session.AuxiliaryContentId}");
            session.SetCameraLinked(true);
            Check(
                "link is explicit and session-owned",
                session.IsCameraLinked,
                $"linked={session.IsCameraLinked}");
            session.SetLayout(ViewerWorkspaceLayout.Single);
            Check(
                "single layout fail-closes the link",
                !session.IsCameraLinked,
                $"layout={session.Layout};linked={session.IsCameraLinked}");
            session.SetCameraLinked(true);
            session.ClearAuxiliaryContent();
            Check(
                "clearing auxiliary content unlinks without selecting a replacement",
                !session.IsCameraLinked
                && session.AuxiliaryContentId.Length == 0
                && session.IsAuxiliaryContentExplicitlyCleared,
                $"aux={session.AuxiliaryContentId};cleared={session.IsAuxiliaryContentExplicitlyCleared};linked={session.IsCameraLinked}");
            session.PinAuxiliaryContent(ViewerWorkspaceViewModel.HeightImageViewerContentId);
            session.SetLayout(ViewerWorkspaceLayout.HeightImageOnly);
            Check(
                "height-only mode reuses the pinned Height Image without a comparison slot",
                session.IsHeightImageOnly
                && !session.HasAuxiliarySlot
                && session.FocusedSlotId == ViewerWorkspaceSession.MainSlotId
                && session.AuxiliaryContentId == ViewerWorkspaceViewModel.HeightImageViewerContentId,
                $"layout={session.Layout};slot={session.FocusedSlotId};content={session.AuxiliaryContentId}");
            session.SetLayout(ViewerWorkspaceLayout.Single);
            session.SetLayout(ViewerWorkspaceLayout.SplitVertical);
            Check(
                "returning to the split View retains the selected Height Image",
                session.HasAuxiliarySlot
                && !session.IsHeightImageOnly
                && session.AuxiliaryContentId == ViewerWorkspaceViewModel.HeightImageViewerContentId,
                $"layout={session.Layout};content={session.AuxiliaryContentId}");

            var routingSelection = new InspectionWorkspaceSelectionSession();
            var routingSession = new ViewerWorkspaceSession();
            var routingCompareSlots = (A: string.Empty, B: string.Empty, C: string.Empty);
            using var routingViewModel = new ViewerWorkspaceViewModel(
                routingSession,
                routingSelection,
                ThreeDLocalization.Shared,
                () => (false, string.Empty),
                () =>
                [
                    new ToolWorkbenchRenderableC3DTarget("input.c3d", "Input", "HeightField", "Ready", "input.c3d", "", true, true, null, null),
                    new ToolWorkbenchRenderableC3DTarget("output.c3d", "Output", "HeightField", "Ready", "output.c3d", "", false, true, null, null)
                ],
                () => routingCompareSlots,
                _ => null,
                _ => false,
                () => { });
            routingSelection.SynchronizeTool("step.1", "input.c3d", InspectionWorkspaceRegionRole.None, null, "output.c3d");
            routingViewModel.SetInspectionDataViewCommand.Execute(ViewerInspectionDataView.Output);
            Check(
                "renderable Output resolves the selected step's actual output identity without changing pins",
                routingViewModel.EffectiveMainViewerContentId == "output.c3d"
                && routingViewModel.EffectiveViewerLayout == ViewerWorkspaceLayout.Single
                && routingSession.MainContentId.Length == 0
                && routingSession.AuxiliaryContentId.Length == 0,
                $"main={routingViewModel.EffectiveMainViewerContentId};pin={routingSession.MainContentId}");
            routingViewModel.SetInspectionDataViewCommand.Execute(ViewerInspectionDataView.InputOutput);
            Check(
                "Input / Output uses distinct selected C3D identities without changing graph or pins",
                routingViewModel.EffectiveMainViewerContentId == "input.c3d"
                && routingViewModel.EffectiveAuxiliaryViewerContentId == "output.c3d"
                && routingSession.MainContentId.Length == 0
                && routingSession.AuxiliaryContentId.Length == 0,
                $"input={routingViewModel.EffectiveMainViewerContentId};output={routingViewModel.EffectiveAuxiliaryViewerContentId}");
            routingCompareSlots = ("input.c3d", "output.c3d", string.Empty);
            routingViewModel.RefreshOutputCompareSlots();
            routingSession.SetLayout(ViewerWorkspaceLayout.Single);
            Check(
                "A/B Viewer compare is enabled only for two distinct current 3D artifacts",
                routingViewModel.CanShowOutputCompareViewer
                && routingViewModel.ShowOutputCompareViewerCommand.CanExecute(null),
                $"A={routingCompareSlots.A};B={routingCompareSlots.B};enabled={routingViewModel.CanShowOutputCompareViewer}");
            routingSession.SetCameraLinked(true);
            routingViewModel.ShowOutputCompareViewerCommand.Execute(null);
            Check(
                "explicit A/B compare uses the main Viewer slots without replacing their saved pins or running inspection",
                routingViewModel.IsOutputCompareViewerActive
                && routingViewModel.EffectiveViewerLayout == ViewerWorkspaceLayout.SplitVertical
                && routingViewModel.EffectiveMainViewerContentId == routingCompareSlots.A
                && routingViewModel.EffectiveAuxiliaryViewerContentId == routingCompareSlots.B
                && routingSession.Layout == ViewerWorkspaceLayout.SplitVertical
                && routingSession.MainContentId.Length == 0
                && routingSession.AuxiliaryContentId.Length == 0
                && routingSession.IsCameraLinked,
                $"layout={routingViewModel.EffectiveViewerLayout};A={routingViewModel.EffectiveMainViewerContentId};B={routingViewModel.EffectiveAuxiliaryViewerContentId};pins={routingSession.MainContentId}/{routingSession.AuxiliaryContentId};linked={routingSession.IsCameraLinked}");
            routingViewModel.ShowOutputCompareViewerCommand.Execute(null);
            Check(
                "reinvoking the selected A/B action closes compare and restores the previous Viewer layout without changing pins",
                !routingViewModel.IsOutputCompareViewerActive
                && routingViewModel.IsInspectionInputView
                && routingSession.Layout == ViewerWorkspaceLayout.Single
                && routingSession.MainContentId.Length == 0
                && routingSession.AuxiliaryContentId.Length == 0
                && !routingSession.IsCameraLinked,
                $"compare={routingViewModel.IsOutputCompareViewerActive};view={routingViewModel.InspectionDataView};layout={routingSession.Layout};pins={routingSession.MainContentId}/{routingSession.AuxiliaryContentId};linked={routingSession.IsCameraLinked}");
            routingViewModel.ShowOutputCompareViewerCommand.Execute(null);
            routingCompareSlots = ("input.c3d", "input.c3d", string.Empty);
            routingViewModel.RefreshOutputCompareSlots();
            Check(
                "invalidating an active A/B pair exits compare and restores the saved Viewer layout",
                !routingViewModel.IsOutputCompareViewerActive
                && routingViewModel.IsInspectionInputView
                && routingSession.Layout == ViewerWorkspaceLayout.Single
                && routingSession.MainContentId.Length == 0
                && routingSession.AuxiliaryContentId.Length == 0
                && !routingViewModel.ShowOutputCompareViewerCommand.CanExecute(null),
                $"compare={routingViewModel.IsOutputCompareViewerActive};view={routingViewModel.InspectionDataView};layout={routingSession.Layout};pins={routingSession.MainContentId}/{routingSession.AuxiliaryContentId};enabled={routingViewModel.ShowOutputCompareViewerCommand.CanExecute(null)}");
            routingCompareSlots = ("input.c3d", "output.c3d", string.Empty);
            routingViewModel.RefreshOutputCompareSlots();
            routingViewModel.ShowOutputCompareViewerCommand.Execute(null);
            routingSelection.SynchronizeTool("step.2", "input.c3d", InspectionWorkspaceRegionRole.None, null, "output.c3d");
            Check(
                "changing steps exits A/B compare, restores the prior Viewer layout, and returns to Input without pinning or running",
                routingViewModel.IsInspectionInputView
                && !routingViewModel.IsOutputCompareViewerActive
                && routingSession.Layout == ViewerWorkspaceLayout.Single
                && routingSession.MainContentId.Length == 0,
                $"view={routingViewModel.InspectionDataView};compare={routingViewModel.IsOutputCompareViewerActive};layout={routingSession.Layout};pin={routingSession.MainContentId}");
            routingCompareSlots = ("input.c3d", "input.c3d", string.Empty);
            routingViewModel.RefreshOutputCompareSlots();
            Check(
                "duplicate A/B artifact identities cannot enter compare mode",
                !routingViewModel.CanShowOutputCompareViewer
                && !routingViewModel.ShowOutputCompareViewerCommand.CanExecute(null),
                $"A={routingCompareSlots.A};B={routingCompareSlots.B};enabled={routingViewModel.CanShowOutputCompareViewer}");

            using var mainViewer = new OpenVisionThreeDViewerControl(loadDefaultSamples: false);
            using var auxiliaryViewer = new OpenVisionThreeDViewerControl(loadDefaultSamples: false);
            mainViewer.SidePanelsVisible = false;
            auxiliaryViewer.SidePanelsVisible = false;
            using var disposedWorkspaceView = new ViewerWorkspaceView();
            disposedWorkspaceView.Dispose();
            disposedWorkspaceView.MainViewerContent = mainViewer;
            Check(
                "disposed workspace ignores late main Viewer content callback",
                !disposedWorkspaceView.HasAttachedMainViewer,
                $"attached={disposedWorkspaceView.HasAttachedMainViewer}|content={disposedWorkspaceView.MainViewerContent is not null}");

            using var layoutWorkspaceView = new ViewerWorkspaceView
            {
                MainViewerContent = mainViewer,
            };
            layoutWorkspaceView.Measure(new Size(826, 600));
            layoutWorkspaceView.Arrange(new Rect(0, 0, 826, 600));
            layoutWorkspaceView.UpdateLayout();
            layoutTestWindow = new Window
            {
                Width = 856,
                Height = 630,
                WindowStyle = WindowStyle.None,
                WindowStartupLocation = WindowStartupLocation.Manual,
                ShowInTaskbar = false,
                ShowActivated = false,
                Content = layoutWorkspaceView,
            };
            OpenVisionLab.ThreeD.Shell.Verification.Smoke.ShellWindowNativeInterop.TryGetLeftmostWorkArea(out var testWorkArea);
            layoutTestWindow.Left = testWorkArea.Left;
            layoutTestWindow.Top = testWorkArea.Top;
            layoutTestWindow.Show();
            var monitorReport = reportPath + ".monitor.txt";
            OpenVisionLab.ThreeD.Shell.Verification.Smoke.ShellWindowNativeInterop.AppendWindowMonitorEvidence(layoutTestWindow, monitorReport);
            layoutTestWindow.UpdateLayout();
            var mainViewerPresenter = (ContentPresenter)layoutWorkspaceView.FindName("MainViewerPresenter");
            mainViewerPresenter.Visibility = Visibility.Visible;
            layoutWorkspaceView.UpdateLayout();
            var layoutToolbar = (Border)layoutWorkspaceView.FindName("ViewerLayoutToolbar");
            var threeDViewerMode = (ToggleButton)layoutWorkspaceView.FindName("ThreeDViewerMode");
            var heightOnlyViewerMode = (ToggleButton)layoutWorkspaceView.FindName("HeightOnlyViewerMode");
            var threeDHeightViewerMode = (ToggleButton)layoutWorkspaceView.FindName("ThreeDHeightViewerMode");
            var heightImageModeLabel = (TextBlock)layoutWorkspaceView.FindName("HeightImageModeLabel");
            var viewerWorkspaceOptionsMenu = (Menu)layoutWorkspaceView.FindName("ViewerWorkspaceOptionsMenu");
            var mainPresentationBar = (ViewerPresentationBar)layoutWorkspaceView.FindName("MainViewerPresentationBar");
            var auxiliaryPresentationBar = (ViewerPresentationBar)layoutWorkspaceView.FindName("AuxiliaryViewerPresentationBar");
            var mainViewerPin = (ComboBox)layoutWorkspaceView.FindName("MainViewerContentPin");
            var auxiliaryViewerPin = (ComboBox)layoutWorkspaceView.FindName("AuxiliaryViewerContentPin");
            foreach (var pinWidth in new[] { 90d, 360d })
            {
                var pin = new ComboBox
                {
                    Style = (Style)layoutWorkspaceView.Resources["ViewerSourcePinStyle"],
                    Width = pinWidth,
                    VerticalAlignment = VerticalAlignment.Center,
                    Tag = pinWidth < 100 ? "coupon.C3D" : "긴_입력_이름_1280x840_반복_검사_맵.C3D",
                    ItemsSource = new[] { new { ViewerName = "coupon.C3D", SourcePath = "coupon.C3D" } }
                };
                var pinWindow = new Window
                {
                    Owner = layoutTestWindow, Content = pin, Width = pinWidth + 32, Height = 96,
                    Left = layoutTestWindow.Left, Top = layoutTestWindow.Top,
                    WindowStyle = WindowStyle.None, ShowActivated = false, ShowInTaskbar = false
                };
                try
                {
                    pinWindow.Show();
                    pinWindow.Dispatcher.Invoke(DispatcherPriority.Render, new Action(pinWindow.UpdateLayout));
                    var toggle = OpenVisionLab.ThreeD.Shell.Verification.Smoke.ShellSmokeArtifacts.FindVisualDescendants<ToggleButton>(pin).Single();
                    var toggleBounds = toggle.TransformToAncestor(pin).TransformBounds(new Rect(toggle.RenderSize));
                    var hitPoints = new[] { new Point(5, pin.ActualHeight / 2), new Point(pin.ActualWidth / 2, pin.ActualHeight / 2), new Point(pin.ActualWidth - 8, pin.ActualHeight / 2) };
                    var hits = hitPoints.Select(point => pin.InputHitTest(point) as DependencyObject).ToArray();
                    Check($"V5 source selector visible body and arrow share one hit region at {pinWidth}px",
                        toggleBounds.Left <= 0.5 && toggleBounds.Top <= 0.5
                        && Math.Abs(toggleBounds.Width - pin.ActualWidth) <= 0.5 && Math.Abs(toggleBounds.Height - pin.ActualHeight) <= 0.5
                        && hits.All(hit => hit is not null && IsVisualDescendant(toggle, hit)),
                        $"selector={pin.RenderSize};toggle={toggleBounds};hits={string.Join(',', hits.Select(hit => hit?.GetType().Name ?? "none"))}");
                    OpenVisionLab.ThreeD.Viewer.Rendering.WpfScreenshotCapture.Save(
                        OpenVisionLab.ThreeD.Viewer.Rendering.WpfScreenshotCapture.Capture(pinWindow).Bitmap,
                        Path.ChangeExtension(reportPath, $".source-pin-{pinWidth:F0}.png"));
                }
                finally { pinWindow.Close(); }
            }
            Check("V5 both source menus wrap complete names and use a single pointer-down toggle",
                new[] { mainViewerPin, auxiliaryViewerPin }.All(pin =>
                {
                    pin.ApplyTemplate();
                    return string.IsNullOrEmpty(pin.DisplayMemberPath) && pin.ItemTemplate.LoadContent() is TextBlock { TextWrapping: TextWrapping.Wrap, MaxWidth: 392 }
                        && OpenVisionLab.ThreeD.Shell.Verification.Smoke.ShellSmokeArtifacts.FindVisualDescendants<ToggleButton>(pin).Any(toggle => toggle.ClickMode == ClickMode.Press);
                }), "main and auxiliary share one bounded wrapping source-pin template; actual pointer input is verified separately");
            var mainSlot = (Border)layoutWorkspaceView.FindName("MainSlot");
            var mainViewerGeometry = (ComboBox)mainViewer.FindName("GeometryStyleSelector");
            var mainDiagnosticChannel = (ComboBox)mainViewer.FindName("DiagnosticChannelSelector");
            var mainViewerDisplayOptions = (MenuItem)mainViewer.FindName("ViewerDisplayOptionsMenu");
            var heightDistributionPanel = mainViewer.FindName("C3DHeightDistributionPanel");
            var mainPresentationPalette = (ListBox)mainPresentationBar.FindName("PaletteSelector");
            var mainPresentationDisplayOptions = (Panel)mainPresentationBar.FindName("ViewerDisplayOptionsContent");
            var viewerWorkspaceOptionsRoot = (MenuItem)viewerWorkspaceOptionsMenu.Items[0];
            var splitViewerMenuItem = viewerWorkspaceOptionsRoot.Items.OfType<MenuItem>().Single(item => AutomationProperties.GetAutomationId(item) == "ViewerSplitHorizontalLayout");
            var popOutViewerMenuItem = viewerWorkspaceOptionsRoot.Items.OfType<MenuItem>().Single(item => AutomationProperties.GetAutomationId(item) == "ViewerPopOutLayout");
            var cameraLinkMenuItem = viewerWorkspaceOptionsRoot.Items.OfType<MenuItem>().Single(item => AutomationProperties.GetAutomationId(item) == "ViewerCameraLink");
            var viewerDisplaySettingsButton = (Button)mainPresentationBar.FindName("ViewerDisplaySettingsButton");
            var viewerDisplaySettingsPopup = (Popup)mainPresentationBar.FindName("ViewerDisplaySettingsPopup");
            var presentationDiagnosticGroup = mainPresentationBar.FindName("DiagnosticChannelGroup");
            var viewerGrid = (Grid)layoutWorkspaceView.FindName("ViewerGrid");
            var mainSlotHeader = (Border)layoutWorkspaceView.FindName("MainViewerSlotHeader");
            var auxiliarySlot = (Border)layoutWorkspaceView.FindName("AuxiliarySlot");
            Check(
                "v3 slot emphasis belongs to the header and has no duplicate cursor row",
                mainSlot.BorderThickness == new Thickness(0)
                && auxiliarySlot.BorderThickness == new Thickness(0)
                && mainSlotHeader.BorderThickness.Left == 0
                && mainSlotHeader.BorderThickness.Right == 0
                && !OpenVisionLab.ThreeD.Shell.Verification.Smoke.ShellSmokeArtifacts.FindVisualDescendants<Border>(layoutWorkspaceView)
                    .Any(border => AutomationProperties.GetAutomationId(border) == "SharedHeightCursorSummary"),
                $"main={mainSlot.BorderThickness};aux={auxiliarySlot.BorderThickness};header={mainSlotHeader.BorderThickness}");
            var viewerBottomStatus = (Border)mainViewer.FindName("ViewerBottomStatus");
            var nativeViewport = (FrameworkElement)mainViewer.FindName("Viewport");
            var viewportBeforeCursor = nativeViewport.TransformToAncestor(mainViewer).TransformBounds(new Rect(nativeViewport.RenderSize));
            var coordinateStatus = OpenVisionLab.ThreeD.Shell.Verification.Smoke.ShellSmokeArtifacts.FindVisualDescendants<TextBlock>(mainViewer)
                .Single(text => AutomationProperties.GetAutomationId(text) == "ViewerCoordinateStatus");
            var longCoordinate = new System.Windows.Documents.Run("src X=1,279.000, Y=-32,768.000, Z=839.000 -> aligned X=1,289.000, Y=-32,760.000, Z=844.000 | raw -32768.000");
            coordinateStatus.Inlines.Add(longCoordinate);
            mainViewer.UpdateLayout();
            var viewportWithCursor = nativeViewport.TransformToAncestor(mainViewer).TransformBounds(new Rect(nativeViewport.RenderSize));
            coordinateStatus.Inlines.Remove(longCoordinate);
            mainViewer.UpdateLayout();
            Check(
                "v3 cursor text does not move or resize the 3D viewport and remains below it",
                viewportBeforeCursor == viewportWithCursor
                && viewerBottomStatus.ActualHeight == 24
                && viewerBottomStatus.TranslatePoint(new Point(), mainViewer).Y >= viewportWithCursor.Bottom,
                $"before={viewportBeforeCursor};after={viewportWithCursor};footer={viewerBottomStatus.ActualHeight:F0}");
            var coordinateScroll = (ScrollViewer)mainViewer.FindName("ViewerCoordinateScroll");
            var mainWidthBeforeLongCoordinate = mainViewer.Width;
            mainViewer.Width = 340;
            layoutTestWindow.UpdateLayout();
            var narrowViewportBeforeCoordinate = nativeViewport.TransformToAncestor(mainViewer).TransformBounds(new Rect(nativeViewport.RenderSize));
            coordinateStatus.Inlines.Add(longCoordinate);
            mainViewer.UpdateLayout();
            coordinateScroll.ScrollToEnd();
            layoutTestWindow.Dispatcher.Invoke(DispatcherPriority.Render, new Action(() => layoutTestWindow.UpdateLayout()));
            Check(
                "v3 long coordinates remain scrollable inside the fixed footer at 340px",
                mainViewer.ActualWidth == 340
                && coordinateScroll.ScrollableHeight > 0
                && coordinateScroll.ComputedVerticalScrollBarVisibility == Visibility.Visible
                && coordinateScroll.VerticalOffset == coordinateScroll.ScrollableHeight
                && coordinateScroll.ViewportHeight >= 13
                && viewerBottomStatus.ActualHeight == 24
                && nativeViewport.TransformToAncestor(mainViewer).TransformBounds(new Rect(nativeViewport.RenderSize)) == narrowViewportBeforeCoordinate,
                $"width={mainViewer.ActualWidth};viewport={coordinateScroll.ViewportHeight};extent={coordinateScroll.ExtentHeight};offset={coordinateScroll.VerticalOffset}/{coordinateScroll.ScrollableHeight};footer={viewerBottomStatus.ActualHeight}");
            OpenVisionLab.ThreeD.Viewer.Rendering.WpfScreenshotCapture.Save(
                OpenVisionLab.ThreeD.Viewer.Rendering.WpfScreenshotCapture.Capture(layoutWorkspaceView).Bitmap,
                Path.ChangeExtension(reportPath, ".long-coordinate-340.png"));
            coordinateStatus.Inlines.Remove(longCoordinate);
            coordinateScroll.ScrollToTop();
            mainViewer.Width = mainWidthBeforeLongCoordinate;
            layoutTestWindow.UpdateLayout();
            Check(
                "R19 Compact Viewer keeps one layout row and a dominant canvas",
                layoutToolbar.ActualHeight <= 42
                && mainPresentationBar.Visibility == Visibility.Visible
                && mainPresentationBar.ActualWidth > 0
                && mainPresentationBar.TranslatePoint(new Point(mainPresentationBar.ActualWidth, 0), layoutWorkspaceView).X <= layoutWorkspaceView.ActualWidth
                && mainPresentationBar.ActualHeight <= 60
                && viewerGrid.ActualHeight >= layoutWorkspaceView.ActualHeight * 0.78,
                $"toolbar={layoutToolbar.ActualWidth:F0}x{layoutToolbar.ActualHeight:F0};presentation={mainPresentationBar.ActualWidth:F0}x{mainPresentationBar.ActualHeight:F0};canvas={viewerGrid.ActualWidth:F0}x{viewerGrid.ActualHeight:F0}");
            Check(
                "R19 height range popup starts closed and follows the loaded host state",
                !viewerDisplaySettingsPopup.IsOpen
                && !mainPresentationBar.Presentation!.IsDisplaySettingsOpen
                && viewerDisplaySettingsButton.IsVisible
                && BindingOperations.GetBindingExpression(mainPresentationPalette, Selector.SelectedItemProperty)?.ParentBinding.Path.Path == "Presentation.SelectedColorMap",
                $"open={viewerDisplaySettingsPopup.IsOpen};button={viewerDisplaySettingsButton.Visibility};visible={viewerDisplaySettingsButton.IsVisible};selected={mainPresentationPalette.SelectedItem}");
            Check(
                "Viewer display toolbar omits internal diagnostics and repeated raw range summaries",
                mainPresentationBar.Content is Border { ToolTip: null }
                && !OpenVisionLab.ThreeD.Shell.Verification.Smoke.ShellSmokeArtifacts.FindVisualDescendants<TextBlock>(mainPresentationBar).Any(text => BindingOperations.GetMultiBindingExpression(text, TextBlock.TextProperty)?.ParentMultiBinding.Bindings.OfType<Binding>().Any(binding => binding.Path?.Path == "Presentation.C3DHeightColorRangeSummary") == true),
                "display settings retain the range editor");
            mainPresentationBar.UpdateLayout();
            Check(
                "v3 Viewer uses layout glyphs and preserves the keyboard Height Image route",
                threeDViewerMode.Content is Wpf.Ui.Controls.SymbolIcon { Symbol: Wpf.Ui.Controls.SymbolRegular.Square24 }
                && heightOnlyViewerMode.Visibility == Visibility.Collapsed
                && BindingOperations.GetBindingExpression(heightOnlyViewerMode, ToggleButton.IsCheckedProperty)?.ParentBinding.Path.Path == "IsHeightImageOnlyViewerLayout"
                && heightImageModeLabel.Text == ThreeDLocalization.Shared.Resolve("ThreeD.Viewer.SplitLayout", "좌우 2개", "Two side by side")
                && IsVisualDescendant(mainSlot, mainViewerPin),
                $"singleGlyph={threeDViewerMode.Content is Wpf.Ui.Controls.SymbolIcon};heightRoute={heightOnlyViewerMode.Visibility};pinInCanvas={IsVisualDescendant(mainSlot, mainViewerPin)}");
            Check(
                "normal Viewer headers select real content without A/B comparison badges",
                layoutWorkspaceView.FindName("AuxiliarySlotBadge") is null
                && BindingOperations.GetBindingExpression(mainViewerPin, Selector.SelectedValueProperty)?.ParentBinding.Path.Path == "MainViewerDisplayedContentId"
                && BindingOperations.GetBindingExpression(auxiliaryViewerPin, Selector.SelectedValueProperty)?.ParentBinding.Path.Path == "AuxiliaryViewerContentId"
                && Grid.GetColumn(mainViewerPin) == 1
                && Grid.GetColumn((ContentControl)layoutWorkspaceView.FindName("MainPaneSourceRole")) == 0
                && (!mainViewerPin.IsVisible || mainViewerPin.TranslatePoint(new Point(), mainSlot).X >= ((ContentControl)layoutWorkspaceView.FindName("MainPaneSourceRole")).ActualWidth + 7),
                $"mainPinX={mainViewerPin.TranslatePoint(new Point(), mainSlot).X:F0};mainBinding={BindingOperations.GetBindingExpression(mainViewerPin, Selector.SelectedValueProperty)?.ParentBinding.Path.Path};auxBinding={BindingOperations.GetBindingExpression(auxiliaryViewerPin, Selector.SelectedValueProperty)?.ParentBinding.Path.Path}");
            var viewerDataButtons = new[] { "ViewerInputButton", "ViewerOutputButton", "ViewerInputOutputButton" }
                .Select(name => (ToggleButton)layoutWorkspaceView.FindName(name)).ToArray();
            Check("D40 compact Viewer commands reuse the existing input/result display route",
                threeDViewerMode.Width == 28 && threeDViewerMode.Height == 24
                && viewerDataButtons.All(button => button.Width == 28 && button.Height == 24
                    && BindingOperations.GetBindingExpression(button, ButtonBase.CommandProperty)?.ParentBinding.Path.Path == "SetInspectionDataViewCommand"
                    && button.ToolTip is string { Length: > 0 } && AutomationProperties.GetName(button).Length > 0)
                && viewerDataButtons.Select(button => button.CommandParameter).SequenceEqual(new object[] { ViewerInspectionDataView.Input, ViewerInspectionDataView.Output, ViewerInspectionDataView.InputOutput }),
                $"layout={threeDViewerMode.Width}x{threeDViewerMode.Height};parameters={string.Join(',', viewerDataButtons.Select(button => button.CommandParameter))}");
            Check(
                "unavailable 3D diagnostic channel does not clip narrow Viewer toolbar commands",
                !mainViewer.DisplayEditor.CanSelectDiagnosticChannel
                && mainDiagnosticChannel.Visibility == Visibility.Collapsed
                && presentationDiagnosticGroup is null
                && BindingOperations.GetBindingExpression(mainDiagnosticChannel, UIElement.VisibilityProperty)?.ParentBinding.Path.Path == "Display.CanSelectDiagnosticChannel",
                $"available={mainViewer.DisplayEditor.CanSelectDiagnosticChannel};native={mainDiagnosticChannel.Visibility};duplicatePresentationChannel={presentationDiagnosticGroup is not null}");
            Check(
                "R19 detailed Viewer display options and shared menu start closed",
                !mainViewerDisplayOptions.IsSubmenuOpen
                && !viewerDisplaySettingsPopup.IsOpen
                && !viewerWorkspaceOptionsRoot.IsSubmenuOpen
                && viewerWorkspaceOptionsMenu.IsVisible,
                $"geometry={mainViewerDisplayOptions.IsSubmenuOpen};workspaceMenuOpen={viewerWorkspaceOptionsRoot.IsSubmenuOpen};slot={viewerDisplaySettingsPopup.IsOpen}");
            Check(
                "Viewer basic controls are named and supplemental controls belong to their slot",
                IsVisualDescendant(mainSlot, mainPresentationBar)
                && !IsVisualDescendant((Panel)layoutWorkspaceView.FindName("ViewerCommandRows"), mainPresentationBar)
                && ((Button)mainViewer.FindName("ViewerFitAllButton")).Content is string fitCaption && fitCaption.Length > 0
                && ((Button)mainViewer.FindName("ViewerFitRoiButton")).Command is not null
                && ((MenuItem)mainViewer.FindName("ViewerViewMenuRoot")).Template is not null
                && viewerWorkspaceOptionsRoot.Template is not null
                && viewerWorkspaceOptionsRoot.Items.OfType<MenuItem>().Any(item => item.Name == "ViewerInspectionOutputCompare"),
                "fit/ROI buttons have existing commands; comparison is a submenu; color controls live in the 3D slot");
            ((MenuItem)mainViewer.FindName("ViewerViewMenuRoot")).IsSubmenuOpen = true;
            mainViewerDisplayOptions.IsSubmenuOpen = true;
            viewerDisplaySettingsButton.Command!.Execute(null);
            mainViewer.UpdateLayout();
            mainPresentationBar.UpdateLayout();
            layoutWorkspaceView.UpdateLayout();
            Check(
                "R19 shared Viewer menu owns layout and camera link once; display options stay with each Viewer",
                viewerDisplaySettingsPopup.IsOpen
                && viewerWorkspaceOptionsMenu.Items.Count == 1
                && viewerWorkspaceOptionsRoot.Items.Count == 10
                && IsVisualDescendant(mainPresentationDisplayOptions, mainPresentationPalette)
                && !IsVisualDescendant(mainPresentationDisplayOptions, viewerWorkspaceOptionsMenu)
                && !IsVisualDescendant(mainPresentationDisplayOptions, viewerDisplaySettingsButton)
                && mainPresentationBar.FindName("ViewerWorkspaceOptionsMenu") is null
                && auxiliaryPresentationBar.FindName("ViewerWorkspaceOptionsMenu") is null
                && viewerWorkspaceOptionsMenu.Visibility == Visibility.Visible
                && BindingOperations.GetBindingExpression(splitViewerMenuItem, MenuItem.CommandProperty)?.ParentBinding.Path.Path == "SplitViewerHorizontallyCommand"
                && BindingOperations.GetBindingExpression(popOutViewerMenuItem, MenuItem.CommandProperty)?.ParentBinding.Path.Path == "PopOutViewerCommand"
                && BindingOperations.GetBindingExpression(cameraLinkMenuItem, MenuItem.CommandProperty)?.ParentBinding.Path.Path == "ToggleViewerCameraLinkCommand"
                && viewerDisplaySettingsButton.IsVisible,
                $"layoutMenu={viewerWorkspaceOptionsMenu.Visibility};workspaceMenu={viewerWorkspaceOptionsMenu.IsVisible}/{viewerWorkspaceOptionsRoot.IsSubmenuOpen};slot={viewerDisplaySettingsPopup.IsOpen};bar={mainPresentationBar.IsVisible}/{mainPresentationBar.ActualWidth:F0}x{mainPresentationBar.ActualHeight:F0};range={viewerDisplaySettingsButton.IsVisible};cameraLink={cameraLinkMenuItem.Command}");
            var auxiliarySettingsPopup = (Popup)auxiliaryPresentationBar.FindName("ViewerDisplaySettingsPopup");
            var auxiliarySettingsButton = (Button)auxiliaryPresentationBar.FindName("ViewerDisplaySettingsButton");
            Check("V5 auxiliary 3D settings anchor belongs to its visible toolbar rather than the Window",
                auxiliaryPresentationBar.DialogPlacementTarget is null && auxiliarySettingsPopup.PlacementTarget == auxiliarySettingsButton,
                $"external={auxiliaryPresentationBar.DialogPlacementTarget};target={auxiliarySettingsPopup.PlacementTarget}");
            var settingsEscape = ((Grid)viewerDisplaySettingsPopup.Child).InputBindings.OfType<System.Windows.Input.KeyBinding>().Single(binding => binding.Key == System.Windows.Input.Key.Escape);
            Check("V5 popup Escape binds the current presentation close command explicitly",
                ReferenceEquals(settingsEscape.Command, mainPresentationBar.Presentation!.CloseDisplaySettingsCommand), "same presentation close command");
            mainPresentationBar.Presentation!.CancelDisplaySettingsCommand.Execute(null);
            using var heightImageViewModel = new HeightImageViewerViewModel(
                ThreeDLocalization.Shared,
                new SharedHeightCursorSession());
            var embeddedHeightImageView = new HeightImageViewerView
            {
                DataContext = heightImageViewModel,
                UseEmbeddedChrome = true,
            };
            var contentBeforeHeightPopup = layoutTestWindow.Content;
            layoutTestWindow.Content = new Border { Width = 400, Height = 360, Child = embeddedHeightImageView };
            layoutTestWindow.Dispatcher.Invoke(DispatcherPriority.Loaded, new Action(layoutTestWindow.UpdateLayout));
            embeddedHeightImageView.Measure(new Size(400, 360));
            embeddedHeightImageView.Arrange(new Rect(0, 0, 400, 360));
            embeddedHeightImageView.UpdateLayout();
            var heightImageChrome = (Border)embeddedHeightImageView.FindName("HeightImageChrome");
            var heightImageStatus = (TextBlock)embeddedHeightImageView.FindName("HeightImageStatus");
            var heightImageManualControls = (WrapPanel)embeddedHeightImageView.FindName("HeightImageManualDisplayControls");
            var heightImageRangeSummary = (StackPanel)embeddedHeightImageView.FindName("HeightImageRangeSummary");
            var heightImageRoiHint = (TextBlock)embeddedHeightImageView.FindName("HeightImageRoiInteractionHint");
            var heightImagePalette = (ComboBox)embeddedHeightImageView.FindName("PaletteSelector");
            var heightImageInvalidOverlay = (WrapPanel)embeddedHeightImageView.FindName("HeightImageInvalidOverlayControls");
            var heightImageDisplaySettings = (Expander)embeddedHeightImageView.FindName("HeightImageDisplaySettings");
            var heightImageDisplaySettingsToggle = (ToggleButton)embeddedHeightImageView.FindName("HeightImageDisplaySettingsToggle");
            var heightImageDisplaySettingsPopup = (Popup)embeddedHeightImageView.FindName("HeightImageDisplaySettingsPopup");
            var heightImageScroller = (ScrollViewer)embeddedHeightImageView.FindName("ImageScrollViewer");
            var heightImageCursorStatus = (Border)embeddedHeightImageView.FindName("HeightImageCursorStatus");
            Check(
                "v3 Height Image reserves a fixed bottom coordinate row even without a cursor",
                heightImageCursorStatus.ActualHeight == 24
                && heightImageCursorStatus.TranslatePoint(new Point(), embeddedHeightImageView).Y >= heightImageScroller.TranslatePoint(new Point(0, heightImageScroller.ActualHeight), embeddedHeightImageView).Y
                && heightImageViewModel.CursorCoordinateSummary == "X — · Y — · Z —",
                $"footer={heightImageCursorStatus.ActualHeight:F0};coordinates={heightImageViewModel.CursorCoordinateSummary}");
            Check("D40 empty Height Image has no stale legend and its fixed footer retains keyboard scrolling",
                heightImageViewModel.LegendTickLabels.Count == 0 && heightImageViewModel.LegendUnit.Length == 0
                && ((Grid)embeddedHeightImageView.FindName("HeightImageHeightLegend")).Visibility == Visibility.Collapsed
                && OpenVisionLab.ThreeD.Shell.Verification.Smoke.ShellSmokeArtifacts.FindVisualDescendants<ScrollViewer>(heightImageCursorStatus)
                    .Any(scroll => scroll.Focusable && scroll.VerticalScrollBarVisibility == ScrollBarVisibility.Auto)
                && coordinateStatus.FontSize == 11.5 && coordinateStatus.FontWeight == FontWeights.Normal,
                $"ticks={heightImageViewModel.LegendTickLabels.Count};unit={heightImageViewModel.LegendUnit};nativeFooter={coordinateStatus.FontSize}/{coordinateStatus.FontWeight}");
            var heightLegendBackdrop = ((Grid)embeddedHeightImageView.FindName("HeightImageHeightLegend")).Background;
            Check("D40 Height Image legend has an opaque semantic backdrop over bright or missing input cells",
                heightLegendBackdrop is SolidColorBrush { Opacity: 1, Color.A: 255 }
                && ReferenceEquals(heightLegendBackdrop, embeddedHeightImageView.FindResource("ThreeD.ViewportBrush")),
                $"background={heightLegendBackdrop}");
            Check(
                "embedded Height Image starts with display settings closed and a dominant canvas",
                heightImageStatus.Visibility == Visibility.Collapsed
                && !heightImageDisplaySettingsPopup.IsOpen
                && heightImageRangeSummary.Visibility == Visibility.Collapsed
                && heightImageRoiHint.Visibility == Visibility.Collapsed
                && !heightImageDisplaySettings.IsExpanded
                && heightImageDisplaySettings.Visibility == Visibility.Collapsed
                && heightImageDisplaySettingsToggle.IsChecked == false
                && heightImagePalette.ActualHeight == 0
                && heightImageInvalidOverlay.ActualHeight == 0
                && heightImageChrome.ActualHeight <= 36
                && Grid.GetRow(heightImageChrome) == 1
                && heightImageScroller.ActualHeight >= 250,
                $"chrome={heightImageChrome.ActualWidth:F0}x{heightImageChrome.ActualHeight:F0};image={heightImageScroller.ActualWidth:F0}x{heightImageScroller.ActualHeight:F0};settings={heightImageDisplaySettings.IsExpanded}/{heightImageDisplaySettings.Visibility};status={heightImageStatus.Visibility};manual={heightImageManualControls.Visibility};range={heightImageRangeSummary.Visibility};hint={heightImageRoiHint.Visibility};palette={heightImagePalette.ActualHeight:F0};invalid={heightImageInvalidOverlay.ActualHeight:F0}");
            heightImageDisplaySettings.IsExpanded = true;
            embeddedHeightImageView.Measure(new Size(400, 360));
            embeddedHeightImageView.Arrange(new Rect(0, 0, 400, 360));
            embeddedHeightImageView.UpdateLayout();
            Check(
                "embedded Height Image retains bound display controls when settings expand",
                heightImageDisplaySettings.IsExpanded
                && heightImageDisplaySettings.Visibility == Visibility.Visible
                && heightImageDisplaySettingsToggle.IsChecked == true
                && heightImageDisplaySettingsPopup.IsOpen
                && heightImageManualControls.Visibility == Visibility.Visible
                && heightImageScroller.ActualHeight >= 250
                && heightImageDisplaySettings.Content is WrapPanel
                && heightImagePalette.SelectedItem is HeightImagePaletteChoice
                && BindingOperations.GetBindingExpression(heightImagePalette, Selector.SelectedItemProperty)?.ParentBinding.Path.Path == "SelectedPaletteChoice",
                $"settings={heightImageDisplaySettings.IsExpanded}/{heightImageDisplaySettings.Visibility};toggle={heightImageDisplaySettingsToggle.IsChecked};popup={heightImageDisplaySettingsPopup.IsOpen};manual={heightImageManualControls.Visibility};canvas={heightImageScroller.ActualHeight:F0};content={heightImageDisplaySettings.Content?.GetType().Name};selected={heightImagePalette.SelectedItem}");
            var heightSettingsPopupChild = heightImageDisplaySettingsPopup.Child as FrameworkElement ?? throw new InvalidOperationException("Height settings popup has no rendered content.");
            heightSettingsPopupChild.UpdateLayout();
            var heightRangeButtons = heightImageManualControls.Children.OfType<Button>().ToArray();
            Check("D40 Height Image range buttons wrap inside the actual settings popup without caption clipping",
                heightRangeButtons.Length == 2
                && heightImageDisplaySettings.Content is FrameworkElement heightRangeContent
                && heightImageManualControls.ActualWidth <= heightRangeContent.ActualWidth
                && heightRangeButtons.All(button => button.ActualWidth + button.Margin.Left + button.Margin.Right >= button.DesiredSize.Width
                    && button.TranslatePoint(new Point(button.ActualWidth, button.ActualHeight), heightImageManualControls).X <= heightImageManualControls.ActualWidth + 1
                    && button.TranslatePoint(new Point(button.ActualWidth, button.ActualHeight), heightImageManualControls).Y <= heightImageManualControls.ActualHeight + 1),
                $"controls={heightImageManualControls.ActualWidth:F0}x{heightImageManualControls.ActualHeight:F0};buttons={string.Join(";", heightRangeButtons.Select(button => $"{button.Content}:{button.ActualWidth:F0}x{button.ActualHeight:F0}/{button.DesiredSize.Width:F0}"))}");
            OpenVisionLab.ThreeD.Viewer.Rendering.WpfScreenshotCapture.Save(
                OpenVisionLab.ThreeD.Viewer.Rendering.WpfScreenshotCapture.Capture(heightSettingsPopupChild).Bitmap,
                Path.ChangeExtension(reportPath, ".d40-height-popup.png"));
            heightImageDisplaySettings.SetCurrentValue(Expander.IsExpandedProperty, false);
            layoutTestWindow.Content = contentBeforeHeightPopup;
            layoutTestWindow.Dispatcher.Invoke(DispatcherPriority.Loaded, new Action(layoutTestWindow.UpdateLayout));

            var originalLanguage = OpenVisionLanguageService.CurrentLanguage;
            var selectedHeightPalette = heightImageViewModel.SelectedPalette;
            var mainGeometryStyle = mainViewer.DisplayEditor.SelectedGeometryStyle;
            var mainColorMap = mainViewer.DisplayEditor.SelectedColorMap;
            try
            {
                OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
                embeddedHeightImageView.UpdateLayout();
                layoutWorkspaceView.UpdateLayout();
                var englishHeightChoice = heightImagePalette.SelectedItem as HeightImagePaletteChoice;
                Check(
                    "Viewer selections survive English localization without validation errors",
                    heightImageViewModel.SelectedPalette == selectedHeightPalette
                    && englishHeightChoice?.Value == selectedHeightPalette
                    && englishHeightChoice.DisplayName == "Height"
                    && !Validation.GetHasError(heightImagePalette)
                    && mainViewerGeometry.SelectedItem as string == mainGeometryStyle
                    && mainPresentationPalette.SelectedItem as string == mainColorMap
                    && mainViewer.DisplayEditor.SelectedGeometryStyle == mainGeometryStyle
                    && mainViewer.DisplayEditor.SelectedColorMap == mainColorMap,
                    $"heightImage={englishHeightChoice?.DisplayName};geometry={mainViewerGeometry.SelectedItem};palette={mainPresentationPalette.SelectedItem}");

                OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
                embeddedHeightImageView.UpdateLayout();
                layoutWorkspaceView.UpdateLayout();
                var selectedHeightChoice = heightImagePalette.SelectedItem as HeightImagePaletteChoice;
                Check(
                    "Viewer selections survive Korean localization without validation errors",
                    heightImageViewModel.SelectedPalette == selectedHeightPalette
                    && selectedHeightChoice?.Value == selectedHeightPalette
                    && selectedHeightChoice.DisplayName == "높이"
                    && !Validation.GetHasError(heightImagePalette)
                    && mainViewerGeometry.SelectedItem as string == mainGeometryStyle
                    && mainPresentationPalette.SelectedItem as string == mainColorMap
                    && mainViewer.DisplayEditor.SelectedGeometryStyle == mainGeometryStyle
                    && mainViewer.DisplayEditor.SelectedColorMap == mainColorMap,
                    $"heightImage={selectedHeightChoice?.DisplayName};geometry={mainViewer.DisplayEditor.SelectedGeometryStyle};palette={mainViewer.DisplayEditor.SelectedColorMap}");
            }
            finally
            {
                OpenVisionLanguageService.SetLanguage(originalLanguage, save: false);
            }
            layoutWorkspaceView.MainViewerContent = null;
            Check("detaching Shell restores the standalone Fit-all control",
                mainViewer.WorkspaceContextMenu is null && ((Button)mainViewer.FindName("ViewerFitAllButton")).Visibility == Visibility.Visible,
                $"menu={mainViewer.WorkspaceContextMenu is null};fit={((Button)mainViewer.FindName("ViewerFitAllButton")).Visibility}");

            var mainInspectionBeforePresentation = mainViewer.HostState.Inspection;
            var auxiliaryInspectionBeforePresentation = auxiliaryViewer.HostState.Inspection;
            var presentationApplied =
                mainViewer.TrySetSelectedColorMap("Solid")
                && auxiliaryViewer.TrySetSelectedColorMap("Height")
                && mainViewer.TrySetSelectionOverlayVisible(false)
                && auxiliaryViewer.TrySetSelectionOverlayVisible(true)
                && mainViewer.TrySetResultOverlayVisible(true)
                && auxiliaryViewer.TrySetResultOverlayVisible(false)
                && mainViewer.TrySetMeasurementVisible(false)
                && auxiliaryViewer.TrySetMeasurementVisible(true);
            var mainPresentation = mainViewer.HostState.Presentation;
            var auxiliaryPresentation = auxiliaryViewer.HostState.Presentation;
            Check(
                "two real Viewer instances keep independent palette and overlay state",
                presentationApplied
                && mainPresentation.SelectedColorMap == "Solid"
                && auxiliaryPresentation.SelectedColorMap == "Height"
                && !mainViewer.HostState.SelectionOverlayVisible
                && auxiliaryViewer.HostState.SelectionOverlayVisible
                && mainPresentation.ResultOverlayVisible
                && !auxiliaryPresentation.ResultOverlayVisible
                && !mainPresentation.MeasurementVisible
                && auxiliaryPresentation.MeasurementVisible,
                $"main={mainPresentation.SelectedColorMap};aux={auxiliaryPresentation.SelectedColorMap}");

            var mainCamera = new ViewerCameraState(
                18.0,
                36.0,
                7.25,
                1.0,
                2.0,
                3.0,
                ViewerProjectionMode.Perspective,
                10.0);
            var beforeAuxiliaryCamera = auxiliaryViewer.CaptureCameraState();
            Check(
                "camera state remains independent before an explicit link copy",
                mainViewer.TryApplyCameraState(mainCamera)
                && auxiliaryViewer.CaptureCameraState() == beforeAuxiliaryCamera,
                $"main={mainViewer.CaptureCameraState()}|aux={beforeAuxiliaryCamera}");
            Check(
                "explicit camera copy synchronizes Main to Auxiliary",
                auxiliaryViewer.TryApplyCameraState(mainViewer.CaptureCameraState())
                && auxiliaryViewer.CaptureCameraState() == mainViewer.CaptureCameraState(),
                $"main={mainViewer.CaptureCameraState()}|aux={auxiliaryViewer.CaptureCameraState()}");

            var beforeInvalidCamera = auxiliaryViewer.CaptureCameraState();
            Check(
                "invalid camera copy fails closed",
                !auxiliaryViewer.TryApplyCameraState(
                    mainCamera with { OrthographicHeight = double.NaN })
                && auxiliaryViewer.CaptureCameraState() == beforeInvalidCamera,
                auxiliaryViewer.CaptureCameraState().ToString());

            Check(
                "display and camera actions do not execute inspection",
                mainViewer.HostState.Inspection.PublishedResultCount == mainInspectionBeforePresentation.PublishedResultCount
                && auxiliaryViewer.HostState.Inspection.PublishedResultCount == auxiliaryInspectionBeforePresentation.PublishedResultCount
                && mainViewer.HostState.Inspection.PublishedResultCount == 0
                && auxiliaryViewer.HostState.Inspection.PublishedResultCount == 0,
                $"mainPreview={mainViewer.HostState.Inspection.PreviewStatus};auxPreview={auxiliaryViewer.HostState.Inspection.PreviewStatus};mainPublished={mainViewer.HostState.Inspection.PublishedResultCount};auxPublished={auxiliaryViewer.HostState.Inspection.PublishedResultCount}");

            var recipePath = Path.Combine(Directory.GetCurrentDirectory(), "3D", "Samples", "ThicknessCouponV1", "inspection-recipe.ov3d-recipe.json");
            using var authoredWorkbench = new ToolWorkbenchViewModel(Path.Combine(Path.GetDirectoryName(reportPath)!, "viewer-recent.json"));
            var recipeOpened = authoredWorkbench.TryOpenTeachingRecipe(recipePath, out _);
            layoutWorkspaceView.MainViewerContent = mainViewer;
            layoutWorkspaceView.DataContext = authoredWorkbench;
            layoutWorkspaceView.UpdateLayout();
            Check("embedded Viewer keeps one direct Fit-all action and retains ROI navigation",
                mainViewer.WorkspaceContextMenu is not null && ((Button)mainViewer.FindName("ViewerFitAllButton")).Visibility == Visibility.Collapsed
                && ((Button)mainViewer.FindName("ViewerFitRoiButton")).Visibility == Visibility.Visible,
                $"fit={((Button)mainViewer.FindName("ViewerFitAllButton")).Visibility};roi={((Button)mainViewer.FindName("ViewerFitRoiButton")).Visibility}");
            var viewerSourceLoaded = mainViewer.LoadC3DSource(authoredWorkbench.Source.Path);
            var heightPaletteSelected = mainViewer.TrySetSelectedColorMap("Height");
            layoutWorkspaceView.UpdateLayout();
            Check(
                "opening a saved recipe displays the main slot's real C3D source",
                recipeOpened
                && viewerSourceLoaded
                && mainViewer.CurrentC3DSourcePath == authoredWorkbench.Source.Path,
                $"opened={recipeOpened};viewerLoaded={viewerSourceLoaded};viewer={mainViewer.CurrentC3DSourcePath};source={authoredWorkbench.Source.Path}");
            mainPresentationBar.Presentation.OpenDisplaySettingsCommand.Execute(null);
            layoutTestWindow.Dispatcher.Invoke(DispatcherPriority.Loaded, new Action(layoutTestWindow.UpdateLayout));
            var distributionRadio = OpenVisionLab.ThreeD.Shell.Verification.Smoke.ShellSmokeArtifacts.FindVisualDescendants<RadioButton>(viewerDisplaySettingsPopup.Child)
                .Single(radio => AutomationProperties.GetAutomationId(radio) == "ViewerHeightRangeDistribution");
            distributionRadio.SetCurrentValue(ToggleButton.IsCheckedProperty, true);
            System.Windows.Input.Keyboard.Focus(distributionRadio);
            distributionRadio.RaiseEvent(new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice, PresentationSource.FromVisual(distributionRadio), 0, System.Windows.Input.Key.Escape)
                { RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent });
            layoutTestWindow.Dispatcher.Invoke(DispatcherPriority.Loaded, new Action(layoutTestWindow.UpdateLayout));
            Check("V5 Escape from the checked range radio closes the popup and preserves the applied range",
                !viewerDisplaySettingsPopup.IsOpen && !mainPresentationBar.Presentation.IsDisplaySettingsOpen
                && mainViewer.HostState.Presentation.HeightDisplayOptions.RangeMode == "robust"
                && mainViewer.HostState.Inspection.PublishedResultCount == 0,
                $"popup={viewerDisplaySettingsPopup.IsOpen};model={mainPresentationBar.Presentation.IsDisplaySettingsOpen};mode={mainViewer.HostState.Presentation.HeightDisplayOptions.RangeMode}");
            mainPresentationBar.Presentation.OpenDisplaySettingsCommand.Execute(null);
            layoutTestWindow.Dispatcher.Invoke(DispatcherPriority.Loaded, new Action(layoutTestWindow.UpdateLayout));
            var ownerEscapeMinimum = mainViewer.HostState.Presentation.C3DHeightColorMinimumRaw;
            var ownerEscapeMaximum = mainViewer.HostState.Presentation.C3DHeightColorMaximumRaw;
            distributionRadio.SetCurrentValue(ToggleButton.IsCheckedProperty, true);
            layoutTestWindow.RaiseEvent(new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice, PresentationSource.FromVisual(layoutTestWindow), 0, System.Windows.Input.Key.Escape)
                { RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent });
            layoutTestWindow.Dispatcher.Invoke(DispatcherPriority.Loaded, new Action(layoutTestWindow.UpdateLayout));
            Check("V5 owner Window Escape closes radio popup even when keyboard input returns to its owner",
                !viewerDisplaySettingsPopup.IsOpen && !mainPresentationBar.Presentation.IsDisplaySettingsOpen
                && mainViewer.HostState.Presentation.C3DHeightColorMinimumRaw == ownerEscapeMinimum
                && mainViewer.HostState.Presentation.C3DHeightColorMaximumRaw == ownerEscapeMaximum && !authoredWorkbench.IsDirty,
                $"popup={viewerDisplaySettingsPopup.IsOpen};range={ownerEscapeMinimum}..{ownerEscapeMaximum};dirty={authoredWorkbench.IsDirty}");
            var workspaceLayoutKey = OpenVisionLab.ThreeD.Presentation.Behaviors.ViewerKeyboardBehavior.GetBindings(layoutWorkspaceView)
                .Single(binding => binding.Key == System.Windows.Input.Key.D4 && binding.Modifiers == System.Windows.Input.ModifierKeys.Control);
            Check("V5 loaded workspace keyboard command inherits the actual Workbench owner",
                ReferenceEquals(workspaceLayoutKey.Command, authoredWorkbench.SetFourViewerLayoutCommand), $"command={workspaceLayoutKey.Command}");
            layoutTestWindow.Activate();
            layoutTestWindow.Dispatcher.Invoke(DispatcherPriority.ContextIdle, new Action(layoutTestWindow.UpdateLayout));
            mainPresentationBar.Presentation.OpenDisplaySettingsCommand.Execute(null);
            mainPresentationBar.Presentation.IsBandedHeightColor = true;
            layoutTestWindow.Dispatcher.Invoke(DispatcherPriority.Loaded, new Action(layoutTestWindow.UpdateLayout));
            var bandSelector = (ComboBox)mainPresentationBar.FindName("ViewerHeightBandCountSelector");
            var bandCountBeforeEscape = bandSelector.SelectedItem;
            System.Windows.Input.Keyboard.Focus(bandSelector);
            bandSelector.SetCurrentValue(ComboBox.IsDropDownOpenProperty, true);
            layoutTestWindow.Dispatcher.Invoke(DispatcherPriority.Loaded, new Action(layoutTestWindow.UpdateLayout));
            Check("V5 nested Escape starts with both the settings popup and band dropdown actually open",
                bandSelector.IsDropDownOpen && viewerDisplaySettingsPopup.IsOpen,
                $"dropdown={bandSelector.IsDropDownOpen};popup={viewerDisplaySettingsPopup.IsOpen};focus={System.Windows.Input.Keyboard.FocusedElement?.GetType().Name}");
            layoutTestWindow.RaiseEvent(new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice, PresentationSource.FromVisual(layoutTestWindow), 0, System.Windows.Input.Key.Escape)
                { RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent });
            layoutTestWindow.Dispatcher.Invoke(DispatcherPriority.Loaded, new Action(layoutTestWindow.UpdateLayout));
            Check("V5 first Escape closes only the nested band dropdown and preserves its selected value",
                !bandSelector.IsDropDownOpen && viewerDisplaySettingsPopup.IsOpen && mainPresentationBar.Presentation.IsDisplaySettingsOpen
                && Equals(bandSelector.SelectedItem, bandCountBeforeEscape) && !authoredWorkbench.IsDirty,
                $"dropdown={bandSelector.IsDropDownOpen};popup={viewerDisplaySettingsPopup.IsOpen};band={bandSelector.SelectedItem}");
            layoutTestWindow.RaiseEvent(new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice, PresentationSource.FromVisual(layoutTestWindow), 0, System.Windows.Input.Key.Escape)
                { RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent });
            layoutTestWindow.Dispatcher.Invoke(DispatcherPriority.Loaded, new Action(layoutTestWindow.UpdateLayout));
            Check("V5 second Escape closes its parent popup and restores automatic dismissal",
                !viewerDisplaySettingsPopup.IsOpen && !mainPresentationBar.Presentation.IsDisplaySettingsOpen
                && !viewerDisplaySettingsPopup.StaysOpen && Equals(bandSelector.SelectedItem, bandCountBeforeEscape)
                && !authoredWorkbench.IsDirty, $"popup={viewerDisplaySettingsPopup.IsOpen};staysOpen={viewerDisplaySettingsPopup.StaysOpen};band={bandSelector.SelectedItem}");
            mainPresentationBar.Presentation.IsContinuousHeightColor = true;
            mainPresentationBar.Presentation.CloseDisplaySettingsCommand.Execute(null);
            Check("D40 pane role labels project actual input identity without changing candidates or execution",
                authoredWorkbench.MainViewerPane.IsInputSource && !authoredWorkbench.MainViewerPane.IsOutputSource
                && ReferenceEquals(((ContentControl)layoutWorkspaceView.FindName("MainPaneSourceRole")).Content, authoredWorkbench.MainViewerPane)
                && viewerDataButtons.All(button => ReferenceEquals(button.Command, authoredWorkbench.SetInspectionDataViewCommand))
                && OpenVisionLab.ThreeD.Shell.Verification.Smoke.ShellSmokeArtifacts.FindVisualDescendants<TextBlock>((ContentControl)layoutWorkspaceView.FindName("MainPaneSourceRole"))
                    .Any(text => text.Text == authoredWorkbench.Localization.Input && text.IsVisible)
                && !authoredWorkbench.IsDirty && !authoredWorkbench.IsOrderedRunRunning,
                $"input={authoredWorkbench.MainViewerPane.IsInputSource};output={authoredWorkbench.MainViewerPane.IsOutputSource};dirty={authoredWorkbench.IsDirty}");
            Check("V5 source pin captions follow the current pane identity while selection remains ID-based",
                Equals(((ComboBox)layoutWorkspaceView.FindName("MainViewerContentPin")).Tag, authoredWorkbench.MainViewerPane.SourceName)
                && Equals(((ComboBox)layoutWorkspaceView.FindName("AuxiliaryViewerContentPin")).Tag, authoredWorkbench.AuxiliaryViewerPane.SourceName)
                && ((ComboBox)layoutWorkspaceView.FindName("MainViewerContentPin")).SelectedValuePath == "Id"
                && ((ComboBox)layoutWorkspaceView.FindName("AuxiliaryViewerContentPin")).SelectedValuePath == "Id",
                $"main={authoredWorkbench.MainViewerPane.SourceName};auxiliary={authoredWorkbench.AuxiliaryViewerPane.SourceName}");
            using (var pendingWorkbench = new ToolWorkbenchViewModel())
            using (var pendingViewer = new OpenVisionThreeDViewerControl(loadDefaultSamples: false) { SidePanelsVisible = false })
            using (var readyViewer = new OpenVisionThreeDViewerControl(loadDefaultSamples: false) { SidePanelsVisible = false })
            using (var pendingLinks = new OpenVisionLab.ThreeD.Shell.Coordination.ViewerWorkspaceLinkCoordinator())
            {
                pendingWorkbench.SetC3DSource(authoredWorkbench.Source.Path, markDirty: false);
                pendingWorkbench.AuxiliaryViewerPane.UpdateSource(new ViewerWorkspaceCandidateItem(pendingWorkbench.Source.Id, "Input",
                    ViewerWorkspaceCandidateKind.ThreeDArtifact, authoredWorkbench.Source.Path, "C3D", "Ready", true), ViewerPaneKind.ThreeD, true);
                var previousViewerPath = Path.Combine(Path.GetDirectoryName(reportPath)!, "profile-previous.C3D");
                File.Copy(authoredWorkbench.Source.Path, previousViewerPath, overwrite: true);
                pendingViewer.LoadC3DSource(previousViewerPath);
                readyViewer.LoadC3DSource(authoredWorkbench.Source.Path);
                pendingLinks.SetWorkbench(pendingWorkbench);
                pendingLinks.SetMainViewer(readyViewer);
                pendingLinks.RestoreDisplayProfiles(new Dictionary<string, HeightDisplayProfile>
                {
                    ["main/3d"] = new(new HeightDisplayOptions { Palette = "Grayscale", IsLinked = false }, new Dictionary<string, string>(), string.Empty),
                    ["auxiliary/3d"] = new(new HeightDisplayOptions { Palette = "Thermal", IsLinked = false }, new Dictionary<string, string>(), string.Empty)
                });
                pendingLinks.SetAuxiliaryViewer(pendingViewer);
                var loadedPending = pendingViewer.LoadC3DSource(authoredWorkbench.Source.Path);
                pendingLinks.Refresh();
                Check("V5 a lazily created 3D host restores its palette after source capabilities become available",
                    loadedPending && pendingViewer.HostState.Presentation.SelectedColorMap == "Thermal"
                    && !pendingViewer.HostState.Presentation.HeightDisplayOptions.IsLinked
                    && readyViewer.HostState.Presentation.SelectedColorMap == "Grayscale" && !readyViewer.HostState.Presentation.HeightDisplayOptions.IsLinked,
                    $"loaded={loadedPending};palette={pendingViewer.HostState.Presentation.SelectedColorMap};linked={pendingViewer.HostState.Presentation.HeightDisplayOptions.IsLinked};main={readyViewer.HostState.Presentation.SelectedColorMap}");
            }
            var fourLayoutButton = OpenVisionLab.ThreeD.Shell.Verification.Smoke.ShellSmokeArtifacts.FindVisualDescendants<ToggleButton>(layoutWorkspaceView)
                .Single(button => AutomationProperties.GetAutomationId(button) == "ViewerFourLayoutButton");
            var cubeCameraBefore = mainViewer.CaptureCameraState();
            var cubeViewModel = (OpenVisionLab.ThreeD.Viewer.ViewModels.MainWindowViewModel)mainViewer.DataContext;

            var roiViewport = (SharpGL.WPF.OpenGLControl)mainViewer.FindName("Viewport");
            var roiDrawCount = 0;
            void CountRoiDraw(object? sender, SharpGL.WPF.OpenGLRoutedEventArgs args) => roiDrawCount++;
            var roiFilterBefore = authoredWorkbench.ViewerDisplayedRoiIds;
            var roiSelectionBefore = cubeViewModel.SelectedTeachingSelectionId;
            roiViewport.OpenGLDraw += CountRoiDraw;
            try
            {
                Check("D70 ROI render verification uses the loaded real viewport", roiViewport.IsLoaded, $"loaded={roiViewport.IsLoaded}");
                mainViewer.SetTeachingSelectionDisplayFilter(null);
                var drawBefore = roiDrawCount;
                mainViewer.SetTeachingSelectionDisplayFilter(null);
                Check("D70 identical all-ROI filter skips synchronous render", roiDrawCount == drawBefore && cubeViewModel.IsTeachingSelectionDisplayed("any-roi"), $"drawDelta={roiDrawCount - drawBefore}");
                mainViewer.SetTeachingSelectionDisplayFilter([]);
                Check("D70 empty ROI filter renders and hides selections", roiDrawCount > drawBefore && !cubeViewModel.IsTeachingSelectionDisplayed("any-roi"), $"drawDelta={roiDrawCount - drawBefore}");
                drawBefore = roiDrawCount;
                mainViewer.SetTeachingSelectionDisplayFilter(Array.Empty<string>());
                Check("D70 identical empty ROI filter skips synchronous render", roiDrawCount == drawBefore && !cubeViewModel.IsTeachingSelectionDisplayed("any-roi"), $"drawDelta={roiDrawCount - drawBefore}");
                var filterDraft = new[] { "selection.pad-01.reference-roi" };
                mainViewer.SetTeachingSelectionDisplayFilter(filterDraft);
                Check("D70 changed ROI filter renders its actual visibility", roiDrawCount > drawBefore && cubeViewModel.IsTeachingSelectionDisplayed(filterDraft[0]) && !cubeViewModel.IsTeachingSelectionDisplayed("other-roi"), $"drawDelta={roiDrawCount - drawBefore}");
                drawBefore = roiDrawCount;
                mainViewer.SetTeachingSelectionDisplayFilter(["SELECTION.PAD-01.REFERENCE-ROI"]);
                Check("D70 case-equivalent ROI filter skips synchronous render", roiDrawCount == drawBefore && cubeViewModel.IsTeachingSelectionDisplayed(filterDraft[0]), $"drawDelta={roiDrawCount - drawBefore}");
                filterDraft[0] = "other-roi";
                Check("D70 ROI display filter retains its input snapshot", cubeViewModel.IsTeachingSelectionDisplayed("selection.pad-01.reference-roi") && !cubeViewModel.IsTeachingSelectionDisplayed(filterDraft[0]), "caller-array mutation has no effect");
                mainViewer.SetTeachingSelectionDisplayFilter(filterDraft);
                Check("D70 changed snapshot renders and replaces visible ROI IDs", roiDrawCount > drawBefore && cubeViewModel.IsTeachingSelectionDisplayed(filterDraft[0]) && !cubeViewModel.IsTeachingSelectionDisplayed("selection.pad-01.reference-roi"), $"drawDelta={roiDrawCount - drawBefore}");
                drawBefore = roiDrawCount;
                mainViewer.SetTeachingSelectionDisplayFilter(null);
                Check("D70 null filter restores all ROI visibility with a render", roiDrawCount > drawBefore && cubeViewModel.IsTeachingSelectionDisplayed("any-roi"), $"drawDelta={roiDrawCount - drawBefore}");
                mainViewer.SetSelectedTeachingSelection(null);
                drawBefore = roiDrawCount;
                mainViewer.SetSelectedTeachingSelection(null);
                Check("D70 unchanged cleared ROI selection skips synchronous render", roiDrawCount == drawBefore && cubeViewModel.SelectedTeachingSelectionId is null, $"drawDelta={roiDrawCount - drawBefore}");
                mainViewer.SetSelectedTeachingSelection("selection.pad-01.reference-roi");
                Check("D70 changed ROI selection renders its final state once without inspection", roiDrawCount == drawBefore + 1 && cubeViewModel.SelectedTeachingSelectionId == "selection.pad-01.reference-roi" && mainViewer.HostState.Inspection.PublishedResultCount == 0, $"drawDelta={roiDrawCount - drawBefore}");
                drawBefore = roiDrawCount;
                mainViewer.SetSelectedTeachingSelection("SELECTION.PAD-01.REFERENCE-ROI");
                Check("D70 case-equivalent ROI selection skips synchronous render", roiDrawCount == drawBefore && cubeViewModel.SelectedTeachingSelectionId == "selection.pad-01.reference-roi", $"drawDelta={roiDrawCount - drawBefore}");
                mainViewer.SetSelectedTeachingSelection(null);
                Check("D70 clearing ROI selection renders the restored state once", roiDrawCount == drawBefore + 1 && cubeViewModel.SelectedTeachingSelectionId is null, $"drawDelta={roiDrawCount - drawBefore}");
                PropertyChangedEventHandler rejectSelectionObserver = (_, args) =>
                {
                    if (args.PropertyName == nameof(OpenVisionLab.ThreeD.Viewer.ViewModels.MainWindowViewModel.SelectedTeachingRoiDisplayHeightOffset))
                        throw new InvalidOperationException("Owned selection observer failure");
                };
                var selectionObserverFailed = false;
                cubeViewModel.PropertyChanged += rejectSelectionObserver;
                try { mainViewer.SetSelectedTeachingSelection("selection.pad-01.reference-roi"); }
                catch (InvalidOperationException exception) when (exception.Message == "Owned selection observer failure") { selectionObserverFailed = true; }
                finally { cubeViewModel.PropertyChanged -= rejectSelectionObserver; }
                drawBefore = roiDrawCount;
                mainViewer.SetSelectedTeachingSelection(null);
                Check("selection rendering recovers synchronously after a throwing notification observer",
                    selectionObserverFailed && roiDrawCount == drawBefore + 1 && cubeViewModel.SelectedTeachingSelectionId is null
                    && mainViewer.HostState.Inspection.PublishedResultCount == 0, $"observerFailed={selectionObserverFailed};drawDelta={roiDrawCount - drawBefore}");
            }
            finally
            {
                mainViewer.SetTeachingSelectionDisplayFilter(roiFilterBefore);
                mainViewer.SetSelectedTeachingSelection(roiSelectionBefore);
                roiViewport.OpenGLDraw -= CountRoiDraw;
            }
            var cubeRight = (Button)mainViewer.FindName("OrientationCubeRight");
            var cubeProjection = (Button)mainViewer.FindName("CubeProjectionToggle");
            var paneMenuHost = (ContentControl)layoutWorkspaceView.FindName("MainPaneMenuHost");
            var nativeMenu = (Menu)mainViewer.FindName("ViewerViewMenu");
            var nativeToolbar = (Border)mainViewer.FindName("ViewerAdvancedToolbar");
            var cubePanel = (StackPanel)mainViewer.FindName("OrientationCubePanel");
            var cubeViewport = (System.Windows.Controls.Viewport3D)mainViewer.FindName("OrientationCubeViewport");
            var nativeFitRoi = (Button)mainViewer.FindName("ViewerFitRoiButton");
            var floatingTools = (Border)mainPresentationBar.FindName("ViewportToolsSurface");
            var basicTools = OpenVisionLab.ThreeD.Shell.Verification.Smoke.ShellSmokeArtifacts.FindVisualDescendants<ButtonBase>(floatingTools)
                .Where(button => AutomationProperties.GetAutomationId(button) is "ViewerRotate" or "ViewerMove" or "ViewerRoiCapture" or "ViewerMeasure" or "ViewerFit" or "ViewerFitRoiDirect").ToArray();
            var colorTool = viewerDisplaySettingsButton;
            Check("D41 HTML-size floating toolbar has seven 28px controls without an extra caption",
                basicTools.Length == 6 && basicTools.All(button => button.ActualWidth == 28 && button.ActualHeight == 28)
                && colorTool.ActualWidth == 28 && colorTool.ActualHeight == 28
                && floatingTools.ActualWidth == 234 && floatingTools.ActualHeight == 36
                && !OpenVisionLab.ThreeD.Shell.Verification.Smoke.ShellSmokeArtifacts.FindVisualDescendants<TextBlock>(floatingTools)
                    .Any(text => text.IsVisible && BindingOperations.GetBindingExpression(text, TextBlock.TextProperty)?.ParentBinding.Path.Path == "SlotLabel"),
                $"tools={basicTools.Length + 1};surface={floatingTools.ActualWidth}x{floatingTools.ActualHeight};buttons={string.Join(',', basicTools.Select(button => $"{button.ActualWidth}x{button.ActualHeight}"))}");
            Check("D41 ROI and fit buttons retain existing command and mutable-state owners",
                ReferenceEquals(mainPresentationBar.RoiCaptureCommand, authoredWorkbench.BeginTeachingSelectionCaptureCommand)
                && ReferenceEquals(mainPresentationBar.FitRoiCommand, cubeViewModel.FitRoiCommand)
                && ReferenceEquals(basicTools.Single(button => AutomationProperties.GetAutomationId(button) == "ViewerRoiCapture").Command, authoredWorkbench.BeginTeachingSelectionCaptureCommand)
                && ReferenceEquals(basicTools.Single(button => AutomationProperties.GetAutomationId(button) == "ViewerFitRoiDirect").Command, cubeViewModel.FitRoiCommand)
                && !authoredWorkbench.IsDirty && !authoredWorkbench.IsOrderedRunRunning && mainViewer.HostState.Inspection.PublishedResultCount == 0,
                $"capture={mainPresentationBar.RoiCaptureCommand is not null};fit={mainPresentationBar.FitRoiCommand is not null};dirty={authoredWorkbench.IsDirty}");
            Check("D41 removes the long lower-left ROI banner and keeps a fixed compact footer",
                !OpenVisionLab.ThreeD.Shell.Verification.Smoke.ShellSmokeArtifacts.FindVisualDescendants<FrameworkElement>(mainViewer).Any(element => AutomationProperties.GetAutomationId(element) == "ViewerSelectedRoiStatus")
                && viewerBottomStatus.ActualHeight == 24 && coordinateStatus.FontSize == 11.5
                && cubeViewModel.DisplayedCoordinateCaption.StartsWith("X ", StringComparison.Ordinal),
                $"footer={viewerBottomStatus.ActualHeight};coordinate={cubeViewModel.DisplayedCoordinateCaption}");
            var cubeHome = (Button)mainViewer.FindName("CubeHomeButton");
            var cubeProjectionButtons = (StackPanel)mainViewer.FindName("CubeProjectionButtons");
            Check("D41 perspective and reset remain together below the Compact cube",
                cubeProjection.ActualHeight == 20 && cubeHome.ActualHeight == 20
                && cubeProjection.TranslatePoint(new Point(), cubeProjectionButtons).Y == cubeHome.TranslatePoint(new Point(), cubeProjectionButtons).Y
                && cubeProjectionButtons.ActualHeight == 20 && ReferenceEquals(cubeHome.Command, cubeViewModel.SetCameraOrientationCommand),
                $"buttons={cubeProjectionButtons.ActualWidth}x{cubeProjectionButtons.ActualHeight};projection={cubeProjection.ActualWidth};home={cubeHome.ActualWidth}");
            var projectionToggleBefore = mainViewer.CaptureCameraState();
            cubeProjection.Command!.Execute(null);
            mainViewer.UpdateLayout();
            Check("D41 orthographic caption retains one projection/reset row",
                cubeViewModel.IsOrthographicView && cubeProjectionButtons.ActualHeight == 20
                && cubeProjection.TranslatePoint(new Point(), cubeProjectionButtons).Y == cubeHome.TranslatePoint(new Point(), cubeProjectionButtons).Y,
                $"buttons={cubeProjectionButtons.ActualWidth}x{cubeProjectionButtons.ActualHeight};projection={((TextBlock)mainViewer.FindName("CubeOrthographicCaption")).Text}");
            var orthographicToggleState = mainViewer.CaptureCameraState();
            Check("D42 projection toggle preserves orientation, target and target-plane scale",
                orthographicToggleState.ProjectionMode == ViewerProjectionMode.Orthographic
                && orthographicToggleState.YawDegrees == projectionToggleBefore.YawDegrees
                && orthographicToggleState.PitchDegrees == projectionToggleBefore.PitchDegrees
                && orthographicToggleState.TargetX == projectionToggleBefore.TargetX
                && orthographicToggleState.TargetY == projectionToggleBefore.TargetY
                && orthographicToggleState.TargetZ == projectionToggleBefore.TargetZ
                && orthographicToggleState.Distance == projectionToggleBefore.Distance
                && Math.Abs(orthographicToggleState.OrthographicHeight - projectionToggleBefore.Distance * 2 * Math.Tan(Math.PI / 8)) < 1e-8,
                $"before={projectionToggleBefore};after={orthographicToggleState}");
            var auxiliaryCameraBeforeToggle = auxiliaryViewer.CaptureCameraState();
            Check("D42 generic orthographic camera transfers through the existing host contract",
                auxiliaryViewer.TryApplyCameraState(orthographicToggleState) && auxiliaryViewer.CaptureCameraState() == orthographicToggleState
                && auxiliaryViewer.HostState.Inspection.IsOrthographicView && !auxiliaryViewer.HostState.Inspection.IsTopOrthographicView,
                $"camera={auxiliaryViewer.CaptureCameraState()}");
            auxiliaryViewer.TryApplyCameraState(auxiliaryCameraBeforeToggle);
            var projectWorld = typeof(OpenVisionThreeDViewerControl).GetMethod("TryProjectWorldPositionToViewport", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            var pickRayMethod = typeof(OpenVisionThreeDViewerControl).GetMethod("CreatePickRay", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            var projectedWorld = new System.Numerics.Vector3((float)orthographicToggleState.TargetX + 0.1f, (float)orthographicToggleState.TargetY, (float)orthographicToggleState.TargetZ + 0.2f);
            object[] projectArguments = [projectedWorld, new Point()];
            var projected = (bool)projectWorld.Invoke(mainViewer, projectArguments)!;
            var pickRay = ((System.Numerics.Vector3 Origin, System.Numerics.Vector3 Direction))pickRayMethod.Invoke(mainViewer, [projectArguments[1]])!;
            Check("D42 generic orthographic rendering and picking use the same projection",
                projected && System.Numerics.Vector3.Cross(projectedWorld - pickRay.Origin, pickRay.Direction).Length() < 0.001f,
                $"screen={projectArguments[1]};ray={pickRay};world={projectedWorld}");
            cubeViewModel.ZoomCamera(0.5);
            cubeViewModel.ExitTopOrthographicForOrbit();
            Check("D42 zoom and orbit do not exit generic orthographic projection",
                cubeViewModel.IsOrthographicView && !cubeViewModel.IsTopOrthographicView
                && cubeViewModel.OrthographicHeight == orthographicToggleState.OrthographicHeight * 0.5
                && cubeViewModel.YawDegrees == orthographicToggleState.YawDegrees && cubeViewModel.PitchDegrees == orthographicToggleState.PitchDegrees,
                $"camera={mainViewer.CaptureCameraState()}");
            cubeProjection.Command.Execute(null);
            Check("D42 return to perspective keeps the orthographic zoom and pan without inspection",
                cubeViewModel.IsPerspectiveView && Math.Abs(cubeViewModel.CameraDistance - projectionToggleBefore.Distance * 0.5) < 1e-8
                && cubeViewModel.CameraTargetX == projectionToggleBefore.TargetX && cubeViewModel.CameraTargetZ == projectionToggleBefore.TargetZ
                && !authoredWorkbench.IsDirty && !authoredWorkbench.IsOrderedRunRunning && !authoredWorkbench.IsSelectedStepPreviewRunning
                && mainViewer.HostState.Inspection.PublishedResultCount == mainInspectionBeforePresentation.PublishedResultCount,
                $"camera={mainViewer.CaptureCameraState()};dirty={authoredWorkbench.IsDirty}");
            mainViewer.TryApplyCameraState(orthographicToggleState);
            var cubeLanguageBefore = OpenVisionLanguageService.CurrentLanguage;
            try
            {
                OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
                mainViewer.Dispatcher.Invoke(DispatcherPriority.Loaded, new Action(mainViewer.UpdateLayout));
                var projectionBounds = cubeProjection.TransformToAncestor((Grid)mainViewer.FindName("ViewportSurface")).TransformBounds(new Rect(cubeProjection.RenderSize));
                var homeBounds = cubeHome.TransformToAncestor((Grid)mainViewer.FindName("ViewportSurface")).TransformBounds(new Rect(cubeHome.RenderSize));
                Check("D41 English projection/reset captions follow the compact contract on one row",
                    ((TextBlock)mainViewer.FindName("CubeOrthographicCaption")).Text == "Ortho"
                    && cubeHome.Content as string == OpenVisionLab.ThreeD.Viewer.Localization.ViewerLocalization.Shared.Resolve("ThreeD.Viewer.HomeView", "초기화", "Home")
                    && cubeProjectionButtons.ActualHeight == 20 && projectionBounds.Top == homeBounds.Top
                    && projectionBounds.Left >= 0 && homeBounds.Right <= nativeViewport.ActualWidth,
                    $"buttons={cubeProjectionButtons.ActualWidth}x{cubeProjectionButtons.ActualHeight};projection={projectionBounds};home={homeBounds};caption={cubeHome.Content}");
            }
            finally
            {
                OpenVisionLanguageService.SetLanguage(cubeLanguageBefore, save: false);
                mainViewer.TryApplyCameraState(cubeCameraBefore);
                mainViewer.UpdateLayout();
            }
            mainViewer.FitAll();
            mainViewer.UpdateLayout();
            var floorLabels = (Canvas)mainViewer.FindName("ViewerFloorLabels");
            var refreshFloorLabels = typeof(OpenVisionThreeDViewerControl).GetMethod("UpdateC3DFloorLabels", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            refreshFloorLabels.Invoke(mainViewer, null);
            mainViewer.UpdateLayout();
            var floorTicks = floorLabels.Children.OfType<TextBlock>().Skip(2).ToArray();
            var floorBefore = mainPresentationBar.Presentation!.FloorGridVisible;
            mainPresentationBar.Presentation.FloorGridVisible = false;
            refreshFloorLabels.Invoke(mainViewer, null);
            Check("D42 floor display option clears grid labels through the existing display revision",
                !mainViewer.HostState.Presentation.FloorGridVisible && floorLabels.Visibility == Visibility.Collapsed
                && floorLabels.Children.OfType<TextBlock>().All(text => text.Visibility == Visibility.Collapsed)
                && !authoredWorkbench.IsDirty && mainViewer.HostState.Inspection.PublishedResultCount == 0,
                $"floor={floorLabels.Visibility};display={mainViewer.HostState.Presentation.FloorGridVisible}");
            mainPresentationBar.Presentation.FloorGridVisible = floorBefore;
            refreshFloorLabels.Invoke(mainViewer, null);
            mainViewer.UpdateLayout();
            Check("D42 floor option restores dimensional labels without camera or recipe changes",
                mainViewer.HostState.Presentation.FloorGridVisible && floorTicks.Count(text => text.IsVisible) >= 4 && !authoredWorkbench.IsDirty,
                $"visible={floorTicks.Count(text => text.IsVisible)}");
            var visibleFloorBounds = floorLabels.Children.OfType<TextBlock>().Where(text => text.IsVisible)
                .Select(text => text.TransformToAncestor((Grid)mainViewer.FindName("ViewportSurface")).TransformBounds(new Rect(text.RenderSize))).ToArray();
            Check("D41 floor ticks use the saved 1280x840 input dimensions and camera projection",
                floorTicks.Select(text => text.Text).SequenceEqual(new[] { "0", "160", "320", "480", "640", "800", "960", "1120", "1280", "0", "160", "320", "480", "640", "800" })
                && floorTicks.Count(text => text.IsVisible) >= 4
                && visibleFloorBounds.All(bounds => bounds.Left >= 0 && bounds.Top >= 0 && bounds.Right <= nativeViewport.ActualWidth && bounds.Bottom <= nativeViewport.ActualHeight)
                && visibleFloorBounds.SelectMany((first, index) => visibleFloorBounds.Skip(index + 1).Select(second => first.IntersectsWith(second))).All(overlap => !overlap),
                $"ticks={string.Join(',', floorTicks.Select(text => text.Text))};visible={floorTicks.Count(text => text.IsVisible)};viewport={nativeViewport.ActualWidth}x{nativeViewport.ActualHeight};bounds={string.Join('|', visibleFloorBounds)}");
            cubeViewModel.C3DSampleVisible = false;
            refreshFloorLabels.Invoke(mainViewer, null);
            Check("D41 hiding C3D also clears its dimensional overlays without publishing inspection",
                floorLabels.Visibility == Visibility.Collapsed && floorLabels.Children.OfType<TextBlock>().All(text => text.Visibility == Visibility.Collapsed)
                && mainViewer.HostState.Inspection.PublishedResultCount == 0 && !authoredWorkbench.IsDirty,
                $"floor={floorLabels.Visibility};published={mainViewer.HostState.Inspection.PublishedResultCount}");
            cubeViewModel.C3DSampleVisible = true;
            mainViewer.TryApplyCameraState(cubeCameraBefore);
            mainViewer.UpdateLayout();
            Check("v3 real Viewer menu moves into its pane header without changing command ownership",
                ReferenceEquals(paneMenuHost.Content, nativeMenu) && ReferenceEquals(nativeMenu.DataContext, cubeViewModel)
                && ReferenceEquals(nativeFitRoi.Command, cubeViewModel.FitRoiCommand)
                && nativeToolbar.Visibility == Visibility.Collapsed && nativeMenu.ActualHeight == 28
                && cubePanel.Width == 80 && cubeViewport.Height == 80 && cubeProjection.ActualHeight == 20
                && !authoredWorkbench.IsDirty && !authoredWorkbench.IsOrderedRunRunning,
                $"menu={nativeMenu.ActualWidth}x{nativeMenu.ActualHeight};toolbar={nativeToolbar.Visibility};cube={cubePanel.Width}/{cubeViewport.Height};projection={cubeProjection.ActualHeight}");
            mainViewer.SetWorkspaceHeaderHost(null, false);
            mainViewer.UpdateLayout();
            Check("detached Viewer restores its own menu before a workspace reattaches it",
                paneMenuHost.Content is null && ReferenceEquals(nativeMenu.Parent, mainViewer.FindName("ViewerToolbarActions"))
                && nativeToolbar.Visibility == Visibility.Visible && ReferenceEquals(nativeMenu.DataContext, cubeViewModel)
                && mainViewer.CaptureCameraState() == cubeCameraBefore,
                $"parent={nativeMenu.Parent?.GetType().Name};header={paneMenuHost.Content is not null};toolbar={nativeToolbar.Visibility}");
            mainViewer.SetWorkspaceHeaderHost(paneMenuHost, true);
            using (var chromeLifetimeViewer = new OpenVisionThreeDViewerControl(loadDefaultSamples: false))
            {
                var lifetimeHeader = new ContentControl();
                chromeLifetimeViewer.SetWorkspaceHeaderHost(lifetimeHeader, false);
                chromeLifetimeViewer.Dispose();
                Check("native Viewer disposal releases the external pane menu host", lifetimeHeader.Content is null, $"host={lifetimeHeader.Content is not null}");
            }
            cubeRight.Command?.Execute(cubeRight.CommandParameter);
            var cubeRightApplied = cubeViewModel.YawDegrees == 90 && cubeViewModel.PitchDegrees == 0;
            cubeProjection.Command?.Execute(null);
            var cubeTopApplied = cubeViewModel.IsOrthographicView && cubeViewModel.YawDegrees == 90 && cubeViewModel.PitchDegrees == 0;
            cubeProjection.Command?.Execute(null);
            Check("v3 native cube commands and actual height range are bound without inspection",
                ReferenceEquals(cubeRight.Command, cubeViewModel.SetCameraOrientationCommand) && cubeRightApplied && cubeTopApplied
                && cubeViewModel.IsPerspectiveView && cubeViewModel.C3DHeightColorTickLabels.Count == 5
                && cubeViewModel.C3DHeightColorTickLabels[0] == cubeViewModel.C3DHeightColorMaximumRaw.ToString("F2", System.Globalization.CultureInfo.CurrentCulture)
                && cubeViewModel.C3DHeightColorTickLabels[4] == cubeViewModel.C3DHeightColorMinimumRaw.ToString("F2", System.Globalization.CultureInfo.CurrentCulture)
                && mainViewer.HostState.Inspection.PublishedResultCount == mainInspectionBeforePresentation.PublishedResultCount,
                $"right={cubeRightApplied};top={cubeTopApplied};projection={cubeViewModel.ProjectionMode};ticks={string.Join(',', cubeViewModel.C3DHeightColorTickLabels)}");
            var nativeWidthBeforeChrome = mainViewer.Width;
            var nativeHeightBeforeChrome = mainViewer.Height;
            var cameraBeforeChrome = mainViewer.CaptureCameraState();
            mainViewer.Width = 340;
            mainViewer.Height = 240;
            layoutTestWindow.Dispatcher.Invoke(DispatcherPriority.Render, new Action(layoutTestWindow.UpdateLayout));
            using (var signedViewer = new OpenVisionThreeDViewerControl(loadDefaultSamples: false) { SidePanelsVisible = false })
            {
                var hash = new string('A', 64);
                var signed = OpenVisionLab.ThreeD.Core.C3DSignedHeightDeltaArtifact.Create("step.delta", "output.delta", "source", hash, hash, 24,
                    "background", hash, hash, 24, "mm", "frame.c3d-grid-index", 2, 2, 0, 0,
                    [-8, 0, 4, double.NaN], 4, 3, 3, 1, 1, 1, 1, "Isolated WPF signed-map fixture");
                var signedFrame = C3DHeightImageFrame.Create(signed);
                Check("V5 signed 3D host exposes a zero-centred bound contract", signedViewer.ShowSignedDeltaFrame(signedFrame)
                    && signedViewer.HostState.Presentation.C3DHeightColorMinimumRaw == -8
                    && signedViewer.HostState.Presentation.C3DHeightColorMaximumRaw == 8
                    && signedViewer.HostState.Presentation.C3DHeightDistributionVisible,
                    $"path={signedViewer.CurrentC3DSourcePath};range={signedViewer.HostState.Presentation.C3DHeightColorMinimumRaw}..{signedViewer.HostState.Presentation.C3DHeightColorMaximumRaw}");
                var signedWindow = new Window { Owner = layoutTestWindow, Content = signedViewer, Width = 600, Height = 400,
                    Left = layoutTestWindow.Left, Top = layoutTestWindow.Top, WindowStartupLocation = WindowStartupLocation.Manual };
                try
                {
                    signedWindow.Show();
                    signedWindow.Dispatcher.Invoke(DispatcherPriority.Loaded, new Action(signedWindow.UpdateLayout));
                    var signedBar = (HeightLegendRangeBar)signedViewer.FindName("ViewerHeightLegendRangeBar");
                    Check("V5 loaded signed 3D control binds the actual signed host values", signedBar.Minimum == -8 && signedBar.Maximum == 8
                        && signedBar.Histogram?.Sum() == 3 && signedBar.Gradient is not null && signedBar.IsVisible && signedBar.Unit == "mm",
                        $"range={signedBar.Minimum}..{signedBar.Maximum};bins={signedBar.Histogram?.Sum()};visible={signedBar.IsVisible};unit={signedBar.Unit}");
                    OpenVisionLab.ThreeD.Viewer.Rendering.WpfScreenshotCapture.Save(
                        OpenVisionLab.ThreeD.Viewer.Rendering.WpfScreenshotCapture.Capture(signedWindow).Bitmap,
                        Path.ChangeExtension(reportPath, ".signed-3d.png"));
                }
                finally { signedWindow.Close(); }
            }
            var nativeLegend = (Grid)mainViewer.FindName("ViewerHeightLegend");
            var contextualLegend = (HeightLegendRangeBar)mainViewer.FindName("ViewerHeightLegendRangeBar");
            Check("height legend projects SDK histogram bins and full automatic reference lines", contextualLegend.Histogram is { Count: > 0 }
                && ((System.Windows.Shapes.Path)contextualLegend.FindName("HeightHistogram")).Data.Bounds.Height > 0
                && ((System.Windows.Shapes.Line)contextualLegend.FindName("AutomaticUpperLine")).Y1 < ((System.Windows.Shapes.Line)contextualLegend.FindName("AutomaticLowerLine")).Y1,
                "source histogram and automatic full-range lines rendered through bound shared control");
            var nativeSurface = (Grid)mainViewer.FindName("ViewportSurface");
            Check("V5 native viewport opts into context selection before SharpGL resize",
                ViewportResizeContext.GetIsEnabled((DependencyObject)mainViewer.FindName("Viewport")),
                "existing SharpGL control and inherited Viewer DataContext are retained; context preparation is View plumbing only");
            var displayUnits = (StackPanel)mainViewer.FindName("ViewerDisplayUnits");
            var unitsBounds = displayUnits.TransformToAncestor(mainViewer).TransformBounds(new Rect(displayUnits.RenderSize));
            coordinateStatus.Inlines.Add(longCoordinate);
            mainViewer.UpdateLayout();
            Check("D41 long C3D coordinates keep projection and units within a 340px footer",
                unitsBounds.Left >= 0 && unitsBounds.Right <= mainViewer.ActualWidth && coordinateScroll.ActualWidth > 13
                && coordinateScroll.ScrollableHeight > 0 && viewerBottomStatus.ActualHeight == 24,
                $"width={mainViewer.ActualWidth};units={unitsBounds};coordinates={coordinateScroll.ActualWidth}/{coordinateScroll.ScrollableHeight}");
            coordinateStatus.Inlines.Remove(longCoordinate);
            mainViewer.UpdateLayout();
            var shortPaneCubeBounds = cubePanel.TransformToAncestor(nativeSurface).TransformBounds(new Rect(cubePanel.RenderSize));
            var legendBounds = nativeLegend.TransformToAncestor(nativeSurface).TransformBounds(new Rect(nativeLegend.RenderSize));
            var savedLegendCollapse = contextualLegend.IsCollapsed;
            var compactInformation = (TextBlock)contextualLegend.FindName("LegendInformation");
            var informationScroll = (ScrollViewer)contextualLegend.FindName("LegendInformationScroll");
            var collapseButton = OpenVisionLab.ThreeD.Shell.Verification.Smoke.ShellSmokeArtifacts.FindVisualDescendants<Button>(contextualLegend)
                .Single(button => System.Windows.Automation.AutomationProperties.GetAutomationId(button) == "HeightLegendCollapse");
            Check("V5 short native pane folds only presentation to leave the model and cube visible",
                contextualLegend.IsCompactLayout && contextualLegend.IsEffectivelyCollapsed && !savedLegendCollapse
                && !collapseButton.IsEnabled && !shortPaneCubeBounds.IntersectsWith(legendBounds)
                && legendBounds.Width <= 160 && legendBounds.Top >= 44 && legendBounds.Bottom <= nativeSurface.ActualHeight
                && !compactInformation.IsVisible && ((TextBlock)contextualLegend.FindName("SpaceConstraintMessage")).IsVisible
                && mainViewer.CaptureCameraState() == cameraBeforeChrome,
                $"surface={nativeSurface.RenderSize};cube={shortPaneCubeBounds};legend={legendBounds};saved={savedLegendCollapse};effective={contextualLegend.IsEffectivelyCollapsed}");
            OpenVisionLab.ThreeD.Viewer.Rendering.WpfScreenshotCapture.Save(
                OpenVisionLab.ThreeD.Viewer.Rendering.WpfScreenshotCapture.Capture(mainViewer).Bitmap,
                Path.ChangeExtension(reportPath, ".short-legend.png"));
            mainViewer.Height = 540;
            mainViewer.UpdateLayout();
            var roomyLegendBounds = nativeLegend.TransformToAncestor(nativeSurface).TransformBounds(new Rect(nativeLegend.RenderSize));
            var roomyCubeBounds = cubePanel.TransformToAncestor(nativeSurface).TransformBounds(new Rect(cubePanel.RenderSize));
            Check("V5 sufficient pane space restores all five values and saved expansion without camera or inspection changes",
                !contextualLegend.IsCompactLayout && !contextualLegend.IsEffectivelyCollapsed && contextualLegend.IsCollapsed == savedLegendCollapse
                && collapseButton.IsEnabled && compactInformation.IsVisible && !roomyCubeBounds.IntersectsWith(roomyLegendBounds)
                && roomyLegendBounds.Bottom <= nativeSurface.ActualHeight
                && OpenVisionLab.ThreeD.Shell.Verification.Smoke.ShellSmokeArtifacts.FindVisualDescendants<TextBlock>(nativeLegend).Count(text => text.FontFamily.Source == "Consolas" && text.IsVisible) == 5
                && mainViewer.CaptureCameraState() == cameraBeforeChrome
                && mainViewer.HostState.Inspection.PublishedResultCount == mainInspectionBeforePresentation.PublishedResultCount,
                $"surface={nativeSurface.RenderSize};legend={roomyLegendBounds};saved={contextualLegend.IsCollapsed};effective={contextualLegend.IsEffectivelyCollapsed}");
            var originalLegendInformation = contextualLegend.Information;
            var longLegendInformation = string.Concat(Enumerable.Repeat("긴_입력_맵_이름_1280x840_", 12)) + ".C3D\n결측 15.5%\n범위 밖 ↓10.0% ↑10.0%";
            contextualLegend.SetCurrentValue(HeightLegendRangeBar.InformationProperty, longLegendInformation);
            mainViewer.UpdateLayout();
            var rangeBeforeInformationScroll = (contextualLegend.Minimum, contextualLegend.Maximum);
            informationScroll.ScrollToBottom();
            mainViewer.UpdateLayout();
            Check("V5 long map information wraps and scrolls to its final statistics within the pane",
                compactInformation.Text == longLegendInformation && compactInformation.TextWrapping == TextWrapping.Wrap
                && informationScroll.ActualHeight <= 60 && informationScroll.ScrollableHeight > 0 && informationScroll.VerticalOffset > 0
                && (contextualLegend.Minimum, contextualLegend.Maximum) == rangeBeforeInformationScroll
                && contextualLegend.IsCollapsed == savedLegendCollapse && !authoredWorkbench.IsDirty,
                $"information={informationScroll.ActualHeight};scroll={informationScroll.VerticalOffset}/{informationScroll.ScrollableHeight};range={rangeBeforeInformationScroll}");
            OpenVisionLab.ThreeD.Viewer.Rendering.WpfScreenshotCapture.Save(
                OpenVisionLab.ThreeD.Viewer.Rendering.WpfScreenshotCapture.Capture(mainViewer).Bitmap,
                Path.ChangeExtension(reportPath, ".long-map-information.png"));
            contextualLegend.SetCurrentValue(HeightLegendRangeBar.InformationProperty, originalLegendInformation);
            informationScroll.ScrollToTop();
            contextualLegend.SetCurrentValue(HeightLegendRangeBar.IsCollapsedProperty, true);
            mainViewer.Height = 240;
            mainViewer.UpdateLayout();
            mainViewer.Height = 540;
            mainViewer.UpdateLayout();
            Check("V5 resize preserves an explicit collapsed preference instead of auto-expanding it",
                contextualLegend.IsCollapsed && contextualLegend.IsEffectivelyCollapsed && !contextualLegend.IsCompactLayout && collapseButton.IsEnabled,
                $"saved={contextualLegend.IsCollapsed};effective={contextualLegend.IsEffectivelyCollapsed};small={contextualLegend.IsCompactLayout}");
            contextualLegend.SetCurrentValue(HeightLegendRangeBar.IsCollapsedProperty, savedLegendCollapse);
            mainViewer.Width = nativeWidthBeforeChrome;
            mainViewer.Height = nativeHeightBeforeChrome;
            layoutTestWindow.UpdateLayout();
            mainViewer.TryApplyCameraState(cubeCameraBefore);
            mainViewer.SetAppliedTeachingSelections(authoredWorkbench.GetCurrentAppliedTeachingSelections());
            var selectedBeforeCaptions = cubeViewModel.SelectedTeachingSelectionId;
            mainViewer.SetSelectedTeachingSelection("selection.pad-01.reference-roi");
            mainViewer.FitAll();
            var refreshRoiCaptions = typeof(OpenVisionThreeDViewerControl).GetMethod("UpdateTeachingRoiCaptions", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            refreshRoiCaptions.Invoke(mainViewer, null);
            mainViewer.UpdateLayout();
            var referenceCaption = (Border)mainViewer.FindName("TeachingReferenceCaption");
            var measurementCaption = (Border)mainViewer.FindName("TeachingMeasurementCaption");
            Check("D41 active ROI pair has compact role captions projected from real selections",
                referenceCaption.IsVisible && measurementCaption.IsVisible
                && ((TextBlock)referenceCaption.Child).Text == "기준" && ((TextBlock)measurementCaption.Child).Text == "측정"
                && !referenceCaption.TransformToAncestor(nativeSurface).TransformBounds(new Rect(referenceCaption.RenderSize))
                    .IntersectsWith(measurementCaption.TransformToAncestor(nativeSurface).TransformBounds(new Rect(measurementCaption.RenderSize)))
                && !authoredWorkbench.IsDirty && mainViewer.HostState.Inspection.PublishedResultCount == 0,
                $"reference={referenceCaption.Visibility};measure={measurementCaption.Visibility};dirty={authoredWorkbench.IsDirty}");
            mainViewer.Width = 640;
            mainViewer.Height = 464;
            mainViewer.UpdateLayout();
            mainViewer.FitAll();
            refreshRoiCaptions.Invoke(mainViewer, null);
            mainViewer.UpdateLayout();
            Check("D41 Compact retains both ROI role captions when their projected anchors are close",
                referenceCaption.IsVisible && measurementCaption.IsVisible
                && !referenceCaption.TransformToAncestor(nativeSurface).TransformBounds(new Rect(referenceCaption.RenderSize))
                    .IntersectsWith(measurementCaption.TransformToAncestor(nativeSurface).TransformBounds(new Rect(measurementCaption.RenderSize)))
                && measurementCaption.TransformToAncestor(nativeSurface).TransformBounds(new Rect(measurementCaption.RenderSize)).Bottom <= nativeSurface.ActualHeight,
                $"surface={nativeSurface.ActualWidth}x{nativeSurface.ActualHeight};reference={referenceCaption.Visibility};measure={measurementCaption.Visibility}");
            mainViewer.Width = nativeWidthBeforeChrome;
            mainViewer.Height = nativeHeightBeforeChrome;
            mainViewer.SetSelectedTeachingSelection(selectedBeforeCaptions);
            mainViewer.TryApplyCameraState(cubeCameraBefore);
            var sourceSelectionIds = authoredWorkbench.GetCurrentAppliedTeachingSelections().Select(selection => selection.Id).ToArray();
            var selectedToolRoiIds = sourceSelectionIds.Where(id => authoredWorkbench.SelectedPipelineStep!.InputEntityIds.Contains(id)).ToArray();
            var originalRoiSelectionId = cubeViewModel.SelectedTeachingSelectionId;
            var allRoiCount = authoredWorkbench.HeightImageViewer.RoiWorkspace.VisibleOverlays.Count;
            var roiSelectedButton = (ToggleButton)layoutWorkspaceView.FindName("ViewerRoiSelectedButton");
            var roiNoneButton = (ToggleButton)layoutWorkspaceView.FindName("ViewerRoiNoneButton");
            var roiAllButton = (ToggleButton)layoutWorkspaceView.FindName("ViewerRoiAllButton");
            roiSelectedButton.Command.Execute(roiSelectedButton.CommandParameter);
            layoutTestWindow.Dispatcher.Invoke(DispatcherPriority.Render, new Action(layoutTestWindow.UpdateLayout));
            Check("v3 Selected tool visibility retains both real ROI slots and hides other tools without editing",
                roiSelectedButton.IsChecked == true && authoredWorkbench.IsViewerRoiSelected && selectedToolRoiIds.Length == 2
                && authoredWorkbench.HeightImageViewer.RoiWorkspace.VisibleOverlays.Select(item => item.SelectionId).Order().SequenceEqual(selectedToolRoiIds.Order())
                && sourceSelectionIds.Where(cubeViewModel.IsTeachingSelectionDisplayed).Order().SequenceEqual(selectedToolRoiIds.Order())
                && cubeViewModel.AppliedTeachingSelections.Count == sourceSelectionIds.Length && !authoredWorkbench.IsDirty,
                $"source={sourceSelectionIds.Length};selected={string.Join(',', selectedToolRoiIds)};visible={authoredWorkbench.HeightImageViewer.RoiWorkspace.VisibleOverlays.Count}");
            roiNoneButton.Command.Execute(roiNoneButton.CommandParameter);
            Check("v3 Off hides applied regions in both native presentations while preserving selection and recipe",
                authoredWorkbench.IsViewerRoiHidden && authoredWorkbench.HeightImageViewer.RoiWorkspace.VisibleOverlays.Count == 0
                && !sourceSelectionIds.Any(cubeViewModel.IsTeachingSelectionDisplayed)
                && cubeViewModel.SelectedTeachingSelectionId == originalRoiSelectionId && cubeViewModel.AppliedTeachingSelections.Count == sourceSelectionIds.Length
                && !authoredWorkbench.IsDirty && !authoredWorkbench.IsOrderedRunRunning && mainViewer.HostState.Inspection.PublishedResultCount == 0,
                $"scope={authoredWorkbench.ViewerRoiScope};visible={authoredWorkbench.HeightImageViewer.RoiWorkspace.VisibleOverlays.Count};selected={cubeViewModel.SelectedTeachingSelectionId}");
            roiAllButton.Command.Execute(roiAllButton.CommandParameter);
            Check("v3 All restores every actual region without inspecting or changing the selected tool",
                authoredWorkbench.IsViewerRoiAll && sourceSelectionIds.All(cubeViewModel.IsTeachingSelectionDisplayed)
                && allRoiCount > 2 && authoredWorkbench.HeightImageViewer.RoiWorkspace.VisibleOverlays.Count == allRoiCount
                && !authoredWorkbench.IsDirty && !authoredWorkbench.IsSelectedStepPreviewRunning && !authoredWorkbench.IsOrderedRunRunning,
                $"all={allRoiCount};restored={authoredWorkbench.HeightImageViewer.RoiWorkspace.VisibleOverlays.Count};dirty={authoredWorkbench.IsDirty}");
            var quadSelectedStep = authoredWorkbench.SelectedPipelineStep;
            var quadDirty = authoredWorkbench.IsDirty;
            authoredWorkbench.AuxiliaryViewerContentId = ToolWorkbenchViewModel.HeightImageViewerContentId;
            var quadAuxiliaryPin = authoredWorkbench.AuxiliaryViewerContentId;
            fourLayoutButton.Command.Execute(fourLayoutButton.CommandParameter);
            WaitForPresentationTask(Task.WhenAll(authoredWorkbench.HeightImageViewer.ObservedLoadTask,
                authoredWorkbench.HeightViewerPane.HeightImage.ObservedLoadTask));
            layoutWorkspaceView.UpdateLayout();
            layoutTestWindow.Dispatcher.Invoke(DispatcherPriority.Render, new Action(layoutTestWindow.UpdateLayout));
            var quadSlots = new[] { "MainSlot", "AuxiliarySlot", "FourSectionSlot", "FourHeightSlot" }.Select(name => (Border)layoutWorkspaceView.FindName(name)).ToArray();
            var quadBounds = quadSlots.Select(slot => slot.TransformToAncestor(layoutWorkspaceView).TransformBounds(new Rect(slot.RenderSize))).ToArray();
            Check(
                "v3 Four renders four non-overlapping actual data views without inspecting or editing",
                authoredWorkbench.IsFourViewerLayout && fourLayoutButton.IsChecked == true
                && quadSlots.All(slot => slot.IsVisible && slot.ActualWidth > 100 && slot.ActualHeight > 100)
                && quadBounds.SelectMany((bounds, index) => quadBounds.Skip(index + 1).Select(other => !bounds.IntersectsWith(other))).All(value => value)
                && authoredWorkbench.HeightImageViewer.Frame?.SourcePath == authoredWorkbench.Source.Path
                && ReferenceEquals(quadSelectedStep, authoredWorkbench.SelectedPipelineStep) && authoredWorkbench.IsDirty == quadDirty
                && !authoredWorkbench.IsOrderedRunRunning && !authoredWorkbench.IsSelectedStepPreviewRunning
                && mainViewer.HostState.Inspection.PublishedResultCount == 0,
                $"four={authoredWorkbench.IsFourViewerLayout};checked={fourLayoutButton.IsChecked};bounds={string.Join(';', quadBounds)};image={authoredWorkbench.HeightImageViewer.Frame?.SourcePath};dirty={authoredWorkbench.IsDirty}");
            OpenVisionLab.ThreeD.Viewer.Rendering.WpfScreenshotCapture.Save(
                OpenVisionLab.ThreeD.Viewer.Rendering.WpfScreenshotCapture.Capture(layoutTestWindow).Bitmap,
                Path.ChangeExtension(reportPath, ".four.png"));
            Check("v3 Four preserves the original auxiliary pin while its default 3D pane displays actual input",
                authoredWorkbench.AuxiliaryViewerContentId == quadAuxiliaryPin
                && authoredWorkbench.AuxiliaryViewerPane.SourcePath == authoredWorkbench.Source.Path
                && authoredWorkbench.AuxiliaryViewerPane.IsThreeD,
                $"pin={authoredWorkbench.AuxiliaryViewerContentId};kind={authoredWorkbench.AuxiliaryViewerPane.Kind};source={authoredWorkbench.AuxiliaryViewerPane.SourcePath}");
            var firstFourHeightView = (HeightImageViewerView)layoutWorkspaceView.FindName("FourHeightImageView");
            // 3D 렌더가 계속되므로 전역 유휴 상태 대신 입력 바인딩과 레이아웃 반영만 기다린다.
            layoutTestWindow.Dispatcher.Invoke(DispatcherPriority.Loaded, new Action(layoutTestWindow.UpdateLayout));
            var firstFourHeightImage = (Image)firstFourHeightView.FindName("HeightImage");
            Check("V5 the first Four Height view renders its identified native input after load completion",
                firstFourHeightView.HasNativeCoordinateImage && firstFourHeightImage.ActualWidth > 0 && firstFourHeightImage.ActualHeight > 0
                && authoredWorkbench.HeightViewerPane.HeightImage.Frame?.SourcePath == authoredWorkbench.HeightViewerPane.SourcePath,
                $"native={firstFourHeightView.HasNativeCoordinateImage};image={firstFourHeightImage.RenderSize};frame={authoredWorkbench.HeightViewerPane.HeightImage.Frame?.SourcePath}");
            var heightLegendModel = authoredWorkbench.HeightViewerPane.HeightImage;
            var savedHeightLegendCollapse = heightLegendModel.IsHeightLegendCollapsed;
            heightLegendModel.IsHeightLegendCollapsed = false;
            var heightLegendFixture = new HeightImageViewerView { DataContext = heightLegendModel, UseEmbeddedChrome = true };
            var heightLegendWindow = new Window
            {
                Owner = layoutTestWindow, Content = heightLegendFixture, Width = 320, Height = 216, MinWidth = 0, MinHeight = 0,
                Left = testWorkArea.Left, Top = testWorkArea.Top, WindowStyle = WindowStyle.None,
                ShowInTaskbar = false, ShowActivated = false
            };
            try
            {
                var heightRangeBefore = (heightLegendModel.HeightLegendMinimum, heightLegendModel.HeightLegendMaximum);
                var heightFrameBefore = heightLegendModel.Frame;
                heightLegendWindow.Show();
                heightLegendWindow.Dispatcher.Invoke(DispatcherPriority.Loaded, new Action(heightLegendWindow.UpdateLayout));
                var imageLegend = (OpenVisionLab.ThreeD.Presentation.Controls.HeightLegendRangeBar)heightLegendFixture.FindName("HeightLegendTrack");
                var imageTools = (Border)heightLegendFixture.FindName("HeightImageChrome");
                Rect HeightChromeBounds(FrameworkElement element) => new(element.TranslatePoint(new Point(), heightLegendFixture), element.RenderSize);
                var constrainedLegendBounds = HeightChromeBounds(imageLegend);
                Check("V5 short Height legend folds before numeric labels overlap the floating toolbar",
                    imageLegend.IsCompactLayout && imageLegend.IsEffectivelyCollapsed && !heightLegendModel.IsHeightLegendCollapsed
                    && !constrainedLegendBounds.IntersectsWith(HeightChromeBounds(imageTools))
                    && constrainedLegendBounds.Bottom <= heightLegendFixture.ActualHeight
                    && ((TextBlock)imageLegend.FindName("SpaceConstraintMessage")).IsVisible && heightLegendFixture.HasNativeCoordinateImage,
                    $"view={heightLegendFixture.RenderSize};legend={constrainedLegendBounds};toolbar={HeightChromeBounds(imageTools)};saved={heightLegendModel.IsHeightLegendCollapsed}");
                OpenVisionLab.ThreeD.Viewer.Rendering.WpfScreenshotCapture.Save(
                    OpenVisionLab.ThreeD.Viewer.Rendering.WpfScreenshotCapture.Capture(heightLegendWindow).Bitmap,
                    Path.ChangeExtension(reportPath, ".height-short-legend.png"));
                heightLegendWindow.Width = 480;
                heightLegendWindow.Height = 460;
                heightLegendWindow.Dispatcher.Invoke(DispatcherPriority.Loaded, new Action(heightLegendWindow.UpdateLayout));
                var heightValueLabels = new[] { "UpperValueLabel", "UpperQuarterLabel", "MiddleLabel", "LowerQuarterLabel", "LowerValueLabel" }
                    .Select(name => (TextBlock)imageLegend.FindName(name)).ToArray();
                Check("V5 enlarged Height restores all five visible numeric labels without changing its range or input",
                    !imageLegend.IsCompactLayout && !imageLegend.IsEffectivelyCollapsed && !heightLegendModel.IsHeightLegendCollapsed
                    && heightValueLabels.All(label => label.IsVisible && !string.IsNullOrWhiteSpace(label.Text)
                        && HeightChromeBounds(label).Top >= 0 && HeightChromeBounds(label).Bottom <= heightLegendFixture.ActualHeight
                        && !HeightChromeBounds(label).IntersectsWith(HeightChromeBounds(imageTools)))
                    && (heightLegendModel.HeightLegendMinimum, heightLegendModel.HeightLegendMaximum) == heightRangeBefore
                    && ReferenceEquals(heightLegendModel.Frame, heightFrameBefore) && !authoredWorkbench.IsDirty,
                    $"labels={string.Join(',', heightValueLabels.Select(label => label.Text))};range={heightRangeBefore};saved={heightLegendModel.IsHeightLegendCollapsed}");
                OpenVisionLab.ThreeD.Viewer.Rendering.WpfScreenshotCapture.Save(
                    OpenVisionLab.ThreeD.Viewer.Rendering.WpfScreenshotCapture.Capture(heightLegendWindow).Bitmap,
                    Path.ChangeExtension(reportPath, ".height-enlarged-legend.png"));
                var heightCollapseButton = OpenVisionLab.ThreeD.Shell.Verification.Smoke.ShellSmokeArtifacts.FindVisualDescendants<Button>(imageLegend)
                    .Single(button => AutomationProperties.GetAutomationId(button) == "HeightLegendCollapse");
                heightCollapseButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                heightLegendWindow.Height = 216;
                heightLegendWindow.Dispatcher.Invoke(DispatcherPriority.Loaded, new Action(heightLegendWindow.UpdateLayout));
                heightLegendWindow.Height = 460;
                heightLegendWindow.Dispatcher.Invoke(DispatcherPriority.Loaded, new Action(heightLegendWindow.UpdateLayout));
                Check("V5 Height manual collapse remains the saved choice across constrained and enlarged sizes",
                    heightLegendModel.IsHeightLegendCollapsed && imageLegend.IsCollapsed && imageLegend.IsEffectivelyCollapsed
                    && !imageLegend.IsCompactLayout && heightCollapseButton.IsEnabled, "manual collapse retained after both actual size changes");
            }
            finally
            {
                heightLegendModel.IsHeightLegendCollapsed = savedHeightLegendCollapse;
                heightLegendWindow.Close();
            }
            var mainKindButtons = OpenVisionLab.ThreeD.Shell.Verification.Smoke.ShellSmokeArtifacts.FindVisualDescendants<ToggleButton>(quadSlots[0])
                .Where(button => AutomationProperties.GetAutomationId(button).StartsWith("PaneKind", StringComparison.Ordinal)).ToArray();
            var mainHeightKind = mainKindButtons.Single(button => AutomationProperties.GetAutomationId(button) == "PaneKindHeight");
            var mainSectionKind = mainKindButtons.Single(button => AutomationProperties.GetAutomationId(button) == "PaneKindSection");
            var mainThreeDKind = mainKindButtons.Single(button => AutomationProperties.GetAutomationId(button) == "PaneKindThreeD");
            mainHeightKind.Command.Execute(mainHeightKind.CommandParameter);
            WaitForPresentationTask(authoredWorkbench.MainViewerPane.HeightImage.ObservedLoadTask);
            layoutTestWindow.UpdateLayout();
            layoutTestWindow.Dispatcher.Invoke(DispatcherPriority.Loaded, new Action(layoutTestWindow.UpdateLayout));
            var mainPaneHeightView = (HeightImageViewerView)layoutWorkspaceView.FindName("MainPaneHeightView");
            var sharedHeightDisplayButton = (ToggleButton)mainPaneHeightView.FindName("HeightImageDisplaySettingsToggle");
            var sharedHeightDisplay = mainPresentationBar.Presentation!;
            var sharedHeightPopup = (Popup)mainPresentationBar.FindName("ViewerDisplaySettingsPopup");
            var mainHeightDisplayModel = authoredWorkbench.MainViewerPane.HeightImage;
            var mainHeightPaletteBeforePopup = mainHeightDisplayModel.SelectedPalette;
            var nativePresentationBeforeHeightPopup = mainViewer.HostState.Presentation;
            var otherPaneHeightBeforePopup = authoredWorkbench.HeightViewerPane.HeightImage.DisplayFrame;
            sharedHeightDisplayButton.Command!.Execute(null);
            layoutTestWindow.Dispatcher.Invoke(DispatcherPriority.Loaded, new Action(layoutTestWindow.UpdateLayout));
            var sharedHeightPopupChild = (FrameworkElement)sharedHeightPopup.Child;
            sharedHeightPopupChild.UpdateLayout();
            Check("D45 Main Height opens the same HTML display popup from its actual bound button",
                sharedHeightPopup.IsOpen && sharedHeightPopupChild.IsVisible && sharedHeightPopupChild.ActualWidth == 300
                && ReferenceEquals(sharedHeightPopup.PlacementTarget, sharedHeightDisplayButton) && sharedHeightDisplayButton.IsChecked == true
                && !((Expander)mainPaneHeightView.FindName("HeightImageDisplaySettings")).IsExpanded,
                $"open={sharedHeightPopup.IsOpen};visible={sharedHeightPopupChild.IsVisible};width={sharedHeightPopupChild.ActualWidth};target={sharedHeightPopup.PlacementTarget}");
            var heightPopupPalette = (ListBox)mainPresentationBar.FindName("PaletteSelector");
            var heightPopupPaletteItems = heightPopupPalette.Items.Cast<object>().Select(item => (ListBoxItem)heightPopupPalette.ItemContainerGenerator.ContainerFromItem(item)).ToArray();
            Check("V5 Height popup matches the five supported palettes and disables unsupported deviation",
                heightPopupPalette.Items.Cast<string>().SequenceEqual(new[] { "Height", "Grayscale", "Solid", "Thermal", "ColorVisionAssist", "Deviation" })
                && heightPopupPaletteItems.All(item => item.ActualHeight == 40 && item.ActualWidth > 50)
                && heightPopupPaletteItems.Take(5).All(item => item.IsEnabled) && !heightPopupPaletteItems[5].IsEnabled,
                $"items={string.Join(",", heightPopupPalette.Items.Cast<string>())};sizes={string.Join(",", heightPopupPaletteItems.Select(item => $"{item.ActualWidth:F0}x{item.ActualHeight:F0}/{item.IsEnabled}"))}");
            var heightRangeLinkToggle = (CheckBox)mainPresentationBar.FindName("ViewerHeightRangeLinkedToggle");
            heightRangeLinkToggle.IsChecked = false;
            layoutTestWindow.UpdateLayout();
            nativePresentationBeforeHeightPopup = mainViewer.HostState.Presentation;
            Check("V5 rendered range-link checkbox updates both existing owners without inspection",
                !mainHeightDisplayModel.HeightDisplayOptions.IsLinked && !nativePresentationBeforeHeightPopup.HeightDisplayOptions.IsLinked
                && !authoredWorkbench.IsDirty && !authoredWorkbench.IsOrderedRunRunning,
                $"heightLinked={mainHeightDisplayModel.HeightDisplayOptions.IsLinked};nativeLinked={nativePresentationBeforeHeightPopup.HeightDisplayOptions.IsLinked}");
            sharedHeightDisplay.SelectedColorMap = "Solid";
            Check("D45 Main Height palette changes real height pixels without changing other panes or inspection",
                mainHeightDisplayModel.SelectedPalette == C3DHeightImagePalette.Monochrome
                && mainHeightDisplayModel.LegendColor0 == "#739BC2" && mainHeightDisplayModel.LegendColor3 == "#739BC2"
                && mainViewer.HostState.Presentation.SelectedColorMap == nativePresentationBeforeHeightPopup.SelectedColorMap
                && ReferenceEquals(otherPaneHeightBeforePopup, authoredWorkbench.HeightViewerPane.HeightImage.DisplayFrame)
                && !authoredWorkbench.IsDirty && !authoredWorkbench.IsOrderedRunRunning,
                $"heightPalette={mainHeightDisplayModel.SelectedPalette};nativePalette={mainViewer.HostState.Presentation.SelectedColorMap};dirty={authoredWorkbench.IsDirty}");
            sharedHeightDisplay.IsManualRange = true;
            sharedHeightDisplay.DraftHeightRangeMinimum = (mainHeightDisplayModel.Frame!.DefaultDisplayMinimum + 1).ToString("G", CultureInfo.InvariantCulture);
            sharedHeightDisplay.DraftHeightRangeMaximum = (mainHeightDisplayModel.Frame.DefaultDisplayMaximum - 1).ToString("G", CultureInfo.InvariantCulture);
            sharedHeightDisplay.ApplyDisplaySettingsCommand.Execute(null);
            Check("D45 height popup applies only the active pane range and remains open",
                !mainHeightDisplayModel.IsAutoRange && sharedHeightDisplay.IsDisplaySettingsOpen
                && mainViewer.HostState.Presentation.C3DHeightColorRangeRevision == nativePresentationBeforeHeightPopup.C3DHeightColorRangeRevision
                && ReferenceEquals(otherPaneHeightBeforePopup, authoredWorkbench.HeightViewerPane.HeightImage.DisplayFrame)
                && !authoredWorkbench.IsDirty && mainViewer.HostState.Inspection.PublishedResultCount == 0,
                $"auto={mainHeightDisplayModel.IsAutoRange};open={sharedHeightDisplay.IsDisplaySettingsOpen};nativeRev={mainViewer.HostState.Presentation.C3DHeightColorRangeRevision}");
            Check("D45 Height popup automatic range summary remains source-derived during a manual range edit",
                sharedHeightDisplay.DefaultHeightRangeSummary == $"{mainHeightDisplayModel.Frame.DefaultDisplayMinimum.ToString("F2", CultureInfo.InvariantCulture)} - {mainHeightDisplayModel.Frame.DefaultDisplayMaximum.ToString("F2", CultureInfo.InvariantCulture)} {mainHeightDisplayModel.Frame.Unit}",
                $"summary={sharedHeightDisplay.DefaultHeightRangeSummary};manual={mainHeightDisplayModel.RangeMinimumText}/{mainHeightDisplayModel.RangeMaximumText}");
            mainHeightDisplayModel.TryApplyManualRange(mainHeightDisplayModel.Frame.DefaultDisplayMinimum + 2, mainHeightDisplayModel.Frame.DefaultDisplayMaximum - 2);
            sharedHeightDisplay.ApplyDisplaySettingsCommand.Execute(null);
            var rejectedHeightRangeEdit = sharedHeightDisplay.HasDisplaySettingsStaleError;
            sharedHeightDisplay.CancelDisplaySettingsCommand.Execute(null);
            sharedHeightDisplay.IsAutomaticRange = true;
            Check("D45 height popup rejects an external range edit and recovers through cancel and automatic range",
                rejectedHeightRangeEdit && !sharedHeightDisplay.HasDisplaySettingsStaleError && mainHeightDisplayModel.IsAutoRange
                && sharedHeightDisplay.IsDisplaySettingsOpen && !authoredWorkbench.IsDirty && !authoredWorkbench.IsSelectedStepPreviewRunning,
                $"rejected={rejectedHeightRangeEdit};stale={sharedHeightDisplay.HasDisplaySettingsStaleError};auto={mainHeightDisplayModel.IsAutoRange}");
            sharedHeightDisplay.SelectedColorMap = "Height";
            OpenVisionLab.ThreeD.Viewer.Rendering.WpfScreenshotCapture.Save(
                OpenVisionLab.ThreeD.Viewer.Rendering.WpfScreenshotCapture.Capture(sharedHeightPopupChild).Bitmap,
                Path.ChangeExtension(reportPath, ".d45-main-height-popup.png"));
            sharedHeightDisplay.CloseDisplaySettingsCommand.Execute(null);
            mainHeightDisplayModel.SelectedPalette = mainHeightPaletteBeforePopup;
            foreach (var (pane, barName, heightName) in new[]
            {
                (authoredWorkbench.AuxiliaryViewerPane, "AuxiliaryViewerPresentationBar", "AuxiliaryViewerHost"),
                (authoredWorkbench.SectionViewerPane, "FourSectionSlotPresentationBar", "SectionPaneHeightView"),
                (authoredWorkbench.HeightViewerPane, "FourHeightSlotPresentationBar", "FourHeightImageView")
            })
            {
                var previousKind = pane.Kind;
                pane.SetKindCommand.Execute(ViewerPaneKind.Height);
                WaitForPresentationTask(pane.HeightImage.ObservedLoadTask);
                layoutTestWindow.UpdateLayout();
                layoutTestWindow.Dispatcher.Invoke(DispatcherPriority.Loaded, new Action(layoutTestWindow.UpdateLayout));
                var bar = (ViewerPresentationBar)layoutWorkspaceView.FindName(barName);
                var heightView = heightName == "AuxiliaryViewerHost"
                    ? ((ContentControl)layoutWorkspaceView.FindName(heightName)).Content as HeightImageViewerView
                    : layoutWorkspaceView.FindName(heightName) as HeightImageViewerView;
                var display = heightView?.DisplayPresentation;
                var toggle = heightView?.FindName("HeightImageDisplaySettingsToggle") as ToggleButton;
                Check($"height pane {barName} uses its own existing display VM and anchor", display is not null
                    && ReferenceEquals(display, bar.Presentation) && ReferenceEquals(bar.HeightImage, pane.HeightImage)
                    && ReferenceEquals(bar.DialogPlacementTarget, heightView?.DisplaySettingsAnchor)
                    && toggle?.Command?.CanExecute(null) == true && !bar.IsVisible, $"display={display is not null};command={toggle?.Command?.CanExecute(null)}");
                var previousOptions = pane.HeightImage.HeightDisplayOptions;
                pane.HeightImage.SetHeightDisplayLinked(false);
                var beforeOtherPalette = mainHeightDisplayModel.SelectedPalette;
                toggle!.Command.Execute(toggle.CommandParameter);
                layoutTestWindow.Dispatcher.Invoke(DispatcherPriority.Render, new Action(layoutTestWindow.UpdateLayout));
                var popup = (Popup)bar.FindName("ViewerDisplaySettingsPopup");
                Check($"height pane {barName} opens the shared themed popup", popup.IsOpen && popup.Child?.IsVisible == true
                    && display!.IsDisplaySettingsOpen, $"popup={popup.IsOpen};visible={popup.Child?.IsVisible}");
                display!.SelectedColorMap = "Thermal";
                Check($"height pane {barName} writes its own palette while unlinked", pane.HeightImage.SelectedPalette == C3DHeightImagePalette.Thermal
                    && mainHeightDisplayModel.SelectedPalette == beforeOtherPalette && !authoredWorkbench.IsDirty
                    && !authoredWorkbench.IsSelectedStepPreviewRunning, $"palette={pane.HeightImage.SelectedPalette};other={mainHeightDisplayModel.SelectedPalette}");
                display.CloseDisplaySettingsCommand.Execute(null);
                pane.HeightImage.TryApplyHeightDisplayOptions(previousOptions);
                pane.SetKindCommand.Execute(previousKind);
                layoutTestWindow.UpdateLayout();
            }
            var heightLegacyMenu = mainPaneHeightView.ContextMenu;
            heightLegacyMenu.PlacementTarget = mainPaneHeightView;
            heightLegacyMenu.IsOpen = true;
            var heightLegacyPaletteMenu = heightLegacyMenu.Items.OfType<MenuItem>().Last();
            heightLegacyPaletteMenu.IsSubmenuOpen = true;
            layoutTestWindow.Dispatcher.Invoke(DispatcherPriority.Render, new Action(layoutTestWindow.UpdateLayout));
            var heightLegacyPalette = (ComboBox)((MenuItem)heightLegacyPaletteMenu.Items[0]).Header;
            var sourceBeforeLegacyPalette = mainHeightDisplayModel.Frame;
            heightLegacyPalette.SelectedItem = mainHeightDisplayModel.PaletteChoices.Single(choice => choice.Value == C3DHeightImagePalette.Thermal);
            Check("D45 legacy Height thermal palette remains accessible through the existing view context menu",
                heightLegacyPalette.IsVisible && heightLegacyPalette.IsEnabled && mainHeightDisplayModel.SelectedPalette == C3DHeightImagePalette.Thermal
                && ReferenceEquals(sourceBeforeLegacyPalette, mainHeightDisplayModel.Frame) && !authoredWorkbench.IsDirty && !authoredWorkbench.IsOrderedRunRunning,
                $"visible={heightLegacyPalette.IsVisible};palette={mainHeightDisplayModel.SelectedPalette};dirty={authoredWorkbench.IsDirty}");
            heightLegacyMenu.IsOpen = false;
            mainHeightDisplayModel.SelectedPalette = mainHeightPaletteBeforePopup;
            mainHeightDisplayModel.SetHeightDisplayLinked(true);
            layoutTestWindow.UpdateLayout();
            Check("V5 owner-to-control range-link restoration renders the checked state",
                heightRangeLinkToggle.IsChecked == true && mainViewer.HostState.Presentation.HeightDisplayOptions.IsLinked
                && !authoredWorkbench.IsDirty && !authoredWorkbench.IsOrderedRunRunning,
                $"checked={heightRangeLinkToggle.IsChecked};nativeLinked={mainViewer.HostState.Presentation.HeightDisplayOptions.IsLinked}");
            Check("D44 Height toolbar follows the six HTML tools using existing Main commands",
                new[] { "HeightMoveTool", "HeightRoiTool", "HeightProfileTool", "HeightFitRoiTool", "HeightImageDisplaySettingsToggle" }.All(name => mainPaneHeightView.FindName(name) is ButtonBase { Command: not null } or ToggleButton)
                && ReferenceEquals(mainPaneHeightView.Presentation, mainPresentationBar.Presentation)
                && ReferenceEquals(mainPaneHeightView.RoiCaptureCommand, authoredWorkbench.BeginTeachingSelectionCaptureCommand)
                && ((HeightImageViewerView)layoutWorkspaceView.FindName("FourHeightImageView")).RoiCaptureCommand is null,
                $"mainCapture={mainPaneHeightView.RoiCaptureCommand is not null};auxiliaryCapture={((HeightImageViewerView)layoutWorkspaceView.FindName("FourHeightImageView")).RoiCaptureCommand is not null}");
            Check("D44 Height axes and footer use native cells, actual filename and scale",
                ((Canvas)mainPaneHeightView.FindName("HeightAxes")).Children.OfType<TextBlock>().Any(text => double.TryParse(text.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                && authoredWorkbench.MainViewerPane.SourceName == Path.GetFileName(authoredWorkbench.Source.Path)
                && authoredWorkbench.MainViewerPane.HeightImage.PixelsPerCellSummary.Contains("px/", StringComparison.Ordinal),
                $"name={authoredWorkbench.MainViewerPane.SourceName};scale={authoredWorkbench.MainViewerPane.HeightImage.PixelsPerCellSummary};axes={((Canvas)mainPaneHeightView.FindName("HeightAxes")).Children.Count};image={((Image)mainPaneHeightView.FindName("HeightImage")).ActualWidth};visible={mainPaneHeightView.IsVisible}");
            var heightFrameBeforeFit = authoredWorkbench.MainViewerPane.HeightImage.Frame;
            var heightInspectionBeforeFit = mainViewer.HostState.Inspection;
            var fitRoi = (Button)mainPaneHeightView.FindName("HeightFitRoiTool");
            Check("D44 explicit Height Fit ROI is bound and enabled only for a visible active ROI",
                ReferenceEquals(fitRoi.Command, authoredWorkbench.MainViewerPane.HeightImage.FitRoiCommand)
                && fitRoi.Command.CanExecute(null) && authoredWorkbench.MainViewerPane.HeightImage.FitRoiRectangle is not null,
                $"roi={authoredWorkbench.MainViewerPane.HeightImage.FitRoiRectangle}");
            fitRoi.Command.Execute(null);
            layoutTestWindow.Dispatcher.Invoke(DispatcherPriority.Loaded, new Action(layoutTestWindow.UpdateLayout));
            Check("D44 Height Fit ROI changes only the viewport, not input, recipe or execution",
                ReferenceEquals(heightFrameBeforeFit, authoredWorkbench.MainViewerPane.HeightImage.Frame)
                && authoredWorkbench.MainViewerPane.HeightImage.ZoomPercent > 0
                && mainViewer.HostState.Inspection == heightInspectionBeforeFit && !authoredWorkbench.IsDirty,
                $"scale={authoredWorkbench.MainViewerPane.HeightImage.ZoomPercent};dirty={authoredWorkbench.IsDirty}");
            var fitImage = (Image)mainPaneHeightView.FindName("HeightImage");
            var fitRectangle = authoredWorkbench.MainViewerPane.HeightImage.FitRoiRectangle!;
            var fitScale = fitImage.Width / heightFrameBeforeFit!.Width;
            var fittedRoiBounds = new Rect(fitImage.TranslatePoint(new Point(fitRectangle.Column * fitScale, fitRectangle.Row * fitScale), mainPaneHeightView), new Size(fitRectangle.ColumnCount * fitScale, fitRectangle.RowCount * fitScale));
            var fitToolbar = (Border)mainPaneHeightView.FindName("HeightImageChrome");
            var fitToolbarBounds = new Rect(fitToolbar.TranslatePoint(new Point(), mainPaneHeightView), new Size(fitToolbar.ActualWidth, fitToolbar.ActualHeight));
            var fitViewport = (ScrollViewer)mainPaneHeightView.FindName("ImageScrollViewer");
            var fitViewportBounds = new Rect(fitViewport.TranslatePoint(new Point(), mainPaneHeightView), new Size(fitViewport.ViewportWidth, fitViewport.ViewportHeight));
            Check("D44 Height Fit ROI reserves actual floating-toolbar space without cropping the ROI",
                !fittedRoiBounds.IntersectsWith(fitToolbarBounds) && fitViewportBounds.Contains(fittedRoiBounds),
                $"roi={fittedRoiBounds};toolbar={fitToolbarBounds};viewport={fitViewportBounds}");
            OpenVisionLab.ThreeD.Viewer.Rendering.WpfScreenshotCapture.Save(
                OpenVisionLab.ThreeD.Viewer.Rendering.WpfScreenshotCapture.Capture(layoutTestWindow).Bitmap,
                Path.ChangeExtension(reportPath, ".d44-height-fit-roi.png"));
            authoredWorkbench.MainViewerPane.HeightImage.FitCommand.Execute(null);
            layoutTestWindow.Dispatcher.Invoke(DispatcherPriority.Loaded, new Action(layoutTestWindow.UpdateLayout));
            var hatch = (System.Windows.Shapes.Rectangle)mainPaneHeightView.FindName("MissingCellHatch");
            var hatchFrame = authoredWorkbench.MainViewerPane.HeightImage.Frame!;
            var hatchBitmap = ((ImageBrush)hatch.OpacityMask).ImageSource as BitmapSource ?? throw new InvalidOperationException("Missing hatch mask bitmap.");
            var hatchPixels = new byte[hatchBitmap.PixelWidth * hatchBitmap.PixelHeight * 4];
            hatchBitmap.CopyPixels(hatchPixels, hatchBitmap.PixelWidth * 4, 0);
            Check("D42 missing hatch mask maps every real missing cell and no valid cell",
                hatchBitmap.PixelWidth == hatchFrame.Width && hatchBitmap.PixelHeight == hatchFrame.Height
                && Enumerable.Range(0, hatchFrame.Width * hatchFrame.Height).All(index => hatchFrame.InvalidCellMap.TryIsMissing(index % hatchFrame.Width, index / hatchFrame.Width, out var missing) && hatchPixels[index * 4 + 3] == (missing ? 255 : 0))
                && hatch.Fill is DrawingBrush { TileMode: TileMode.Tile, ViewportUnits: BrushMappingMode.Absolute, Viewport.Width: 8, Viewport.Height: 8 },
                $"mask={hatchBitmap.PixelWidth}x{hatchBitmap.PixelHeight};missing={hatchFrame.MissingCount};map={hatchFrame.InvalidCellMap.Sha256}");
            var hatchPixelHash = authoredWorkbench.MainViewerPane.HeightImage.DisplayFrame!.PixelSha256;
            authoredWorkbench.MainViewerPane.HeightImage.ShowInvalidCells = false;
            mainPaneHeightView.UpdateLayout();
            var loadedHeightScroller = (ScrollViewer)mainPaneHeightView.FindName("ImageScrollViewer");
            var heightLegend = (Grid)mainPaneHeightView.FindName("HeightImageHeightLegend");
            var heightImageBounds = loadedHeightScroller.TransformToAncestor(mainPaneHeightView).TransformBounds(new Rect(loadedHeightScroller.RenderSize));
            var heightLegendBounds = heightLegend.TransformToAncestor(mainPaneHeightView).TransformBounds(new Rect(heightLegend.RenderSize));
            Check("D42 loaded Height Image legend has its own column and cannot cover image pixels or ROIs",
                heightLegend.IsVisible && heightLegend.ActualWidth > 0 && heightLegend.ActualHeight > 0
                && Grid.GetColumn(heightLegend) == 1 && !heightImageBounds.IntersectsWith(heightLegendBounds)
                && heightLegendBounds.Right <= mainPaneHeightView.ActualWidth
                && heightLegendBounds.Bottom <= mainPaneHeightView.ActualHeight,
                $"image={heightImageBounds};legend={heightLegendBounds};view={mainPaneHeightView.ActualWidth}x{mainPaneHeightView.ActualHeight}");
            // 공간 제약 접힘은 앞에서 확인했으며 숫자 기하는 실제 크게 보기 상태에서 확인한다.
            authoredWorkbench.ToggleViewerPaneMaximizedCommand.Execute("viewer.main");
            layoutTestWindow.Dispatcher.Invoke(DispatcherPriority.Loaded, new Action(layoutTestWindow.UpdateLayout));
            var heightLegendTrack = (FrameworkElement)((HeightLegendRangeBar)mainPaneHeightView.FindName("HeightLegendTrack")).FindName("Track");
            var heightBody = (FrameworkElement)loadedHeightScroller.Parent;
            Check("D45 Height Image legend follows the HTML body-height ratio and bounds",
                Math.Abs(heightLegendTrack.ActualHeight - Math.Clamp(heightBody.ActualHeight * 0.32, 90, 200)) < 1,
                $"body={heightBody.ActualHeight:F2};legend={heightLegendTrack.ActualHeight:F2}");
            var rangeBar = (HeightLegendRangeBar)mainPaneHeightView.FindName("HeightLegendTrack");
            mainHeightDisplayModel.TryApplyManualRange(100, 100.5);
            layoutTestWindow.UpdateLayout();
            var upperRangeLabel = (TextBlock)rangeBar.FindName("UpperValueLabel");
            var lowerRangeLabel = (TextBlock)rangeBar.FindName("LowerValueLabel");
            var upperRangeBounds = upperRangeLabel.TransformToAncestor(rangeBar).TransformBounds(new Rect(upperRangeLabel.RenderSize));
            var lowerRangeBounds = lowerRangeLabel.TransformToAncestor(rangeBar).TransformBounds(new Rect(lowerRangeLabel.RenderSize));
            Check("V5 narrow range keeps both endpoint values visible without overlapping secondary ticks",
                upperRangeLabel.Text == "100.5" && lowerRangeLabel.Text == "100"
                && upperRangeBounds.Bottom <= lowerRangeBounds.Top && lowerRangeBounds.Bottom <= rangeBar.ActualHeight
                && ((TextBlock)rangeBar.FindName("MiddleLabel")).Visibility == Visibility.Collapsed,
                $"upper={upperRangeLabel.Text}/{upperRangeBounds};lower={lowerRangeLabel.Text}/{lowerRangeBounds};bar={rangeBar.RenderSize}");
            mainHeightDisplayModel.UseAutoRange();
            rangeBar.IsCollapsed = true;
            layoutTestWindow.UpdateLayout();
            rangeBar.IsCollapsed = false;
            layoutTestWindow.UpdateLayout();
            layoutTestWindow.Dispatcher.Invoke(DispatcherPriority.Loaded, new Action(layoutTestWindow.UpdateLayout));
            Check("V5 expanding the legend recomputes geometry after the plot is visible",
                Canvas.GetTop(lowerRangeLabel) > heightLegendTrack.ActualHeight * 0.5
                && Canvas.GetTop(upperRangeLabel) < 1
                && ((TextBlock)rangeBar.FindName("MiddleLabel")).Visibility == Visibility.Visible,
                $"upper={Canvas.GetTop(upperRangeLabel)};lower={Canvas.GetTop(lowerRangeLabel)};plot={heightLegendTrack.ActualHeight}");
            authoredWorkbench.ToggleViewerPaneMaximizedCommand.Execute("viewer.main");
            layoutTestWindow.Dispatcher.Invoke(DispatcherPriority.Loaded, new Action(layoutTestWindow.UpdateLayout));
            Check("D42 missing hatch hides without changing input identity or native coordinates",
                hatch.Visibility == Visibility.Collapsed && ReferenceEquals(hatchFrame, authoredWorkbench.MainViewerPane.HeightImage.Frame)
                && mainPaneHeightView.HasNativeCoordinateImage && !authoredWorkbench.IsDirty && !authoredWorkbench.IsOrderedRunRunning,
                $"visible={hatch.Visibility};source={hatchFrame.SourceContentSha256}");
            authoredWorkbench.MainViewerPane.HeightImage.ShowInvalidCells = true;
            mainPaneHeightView.UpdateLayout();
            Check("D42 missing hatch restores its mask and exact display pixels without execution",
                hatch.Visibility == Visibility.Visible && ReferenceEquals(hatchBitmap, ((ImageBrush)hatch.OpacityMask).ImageSource)
                && authoredWorkbench.MainViewerPane.HeightImage.DisplayFrame!.PixelSha256 == hatchPixelHash
                && !authoredWorkbench.IsDirty && !authoredWorkbench.IsSelectedStepPreviewRunning && mainViewer.HostState.Inspection.PublishedResultCount == 0,
                $"visible={hatch.Visibility};displaySha={hatchPixelHash}");
            Check("v3 rendered pane-kind command selects real native height pixels independently of other panes",
                mainHeightKind.IsChecked == true && mainPaneHeightView.IsVisible && mainPaneHeightView.HasNativeCoordinateImage
                && authoredWorkbench.MainViewerPane.HeightImage.Frame?.SourcePath == authoredWorkbench.Source.Path
                && !((HeightProfileView)layoutWorkspaceView.FindName("MainPaneProfile")).IsVisible && !mainViewer.IsVisible
                && authoredWorkbench.AuxiliaryViewerPane.IsThreeD && authoredWorkbench.SectionViewerPane.IsSection
                && authoredWorkbench.HeightViewerPane.IsHeight && authoredWorkbench.AuxiliaryViewerContentId == quadAuxiliaryPin,
                $"height={mainHeightKind.IsChecked}/{mainPaneHeightView.IsVisible}/{mainPaneHeightView.HasNativeCoordinateImage};frame={authoredWorkbench.MainViewerPane.HeightImage.Frame?.SourcePath};display={authoredWorkbench.MainViewerPane.HeightImage.DisplayFrame is not null};context={ReferenceEquals(mainPaneHeightView.DataContext, authoredWorkbench.MainViewerPane.HeightImage)};other={authoredWorkbench.AuxiliaryViewerPane.Kind}/{authoredWorkbench.SectionViewerPane.Kind}/{authoredWorkbench.HeightViewerPane.Kind}");
            mainSectionKind.Command.Execute(mainSectionKind.CommandParameter);
            layoutTestWindow.UpdateLayout();
            var mainPaneProfile = (HeightProfileView)layoutWorkspaceView.FindName("MainPaneProfile");
            Check("v3 Section tab binds the matching real 3D host and returns without an inspection",
                mainSectionKind.IsChecked == true && mainPaneProfile.IsVisible && ReferenceEquals(mainPaneProfile.ViewerHost, mainViewer)
                && !mainPaneHeightView.IsVisible && ReferenceEquals(quadSelectedStep, authoredWorkbench.SelectedPipelineStep)
                && authoredWorkbench.IsDirty == quadDirty && !authoredWorkbench.IsOrderedRunRunning
                && mainViewer.HostState.Inspection.PublishedResultCount == 0,
                $"section={mainSectionKind.IsChecked}/{mainPaneProfile.IsVisible};host={ReferenceEquals(mainPaneProfile.ViewerHost, mainViewer)};dirty={authoredWorkbench.IsDirty}");
            var beforeProfileMode = mainViewer.HostState.SelectionMode;
            var profileInput = authoredWorkbench.MainViewerPane.HeightImage.Frame!;
            var profileRow = profileInput.Height / 3;
            Check("D44 Section is fed by actual input cells without Preview or Run",
                mainViewer.TryRequestHeightProfile(OpenVisionLab.ThreeD.Viewer.Hosting.ViewerHeightProfileAction.Begin)
                && mainViewer.TryPickHeightProfileCell(new(profileInput.SourceContentSha256, profileRow, profileInput.Width / 20))
                && mainViewer.TryPickHeightProfileCell(new(profileInput.SourceContentSha256, profileRow, profileInput.Width * 19 / 20))
                && mainViewer.HostState.Profile.HasEndpoints && mainViewer.HostState.Profile.GridDistanceCells > 0
                && !authoredWorkbench.IsDirty && mainViewer.HostState.Inspection.PublishedResultCount == 0,
                $"profile={mainViewer.HostState.Profile.EndpointSummary};distance={mainViewer.HostState.Profile.GridDistanceCells}");
            var chartFixture = new HeightProfileView { ViewerHost = mainViewer, UseEmbeddedChrome = true, SelectionContext = "D44 representative long selected tool and ROI context" };
            chartTestWindow = new Window { Owner = layoutTestWindow, Title = "D44 profile render fixture", Width = 700, Height = 440, MinWidth = 0, MinHeight = 0, WindowStyle = WindowStyle.None, ShowInTaskbar = false, ShowActivated = false, Left = testWorkArea.Left, Top = testWorkArea.Top, Content = chartFixture };
            chartTestWindow.Closed += (_, _) => chartFixture.Dispose();
            chartTestWindow.Show();
            var chartCanvas = (Canvas)chartFixture.FindName("ProfileChartCanvas");
            foreach (var chartSize in new[] { new Size(700, 440), new Size(400, 300) })
            {
                chartTestWindow.Width = chartSize.Width;
                chartTestWindow.Height = chartSize.Height;
                chartTestWindow.Dispatcher.Invoke(DispatcherPriority.Loaded, new Action(chartTestWindow.UpdateLayout));
                OpenVisionLab.ThreeD.Shell.Verification.Smoke.ShellWindowNativeInterop.AppendWindowMonitorEvidence(chartTestWindow, monitorReport);
                Check($"D44 Section fills {chartSize.Width}x{chartSize.Height} with numeric axes and fixed-width strokes",
                    chartCanvas.ActualHeight >= chartFixture.ActualHeight - 55
                    && chartCanvas.Children.OfType<System.Windows.Shapes.Path>().Any(path => path.StrokeThickness == 1.8 && path.Stroke is not null)
                    && chartCanvas.Children.OfType<TextBlock>().Count(text => double.TryParse(text.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out _)) >= 5
                    && chartCanvas.Children.OfType<Border>().Count(border => border.Width == 14 && border.Height == 14) == 2,
                    $"chart={chartCanvas.ActualWidth}x{chartCanvas.ActualHeight};view={chartFixture.ActualWidth}x{chartFixture.ActualHeight}");
                var profileMenu = (ContextMenu)chartFixture.Resources["ProfileContextMenu"];
                var calibrationCaption = profileMenu.Items.OfType<MenuItem>().Single(item => AutomationProperties.GetAutomationId(item) == "ProfileCalibrationStatus");
                var footerRuns = ((DockPanel)((Border)chartFixture.FindName("ProfileFooter")).Child).Children.OfType<TextBlock>().SelectMany(text => text.Inlines.OfType<System.Windows.Documents.Run>()).Select(run => run.Text).ToArray();
                var cellUnit = ThreeDLocalization.Shared.Resolve("ThreeD.Profile.Cells", " 셀", " cells");
                Check("D45 profile keeps raw/cell units visible and calibration identity accessible without a duplicate footer caption",
                    calibrationCaption.Header?.ToString() == "교정 없음"
                    && footerRuns.Contains(cellUnit)
                    && !footerRuns.Any(text => text.Contains("교정 없음", StringComparison.Ordinal)),
                    $"calibration={calibrationCaption.Header};footer={string.Concat(footerRuns)};unit={cellUnit}");
                Check("D44 linked cursor selects the actual valid B endpoint", mainViewer.TrySetHeightProfileCursor(1), "valid endpoint; the source midpoint may be missing");
                chartTestWindow.Dispatcher.Invoke(DispatcherPriority.Loaded, new Action(chartTestWindow.UpdateLayout));
                Check("D44 live Host cursor state reaches the chart binding", mainViewer.HostState.Profile.LinkedCursorVisible && chartFixture.ProfileState.LinkedCursorVisible,
                    $"host={mainViewer.HostState.Profile.LinkedCursorVisible};view={chartFixture.ProfileState.LinkedCursorVisible};hostLoaded={mainViewer.IsLoaded}");
                var cursorReadout = chartCanvas.Children.OfType<Border>().Single(border => AutomationProperties.GetAutomationId(border) == "ProfileCursorReadout");
                Check("D44 linked cursor stays within the chart and never scales its seven-pixel marker",
                    Canvas.GetLeft(cursorReadout) >= 58 && Canvas.GetLeft(cursorReadout) + cursorReadout.ActualWidth <= chartCanvas.ActualWidth
                    && Canvas.GetTop(cursorReadout) >= 0 && Canvas.GetTop(cursorReadout) + cursorReadout.ActualHeight <= chartCanvas.ActualHeight
                    && chartCanvas.Children.OfType<System.Windows.Shapes.Ellipse>().Single().Width == 7,
                    $"readout={Canvas.GetLeft(cursorReadout)},{Canvas.GetTop(cursorReadout)},{cursorReadout.ActualWidth}x{cursorReadout.ActualHeight}");
                Check("D57 existing inline cursor keeps the XYH readout", ((TextBlock)cursorReadout.Child).Text == mainViewer.HostState.Profile.LinkedCursorSummary, ((TextBlock)cursorReadout.Child).Text);
                OpenVisionLab.ThreeD.Viewer.Rendering.WpfScreenshotCapture.Save(
                    OpenVisionLab.ThreeD.Viewer.Rendering.WpfScreenshotCapture.Capture(chartTestWindow).Bitmap,
                    Path.ChangeExtension(reportPath, $".d44-section-{chartSize.Width:F0}.png"));
            }
            chartFixture.ShowCoordinateFooter = false;
            foreach (var drawerSize in new[] { new Size(700, 196), new Size(400, 136) })
            {
                chartTestWindow.Width = drawerSize.Width;
                chartTestWindow.Height = drawerSize.Height;
                chartTestWindow.Dispatcher.Invoke(DispatcherPriority.Loaded, new Action(chartTestWindow.UpdateLayout));
                OpenVisionLab.ThreeD.Shell.Verification.Smoke.ShellWindowNativeInterop.AppendWindowMonitorEvidence(chartTestWindow, monitorReport);
                var yLabels = chartCanvas.Children.OfType<TextBlock>().Where(text => Canvas.GetLeft(text) == 2).ToArray();
                var xLabels = chartCanvas.Children.OfType<TextBlock>().Where(text => Canvas.GetTop(text) > chartCanvas.ActualHeight - 28).ToArray();
                Check($"D45 Section drawer uses the HTML tick density at {drawerSize.Width}x{drawerSize.Height}",
                    yLabels.Length == (drawerSize.Height > 180 ? 6 : 4)
                    && xLabels.Length >= 3 && xLabels.All(text => double.TryParse(text.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                    && ((HeightProfileViewModel)chartFixture.DataContext).ProfileGridDistance == mainViewer.HostState.Profile.GridDistanceCells.ToString("0.#", CultureInfo.InvariantCulture)
                    && ((Border)chartFixture.FindName("ProfileFooter")).Visibility == Visibility.Collapsed,
                    $"yTicks={yLabels.Length};xTicks={string.Join(',', xLabels.Select(text => text.Text))};distance={((HeightProfileViewModel)chartFixture.DataContext).ProfileGridDistance}");
                OpenVisionLab.ThreeD.Viewer.Rendering.WpfScreenshotCapture.Save(
                    OpenVisionLab.ThreeD.Viewer.Rendering.WpfScreenshotCapture.Capture(chartTestWindow).Bitmap,
                    Path.ChangeExtension(reportPath, $".d45-section-drawer-{drawerSize.Width:F0}.png"));
            }
            chartFixture.UseTeachingDrawerLayout = true;
            foreach (var drawerSize in new[] { new Size(700, 196), new Size(400, 136) })
            {
                chartTestWindow.Width = drawerSize.Width;
                chartTestWindow.Height = drawerSize.Height;
                chartTestWindow.Dispatcher.Invoke(DispatcherPriority.Loaded, new Action(chartTestWindow.UpdateLayout));
                var profileState = mainViewer.HostState.Profile;
                var liveCursor = mainViewer.LinkedHeightCursor!.Value;
                var liveReadout = (TextBlock)chartCanvas.Children.OfType<Border>().Single(border => AutomationProperties.GetAutomationId(border) == "ProfileCursorReadout").Child;
                var expectedReadout = $"{profileState.GridDistanceCells.ToString("0.0", CultureInfo.InvariantCulture)} 셀 · {liveCursor.RawHeight.ToString("0.##", CultureInfo.InvariantCulture)}";
                Check($"D57 actual host cursor reaches the drawer readout at {drawerSize.Width}x{drawerSize.Height}",
                    profileState.LinkedCursorRawHeight == liveCursor.RawHeight && profileState.LinkedCursorDistanceCells == profileState.GridDistanceCells
                    && chartFixture.DrawerCursorSummary == expectedReadout && liveReadout.Text == expectedReadout,
                    $"raw={profileState.LinkedCursorRawHeight};distance={profileState.LinkedCursorDistanceCells};readout={liveReadout.Text}");
                var padding = Math.Max((profileState.MaximumRaw - profileState.MinimumRaw) * 0.08, 0.05);
                var yLabels = chartCanvas.Children.OfType<TextBlock>().Where(text => Canvas.GetLeft(text) == 2).ToArray();
                Check($"D53 drawer uses the HTML raw-axis range at {drawerSize.Width}x{drawerSize.Height}",
                    yLabels.First().Text == (profileState.MaximumRaw + padding).ToString("0.00", CultureInfo.InvariantCulture)
                    && yLabels.Last().Text == (profileState.MinimumRaw - padding).ToString("0.00", CultureInfo.InvariantCulture)
                    && ((HeightProfileViewModel)chartFixture.DataContext).ProfileDrawerGridDistance == profileState.GridDistanceCells.ToString("0.0", CultureInfo.InvariantCulture)
                    && chartCanvas.Children.OfType<System.Windows.Shapes.Ellipse>().Single().Width == 8,
                    $"axis={yLabels.First().Text}..{yLabels.Last().Text};distance={((HeightProfileViewModel)chartFixture.DataContext).ProfileDrawerGridDistance}");
                var xLabels = chartCanvas.Children.OfType<TextBlock>().Where(text => text.TextAlignment == TextAlignment.Center && Canvas.GetTop(text) == chartCanvas.ActualHeight - 22).ToArray();
                Check($"D54 drawer ticks keep the HTML endpoint centers and vertical baselines at {drawerSize.Width}x{drawerSize.Height}",
                    Canvas.GetTop(yLabels.First()) == -6 && Canvas.GetTop(yLabels.Last()) == chartCanvas.ActualHeight - 34
                    && Math.Abs(Canvas.GetLeft(xLabels.Last()) + xLabels.Last().Width / 2 - (chartCanvas.ActualWidth - 16)) < 0.01
                    && yLabels.All(label => label.FontWeight == FontWeights.Normal)
                    && chartCanvas.Children.OfType<System.Windows.Shapes.Line>().Where(line => line.StrokeDashArray is null || line.StrokeDashArray.Count == 0).All(line => line.StrokeThickness == 1),
                    "Actual rendered drawer geometry; default Main/Aux/Inline path stays separate.");
                Check($"D54 narrow toolbar condition follows the actual drawer width at {drawerSize.Width}x{drawerSize.Height}",
                    chartFixture.IsTeachingDrawerNarrow == (drawerSize.Width <= 490) && chartFixture.IsTeachingDrawerCompact == (drawerSize.Width < 650),
                    $"actual={chartFixture.ActualWidth};narrow={chartFixture.IsTeachingDrawerNarrow};compact={chartFixture.IsTeachingDrawerCompact}");
                OpenVisionLab.ThreeD.Viewer.Rendering.WpfScreenshotCapture.Save(
                    OpenVisionLab.ThreeD.Viewer.Rendering.WpfScreenshotCapture.Capture(chartTestWindow).Bitmap,
                    Path.ChangeExtension(reportPath, $".d53-section-drawer-{drawerSize.Width:F0}.png"));
            }
            var beforeCursorLeave = mainViewer.HostState.Profile;
            var drawerAdapter = (HeightProfileViewModel)chartFixture.DataContext;
            chartCanvas.RaiseEvent(new System.Windows.Input.MouseEventArgs(System.Windows.Input.Mouse.PrimaryDevice, Environment.TickCount) { RoutedEvent = System.Windows.Input.Mouse.MouseLeaveEvent });
            chartTestWindow.Dispatcher.Invoke(DispatcherPriority.Loaded, new Action(chartTestWindow.UpdateLayout));
            Check("D53 existing cursor no-hit command clears cursor without changing endpoints or executing inspection",
                !mainViewer.HostState.Profile.LinkedCursorVisible && !chartFixture.ProfileState.LinkedCursorVisible
                && double.IsNaN(mainViewer.HostState.Profile.LinkedCursorRawHeight) && double.IsNaN(mainViewer.HostState.Profile.LinkedCursorDistanceCells)
                && drawerAdapter.ProfileDrawerLinkedCursorSummary.Length == 0 && chartFixture.DrawerCursorSummary.Length == 0
                && mainViewer.HostState.Profile.EndpointSummary == beforeCursorLeave.EndpointSummary
                && mainViewer.HostState.Profile.PathData == beforeCursorLeave.PathData
                && !authoredWorkbench.IsDirty && mainViewer.HostState.Inspection.PublishedResultCount == 0,
                "synthetic View mouse-leave event through the existing command; actual pointer-leave remains a separate Computer Use check");
            mainViewer.TrySetHeightProfileCursor(1);
            chartFixture.SetCurrentValue(HeightProfileView.ProfileStateProperty, mainViewer.HostState.Profile with
            {
                PathData = "M 0,51 L 240,51", ValidSampleCount = 2, MissingSampleCount = 0,
                MinimumRaw = 10, MaximumRaw = 10, LinkedCursorVisible = false
            });
            chartTestWindow.UpdateLayout();
            var flatCurve = chartCanvas.Children.OfType<System.Windows.Shapes.Path>().Single(path => path.Stroke is not null);
            Check("D53 constant raw-height section is centered with minimum 0.05 axis padding",
                Math.Abs(flatCurve.Data.Transform.Transform(new Point(0, 51)).Y - (chartCanvas.ActualHeight - 28) / 2) < 0.01
                && chartCanvas.Children.OfType<TextBlock>().Any(text => text.Text == "10.05")
                && chartCanvas.Children.OfType<TextBlock>().Any(text => text.Text == "9.95"),
                "isolated constant-height render fixture; raw value remains 10");
            var normalDrawerState = chartFixture.ProfileState;
            foreach (var drawerSize in new[] { new Size(700, 196), new Size(400, 136) })
            {
                chartTestWindow.Width = drawerSize.Width;
                chartTestWindow.Height = drawerSize.Height;
                chartFixture.SetCurrentValue(HeightProfileView.ProfileStateProperty, normalDrawerState with
                {
                    MinimumRaw = -1234567.875, MaximumRaw = -1234567.375, LinkedCursorVisible = false
                });
                chartTestWindow.Dispatcher.Invoke(DispatcherPriority.Loaded, new Action(chartTestWindow.UpdateLayout));
                var labels = chartCanvas.Children.OfType<TextBlock>().Where(text => Canvas.GetLeft(text) == 2).ToArray();
                var measuredLabel = new TextBlock { Text = "-1234567.33", FontFamily = new FontFamily("Consolas"), FontSize = 10.5 };
                measuredLabel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                var endpointA = chartCanvas.Children.OfType<Border>().Single(border => border.Child is TextBlock { Text: "A" });
                var info = (FrameworkElement)chartFixture.FindName(drawerSize.Width < 650 ? "ProfileCompactInfo" : "ProfileWideInfo");
                Check($"D57 long signed drawer ticks remain distinct and align with the plot at {drawerSize.Width}x{drawerSize.Height}",
                    labels.First().Text == "-1234567.33" && labels.Last().Text == "-1234567.93"
                    && labels.All(label => label.Width >= measuredLabel.DesiredSize.Width && label.ActualHeight < 20)
                    && Math.Abs(Canvas.GetLeft(endpointA) + 7 - info.Margin.Left) < 0.01,
                    $"tick={labels.First().Text}..{labels.Last().Text};labelWidth={labels.First().Width};required={measuredLabel.DesiredSize.Width};left={info.Margin.Left}");
                OpenVisionLab.ThreeD.Viewer.Rendering.WpfScreenshotCapture.Save(
                    OpenVisionLab.ThreeD.Viewer.Rendering.WpfScreenshotCapture.Capture(chartTestWindow).Bitmap,
                    Path.ChangeExtension(reportPath, $".d57-long-signed-drawer-{drawerSize.Width:F0}.png"));
            }
            chartFixture.SetCurrentValue(HeightProfileView.ProfileStateProperty, normalDrawerState);
            chartTestWindow.UpdateLayout();
            Check("D57 ordinary drawer values restore the existing 58-pixel plot origin", ((FrameworkElement)chartFixture.FindName("ProfileCompactInfo")).Margin.Left == 58, "long signed -> normal raw values");
            chartFixture.UseTeachingDrawerLayout = false;
            chartFixture.SetCurrentValue(HeightProfileView.ProfileStateProperty, mainViewer.HostState.Profile);
            chartFixture.ShowCoordinateFooter = true;
            var missingRow = -1;
            var missingColumn = -1;
            for (var row = 0; row < profileInput.Height && missingColumn < 0; row++)
            {
                for (var column = 0; column < profileInput.Width; column++)
                {
                    profileInput.InvalidCellMap.TryIsMissing(column, row, out var isMissing);
                    if (!isMissing) continue;
                    missingRow = row;
                    missingColumn = column;
                    break;
                }
            }
            var profileBeforeMissingPick = mainViewer.HostState.Profile;
            Check("D44 actual missing input cell cannot become a profile endpoint",
                missingColumn >= 0 && mainViewer.TryRequestHeightProfile(OpenVisionLab.ThreeD.Viewer.Hosting.ViewerHeightProfileAction.Repick)
                && !mainViewer.TryPickHeightProfileCell(new(profileInput.SourceContentSha256, missingRow, missingColumn))
                && mainViewer.HostState.Profile.EndpointSummary == profileBeforeMissingPick.EndpointSummary
                && mainViewer.HostState.Profile.GridDistanceCells == profileBeforeMissingPick.GridDistanceCells,
                $"row={missingRow};column={missingColumn};mask={profileInput.InvalidCellMap.Sha256}");
            Check("D44 cancel after a missing-cell pick restores the real pair and cursor",
                mainViewer.TryRequestHeightProfile(OpenVisionLab.ThreeD.Viewer.Hosting.ViewerHeightProfileAction.Cancel)
                && mainViewer.HostState.Profile.EndpointSummary == profileBeforeMissingPick.EndpointSummary
                && mainViewer.HostState.Profile.GridDistanceCells == profileBeforeMissingPick.GridDistanceCells
                && mainViewer.TrySetHeightProfileCursor(1),
                $"endpoints={mainViewer.HostState.Profile.EndpointSummary};distance={mainViewer.HostState.Profile.GridDistanceCells}");
            chartFixture.SetCurrentValue(HeightProfileView.ProfileStateProperty, mainViewer.HostState.Profile with
            {
                PathData = "M 0,51 L 60,40 M 180,15 L 240,3", ValidSampleCount = 4, MissingSampleCount = 2,
                MinimumRaw = 1, MaximumRaw = 2, LinkedCursorVisible = false
            });
            chartTestWindow.UpdateLayout();
            Check("D44 isolated gap fixture hatches gaps without joining them",
                chartCanvas.Children.OfType<Canvas>().Count() == 1
                && chartCanvas.Children.OfType<System.Windows.Shapes.Path>().Any(path => path.Stroke is not null && path.Data is PathGeometry { Figures.Count: 2 }),
                "typed render fixture, not a product-input or pointer claim");
            chartFixture.UseTeachingDrawerLayout = true;
            chartTestWindow.UpdateLayout();
            Check("D53 drawer missing band is labeled and separate valid segments stay disconnected",
                chartCanvas.Children.OfType<TextBlock>().Any(text => text.Text == "결측")
                && chartCanvas.Children.OfType<Canvas>().Single().Children.Count == 0
                && chartCanvas.Children.OfType<System.Windows.Shapes.Path>().Any(path => path.Stroke is not null && path.Data is PathGeometry { Figures.Count: 2 }),
                "isolated missing-section render fixture; no interpolation across a missing band");
            chartFixture.SetCurrentValue(HeightProfileView.ProfileStateProperty, OpenVisionLab.ThreeD.Viewer.Hosting.ViewerHostProfileState.Empty);
            chartTestWindow.UpdateLayout();
            Check("D44 empty profile clears axes, cursor, endpoints and the default placeholder path", chartCanvas.Children.Count == 0, $"children={chartCanvas.Children.Count}");
            using (var emptyProfileHost = new OpenVisionThreeDViewerControl(loadDefaultSamples: false))
            {
                chartFixture.ViewerHost = emptyProfileHost;
                chartTestWindow.Dispatcher.Invoke(DispatcherPriority.Loaded, new Action(chartTestWindow.UpdateLayout));
                var emptyCaption = (TextBlock)chartFixture.FindName("DrawerProfileEmptyMessage");
                Check("D53 drawer distinguishes no input with a visible wrapped instruction",
                    emptyCaption.IsVisible && emptyCaption.Text == "입력이 없습니다" && emptyCaption.TextWrapping == TextWrapping.Wrap,
                    $"text={emptyCaption.Text};visible={emptyCaption.IsVisible}");
                chartFixture.ViewerHost = mainViewer;
            }
            mainViewer.TryRequestHeightProfile(OpenVisionLab.ThreeD.Viewer.Hosting.ViewerHeightProfileAction.Clear);
            chartTestWindow.Dispatcher.Invoke(DispatcherPriority.Loaded, new Action(chartTestWindow.UpdateLayout));
            var selectCaption = (TextBlock)chartFixture.FindName("DrawerProfileEmptyMessage");
            Check("D53 drawer with actual input and no A/B gives the HTML section-tool instruction",
                selectCaption.IsVisible && selectCaption.Text == "단면 도구(S)로 3D 또는 높이 보기에서 A–B를 그리세요"
                && !((HeightProfileViewModel)chartFixture.DataContext).ProfileAllSamplesMissing,
                $"text={selectCaption.Text};visible={selectCaption.IsVisible}");
            var actualDrawerAdapter = chartFixture.DataContext;
            chartFixture.DataContext = new { ProfileHasEndpoints = true, ProfileAllSamplesMissing = true, ProfileHasInput = true, ProfileIsCollapsed = false };
            chartFixture.SetCurrentValue(HeightProfileView.ProfileStateProperty, beforeCursorLeave with { ValidSampleCount = 0, MissingSampleCount = 2, PathData = string.Empty, LinkedCursorVisible = false });
            chartTestWindow.Dispatcher.Invoke(DispatcherPriority.Loaded, new Action(chartTestWindow.UpdateLayout));
            Check("D53 isolated all-missing snapshot shows an explicit message without stale axes or cursor",
                selectCaption.IsVisible && selectCaption.Text == "단면 구간이 모두 결측입니다" && chartCanvas.Children.Count == 0
                && ((Grid)chartFixture.FindName("ProfileChartBody")).Children.OfType<DockPanel>().All(header => header.Visibility == Visibility.Collapsed)
                && ((Grid)chartFixture.FindName("ProfileChartBody")).Children.OfType<Grid>().All(header => header.Visibility == Visibility.Collapsed),
                "isolated View binding fixture; message replaces both information rows; actual endpoint selection rejects missing cells");
            chartFixture.DataContext = actualDrawerAdapter;
            chartTestWindow.Close();
            mainViewer.TryRequestHeightProfile(OpenVisionLab.ThreeD.Viewer.Hosting.ViewerHeightProfileAction.Clear);
            mainViewer.TrySetSelectionMode(beforeProfileMode);
            mainThreeDKind.Command.Execute(mainThreeDKind.CommandParameter);
            layoutTestWindow.UpdateLayout();
            Check("v3 3D tab restores the real renderer and hides both alternate visual trees",
                mainThreeDKind.IsChecked == true && mainViewer.IsVisible && !mainPaneHeightView.IsVisible && !mainPaneProfile.IsVisible,
                $"3d={mainViewer.IsVisible};height={mainPaneHeightView.IsVisible};section={mainPaneProfile.IsVisible}");
            var mainZoomBefore = authoredWorkbench.MainViewerPane.HeightImage.ZoomPercent;
            var fourthHeight = authoredWorkbench.HeightViewerPane.HeightImage;
            fourthHeight.SetZoom(175);
            Check("v3 height panes share input/ROI evidence while keeping their display scale independent",
                !ReferenceEquals(fourthHeight, authoredWorkbench.MainViewerPane.HeightImage)
                && ReferenceEquals(fourthHeight.Frame, authoredWorkbench.MainViewerPane.HeightImage.Frame)
                && ReferenceEquals(fourthHeight.RoiWorkspace, authoredWorkbench.HeightImageViewer.RoiWorkspace)
                && authoredWorkbench.MainViewerPane.HeightImage.ZoomPercent == mainZoomBefore && fourthHeight.ZoomPercent == 175,
                $"main={authoredWorkbench.MainViewerPane.HeightImage.ZoomPercent};fourth={fourthHeight.ZoomPercent};sameFrame={ReferenceEquals(fourthHeight.Frame, authoredWorkbench.MainViewerPane.HeightImage.Frame)};mainSource={authoredWorkbench.MainViewerPane.HeightImage.Frame?.SourcePath};fourthSource={fourthHeight.Frame?.SourcePath};fourthError={fourthHeight.Error}");
            var lowerThreeDKind = OpenVisionLab.ThreeD.Shell.Verification.Smoke.ShellSmokeArtifacts.FindVisualDescendants<ToggleButton>(quadSlots[3])
                .Single(button => AutomationProperties.GetAutomationId(button) == "PaneKindThreeD");
            lowerThreeDKind.Command.Execute(lowerThreeDKind.CommandParameter);
            layoutTestWindow.UpdateLayout();
            var lowerNativeViewer = ((ContentControl)layoutWorkspaceView.FindName("FourHeightSlotThreeDHost")).Content as OpenVisionThreeDViewerControl;
            Check("v3 fourth pane switches to an independently owned 3D renderer of the same actual source",
                lowerThreeDKind.IsChecked == true && lowerNativeViewer?.CurrentC3DSourcePath == authoredWorkbench.Source.Path
                && authoredWorkbench.MainViewerPane.IsThreeD && authoredWorkbench.SectionViewerPane.IsSection
                && authoredWorkbench.AuxiliaryViewerContentId == quadAuxiliaryPin && authoredWorkbench.IsDirty == quadDirty,
                $"lower={lowerThreeDKind.IsChecked};source={lowerNativeViewer?.CurrentC3DSourcePath};pin={authoredWorkbench.AuxiliaryViewerContentId}");
            Progress("before settings preview constructor");
            using (var previewWorkbench = new ToolWorkbenchViewModel(Path.Combine(Path.GetDirectoryName(reportPath)!, "settings-preview-recent.json")))
            {
                Progress("before settings preview open");
                if (!previewWorkbench.TryOpenTeachingRecipe(recipePath, out var previewOpenMessage)) throw new InvalidOperationException(previewOpenMessage);
                var previewStep = previewWorkbench.PipelineSteps.First();
                previewWorkbench.SelectedPipelineStep = previewStep;
                var previewInputs = previewStep.InputEntityIds.ToArray();
                Progress("before settings view creation");
                var settingsView = new SelectedToolWorkspaceView { DataContext = previewWorkbench, Width = 290, HorizontalAlignment = HorizontalAlignment.Left };
                var settingsTestWindow = new Window
                {
                    Owner = layoutTestWindow,
                    Title = "D70 settings presentation fixture",
                    Width = settingsView.Width,
                    Height = layoutTestWindow.Height,
                    WindowStyle = WindowStyle.None,
                    ResizeMode = ResizeMode.NoResize,
                    Left = testWorkArea.Left,
                    Top = testWorkArea.Top,
                    ShowInTaskbar = false,
                    ShowActivated = false,
                    Content = settingsView
                };
                try
                {
                    void SelectSettingsTab(string name)
                    {
                        ((RadioButton)settingsView.FindName(name)).SetCurrentValue(ToggleButton.IsCheckedProperty, true);
                        settingsTestWindow.Dispatcher.Invoke(DispatcherPriority.Loaded, new Action(settingsTestWindow.UpdateLayout));
                    }
                    Progress("before independent settings view attach");
                    settingsTestWindow.Show();
                    SelectSettingsTab("EditorDetailTab");
                    SelectSettingsTab("DetailIoTab");
                    settingsView.BringOutputIntoView();
                    settingsTestWindow.Dispatcher.Invoke(DispatcherPriority.Loaded, new Action(settingsTestWindow.UpdateLayout));
                    Progress($"settings I/O fixture: detail={((RadioButton)settingsView.FindName("EditorDetailTab")).IsChecked};io={((RadioButton)settingsView.FindName("DetailIoTab")).IsChecked};expanded={((Expander)settingsView.FindName("SettingsDetails")).IsExpanded}");
                    OpenVisionLab.ThreeD.Shell.Verification.Smoke.ShellWindowNativeInterop.AppendWindowMonitorEvidence(settingsTestWindow, monitorReport);
                    Progress("before preview await");
                    WaitForPresentationTask(previewWorkbench.SelectedToolWorkspace.DataInputs.Single().Preview!.ObservedLoadTask);
                    settingsTestWindow.Dispatcher.Invoke(DispatcherPriority.Loaded, new Action(settingsTestWindow.UpdateLayout));
                    var settingsInput = previewWorkbench.SelectedToolWorkspace.DataInputs.Single();
                    var previewImage = OpenVisionLab.ThreeD.Shell.Verification.Smoke.ShellSmokeArtifacts.FindVisualDescendants<Image>(settingsView)
                        .Single(image => AutomationProperties.GetAutomationId(image) == "SelectedToolInputThumbnail");
                    var inputPicker = OpenVisionLab.ThreeD.Shell.Verification.Smoke.ShellSmokeArtifacts.FindVisualDescendants<ComboBox>(settingsView)
                        .Single(picker => AutomationProperties.GetAutomationId(picker) == "SelectedToolInputRoute");
                    var previewSha = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(previewWorkbench.Source.Path)));
                    Check("v3 Input shows one identified native map while preserving both typed ROI slots",
                        settingsInput.Preview?.Frame?.SourceContentSha256 == previewSha
                        && previewImage.IsVisible && previewImage.Source is BitmapSource bitmap && bitmap.PixelWidth == settingsInput.Preview.Frame.Width
                        && bitmap.PixelHeight == settingsInput.Preview.Frame.Height && inputPicker.SelectedValue as string == previewInputs[0]
                        && previewWorkbench.SelectedToolWorkspace.Inputs.Count == 3 && previewWorkbench.SelectedToolWorkspace.Regions.Count == 2
                        && previewWorkbench.SelectedToolWorkspace.CurrentOutput?.HasExecutionResult == false
                        && !previewWorkbench.IsDirty && !previewWorkbench.IsOrderedRunRunning && !previewWorkbench.IsSelectedStepPreviewRunning,
                        $"image={previewImage.Source is BitmapSource};input={inputPicker.SelectedValue};hash={settingsInput.Preview?.Frame?.SourceContentSha256};dirty={previewWorkbench.IsDirty}");
                    var settingsScroller = (ScrollViewer)settingsView.FindName("SelectedToolScrollViewer");
                    var settingsActions = (Border)settingsView.FindName("SelectedToolActionBar");
                    var scrollBounds = settingsScroller.TransformToAncestor(settingsView).TransformBounds(new Rect(settingsScroller.RenderSize));
                    var actionBounds = settingsActions.TransformToAncestor(settingsView).TransformBounds(new Rect(settingsActions.RenderSize));
                    var pickerBounds = inputPicker.TransformToAncestor(settingsView).TransformBounds(new Rect(inputPicker.RenderSize));
                    Check("v3 settings inputs are rendered above the separate action footer",
                        settingsScroller.IsVisible && inputPicker.IsVisible && settingsActions.IsVisible
                        && scrollBounds.Bottom <= actionBounds.Top + 1 && pickerBounds.Top >= 0
                        && pickerBounds.Bottom <= scrollBounds.Top + 1 && !settingsScroller.IsAncestorOf(inputPicker)
                        && Grid.GetRow(settingsActions) == 3,
                        $"scroll={scrollBounds};action={actionBounds};input={pickerBounds}");
                    SelectSettingsTab("EditorRegionTab");
                    var appliedRoiColumn = OpenVisionLab.ThreeD.Shell.Verification.Smoke.ShellSmokeArtifacts.FindVisualDescendants<TextBox>(settingsView).Single(editor => AutomationProperties.GetAutomationId(editor) == "AppliedRoiColumn");
                    var appliedRoiRow = OpenVisionLab.ThreeD.Shell.Verification.Smoke.ShellSmokeArtifacts.FindVisualDescendants<TextBox>(settingsView).Single(editor => AutomationProperties.GetAutomationId(editor) == "AppliedRoiRow");
                    var appliedRoiWidth = OpenVisionLab.ThreeD.Shell.Verification.Smoke.ShellSmokeArtifacts.FindVisualDescendants<TextBox>(settingsView).Single(editor => AutomationProperties.GetAutomationId(editor) == "AppliedRoiColumnCount");
                    var appliedRoiHeight = OpenVisionLab.ThreeD.Shell.Verification.Smoke.ShellSmokeArtifacts.FindVisualDescendants<TextBox>(settingsView).Single(editor => AutomationProperties.GetAutomationId(editor) == "AppliedRoiRowCount");
                    var appliedRectangle = previewWorkbench.SelectedToolWorkspace.ActiveRegion?.Rectangle;
                    Check("v3 ROI fields display the actual applied rectangle and preserve explicit editing",
                        appliedRectangle is not null && appliedRoiColumn.Text == appliedRectangle.Column.ToString()
                        && appliedRoiRow.Text == appliedRectangle.Row.ToString() && appliedRoiWidth.Text == appliedRectangle.ColumnCount.ToString()
                        && appliedRoiHeight.Text == appliedRectangle.RowCount.ToString()
                        && new[] { appliedRoiColumn, appliedRoiRow, appliedRoiWidth, appliedRoiHeight }.All(editor => editor.IsReadOnly)
                        && !previewWorkbench.IsDirty && !previewWorkbench.IsOrderedRunRunning && !previewWorkbench.IsSelectedStepPreviewRunning,
                        $"rectangle={appliedRectangle};column={appliedRoiColumn.Text};row={appliedRoiRow.Text};width={appliedRoiWidth.Text};height={appliedRoiHeight.Text}");
                    var settingsWidthBeforeRoiCheck = settingsView.Width;
                    foreach (var (viewWidth, expectedFieldWidth) in new[] { (286d, 128d), (316d, 143d) })
                    {
                        settingsView.Width = viewWidth;
                        settingsTestWindow.Dispatcher.Invoke(DispatcherPriority.Loaded, new Action(settingsTestWindow.UpdateLayout));
                        var roiFields = new[] { appliedRoiColumn, appliedRoiRow, appliedRoiWidth, appliedRoiHeight };
                        var columnBounds = appliedRoiColumn.TransformToAncestor(settingsView).TransformBounds(new Rect(appliedRoiColumn.RenderSize));
                        var rowBounds = appliedRoiRow.TransformToAncestor(settingsView).TransformBounds(new Rect(appliedRoiRow.RenderSize));
                        var widthBounds = appliedRoiWidth.TransformToAncestor(settingsView).TransformBounds(new Rect(appliedRoiWidth.RenderSize));
                        var verticalBar = (ScrollBar)settingsScroller.Template.FindName("PART_VerticalScrollBar", settingsScroller);
                        var fieldWidthWithGutter = expectedFieldWidth - verticalBar.ActualWidth / 2;
                        Check($"ROI fields retain equal 30px rows and 10px gaps beside the approved scrollbar gutter at {viewWidth}px",
                            roiFields.All(editor => Math.Abs(editor.ActualHeight - 30) < 0.25 && Math.Abs(editor.ActualWidth - fieldWidthWithGutter) < 0.25)
                            && Math.Abs(rowBounds.Left - columnBounds.Right - 10) < 0.25
                            && Math.Abs(widthBounds.Top - columnBounds.Top - 59.4) < 0.25,
                            $"field={appliedRoiColumn.ActualWidth:F2}x{appliedRoiColumn.ActualHeight:F2};gap={rowBounds.Left - columnBounds.Right:F2};rowDelta={widthBounds.Top - columnBounds.Top:F2}");
                    }
                    settingsView.Width = settingsWidthBeforeRoiCheck;
                    settingsTestWindow.Dispatcher.Invoke(DispatcherPriority.Loaded, new Action(settingsTestWindow.UpdateLayout));
                    OpenVisionLab.ThreeD.Viewer.Rendering.WpfScreenshotCapture.Save(
                        OpenVisionLab.ThreeD.Viewer.Rendering.WpfScreenshotCapture.Capture(settingsView).Bitmap,
                        Path.ChangeExtension(reportPath, ".settings-input-290.png"));
                    SelectSettingsTab("EditorDetailTab");
                    SelectSettingsTab("DetailIoTab");
                    previewStep.InputEntityIdsText = string.Join("; ", new[] { previewInputs[1], previewInputs[1], previewInputs[2] });
                    settingsTestWindow.Dispatcher.Invoke(DispatcherPriority.Loaded, new Action(settingsTestWindow.UpdateLayout));
                    Check("v3 a non-map input clears previous thumbnail pixels before recovery",
                        previewWorkbench.SelectedToolWorkspace.DataInputs.Single().Preview?.Frame is null
                        && !previewWorkbench.IsOrderedRunRunning && !previewWorkbench.IsSelectedStepPreviewRunning,
                        $"frame={previewWorkbench.SelectedToolWorkspace.DataInputs.Single().Preview?.Frame is not null};route={previewStep.InputEntityIdsText}");
                    inputPicker = OpenVisionLab.ThreeD.Shell.Verification.Smoke.ShellSmokeArtifacts.FindVisualDescendants<ComboBox>(settingsView)
                        .Single(picker => AutomationProperties.GetAutomationId(picker) == "SelectedToolInputRoute");
                    Progress("before picker popup");
                    inputPicker.IsDropDownOpen = true;
                    Progress("before picker choice");
                    inputPicker.SelectedValue = previewInputs[0];
                    Progress("before picker close");
                    inputPicker.IsDropDownOpen = false;
                    Progress("before preview await");
                    WaitForPresentationTask(previewWorkbench.SelectedToolWorkspace.DataInputs.Single().Preview!.ObservedLoadTask);
                    settingsTestWindow.Dispatcher.Invoke(DispatcherPriority.Loaded, new Action(settingsTestWindow.UpdateLayout));
                    inputPicker = OpenVisionLab.ThreeD.Shell.Verification.Smoke.ShellSmokeArtifacts.FindVisualDescendants<ComboBox>(settingsView)
                        .Single(picker => AutomationProperties.GetAutomationId(picker) == "SelectedToolInputRoute");
                    Check("v3 rendered input picker repairs its slot through the existing validator without changing either ROI",
                        previewStep.InputEntityIds.SequenceEqual(previewInputs) && inputPicker.SelectedValue as string == previewInputs[0]
                        && previewWorkbench.SelectedToolWorkspace.DataInputs.Single().Preview?.Frame?.SourceContentSha256 == previewSha
                        && !previewWorkbench.HasPendingFlowConnection && !previewWorkbench.IsOrderedRunRunning && !previewWorkbench.IsSelectedStepPreviewRunning,
                        $"route={previewStep.InputEntityIdsText};picker={inputPicker.SelectedValue};pending={previewWorkbench.HasPendingFlowConnection}");
                    var rejectedChoice = new SelectedToolInputChoice(previewStep.FlowInputs[0], previewWorkbench.PipelineSteps[1].OutputEntityId, "Wrong typed producer", true);
                    Progress("before invalid producer command");
                    previewWorkbench.ChooseWorkspaceInputCommand.Execute(rejectedChoice);
                    Check("v3 invalid typed producer is rejected and releases the transient connection",
                        previewStep.InputEntityIds.SequenceEqual(previewInputs) && !previewWorkbench.HasPendingFlowConnection
                        && previewWorkbench.SelectedToolWorkspace.DataInputs.Single().Preview?.Frame?.SourceContentSha256 == previewSha
                        && !previewWorkbench.IsOrderedRunRunning && !previewWorkbench.IsSelectedStepPreviewRunning,
                        $"route={previewStep.InputEntityIdsText};pending={previewWorkbench.HasPendingFlowConnection};feedback={previewWorkbench.FlowConnectionMessage}");

                    SelectSettingsTab("EditorJudgmentTab");
                    SelectSettingsTab("EditorAllTab");
                    var conditionsGrid = (RecipeStepPropertyGridHost)settingsView.FindName("StepPropertyGrid");
                    var acceptanceGrid = (RecipeStepPropertyGridHost)settingsView.FindName("AcceptancePropertyGrid");
                    var advancedSamples = (CheckBox)settingsView.FindName("ShowAdvancedSamples");
                    var parameterDisplayDirtyBefore = previewWorkbench.IsDirty;
                    conditionsGrid.CommitPendingEdit(out _);
                    acceptanceGrid.CommitPendingEdit(out _);
                    var parameterDraft = (ThicknessStepProperties)previewWorkbench.SelectedToolWorkspace.ParameterDraft!;
                    Check("v3 normal settings place both thickness limits in Decision and keep the same draft",
                        conditionsGrid.VisiblePropertyCount == 1 && acceptanceGrid.VisiblePropertyCount == 2
                        && conditionsGrid.ShowAdvancedParameters && advancedSamples.IsChecked == true && advancedSamples.Visibility == Visibility.Collapsed
                        && ReferenceEquals(conditionsGrid.SelectedObject, parameterDraft) && ReferenceEquals(acceptanceGrid.SelectedObject, parameterDraft)
                        && previewWorkbench.IsDirty == parameterDisplayDirtyBefore && !previewWorkbench.IsOrderedRunRunning && !previewWorkbench.IsSelectedStepPreviewRunning,
                        $"conditions={conditionsGrid.VisiblePropertyCount};decision={acceptanceGrid.VisiblePropertyCount};sameDraft={ReferenceEquals(conditionsGrid.SelectedObject, acceptanceGrid.SelectedObject)};dirty={parameterDisplayDirtyBefore}/{previewWorkbench.IsDirty}");
                    conditionsGrid.CommitPendingEdit(out _);
                    acceptanceGrid.CommitPendingEdit(out _);
                    var conditionNames = TypeDescriptor.GetProperties(((System.Windows.Controls.WpfPropertyGrid.PropertyGrid)conditionsGrid.FindName("InnerGrid")).SelectedObject).Cast<PropertyDescriptor>().Select(property => property.Name).ToArray();
                    var decisionNames = TypeDescriptor.GetProperties(((System.Windows.Controls.WpfPropertyGrid.PropertyGrid)acceptanceGrid.FindName("InnerGrid")).SelectedObject).Cast<PropertyDescriptor>().Select(property => property.Name).ToArray();
                    Check("v3 Conditions and Decision preserve every thickness parameter without duplication",
                        conditionNames.SequenceEqual(new[] { nameof(ThicknessStepProperties.MinimumValidSampleCount) })
                        && decisionNames.ToHashSet().SetEquals(new[] { nameof(ThicknessStepProperties.MinimumThickness), nameof(ThicknessStepProperties.MaximumThickness) })
                        && conditionsGrid.VisiblePropertyCount + acceptanceGrid.VisiblePropertyCount == 3,
                        $"conditions={string.Join(",", conditionNames)};decision={string.Join(",", decisionNames)}");
                    var applyParameters = (Button)settingsView.FindName("SelectedToolFooterApplyButton");
                    Progress("resolved named Teaching Apply button before draft");
                    var originalParameters = previewStep.Parameters.ToDictionary(parameter => parameter.Name, parameter => parameter.Value);
                    foreach (var gridAndField in new[] { (conditionsGrid, nameof(ThicknessStepProperties.MinimumValidSampleCount)), (acceptanceGrid, nameof(ThicknessStepProperties.MinimumThickness)) })
                    {
                        SelectSettingsTab(ReferenceEquals(gridAndField.Item1, conditionsGrid) ? "EditorAllTab" : "EditorJudgmentTab");
                        var numericEditor = OpenVisionLab.ThreeD.Shell.Verification.Smoke.ShellSmokeArtifacts.FindVisualDescendants<TextBox>(gridAndField.Item1)
                            .Single(editor => AutomationProperties.GetAutomationId(editor) == gridAndField.Item2);
                        numericEditor.BringIntoView();
                        numericEditor.Focus();
                        numericEditor.SetCurrentValue(TextBox.TextProperty, "invalid-number");
                        previewWorkbench.MarkSelectedStepParameterDraftDirtyCommand.Execute(null);
                        settingsTestWindow.Dispatcher.Invoke(DispatcherPriority.Render, new Action(settingsTestWindow.UpdateLayout));
                        Check($"v3 malformed {gridAndField.Item2} exposes the Teaching draft footer", applyParameters.IsVisible, $"visible={applyParameters.IsVisible};pending={previewWorkbench.HasPendingStepParameterChanges}");
                        applyParameters.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                        var parametersPreserved = previewStep.Parameters.All(parameter => originalParameters[parameter.Name] == parameter.Value);
                        var editRejected = !settingsView.CommitPendingParameterEdit(out _);
                        Check($"v3 shared Apply rejects malformed {gridAndField.Item2} without mutating recipe or executing",
                            Validation.GetHasError(numericEditor) && previewWorkbench.HasPendingStepParameterChanges
                            && parametersPreserved && editRejected && !previewWorkbench.IsOrderedRunRunning && !previewWorkbench.IsSelectedStepPreviewRunning,
                            $"error={Validation.GetHasError(numericEditor)};pending={previewWorkbench.HasPendingStepParameterChanges};preserved={parametersPreserved};rejected={editRejected};acceptanceVisible={acceptanceGrid.IsVisible};parameters={string.Join(';', previewStep.Parameters.Select(parameter => parameter.Name + '=' + parameter.Value))};status={previewWorkbench.StepParameterEditStatus}");
                        numericEditor.SetCurrentValue(TextBox.TextProperty, originalParameters[gridAndField.Item2]);
                        if (!gridAndField.Item1.CommitPendingEdit(out var recoveredMessage)) throw new InvalidOperationException(recoveredMessage);
                    }
                    applyParameters.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                    Check("v3 shared Apply recovers with valid input and preserves the execution boundary",
                        !previewWorkbench.HasPendingStepParameterChanges && settingsView.CommitPendingParameterEdit(out _)
                        && previewStep.Parameters.All(parameter => originalParameters[parameter.Name] == parameter.Value)
                        && !previewWorkbench.IsOrderedRunRunning && !previewWorkbench.IsSelectedStepPreviewRunning,
                        $"pending={previewWorkbench.HasPendingStepParameterChanges};status={previewWorkbench.StepParameterEditStatus}");
                    settingsView.IsLabPresentation = true;
                    settingsTestWindow.Dispatcher.Invoke(DispatcherPriority.Render, new Action(settingsTestWindow.UpdateLayout));
                    Check("v3 Tool Lab preserves sample detail revealed during error recovery", advancedSamples.IsChecked == true && advancedSamples.IsVisible,
                        $"advanced={advancedSamples.IsChecked}/{advancedSamples.IsVisible};filter={conditionsGrid.AcceptanceParametersOnly};compact={conditionsGrid.IsCompactPresentation};properties={conditionsGrid.VisiblePropertyCount}");
                    advancedSamples.SetCurrentValue(ToggleButton.IsCheckedProperty, true);
                    conditionsGrid.CommitPendingEdit(out _);
                    settingsTestWindow.Dispatcher.Invoke(DispatcherPriority.Render, new Action(settingsTestWindow.UpdateLayout));
                    Check("v3 Tool Lab restores the full original PropertyGrid and hides the duplicate Decision editor",
                        conditionsGrid.AcceptanceParametersOnly is null && conditionsGrid.VisiblePropertyCount == 3
                        && acceptanceGrid.Visibility == Visibility.Collapsed && ReferenceEquals(conditionsGrid.SelectedObject, previewWorkbench.SelectedToolWorkspace.ParameterDraft),
                        $"filter={conditionsGrid.AcceptanceParametersOnly};properties={conditionsGrid.VisiblePropertyCount};decision={acceptanceGrid.Visibility}");
                }
                finally
                {
                    settingsTestWindow.Close();
                }
                Check("D70 separate Settings fixture preserves the loaded Viewer command target",
                    layoutWorkspaceView.IsLoaded && layoutWorkspaceView.HasAttachedMainViewer && ReferenceEquals(layoutWorkspaceView.DataContext, authoredWorkbench),
                    $"loaded={layoutWorkspaceView.IsLoaded};attached={layoutWorkspaceView.HasAttachedMainViewer};sameContext={ReferenceEquals(layoutWorkspaceView.DataContext, authoredWorkbench)}");
            }
            authoredWorkbench.SetFourViewerLayoutCommand.Execute(null);
            layoutWorkspaceView.UpdateLayout();
            var restoreFirstRow = ((RowDefinition)layoutWorkspaceView.FindName("FirstRow")).Height;
            var restoreSecondRow = ((RowDefinition)layoutWorkspaceView.FindName("SecondRow")).Height;
            var restoreFirstColumn = ((ColumnDefinition)layoutWorkspaceView.FindName("FirstColumn")).Width;
            var restoreSecondColumn = ((ColumnDefinition)layoutWorkspaceView.FindName("SecondColumn")).Width;
            var sectionMaximize = OpenVisionLab.ThreeD.Shell.Verification.Smoke.ShellSmokeArtifacts.FindVisualDescendants<Button>(layoutWorkspaceView)
                .Single(button => AutomationProperties.GetAutomationId(button) == "FourSectionPaneMaximize");
            var auxiliaryContentBeforeMaximize = ((ContentControl)layoutWorkspaceView.FindName("AuxiliaryViewerHost")).Content;
            sectionMaximize.Command.Execute(sectionMaximize.CommandParameter);
            layoutWorkspaceView.UpdateLayout();
            Check("D45 bound section pane maximize fills the workspace without changing inputs or execution",
                authoredWorkbench.MaximizedViewerSlotId == "viewer.section" && quadSlots[2].Visibility == Visibility.Visible
                && quadSlots.Where((_, index) => index != 2).All(slot => slot.Visibility == Visibility.Collapsed)
                && Grid.GetRow(quadSlots[2]) == 0 && Grid.GetColumn(quadSlots[2]) == 0
                && Grid.GetRowSpan(quadSlots[2]) == 3 && Grid.GetColumnSpan(quadSlots[2]) == 3
                && authoredWorkbench.AuxiliaryViewerContentId == quadAuxiliaryPin
                && ReferenceEquals(quadSelectedStep, authoredWorkbench.SelectedPipelineStep)
                && authoredWorkbench.IsDirty == quadDirty && mainViewer.HostState.Inspection.PublishedResultCount == 0,
                $"max={authoredWorkbench.MaximizedViewerSlotId};panes={string.Join(',', quadSlots.Select(slot => slot.Visibility))}");
            sectionMaximize.Command.Execute(sectionMaximize.CommandParameter);
            layoutWorkspaceView.UpdateLayout();
            var restoredQuadBounds = quadSlots.Select(slot => slot.TransformToAncestor(layoutWorkspaceView).TransformBounds(new Rect(slot.RenderSize))).ToArray();
            Check("V5 pane restore returns all four grid positions and removes maximized overlap",
                quadSlots.Select((slot, index) => Grid.GetRow(slot) == (index < 2 ? 0 : 2)
                    && Grid.GetColumn(slot) == (index % 2 == 0 ? 0 : 2)
                    && Grid.GetRowSpan(slot) == 1 && Grid.GetColumnSpan(slot) == 1).All(value => value)
                && restoredQuadBounds.SelectMany((bounds, index) => restoredQuadBounds.Skip(index + 1).Select(other => !bounds.IntersectsWith(other))).All(value => value),
                $"positions={string.Join(';', quadSlots.Select(slot => $"{Grid.GetRow(slot)},{Grid.GetColumn(slot)},{Grid.GetRowSpan(slot)},{Grid.GetColumnSpan(slot)}"))};bounds={string.Join(';', restoredQuadBounds)}");
            Check("D45 bound pane restore preserves the four-pane proportions and existing content",
                string.IsNullOrEmpty(authoredWorkbench.MaximizedViewerSlotId) && quadSlots.All(slot => slot.Visibility == Visibility.Visible)
                && ((RowDefinition)layoutWorkspaceView.FindName("FirstRow")).Height == restoreFirstRow
                && ((RowDefinition)layoutWorkspaceView.FindName("SecondRow")).Height == restoreSecondRow
                && ((ColumnDefinition)layoutWorkspaceView.FindName("FirstColumn")).Width == restoreFirstColumn
                && ((ColumnDefinition)layoutWorkspaceView.FindName("SecondColumn")).Width == restoreSecondColumn
                && ReferenceEquals(((ContentControl)layoutWorkspaceView.FindName("AuxiliaryViewerHost")).Content, auxiliaryContentBeforeMaximize)
                && authoredWorkbench.IsDirty == quadDirty && mainViewer.HostState.Inspection.PublishedResultCount == 0,
                $"max={authoredWorkbench.MaximizedViewerSlotId};panes={string.Join(',', quadSlots.Select(slot => slot.Visibility))}");
            authoredWorkbench.HeightViewerPane.SetKindCommand.Execute(ViewerPaneKind.Height);
            authoredWorkbench.SetSingleViewerLayoutCommand.Execute(null);
            layoutWorkspaceView.UpdateLayout();
            Check("v3 returning from Four hides extra consumers and restores the single viewport without execution",
                authoredWorkbench.IsSingleViewerLayout && fourLayoutButton.IsChecked == false
                && quadSlots[2].Visibility == Visibility.Collapsed && quadSlots[3].Visibility == Visibility.Collapsed
                && authoredWorkbench.AuxiliaryViewerContentId == quadAuxiliaryPin
                && ReferenceEquals(quadSelectedStep, authoredWorkbench.SelectedPipelineStep) && authoredWorkbench.IsDirty == quadDirty
                && !authoredWorkbench.IsOrderedRunRunning && !authoredWorkbench.IsSelectedStepPreviewRunning,
                $"single={authoredWorkbench.IsSingleViewerLayout};extra={quadSlots[2].Visibility}/{quadSlots[3].Visibility};dirty={authoredWorkbench.IsDirty}");

            Check(
                "height palette keeps the R19 3D surface free of a distribution card without running inspection",
                heightPaletteSelected
                && heightDistributionPanel is null
                && mainViewer.HostState.Presentation.C3DHeightDistributionVisible
                && mainViewer.HostState.Inspection.PublishedResultCount == 0,
                $"heightPalette={heightPaletteSelected};selected={mainViewer.DisplayEditor.SelectedColorMap};overlay={heightDistributionPanel is not null};source={mainViewer.CurrentC3DSourcePath};published={mainViewer.HostState.Inspection.PublishedResultCount}");
            mainViewerDisplayOptions.IsSubmenuOpen = false;
            ((MenuItem)mainViewer.FindName("ViewerViewMenuRoot")).IsSubmenuOpen = false;
            mainPresentationBar.Presentation!.CancelDisplaySettingsCommand.Execute(null);
            layoutTestWindow.Dispatcher.Invoke(DispatcherPriority.Render, new Action(() =>
            {
                mainViewerDisplayOptions.UpdateLayout();
                mainPresentationBar.UpdateLayout();
                layoutWorkspaceView.UpdateLayout();
            }));
            var mainPresentationOptionsContent = mainPresentationDisplayOptions;
            var collapsedFrame = new DispatcherFrame();
            var collapseTimeout = System.Diagnostics.Stopwatch.StartNew();
            var collapseTimer = new DispatcherTimer(DispatcherPriority.Background, layoutTestWindow.Dispatcher) { Interval = TimeSpan.FromMilliseconds(16) };
            EventHandler collapsedTick = (_, _) =>
            {
                layoutTestWindow.UpdateLayout();
                var end = mainPresentationBar.TranslatePoint(new Point(mainPresentationBar.ActualWidth, mainPresentationBar.ActualHeight), layoutWorkspaceView);
                if (end.Y <= mainSlot.TranslatePoint(new Point(0, 80), layoutWorkspaceView).Y || collapseTimeout.Elapsed >= TimeSpan.FromSeconds(1))
                    collapsedFrame.Continue = false;
            };
            collapseTimer.Tick += collapsedTick;
            try
            {
                collapseTimer.Start();
                Dispatcher.PushFrame(collapsedFrame);
            }
            finally
            {
                collapseTimer.Stop();
                collapseTimer.Tick -= collapsedTick;
            }
            foreach (var hostWidth in new[] { 856d, 1140d })
            {
                layoutTestWindow.Width = hostWidth;
                layoutTestWindow.Dispatcher.Invoke(DispatcherPriority.Render, new Action(() => layoutTestWindow.UpdateLayout()));
                var controlsEnd = mainPresentationBar.TranslatePoint(new Point(mainPresentationBar.ActualWidth, mainPresentationBar.ActualHeight), layoutWorkspaceView);
                var commandRows = (Panel)layoutWorkspaceView.FindName("ViewerCommandRows");
                var commandRowDetails = string.Join(";", commandRows.Children.OfType<FrameworkElement>().Select(child =>
                    $"{child.Name}={child.ActualWidth:F0}x{child.ActualHeight:F0}@{child.TranslatePoint(new Point(0, 0), commandRows)}"));
                OpenVisionLab.ThreeD.Viewer.Rendering.WpfScreenshotCapture.Save(
                    OpenVisionLab.ThreeD.Viewer.Rendering.WpfScreenshotCapture.Capture(layoutWorkspaceView).Bitmap,
                    Path.ChangeExtension(reportPath, $".loaded-{hostWidth:0}.png"));
                Check($"Loaded Viewer command rows remain reachable at {hostWidth:0}px",
                    controlsEnd.X <= layoutWorkspaceView.ActualWidth + 1
                    && IsVisualDescendant(mainSlot, mainPresentationBar)
                    && controlsEnd.Y <= mainSlot.TranslatePoint(new Point(0, 80), layoutWorkspaceView).Y
                    && commandRows.ActualHeight <= 104
                    && !viewerDisplaySettingsPopup.IsOpen
                    && !viewerWorkspaceOptionsRoot.IsSubmenuOpen
                    && viewerDisplaySettingsButton.Visibility == Visibility.Visible
                    && viewerDisplaySettingsButton.IsVisible
                    && mainViewer.HostState.Inspection.PublishedResultCount == 0,
                    $"end={controlsEnd};rows={commandRows.ActualHeight};width={layoutWorkspaceView.ActualWidth};slotTop={mainSlot.TranslatePoint(new Point(), layoutWorkspaceView).Y};header={mainSlotHeader.ActualHeight};bar={mainPresentationBar.ActualHeight};rangeButton={viewerDisplaySettingsButton.Visibility}/{viewerDisplaySettingsButton.IsVisible};options={viewerDisplaySettingsPopup.IsOpen};content={mainPresentationOptionsContent.Visibility}/{mainPresentationOptionsContent.IsVisible}/{mainPresentationOptionsContent.ActualHeight:F0};children={commandRowDetails}");
            }
            foreach (var chromeWidth in new[] { 1280d, 1920d })
            {
                layoutTestWindow.ResizeMode = ResizeMode.NoResize;
                layoutTestWindow.Width = chromeWidth;
                layoutTestWindow.Height = chromeWidth < 1500 ? 760 : 1040;
                layoutTestWindow.Dispatcher.Invoke(DispatcherPriority.Render, new Action(layoutTestWindow.UpdateLayout));
                var chromeFrames = 0;
                var chromeRendered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                EventHandler chromeRendering = (_, _) =>
                {
                    if (++chromeFrames >= 3) chromeRendered.TrySetResult();
                };
                CompositionTarget.Rendering += chromeRendering;
                try
                {
                    mainViewer.RequestVisibleFrame();
                    WaitForPresentationTask(chromeRendered.Task.WaitAsync(TimeSpan.FromSeconds(5)));
                }
                finally
                {
                    CompositionTarget.Rendering -= chromeRendering;
                }
                var header = (Border)layoutWorkspaceView.FindName("MainViewerSlotHeader");
                var menuBounds = nativeMenu.TransformToAncestor(layoutWorkspaceView).TransformBounds(new Rect(nativeMenu.RenderSize));
                var cubeBounds = cubePanel.TransformToAncestor(layoutWorkspaceView).TransformBounds(new Rect(cubePanel.RenderSize));
                var expectedCubeSize = chromeWidth < 1500 ? 80 : 92;
                Check($"v3 Viewer header and {expectedCubeSize}px cube remain separate at {chromeWidth:0}px",
                    nativeMenu.IsVisible && header.ActualHeight == 32 && nativeMenu.ActualHeight == 28
                    && layoutWorkspaceView.ActualWidth + layoutWorkspaceView.Margin.Left + layoutWorkspaceView.Margin.Right == chromeWidth
                    && layoutWorkspaceView.ActualHeight + layoutWorkspaceView.Margin.Top + layoutWorkspaceView.Margin.Bottom == layoutTestWindow.Height
                    && cubePanel.ActualWidth == expectedCubeSize && cubeViewport.ActualHeight == expectedCubeSize
                    && !menuBounds.IntersectsWith(cubeBounds) && menuBounds.Right <= layoutWorkspaceView.ActualWidth
                    && ReferenceEquals(nativeFitRoi.Command, cubeViewModel.FitRoiCommand)
                    && mainViewer.HostState.Inspection.PublishedResultCount == 0,
                    $"size={layoutWorkspaceView.ActualWidth}x{layoutWorkspaceView.ActualHeight};menu={menuBounds};cube={cubeBounds};viewport={cubeViewport.ActualHeight};header={header.ActualHeight};frames={chromeFrames}");
                OpenVisionLab.ThreeD.Viewer.Rendering.WpfScreenshotCapture.Save(
                    OpenVisionLab.ThreeD.Viewer.Rendering.WpfScreenshotCapture.Capture(layoutTestWindow).Bitmap,
                    Path.ChangeExtension(reportPath, $".d16-chrome-{chromeWidth:0}.png"));
            }
            var nativeMenuRoot = (MenuItem)mainViewer.FindName("ViewerViewMenuRoot");
            nativeMenuRoot.IsSubmenuOpen = true;
            layoutTestWindow.Dispatcher.Invoke(DispatcherPriority.Render, new Action(layoutTestWindow.UpdateLayout));
            var nativeMenuPopup = (Popup)nativeMenuRoot.Template.FindName("PART_Popup", nativeMenuRoot);
            Check("reparented native Viewer menu renders its popup with the original command owner",
                nativeMenuPopup.IsOpen && nativeMenuPopup.Child is FrameworkElement { IsVisible: true, ActualWidth: > 100, ActualHeight: > 100 }
                && ReferenceEquals(nativeFitRoi.Command, cubeViewModel.FitRoiCommand)
                && !authoredWorkbench.IsOrderedRunRunning && mainViewer.HostState.Inspection.PublishedResultCount == 0,
                $"open={nativeMenuPopup.IsOpen};size={((FrameworkElement)nativeMenuPopup.Child).ActualWidth}x{((FrameworkElement)nativeMenuPopup.Child).ActualHeight};fitCommand={nativeFitRoi.Command?.GetType().Name}");
            var legacyNativePalette = (ComboBox)mainViewer.FindName("ViewerLegacyColorMapSelector");
            var previousLegacyNativePalette = legacyNativePalette.SelectedItem;
            mainViewerDisplayOptions.IsSubmenuOpen = true;
            layoutTestWindow.Dispatcher.Invoke(DispatcherPriority.Render, new Action(layoutTestWindow.UpdateLayout));
            legacyNativePalette.SelectedItem = "Thermal";
            var legacyDisplayContent = (Panel)mainViewer.FindName("ViewerDisplayOptionsContent");
            var legacyOverlayControls = new[] { "ViewerLegacySelectionOverlay", "ViewerLegacyResultOverlay", "ViewerLegacyMeasurementOverlay" }.Select(id =>
                OpenVisionLab.ThreeD.Shell.Verification.Smoke.ShellSmokeArtifacts.FindVisualDescendants<CheckBox>(legacyDisplayContent)
                    .Single(control => AutomationProperties.GetAutomationId(control) == id)).ToArray();
            var previousLegacyOverlays = legacyOverlayControls.Select(control => control.IsChecked).ToArray();
            foreach (var control in legacyOverlayControls) control.IsChecked = control.IsChecked != true;
            Check("D45 native legacy palette and overlay bindings remain available outside the HTML popup",
                legacyNativePalette.IsVisible && legacyNativePalette.IsEnabled && legacyNativePalette.Items.Contains("Thermal")
                && mainViewer.HostState.Presentation.SelectedColorMap == "Thermal"
                && legacyOverlayControls.All(control => control.IsVisible && control.IsEnabled)
                && cubeViewModel.SelectionOverlayVisible == (legacyOverlayControls[0].IsChecked == true)
                && cubeViewModel.ResultOverlayVisible == (legacyOverlayControls[1].IsChecked == true)
                && cubeViewModel.MeasurementVisible == (legacyOverlayControls[2].IsChecked == true)
                && !authoredWorkbench.IsDirty && mainViewer.HostState.Inspection.PublishedResultCount == 0,
                $"visible={legacyNativePalette.IsVisible};palette={mainViewer.HostState.Presentation.SelectedColorMap};overlays={string.Join(',', legacyOverlayControls.Select(control => $"{control.IsVisible}/{control.IsChecked}"))};dirty={authoredWorkbench.IsDirty}");
            for (var index = 0; index < legacyOverlayControls.Length; index++) legacyOverlayControls[index].IsChecked = previousLegacyOverlays[index];
            OpenVisionLab.ThreeD.Viewer.Rendering.WpfScreenshotCapture.Save(
                OpenVisionLab.ThreeD.Viewer.Rendering.WpfScreenshotCapture.Capture(legacyDisplayContent).Bitmap,
                Path.ChangeExtension(reportPath, ".d45-native-legacy-display-menu.png"));
            legacyNativePalette.SelectedItem = previousLegacyNativePalette;
            mainViewerDisplayOptions.IsSubmenuOpen = false;
            OpenVisionLab.ThreeD.Viewer.Rendering.WpfScreenshotCapture.Save(
                OpenVisionLab.ThreeD.Viewer.Rendering.WpfScreenshotCapture.Capture((FrameworkElement)nativeMenuPopup.Child).Bitmap,
                Path.ChangeExtension(reportPath, ".d16-menu-popup.png"));
            nativeMenuRoot.IsSubmenuOpen = false;
            layoutTestWindow.ResizeMode = ResizeMode.CanResize;
            layoutTestWindow.Width = 1140;
            layoutTestWindow.Height = 630;
            layoutTestWindow.Dispatcher.Invoke(DispatcherPriority.Render, new Action(layoutTestWindow.UpdateLayout));
            var rangeEditor = mainPresentationBar.Presentation!;
            var rangeBeforeEdit = mainViewer.HostState.Presentation;
            mainPresentationBar.UpdateLayout();
            layoutTestWindow.Dispatcher.Invoke(DispatcherPriority.Render, new Action(() => layoutTestWindow.UpdateLayout()));
            Check(
                "loaded height range controls are directly reachable from the viewport toolbar",
                !viewerDisplaySettingsPopup.IsOpen
                && viewerDisplaySettingsButton.IsVisible
                && viewerDisplaySettingsButton.Command?.CanExecute(null) == true,
                $"options={viewerDisplaySettingsPopup.IsOpen};rangeButton={viewerDisplaySettingsButton.Visibility};canExecute={viewerDisplaySettingsButton.Command?.CanExecute(null)}");
            viewerDisplaySettingsButton.Command!.Execute(null);
            mainPresentationBar.UpdateLayout();
            layoutTestWindow.Dispatcher.Invoke(DispatcherPriority.Render, new Action(() => layoutTestWindow.UpdateLayout()));
            var popupChild = viewerDisplaySettingsPopup.Child;
            Check(
                "settings popup opens from the live workspace host",
                viewerDisplaySettingsPopup.IsOpen && popupChild is not null && ReferenceEquals(viewerDisplaySettingsPopup.PlacementTarget, viewerDisplaySettingsButton),
                $"vmOpen={rangeEditor.IsDisplaySettingsOpen};popupOpen={viewerDisplaySettingsPopup.IsOpen};child={popupChild?.GetType().Name ?? "(none)"};target={viewerDisplaySettingsPopup.PlacementTarget?.GetType().Name ?? "(none)"}");
            var viewerDisplaySettingsDialog = OpenVisionLab.ThreeD.Shell.Verification.Smoke.ShellSmokeArtifacts.FindVisualDescendants<Border>(popupChild!)
                .Single(border => AutomationProperties.GetAutomationId(border) == "ViewerDisplaySettingsDialog");
            var rangeAutoSelector = (RadioButton)mainPresentationBar.FindName("ViewerHeightRangeAuto");
            var rangeManualSelector = (RadioButton)mainPresentationBar.FindName("ViewerHeightRangeManual");
            var minimumInput = OpenVisionLab.ThreeD.Shell.Verification.Smoke.ShellSmokeArtifacts.FindVisualDescendants<TextBox>(popupChild!)
                .Single(textBox => AutomationProperties.GetAutomationId(textBox) == "ViewerHeightRangeMinimumInput");
            var maximumInput = OpenVisionLab.ThreeD.Shell.Verification.Smoke.ShellSmokeArtifacts.FindVisualDescendants<TextBox>(popupChild!)
                .Single(textBox => AutomationProperties.GetAutomationId(textBox) == "ViewerHeightRangeMaximumInput");
            var emphasisSelectors = OpenVisionLab.ThreeD.Shell.Verification.Smoke.ShellSmokeArtifacts.FindVisualDescendants<RadioButton>(popupChild!)
                .Where(button => AutomationProperties.GetAutomationId(button).StartsWith("ViewerHeightEmphasis", StringComparison.Ordinal)).ToArray();
            var emphasisSelector3 = emphasisSelectors.Single(button => AutomationProperties.GetAutomationId(button) == "ViewerHeightEmphasis3");
            var displaySettingsOverlay = (Grid)popupChild!;
            var paletteItems = mainPresentationPalette.Items.Cast<object>().Select(item =>
                mainPresentationPalette.ItemContainerGenerator.ContainerFromItem(item) as ListBoxItem).ToArray();
            Check(
                "D42 every palette choice wraps inside the popup without horizontal clipping",
                paletteItems.All(item => item is { IsVisible: true, ActualWidth: > 0 }
                    && item.TranslatePoint(new Point(), mainPresentationPalette).X >= 0
                    && item.TranslatePoint(new Point(item.ActualWidth, item.ActualHeight), mainPresentationPalette).X <= mainPresentationPalette.ActualWidth + 1)
                && ScrollViewer.GetHorizontalScrollBarVisibility(mainPresentationPalette) == ScrollBarVisibility.Disabled,
                $"choices={paletteItems.Length};width={mainPresentationPalette.ActualWidth};sizes={string.Join(",", paletteItems.Select(item => $"{item?.ActualWidth:F0}x{item?.ActualHeight:F0}"))}");
            Check(
                "D42 display panel omits the duplicate internal range summary and retains the actual range editor",
                !OpenVisionLab.ThreeD.Shell.Verification.Smoke.ShellSmokeArtifacts.FindVisualDescendants<TextBlock>(displaySettingsOverlay).Any(text =>
                    BindingOperations.GetBindingExpression(text, TextBlock.TextProperty)?.ParentBinding.Path?.Path == "C3DHeightColorRangeSummary"
                    || text.Text.StartsWith("Height color range", StringComparison.Ordinal))
                && rangeAutoSelector.IsVisible && rangeManualSelector.IsVisible,
                $"auto={rangeAutoSelector.IsVisible};manual={rangeManualSelector.IsVisible};unit={mainViewer.HostState.Presentation.C3DHeightColorRangeSummary}");
            OpenVisionLab.ThreeD.Viewer.Rendering.WpfScreenshotCapture.Save(
                OpenVisionLab.ThreeD.Viewer.Rendering.WpfScreenshotCapture.Capture((FrameworkElement)popupChild!).Bitmap,
                Path.ChangeExtension(reportPath, ".d42-display-popup.png"));
            Check(
                "D42 settings opens in one 300px toolbar popup and keeps automatic values read-only",
                rangeEditor.IsDisplaySettingsOpen
                && BindingOperations.GetBindingExpression(viewerDisplaySettingsPopup, Popup.IsOpenProperty)?.ParentBinding.Path.Path == "IsDisplaySettingsOpen"
                && BindingOperations.GetBindingExpression(viewerDisplaySettingsPopup, Popup.IsOpenProperty)?.ParentBinding.Mode == BindingMode.TwoWay
                && viewerDisplaySettingsPopup.Placement == PlacementMode.Bottom
                && ReferenceEquals(viewerDisplaySettingsPopup.PlacementTarget, viewerDisplaySettingsButton)
                && Math.Abs(displaySettingsOverlay.ActualWidth - 300) < 1
                && displaySettingsOverlay.ActualHeight <= 600
                && Math.Abs(viewerDisplaySettingsDialog.ActualWidth - 300) < 1
                && rangeAutoSelector.IsChecked == true && rangeManualSelector.IsChecked == false
                && emphasisSelectors.Single(button => AutomationProperties.GetAutomationId(button) == "ViewerHeightEmphasis1").IsChecked == true
                && !minimumInput.IsEnabled
                && !maximumInput.IsEnabled
                && minimumInput.Text == rangeBeforeEdit.C3DHeightColorMinimumRaw.ToString("G", CultureInfo.InvariantCulture)
                && maximumInput.Text == rangeBeforeEdit.C3DHeightColorMaximumRaw.ToString("G", CultureInfo.InvariantCulture)
                && mainViewer.HostState.Inspection.PublishedResultCount == 0,
                $"emphasis={rangeEditor.DraftHeightEmphasis};min={minimumInput.Text};max={maximumInput.Text};placement={viewerDisplaySettingsPopup.Placement};target={viewerDisplaySettingsPopup.PlacementTarget?.GetType().Name};overlay={displaySettingsOverlay.ActualWidth}x{displaySettingsOverlay.ActualHeight};dialog={viewerDisplaySettingsDialog.ActualWidth};mode={rangeEditor.DraftHeightRangeMode};published={mainViewer.HostState.Inspection.PublishedResultCount}");
            emphasisSelector3.IsChecked = true;
            Check(
                "D45 HTML height emphasis is immediate without applying a range or publishing a result",
                mainViewer.HostState.Presentation.C3DHeightEmphasis == 3
                && mainViewer.HostState.Presentation.C3DHeightColorRangeAuto == rangeBeforeEdit.C3DHeightColorRangeAuto
                && mainViewer.HostState.Presentation.C3DHeightColorMinimumRaw == rangeBeforeEdit.C3DHeightColorMinimumRaw
                && mainViewer.HostState.Presentation.C3DHeightColorMaximumRaw == rangeBeforeEdit.C3DHeightColorMaximumRaw
                && mainViewer.HostState.Inspection.PublishedResultCount == 0,
                $"emphasis={mainViewer.HostState.Presentation.C3DHeightEmphasis};published={mainViewer.HostState.Inspection.PublishedResultCount}");
            rangeManualSelector.IsChecked = true;
            minimumInput.Text = (rangeBeforeEdit.C3DHeightColorMinimumRaw + 1).ToString("G", CultureInfo.InvariantCulture);
            maximumInput.Text = (rangeBeforeEdit.C3DHeightColorMaximumRaw - 1).ToString("G", CultureInfo.InvariantCulture);
            rangeEditor.CancelDisplaySettingsCommand.Execute(null);
            mainPresentationBar.UpdateLayout();
            Check(
                "D45 cancel discards draft range and retains the immediate HTML height emphasis",
                rangeEditor.IsDisplaySettingsOpen
                && mainViewer.HostState.Presentation.C3DHeightColorRangeAuto == rangeBeforeEdit.C3DHeightColorRangeAuto
                && mainViewer.HostState.Presentation.C3DHeightColorMinimumRaw == rangeBeforeEdit.C3DHeightColorMinimumRaw
                && mainViewer.HostState.Presentation.C3DHeightColorMaximumRaw == rangeBeforeEdit.C3DHeightColorMaximumRaw
                && mainViewer.HostState.Presentation.C3DHeightEmphasis == 3
                && mainViewer.HostState.Inspection.PublishedResultCount == 0,
                $"vmOpen={rangeEditor.IsDisplaySettingsOpen};popupOpen={viewerDisplaySettingsPopup.IsOpen};auto={mainViewer.HostState.Presentation.C3DHeightColorRangeAuto};published={mainViewer.HostState.Inspection.PublishedResultCount}");

            viewerDisplaySettingsButton.Command.Execute(null);
            mainPresentationBar.UpdateLayout();
            rangeEditor.DraftHeightEmphasis = "4";
            rangeEditor.ApplyDisplaySettingsCommand.Execute(null);
            Check(
                "unsupported height emphasis is rejected without changing the current view",
                rangeEditor.IsDisplaySettingsOpen
                && rangeEditor.HasDisplaySettingsEmphasisError
                && mainViewer.HostState.Presentation.C3DHeightEmphasis == 3
                && mainViewer.HostState.Inspection.PublishedResultCount == 0,
                $"vmOpen={rangeEditor.IsDisplaySettingsOpen};emphasisError={rangeEditor.HasDisplaySettingsEmphasisError};emphasis={mainViewer.HostState.Presentation.C3DHeightEmphasis};published={mainViewer.HostState.Inspection.PublishedResultCount}");
            rangeEditor.CancelDisplaySettingsCommand.Execute(null);

            viewerDisplaySettingsButton.Command.Execute(null);
            mainPresentationBar.UpdateLayout();
            rangeManualSelector.IsChecked = true;
            minimumInput.Text = (rangeBeforeEdit.C3DHeightColorMaximumRaw + 1).ToString("G", CultureInfo.InvariantCulture);
            maximumInput.Text = rangeBeforeEdit.C3DHeightColorMinimumRaw.ToString("G", CultureInfo.InvariantCulture);
            rangeEditor.ApplyDisplaySettingsCommand.Execute(null);
            mainPresentationBar.UpdateLayout();
            Check(
                "invalid manual range reports validation and keeps the popup and Viewer unchanged",
                rangeEditor.IsDisplaySettingsOpen
                && rangeEditor.HasDisplaySettingsRangeError
                && mainViewer.HostState.Presentation.C3DHeightColorRangeAuto == rangeBeforeEdit.C3DHeightColorRangeAuto
                && mainViewer.HostState.Presentation.C3DHeightColorMinimumRaw == rangeBeforeEdit.C3DHeightColorMinimumRaw
                && mainViewer.HostState.Presentation.C3DHeightColorMaximumRaw == rangeBeforeEdit.C3DHeightColorMaximumRaw
                && mainViewer.HostState.Inspection.PublishedResultCount == 0,
                $"vmOpen={rangeEditor.IsDisplaySettingsOpen};popupOpen={viewerDisplaySettingsPopup.IsOpen};validation={rangeEditor.HasDisplaySettingsRangeError};published={mainViewer.HostState.Inspection.PublishedResultCount}");
            rangeEditor.CancelDisplaySettingsCommand.Execute(null);

            viewerDisplaySettingsButton.Command.Execute(null);
            mainPresentationBar.UpdateLayout();
            rangeManualSelector.IsChecked = true;
            var expectedMinimum = rangeBeforeEdit.C3DHeightColorMinimumRaw + (rangeBeforeEdit.C3DHeightColorMaximumRaw - rangeBeforeEdit.C3DHeightColorMinimumRaw) * 0.2;
            var expectedMaximum = rangeBeforeEdit.C3DHeightColorMaximumRaw - (rangeBeforeEdit.C3DHeightColorMaximumRaw - rangeBeforeEdit.C3DHeightColorMinimumRaw) * 0.2;
            minimumInput.Text = expectedMinimum.ToString("G", CultureInfo.InvariantCulture);
            maximumInput.Text = expectedMaximum.ToString("G", CultureInfo.InvariantCulture);
            emphasisSelector3.IsChecked = true;
            rangeEditor.ApplyDisplaySettingsCommand.Execute(null);
            mainPresentationBar.UpdateLayout();
            Check(
                "valid manual range applies through the existing Viewer host without running inspection",
                rangeEditor.IsDisplaySettingsOpen
                && !mainViewer.HostState.Presentation.C3DHeightColorRangeAuto
                && Math.Abs(mainViewer.HostState.Presentation.C3DHeightColorMinimumRaw - expectedMinimum) < 1e-9
                && Math.Abs(mainViewer.HostState.Presentation.C3DHeightColorMaximumRaw - expectedMaximum) < 1e-9
                && mainViewer.HostState.Presentation.C3DHeightEmphasis == 3
                && mainViewer.HostState.Inspection.PublishedResultCount == 0,
                $"vmOpen={rangeEditor.IsDisplaySettingsOpen};popupOpen={viewerDisplaySettingsPopup.IsOpen};range={mainViewer.HostState.Presentation.C3DHeightColorMinimumRaw:G}..{mainViewer.HostState.Presentation.C3DHeightColorMaximumRaw:G};published={mainViewer.HostState.Inspection.PublishedResultCount}");

            viewerDisplaySettingsButton.Command.Execute(null);
            mainPresentationBar.UpdateLayout();
            rangeAutoSelector.IsChecked = true;
            Check("D45 HTML automatic range restores immediately and keeps the popup open",
                mainViewer.HostState.Presentation.C3DHeightColorRangeAuto && rangeEditor.IsDisplaySettingsOpen
                && mainViewer.HostState.Inspection.PublishedResultCount == 0,
                $"auto={mainViewer.HostState.Presentation.C3DHeightColorRangeAuto};open={rangeEditor.IsDisplaySettingsOpen}");
            rangeEditor.ApplyDisplaySettingsCommand.Execute(null);
            mainPresentationBar.UpdateLayout();
            Check(
                "automatic mode restores source range without inspection",
                rangeEditor.IsDisplaySettingsOpen
                && mainViewer.HostState.Presentation.C3DHeightColorRangeAuto
                && mainViewer.HostState.Inspection.PublishedResultCount == 0,
                $"vmOpen={rangeEditor.IsDisplaySettingsOpen};popupOpen={viewerDisplaySettingsPopup.IsOpen};auto={mainViewer.HostState.Presentation.C3DHeightColorRangeAuto};published={mainViewer.HostState.Inspection.PublishedResultCount}");

            viewerDisplaySettingsButton.Command.Execute(null);
            mainPresentationBar.UpdateLayout();
            rangeAutoSelector.IsChecked = true;
            var externalMinimum = expectedMinimum + (expectedMaximum - expectedMinimum) * 0.1;
            var externalMaximum = expectedMaximum - (expectedMaximum - expectedMinimum) * 0.1;
            mainViewer.TryApplyLinkedC3DHeightColorRange(externalMinimum, externalMaximum);
            rangeEditor.ApplyDisplaySettingsCommand.Execute(null);
            mainPresentationBar.UpdateLayout();
            Check(
                "changed range revision is rejected as stale and leaves the external range intact",
                rangeEditor.IsDisplaySettingsOpen
                && rangeEditor.HasDisplaySettingsStaleError
                && !mainViewer.HostState.Presentation.C3DHeightColorRangeAuto
                && Math.Abs(mainViewer.HostState.Presentation.C3DHeightColorMinimumRaw - externalMinimum) < 1e-9
                && Math.Abs(mainViewer.HostState.Presentation.C3DHeightColorMaximumRaw - externalMaximum) < 1e-9
                && mainViewer.HostState.Inspection.PublishedResultCount == 0,
                $"vmOpen={rangeEditor.IsDisplaySettingsOpen};popupOpen={viewerDisplaySettingsPopup.IsOpen};stale={rangeEditor.HasDisplaySettingsStaleError};auto={mainViewer.HostState.Presentation.C3DHeightColorRangeAuto};published={mainViewer.HostState.Inspection.PublishedResultCount}");
            rangeEditor.CancelDisplaySettingsCommand.Execute(null);
            rangeEditor.CloseDisplaySettingsCommand.Execute(null);
            mainViewer.TryResetC3DHeightColorRange();
            mainPresentationBar.UpdateLayout();
            authoredWorkbench.SelectedPipelineStep = authoredWorkbench.PipelineSteps[0];
            var pinnedMainBeforeDataView = authoredWorkbench.MainViewerContentId;
            var outputButton = (MenuItem)layoutWorkspaceView.FindName("ViewerInspectionOutput");
            var bothButton = (MenuItem)layoutWorkspaceView.FindName("ViewerInspectionInputOutput");
            var compareButton = (MenuItem)layoutWorkspaceView.FindName("ViewerInspectionOutputCompare");
            Check(
                "A/B Viewer action binds to the explicit compare command and active state",
                BindingOperations.GetBindingExpression(compareButton, MenuItem.CommandProperty)?.ParentBinding.Path.Path == "ShowOutputCompareViewerCommand"
                && BindingOperations.GetBindingExpression(compareButton, MenuItem.IsCheckedProperty)?.ParentBinding.Path.Path == "IsOutputCompareViewerActive"
                && BindingOperations.GetBindingExpression(compareButton, UIElement.IsEnabledProperty)?.ParentBinding.Path.Path == "CanShowOutputCompareViewer"
                && compareButton.IsEnabled == authoredWorkbench.CanShowOutputCompareViewer,
                $"command={BindingOperations.GetBindingExpression(compareButton, MenuItem.CommandProperty)?.ParentBinding.Path.Path};active={BindingOperations.GetBindingExpression(compareButton, MenuItem.IsCheckedProperty)?.ParentBinding.Path.Path};enabled={compareButton.IsEnabled}");
            var selectedStepBeforeEmptyCompare = authoredWorkbench.WorkspaceSelection.SelectedStepId;
            var selectedInputBeforeEmptyCompare = authoredWorkbench.WorkspaceSelection.SelectedInputEntityId;
            var sourcePathBeforeEmptyCompare = authoredWorkbench.Source.Path;
            var compareCandidatesBeforeEmptyCompare = authoredWorkbench.CompareCandidates.Select(item => item.Id).ToArray();
            var viewerLayoutBeforeEmptyCompare = authoredWorkbench.EffectiveViewerLayout;
            var recipeBytesBeforeEmptyCompare = File.ReadAllBytes(recipePath);
            Check(
                "selected saved step enables empty A/B compare without output candidates",
                compareButton.IsEnabled
                && authoredWorkbench.CanShowOutputCompareViewer
                && authoredWorkbench.CompareSlotAArtifactId.Length == 0
                && authoredWorkbench.CompareSlotBArtifactId.Length == 0
                && !authoredWorkbench.CompareCandidates.Any(item => item.Id.Length > 0 && !item.IsSource),
                $"enabled={compareButton.IsEnabled};A={authoredWorkbench.CompareSlotAArtifactId};B={authoredWorkbench.CompareSlotBArtifactId};outputs={authoredWorkbench.CompareCandidates.Count(item => item.Id.Length > 0 && !item.IsSource)}");
            compareButton.Command.Execute(null);
            layoutWorkspaceView.UpdateLayout();
            var emptyCompareMain = (Border)layoutWorkspaceView.FindName("ViewerCompareMainNoGeometry");
            Check(
                "empty A slot renders localized guidance without changing recipe, input, selection, or results",
                authoredWorkbench.IsOutputCompareViewerActive
                && compareButton.IsChecked == true
                && emptyCompareMain.Visibility == Visibility.Visible
                && HasVisualText(layoutWorkspaceView, authoredWorkbench.Localization.OutputCompareNoSelection)
                && HasVisualText(layoutWorkspaceView, authoredWorkbench.Localization.OutputCompareDetail)
                && authoredWorkbench.CompareSlotAArtifactId.Length == 0
                && authoredWorkbench.CompareSlotBArtifactId.Length == 0
                && authoredWorkbench.WorkspaceSelection.SelectedStepId == selectedStepBeforeEmptyCompare
                && authoredWorkbench.WorkspaceSelection.SelectedInputEntityId == selectedInputBeforeEmptyCompare
                && authoredWorkbench.Source.Path == sourcePathBeforeEmptyCompare
                && authoredWorkbench.MainViewerContentId == pinnedMainBeforeDataView
                && mainViewer.CurrentC3DSourcePath is null
                && authoredWorkbench.EffectiveMainViewerContentId.Length == 0
                && authoredWorkbench.EffectiveAuxiliaryViewerContentId.Length == 0
                && mainViewer.HostState.Inspection.PublishedResultCount == 0
                && !authoredWorkbench.IsSelectedStepPreviewRunning
                && !authoredWorkbench.IsDirty
                && compareCandidatesBeforeEmptyCompare.SequenceEqual(authoredWorkbench.CompareCandidates.Select(item => item.Id))
                && recipeBytesBeforeEmptyCompare.SequenceEqual(File.ReadAllBytes(recipePath)),
                $"compare={authoredWorkbench.IsOutputCompareViewerActive};empty={emptyCompareMain.Visibility};step={authoredWorkbench.WorkspaceSelection.SelectedStepId};input={authoredWorkbench.WorkspaceSelection.SelectedInputEntityId};source={mainViewer.CurrentC3DSourcePath};published={mainViewer.HostState.Inspection.PublishedResultCount};dirty={authoredWorkbench.IsDirty}");
            compareButton.Command.Execute(null);
            layoutWorkspaceView.UpdateLayout();
            Check(
                "closing empty A/B compare restores the prior Viewer layout and loaded input",
                !authoredWorkbench.IsOutputCompareViewerActive
                && compareButton.IsChecked == false
                && authoredWorkbench.EffectiveViewerLayout == viewerLayoutBeforeEmptyCompare
                && mainViewer.CurrentC3DSourcePath == authoredWorkbench.Source.Path
                && mainViewer.HostState.Inspection.PublishedResultCount == 0
                && !authoredWorkbench.IsDirty,
                $"compare={authoredWorkbench.IsOutputCompareViewerActive};layout={authoredWorkbench.EffectiveViewerLayout};source={mainViewer.CurrentC3DSourcePath};published={mainViewer.HostState.Inspection.PublishedResultCount};dirty={authoredWorkbench.IsDirty}");
            authoredWorkbench.SetInspectionDataViewCommand.Execute(ViewerInspectionDataView.Output);
            layoutWorkspaceView.UpdateLayout();
            Check(
                "scalar Output shows its own empty geometry state without reusing the Input shape",
                authoredWorkbench.IsInspectionOutputView
                && authoredWorkbench.EffectiveMainViewerContentId.Length == 0
                && mainViewer.CurrentC3DSourcePath is null
                && ((Border)layoutWorkspaceView.FindName("ViewerOutputNoGeometry")).Visibility == Visibility.Visible
                && ((Border)VisualTreeHelper.GetParent(mainPresentationBar)).Visibility == Visibility.Collapsed
                && outputButton.IsChecked == true
                && authoredWorkbench.MainViewerContentId == pinnedMainBeforeDataView
                && mainViewer.HostState.Inspection.PublishedResultCount == 0,
                $"view={authoredWorkbench.EffectiveMainViewerContentId};source={mainViewer.CurrentC3DSourcePath};pin={authoredWorkbench.MainViewerContentId};published={mainViewer.HostState.Inspection.PublishedResultCount}");
            authoredWorkbench.SetInspectionDataViewCommand.Execute(ViewerInspectionDataView.InputOutput);
            layoutWorkspaceView.UpdateLayout();
            Check(
                "Input / Output restores the actual input beside an evidence-only output",
                authoredWorkbench.IsInspectionInputOutputView
                && authoredWorkbench.EffectiveViewerLayout == ViewerWorkspaceLayout.SplitVertical
                && mainViewer.CurrentC3DSourcePath == authoredWorkbench.Source.Path
                && ((Border)layoutWorkspaceView.FindName("ViewerComparedOutputNoGeometry")).Visibility == Visibility.Visible
                && bothButton.IsChecked == true
                && authoredWorkbench.MainViewerContentId == pinnedMainBeforeDataView,
                $"layout={authoredWorkbench.EffectiveViewerLayout};source={mainViewer.CurrentC3DSourcePath};pin={authoredWorkbench.MainViewerContentId}");
            authoredWorkbench.SelectedPipelineStep = authoredWorkbench.PipelineSteps[1];
            layoutWorkspaceView.UpdateLayout();
            Check(
                "changing the selected tool returns to Input without executing or changing the pinned source",
                authoredWorkbench.IsInspectionInputView
                && mainViewer.CurrentC3DSourcePath == authoredWorkbench.Source.Path
                && authoredWorkbench.MainViewerContentId == pinnedMainBeforeDataView
                && mainViewer.HostState.Inspection.PublishedResultCount == 0,
                $"input={authoredWorkbench.WorkspaceSelection.SelectedInputEntityId};source={mainViewer.CurrentC3DSourcePath};pin={authoredWorkbench.MainViewerContentId}");
            authoredWorkbench.SelectedPipelineStep = null;
            authoredWorkbench.ClearMainViewerPinCommand.Execute(null);
            Check(
                "clearing the main content pin releases the previous shape without running inspection",
                mainViewer.CurrentC3DSourcePath is null
                && !authoredWorkbench.ViewerWorkspace.IsMainContentPinned
                && mainViewer.HostState.Inspection.PublishedResultCount == 0,
                $"viewer={mainViewer.CurrentC3DSourcePath};pin={authoredWorkbench.MainViewerContentId};published={mainViewer.HostState.Inspection.PublishedResultCount}");

            authoredWorkbench.SelectedPipelineStep = authoredWorkbench.PipelineSteps[0];
            authoredWorkbench.OpenHeightImageCommand.Execute(null);
            authoredWorkbench.InspectionQueue.AddSources([authoredWorkbench.Source.Path]);
            authoredWorkbench.InspectionQueue.SelectedSample = authoredWorkbench.InspectionQueue.Samples.Single();
            authoredWorkbench.SetInspectionFileView(true);
            layoutWorkspaceView.UpdateLayout();
            var auxiliaryKindLabel = (TextBlock)layoutWorkspaceView.FindName("AuxiliaryViewerKindLabel");
            var auxiliarySourceSummary = (TextBlock)layoutWorkspaceView.FindName("AuxiliaryViewerSourceSummary");
            Check(
                "inspection labels the effective height image independently of its hidden pin selector",
                authoredWorkbench.EffectiveAuxiliaryViewerContentId == ToolWorkbenchViewModel.HeightImageViewerContentId
                && auxiliaryKindLabel.Text == authoredWorkbench.Localization.HeightImage
                && auxiliaryViewerPin.Visibility == Visibility.Collapsed
                && auxiliarySourceSummary.Visibility == Visibility.Visible
                && auxiliarySourceSummary.Text == Path.GetFileName(authoredWorkbench.InspectionQueue.SelectedSample!.SourcePath)
                && authoredWorkbench.AuxiliaryViewerPane.IsHeight
                && authoredWorkbench.InspectionQueue.Samples.Single().State == InspectionQueueItemState.Untested,
                $"effective={authoredWorkbench.EffectiveAuxiliaryViewerContentId};kind={auxiliaryKindLabel.Text};pin={auxiliaryViewerPin.Visibility};summary={auxiliarySourceSummary.Text};state={authoredWorkbench.InspectionQueue.Samples.Single().State}");
            authoredWorkbench.SetInspectionFileView(false);
            layoutWorkspaceView.UpdateLayout();
            Check(
                "returning from inspection restores the existing height-image pin selection without executing or editing",
                auxiliaryViewerPin.SelectedItem is ViewerWorkspaceCandidateItem candidate
                && candidate.Id == ToolWorkbenchViewModel.HeightImageViewerContentId
                && auxiliaryKindLabel.Text == authoredWorkbench.Localization.HeightImage
                && mainViewer.HostState.Inspection.PublishedResultCount == 0
                && !authoredWorkbench.IsOrderedRunRunning
                && !authoredWorkbench.IsSelectedStepPreviewRunning
                && !authoredWorkbench.IsDirty,
                $"selected={auxiliaryViewerPin.SelectedValue};kind={auxiliaryKindLabel.Text};published={mainViewer.HostState.Inspection.PublishedResultCount};dirty={authoredWorkbench.IsDirty}");

            var sampleInputPath = Path.Combine(Path.GetDirectoryName(reportPath)!, "validation-xy-owned.C3D");
            var malformedInputPath = Path.Combine(Path.GetDirectoryName(reportPath)!, "validation-xy-truncated-owned.C3D");
            File.Copy(authoredWorkbench.Source.Path, sampleInputPath, overwrite: true);
            using (var sourceStream = File.OpenRead(sampleInputPath))
            {
                var prefix = new byte[128];
                sourceStream.ReadExactly(prefix);
                File.WriteAllBytes(malformedInputPath, prefix);
            }
            authoredWorkbench.SetValidationSetSources([sampleInputPath, malformedInputPath]);
            authoredWorkbench.SetValidationSampleView(true);
            var validationDirty = authoredWorkbench.IsDirty;
            authoredWorkbench.SelectedValidationSetSample = authoredWorkbench.ValidationSetSamples.Single(sample => sample.SourcePath == sampleInputPath);
            WaitForPresentationTask(authoredWorkbench.EnsureHeightImageSourceAsync());
            layoutWorkspaceView.UpdateLayout();
            var expectedSampleSha = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(sampleInputPath)));
            Check("v3 Validation XY loads the selected sample identity and renders its native pixels",
                authoredWorkbench.HeightImageViewer.Frame is { } sampleFrame
                && sampleFrame.SourcePath == sampleInputPath && sampleFrame.SourceContentSha256.Equals(expectedSampleSha, StringComparison.OrdinalIgnoreCase)
                && layoutWorkspaceView.HasCoordinateTrueHeightImage && !authoredWorkbench.HeightImageViewer.HasError
                && authoredWorkbench.Source.Path != sampleInputPath && authoredWorkbench.IsDirty == validationDirty
                && !authoredWorkbench.IsValidationSetRunning && !authoredWorkbench.IsOrderedRunRunning,
                $"frame={authoredWorkbench.HeightImageViewer.Frame?.SourcePath};teaching={authoredWorkbench.Source.Path};native={layoutWorkspaceView.HasCoordinateTrueHeightImage};error={authoredWorkbench.HeightImageViewer.HasError}");
            var actualHeightFrame = authoredWorkbench.HeightImageViewer.Frame!;
            var legendNotifications = new HashSet<string>();
            PropertyChangedEventHandler legendChanged = (_, args) => { if (args.PropertyName is not null) legendNotifications.Add(args.PropertyName); };
            authoredWorkbench.HeightImageViewer.PropertyChanged += legendChanged;
            var manualLegendApplied = authoredWorkbench.HeightImageViewer.TryApplyManualRange(-10, 30);
            Check("D40 Height Image legend follows the actual frame unit and manual range at five evenly spaced labels",
                manualLegendApplied && authoredWorkbench.HeightImageViewer.LegendUnit == actualHeightFrame.Unit
                && authoredWorkbench.HeightImageViewer.LegendTickLabels.SequenceEqual(new[] { "30.00", "20.00", "10.00", "0.00", "-10.00" })
                && legendNotifications.Contains(nameof(HeightImageViewerViewModel.LegendTickLabels))
                && legendNotifications.Contains(nameof(HeightImageViewerViewModel.LegendUnit))
                && !authoredWorkbench.IsOrderedRunRunning && authoredWorkbench.IsDirty == validationDirty,
                $"unit={authoredWorkbench.HeightImageViewer.LegendUnit};ticks={string.Join(',', authoredWorkbench.HeightImageViewer.LegendTickLabels)};dirty={authoredWorkbench.IsDirty}");
            var validLegendBeforeBadRange = authoredWorkbench.HeightImageViewer.LegendTickLabels.ToArray();
            var invalidLegendRejected = !authoredWorkbench.HeightImageViewer.TryApplyManualRange(30, -10);
            Check("D40 invalid manual range retains the previous five legend labels and reports validation",
                invalidLegendRejected && authoredWorkbench.HeightImageViewer.LegendTickLabels.SequenceEqual(validLegendBeforeBadRange)
                && authoredWorkbench.HeightImageViewer.RangeError.Length > 0 && authoredWorkbench.IsDirty == validationDirty,
                $"rejected={invalidLegendRejected};error={authoredWorkbench.HeightImageViewer.RangeError}");
            authoredWorkbench.HeightImageViewer.UseAutoRange();
            authoredWorkbench.HeightImageViewer.PropertyChanged -= legendChanged;
            Check("D40 auto range restores legend endpoints from the supplied frame rather than HTML example values",
                authoredWorkbench.HeightImageViewer.LegendTickLabels.Count == 5
                && authoredWorkbench.HeightImageViewer.LegendTickLabels[0] == actualHeightFrame.DefaultDisplayMaximum.ToString("0.00", CultureInfo.InvariantCulture)
                && authoredWorkbench.HeightImageViewer.LegendTickLabels[4] == actualHeightFrame.DefaultDisplayMinimum.ToString("0.00", CultureInfo.InvariantCulture),
                $"ticks={string.Join(',', authoredWorkbench.HeightImageViewer.LegendTickLabels)}");
            authoredWorkbench.SelectedValidationSetSample = authoredWorkbench.ValidationSetSamples.Single(sample => sample.SourcePath == malformedInputPath);
            WaitForPresentationTask(authoredWorkbench.EnsureHeightImageSourceAsync());
            layoutWorkspaceView.UpdateLayout();
            Check("v3 a truncated Validation XY input clears stale pixels and reports the load failure",
                authoredWorkbench.HeightImageViewer.Frame is null && authoredWorkbench.HeightImageViewer.HasError && !authoredWorkbench.HeightImageViewer.IsLoading
                && !layoutWorkspaceView.HasCoordinateTrueHeightImage && authoredWorkbench.IsDirty == validationDirty,
                $"frame={authoredWorkbench.HeightImageViewer.Frame is not null};error={authoredWorkbench.HeightImageViewer.Error};native={layoutWorkspaceView.HasCoordinateTrueHeightImage}");
            Check("D40 failed input clears legend values and unit along with stale Height Image pixels",
                authoredWorkbench.HeightImageViewer.LegendTickLabels.Count == 0 && authoredWorkbench.HeightImageViewer.LegendUnit.Length == 0,
                $"ticks={authoredWorkbench.HeightImageViewer.LegendTickLabels.Count};unit={authoredWorkbench.HeightImageViewer.LegendUnit}");
            authoredWorkbench.SelectedValidationSetSample = authoredWorkbench.ValidationSetSamples.Single(sample => sample.SourcePath == sampleInputPath);
            WaitForPresentationTask(authoredWorkbench.EnsureHeightImageSourceAsync());
            layoutWorkspaceView.UpdateLayout();
            Check("v3 valid selection after malformed Validation XY restores actual pixels without running or editing",
                authoredWorkbench.HeightImageViewer.Frame?.SourcePath == sampleInputPath && !authoredWorkbench.HeightImageViewer.HasError
                && layoutWorkspaceView.HasCoordinateTrueHeightImage && authoredWorkbench.IsDirty == validationDirty
                && !authoredWorkbench.IsOrderedRunRunning && !authoredWorkbench.IsValidationSetRunning,
                $"frame={authoredWorkbench.HeightImageViewer.Frame?.SourcePath};error={authoredWorkbench.HeightImageViewer.Error};native={layoutWorkspaceView.HasCoordinateTrueHeightImage};dirty={authoredWorkbench.IsDirty}");
            authoredWorkbench.SetValidationSampleView(false);

            Progress("before narrow record creation");
            var narrowRecordView = new RecipePipelineReviewView
            {
                Width = 300,
                DataContext = authoredWorkbench,
                RunRecordSummary = "검사 기록 스키마 1.10 | 순서형 단계 128개 | 최종 Limited · 규격 미판정"
            };
            var recordTestWindow = new Window
            {
                Width = 300,
                Height = 500,
                WindowStyle = WindowStyle.None,
                Left = testWorkArea.Left,
                Top = testWorkArea.Top,
                ShowInTaskbar = false,
                ShowActivated = false,
                Content = narrowRecordView
            };
            try
            {
                recordTestWindow.Show();
                narrowRecordView.SetPresentationMode(RecipeReviewPresentationMode.Results);
                // Live 3D hosts may continuously schedule Render; Loaded settles bindings/layout without waiting for global idleness.
                recordTestWindow.Dispatcher.Invoke(DispatcherPriority.Loaded, new Action(recordTestWindow.UpdateLayout));
                OpenVisionLab.ThreeD.Shell.Verification.Smoke.ShellWindowNativeInterop.AppendWindowMonitorEvidence(recordTestWindow, monitorReport);
                var recordSummary = (TextBlock)narrowRecordView.FindName("RunRecordStatusSummary");
                OpenVisionLab.ThreeD.Viewer.Rendering.WpfScreenshotCapture.Save(
                    OpenVisionLab.ThreeD.Viewer.Rendering.WpfScreenshotCapture.Capture(recordTestWindow).Bitmap,
                    Path.ChangeExtension(reportPath, ".narrow-record.png"));
                Check(
                    "a narrow results record wraps its final verdict instead of clipping it",
                    recordSummary.Text.EndsWith("최종 Limited · 규격 미판정", StringComparison.Ordinal)
                    && recordSummary.IsVisible
                    && recordSummary.TextWrapping == TextWrapping.Wrap
                    && recordSummary.ActualHeight > 20
                    && recordSummary.ActualWidth <= 300,
                    $"text={recordSummary.Text};size={recordSummary.RenderSize};wrapping={recordSummary.TextWrapping};view={narrowRecordView.RenderSize};loaded={narrowRecordView.IsLoaded}");
            }
            finally
            {
                recordTestWindow.Close();
            }

            using (var pinnedWorkbench = new ToolWorkbenchViewModel(Path.Combine(Path.GetDirectoryName(reportPath)!, "pinned-height-recent.json")))
            {
                if (!pinnedWorkbench.TryOpenTeachingRecipe(recipePath, out var pinOpenError)) throw new InvalidOperationException(pinOpenError);
                var otherInput = Path.Combine(Path.GetDirectoryName(reportPath)!, "pinned-other-input.C3D");
                var badInput = Path.Combine(Path.GetDirectoryName(reportPath)!, "pinned-truncated-input.C3D");
                File.Copy(pinnedWorkbench.Source.Path, otherInput);
                File.Copy(otherInput, badInput);
                pinnedWorkbench.SetValidationSetSources([otherInput, badInput]);
                pinnedWorkbench.SplitViewerVerticallyCommand.Execute(null);
                WaitForPresentationTask(pinnedWorkbench.EnsureHeightImageSourceAsync());
                var teachingFrame = pinnedWorkbench.HeightImageViewer.Frame;
                var teachingPath = pinnedWorkbench.Source.Path;
                var pinnedDirty = pinnedWorkbench.IsDirty;
                var pinnedRois = pinnedWorkbench.GetCurrentAppliedTeachingSelections().Select(item => item.Id).ToArray();
                var otherCandidate = pinnedWorkbench.ViewerWorkspaceCandidates.Single(item => item.SourcePath == otherInput);
                var badCandidate = pinnedWorkbench.ViewerWorkspaceCandidates.Single(item => item.SourcePath == badInput);
                pinnedWorkbench.AuxiliaryViewerContentId = otherCandidate.Id;
                pinnedWorkbench.AuxiliaryViewerPane.SetKindCommand.Execute(ViewerPaneKind.Height);
                WaitForPresentationTask(pinnedWorkbench.AuxiliaryViewerPane.HeightImage.ObservedLoadTask);
                var otherFrame = pinnedWorkbench.AuxiliaryViewerPane.HeightImage.Frame;
                var expectedOtherSha = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(otherInput)));
                Check("V5 pinned Height reads the selected file independently of the teaching frame",
                    otherFrame?.SourcePath == otherInput && otherFrame.SourceContentSha256 == expectedOtherSha
                    && !ReferenceEquals(otherFrame, teachingFrame) && ReferenceEquals(pinnedWorkbench.HeightImageViewer.Frame, teachingFrame),
                    $"pin={pinnedWorkbench.AuxiliaryViewerPane.SourcePath};frame={otherFrame?.SourcePath};teaching={teachingFrame?.SourcePath}");
                File.WriteAllBytes(badInput, File.ReadAllBytes(otherInput)[..128]);
                pinnedWorkbench.AuxiliaryViewerContentId = badCandidate.Id;
                WaitForPresentationTask(pinnedWorkbench.AuxiliaryViewerPane.HeightImage.ObservedLoadTask);
                Check("V5 a registered file truncated before Height selection clears the previous file and reports its own error",
                    pinnedWorkbench.AuxiliaryViewerPane.IsHeight && pinnedWorkbench.AuxiliaryViewerPane.HeightImage.Frame is null
                    && pinnedWorkbench.AuxiliaryViewerPane.HeightImage.HasError && ReferenceEquals(pinnedWorkbench.HeightImageViewer.Frame, teachingFrame),
                    $"frame={pinnedWorkbench.AuxiliaryViewerPane.HeightImage.Frame?.SourcePath};error={pinnedWorkbench.AuxiliaryViewerPane.HeightImage.Error}");
                pinnedWorkbench.AuxiliaryViewerContentId = otherCandidate.Id;
                WaitForPresentationTask(pinnedWorkbench.AuxiliaryViewerPane.HeightImage.ObservedLoadTask);
                Check("V5 valid pinned Height recovers without changing the teaching input, ROIs or execution state",
                    pinnedWorkbench.AuxiliaryViewerPane.HeightImage.Frame?.SourcePath == otherInput && !pinnedWorkbench.AuxiliaryViewerPane.HeightImage.HasError
                    && pinnedWorkbench.Source.Path == teachingPath && pinnedWorkbench.IsDirty == pinnedDirty
                    && pinnedWorkbench.GetCurrentAppliedTeachingSelections().Select(item => item.Id).SequenceEqual(pinnedRois)
                    && !pinnedWorkbench.IsOrderedRunRunning && !pinnedWorkbench.IsSelectedStepPreviewRunning,
                    $"frame={pinnedWorkbench.AuxiliaryViewerPane.HeightImage.Frame?.SourcePath};teaching={pinnedWorkbench.Source.Path};dirty={pinnedWorkbench.IsDirty}");
                pinnedWorkbench.AuxiliaryViewerContentId = ToolWorkbenchViewModel.HeightImageViewerContentId;
                WaitForPresentationTask(pinnedWorkbench.AuxiliaryViewerPane.HeightImage.ObservedLoadTask);
                Check("V5 the default Height Image item retains the existing shared input controls",
                    ReferenceEquals(pinnedWorkbench.AuxiliaryViewerPane.HeightImage, pinnedWorkbench.HeightImageViewer)
                    && ReferenceEquals(pinnedWorkbench.HeightImageViewer.Frame, teachingFrame), "default route and Frame retained");
            }
            layoutTestWindow.Close();
            summary = $"Viewer workspace presentation verification: Pass ({passed} checks)";
            lines.Add(summary);
            WriteReport(reportPath, lines);
            return true;
        }
        catch (Exception exception)
        {
            chartTestWindow?.Close();
            layoutTestWindow?.Close();
            summary = $"Viewer workspace presentation verification: Fail after {passed} checks: {exception.Message}";
            lines.Add(summary);
            lines.Add(exception.ToString());
            WriteReport(reportPath, lines);
            return false;
        }
    }

    public static bool VerifyPaneRestoration(string reportPath, out string summary)
    {
        var lines = new List<string>();
        var passed = 0;
        Window? host = null;
        void Check(string name, bool condition, string detail)
        {
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
            if (!condition) throw new InvalidOperationException(name + ": " + detail);
            passed++;
        }

        try
        {
            var root = Path.GetDirectoryName(Path.GetFullPath(reportPath))!;
            using var workbench = new ToolWorkbenchViewModel(Path.Combine(root, "recent.json"));
            var recipe = Path.Combine(root, "native-group-fixture", "grouped.ov3d-recipe.json");
            Check("pane restoration fixture opens without execution", workbench.TryOpenTeachingRecipe(recipe, out var message), message);
            using var viewer = new OpenVisionThreeDViewerControl { SidePanelsVisible = false };
            using var view = new ViewerWorkspaceView { DataContext = workbench, MainViewerContent = viewer };
            OpenVisionLab.ThreeD.Shell.Verification.Smoke.ShellWindowNativeInterop.TryGetLeftmostWorkArea(out var area);
            host = new Window { Content = view, WindowStyle = WindowStyle.None, WindowStartupLocation = WindowStartupLocation.Manual,
                ShowInTaskbar = false, Left = area.Left, Top = area.Top, Width = 1280, Height = 760 };
            host.Show();
            OpenVisionLab.ThreeD.Shell.Verification.Smoke.ShellWindowNativeInterop.AppendWindowMonitorEvidence(host, reportPath + ".monitor.txt");
            var slots = new[] { "MainSlot", "AuxiliarySlot", "FourSectionSlot", "FourHeightSlot" }.Select(name => (Border)view.FindName(name)).ToArray();
            var ids = new[] { "MainPaneMaximize", "AuxiliaryPaneMaximize", "FourSectionPaneMaximize", "FourHeightPaneMaximize" };
            var slotIds = new[] { "viewer.main", "viewer.auxiliary", "viewer.section", "viewer.height" };
            var source = workbench.Source.Path;
            var rois = workbench.GetCurrentAppliedTeachingSelections().ToArray();
            var dirty = workbench.IsDirty;
            foreach (var width in new[] { 1280, 1920 })
            {
                host.Width = width;
                host.Height = width == 1280 ? 760 : 1040;
                workbench.SetFourViewerLayoutCommand.Execute(null);
                WaitForPresentationTask(workbench.HeightViewerPane.HeightImage.ObservedLoadTask);
                host.Dispatcher.Invoke(DispatcherPriority.Render, new Action(host.UpdateLayout));
                var frame = workbench.HeightViewerPane.HeightImage.Frame;
                var selected = workbench.SelectedPipelineStep;
                var pin = workbench.AuxiliaryViewerContentId;
                bool Restored()
                {
                    var bounds = slots.Select(slot => slot.TransformToAncestor(view).TransformBounds(new Rect(slot.RenderSize))).ToArray();
                    return workbench.MaximizedViewerSlotId.Length == 0 && workbench.IsFourViewerLayout
                        && slots.Select((slot, index) => slot.IsVisible && slot.ActualWidth > 100 && slot.ActualHeight > 100
                            && Grid.GetRow(slot) == (index < 2 ? 0 : 2) && Grid.GetColumn(slot) == (index % 2 == 0 ? 0 : 2)
                            && Grid.GetRowSpan(slot) == 1 && Grid.GetColumnSpan(slot) == 1).All(value => value)
                        && bounds.SelectMany((rect, index) => bounds.Skip(index + 1).Select(other => !rect.IntersectsWith(other))).All(value => value)
                        && source == workbench.Source.Path && workbench.GetCurrentAppliedTeachingSelections().SequenceEqual(rois)
                        && ReferenceEquals(frame, workbench.HeightViewerPane.HeightImage.Frame) && ReferenceEquals(selected, workbench.SelectedPipelineStep)
                        && pin == workbench.AuxiliaryViewerContentId && dirty == workbench.IsDirty
                        && !workbench.IsOrderedRunRunning && !workbench.IsSelectedStepPreviewRunning && viewer.HostState.Inspection.PublishedResultCount == 0;
                }
                for (var index = 0; index < slots.Length; index++)
                {
                    var button = OpenVisionLab.ThreeD.Shell.Verification.Smoke.ShellSmokeArtifacts.FindVisualDescendants<Button>(view)
                        .Single(item => AutomationProperties.GetAutomationId(item) == ids[index]);
                    button.Command.Execute(button.CommandParameter);
                    host.UpdateLayout();
                    Check($"{width} {slotIds[index]} bound maximize fills the workspace", workbench.MaximizedViewerSlotId == slotIds[index]
                        && slots[index].ActualWidth > view.ActualWidth * 0.95 && slots[index].ActualHeight > view.ActualHeight * 0.8,
                        $"slot={workbench.MaximizedViewerSlotId};size={slots[index].RenderSize}");
                    button.Command.Execute(button.CommandParameter);
                    host.UpdateLayout();
                    Check($"{width} {slotIds[index]} restore returns four separate panes without changing inspection state", Restored(), "grid positions/spans/bounds, Frame/ROI/pin/selection/dirty/execution");
                }
                workbench.ToggleViewerPaneMaximizedCommand.Execute("viewer.height");
                workbench.SetFourViewerLayoutCommand.Execute(null);
                host.UpdateLayout();
                Check($"{width} selecting the current Four layout exits pane maximization", Restored(), "same-layout command");
                workbench.ToggleViewerPaneMaximizedCommand.Execute("viewer.height");
                workbench.SetSingleViewerLayoutCommand.Execute(null);
                workbench.SetFourViewerLayoutCommand.Execute(null);
                host.UpdateLayout();
                Check($"{width} Single then Four restores the Height slot after maximization", Restored(), "alternate layout path");
                workbench.ToggleViewerPaneMaximizedCommand.Execute("viewer.height");
                host.UpdateLayout();
                var heightButton = OpenVisionLab.ThreeD.Shell.Verification.Smoke.ShellSmokeArtifacts.FindVisualDescendants<Button>(view)
                    .Single(item => AutomationProperties.GetAutomationId(item) == "FourHeightPaneMaximize");
                System.Windows.Input.Keyboard.Focus(heightButton);
                Check($"{width} keyboard restoration starts inside the maximized Height pane", heightButton.IsKeyboardFocused && view.IsKeyboardFocusWithin, "real WPF keyboard focus");
                workbench.SetSingleViewerLayoutCommand.Execute(null);
                host.UpdateLayout();
                Check($"{width} hiding the focused pane retains keyboard focus inside the workspace", view.IsKeyboardFocusWithin
                    && System.Windows.Input.Keyboard.FocusedElement is UIElement { IsVisible: true }, $"focus={System.Windows.Input.Keyboard.FocusedElement};within={view.IsKeyboardFocusWithin};oldVisible={heightButton.IsVisible}");
                var keyHandled = OpenVisionLab.ThreeD.Presentation.Behaviors.ViewerKeyboardBehavior.TryExecute(view,
                    System.Windows.Input.Keyboard.FocusedElement as DependencyObject, System.Windows.Input.Key.D4, System.Windows.Input.ModifierKeys.Control);
                host.UpdateLayout();
                Check($"{width} Ctrl4 routes through the retained workspace focus after Single", keyHandled && Restored(), "existing keyboard behavior and layout command");
            }
            summary = $"Viewer pane restoration verification: Pass ({passed} checks)";
            WriteReport(reportPath, lines.Append(summary));
            return true;
        }
        catch (Exception exception)
        {
            summary = $"Viewer pane restoration verification: Fail after {passed} checks: {exception.Message}";
            WriteReport(reportPath, lines.Append(summary));
            return false;
        }
        finally { host?.Close(); }
    }

    public static bool VerifyLegendKeyboard(string reportPath, out string summary)
    {
        var lines = new List<string> { "Boundary|Rendered Height Image keyboard routes; synthetic WPF keys/drag events and capture, not physical held-drag Escape." };
        var passed = 0;
        Window? host = null;
        void Check(string name, bool condition, string detail)
        {
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
            if (!condition) throw new InvalidOperationException(name + ": " + detail);
            passed++;
        }

        try
        {
            var root = Path.GetDirectoryName(Path.GetFullPath(reportPath))!;
            using var workbench = new ToolWorkbenchViewModel(Path.Combine(root, "recent.json"));
            Check("legend keyboard fixture opens without execution", workbench.TryOpenTeachingRecipe(Path.Combine(root, "native-group-fixture", "grouped.ov3d-recipe.json"), out var message), message);
            var image = workbench.HeightImageViewer;
            WaitForPresentationTask(workbench.EnsureHeightImageSourceAsync());
            Check("isolated Height fixture loads the recipe's actual source", image.HasImage && image.Frame?.SourcePath == workbench.Source.Path,
                $"source={image.Frame?.SourcePath};error={image.Error}");
            var view = new HeightImageViewerView { DataContext = image, UseEmbeddedChrome = true };
            OpenVisionLab.ThreeD.Shell.Verification.Smoke.ShellWindowNativeInterop.TryGetLeftmostWorkArea(out var area);
            host = new Window { Content = view, WindowStyle = WindowStyle.None, WindowStartupLocation = WindowStartupLocation.Manual,
                ShowInTaskbar = false, Left = area.Left, Top = area.Top, Width = 1280, Height = 760 };
            host.Show();
            host.Activate();
            OpenVisionLab.ThreeD.Shell.Verification.Smoke.ShellWindowNativeInterop.AppendWindowMonitorEvidence(host, reportPath + ".monitor.txt");
            var bar = (HeightLegendRangeBar)view.FindName("HeightLegendTrack");
            var upper = (Thumb)bar.FindName("UpperHandle");
            var popup = (Popup)bar.FindName("NumericPopup");
            var numeric = (TextBox)bar.FindName("NumericInput");
            var beforeSource = workbench.Source.Path;
            var beforeRois = workbench.GetCurrentAppliedTeachingSelections().ToArray();
            var beforeDirty = workbench.IsDirty;
            var beforeFrame = image.Frame;
            void Press(UIElement target, System.Windows.Input.Key key)
            {
                var args = new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice, PresentationSource.FromVisual(target), 0, key)
                    { RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent };
                target.RaiseEvent(args);
                if (!args.Handled)
                {
                    args.RoutedEvent = System.Windows.Input.Keyboard.KeyDownEvent;
                    target.RaiseEvent(args);
                }
                host.Dispatcher.Invoke(DispatcherPriority.Loaded, new Action(host.UpdateLayout));
            }
            bool Preserved() => workbench.IsDirty == beforeDirty && workbench.Source.Path == beforeSource
                && workbench.GetCurrentAppliedTeachingSelections().SequenceEqual(beforeRois) && ReferenceEquals(beforeFrame, image.Frame)
                && !workbench.IsOrderedRunRunning && !workbench.IsSelectedStepPreviewRunning;
            foreach (var width in new[] { 1280, 1920 })
            {
                host.Width = width;
                host.Height = width == 1280 ? 760 : 1040;
                host.Dispatcher.Invoke(DispatcherPriority.Loaded, new Action(host.UpdateLayout));
                workbench.BeginTeachingSelectionCaptureCommand.Execute(null);
                Check($"{width} isolated Height fixture supplies the existing ROI through its candidate contract",
                    workbench.TryUpdateHeightImageRoiCandidate(beforeRois[0].GridRectangle!, out var candidateMessage), candidateMessage);
                Check($"{width} actual ROI review is active before legend input", image.RoiWorkspace.IsCaptureActive && image.RoiWorkspace.ApplyCommand.CanExecute(null), $"capture={image.RoiWorkspace.IsCaptureActive};lifecycle={image.RoiWorkspace.Lifecycle}");
                System.Windows.Input.Keyboard.Focus(upper);
                Check($"{width} legend has actual keyboard focus", upper.IsKeyboardFocused && upper.IsVisible, $"focused={upper.IsKeyboardFocused};visible={upper.IsVisible};enabled={upper.IsEnabled};windowActive={host.IsActive};size={upper.RenderSize}");
                var beforeRange = (image.HeightLegendMinimum, image.HeightLegendMaximum);
                var beforeOptions = image.HeightDisplayOptions;
                Press(upper, System.Windows.Input.Key.Enter);
                Check($"{width} Enter opens the legend numeric editor without applying ROI", popup.IsOpen && image.RoiWorkspace.IsCaptureActive && Preserved(), $"popup={popup.IsOpen};capture={image.RoiWorkspace.IsCaptureActive};dirty={workbench.IsDirty}");
                numeric.Text = "NaN";
                Press(numeric, System.Windows.Input.Key.Enter);
                Check($"{width} invalid legend input stays editable without applying ROI", popup.IsOpen && image.RangeError.Length > 0 && image.RoiWorkspace.IsCaptureActive && Preserved(), $"popup={popup.IsOpen};error={image.RangeError}");
                var numericError = (TextBlock)bar.FindName("NumericError");
                var popupBorder = (Border)popup.Child;
                numericError.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Check($"{width} Korean numeric error stays on one unclipped line inside the Window", numericError.TextWrapping == TextWrapping.NoWrap
                    && numericError.ActualWidth + 1 >= numericError.DesiredSize.Width && popupBorder.ActualWidth < host.ActualWidth
                    && popupBorder.PointToScreen(new Point()).X >= host.Left
                    && popupBorder.PointToScreen(new Point(popupBorder.ActualWidth, 0)).X <= host.Left + host.ActualWidth,
                    $"text={numericError.Text};actual={numericError.ActualWidth};desired={numericError.DesiredSize.Width};popup={popupBorder.ActualWidth}");
                OpenVisionLab.ThreeD.Viewer.Rendering.WpfScreenshotCapture.Save(
                    OpenVisionLab.ThreeD.Viewer.Rendering.WpfScreenshotCapture.Capture(host).Bitmap, Path.ChangeExtension(reportPath, $".invalid-{width}.png"));
                Press(numeric, System.Windows.Input.Key.Escape);
                Check($"{width} editor Escape restores display range and leaves ROI review active", !popup.IsOpen && image.RoiWorkspace.IsCaptureActive && Preserved()
                    && (image.HeightLegendMinimum, image.HeightLegendMaximum) == beforeRange && image.HeightDisplayOptions == beforeOptions,
                    $"popup={popup.IsOpen};capture={image.RoiWorkspace.IsCaptureActive};range={image.HeightLegendMinimum}..{image.HeightLegendMaximum}");
                foreach (var name in new[] { "UpperHandle", "LowerHandle", "RangeBody" })
                {
                    var thumb = (Thumb)bar.FindName(name);
                    System.Windows.Input.Keyboard.Focus(thumb);
                    thumb.RaiseEvent(new DragStartedEventArgs(0, 0) { RoutedEvent = Thumb.DragStartedEvent });
                    thumb.CaptureMouse();
                    thumb.RaiseEvent(new DragDeltaEventArgs(0, name == "LowerHandle" ? -5 : 5) { RoutedEvent = Thumb.DragDeltaEvent });
                    Check($"{width} {name} has an in-flight display edit", thumb.IsMouseCaptured && image.HeightDisplayOptions.IsRangeDragActive
                        && (image.HeightLegendMinimum, image.HeightLegendMaximum) != beforeRange && image.RoiWorkspace.IsCaptureActive && Preserved(),
                        $"capture={thumb.IsMouseCaptured};range={image.HeightLegendMinimum}..{image.HeightLegendMaximum}");
                    Press(thumb, System.Windows.Input.Key.Escape);
                    Check($"{width} {name} Escape restores its snapshot and releases capture without cancelling ROI", !bar.IsMouseCaptureWithin
                        && (image.HeightLegendMinimum, image.HeightLegendMaximum) == beforeRange && image.HeightDisplayOptions == beforeOptions
                        && image.RoiWorkspace.IsCaptureActive && Preserved(), $"mouseCapture={bar.IsMouseCaptureWithin};roiCapture={image.RoiWorkspace.IsCaptureActive}");
                }
                Press(upper, System.Windows.Input.Key.Enter);
                numeric.Text = (beforeRange.Item2 - (beforeRange.Item2 - beforeRange.Item1) * 0.05).ToString("R", CultureInfo.InvariantCulture);
                Press(numeric, System.Windows.Input.Key.Enter);
                Check($"{width} valid retry commits only the display range", !popup.IsOpen && image.RangeError.Length == 0 && !image.HeightDisplayOptions.IsRangeDragActive
                    && image.HeightLegendMaximum < beforeRange.Item2 && image.RoiWorkspace.IsCaptureActive && Preserved(), $"maximum={image.HeightLegendMaximum};capture={image.RoiWorkspace.IsCaptureActive}");
                workbench.CancelTeachingSelectionCaptureCommand.Execute(null);
                image.UseAutoRange();
                Check($"{width} explicit ROI cancellation remains available after legend recovery", !image.RoiWorkspace.IsCaptureActive && Preserved(), "existing command; source/ROI/dirty/Frame/execution unchanged");
            }
            summary = $"Height legend keyboard verification: Pass ({passed} checks)";
            WriteReport(reportPath, lines.Append(summary));
            return true;
        }
        catch (Exception exception)
        {
            summary = $"Height legend keyboard verification: Fail after {passed} checks: {exception.Message}";
            WriteReport(reportPath, lines.Append(summary));
            return false;
        }
        finally { host?.Close(); }
    }


    public static bool VerifyLegendInputFocus(string reportPath, out string summary)
    {
        var lines = new List<string> { "Boundary|Actual3D and Height WPF controls; routed keyboard/focus and range binding, not Computer Use or pointer-held evidence." };
        var passed = 0;
        Window? host = null;
        void Check(string name, bool condition, string detail)
        {
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
            if (!condition) throw new InvalidOperationException(name + ": " + detail);
            passed++;
        }
        try
        {
            var root = Path.GetDirectoryName(Path.GetFullPath(reportPath))!;
            using var workbench = new ToolWorkbenchViewModel(Path.Combine(root, "recent.json"));
            Check("focus fixture opens without execution", workbench.TryOpenTeachingRecipe(Path.Combine(root, "native-group-fixture", "grouped.ov3d-recipe.json"), out var message), message);
            using var viewer = new OpenVisionThreeDViewerControl(loadDefaultSamples: false) { SidePanelsVisible = false };
            Check("3D consumer loads its source", viewer.LoadC3DSource(workbench.Source.Path) && viewer.TrySetSelectedColorMap("Height"), workbench.Source.Path);
            WaitForPresentationTask(workbench.EnsureHeightImageSourceAsync());
            var image = new HeightImageViewerView { DataContext = workbench.HeightImageViewer, UseEmbeddedChrome = true };
            OpenVisionLab.ThreeD.Shell.Verification.Smoke.ShellWindowNativeInterop.TryGetLeftmostWorkArea(out var area);
            host = new Window { Content = viewer, WindowStyle = WindowStyle.None, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual, Left = area.Left, Top = area.Top, Width = 1280, Height = 760 };
            host.Show();
            host.Activate();
            OpenVisionLab.ThreeD.Shell.Verification.Smoke.ShellWindowNativeInterop.AppendWindowMonitorEvidence(host, reportPath + ".monitor.txt");
            foreach (var consumer in new[] { "3D", "Height" })
            {
                host.Content = consumer == "3D" ? viewer : image;
                var bar = consumer == "3D" ? (HeightLegendRangeBar)viewer.FindName("ViewerHeightLegendRangeBar") : (HeightLegendRangeBar)image.FindName("HeightLegendTrack");
                var upper = (Thumb)bar.FindName("UpperHandle");
                var numeric = (TextBox)bar.FindName("NumericInput");
                var popup = (Popup)bar.FindName("NumericPopup");
                void Settle() => host.Dispatcher.Invoke(DispatcherPriority.Input, new Action(host.UpdateLayout));
                void Press(UIElement target, System.Windows.Input.Key key)
                {
                    var args = new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice, PresentationSource.FromVisual(target), 0, key) { RoutedEvent = System.Windows.Input.Keyboard.KeyDownEvent };
                    target.RaiseEvent(args);
                    Settle();
                }
                foreach (var width in new[] { 1280, 1920 })
                {
                    host.Width = width;
                    host.Height = width == 1280 ? 760 : 1040;
                    Settle();
                    System.Windows.Input.Keyboard.Focus(upper);
                    Check($"{consumer}/{width} handle receives keyboard focus", upper.IsKeyboardFocused && upper.IsVisible, $"focus={System.Windows.Input.Keyboard.FocusedElement};visible={upper.IsVisible}");
                    var original = (bar.Minimum, bar.Maximum);
                    Press(upper, System.Windows.Input.Key.Enter);
                    Check($"{consumer}/{width} Enter-open focuses numeric text", popup.IsOpen && numeric.IsKeyboardFocused && numeric.SelectionLength == numeric.Text.Length, $"focus={System.Windows.Input.Keyboard.FocusedElement};selection={numeric.SelectionLength}");
                    numeric.Text = "NaN";
                    Press(numeric, System.Windows.Input.Key.Enter);
                    Check($"{consumer}/{width} invalid entry retains actual numeric focus", popup.IsOpen && numeric.IsKeyboardFocused && !string.IsNullOrEmpty(bar.ErrorText), bar.ErrorText);
                    numeric.Text = (original.Maximum - (original.Maximum - original.Minimum) * 0.05).ToString("R", CultureInfo.InvariantCulture);
                    Press(numeric, System.Windows.Input.Key.Enter);
                    Check($"{consumer}/{width} valid retry commits range and returns handle focus", !popup.IsOpen && upper.IsKeyboardFocused && bar.Maximum < original.Maximum, $"range={bar.Minimum}..{bar.Maximum};focus={System.Windows.Input.Keyboard.FocusedElement}");
                    var committed = (bar.Minimum, bar.Maximum);
                    Press(upper, System.Windows.Input.Key.Enter);
                    Press(numeric, System.Windows.Input.Key.Escape);
                    Check($"{consumer}/{width} Escape preserves committed range and handle focus", !popup.IsOpen && upper.IsKeyboardFocused && committed == (bar.Minimum, bar.Maximum), $"range={bar.Minimum}..{bar.Maximum}");
                    Press(upper, System.Windows.Input.Key.Enter);
                    popup.IsOpen = false;
                    Settle();
                    Check($"{consumer}/{width} queued focus cannot reopen a closed editor", !popup.IsOpen && !numeric.IsKeyboardFocused, "Popup lifecycle owns cancellation");
                    Check($"{consumer}/{width} display input does not execute or dirty the recipe", !workbench.IsDirty && !workbench.IsSelectedStepPreviewRunning && !workbench.IsValidationSetRunning && viewer.HostState.Inspection.PublishedResultCount == 0, "separate control/command proof");
                }
            }
            summary = $"Legend input focus verification: Pass ({passed} checks)";
            WriteReport(reportPath, lines.Append(summary));
            return true;
        }
        catch (Exception exception)
        {
            summary = $"Legend input focus verification: Fail after {passed} checks: {exception.Message}";
            WriteReport(reportPath, lines.Append(summary));
            return false;
        }
        finally { host?.Close(); }
    }

    public static bool VerifyProfileFooter(string reportPath, out string summary)
    {
        var lines = new List<string> { "Boundary|Real WPF pane consumers and bindings; automated layout rendering, not Computer Use or physical input." };
        var passed = 0;
        Window? host = null;
        void Check(string name, bool condition, string detail)
        {
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
            if (!condition) throw new InvalidOperationException(name + ": " + detail);
            passed++;
        }

        try
        {
            var root = Path.GetDirectoryName(Path.GetFullPath(reportPath))!;
            using var workbench = new ToolWorkbenchViewModel(Path.Combine(root, "recent.json"));
            Check("footer fixture opens without execution", workbench.TryOpenTeachingRecipe(Path.Combine(root, "native-group-fixture", "grouped.ov3d-recipe.json"), out var message), message);
            using var viewer = new OpenVisionThreeDViewerControl(loadDefaultSamples: false) { SidePanelsVisible = false };
            using var view = new ViewerWorkspaceView { DataContext = workbench, MainViewerContent = viewer, HorizontalAlignment = HorizontalAlignment.Left };
            OpenVisionLab.ThreeD.Shell.Verification.Smoke.ShellWindowNativeInterop.TryGetLeftmostWorkArea(out var area);
            host = new Window { Content = view, WindowStyle = WindowStyle.None, WindowStartupLocation = WindowStartupLocation.Manual,
                ShowInTaskbar = false, Left = area.Left, Top = area.Top, Width = 1280, Height = 760 };
            host.Show();
            OpenVisionLab.ThreeD.Shell.Verification.Smoke.ShellWindowNativeInterop.AppendWindowMonitorEvidence(host, reportPath + ".monitor.txt");
            workbench.SetFourViewerLayoutCommand.Execute(null);
            var panes = new[] { workbench.MainViewerPane, workbench.AuxiliaryViewerPane, workbench.SectionViewerPane, workbench.HeightViewerPane };
            foreach (var pane in panes) pane.SetKindCommand.Execute(ViewerPaneKind.Section);
            var profiles = new[] { "MainPaneProfile", "AuxiliaryPaneProfile", "FourSectionProfile", "FourHeightProfile" }.Select(name => (HeightProfileView)view.FindName(name)).ToArray();
            var source = workbench.Source.Path;
            var rois = workbench.GetCurrentAppliedTeachingSelections().ToArray();
            var dirty = workbench.IsDirty;
            host.Dispatcher.Invoke(DispatcherPriority.Loaded, new Action(host.UpdateLayout));
            var context = view.SelectionContext;
            foreach (var width in new[] { 1280, 1920 })
            {
                host.Width = width;
                host.Height = width == 1280 ? 760 : 1040;
                view.Width = width - 620;
                foreach (var longContext in new[] { false, true })
                {
                    view.SetCurrentValue(ViewerWorkspaceView.SelectionContextProperty, longContext
                        ? context + " · 선택한 검사 영역과 측정 조건을 확인하는 긴 문맥 / Thickness Coupon v1 / raw-height"
                        : context);
                    host.Dispatcher.Invoke(DispatcherPriority.Render, new Action(host.UpdateLayout));
                    OpenVisionLab.ThreeD.Viewer.Rendering.WpfScreenshotCapture.Save(
                        OpenVisionLab.ThreeD.Viewer.Rendering.WpfScreenshotCapture.Capture(host).Bitmap, Path.ChangeExtension(reportPath, $".footer-{width}-{longContext}.png"));
                    foreach (var profile in profiles)
                    {
                        var footer = (Border)profile.FindName("ProfileFooter");
                        var scroll = OpenVisionLab.ThreeD.Shell.Verification.Smoke.ShellSmokeArtifacts.FindVisualDescendants<ScrollViewer>(footer).Single();
                        var caption = (TextBlock)scroll.Content;
                        Check($"{width} {profile.Name} {(longContext ? "long" : "bound")} context is fully visible",
                            profile.IsVisible && footer.IsVisible && caption.ActualHeight > 0 && scroll.ExtentHeight <= scroll.ViewportHeight + 0.5,
                            $"footer={footer.RenderSize};text={caption.RenderSize};extent={scroll.ExtentHeight};viewport={scroll.ViewportHeight};context={view.SelectionContext}");
                        Check($"{width} {profile.Name} context stays inside its pane with a usable chart",
                            footer.TransformToAncestor(profile).TransformBounds(new Rect(footer.RenderSize)).Bottom <= profile.ActualHeight + 0.5
                            && ((Grid)profile.FindName("ProfileChartBody")).ActualHeight > 60
                            && caption.Inlines.OfType<System.Windows.Documents.Run>().Any(run => run.Text == view.SelectionContext),
                            $"pane={profile.RenderSize};chart={((Grid)profile.FindName("ProfileChartBody")).RenderSize}");
                    }
                }
                view.SetCurrentValue(ViewerWorkspaceView.SelectionContextProperty, string.Empty);
                host.Dispatcher.Invoke(DispatcherPriority.Render, new Action(host.UpdateLayout));
                foreach (var profile in profiles)
                {
                    var footer = (Border)profile.FindName("ProfileFooter");
                    Check($"{width} {profile.Name} short context restores the normal footer", Math.Abs(footer.ActualHeight - 24) < 0.5, $"height={footer.ActualHeight}");
                    profile.ShowCoordinateFooter = false;
                    host.UpdateLayout();
                    Check($"{width} {profile.Name} hidden footer releases its row", footer.Visibility == Visibility.Collapsed, $"visibility={footer.Visibility}");
                    profile.ShowCoordinateFooter = true;
                }
                view.SetCurrentValue(ViewerWorkspaceView.SelectionContextProperty, context);
                host.Dispatcher.Invoke(DispatcherPriority.Render, new Action(host.UpdateLayout));
                Check($"{width} presentation changes preserve input, ROI, recipe and execution state", source == workbench.Source.Path
                    && rois.SequenceEqual(workbench.GetCurrentAppliedTeachingSelections()) && dirty == workbench.IsDirty
                    && !workbench.IsOrderedRunRunning && !workbench.IsSelectedStepPreviewRunning && viewer.HostState.Inspection.PublishedResultCount == 0,
                    "no Run, save, ROI edit or manual-range persistence");
            }
            summary = $"Profile footer verification: Pass ({passed} checks)";
            WriteReport(reportPath, lines.Append(summary));
            return true;
        }
        catch (Exception exception)
        {
            summary = $"Profile footer verification: Fail after {passed} checks: {exception.Message}";
            WriteReport(reportPath, lines.Append(summary));
            return false;
        }
        finally { host?.Close(); }
    }

    private static void WriteReport(string reportPath, IEnumerable<string> lines)
    {
        var fullPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllLines(fullPath, lines);
    }

    private static void WaitForPresentationTask(Task task)
    {
        if (task.IsCompleted) { task.GetAwaiter().GetResult(); return; }
        var waitFrame = new DispatcherFrame();
        var started = System.Diagnostics.Stopwatch.StartNew();
        var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(10) };
        timer.Tick += (_, _) => { if (task.IsCompleted || started.Elapsed > TimeSpan.FromSeconds(15)) waitFrame.Continue = false; };
        timer.Start();
        try { Dispatcher.PushFrame(waitFrame); }
        finally { timer.Stop(); }
        if (!task.IsCompleted) throw new TimeoutException("Height Image presentation did not settle within 15 seconds.");
        task.GetAwaiter().GetResult();
    }

    private static bool HasVisualText(DependencyObject root, string expected)
    {
        if (root is TextBlock textBlock
            && string.Equals(textBlock.Text, expected, StringComparison.Ordinal))
        {
            return true;
        }

        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            if (HasVisualText(VisualTreeHelper.GetChild(root, index), expected))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsVisualDescendant(DependencyObject root, DependencyObject candidate)
    {
        if (ReferenceEquals(root, candidate))
        {
            return true;
        }

        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            if (IsVisualDescendant(VisualTreeHelper.GetChild(root, index), candidate))
            {
                return true;
            }
        }

        return false;
    }

}
