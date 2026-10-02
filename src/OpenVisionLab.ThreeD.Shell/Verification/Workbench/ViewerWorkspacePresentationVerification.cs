using System.IO;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Shell.Views.Workbench;
using OpenVisionLab.ThreeD.Viewer;
using OpenVisionLab.ThreeD.Viewer.Models;

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
        Window? layoutTestWindow = null;

        void Check(string name, bool condition, string detail)
        {
            if (!condition)
            {
                throw new InvalidOperationException($"{name}: {detail}");
            }

            passed++;
            lines.Add($"PASS | {name} | {detail}");
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
            var mainSlot = (Border)layoutWorkspaceView.FindName("MainSlot");
            var mainViewerGeometry = (ComboBox)mainViewer.FindName("GeometryStyleSelector");
            var mainDiagnosticChannel = (ComboBox)mainViewer.FindName("DiagnosticChannelSelector");
            var mainViewerDisplayOptions = (MenuItem)mainViewer.FindName("ViewerDisplayOptionsMenu");
            var heightDistributionPanel = mainViewer.FindName("C3DHeightDistributionPanel");
            var mainPresentationPalette = (ComboBox)mainPresentationBar.FindName("PaletteSelector");
            var mainPresentationDisplayOptions = (Expander)mainPresentationBar.FindName("ViewerSlotDisplayOptionsExpander");
            var viewerWorkspaceOptionsRoot = (MenuItem)viewerWorkspaceOptionsMenu.Items[0];
            var splitViewerMenuItem = (MenuItem)viewerWorkspaceOptionsRoot.Items[0];
            var popOutViewerMenuItem = (MenuItem)viewerWorkspaceOptionsRoot.Items[1];
            var cameraLinkMenuItem = (MenuItem)viewerWorkspaceOptionsRoot.Items[3];
            var viewerDisplaySettingsButton = (Button)mainPresentationBar.FindName("ViewerDisplaySettingsButton");
            var viewerDisplaySettingsPopup = (Popup)mainPresentationBar.FindName("ViewerDisplaySettingsPopup");
            var presentationDiagnosticGroup = mainPresentationBar.FindName("DiagnosticChannelGroup");
            var viewerGrid = (Grid)layoutWorkspaceView.FindName("ViewerGrid");
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
                && viewerDisplaySettingsButton.Visibility == (mainPresentationBar.Presentation.HasC3DHeightColorRange ? Visibility.Visible : Visibility.Collapsed)
                && !viewerDisplaySettingsButton.IsVisible
                && BindingOperations.GetBindingExpression(mainPresentationPalette, Selector.SelectedItemProperty)?.ParentBinding.Path.Path == "Presentation.SelectedColorMap",
                $"open={viewerDisplaySettingsPopup.IsOpen};button={viewerDisplaySettingsButton.Visibility};visible={viewerDisplaySettingsButton.IsVisible};selected={mainPresentationPalette.SelectedItem}");
            Check(
                "Viewer display toolbar omits internal diagnostics and repeated raw range summaries",
                mainPresentationBar.Content is Border { ToolTip: null }
                && !OpenVisionLab.ThreeD.Shell.Verification.Smoke.ShellSmokeArtifacts.FindVisualDescendants<TextBlock>(mainPresentationBar).Any(text => BindingOperations.GetMultiBindingExpression(text, TextBlock.TextProperty)?.ParentMultiBinding.Bindings.OfType<Binding>().Any(binding => binding.Path?.Path == "Presentation.C3DHeightColorRangeSummary") == true),
                "display settings retain the range editor");
            mainPresentationBar.UpdateLayout();
            Check(
                "R19 Viewer presents direct 3D, Height Image, and combined modes before source pin details",
                string.Equals(threeDViewerMode.Content as string, "3D", StringComparison.Ordinal)
                && string.Equals(heightOnlyViewerMode.Content as string, "XY", StringComparison.Ordinal)
                && BindingOperations.GetBindingExpression(heightOnlyViewerMode, ToggleButton.IsCheckedProperty)?.ParentBinding.Path.Path == "IsHeightImageOnlyViewerLayout"
                && HasVisualText(threeDHeightViewerMode, ThreeDLocalization.Shared.Resolve("ThreeD.Viewer.SplitLayout", "분할", "Split"))
                && heightImageModeLabel.Text == ThreeDLocalization.Shared.Resolve("ThreeD.Viewer.SplitLayout", "분할", "Split")
                && IsVisualDescendant(mainSlot, mainViewerPin),
                $"single={threeDViewerMode.Content};heightBinding={BindingOperations.GetBindingExpression(heightOnlyViewerMode, ContentControl.ContentProperty)?.ParentBinding.Path.Path};pinInCanvas={IsVisualDescendant(mainSlot, mainViewerPin)}");
            Check(
                "normal Viewer headers select real content without A/B comparison badges",
                layoutWorkspaceView.FindName("AuxiliarySlotBadge") is null
                && BindingOperations.GetBindingExpression(mainViewerPin, Selector.SelectedValueProperty)?.ParentBinding.Path.Path == "MainViewerContentId"
                && BindingOperations.GetBindingExpression(auxiliaryViewerPin, Selector.SelectedValueProperty)?.ParentBinding.Path.Path == "AuxiliaryViewerContentId"
                && mainViewerPin.TranslatePoint(new Point(), mainSlot).X < 30,
                $"mainPinX={mainViewerPin.TranslatePoint(new Point(), mainSlot).X:F0};mainBinding={BindingOperations.GetBindingExpression(mainViewerPin, Selector.SelectedValueProperty)?.ParentBinding.Path.Path};auxBinding={BindingOperations.GetBindingExpression(auxiliaryViewerPin, Selector.SelectedValueProperty)?.ParentBinding.Path.Path}");
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
                && !mainPresentationDisplayOptions.IsExpanded
                && !viewerWorkspaceOptionsRoot.IsSubmenuOpen
                && viewerWorkspaceOptionsMenu.IsVisible,
                $"geometry={mainViewerDisplayOptions.IsSubmenuOpen};workspaceMenuOpen={viewerWorkspaceOptionsRoot.IsSubmenuOpen};slot={mainPresentationDisplayOptions.IsExpanded}");
            Check(
                "Viewer basic controls are named and supplemental controls belong to their slot",
                IsVisualDescendant(mainSlot, mainPresentationBar)
                && !IsVisualDescendant((WrapPanel)layoutWorkspaceView.FindName("ViewerCommandRows"), mainPresentationBar)
                && ((Button)mainViewer.FindName("ViewerFitAllButton")).Content is string fitCaption && fitCaption.Length > 0
                && ((Button)mainViewer.FindName("ViewerFitRoiButton")).Command is not null
                && ((MenuItem)mainViewer.FindName("ViewerViewMenuRoot")).Template is not null
                && viewerWorkspaceOptionsRoot.Template is not null
                && viewerWorkspaceOptionsRoot.Items.OfType<MenuItem>().Any(item => item.Name == "ViewerInspectionOutputCompare"),
                "fit/ROI buttons have existing commands; comparison is a submenu; color controls live in the 3D slot");
            ((MenuItem)mainViewer.FindName("ViewerViewMenuRoot")).IsSubmenuOpen = true;
            mainViewerDisplayOptions.IsSubmenuOpen = true;
            mainPresentationDisplayOptions.IsExpanded = true;
            mainViewer.UpdateLayout();
            mainPresentationBar.UpdateLayout();
            layoutWorkspaceView.UpdateLayout();
            Check(
                "R19 shared Viewer menu owns layout and camera link once; display options stay with each Viewer",
                mainPresentationDisplayOptions.IsExpanded
                && viewerWorkspaceOptionsMenu.Items.Count == 1
                && viewerWorkspaceOptionsRoot.Items.Count == 6
                && mainPresentationDisplayOptions.Content is WrapPanel advancedOptions
                && !advancedOptions.Children.Contains(viewerWorkspaceOptionsMenu)
                && advancedOptions.Children.Contains(viewerDisplaySettingsButton)
                && mainPresentationBar.FindName("ViewerWorkspaceOptionsMenu") is null
                && auxiliaryPresentationBar.FindName("ViewerWorkspaceOptionsMenu") is null
                && viewerWorkspaceOptionsMenu.Visibility == Visibility.Visible
                && BindingOperations.GetBindingExpression(splitViewerMenuItem, MenuItem.CommandProperty)?.ParentBinding.Path.Path == "SplitViewerHorizontallyCommand"
                && BindingOperations.GetBindingExpression(popOutViewerMenuItem, MenuItem.CommandProperty)?.ParentBinding.Path.Path == "PopOutViewerCommand"
                && BindingOperations.GetBindingExpression(cameraLinkMenuItem, MenuItem.CommandProperty)?.ParentBinding.Path.Path == "ToggleViewerCameraLinkCommand"
                && viewerDisplaySettingsButton.IsVisible == mainPresentationBar.Presentation!.HasC3DHeightColorRange,
                $"layoutMenu={viewerWorkspaceOptionsMenu.Visibility};workspaceMenu={viewerWorkspaceOptionsMenu.IsVisible}/{viewerWorkspaceOptionsRoot.IsSubmenuOpen};slot={mainPresentationDisplayOptions.IsExpanded};bar={mainPresentationBar.IsVisible}/{mainPresentationBar.ActualWidth:F0}x{mainPresentationBar.ActualHeight:F0};range={viewerDisplaySettingsButton.IsVisible}/{mainPresentationBar.Presentation!.HasC3DHeightColorRange};cameraLink={cameraLinkMenuItem.Command}");
            using var heightImageViewModel = new HeightImageViewerViewModel(
                ThreeDLocalization.Shared,
                new SharedHeightCursorSession());
            var embeddedHeightImageView = new HeightImageViewerView
            {
                DataContext = heightImageViewModel,
                UseEmbeddedChrome = true,
            };
            embeddedHeightImageView.Measure(new Size(400, 360));
            embeddedHeightImageView.Arrange(new Rect(0, 0, 400, 360));
            embeddedHeightImageView.UpdateLayout();
            var heightImageChrome = (Border)embeddedHeightImageView.FindName("HeightImageChrome");
            var heightImageStatus = (TextBlock)embeddedHeightImageView.FindName("HeightImageStatus");
            var heightImageManualControls = (StackPanel)embeddedHeightImageView.FindName("HeightImageManualDisplayControls");
            var heightImageRangeSummary = (StackPanel)embeddedHeightImageView.FindName("HeightImageRangeSummary");
            var heightImageRoiHint = (TextBlock)embeddedHeightImageView.FindName("HeightImageRoiInteractionHint");
            var heightImagePalette = (ComboBox)embeddedHeightImageView.FindName("PaletteSelector");
            var heightImageInvalidOverlay = (StackPanel)embeddedHeightImageView.FindName("HeightImageInvalidOverlayControls");
            var heightImageDisplaySettings = (Expander)embeddedHeightImageView.FindName("HeightImageDisplaySettings");
            var heightImageDisplaySettingsToggle = (ToggleButton)embeddedHeightImageView.FindName("HeightImageDisplaySettingsToggle");
            var heightImageScroller = (ScrollViewer)embeddedHeightImageView.FindName("ImageScrollViewer");
            Check(
                "embedded Height Image starts with display settings closed and a dominant canvas",
                heightImageStatus.Visibility == Visibility.Collapsed
                && heightImageManualControls.Visibility == Visibility.Collapsed
                && heightImageRangeSummary.Visibility == Visibility.Collapsed
                && heightImageRoiHint.Visibility == Visibility.Collapsed
                && !heightImageDisplaySettings.IsExpanded
                && heightImageDisplaySettings.Visibility == Visibility.Collapsed
                && heightImageDisplaySettingsToggle.IsChecked == false
                && heightImagePalette.ActualHeight == 0
                && heightImageInvalidOverlay.ActualHeight == 0
                && heightImageChrome.ActualHeight <= 85
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
                && heightImageScroller.ActualHeight >= 250
                && heightImageDisplaySettings.Content is WrapPanel
                && heightImagePalette.SelectedItem is HeightImagePaletteChoice
                && BindingOperations.GetBindingExpression(heightImagePalette, Selector.SelectedItemProperty)?.ParentBinding.Path.Path == "SelectedPaletteChoice",
                $"settings={heightImageDisplaySettings.IsExpanded};content={heightImageDisplaySettings.Content?.GetType().Name};selected={heightImagePalette.SelectedItem}");

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
            Check(
                "height palette keeps the R19 3D surface free of a distribution card without running inspection",
                heightPaletteSelected
                && heightDistributionPanel is null
                && mainViewer.HostState.Presentation.C3DHeightDistributionVisible
                && mainViewer.HostState.Inspection.PublishedResultCount == 0,
                $"heightPalette={heightPaletteSelected};selected={mainViewer.DisplayEditor.SelectedColorMap};overlay={heightDistributionPanel is not null};source={mainViewer.CurrentC3DSourcePath};published={mainViewer.HostState.Inspection.PublishedResultCount}");
            mainViewerDisplayOptions.IsSubmenuOpen = false;
            ((MenuItem)mainViewer.FindName("ViewerViewMenuRoot")).IsSubmenuOpen = false;
            mainPresentationDisplayOptions.IsExpanded = false;
            layoutTestWindow.Dispatcher.Invoke(DispatcherPriority.Render, new Action(() =>
            {
                mainViewerDisplayOptions.UpdateLayout();
                mainPresentationBar.UpdateLayout();
                layoutWorkspaceView.UpdateLayout();
            }));
            var mainPresentationOptionsContent = (FrameworkElement)mainPresentationDisplayOptions.Content;
            foreach (var hostWidth in new[] { 856d, 1140d })
            {
                layoutTestWindow.Width = hostWidth;
                layoutTestWindow.Dispatcher.Invoke(DispatcherPriority.Render, new Action(() => layoutTestWindow.UpdateLayout()));
                var controlsEnd = mainPresentationBar.TranslatePoint(new Point(mainPresentationBar.ActualWidth, mainPresentationBar.ActualHeight), layoutWorkspaceView);
                var commandRows = (WrapPanel)layoutWorkspaceView.FindName("ViewerCommandRows");
                var commandRowDetails = string.Join(";", commandRows.Children.OfType<FrameworkElement>().Select(child =>
                    $"{child.Name}={child.ActualWidth:F0}x{child.ActualHeight:F0}@{child.TranslatePoint(new Point(0, 0), commandRows)}"));
                Check($"Loaded Viewer command rows remain reachable at {hostWidth:0}px",
                    controlsEnd.X <= layoutWorkspaceView.ActualWidth + 1
                    && IsVisualDescendant(mainSlot, mainPresentationBar)
                    && controlsEnd.Y <= mainSlot.TranslatePoint(new Point(0, 80), layoutWorkspaceView).Y
                    && commandRows.ActualHeight <= 104
                    && !mainPresentationDisplayOptions.IsExpanded
                    && !viewerWorkspaceOptionsRoot.IsSubmenuOpen
                    && viewerDisplaySettingsButton.Visibility == Visibility.Visible
                    && !viewerDisplaySettingsButton.IsVisible
                    && mainViewer.HostState.Inspection.PublishedResultCount == 0,
                    $"end={controlsEnd};rows={commandRows.ActualHeight};width={layoutWorkspaceView.ActualWidth};rangeButton={viewerDisplaySettingsButton.Visibility}/{viewerDisplaySettingsButton.IsVisible};options={mainPresentationDisplayOptions.IsExpanded};content={mainPresentationOptionsContent.Visibility}/{mainPresentationOptionsContent.IsVisible}/{mainPresentationOptionsContent.ActualHeight:F0};children={commandRowDetails}");
            }
            var rangeEditor = mainPresentationBar.Presentation!;
            var rangeBeforeEdit = mainViewer.HostState.Presentation;
            mainPresentationDisplayOptions.IsExpanded = true;
            mainPresentationBar.UpdateLayout();
            layoutTestWindow.Dispatcher.Invoke(DispatcherPriority.Render, new Action(() => layoutTestWindow.UpdateLayout()));
            Check(
                "loaded height range controls remain available inside expanded Viewer options",
                mainPresentationDisplayOptions.IsExpanded
                && mainPresentationDisplayOptions.Content is WrapPanel loadedAdvancedOptions
                && loadedAdvancedOptions.Children.Contains(viewerDisplaySettingsButton)
                && viewerDisplaySettingsButton.Visibility == Visibility.Visible
                && viewerDisplaySettingsButton.Command?.CanExecute(null) == true,
                $"options={mainPresentationDisplayOptions.IsExpanded};rangeButton={viewerDisplaySettingsButton.Visibility};canExecute={viewerDisplaySettingsButton.Command?.CanExecute(null)}");
            viewerDisplaySettingsButton.Command!.Execute(null);
            mainPresentationBar.UpdateLayout();
            layoutTestWindow.Dispatcher.Invoke(DispatcherPriority.Render, new Action(() => layoutTestWindow.UpdateLayout()));
            var popupChild = viewerDisplaySettingsPopup.Child;
            Check(
                "settings popup opens from the live workspace host",
                viewerDisplaySettingsPopup.IsOpen && popupChild is not null && ReferenceEquals(viewerDisplaySettingsPopup.PlacementTarget, layoutTestWindow),
                $"vmOpen={rangeEditor.IsDisplaySettingsOpen};popupOpen={viewerDisplaySettingsPopup.IsOpen};child={popupChild?.GetType().Name ?? "(none)"};target={viewerDisplaySettingsPopup.PlacementTarget?.GetType().Name ?? "(none)"}");
            var viewerDisplaySettingsDialog = OpenVisionLab.ThreeD.Shell.Verification.Smoke.ShellSmokeArtifacts.FindVisualDescendants<Border>(popupChild!)
                .Single(border => AutomationProperties.GetAutomationId(border) == "ViewerDisplaySettingsDialog");
            var rangeModeSelector = OpenVisionLab.ThreeD.Shell.Verification.Smoke.ShellSmokeArtifacts.FindVisualDescendants<ComboBox>(popupChild!)
                .Single(comboBox => AutomationProperties.GetAutomationId(comboBox) == "ViewerHeightRangeMode");
            var minimumInput = OpenVisionLab.ThreeD.Shell.Verification.Smoke.ShellSmokeArtifacts.FindVisualDescendants<TextBox>(popupChild!)
                .Single(textBox => AutomationProperties.GetAutomationId(textBox) == "ViewerHeightRangeMinimumInput");
            var maximumInput = OpenVisionLab.ThreeD.Shell.Verification.Smoke.ShellSmokeArtifacts.FindVisualDescendants<TextBox>(popupChild!)
                .Single(textBox => AutomationProperties.GetAutomationId(textBox) == "ViewerHeightRangeMaximumInput");
            var emphasisSelector = OpenVisionLab.ThreeD.Shell.Verification.Smoke.ShellSmokeArtifacts.FindVisualDescendants<ComboBox>(popupChild!)
                .Single(comboBox => AutomationProperties.GetAutomationId(comboBox) == "ViewerHeightEmphasis");
            var displaySettingsOverlay = (Grid)popupChild!;
            Check(
                "settings opens centered over the owning product window and keeps automatic values read-only",
                rangeEditor.IsDisplaySettingsOpen
                && BindingOperations.GetBindingExpression(viewerDisplaySettingsPopup, Popup.IsOpenProperty)?.ParentBinding.Path.Path == "IsDisplaySettingsOpen"
                && BindingOperations.GetBindingExpression(viewerDisplaySettingsPopup, Popup.IsOpenProperty)?.ParentBinding.Mode == BindingMode.TwoWay
                && viewerDisplaySettingsPopup.Placement == PlacementMode.Center
                && ReferenceEquals(viewerDisplaySettingsPopup.PlacementTarget, layoutTestWindow)
                && Math.Abs(displaySettingsOverlay.ActualWidth - layoutTestWindow.ActualWidth) < 1
                && Math.Abs(displaySettingsOverlay.ActualHeight - layoutTestWindow.ActualHeight) < 1
                && Math.Abs(viewerDisplaySettingsDialog.ActualWidth - 540) < 1
                && rangeModeSelector.SelectedValue as string == "auto"
                && emphasisSelector.SelectedValue as string == "1"
                && !minimumInput.IsEnabled
                && !maximumInput.IsEnabled
                && minimumInput.Text == rangeBeforeEdit.C3DHeightColorMinimumRaw.ToString("G", CultureInfo.InvariantCulture)
                && maximumInput.Text == rangeBeforeEdit.C3DHeightColorMaximumRaw.ToString("G", CultureInfo.InvariantCulture)
                && mainViewer.HostState.Inspection.PublishedResultCount == 0,
                $"emphasis={emphasisSelector.SelectedValue};min={minimumInput.Text}/{rangeBeforeEdit.C3DHeightColorMinimumRaw.ToString("G", CultureInfo.InvariantCulture)};max={maximumInput.Text}/{rangeBeforeEdit.C3DHeightColorMaximumRaw.ToString("G", CultureInfo.InvariantCulture)};placement={viewerDisplaySettingsPopup.Placement};target={viewerDisplaySettingsPopup.PlacementTarget?.GetType().Name};sameOwner={ReferenceEquals(viewerDisplaySettingsPopup.PlacementTarget, layoutTestWindow)};overlay={displaySettingsOverlay.ActualWidth}x{displaySettingsOverlay.ActualHeight};window={layoutTestWindow.ActualWidth}x{layoutTestWindow.ActualHeight};dialog={viewerDisplaySettingsDialog.ActualWidth};vmOpen={rangeEditor.IsDisplaySettingsOpen};popupOpen={viewerDisplaySettingsPopup.IsOpen};popupBinding={BindingOperations.GetBindingExpression(viewerDisplaySettingsPopup, Popup.IsOpenProperty)?.ParentBinding.Path.Path}/{BindingOperations.GetBindingExpression(viewerDisplaySettingsPopup, Popup.IsOpenProperty)?.ParentBinding.Mode};mode={rangeModeSelector.SelectedValue};minEnabled={minimumInput.IsEnabled};maxEnabled={maximumInput.IsEnabled};published={mainViewer.HostState.Inspection.PublishedResultCount}");
            emphasisSelector.SelectedValue = "3";
            rangeModeSelector.SelectedValue = "manual";
            minimumInput.Text = (rangeBeforeEdit.C3DHeightColorMinimumRaw + 1).ToString("G", CultureInfo.InvariantCulture);
            maximumInput.Text = (rangeBeforeEdit.C3DHeightColorMaximumRaw - 1).ToString("G", CultureInfo.InvariantCulture);
            rangeEditor.CancelDisplaySettingsCommand.Execute(null);
            mainPresentationBar.UpdateLayout();
            Check(
                "cancel discards draft range without changing the Viewer state",
                !rangeEditor.IsDisplaySettingsOpen
                && mainViewer.HostState.Presentation.C3DHeightColorRangeAuto == rangeBeforeEdit.C3DHeightColorRangeAuto
                && mainViewer.HostState.Presentation.C3DHeightColorMinimumRaw == rangeBeforeEdit.C3DHeightColorMinimumRaw
                && mainViewer.HostState.Presentation.C3DHeightColorMaximumRaw == rangeBeforeEdit.C3DHeightColorMaximumRaw
                && mainViewer.HostState.Presentation.C3DHeightEmphasis == rangeBeforeEdit.C3DHeightEmphasis
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
                && mainViewer.HostState.Presentation.C3DHeightEmphasis == rangeBeforeEdit.C3DHeightEmphasis
                && mainViewer.HostState.Inspection.PublishedResultCount == 0,
                $"vmOpen={rangeEditor.IsDisplaySettingsOpen};emphasisError={rangeEditor.HasDisplaySettingsEmphasisError};emphasis={mainViewer.HostState.Presentation.C3DHeightEmphasis};published={mainViewer.HostState.Inspection.PublishedResultCount}");
            rangeEditor.CancelDisplaySettingsCommand.Execute(null);

            viewerDisplaySettingsButton.Command.Execute(null);
            mainPresentationBar.UpdateLayout();
            rangeModeSelector.SelectedValue = "manual";
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
            rangeModeSelector.SelectedValue = "manual";
            var expectedMinimum = rangeBeforeEdit.C3DHeightColorMinimumRaw + (rangeBeforeEdit.C3DHeightColorMaximumRaw - rangeBeforeEdit.C3DHeightColorMinimumRaw) * 0.2;
            var expectedMaximum = rangeBeforeEdit.C3DHeightColorMaximumRaw - (rangeBeforeEdit.C3DHeightColorMaximumRaw - rangeBeforeEdit.C3DHeightColorMinimumRaw) * 0.2;
            minimumInput.Text = expectedMinimum.ToString("G", CultureInfo.InvariantCulture);
            maximumInput.Text = expectedMaximum.ToString("G", CultureInfo.InvariantCulture);
            emphasisSelector.SelectedValue = "3";
            rangeEditor.ApplyDisplaySettingsCommand.Execute(null);
            mainPresentationBar.UpdateLayout();
            Check(
                "valid manual range applies through the existing Viewer host without running inspection",
                !rangeEditor.IsDisplaySettingsOpen
                && !mainViewer.HostState.Presentation.C3DHeightColorRangeAuto
                && Math.Abs(mainViewer.HostState.Presentation.C3DHeightColorMinimumRaw - expectedMinimum) < 1e-9
                && Math.Abs(mainViewer.HostState.Presentation.C3DHeightColorMaximumRaw - expectedMaximum) < 1e-9
                && mainViewer.HostState.Presentation.C3DHeightEmphasis == 3
                && mainViewer.HostState.Inspection.PublishedResultCount == 0,
                $"vmOpen={rangeEditor.IsDisplaySettingsOpen};popupOpen={viewerDisplaySettingsPopup.IsOpen};range={mainViewer.HostState.Presentation.C3DHeightColorMinimumRaw:G}..{mainViewer.HostState.Presentation.C3DHeightColorMaximumRaw:G};published={mainViewer.HostState.Inspection.PublishedResultCount}");

            viewerDisplaySettingsButton.Command.Execute(null);
            mainPresentationBar.UpdateLayout();
            rangeModeSelector.SelectedValue = "auto";
            rangeEditor.ApplyDisplaySettingsCommand.Execute(null);
            mainPresentationBar.UpdateLayout();
            Check(
                "automatic mode restores source range without inspection",
                !rangeEditor.IsDisplaySettingsOpen
                && mainViewer.HostState.Presentation.C3DHeightColorRangeAuto
                && mainViewer.HostState.Inspection.PublishedResultCount == 0,
                $"vmOpen={rangeEditor.IsDisplaySettingsOpen};popupOpen={viewerDisplaySettingsPopup.IsOpen};auto={mainViewer.HostState.Presentation.C3DHeightColorRangeAuto};published={mainViewer.HostState.Inspection.PublishedResultCount}");

            viewerDisplaySettingsButton.Command.Execute(null);
            mainPresentationBar.UpdateLayout();
            rangeModeSelector.SelectedValue = "auto";
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

            layoutTestWindow.Close();
            summary = $"Viewer workspace presentation verification: Pass ({passed} checks)";
            lines.Add(summary);
            WriteReport(reportPath, lines);
            return true;
        }
        catch (Exception exception)
        {
            layoutTestWindow?.Close();
            summary = $"Viewer workspace presentation verification: Fail after {passed} checks: {exception.Message}";
            lines.Add(summary);
            WriteReport(reportPath, lines);
            return false;
        }
    }

    private static void WriteReport(string reportPath, IEnumerable<string> lines)
    {
        var fullPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllLines(fullPath, lines);
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
