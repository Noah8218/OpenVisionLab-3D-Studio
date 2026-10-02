using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Linq;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Docking.Controls;
using OpenVisionLab.ThreeD.Shell.Layout;
using OpenVisionLab.ThreeD.Shell.Verification.Smoke;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Shell.Views.Shell;
using OpenVisionLab.ThreeD.Shell.Views.Workbench;
using OpenVisionLab.ThreeD.Viewer;
using OpenVisionLab.ThreeD.Viewer.Rendering;

namespace OpenVisionLab.ThreeD.Shell;

internal static class ToolWorkbenchDockingVerification
{
    private static readonly string[] WorkbenchContentIds =
    [
        "tool-library",
        "data-layers",
        "tool-inspector",
        "three-d-viewer",
        "evidence-workbench",
        "output-compare",
        "displayed-outputs",
        "linked-view",
        "height-profile",
        "fit-diagnostics",
        "intersection-evidence",
        "correspondence-evidence",
    ];

    private static readonly string[] CalibrationContentIds =
    [
        "calibration-explorer",
        "calibration-workspace",
        "calibration-inspector",
        "calibration-evidence",
    ];

    private const string ThicknessRecipeRelativePath =
        "3D/Samples/ThicknessCouponV1/inspection-recipe.ov3d-recipe.json";

    private static string GetReportDirectory(string reportPath) =>
        Path.GetDirectoryName(Path.GetFullPath(reportPath))!;

    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);

        var lines = new List<string>
        {
            "OpenVisionLab 3D docking workspace verification",
            $"Generated: {DateTimeOffset.Now:O}",
        };
        var passed = 0;
        var total = 0;

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
            Check(
                "verification runs on STA",
                Thread.CurrentThread.GetApartmentState() == ApartmentState.STA,
                Thread.CurrentThread.GetApartmentState().ToString());

            using var firstCallbackViewer = new OpenVisionThreeDViewerControl(loadDefaultSamples: false);
            using var replacementCallbackViewer = new OpenVisionThreeDViewerControl(loadDefaultSamples: false);
            using var disposedCallbackWorkbench = new ToolRecipeWorkbenchView
            {
                ViewerContent = firstCallbackViewer,
            };
            var profileViewerDataContextBeforeDispose =
                disposedCallbackWorkbench.ProfileViewerDataContext;
            var profileViewerHostBeforeDispose =
                disposedCallbackWorkbench.ProfileViewerHost;
            disposedCallbackWorkbench.Dispose();
            disposedCallbackWorkbench.ViewerContent = replacementCallbackViewer;
            Check(
                "disposed Workbench releases profile adapter and ignores late ViewerContent callback",
                profileViewerDataContextBeforeDispose is HeightProfileViewModel
                && ReferenceEquals(profileViewerHostBeforeDispose, firstCallbackViewer)
                && disposedCallbackWorkbench.ProfileViewerHost is null
                && disposedCallbackWorkbench.ProfileViewerDataContext is null,
                $"beforeAdapter={profileViewerDataContextBeforeDispose is HeightProfileViewModel};beforeHost={profileViewerHostBeforeDispose is not null};afterHost={disposedCallbackWorkbench.ProfileViewerHost is not null};after={disposedCallbackWorkbench.ProfileViewerDataContext is not null}");

            var dataContextOwner = new object();
            var viewerOwner = new object();
            var workbench = new ToolRecipeWorkbenchView
            {
                DataContext = dataContextOwner,
                ViewerContent = viewerOwner,
            };
            var workbenchContracts = workbench.GetDockingPaneContracts();
            var shortcutGestures = workbench.InputBindings
                .OfType<KeyBinding>()
                .Select(binding => (binding.Key, binding.Modifiers))
                .ToHashSet();
            Check(
                "Workbench exposes the bounded inspection shortcut set",
                shortcutGestures.SetEquals(
                [
                    (Key.N, ModifierKeys.Control),
                    (Key.O, ModifierKeys.Control),
                    (Key.S, ModifierKeys.Control),
                    (Key.S, ModifierKeys.Control | ModifierKeys.Shift),
                    (Key.Escape, ModifierKeys.None),
                    (Key.Enter, ModifierKeys.None),
                    (Key.O, ModifierKeys.Control | ModifierKeys.Shift),
                    (Key.F5, ModifierKeys.None),
                    (Key.Enter, ModifierKeys.Control),
                    (Key.F5, ModifierKeys.Control),
                    (Key.F5, ModifierKeys.Control | ModifierKeys.Shift),
                ]),
                string.Join(", ", shortcutGestures.OrderBy(item => item.Key).ThenBy(item => item.Modifiers)));
            Check("Workbench exposes twelve dock panes", workbenchContracts.Count == 12, Describe(workbenchContracts));
            Check(
                "Workbench separates Tool Library and Recipe Flow without hosting Recipe Center",
                HasExactIds(workbenchContracts, WorkbenchContentIds)
                && workbenchContracts[0].ContentId == "tool-library"
                && workbenchContracts[1].ContentId == "data-layers"
                && workbenchContracts[0].HasContent
                && workbenchContracts[1].HasContent,
                Describe(workbenchContracts));
            Check(
                "Workbench model preserves Recipe Flow, Selected Tool, then Viewer order",
                workbench.HasRecipeFlowInspectorViewerOrder
                && workbenchContracts[1].ContentId == "data-layers"
                && workbenchContracts[2].ContentId == "tool-inspector"
                && workbenchContracts[3].ContentId == "three-d-viewer",
                Describe(workbenchContracts));
            Check(
                "Workbench default uses Recipe Chain and the single Selected Tool workspace",
                workbench.UsesInspectionWorkspaceV3Composition,
                $"v3Composition={workbench.UsesInspectionWorkspaceV3Composition}");
            Check(
                "Recipe Flow exposes the themed health summary and requirement navigation",
                workbench.HasRecipeHealthNavigation,
                $"recipeHealthNavigation={workbench.HasRecipeHealthNavigation}");
            Check(
                "Empty Viewer keeps one primary input action without a duplicate context ribbon",
                workbench.IsViewerInputFirstActionVisible
                && !workbench.IsViewerContextRibbonVisible
                && !workbench.IsNoRecipeStepBannerVisible,
                $"inputAction={workbench.IsViewerInputFirstActionVisible}; contextRibbon={workbench.IsViewerContextRibbonVisible}; noStepBanner={workbench.IsNoRecipeStepBannerVisible}");
            Check(
                "Selected Tool workspace owns bounded Thickness repeat-grid authoring controls",
                workbench.HasThicknessRepeatGridAuthoringControls,
                $"repeatGrid={workbench.HasThicknessRepeatGridAuthoringControls}");
            Check(
                "Selected Tool owns the single explicit Run and Cancel actions",
                workbench.HasExplicitSelectedToolActions,
                $"selectedToolActions={workbench.HasExplicitSelectedToolActions}");
            Check(
                "Source Quality and Selected Tool surfaces are mutually exclusive",
                workbench.HasExclusiveSelectedToolWorkspaceSurface,
                $"exclusiveWorkspace={workbench.HasExclusiveSelectedToolWorkspaceSurface}");
            Check(
                "Displayed Outputs is adjacent to Viewer",
                workbench.HasAdjacentViewerOutputs,
                $"viewerOutputs={workbench.HasAdjacentViewerOutputs}");
            Check(
                "Workbench Viewer exposes one normal layout toolbar and two reusable slot hosts",
                workbench.HasViewerWorkspaceLayoutToolbar,
                $"viewerWorkspace={workbench.HasViewerWorkspaceLayoutToolbar}");
            Check(
                "Workbench gives the default Viewer a dominant width",
                workbench.HasDominantViewerWidth,
                $"dominantViewer={workbench.HasDominantViewerWidth}");
            Check(
                "Workbench shows the R19 recipe and input context row",
                workbench.HasVisibleWorkspaceCommandBar,
                $"commandBarVisible={workbench.HasVisibleWorkspaceCommandBar}");
            Check(
                "Workbench dock tabs use the top OpenVision themed strip",
                workbench.HasTopThemedDockTabs,
                $"topThemedTabs={workbench.HasTopThemedDockTabs}");
            Check(
                "Task panes expose side collapse while Viewer remains fixed",
                workbench.HasSideCollapsibleTaskPanes,
                $"sideCollapsible={workbench.HasSideCollapsibleTaskPanes}");
            var autoHideRoundTrip = workbench.VerifySupportAutoHideRoundTrip();
            Check(
                "Support pane side collapse restores without changing composition",
                autoHideRoundTrip.Collapsed
                && autoHideRoundTrip.Restored
                && workbench.HasRecipeFlowInspectorViewerOrder,
                $"collapsed={autoHideRoundTrip.Collapsed}; restored={autoHideRoundTrip.Restored}; composition={workbench.HasRecipeFlowInspectorViewerOrder}");

            var navigationViewModel = new ShellMainWindowViewModel();
            var navigationRail = new StudioNavigationRailView
            {
                DataContext = navigationViewModel,
            };
            navigationRail.ApplyResponsiveWidthForVerification(1920);
            var wideRailWidth = navigationRail.Width;
            navigationRail.ApplyResponsiveWidthForVerification(1280);
            var titleBarForLanguage = new StudioTitleBarView
            {
                DataContext = navigationViewModel,
            };
            ArrangeForTextVerification(titleBarForLanguage, 1280);
            var languageSelector = titleBarForLanguage.LanguageSelector;
            var windowControlGroup = (StackPanel)titleBarForLanguage.FindName("WindowControlGroup");
            var minimizeWindowButton = (Button)titleBarForLanguage.FindName("MinimizeWindowButton");
            Check(
                "Workbench v4 rail exposes accessible responsibility and utility routes",
                navigationRail.HasAccessibleResponsibilityRoutes
                && navigationRail.HasAccessibleUtilityRoutes,
                $"responsibilities={navigationRail.HasAccessibleResponsibilityRoutes}; utilities={navigationRail.HasAccessibleUtilityRoutes}");
            var expectedToolLabIds = new[]
            {
                "filter",
                "height-difference-edge",
                "two-point-line",
                "three-point-plane",
                "datum-plane-raw-height-deviation",
                "line-intersection",
                "landmark-correspondence",
                "xyz-affine-solve",
                "xyz-affine-apply",
                "re-grid-height-map",
            };
            var requestedToolLabIds = new List<string>();
            EventHandler<StudioToolLabRequestEventArgs> toolLabRequestHandler = (_, args) =>
                requestedToolLabIds.Add(args.ToolId);
            navigationRail.ToolLabRequested += toolLabRequestHandler;
            var toolLabMenuItems = navigationRail.ToolLabsMenuButton.ContextMenu?.Items
                .OfType<MenuItem>()
                .ToArray()
                ?? [];
            foreach (var toolLabMenuItem in toolLabMenuItems)
            {
                toolLabMenuItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, toolLabMenuItem));
            }
            navigationRail.ToolLabRequested -= toolLabRequestHandler;
            Check(
                "Studio navigation rail emits one ordered typed Tool Lab request per menu item",
                expectedToolLabIds.SequenceEqual(toolLabMenuItems.Select(item => item.Tag as string))
                && expectedToolLabIds.SequenceEqual(requestedToolLabIds),
                $"menuIds=[{string.Join(",", toolLabMenuItems.Select(item => item.Tag))}]; requests=[{string.Join(",", requestedToolLabIds)}]");
            Check(
                "Studio language selector retains the shared themed ComboBox base style",
                languageSelector.Style.BasedOn is not null
                && languageSelector.MinHeight >= 30
                && languageSelector.VerticalContentAlignment == VerticalAlignment.Center,
                $"hasThemedBase={languageSelector.Style.BasedOn is not null}; minHeight={languageSelector.MinHeight:F0}; vertical={languageSelector.VerticalContentAlignment}");
            Check(
                "Title-bar language selector sits before the window controls at full-label width",
                Math.Abs(languageSelector.Width - 112) < 0.1
                && ReferenceEquals(VisualTreeHelper.GetParent(languageSelector), windowControlGroup)
                && windowControlGroup.Children.IndexOf(languageSelector) < windowControlGroup.Children.IndexOf(minimizeWindowButton)
                && AutomationProperties.GetName(languageSelector).Length > 0,
                $"width={languageSelector.Width:F0};parent={VisualTreeHelper.GetParent(languageSelector)?.GetType().Name};languageIndex={windowControlGroup.Children.IndexOf(languageSelector)};minimizeIndex={windowControlGroup.Children.IndexOf(minimizeWindowButton)};name={AutomationProperties.GetName(languageSelector)}");
            languageSelector.IsEnabled = false;
            Check(
                "Studio language selector resolves semantic disabled colors",
                Equals(
                    languageSelector.Background,
                    titleBarForLanguage.FindResource("ThreeD.DisabledSurfaceBrush"))
                && Equals(
                    languageSelector.Foreground,
                    titleBarForLanguage.FindResource("ThreeD.DisabledBrush")),
                $"background={languageSelector.Background}; foreground={languageSelector.Foreground}");
            languageSelector.IsEnabled = true;
            var originalLanguage = OpenVisionLanguageService.CurrentLanguage;
            var englishLanguageOption = navigationViewModel.LanguageOptions.Single(
                option => option.Language == OpenVisionLanguage.English);
            if (originalLanguage == OpenVisionLanguage.English)
            {
                navigationViewModel.SetSelectedLanguageForVerification(
                    navigationViewModel.LanguageOptions.Single(
                        option => option.Language == OpenVisionLanguage.Korean));
            }
            var originalLanguageOption = navigationViewModel.LanguageOptions.Single(
                option => option.Language == originalLanguage);
            var paletteBeforeLanguageChange = navigationViewModel.Workbench.HeightImageViewer.SelectedPalette;
            var auxiliaryContentBeforeLanguageChange = navigationViewModel.Workbench.ViewerWorkspace.AuxiliaryContentId;
            var localizationNotificationCount = 0;
            string? localizationPropertyName = null;
            var workbenchNotifications = new List<string?>();
            PropertyChangedEventHandler localizationHandler = (_, args) =>
            {
                localizationNotificationCount++;
                localizationPropertyName = args.PropertyName;
            };
            PropertyChangedEventHandler workbenchHandler = (_, args) => workbenchNotifications.Add(args.PropertyName);
            ThreeDLocalization.Shared.PropertyChanged += localizationHandler;
            navigationViewModel.Workbench.PropertyChanged += workbenchHandler;
            languageSelector.SelectedItem = englishLanguageOption;
            languageSelector.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
            navigationViewModel.Workbench.PropertyChanged -= workbenchHandler;
            ThreeDLocalization.Shared.PropertyChanged -= localizationHandler;
            Check(
                "Language switch emits one all-properties localization notification",
                localizationNotificationCount == 1
                && string.IsNullOrEmpty(localizationPropertyName),
                $"notifications={localizationNotificationCount}; property='{localizationPropertyName}'");
            Check(
                "Language switch remains bounded and reports completion time",
                navigationViewModel.LastLanguageChangeMilliseconds < 500
                && navigationViewModel.StatusText.Contains("ms", StringComparison.Ordinal),
                $"elapsed={navigationViewModel.LastLanguageChangeMilliseconds:F2} ms; status='{navigationViewModel.StatusText}'");
            Check(
                "Language switch preserves ComboBox selections and visible labels",
                navigationViewModel.Workbench.HeightImageViewer.SelectedPalette == paletteBeforeLanguageChange
                && navigationViewModel.Workbench.HeightImageViewer.PaletteChoices.All(
                    choice => !string.IsNullOrWhiteSpace(choice.DisplayName))
                && string.Equals(
                    navigationViewModel.Workbench.ViewerWorkspace.AuxiliaryContentId,
                    auxiliaryContentBeforeLanguageChange,
                    StringComparison.Ordinal)
                && navigationViewModel.Workbench.ViewerWorkspaceCandidates.All(
                    candidate => !string.IsNullOrWhiteSpace(candidate.DisplayName)),
                $"palette={navigationViewModel.Workbench.HeightImageViewer.SelectedPalette}; paletteLabels={navigationViewModel.Workbench.HeightImageViewer.PaletteChoices.Count}; auxiliary='{navigationViewModel.Workbench.ViewerWorkspace.AuxiliaryContentId}'; candidates={navigationViewModel.Workbench.ViewerWorkspaceCandidates.Count}");
            Check(
                "Language switch restores Viewer selections after localized candidate lists",
                workbenchNotifications.IndexOf(nameof(navigationViewModel.Workbench.MainViewerCandidates)) is var mainCandidatesIndex
                && mainCandidatesIndex >= 0
                && workbenchNotifications.IndexOf(nameof(navigationViewModel.Workbench.MainViewerContentId)) > mainCandidatesIndex
                && workbenchNotifications.IndexOf(nameof(navigationViewModel.Workbench.ViewerWorkspaceCandidates)) is var viewerCandidatesIndex
                && viewerCandidatesIndex >= 0
                && workbenchNotifications.IndexOf(nameof(navigationViewModel.Workbench.AuxiliaryViewerContentId)) > viewerCandidatesIndex
                && new ViewerWorkspaceCandidateItem(
                    ViewerWorkspaceViewModel.HeightImageViewerContentId,
                    "높이 이미지",
                    ViewerWorkspaceCandidateKind.HeightImage,
                    "source.c3d",
                    "HeightField / native grid",
                    "Ready",
                    true)
                == new ViewerWorkspaceCandidateItem(
                    ViewerWorkspaceViewModel.HeightImageViewerContentId,
                    "Height Image",
                    ViewerWorkspaceCandidateKind.HeightImage,
                    "source.c3d",
                    "HeightField / native grid",
                    "Ready",
                    true),
                $"notifications={string.Join(",", workbenchNotifications)}");
            ArrangeForTextVerification(titleBarForLanguage, 1280);
            var selectedLanguageText = FindVisualDescendants<TextBlock>(languageSelector)
                .FirstOrDefault(text => ReferenceEquals(text.DataContext, englishLanguageOption));
            var popupLanguageText = languageSelector.ItemTemplate.LoadContent() as TextBlock;
            if (popupLanguageText is not null)
            {
                popupLanguageText.DataContext = englishLanguageOption;
                popupLanguageText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                popupLanguageText.Arrange(new Rect(popupLanguageText.DesiredSize));
                popupLanguageText.UpdateLayout();
            }
            Check(
                "Title-bar language selector keeps full language names readable",
                selectedLanguageText is { Text: "English", Clip: null, ActualHeight: >= 12 }
                && popupLanguageText is { Text: "English", Clip: null, ActualHeight: >= 12 }
                && selectedLanguageText.ActualWidth + 1 >= popupLanguageText.ActualWidth,
                $"selected='{selectedLanguageText?.Text}' {selectedLanguageText?.ActualWidth:0.#}x{selectedLanguageText?.ActualHeight:0.#}; popup='{popupLanguageText?.Text}' {popupLanguageText?.ActualWidth:0.#}x{popupLanguageText?.ActualHeight:0.#}");
            navigationRail.ApplyResponsiveWidthForVerification(1920);
            ArrangeForTextVerification(navigationRail, 108);
            var wideTeachingLabel = FindVisualDescendants<TextBlock>(navigationRail.AuthoringModeButton)
                .Single(text => ReferenceEquals(text.Style, navigationRail.FindResource("RailLabelStyle")));
            var wideTeachingLabelBounds = wideTeachingLabel.TransformToAncestor(navigationRail).TransformBounds(new Rect(wideTeachingLabel.RenderSize));
            navigationRail.ApplyResponsiveWidthForVerification(1280);
            ArrangeForTextVerification(navigationRail, 86);
            var compactTeachingLabel = FindVisualDescendants<TextBlock>(navigationRail.AuthoringModeButton)
                .Single(text => ReferenceEquals(text.Style, navigationRail.FindResource("RailLabelStyle")));
            var compactTeachingLabelBounds = compactTeachingLabel.TransformToAncestor(navigationRail).TransformBounds(new Rect(compactTeachingLabel.RenderSize));
            Check(
                "R19 navigation labels remain visible in the Wide and Compact rail",
                wideTeachingLabel.Visibility == Visibility.Visible
                && wideTeachingLabel.ActualWidth > 0
                && wideTeachingLabel.ActualHeight > 0
                && wideTeachingLabel.TextWrapping == TextWrapping.Wrap
                && wideTeachingLabelBounds.Left >= 0
                && wideTeachingLabelBounds.Right <= navigationRail.Width + 1
                && compactTeachingLabel.Visibility == Visibility.Visible
                && compactTeachingLabel.ActualWidth > 0
                && compactTeachingLabel.ActualHeight > 0
                && compactTeachingLabel.TextWrapping == TextWrapping.Wrap
                && compactTeachingLabelBounds.Left >= 0
                && compactTeachingLabelBounds.Right <= navigationRail.Width + 1,
                $"wideLabel={wideTeachingLabel.Visibility}/{wideTeachingLabel.ActualWidth:0.#}x{wideTeachingLabel.ActualHeight:0.#}/{wideTeachingLabelBounds}; compactLabel={compactTeachingLabel.Visibility}/{compactTeachingLabel.ActualWidth:0.#}x{compactTeachingLabel.ActualHeight:0.#}/{compactTeachingLabelBounds}");
            languageSelector.SelectedItem = originalLanguageOption;
            languageSelector.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
            Check(
                "R19 rail follows the authored frame in Wide and Compact widths",
                Math.Abs(wideRailWidth - 108) < 0.1
                && navigationRail.IsCompact
                && Math.Abs(navigationRail.Width - 86) < 0.1,
                $"wide={wideRailWidth:F0}; compact={navigationRail.Width:F0}; isCompact={navigationRail.IsCompact}");
            var studioHeader = new StudioHeaderView { DataContext = navigationViewModel };
            ArrangeForTextVerification(studioHeader, 1280);
            var mainMenu = FindVisualDescendants<Menu>(studioHeader).Single();
            var menuBar = VisualTreeHelper.GetParent(VisualTreeHelper.GetParent(mainMenu)) as Border;
            var studioTitleBar = FindVisualDescendants<StudioTitleBarView>(studioHeader).Single();
            studioHeader.Measure(new Size(1280, double.PositiveInfinity));
            Check(
                  "R19 shell title and compact native menu match the authored 44px and 34px rows",
                  Math.Abs(studioTitleBar.ActualHeight - 44) < 0.1
                  && menuBar is not null
                  && Math.Abs(menuBar.ActualHeight - 34) < 0.1
                  && Math.Abs(studioHeader.DesiredSize.Height - 78) < 0.1,
                $"title={studioTitleBar.ActualHeight:F0}; menu={menuBar?.ActualHeight:F0}; headerDesired={studioHeader.DesiredSize.Height:F0}");
            var menuTextBrush = (SolidColorBrush)studioHeader.FindResource("ThreeD.TextBrush");
            var topLevelMenuItems = mainMenu.Items.OfType<MenuItem>().ToArray();
            var topLevelMenuLabels = topLevelMenuItems
                .SelectMany(item => FindVisualDescendants<AccessText>(item))
                .ToArray();
            var topActionButtons = new[]
            {
                (Wpf.Ui.Controls.Button)studioHeader.FindName("LoadTeachingInputHeaderButton"),
                (Wpf.Ui.Controls.Button)studioHeader.FindName("SaveTeachingRecipeHeaderButton"),
            };
            Check(
                "R19 title actions share rounded icon buttons with accessible names",
                topActionButtons.All(button => button.Icon is not null
                    && button.CornerRadius.TopLeft == 5
                    && button.MinHeight == 32
                    && !string.IsNullOrWhiteSpace(AutomationProperties.GetName(button))),
                $"actions={string.Join(';', topActionButtons.Select(button => $"{AutomationProperties.GetName(button)}:icon={button.Icon is not null}:radius={button.CornerRadius.TopLeft}:height={button.MinHeight}"))}");
            Check(
                "R19 top-level menu labels fit the compact command row and use semantic text color",
                topLevelMenuItems.Length == 6
                && topLevelMenuItems.All(item => item.ActualHeight is >= 24 and <= 34
                    && ReferenceEquals(
                        item.Template,
                        studioHeader.FindResource("R19TopMenuItemTemplate"))
                    && item.Foreground is SolidColorBrush brush
                    && brush.Color == menuTextBrush.Color)
                && topLevelMenuLabels.Length == 6
                && topLevelMenuLabels.All(label => label.ActualHeight >= 12
                    && label.ActualWidth > 0
                    && label.Clip is null),
                $"items={topLevelMenuItems.Length}; heights={string.Join(',', topLevelMenuItems.Select(item => item.ActualHeight.ToString("F0", CultureInfo.InvariantCulture)))}; foregrounds={string.Join(',', topLevelMenuItems.Select(item => item.Foreground))}; labels={topLevelMenuLabels.Length}; labelDetails={string.Join(',', topLevelMenuLabels.Select(label => $"{label.Text}:font={label.FontSize:F0}/desired={label.DesiredSize.Height:F0}/actual={label.ActualHeight:F0}/parent={VisualTreeHelper.GetParent(label)?.GetType().Name}:{(VisualTreeHelper.GetParent(label) as FrameworkElement)?.ActualHeight:F0}"))}; labelClips={string.Join(',', topLevelMenuLabels.Select(label => label.Clip is null ? "none" : "clip"))}");
            var recipeManagerView = new OpenVisionLab.ThreeD.Shell.Views.Recipe.RecipeManagerView
            {
                DataContext = navigationViewModel.Workbench,
            };
            ArrangeForTextVerification(recipeManagerView, 1520);
            var recipePreviewView = FindVisualDescendants<OpenVisionLab.ThreeD.Shell.Views.Workbench.HeightImageViewerView>(recipeManagerView).Single();
            Check(
                "R19 Recipe Center input preview binds the isolated read-only Viewer owner",
                ReferenceEquals(recipePreviewView.DataContext, navigationViewModel.Workbench.RecipePreviewViewer)
                && !ReferenceEquals(recipePreviewView.DataContext, navigationViewModel.Workbench.HeightImageViewer),
                $"preview={recipePreviewView.DataContext?.GetType().Name}; isolated={ReferenceEquals(recipePreviewView.DataContext, navigationViewModel.Workbench.RecipePreviewViewer)}");
            var recipeSections = (ListBox)recipeManagerView.FindName("RecipeBrowserSections");
            var recipeBrowserContent = (ContentControl)recipeManagerView.FindName("RecipeBrowserContent");
            var recipeSectionTemplates = new[]
            {
                (Section: "overview", Template: "RecipeOverviewSectionTemplate"),
                (Section: "input", Template: "RecipeInputSectionTemplate"),
                (Section: "tools", Template: "RecipeToolsSectionTemplate"),
                (Section: "regions", Template: "RecipeRegionsSectionTemplate"),
                (Section: "criteria", Template: "RecipeCriteriaSectionTemplate"),
            };
            foreach (var section in recipeSectionTemplates)
            {
                recipeSections.SelectedValue = section.Section;
                recipeSections.GetBindingExpression(System.Windows.Controls.Primitives.Selector.SelectedValueProperty)?.UpdateSource();
                recipeManagerView.UpdateLayout();
                Check(
                    $"R19 Recipe Center section {section.Section} selects its bound content",
                    recipeSections.SelectedValue as string == section.Section
                    && navigationViewModel.Workbench.SelectedRecipeBrowserSection == section.Section
                    && ReferenceEquals(recipeBrowserContent.ContentTemplate, recipeManagerView.FindResource(section.Template)),
                    $"selected={recipeSections.SelectedValue}; state={navigationViewModel.Workbench.SelectedRecipeBrowserSection}; template={recipeBrowserContent.ContentTemplate}");
            }
            recipeSections.SelectedValue = "overview";
            recipeSections.GetBindingExpression(System.Windows.Controls.Primitives.Selector.SelectedValueProperty)?.UpdateSource();
            recipeManagerView.UpdateLayout();
            var recipeBrowserColumns = (Grid)recipeManagerView.FindName("RecipeBrowserColumns");
            var wideRecipeColumns = recipeBrowserColumns.ColumnDefinitions.Select(column => column.ActualWidth).ToArray();
            ArrangeForTextVerification(recipeManagerView, 1244);
            var compactRecipeColumns = recipeBrowserColumns.ColumnDefinitions.Select(column => column.ActualWidth).ToArray();
            Check(
                "R19 Recipe Center columns match the responsive 320/182 and 285/155 authored widths",
                wideRecipeColumns.Length == 3
                && Math.Abs(wideRecipeColumns[0] - 320) < 3
                && Math.Abs(wideRecipeColumns[1] - 182) < 3
                && Math.Abs(compactRecipeColumns[0] - 285) < 3
                && Math.Abs(compactRecipeColumns[1] - 155) < 3,
                $"wide={string.Join(',', wideRecipeColumns.Select(width => width.ToString("F0", CultureInfo.InvariantCulture)))}; compact={string.Join(',', compactRecipeColumns.Select(width => width.ToString("F0", CultureInfo.InvariantCulture)))}");
            recipeManagerView.ApplyResponsiveWidthForVerification(1024);
            ArrangeForTextVerification(recipeManagerView, 1024);
            var compactColumns = recipeBrowserColumns.ColumnDefinitions.Select(column => column.ActualWidth).ToArray();
            var compactSectionsPanel = (Border)recipeManagerView.FindName("RecipeSectionsPanel");
            var compactDetailsPanel = (Border)recipeManagerView.FindName("RecipeDetailsPanel");
            Check(
                "R19 Recipe Center switches to the compact two-column layout with horizontal recipe sections",
                recipeManagerView.IsCompact
                && compactColumns.Length == 3
                && Math.Abs(compactColumns[0] - 280) < 2
                && compactColumns[2] < 1
                && Grid.GetRow(compactSectionsPanel) == 0
                && Grid.GetColumnSpan(compactSectionsPanel) == 2
                && Grid.GetRow(compactDetailsPanel) == 1
                && Grid.GetColumnSpan(compactDetailsPanel) == 2
                && ReferenceEquals(recipeSections.ItemsPanel, recipeManagerView.FindResource("RecipeBrowserCompactSectionsPanel")),
                $"compact={string.Join(',', compactColumns.Select(width => width.ToString("F0", CultureInfo.InvariantCulture)))}; nav={Grid.GetRow(compactSectionsPanel)}/{Grid.GetColumnSpan(compactSectionsPanel)}; details={Grid.GetRow(compactDetailsPanel)}/{Grid.GetColumnSpan(compactDetailsPanel)}; compact={recipeManagerView.IsCompact}");
            recipeManagerView.ApplyResponsiveWidthForVerification(1520);
            ArrangeForTextVerification(recipeManagerView, 1520);
            var recipeManagerWindow = new OpenVisionLab.ThreeD.Shell.Views.Recipe.RecipeManagerWindow
            {
                DataContext = navigationViewModel.Workbench,
            };
            var recipeManagerRoot = (OpenVisionLab.ThreeD.Shell.Views.Recipe.RecipeManagerView)recipeManagerWindow.Content;
            recipeManagerRoot.Measure(new Size(1244, 760));
            recipeManagerRoot.Arrange(new Rect(0, 0, 1244, 760));
            recipeManagerRoot.UpdateLayout();
            var recipeManagerTitle = (Border)recipeManagerRoot.FindName("RecipeManagerPageTitle");
            var recipeManagerCaptionHeight =
                System.Windows.Shell.WindowChrome.GetWindowChrome(recipeManagerWindow)?.CaptionHeight ?? 0;
            Check(
                "Recipe Center uses its page title as the 50px caption without a duplicate app titlebar",
                Math.Abs(recipeManagerTitle.ActualHeight - 50) < 1
                && Math.Abs(recipeManagerCaptionHeight - 50) < 0.1
                && !FindVisualDescendants<StudioTitleBarView>(recipeManagerRoot).Any(),
                $"pageTitle={recipeManagerTitle.ActualHeight:F0}; caption={recipeManagerCaptionHeight:F0}; studioTitlebars={FindVisualDescendants<StudioTitleBarView>(recipeManagerRoot).Count()}");
            Check("R19 exposes six menu groups and two primary work modes",
                mainMenu.Items.Count == 6
                && navigationRail.AuthoringModeButton.Visibility == Visibility.Visible
                && navigationRail.ValidateModeButton.Visibility == Visibility.Visible
                && navigationRail.ResultsModeButton.Visibility == Visibility.Collapsed
                && navigationRail.CalibrateModeButton.Visibility == Visibility.Collapsed
                && navigationRail.AdvancedModeButton.Visibility == Visibility.Collapsed,
                $"menuGroups={mainMenu.Items.Count}; primary=Teaching,Inspection");
            var fileMenu = (MenuItem)mainMenu.Items[0];
            Check("R19 file menu reuses the existing new, open, and save commands",
                ReferenceEquals(((MenuItem)fileMenu.Items[0]).Command, navigationViewModel.Workbench.NewTeachingRecipeCommand)
                && ReferenceEquals(((MenuItem)fileMenu.Items[1]).Command, navigationViewModel.Workbench.OpenTeachingRecipeCommand)
                && ReferenceEquals(((MenuItem)fileMenu.Items[4]).Command, navigationViewModel.Workbench.SaveTeachingRecipeCommand),
                "same Workbench command objects");
            var fileMenuPopup = FindVisualDescendants<System.Windows.Controls.Primitives.Popup>(fileMenu).Single();
            var fileMenuPopupBinding = System.Windows.Data.BindingOperations.GetBindingExpression(
                fileMenuPopup,
                System.Windows.Controls.Primitives.Popup.IsOpenProperty);
            Check(
                "R19 compact top-level menu retains a bound native popup below the command row",
                fileMenuPopup.Placement == System.Windows.Controls.Primitives.PlacementMode.Bottom
                && fileMenuPopupBinding?.ParentBinding.Path?.Path == "IsSubmenuOpen"
                && fileMenu.Items.OfType<MenuItem>().Count() == 6,
                $"placement={fileMenuPopup.Placement}; binding={fileMenuPopupBinding?.ParentBinding.Path?.Path}; commands={fileMenu.Items.OfType<MenuItem>().Count()}");
            var titleBar = new StudioTitleBarView
            {
                Width = 1280,
                RecipeStateText = "Valid | Saved | " + new string('x', 48),
                IsRecipeContextVisible = true,
                SourceQualityStatusText = "Input needs review | " + new string('x', 48),
                SourceQualityStatusToolTip = "Current input state detail",
                SourceQualityStatusKind = "Warning",
                IsSourceQualityStatusVisible = true,
                StatusText = "No active work item | " + new string('x', 48),
            };
            titleBar.Measure(new Size(1280, 44));
            titleBar.Arrange(new Rect(0, 0, 1280, 44));
            titleBar.UpdateLayout();
            var recipeStateText = (TextBlock)titleBar.FindName("RecipeStateStatusText");
            var sourceQualityText = (TextBlock)titleBar.FindName("CurrentSourceQualityStatusText");
            var shellStatusText = (TextBlock)titleBar.FindName("ShellStatusText");
            Check(
                "Fixed-height title bar status stays single-line and retains full accessible text",
                recipeStateText.TextWrapping == TextWrapping.NoWrap
                && recipeStateText.TextTrimming == TextTrimming.CharacterEllipsis
                && AutomationProperties.GetName(recipeStateText) == titleBar.RecipeStateText
                && Equals(recipeStateText.ToolTip, titleBar.RecipeStateText)
                && sourceQualityText.TextWrapping == TextWrapping.NoWrap
                && sourceQualityText.TextTrimming == TextTrimming.CharacterEllipsis
                && shellStatusText.TextWrapping == TextWrapping.NoWrap
                && shellStatusText.TextTrimming == TextTrimming.CharacterEllipsis,
                $"recipe={recipeStateText.TextWrapping}/{recipeStateText.TextTrimming}; source={sourceQualityText.TextWrapping}/{sourceQualityText.TextTrimming}; status={shellStatusText.TextWrapping}/{shellStatusText.TextTrimming}");
            Check(
                "Job Bar exposes the accessible current-source quality state",
                titleBar.HasAccessibleCurrentSourceQualityStatus,
                $"visible={titleBar.IsSourceQualityStatusVisible}; kind={titleBar.SourceQualityStatusKind}; text={titleBar.SourceQualityStatusText}");
            titleBar.IsSourceQualityStatusVisible = false;
            titleBar.UpdateLayout();
            Check(
                "Job Bar hides current-source quality when no input exists",
                titleBar.HasAccessibleCurrentSourceQualityStatus,
                $"visible={titleBar.IsSourceQualityStatusVisible}");
            Check("Workbench hosts the tool picker and every dockable workspace", workbench.HasAllDockContentHosts && workbenchContracts.All(contract => contract.HasContent), Describe(workbenchContracts));
            Check("Workbench panes can float", workbenchContracts.All(contract => contract.CanFloat), Describe(workbenchContracts));
            Check("Workbench required panes cannot close", workbenchContracts.All(contract => !contract.CanClose), Describe(workbenchContracts));
            Check("Fit Diagnostics may hide without closing", workbenchContracts.Single(contract => contract.ContentId == "fit-diagnostics").CanHide == true, Describe(workbenchContracts));
            Check(
                "Output Compare and Displayed Outputs may hide without closing",
                workbenchContracts.Single(contract => contract.ContentId == "output-compare").CanHide == true
                && workbenchContracts.Single(contract => contract.ContentId == "displayed-outputs").CanHide == true,
                Describe(workbenchContracts));

            Check(
                "Workbench starts with the empty bottom review pane collapsed",
                !workbench.IsBottomPaneExpanded && !workbench.IsBottomPaneAttached,
                $"expanded={workbench.IsBottomPaneExpanded}, attached={workbench.IsBottomPaneAttached}");

            workbench.IsBottomPaneExpanded = false;
            var focusCollapsed = !workbench.IsBottomPaneAttached;
            workbench.IsBottomPaneExpanded = true;
            Check(
                "Workbench capture focus detaches then restores bottom pane",
                focusCollapsed && workbench.IsBottomPaneAttached,
                $"collapsed={focusCollapsed}, restored={workbench.IsBottomPaneAttached}");
            workbench.ActivateProfilePane();
            Check(
                "Workbench Profile command selects docked height-profile pane",
                workbench.IsBottomPaneAttached && workbench.IsProfilePaneSelected,
                $"attached={workbench.IsBottomPaneAttached}, selected={workbench.IsProfilePaneSelected}");
            workbench.ActivateSessionLogPane();
            Check(
                "Workbench Session Log command selects docked session pane",
                workbench.IsBottomPaneAttached && workbench.IsSessionLogPaneSelected,
                $"attached={workbench.IsBottomPaneAttached}, selected={workbench.IsSessionLogPaneSelected}");
            workbench.ActivateFlowMap();
            Check(
                "Workbench Flow Map command selects the read-only map in the docked Pipeline pane",
                workbench.IsBottomPaneAttached && workbench.IsFlowMapSelected,
                $"attached={workbench.IsBottomPaneAttached}, selected={workbench.IsFlowMapSelected}");
            workbench.ActivateProblems();
            Check(
                "Workbench Problems command selects the read-only port diagnostics in the docked Pipeline pane",
                workbench.IsBottomPaneAttached && workbench.IsProblemsSelected,
                $"attached={workbench.IsBottomPaneAttached}, selected={workbench.IsProblemsSelected}");
            workbench.ActivateRunRecord();
            Check(
                "Workbench Run Record exposes open, recent, two-run comparison, full-export, and privacy-safe support controls in the docked Pipeline pane",
                workbench.IsBottomPaneAttached
                && workbench.IsRunRecordSelected
                && workbench.HasRunRecordHistoryControls
                && workbench.HasPrivacySafeSupportBundleControls,
                $"attached={workbench.IsBottomPaneAttached}, selected={workbench.IsRunRecordSelected}, history={workbench.HasRunRecordHistoryControls}, support={workbench.HasPrivacySafeSupportBundleControls}");
            workbench.ActivateOutputComparePane();
            Check(
                "Workbench Output Compare command selects a floatable pane with usable default height",
                workbench.IsBottomPaneAttached
                && workbench.IsOutputComparePaneSelected
                && workbench.HasUsableOutputCompareDockHeight,
                $"attached={workbench.IsBottomPaneAttached}, selected={workbench.IsOutputComparePaneSelected}, usableHeight={workbench.HasUsableOutputCompareDockHeight}");
            workbench.ActivateViewerPane();
            Check(
                "A/B comparison returns to the primary Viewer and collapses its temporary comparison dock",
                workbench.IsViewerPaneSelected
                && !workbench.IsBottomPaneExpanded,
                $"viewer={workbench.IsViewerPaneSelected};bottomExpanded={workbench.IsBottomPaneExpanded}");
            workbench.ActivateOutputComparePane();
            workbench.ActivateDisplayedOutputsPane();
            Check(
                "Workbench Displayed Outputs command restores the standard bottom-pane height",
                workbench.IsBottomPaneAttached
                && workbench.IsDisplayedOutputsPaneSelected
                && workbench.HasStandardBottomPaneHeight,
                $"attached={workbench.IsBottomPaneAttached}, selected={workbench.IsDisplayedOutputsPaneSelected}, standardHeight={workbench.HasStandardBottomPaneHeight}");
            workbench.ActivateFitDiagnosticsPane();
            Check("Workbench Fit Diagnostics command selects docked diagnostics pane", workbench.IsBottomPaneAttached && workbench.IsFitDiagnosticsPaneSelected, $"attached={workbench.IsBottomPaneAttached}, selected={workbench.IsFitDiagnosticsPaneSelected}");
            workbench.ActivateIntersectionEvidencePane();
            Check("Workbench Intersection Evidence command selects docked evidence pane", workbench.IsBottomPaneAttached && workbench.IsIntersectionEvidencePaneSelected, $"attached={workbench.IsBottomPaneAttached}, selected={workbench.IsIntersectionEvidencePaneSelected}");
            workbench.ActivateCorrespondenceEvidencePane();
            Check("Workbench Correspondence Evidence command selects docked evidence pane", workbench.IsBottomPaneAttached && workbench.IsCorrespondenceEvidencePaneSelected, $"attached={workbench.IsBottomPaneAttached}, selected={workbench.IsCorrespondenceEvidencePaneSelected}");

            var startPageShell = new ShellMainWindowViewModel();
            startPageShell.ShowStartPage();
            Check(
                "R19 start page hides the workbench and navigation without starting inspection",
                startPageShell.IsStartPageSelected
                && !startPageShell.IsNavigationRailVisible
                && !startPageShell.IsRecipeActionsVisible
                && !startPageShell.IsWorkbenchWorkspaceSelected
                && !startPageShell.IsAuthoringWorkspaceSelected
                && !startPageShell.IsInspectionWorkspaceSelected
                && !startPageShell.Workbench.IsOrderedRunRunning
                && !startPageShell.Workbench.IsSelectedStepPreviewRunning
                && !startPageShell.Workbench.IsValidationSetRunning,
                $"start={startPageShell.IsStartPageSelected}; rail={startPageShell.IsNavigationRailVisible}; workspace={startPageShell.SelectedWorkspaceMode}; running={startPageShell.Workbench.IsOrderedRunRunning || startPageShell.Workbench.IsSelectedStepPreviewRunning || startPageShell.Workbench.IsValidationSetRunning}");
            startPageShell.SelectWorkspaceCommand.Execute(ShellWorkspaceMode.Teach);
            Check(
                "Explicit workspace navigation closes the start page without executing a tool",
                !startPageShell.IsStartPageSelected
                && startPageShell.IsNavigationRailVisible
                && startPageShell.IsRecipeActionsVisible
                && startPageShell.IsWorkbenchWorkspaceSelected
                && startPageShell.SelectedWorkspaceMode == ShellWorkspaceMode.Teach
                && !startPageShell.Workbench.IsOrderedRunRunning
                && !startPageShell.Workbench.IsSelectedStepPreviewRunning,
                $"start={startPageShell.IsStartPageSelected}; rail={startPageShell.IsNavigationRailVisible}; workspace={startPageShell.SelectedWorkspaceMode}; running={startPageShell.Workbench.IsOrderedRunRunning || startPageShell.Workbench.IsSelectedStepPreviewRunning}");
            startPageShell.Dispose();

            var shell = new ShellMainWindowViewModel();
            var recipePath = Path.Combine(FindRepositoryRoot(), ThicknessRecipeRelativePath);
            Check(
                "stage verification opens the current bundled Thickness recipe",
                shell.Workbench.TryOpenTeachingRecipe(recipePath, out var openMessage)
                && shell.Workbench.PipelineSteps.Count == 8
                && shell.Workbench.SelectedPipelineStep is not null,
                openMessage);
            shell.SelectWorkspaceCommand.Execute(ShellWorkspaceMode.Teach);
            Check(
                "Selecting the Teaching workspace does not execute inspection",
                shell.IsTeachWorkspaceSelected
                && !shell.Workbench.IsOrderedRunRunning
                && !shell.Workbench.IsSelectedStepPreviewRunning,
                $"teach={shell.IsTeachWorkspaceSelected}; run={shell.Workbench.IsOrderedRunRunning}; preview={shell.Workbench.IsSelectedStepPreviewRunning}");
            var initialViewerCandidates = shell.Workbench.ViewerWorkspaceCandidates;
            var initialMainViewerCandidate = initialViewerCandidates.SingleOrDefault(candidate =>
                candidate.Id == shell.Workbench.ViewerWorkspace.MainContentId);
            var initialAuxiliaryViewerCandidate = initialViewerCandidates.SingleOrDefault(candidate =>
                candidate.Id == shell.Workbench.ViewerWorkspace.AuxiliaryContentId);
            Check(
                "Teaching starts with 3D alone and keeps XY available without running inspection",
                shell.Workbench.IsSourceReadyForRecipe
                && shell.Workbench.ViewerWorkspace.Layout == ViewerWorkspaceLayout.Single
                && initialMainViewerCandidate is { Kind: ViewerWorkspaceCandidateKind.ThreeDArtifact, IsSource: true }
                && initialAuxiliaryViewerCandidate?.Kind == ViewerWorkspaceCandidateKind.HeightImage
                && !shell.Workbench.IsOrderedRunRunning
                && !shell.Workbench.IsSelectedStepPreviewRunning,
                $"sourceReady={shell.Workbench.IsSourceReadyForRecipe};layout={shell.Workbench.ViewerWorkspace.Layout};main={initialMainViewerCandidate?.Id};aux={initialAuxiliaryViewerCandidate?.Id};run={shell.Workbench.IsOrderedRunRunning};preview={shell.Workbench.IsSelectedStepPreviewRunning}");
            var selectedStep = shell.Workbench.SelectedPipelineStep;
            shell.Workbench.SplitViewerVerticallyCommand.Execute(null);
            var recipeFlowView = new RecipeChainView
            {
                DataContext = shell.Workbench,
                IsTeachingMode = true,
            };
            ArrangeForTextVerification(recipeFlowView, 390);
            var flowAddToolButton = (System.Windows.Controls.Button)recipeFlowView.FindName("RecipeFlowAddTool");
            var flowToolbar = (Border)recipeFlowView.FindName("RecipeFlowToolbar");
            var flowViewport = (ScrollViewer)recipeFlowView.FindName("FlowGraphViewport");
            var flowHint = (Border)recipeFlowView.FindName("RecipeFlowHint");
            var flowZoomValue = (TextBlock)recipeFlowView.FindName("RecipeFlowZoomValue");
            var flowConnectionCanvas = (Canvas)recipeFlowView.FindName("FlowConnectionCanvas");
            var flowPortConnectionCanvas = (Canvas)recipeFlowView.FindName("FlowPortConnectionCanvas");
            var recipeStepList = (ListBox)recipeFlowView.FindName("RecipeStepList");
            var firstFlowStep = shell.Workbench.PipelineSteps[0];
            var firstFlowItem = recipeStepList.ItemContainerGenerator.ContainerFromIndex(0) as ListBoxItem;
            var secondFlowItem = recipeStepList.ItemContainerGenerator.ContainerFromIndex(1) as ListBoxItem;
            var thirdFlowItem = recipeStepList.ItemContainerGenerator.ContainerFromIndex(2) as ListBoxItem;
            var sourceOutputPort = (Button)recipeFlowView.FindName("SourceOutputPort");
            var firstFlowInputPort = firstFlowItem is null
                ? null
                : FindVisualDescendants<Button>(firstFlowItem).FirstOrDefault(button => button.Name == "StepInputPort");
            var flowCancelKeyBinding = recipeFlowView.InputBindings
                .OfType<KeyBinding>()
                .SingleOrDefault(binding => binding.Key == Key.Escape);
            var firstFlowOutputPort = firstFlowItem is null
                ? null
                : FindVisualDescendants<Button>(firstFlowItem).SingleOrDefault(button => button.Name == "StepOutputPort");
            Check(
                "R19 graph port captions belong to full-width, 28-pixel command rows",
                firstFlowInputPort is { ActualWidth: >= 120, ActualHeight: >= 28, Content: StackPanel }
                && firstFlowOutputPort is { ActualWidth: >= 120, ActualHeight: >= 28, Content: TextBlock }
                && firstFlowInputPort.HorizontalContentAlignment == HorizontalAlignment.Left
                && firstFlowOutputPort.HorizontalContentAlignment == HorizontalAlignment.Right,
                $"input={firstFlowInputPort?.ActualWidth}x{firstFlowInputPort?.ActualHeight};output={firstFlowOutputPort?.ActualWidth}x{firstFlowOutputPort?.ActualHeight};captions={firstFlowInputPort?.Content is TextBlock}/{firstFlowOutputPort?.Content is TextBlock}");
            var firstFlowText = firstFlowItem is null
                ? Array.Empty<string>()
                : FindVisualDescendants<TextBlock>(firstFlowItem).Select(text => text.Text).ToArray();
            var flowOutputIds = shell.Workbench.PipelineSteps
                .Select(step => step.OutputEntityId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var expectedFlowConnectionCount = shell.Workbench.PipelineSteps.Sum(step => step.InputEntityIds
                .Where(id => string.Equals(id, shell.Workbench.Source.Id, StringComparison.OrdinalIgnoreCase)
                    || flowOutputIds.Contains(id)
                    || shell.Workbench.Selections.Any(selection => selection.Id.Equals(id, StringComparison.OrdinalIgnoreCase)))
                .Count());
            var firstFlowOrigin = firstFlowItem?.TranslatePoint(new Point(), recipeStepList);
            var secondFlowOrigin = secondFlowItem?.TranslatePoint(new Point(), recipeStepList);
            var thirdFlowOrigin = thirdFlowItem?.TranslatePoint(new Point(), recipeStepList);
            var flowOrigins = Enumerable.Range(0, recipeStepList.Items.Count)
                .Select(index => (recipeStepList.ItemContainerGenerator.ContainerFromIndex(index) as ListBoxItem)
                    ?.TranslatePoint(new Point(), recipeStepList))
                .ToArray();
            var flowIsSavedSourceFanOut = shell.Workbench.PipelineSteps.All(step =>
                step.InputEntityIds.Contains(shell.Workbench.Source.Id, StringComparer.OrdinalIgnoreCase)
                && !step.InputEntityIds.Any(flowOutputIds.Contains));
            var flowRowsMatchR19Reference = flowIsSavedSourceFanOut
                && flowOrigins.Length == 8
                && Enumerable.Range(0, flowOrigins.Length / 2).All(rowIndex =>
                {
                    var first = flowOrigins[rowIndex * 2];
                    var second = flowOrigins[(rowIndex * 2) + 1];
                    return first.HasValue
                        && second.HasValue
                        && second.Value.X > first.Value.X
                        && Math.Abs(first.Value.Y - second.Value.Y) < 2
                        && (rowIndex == 0 || first.Value.Y > flowOrigins[(rowIndex - 1) * 2]!.Value.Y);
                });
            recipeStepList.SelectedIndex = 1;
            recipeFlowView.UpdateLayout();
            var flowSelectionRoundTrip = ReferenceEquals(
                shell.Workbench.SelectedPipelineStep,
                shell.Workbench.PipelineSteps[1]);
            recipeStepList.SelectedItem = selectedStep;
            recipeFlowView.UpdateLayout();
            recipeFlowView.IsTeachingMode = false;
            recipeFlowView.UpdateLayout();
            var flowAddToolHiddenDuringInspection = flowAddToolButton.Visibility == Visibility.Collapsed;
            var flowPresentationHiddenDuringInspection = flowToolbar.Visibility == Visibility.Collapsed
                && flowHint.Visibility == Visibility.Collapsed;
            recipeFlowView.IsTeachingMode = true;
            recipeFlowView.UpdateLayout();
            var flowRouteBeforePortInteraction = firstFlowStep.InputEntityIdsText;
            var flowDirtyBeforePortInteraction = shell.Workbench.IsDirty;
            var flowPortActionCountBeforeInteraction = shell.Workbench.RunLog.Count(item =>
                item.Category is "Preview" or "Publish" or "Run");
            var flowPortCommandsBound = firstFlowInputPort is not null
                && ReferenceEquals(sourceOutputPort.Command, shell.Workbench.BeginFlowConnectionCommand)
                && Equals(sourceOutputPort.CommandParameter, shell.Workbench.Source.Id)
                && ReferenceEquals(firstFlowInputPort.Command, shell.Workbench.ConnectFlowOutputCommand)
                && firstFlowInputPort.CommandParameter is ToolWorkbenchFlowInputItem { Index: 0 } flowInput && ReferenceEquals(flowInput.Step, firstFlowStep)
                && flowCancelKeyBinding is not null
                && ReferenceEquals(flowCancelKeyBinding.Command, shell.Workbench.CancelFlowConnectionCommand)
                && flowCancelKeyBinding.Key == Key.Escape;
            var inputDisabledUntilOutputSelected = firstFlowInputPort?.IsEnabled == false;
            sourceOutputPort.Command.Execute(sourceOutputPort.CommandParameter);
            recipeFlowView.UpdateLayout();
            var outputSelectionEnablesInput = shell.Workbench.HasPendingFlowConnection
                && firstFlowInputPort?.IsEnabled == true;
            flowCancelKeyBinding?.Command.Execute(null);
            recipeFlowView.UpdateLayout();
            Check(
                "R19 graph output/input ports bind to route commands and Escape cancels selection without running inspection",
                flowPortCommandsBound
                && inputDisabledUntilOutputSelected
                && outputSelectionEnablesInput
                && !shell.Workbench.HasPendingFlowConnection
                && firstFlowInputPort?.IsEnabled == false
                && firstFlowStep.InputEntityIdsText == flowRouteBeforePortInteraction
                && shell.Workbench.IsDirty == flowDirtyBeforePortInteraction
                && shell.Workbench.RunLog.Count(item =>
                    item.Category is "Preview" or "Publish" or "Run") == flowPortActionCountBeforeInteraction,
                $"bound={flowPortCommandsBound};inputDisabled={inputDisabledUntilOutputSelected};armed={outputSelectionEnablesInput};pending={shell.Workbench.HasPendingFlowConnection};route={firstFlowStep.InputEntityIdsText};dirty={shell.Workbench.IsDirty};actions={shell.Workbench.RunLog.Count(item => item.Category is "Preview" or "Publish" or "Run")}");
            var flowPaths = flowConnectionCanvas.Children.OfType<System.Windows.Shapes.Path>().ToArray();
            var flowArrows = flowPortConnectionCanvas.Children.OfType<System.Windows.Shapes.Polygon>().ToArray();
            var flowPortPaths = flowPortConnectionCanvas.Children.OfType<System.Windows.Shapes.Path>().ToArray();
            var firstInputGlyph = firstFlowInputPort is null
                ? null
                : FindVisualDescendants<System.Windows.Shapes.Ellipse>(firstFlowInputPort).SingleOrDefault(glyph => glyph.Name == "PortVisual");
            var firstInputAnchor = firstInputGlyph?.TranslatePoint(
                new Point(firstInputGlyph.ActualWidth / 2, firstInputGlyph.ActualHeight / 2), flowConnectionCanvas);
            Check(
                "Expanded graph port hit regions keep saved wire endpoints on the input glyph",
                firstInputAnchor.HasValue
                && Panel.GetZIndex(flowConnectionCanvas) < Panel.GetZIndex(recipeStepList)
                && Panel.GetZIndex(flowPortConnectionCanvas) > Panel.GetZIndex(recipeStepList)
                && !flowConnectionCanvas.IsHitTestVisible && !flowPortConnectionCanvas.IsHitTestVisible
                && flowPortPaths.All(path => path.Data is GeometryGroup { Children.Count: 2 } terminals
                    && terminals.Children.All(geometry => geometry is LineGeometry line
                        && Math.Abs(line.StartPoint.Y - line.EndPoint.Y) < 0.01))
                && flowArrows.Any(arrow => (arrow.Points[0] - firstInputAnchor.Value).Length < 0.01),
                $"inputAnchor={firstInputAnchor};arrows={flowArrows.Length};z={Panel.GetZIndex(flowConnectionCanvas)}/{Panel.GetZIndex(recipeStepList)}/{Panel.GetZIndex(flowPortConnectionCanvas)};hit={flowConnectionCanvas.IsHitTestVisible}/{flowPortConnectionCanvas.IsHitTestVisible};figures={string.Join(',', flowPortPaths.Select(path => path.Data is GeometryGroup group ? group.Children.Count : 0))};sample={flowPortPaths.FirstOrDefault()?.Data}");
            var flowUsesSidePortRouting = flowPaths.All(path => path.Data.Bounds.Width > 0 && path.Data.Bounds.Height > 0)
                && flowArrows.All(arrow => arrow.Points.Count == 3
                    && arrow.Points[1].X < arrow.Points[0].X
                    && arrow.Points[2].X < arrow.Points[0].X);
            Check(
                "R19 Recipe Flow renders saved routes and groups same-depth tools into two-column rows",
                flowAddToolButton.Visibility == Visibility.Visible
                && ReferenceEquals(flowAddToolButton.Command, shell.Workbench.OpenToolLibraryCommand)
                && recipeStepList.ItemsSource == shell.Workbench.PipelineSteps
                && FindVisualDescendants<Canvas>(recipeStepList).Any()
                && !double.IsNaN(recipeStepList.Height)
                && recipeStepList.ActualHeight > 1
                && recipeStepList.Items.Count == shell.Workbench.PipelineSteps.Count
                && firstFlowText.Contains(firstFlowStep.ToolName)
                && firstFlowText.Contains(ThreeDLocalization.Shared.Resolve(
                    "ThreeD.Flow.Input", "입력", "Input"))
                && !firstFlowText.Contains(firstFlowStep.InputSummary)
                && !firstFlowText.Contains(firstFlowStep.InputPortState)
                && firstFlowText.Contains(ThreeDLocalization.Shared.Resolve(
                    "ThreeD.Flow.Output", "출력", "Output"))
                && !firstFlowText.Contains(firstFlowStep.OutputEntityId)
                && !firstFlowText.Contains(firstFlowStep.OutputPortState)
                && !firstFlowText.Contains(firstFlowStep.FlowNodeStatusLabel)
                && expectedFlowConnectionCount > 0
                && recipeFlowView.FlowConnectionCount == expectedFlowConnectionCount
                && flowConnectionCanvas.Children.Count == expectedFlowConnectionCount
                && flowPortConnectionCanvas.Children.Count == expectedFlowConnectionCount * 2
                && flowUsesSidePortRouting
                && flowRowsMatchR19Reference
                && flowSelectionRoundTrip
                && ReferenceEquals(recipeStepList.SelectedItem, selectedStep)
                && flowAddToolHiddenDuringInspection
                && flowPresentationHiddenDuringInspection
                && !shell.Workbench.IsOrderedRunRunning
                && !shell.Workbench.IsSelectedStepPreviewRunning,
                $"addVisible={flowAddToolButton.Visibility}; command={ReferenceEquals(flowAddToolButton.Command, shell.Workbench.OpenToolLibraryCommand)}; items={recipeStepList.Items.Count}; connections={recipeFlowView.FlowConnectionCount}/{expectedFlowConnectionCount}; sideRouting={flowUsesSidePortRouting}; savedFanOut={flowIsSavedSourceFanOut}; rows={flowRowsMatchR19Reference}; nodes={string.Join(';', flowOrigins)}; firstPorts={string.Join('|', firstFlowText)}; selectionRoundTrip={flowSelectionRoundTrip}; inspectHidden={flowAddToolHiddenDuringInspection}/{flowPresentationHiddenDuringInspection}; run={shell.Workbench.IsOrderedRunRunning}; preview={shell.Workbench.IsSelectedStepPreviewRunning}");
            var notRunLabel = ThreeDLocalization.Shared.ResultStatusLabel(ResultStatus.NotRun);
            Check(
                "R19 Recipe Flow omits repeated port states and labels saved, unrun tools as Not run",
                shell.Workbench.PipelineSteps.All(step =>
                    step.OutputPortState == ThreeDLocalization.Shared.FlowPortDeclared
                    && step.FlowNodeStatusLabel == notRunLabel
                    && !step.FlowNodeStatusHasIssue
                    && !step.FlowNodeStatusIsFailure)
                && !firstFlowText.Contains(firstFlowStep.InputPortState)
                && !firstFlowText.Contains(firstFlowStep.OutputPortState)
                && !firstFlowText.Contains(notRunLabel)
                && !shell.Workbench.IsOrderedRunRunning
                && !shell.Workbench.IsSelectedStepPreviewRunning,
                $"notRun='{notRunLabel}'; nodeLabels={string.Join('|', shell.Workbench.PipelineSteps.Select(step => step.FlowNodeStatusLabel))}; firstPorts={string.Join('|', firstFlowText)}; run={shell.Workbench.IsOrderedRunRunning}; preview={shell.Workbench.IsSelectedStepPreviewRunning}");
            var flowPresentationSelectedStep = shell.Workbench.SelectedPipelineStep;
            var flowPresentationDirty = shell.Workbench.IsDirty;
            for (var index = 0; index < 8; index++)
            {
                recipeFlowView.IncreaseFlowZoomCommand.Execute(null);
            }
            var flowZoomStopsAtMaximum = Math.Abs(recipeFlowView.FlowZoom - 1.5) < 0.001
                && !recipeFlowView.IncreaseFlowZoomCommand.CanExecute(null)
                && recipeFlowView.DecreaseFlowZoomCommand.CanExecute(null);
            recipeFlowView.ResetFlowViewCommand.Execute(null);
            recipeFlowView.BringSelectedToolIntoViewCommand.Execute(null);
            recipeFlowView.UpdateLayout();
            Check(
                "R19 Recipe Flow matches the reference graph controls without mutating or running the recipe",
                flowToolbar.Visibility == Visibility.Visible
                && flowHint.Visibility == Visibility.Collapsed
                && flowViewport.HorizontalScrollBarVisibility == ScrollBarVisibility.Auto
                && flowViewport.VerticalScrollBarVisibility == ScrollBarVisibility.Auto
                && !string.IsNullOrWhiteSpace(flowZoomValue.Text)
                && flowZoomStopsAtMaximum
                && Math.Abs(recipeFlowView.FlowZoom - 1d) < 0.001
                && recipeFlowView.IncreaseFlowZoomCommand.CanExecute(null)
                && recipeFlowView.DecreaseFlowZoomCommand.CanExecute(null)
                && ReferenceEquals(flowPresentationSelectedStep, shell.Workbench.SelectedPipelineStep)
                && shell.Workbench.IsDirty == flowPresentationDirty
                && !shell.Workbench.IsOrderedRunRunning
                && !shell.Workbench.IsSelectedStepPreviewRunning,
                $"toolbar={flowToolbar.Visibility}; hint={flowHint.Visibility}; zoom={recipeFlowView.FlowZoom}/{flowZoomValue.Text}; selected={shell.Workbench.SelectedPipelineStep?.Id}; dirty={flowPresentationDirty}->{shell.Workbench.IsDirty}; run={shell.Workbench.IsOrderedRunRunning}; preview={shell.Workbench.IsSelectedStepPreviewRunning}");
            var selectedStepState = selectedStep?.State;
            var recipeStepCount = shell.Workbench.PipelineSteps.Count;
            var recipeDirty = shell.Workbench.IsDirty;
            var validationFixture =
                CompletenessValidationVerificationFixtureFactory.Create(
                    Path.Combine(
                        GetReportDirectory(reportPath),
                        "validation-set-fixture"));
            var browseSelection = ToolWorkbenchRecentRecipeItem.From(validationFixture.RecipePath);
            navigationViewModel.Workbench.SelectedRecentRecipe = browseSelection;
            recipeManagerView.UpdateLayout();
            var selectedRecipeHeading = (StackPanel)recipeManagerView.FindName("SelectedRecipeHeading");
            Check(
                "R19 Recipe Center shows selected recipe details and saved content",
                navigationViewModel.Workbench.HasSelectedRecentRecipe
                && navigationViewModel.Workbench.IsRecipePreviewVisible
                && selectedRecipeHeading.Visibility == Visibility.Visible
                && recipeBrowserContent.Visibility == Visibility.Visible,
                $"selected={navigationViewModel.Workbench.HasSelectedRecentRecipe}; content={navigationViewModel.Workbench.IsRecipePreviewVisible}; heading={selectedRecipeHeading.Visibility}; details={recipeBrowserContent.Visibility}");
            navigationViewModel.Workbench.SelectedRecentRecipe = null;
            recipeManagerView.UpdateLayout();
            Check(
                "R19 Recipe Center hides selected details when selection is cleared",
                !navigationViewModel.Workbench.HasSelectedRecentRecipe
                && !navigationViewModel.Workbench.IsRecipePreviewVisible
                && selectedRecipeHeading.Visibility == Visibility.Collapsed
                && recipeBrowserContent.Visibility == Visibility.Collapsed,
                $"selected={navigationViewModel.Workbench.HasSelectedRecentRecipe}; content={navigationViewModel.Workbench.IsRecipePreviewVisible}; heading={selectedRecipeHeading.Visibility}; details={recipeBrowserContent.Visibility}");
            var validationSamplePaths = validationFixture.Samples
                .Select(sample => sample.SourcePath)
                .ToArray();
            Check(
                "stage verification fixture owns five readable Validation Set samples",
                validationSamplePaths.All(File.Exists),
                string.Join(", ", validationSamplePaths.Select(Path.GetFileName)));
            shell.Workbench.SetValidationSetSources(validationSamplePaths);
            var stageWorkbench = new ToolRecipeWorkbenchView
            {
                DataContext = shell,
                ViewerContent = viewerOwner,
            };
            _ = ShellWindowNativeInterop.TryGetLeftmostWorkArea(out var testWorkArea);
            var testLeft = testWorkArea.Left;
            var testTop = testWorkArea.Top;
            var monitorReportPath = Path.GetFullPath(reportPath) + ".monitors.txt";
            Directory.CreateDirectory(Path.GetDirectoryName(monitorReportPath)!);
            File.WriteAllText(monitorReportPath, string.Empty);
            var stageHost = new Window
            {
                Content = stageWorkbench,
                Width = 1600,
                Height = 900,
                Left = testLeft,
                Top = testTop,
                ShowActivated = false,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
            };
            stageHost.Show();
            stageHost.UpdateLayout();
            ShellWindowNativeInterop.AppendWindowMonitorEvidence(stageHost, monitorReportPath);
            var viewerWorkspaceView = FindVisualDescendants<ViewerWorkspaceView>(stageWorkbench).Single();
            var auxiliaryViewerSelector = FindVisualDescendants<ComboBox>(viewerWorkspaceView).Single(comboBox =>
                System.Windows.Automation.AutomationProperties.GetAutomationId(comboBox) == "AuxiliaryViewerContentPin");
            var auxiliaryViewerContentId = shell.Workbench.AuxiliaryViewerContentId;
            var stageLanguage = OpenVisionLanguageService.CurrentLanguage;
            var auxiliaryViewerSelectorEnglish = false;
            var auxiliaryViewerSelectorKorean = false;
            try
            {
                OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
                stageHost.UpdateLayout();
                auxiliaryViewerSelectorEnglish = Equals(auxiliaryViewerSelector.SelectedValue, auxiliaryViewerContentId)
                    && FindVisualDescendants<TextBlock>(auxiliaryViewerSelector).Any(text => text.Text == "Height Image");

                OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
                stageHost.UpdateLayout();
                auxiliaryViewerSelectorKorean = Equals(auxiliaryViewerSelector.SelectedValue, auxiliaryViewerContentId)
                    && FindVisualDescendants<TextBlock>(auxiliaryViewerSelector).Any(text => text.Text == "높이 이미지");
            }
            finally
            {
                OpenVisionLanguageService.SetLanguage(stageLanguage, save: false);
                stageHost.UpdateLayout();
            }
            Check(
                "Auxiliary Viewer selection keeps its stable output and current language in both directions",
                auxiliaryViewerSelectorEnglish
                && auxiliaryViewerSelectorKorean
                && Equals(auxiliaryViewerSelector.SelectedValue, auxiliaryViewerContentId),
                $"english={auxiliaryViewerSelectorEnglish}; korean={auxiliaryViewerSelectorKorean}; selected={auxiliaryViewerSelector.SelectedValue}; expected={auxiliaryViewerContentId}");
            Check("localized node contract refresh preserves recipe and execution identity",
                shell.Workbench.IsDirty == recipeDirty && ReferenceEquals(selectedStep, shell.Workbench.SelectedPipelineStep)
                && shell.Workbench.PipelineSteps.Count == recipeStepCount && shell.Workbench.SelectedPipelineStep?.State == selectedStepState
                && !shell.Workbench.IsSelectedStepPreviewRunning && !shell.Workbench.IsOrderedRunRunning,
                $"dirty={recipeDirty}->{shell.Workbench.IsDirty};step={shell.Workbench.SelectedPipelineStep?.Id};state={shell.Workbench.SelectedPipelineStep?.State};steps={shell.Workbench.PipelineSteps.Count}");
            var dockWorkspace = (OpenVisionDockWorkspaceView)stageWorkbench.FindName("DockWorkspace");
            var wideWorkbenchPane = (AvalonDock.Layout.LayoutPanel)dockWorkspace.FindName("workbenchPane");
            var supportPane = dockWorkspace.FindName("dataLayersPane");
            var inspectorPane = dockWorkspace.FindName("toolInspectorPane");
            var viewerPane = dockWorkspace.FindName("primaryPane");
            var viewerAnchorable = (AvalonDock.Layout.LayoutAnchorable)dockWorkspace.FindName("viewerAnchorable");
            var displayedOutputsAnchorable = (AvalonDock.Layout.LayoutAnchorable)dockWorkspace.FindName("displayedOutputsAnchorable");
            var wideResultsPaneModel = dockWorkspace.FindName("resultsPane");
            var previousCompareSlotA = shell.Workbench.CompareSlotAArtifactId;
            var previousCompareSlotB = shell.Workbench.CompareSlotBArtifactId;
            var compareCandidateIds = shell.Workbench.RenderableC3DCatalog
                .Where(candidate => candidate.IsDisplayable)
                .Select(candidate => candidate.Id)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(2)
                .ToArray();
            var canRunCompareComposition = compareCandidateIds.Length == 2;
            if (canRunCompareComposition)
            {
                shell.Workbench.CompareSlotAArtifactId = compareCandidateIds[0];
                shell.Workbench.CompareSlotBArtifactId = compareCandidateIds[1];
                canRunCompareComposition = shell.Workbench.ShowOutputCompareViewerCommand.CanExecute(null);
            }
            Check(
                "Teaching compare fixture uses two distinct current renderable artifacts",
                canRunCompareComposition,
                $"candidates={string.Join('|', compareCandidateIds)}; canShow={shell.Workbench.CanShowOutputCompareViewer}");
            stageHost.Width = testWorkArea.Width;
            stageHost.Height = testWorkArea.Height;
            dockWorkspace.SetOperatorStage(OpenVisionOperatorStage.Teach);
            stageHost.UpdateLayout();
            ShellWindowNativeInterop.AppendWindowMonitorEvidence(stageHost, monitorReportPath);
            var viewerLayoutToolbar = (Border)viewerWorkspaceView.FindName("ViewerLayoutToolbar");
            var viewerWorkspaceOptions = (Menu)viewerWorkspaceView.FindName("ViewerWorkspaceOptionsMenu");
            var viewerInspectionDataSwitch = (Border)viewerWorkspaceView.FindName("ViewerInspectionDataSwitch");
            var threeDViewerMode = (ToggleButton)viewerWorkspaceView.FindName("ThreeDViewerMode");
            var heightOnlyViewerMode = (ToggleButton)viewerWorkspaceView.FindName("HeightOnlyViewerMode");
            var threeDHeightViewerMode = (ToggleButton)viewerWorkspaceView.FindName("ThreeDHeightViewerMode");
            Check(
                "single-view controls and Displayed Outputs are available before entering A/B comparison",
                viewerLayoutToolbar.Visibility == Visibility.Visible
                && displayedOutputsAnchorable.IsVisible
                && viewerAnchorable.IsSelected,
                $"toolbar={viewerLayoutToolbar.Visibility};displayedOutputs={displayedOutputsAnchorable.IsVisible};viewerSelected={viewerAnchorable.IsSelected}");
            var wideResultsControlBeforeCompare = FindVisualDescendants<AvalonDock.Controls.LayoutAnchorablePaneControl>(dockWorkspace)
                .SingleOrDefault(pane => ReferenceEquals(pane.Model, wideResultsPaneModel));
            var wideResultsGridBeforeCompare = wideResultsControlBeforeCompare is null
                ? null
                : VisualTreeHelper.GetParent(wideResultsControlBeforeCompare) as Grid;
            var wideResultsRowIndexBeforeCompare = wideResultsControlBeforeCompare is null
                ? -1
                : Grid.GetRow(wideResultsControlBeforeCompare);
            var wideResultsRowBeforeCompare = wideResultsGridBeforeCompare is not null
                && wideResultsRowIndexBeforeCompare > 0
                && wideResultsRowIndexBeforeCompare < wideResultsGridBeforeCompare.RowDefinitions.Count
                    ? wideResultsGridBeforeCompare.RowDefinitions[wideResultsRowIndexBeforeCompare]
                    : null;
            var wideResultsSplitterRowBeforeCompare = wideResultsGridBeforeCompare is not null
                && wideResultsRowIndexBeforeCompare > 0
                    ? wideResultsGridBeforeCompare.RowDefinitions[wideResultsRowIndexBeforeCompare - 1]
                    : null;
            var wideResultsRowMinHeightBeforeCompare = wideResultsRowBeforeCompare?.MinHeight ?? double.NaN;
            var wideResultsSplitterHeightBeforeCompare = wideResultsSplitterRowBeforeCompare?.Height ?? default;
            if (canRunCompareComposition)
            {
                shell.Workbench.ShowOutputCompareViewerCommand.Execute(null);
                stageHost.UpdateLayout();
            }
            WpfScreenshotCapture.Save(
                WpfScreenshotCapture.Capture(stageWorkbench).Bitmap,
                Path.ChangeExtension(reportPath, ".ab-compare-wide.png"));
            var wideCompareViewerPane = FindVisualDescendants<AvalonDock.Controls.LayoutAnchorablePaneControl>(dockWorkspace)
                .Single(pane => ReferenceEquals(pane.Model, viewerPane));
            var wideCompareResultsPane = FindVisualDescendants<AvalonDock.Controls.LayoutAnchorablePaneControl>(dockWorkspace)
                .SingleOrDefault(pane => ReferenceEquals(pane.Model, wideResultsPaneModel));
            var wideCompareViewerBounds = wideCompareViewerPane.TransformToAncestor(dockWorkspace)
                .TransformBounds(new Rect(wideCompareViewerPane.RenderSize));
            var wideCompareViewerTree = DescribeVisualAncestors(wideCompareViewerPane, dockWorkspace);
            var wideCompareResultsBounds = wideCompareResultsPane?.TransformToAncestor(dockWorkspace)
                .TransformBounds(new Rect(wideCompareResultsPane.RenderSize)) ?? Rect.Empty;
            var wideComparePaneBounds = string.Join(';', FindVisualDescendants<AvalonDock.Controls.LayoutAnchorablePaneControl>(dockWorkspace)
                .Select(pane => $"{(ReferenceEquals(pane.Model, wideResultsPaneModel) ? "results" : ReferenceEquals(pane.Model, viewerPane) ? "viewer" : ReferenceEquals(pane.Model, supportPane) ? "support" : ReferenceEquals(pane.Model, inspectorPane) ? "inspector" : "other")}:{string.Join('|', ((AvalonDock.Layout.LayoutAnchorablePane)pane.Model).Children.Select(item => item.ContentId))}:{pane.TransformToAncestor(dockWorkspace).TransformBounds(new Rect(pane.RenderSize))}"));
            var wideRootChildren = string.Join(',', ((AvalonDock.Layout.LayoutPanel)dockWorkspace.FindName("workspaceRootPanel"))
                .Children.Select(child => child.GetType().Name));
            var wideCompareColumn = ((AvalonDock.Layout.LayoutAnchorablePane)wideResultsPaneModel).Parent as AvalonDock.Layout.LayoutPanel;
            Check(
                "A/B comparison keeps display modes and the menu path for closing comparison reachable",
                viewerLayoutToolbar.Visibility == Visibility.Visible
                && viewerWorkspaceOptions.Visibility == Visibility.Visible
                && threeDViewerMode.Visibility == Visibility.Visible
                && heightOnlyViewerMode.Visibility == Visibility.Visible
                && threeDHeightViewerMode.Visibility == Visibility.Visible
                && viewerInspectionDataSwitch.Visibility == Visibility.Visible
                && !displayedOutputsAnchorable.IsVisible
                && ((AvalonDock.Layout.LayoutAnchorablePane)viewerPane).Children.Count == 1
                && viewerAnchorable.IsSelected
                && shell.Workbench.IsOutputCompareViewerActive,
                $"toolbar={viewerLayoutToolbar.Visibility};options={viewerWorkspaceOptions.Visibility};dataSwitch={viewerInspectionDataSwitch.Visibility};displayedOutputs={displayedOutputsAnchorable.IsVisible};viewerSelected={viewerAnchorable.IsSelected};compare={shell.Workbench.IsOutputCompareViewerActive}");
            Check(
                "A/B comparison expands the teaching Viewer and hides Results in Wide layout",
                canRunCompareComposition
                && shell.Workbench.IsOutputCompareViewerActive
                && dockWorkspace.HasOutputCompareAuthoringComposition
                && !dockWorkspace.HasAuthoringResultsPaneComposition
                && wideCompareColumn is not null
                && ReferenceEquals(wideCompareColumn, ((AvalonDock.Layout.LayoutAnchorablePane)viewerPane).Parent)
                && ((AvalonDock.Layout.LayoutAnchorablePane)wideResultsPaneModel).DockHeight.IsStar
                && ((AvalonDock.Layout.LayoutAnchorablePane)wideResultsPaneModel).DockHeight.Value <= 0.001
                && ((AvalonDock.Layout.LayoutAnchorablePane)wideResultsPaneModel).DockMinHeight == 0
                && wideResultsRowBeforeCompare?.MinHeight == 0
                && wideResultsSplitterRowBeforeCompare?.Height == new GridLength(0)
                && (wideCompareResultsPane is null || !wideCompareResultsPane.IsVisible)
                && wideCompareViewerBounds.Bottom >= dockWorkspace.ActualHeight * 0.99
                && shell.Workbench.IsOutputCompareViewerActive
                && ReferenceEquals(selectedStep, shell.Workbench.SelectedPipelineStep)
                 && !shell.Workbench.IsSelectedStepPreviewRunning
                 && !shell.Workbench.IsOrderedRunRunning,
                $"compare={dockWorkspace.HasOutputCompareAuthoringComposition}; resultsParent={((AvalonDock.Layout.LayoutAnchorablePane)wideResultsPaneModel).Parent}; resultsHeight={((AvalonDock.Layout.LayoutAnchorablePane)wideResultsPaneModel).DockHeight}; resultsMinHeight={((AvalonDock.Layout.LayoutAnchorablePane)wideResultsPaneModel).DockMinHeight}; resultsVisible={wideCompareResultsPane?.IsVisible}; viewerBounds={wideCompareViewerBounds}; dockHeight={dockWorkspace.ActualHeight:F0}; viewerTree={wideCompareViewerTree}; preview={shell.Workbench.IsSelectedStepPreviewRunning}; run={shell.Workbench.IsOrderedRunRunning}");
            shell.Workbench.ShowOutputCompareViewerCommand.Execute(null);
            stageHost.UpdateLayout();
            Check(
                "the selected A/B action closes compare and restores its layout controls and Results without running inspection",
                !shell.Workbench.IsOutputCompareViewerActive
                && viewerLayoutToolbar.Visibility == Visibility.Visible
                && viewerWorkspaceOptions.Visibility == Visibility.Visible
                && displayedOutputsAnchorable.IsVisible
                && viewerAnchorable.IsSelected
                && dockWorkspace.HasAuthoringResultsPaneComposition
                && wideResultsRowBeforeCompare?.MinHeight == wideResultsRowMinHeightBeforeCompare
                && wideResultsSplitterRowBeforeCompare?.Height == wideResultsSplitterHeightBeforeCompare
                && ReferenceEquals(selectedStep, shell.Workbench.SelectedPipelineStep)
                && !shell.Workbench.IsSelectedStepPreviewRunning
                && !shell.Workbench.IsOrderedRunRunning,
                $"compare={shell.Workbench.IsOutputCompareViewerActive};toolbar={viewerLayoutToolbar.Visibility};displayedOutputs={displayedOutputsAnchorable.IsVisible};viewerSelected={viewerAnchorable.IsSelected};results={dockWorkspace.HasAuthoringResultsPaneComposition};preview={shell.Workbench.IsSelectedStepPreviewRunning};run={shell.Workbench.IsOrderedRunRunning}");
            displayedOutputsAnchorable.IsSelected = true;
            displayedOutputsAnchorable.IsActive = true;
            stageHost.UpdateLayout();
            shell.Workbench.ShowOutputCompareViewerCommand.Execute(null);
            stageHost.UpdateLayout();
            Check(
                "A/B comparison selects the Viewer and hides a previously selected Displayed Outputs tab",
                shell.Workbench.IsOutputCompareViewerActive
                && !displayedOutputsAnchorable.IsVisible
                && viewerAnchorable.IsSelected
                && viewerAnchorable.IsActive,
                $"compare={shell.Workbench.IsOutputCompareViewerActive};displayedOutputs={displayedOutputsAnchorable.IsVisible};viewer={viewerAnchorable.IsSelected}/{viewerAnchorable.IsActive}");
            shell.Workbench.ShowOutputCompareViewerCommand.Execute(null);
            stageHost.UpdateLayout();
            Check(
                "closing A/B restores the prior Displayed Outputs selection",
                !shell.Workbench.IsOutputCompareViewerActive
                && displayedOutputsAnchorable.IsVisible
                && displayedOutputsAnchorable.IsSelected
                && displayedOutputsAnchorable.IsActive
                && dockWorkspace.HasAuthoringResultsPaneComposition
                && !shell.Workbench.IsSelectedStepPreviewRunning
                && !shell.Workbench.IsOrderedRunRunning,
                $"compare={shell.Workbench.IsOutputCompareViewerActive};displayedOutputs={displayedOutputsAnchorable.IsVisible};selected={displayedOutputsAnchorable.IsSelected}/{displayedOutputsAnchorable.IsActive};results={dockWorkspace.HasAuthoringResultsPaneComposition};preview={shell.Workbench.IsSelectedStepPreviewRunning};run={shell.Workbench.IsOrderedRunRunning}");
            viewerAnchorable.IsSelected = true;
            viewerAnchorable.IsActive = true;
            shell.Workbench.ShowOutputCompareViewerCommand.Execute(null);
            stageHost.UpdateLayout();
            shell.Workbench.SelectedPipelineStep = null;
            var wideCompareClosed = !shell.Workbench.IsOutputCompareViewerActive;
            shell.Workbench.SelectedPipelineStep = selectedStep;
            stageHost.UpdateLayout();
            Check(
                "Closing A/B comparison restores the Results pane and saved Wide teaching layout",
                dockWorkspace.HasAuthoringResultsPaneComposition
                && !dockWorkspace.HasOutputCompareAuthoringComposition
                && wideCompareClosed
                && wideResultsRowBeforeCompare?.MinHeight == wideResultsRowMinHeightBeforeCompare
                && wideResultsSplitterRowBeforeCompare?.Height == wideResultsSplitterHeightBeforeCompare
                && ReferenceEquals(selectedStep, shell.Workbench.SelectedPipelineStep)
                && !shell.Workbench.IsSelectedStepPreviewRunning
                && !shell.Workbench.IsOrderedRunRunning,
                $"results={dockWorkspace.HasAuthoringResultsPaneComposition}; compare={dockWorkspace.HasOutputCompareAuthoringComposition}; preview={shell.Workbench.IsSelectedStepPreviewRunning}; run={shell.Workbench.IsOrderedRunRunning}");
            var widePaneControls = FindVisualDescendants<AvalonDock.Controls.LayoutAnchorablePaneControl>(dockWorkspace).ToArray();
            var widePanes = widePaneControls
                .Where(pane => ReferenceEquals(pane.Model, supportPane)
                    || ReferenceEquals(pane.Model, inspectorPane)
                    || ReferenceEquals(pane.Model, viewerPane))
                .ToArray();
            var wideResultsPaneControl = widePaneControls.Single(pane => ReferenceEquals(pane.Model, wideResultsPaneModel));
            var wideSupportWidth = widePanes.Single(pane => ReferenceEquals(pane.Model, supportPane)).ActualWidth;
            var wideInspectorWidth = widePanes.Single(pane => ReferenceEquals(pane.Model, inspectorPane)).ActualWidth;
            var wideViewerWidth = widePanes.Single(pane => ReferenceEquals(pane.Model, viewerPane)).ActualWidth;
            var wideViewerBounds = widePanes.Single(pane => ReferenceEquals(pane.Model, viewerPane))
                .TransformToAncestor(dockWorkspace).TransformBounds(new Rect(widePanes.Single(pane => ReferenceEquals(pane.Model, viewerPane)).RenderSize));
            var wideHostWidth = stageHost.ActualWidth;
            var wideDockWidth = dockWorkspace.ActualWidth;
            var widePaneSummary = string.Join(',', widePanes.Select(pane => $"{pane.ActualWidth:F0}/{pane.IsVisible}"));
            var wideWeights = $"{((AvalonDock.Layout.LayoutAnchorablePane)supportPane).DockWidth.Value:F2}+{((AvalonDock.Layout.LayoutAnchorablePane)inspectorPane).DockWidth.Value:F2}+{((AvalonDock.Layout.LayoutPanel)((AvalonDock.Layout.LayoutAnchorablePane)viewerPane).Parent).DockWidth.Value:F2}";
            dockWorkspace.ApplyPresentationState(OpenVisionDockPresentationState.Default with
            {
                Wide = OpenVisionDockPresentationState.Default.Wide with
                {
                    AuthoringSupport = 1.30,
                    AuthoringInspector = 1.87,
                    AuthoringViewer = 6.84,
                },
            });
            var appliedWideSupportWeight = ((AvalonDock.Layout.LayoutAnchorablePane)supportPane).DockWidth.Value;
            var appliedWideInspectorWeight = ((AvalonDock.Layout.LayoutAnchorablePane)inspectorPane).DockWidth.Value;
            var appliedWideViewerWeight = ((AvalonDock.Layout.LayoutPanel)((AvalonDock.Layout.LayoutAnchorablePane)viewerPane).Parent).DockWidth.Value;
            stageHost.UpdateLayout();
            var recoveredWidePaneControls = FindVisualDescendants<AvalonDock.Controls.LayoutAnchorablePaneControl>(dockWorkspace).ToArray();
            var recoveredWideSupportWidth = recoveredWidePaneControls.Single(pane => ReferenceEquals(pane.Model, supportPane)).ActualWidth;
            var recoveredWideInspectorWidth = recoveredWidePaneControls.Single(pane => ReferenceEquals(pane.Model, inspectorPane)).ActualWidth;
            var recoveredWideViewerWidth = recoveredWidePaneControls.Single(pane => ReferenceEquals(pane.Model, viewerPane)).ActualWidth;
            Check(
                "R19 Wide authoring keeps Recipe Flow and Tool settings readable with narrow saved ratios",
                recoveredWideSupportWidth >= 413
                && recoveredWideInspectorWidth >= 319
                && recoveredWideViewerWidth >= 700
                && Math.Abs(appliedWideSupportWeight - 1.30) < 0.01
                && Math.Abs(appliedWideInspectorWeight - 1.87) < 0.01
                && Math.Abs(appliedWideViewerWeight - 6.84) < 0.01
                && ReferenceEquals(selectedStep, shell.Workbench.SelectedPipelineStep)
                && shell.Workbench.PipelineSteps.Count == recipeStepCount
                && shell.Workbench.IsDirty == recipeDirty
                && !shell.Workbench.IsSelectedStepPreviewRunning
                && !shell.Workbench.IsOrderedRunRunning,
                $"requested={appliedWideSupportWeight:F2}+{appliedWideInspectorWeight:F2}+{appliedWideViewerWeight:F2}; support={recoveredWideSupportWidth:F0}; inspector={recoveredWideInspectorWidth:F0}; viewer={recoveredWideViewerWidth:F0}; selected={shell.Workbench.SelectedPipelineStep?.Id}; steps={shell.Workbench.PipelineSteps.Count}; dirty={shell.Workbench.IsDirty}; preview={shell.Workbench.IsSelectedStepPreviewRunning}; recipe={shell.Workbench.IsOrderedRunRunning}");
            Check(
                "Teaching keeps one pane heading without duplicate dock title or empty single-tab strip",
                dockWorkspace.IsAuthoringLayout
                && !FindVisualDescendants<AvalonDock.Controls.AnchorablePaneTitle>(dockWorkspace).Any(title => title.IsVisible)
                && recoveredWidePaneControls.Where(pane => pane.Items.Count == 1).All(pane =>
                    pane.Template.FindName("PaneTabHeader", pane) is Border { ActualHeight: 0 }),
                $"authoring={dockWorkspace.IsAuthoringLayout}; titles={string.Join(',', FindVisualDescendants<AvalonDock.Controls.AnchorablePaneTitle>(dockWorkspace).Select(title => title.IsVisible))}; strips={string.Join(',', recoveredWidePaneControls.Where(pane => pane.Items.Count == 1).Select(pane => (pane.Template.FindName("PaneTabHeader", pane) as Border)?.ActualHeight))}");
            dockWorkspace.ResetPresentationState();
            stageHost.UpdateLayout();
            var compactWorkbench = new ToolRecipeWorkbenchView
            {
                DataContext = shell,
                ViewerContent = viewerOwner,
            };
            var compactDockWorkspace = (OpenVisionDockWorkspaceView)compactWorkbench.FindName("DockWorkspace");
            var compactWorkbenchPane = (AvalonDock.Layout.LayoutPanel)compactDockWorkspace.FindName("workbenchPane");
            var compactResultsPaneModel = compactDockWorkspace.FindName("resultsPane");
            var compactInspectorPane = compactDockWorkspace.FindName("toolInspectorPane");
            var compactInspectorAnchorable = (AvalonDock.Layout.LayoutAnchorable)compactDockWorkspace.FindName("toolInspectorAnchorable");
            var compactViewerPane = compactDockWorkspace.FindName("primaryPane");
            compactDockWorkspace.ApplyPresentationState(OpenVisionDockPresentationState.Default with
            {
                Compact = OpenVisionDockPresentationState.Default.Compact with
                {
                    AuthoringSupport = 0.35,
                    AuthoringInspector = 1.20,
                    AuthoringViewer = 3.80,
                },
            });
            compactDockWorkspace.SetOperatorStage(OpenVisionOperatorStage.Teach);
            var compactHost = new Window
            {
                Content = compactWorkbench,
                Width = Math.Min(1280, testWorkArea.Width),
                Height = Math.Min(780, testWorkArea.Height),
                Left = testLeft,
                Top = testTop,
                ShowActivated = false,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
            };
            compactHost.Show();
            compactWorkbench.ActivateSelectedToolPane();
            compactHost.UpdateLayout();
            ShellWindowNativeInterop.AppendWindowMonitorEvidence(compactHost, monitorReportPath);
            var compactResultsControlBeforeCompare = FindVisualDescendants<AvalonDock.Controls.LayoutAnchorablePaneControl>(compactDockWorkspace)
                .SingleOrDefault(pane => ReferenceEquals(pane.Model, compactResultsPaneModel));
            var compactResultsGridBeforeCompare = compactResultsControlBeforeCompare is null
                ? null
                : VisualTreeHelper.GetParent(compactResultsControlBeforeCompare) as Grid;
            var compactResultsRowIndexBeforeCompare = compactResultsControlBeforeCompare is null
                ? -1
                : Grid.GetRow(compactResultsControlBeforeCompare);
            var compactResultsRowBeforeCompare = compactResultsGridBeforeCompare is not null
                && compactResultsRowIndexBeforeCompare > 0
                && compactResultsRowIndexBeforeCompare < compactResultsGridBeforeCompare.RowDefinitions.Count
                    ? compactResultsGridBeforeCompare.RowDefinitions[compactResultsRowIndexBeforeCompare]
                    : null;
            var compactResultsSplitterRowBeforeCompare = compactResultsGridBeforeCompare is not null
                && compactResultsRowIndexBeforeCompare > 0
                    ? compactResultsGridBeforeCompare.RowDefinitions[compactResultsRowIndexBeforeCompare - 1]
                    : null;
            var compactResultsRowMinHeightBeforeCompare = compactResultsRowBeforeCompare?.MinHeight ?? double.NaN;
            var compactResultsSplitterHeightBeforeCompare = compactResultsSplitterRowBeforeCompare?.Height ?? default;
            if (canRunCompareComposition)
            {
                shell.Workbench.ShowOutputCompareViewerCommand.Execute(null);
                compactHost.UpdateLayout();
            }
            WpfScreenshotCapture.Save(
                WpfScreenshotCapture.Capture(compactWorkbench).Bitmap,
                Path.ChangeExtension(reportPath, ".ab-compare-compact.png"));
            var compactCompareViewerPane = FindVisualDescendants<AvalonDock.Controls.LayoutAnchorablePaneControl>(compactDockWorkspace)
                .Single(pane => ReferenceEquals(pane.Model, compactViewerPane));
            var compactCompareResultsPane = FindVisualDescendants<AvalonDock.Controls.LayoutAnchorablePaneControl>(compactDockWorkspace)
                .SingleOrDefault(pane => ReferenceEquals(pane.Model, compactResultsPaneModel));
            var compactCompareViewerBounds = compactCompareViewerPane.TransformToAncestor(compactDockWorkspace)
                .TransformBounds(new Rect(compactCompareViewerPane.RenderSize));
            var compactCompareViewerTree = DescribeVisualAncestors(compactCompareViewerPane, compactDockWorkspace);
            var compactCompareResultsBounds = compactCompareResultsPane?.TransformToAncestor(compactDockWorkspace)
                .TransformBounds(new Rect(compactCompareResultsPane.RenderSize)) ?? Rect.Empty;
            var compactComparePaneBounds = string.Join(';', FindVisualDescendants<AvalonDock.Controls.LayoutAnchorablePaneControl>(compactDockWorkspace)
                .Select(pane => $"{(ReferenceEquals(pane.Model, compactResultsPaneModel) ? "results" : ReferenceEquals(pane.Model, compactViewerPane) ? "viewer" : "other")}:{string.Join('|', ((AvalonDock.Layout.LayoutAnchorablePane)pane.Model).Children.Select(item => item.ContentId))}:{pane.TransformToAncestor(compactDockWorkspace).TransformBounds(new Rect(pane.RenderSize))}"));
            var compactRootChildren = string.Join(',', ((AvalonDock.Layout.LayoutPanel)compactDockWorkspace.FindName("workspaceRootPanel"))
                .Children.Select(child => child.GetType().Name));
            var compactCompareColumn = ((AvalonDock.Layout.LayoutAnchorablePane)compactResultsPaneModel).Parent as AvalonDock.Layout.LayoutPanel;
            Check(
                "A/B comparison expands the teaching Viewer and hides Results in Compact layout",
                canRunCompareComposition
                && shell.Workbench.IsOutputCompareViewerActive
                && compactDockWorkspace.HasOutputCompareAuthoringComposition
                && !compactDockWorkspace.HasAuthoringResultsPaneComposition
                && compactCompareColumn is not null
                && ReferenceEquals(compactCompareColumn, ((AvalonDock.Layout.LayoutAnchorablePane)compactViewerPane).Parent)
                && ((AvalonDock.Layout.LayoutAnchorablePane)compactResultsPaneModel).DockHeight.IsStar
                && ((AvalonDock.Layout.LayoutAnchorablePane)compactResultsPaneModel).DockHeight.Value <= 0.001
                && ((AvalonDock.Layout.LayoutAnchorablePane)compactResultsPaneModel).DockMinHeight == 0
                && compactResultsRowBeforeCompare?.MinHeight == 0
                && compactResultsSplitterRowBeforeCompare?.Height == new GridLength(0)
                && (compactCompareResultsPane is null || !compactCompareResultsPane.IsVisible)
                && compactCompareViewerBounds.Bottom >= compactDockWorkspace.ActualHeight * 0.99
                && shell.Workbench.IsOutputCompareViewerActive
                && ReferenceEquals(selectedStep, shell.Workbench.SelectedPipelineStep)
                && !shell.Workbench.IsSelectedStepPreviewRunning
                && !shell.Workbench.IsOrderedRunRunning,
                $"compare={compactDockWorkspace.HasOutputCompareAuthoringComposition}; resultsParent={((AvalonDock.Layout.LayoutAnchorablePane)compactResultsPaneModel).Parent}; resultsHeight={((AvalonDock.Layout.LayoutAnchorablePane)compactResultsPaneModel).DockHeight}; resultsMinHeight={((AvalonDock.Layout.LayoutAnchorablePane)compactResultsPaneModel).DockMinHeight}; resultsVisible={compactCompareResultsPane?.IsVisible}; viewerBounds={compactCompareViewerBounds}; dockHeight={compactDockWorkspace.ActualHeight:F0}; viewerTree={compactCompareViewerTree}; preview={shell.Workbench.IsSelectedStepPreviewRunning}; run={shell.Workbench.IsOrderedRunRunning}");
            shell.Workbench.SelectedPipelineStep = null;
            var compactCompareClosed = !shell.Workbench.IsOutputCompareViewerActive;
            shell.Workbench.SelectedPipelineStep = selectedStep;
            compactHost.UpdateLayout();
            Check(
                "Closing A/B comparison restores the Results pane and saved Compact teaching layout",
                compactDockWorkspace.HasAuthoringResultsPaneComposition
                && !compactDockWorkspace.HasOutputCompareAuthoringComposition
                && compactCompareClosed
                && compactResultsRowBeforeCompare?.MinHeight == compactResultsRowMinHeightBeforeCompare
                && compactResultsSplitterRowBeforeCompare?.Height == compactResultsSplitterHeightBeforeCompare
                && ReferenceEquals(selectedStep, shell.Workbench.SelectedPipelineStep)
                && !shell.Workbench.IsSelectedStepPreviewRunning
                && !shell.Workbench.IsOrderedRunRunning,
                $"results={compactDockWorkspace.HasAuthoringResultsPaneComposition}; compare={compactDockWorkspace.HasOutputCompareAuthoringComposition}; preview={shell.Workbench.IsSelectedStepPreviewRunning}; run={shell.Workbench.IsOrderedRunRunning}");
            var transition = workbench.VerifyFirstPaneFloatDockRoundTrip();
            Check(
                "Workbench pane Float then Dock transition",
                transition.Floated && transition.Redocked
                && transition.FloatingWindowCountAfterFloat == transition.FloatingWindowCountBefore + 1
                && transition.FloatingWindowCountAfterDock == transition.FloatingWindowCountBefore,
                transition.ToString());
            Check(
                "Workbench state owners survive dock transition",
                ReferenceEquals(workbench.DataContext, dataContextOwner)
                && ReferenceEquals(workbench.ViewerContent, viewerOwner),
                "DataContext and ViewerContent references retained");
            shell.Workbench.CompareSlotAArtifactId = previousCompareSlotA;
            shell.Workbench.CompareSlotBArtifactId = previousCompareSlotB;
            var compactSupportPane = compactDockWorkspace.FindName("dataLayersPane");
            var compactPaneControls = FindVisualDescendants<AvalonDock.Controls.LayoutAnchorablePaneControl>(compactDockWorkspace).ToArray();
            var compactPanes = compactPaneControls
                .Where(pane => ReferenceEquals(pane.Model, compactSupportPane)
                    || ReferenceEquals(pane.Model, compactViewerPane))
                .ToArray();
            var compactResultsPaneControl = compactPaneControls.Single(pane => ReferenceEquals(pane.Model, compactResultsPaneModel));
            var compactSupportWidth = compactPanes.Single(pane => ReferenceEquals(pane.Model, compactSupportPane)).ActualWidth;
            var compactViewerWidth = compactPanes.Single(pane => ReferenceEquals(pane.Model, compactViewerPane)).ActualWidth;
            var compactHostWidth = compactHost.ActualWidth;
            var compactDockWidth = compactDockWorkspace.ActualWidth;
            var compactPaneSummary = string.Join(',', compactPanes.Select(pane =>
            {
                var bounds = pane.TransformToAncestor(compactDockWorkspace).TransformBounds(new Rect(pane.RenderSize));
                var model = (AvalonDock.Layout.LayoutAnchorablePane)pane.Model;
                return $"{bounds.Left:F0}-{bounds.Right:F0}/{pane.IsVisible}/{model.Parent?.GetType().Name}/{model.Children.Count}";
            }));
            var wideResultsBounds = wideResultsPaneControl.TransformToAncestor(dockWorkspace).TransformBounds(new Rect(wideResultsPaneControl.RenderSize));
            var compactResultsBounds = compactResultsPaneControl.TransformToAncestor(compactDockWorkspace).TransformBounds(new Rect(compactResultsPaneControl.RenderSize));
            var compactViewerBounds = compactPanes.Single(pane => ReferenceEquals(pane.Model, compactViewerPane))
                .TransformToAncestor(compactDockWorkspace).TransformBounds(new Rect(compactPanes.Single(pane => ReferenceEquals(pane.Model, compactViewerPane)).RenderSize));
            var compactWeights = $"{((AvalonDock.Layout.LayoutAnchorablePane)compactSupportPane).DockWidth.Value:F2}+{((AvalonDock.Layout.LayoutPanel)((AvalonDock.Layout.LayoutAnchorablePane)compactViewerPane).Parent).DockWidth.Value:F2}";
            var widePaneWidthTotal = wideSupportWidth + wideInspectorWidth + wideViewerWidth;
            var compactPaneWidthTotal = compactSupportWidth + compactViewerWidth;
            var wideScale = widePaneWidthTotal / (301d + 277 + 833);
            var compactScale = compactPaneWidthTotal / (351d + 838);
            var wideResultsUnderViewer = wideResultsBounds.Top >= wideViewerBounds.Bottom - 1
                && Math.Abs(wideResultsBounds.Left - wideViewerBounds.Left) < 3
                && Math.Abs(wideResultsBounds.Right - wideViewerBounds.Right) < 3;
            var compactResultsUnderViewer = compactResultsBounds.Top >= compactViewerBounds.Bottom - 1
                && Math.Abs(compactResultsBounds.Left - compactViewerBounds.Left) < 3
                && Math.Abs(compactResultsBounds.Right - compactViewerBounds.Right) < 3;
            var wideResultsPane = (AvalonDock.Layout.LayoutAnchorablePane)wideResultsPaneModel;
            var compactResultsPane = (AvalonDock.Layout.LayoutAnchorablePane)compactResultsPaneModel;
            var wideViewerResultsColumn = (AvalonDock.Layout.LayoutPanel)wideResultsPane.Parent;
            var compactViewerResultsColumn = (AvalonDock.Layout.LayoutPanel)compactResultsPane.Parent;
            var wideLayoutMatchesR19 = wideWorkbenchPane.Children.Count == 3
                && ReferenceEquals(wideWorkbenchPane.Children[0], supportPane)
                && ReferenceEquals(wideWorkbenchPane.Children[1], inspectorPane)
                && ReferenceEquals(wideWorkbenchPane.Children[2], wideViewerResultsColumn)
                && wideViewerResultsColumn.Orientation == Orientation.Vertical
                && ReferenceEquals(wideViewerResultsColumn.Children[0], viewerPane)
                && ReferenceEquals(wideViewerResultsColumn.Children[1], wideResultsPaneModel);
            var compactLayoutMatchesR19 = compactWorkbenchPane.Children.Count == 2
                && ReferenceEquals(compactWorkbenchPane.Children[0], compactSupportPane)
                && ReferenceEquals(compactWorkbenchPane.Children[1], compactViewerResultsColumn)
                && compactViewerResultsColumn.Orientation == Orientation.Vertical
                && ReferenceEquals(compactViewerResultsColumn.Children[0], compactViewerPane)
                && ReferenceEquals(compactViewerResultsColumn.Children[1], compactResultsPaneModel)
                && ((AvalonDock.Layout.LayoutAnchorablePane)compactSupportPane).Children.Contains(compactInspectorAnchorable)
                && !((AvalonDock.Layout.LayoutAnchorablePane)compactInspectorPane).Children.Contains(compactInspectorAnchorable)
                && compactInspectorAnchorable.IsSelected;
            Check(
                "R19 Wide and Compact workbench columns match the authored proportions and recover a narrow saved Compact layout",
                !dockWorkspace.IsCompactLayout
                && wideLayoutMatchesR19
                && widePanes.Length == 3
                && widePanes.All(pane => pane.IsVisible)
                && Math.Abs(wideSupportWidth - Math.Max(414, widePaneWidthTotal * 0.213)) < 2
                && Math.Abs(wideInspectorWidth - (widePaneWidthTotal - wideSupportWidth) * 1.97 / 7.87) < 2
                && Math.Abs(wideViewerWidth - (widePaneWidthTotal - wideSupportWidth) * 5.90 / 7.87) < 2
                && compactDockWorkspace.IsCompactLayout
                && compactLayoutMatchesR19
                && compactPanes.Length == 2
                && compactPanes.All(pane => pane.IsVisible)
                && Math.Abs(compactSupportWidth - 351 * compactScale) < 2
                && Math.Abs(compactViewerWidth - 838 * compactScale) < 2,
                $"wideHost={wideHostWidth:F0}; wideDock={wideDockWidth:F0}; wideScale={wideScale:F3}; wideWeights={wideWeights}; wide={wideSupportWidth:F0}+{wideInspectorWidth:F0}+{wideViewerWidth:F0}[{widePaneSummary}]; compactHost={compactHostWidth:F0}; compactDock={compactDockWidth:F0}; compactScale={compactScale:F3}; compactWeights={compactWeights}; compact={compactSupportWidth:F0}+{compactViewerWidth:F0}[{compactPaneSummary}]");
            Check(
                "R19 Results is confined to the Viewer column in Wide and Compact",
                wideResultsPaneControl.IsVisible
                && compactResultsPaneControl.IsVisible
                && wideResultsBounds.Height >= 100
                && compactResultsBounds.Height >= 100
                && wideResultsUnderViewer
                && compactResultsUnderViewer,
                $"wideResults={wideResultsBounds}; wideViewer={wideViewerBounds}; compactResults={compactResultsBounds}; compactViewer={compactViewerBounds}");
            compactHost.Close();
            stageHost.UpdateLayout();
            var commandBar = (Border)stageWorkbench.FindName("WorkspaceCommandBar");
            var teachingHeader = new StudioHeaderView { DataContext = shell };
            ArrangeForTextVerification(teachingHeader, 1600);
            var loadTeachingInputButton = FindVisualDescendants<Button>(teachingHeader).Single(button =>
                System.Windows.Automation.AutomationProperties.GetAutomationId(button) == "LoadTeachingInputButton");
            var teachingMenu = FindVisualDescendants<Menu>(teachingHeader).Single();
            var inspectionNavigation = ((MenuItem)teachingMenu.Items[2]).Items.OfType<MenuItem>().Single(item =>
                AutomationProperties.GetAutomationId(item) == "OpenInspectionWorkspaceButton");
            var recipeInfoEditor = ((MenuItem)teachingMenu.Items[0]).Items.OfType<MenuItem>().Single(item =>
                AutomationProperties.GetAutomationId(item) == "OpenRecipeInfoEditor");
            var saveTeachingRecipeButton = (Button)teachingHeader.FindName("SaveTeachingRecipeHeaderButton");
            Check(
                "R19 Teaching collapses the redundant workspace label strip",
                commandBar.Visibility == Visibility.Collapsed
                && commandBar.ActualHeight < 1
                && !stageWorkbench.HasVisibleWorkspaceCommandBar,
                $"visibility={commandBar.Visibility}; actualHeight={commandBar.ActualHeight:F0}; hasVisibleBar={stageWorkbench.HasVisibleWorkspaceCommandBar}");
            Check(
                "R19 Teaching header exposes input and save while menus retain secondary commands",
                loadTeachingInputButton.Visibility == Visibility.Visible
                && saveTeachingRecipeButton.Visibility == Visibility.Visible
                && FindVisualDescendants<Wpf.Ui.Controls.Button>(teachingHeader).Count(button => button.Command is not null) == 2
                && ReferenceEquals(loadTeachingInputButton.Command, shell.Workbench.LoadC3DSourceCommand)
                && ReferenceEquals(saveTeachingRecipeButton.Command, shell.Workbench.SaveTeachingRecipeCommand)
                && ReferenceEquals(recipeInfoEditor.Command, shell.Workbench.OpenRecipeInfoEditorCommand)
                && ReferenceEquals(inspectionNavigation.Command, shell.SelectWorkspaceCommand)
                && Equals(inspectionNavigation.CommandParameter, ShellWorkspaceMode.Inspect),
                $"load={loadTeachingInputButton.Visibility}; save={saveTeachingRecipeButton.Visibility}; infoBound={ReferenceEquals(recipeInfoEditor.Command, shell.Workbench.OpenRecipeInfoEditorCommand)}; inspectParameter={inspectionNavigation.CommandParameter}");
            var selectedToolView = FindVisualDescendants<SelectedToolWorkspaceView>(stageWorkbench).Single();
            var selectedToolHeaderTitle = (TextBlock)selectedToolView.FindName("SelectedToolHeaderTitle");
            var selectedToolStepSelector = (ComboBox)selectedToolView.FindName("SelectedToolStepSelector");
            var selectorOriginal = selectedToolStepSelector.SelectedItem;
            var selectorTarget = shell.Workbench.PipelineSteps.First(step => !ReferenceEquals(step, selectorOriginal));
            selectedToolStepSelector.SelectedItem = selectorTarget;
            stageHost.UpdateLayout();
            var selectorRoundTrip = ReferenceEquals(shell.Workbench.SelectedPipelineStep, selectorTarget);
            selectedToolStepSelector.SelectedItem = selectorOriginal;
            stageHost.UpdateLayout();
            Check(
                "R19 Tool settings header selects an authored step without execution",
                !string.IsNullOrWhiteSpace(selectedToolHeaderTitle.Text)
                && selectedToolStepSelector.ItemsSource == shell.Workbench.PipelineSteps
                && selectedToolStepSelector.Items.Count == shell.Workbench.PipelineSteps.Count
                && selectorRoundTrip
                && ReferenceEquals(shell.Workbench.SelectedPipelineStep, selectorOriginal)
                && !shell.Workbench.IsSelectedStepPreviewRunning
                && !shell.Workbench.IsOrderedRunRunning,
                $"title={selectedToolHeaderTitle.Text}; items={selectedToolStepSelector.Items.Count}; roundTrip={selectorRoundTrip}; preview={shell.Workbench.IsSelectedStepPreviewRunning}; recipe={shell.Workbench.IsOrderedRunRunning}");
            var editorAllTab = (RadioButton)selectedToolView.FindName("EditorAllTab");
            var editorRegionTab = (RadioButton)selectedToolView.FindName("EditorRegionTab");
            var editorJudgmentTab = (RadioButton)selectedToolView.FindName("EditorJudgmentTab");
            var editorDetailTab = (RadioButton)selectedToolView.FindName("EditorDetailTab");
            var sectionMenuLabel = (TextBlock)selectedToolView.FindName("SelectedToolSectionMenuLabel");
            var parametersExpander = (Expander)selectedToolView.FindName("ParametersExpander");
            var regionsExpander = (Expander)selectedToolView.FindName("RegionsExpander");
            var settingsDetails = (Expander)selectedToolView.FindName("SettingsDetails");
            var editorPages = (FrameworkElement)VisualTreeHelper.GetParent(editorAllTab);
            var activeRoiSummary = (Border)selectedToolView.FindName("ActiveRoiLifecycleSummary");
            var repeatGridPanel = (Border)selectedToolView.FindName("ThicknessRepeatGridPanel");
            var numericRoiEditor = (Border)selectedToolView.FindName("GridRectangleNumericEditor");
            var referenceRoiSummary = (TextBlock)selectedToolView.FindName("ReferenceRoiSummaryText");
            var measurementRoiSummary = (TextBlock)selectedToolView.FindName("MeasurementRoiSummaryText");
            var detailContextGroup = (StackPanel)selectedToolView.FindName("SelectedToolDetailContextGroup");
            var detailOutputGroup = (StackPanel)selectedToolView.FindName("SelectedToolDetailOutputGroup");
            Check(
                "R19 Tool settings opens on All with ROI before parameters and no duplicate menu heading",
                editorAllTab.IsChecked == true
                && !sectionMenuLabel.IsVisible
                && parametersExpander.IsVisible
                && parametersExpander.IsExpanded
                && regionsExpander.IsVisible
                && regionsExpander.IsExpanded
                && !activeRoiSummary.IsVisible
                && !repeatGridPanel.IsVisible
                && !numericRoiEditor.IsVisible
                && referenceRoiSummary.Text == shell.Workbench.SelectedToolWorkspace.Regions[0].Detail
                && measurementRoiSummary.Text == shell.Workbench.SelectedToolWorkspace.Regions[1].Detail
                && regionsExpander.TranslatePoint(new Point(), selectedToolView).Y
                   < parametersExpander.TranslatePoint(new Point(), selectedToolView).Y
                && !detailContextGroup.IsVisible
                && !detailOutputGroup.IsVisible,
                $"menuVisible={sectionMenuLabel.IsVisible}; all={editorAllTab.IsChecked}; parameters={parametersExpander.IsVisible}/{parametersExpander.IsExpanded}; regions={regionsExpander.IsVisible}/{regionsExpander.IsExpanded}; repeat={repeatGridPanel.IsVisible}; roi={referenceRoiSummary.Text}; details={detailContextGroup.IsVisible}/{detailOutputGroup.IsVisible}");

            Check(
                "Required settings use non-collapsible headings and visible contents",
                !editorPages.IsVisible && !settingsDetails.IsExpanded
                && parametersExpander.IsVisible && regionsExpander.IsVisible
                && FindVisualDescendants<ToggleButton>(parametersExpander).All(button => button.Name != "SectionHeader"),
                $"tabs={editorPages.IsVisible};details={settingsDetails.IsExpanded};conditions={parametersExpander.IsVisible};regions={regionsExpander.IsVisible}");
            Check(
                "Input, ROI and conditions remain together without inspection",
                regionsExpander.IsVisible && parametersExpander.IsVisible
                && !detailContextGroup.IsVisible && !detailOutputGroup.IsVisible
                && shell.Workbench.IsDirty == recipeDirty
                && !shell.Workbench.IsSelectedStepPreviewRunning && !shell.Workbench.IsOrderedRunRunning,
                $"regions={regionsExpander.IsVisible};parameters={parametersExpander.IsVisible};dirty={shell.Workbench.IsDirty}");
            shell.Workbench.BeginTeachingSelectionCaptureCommand.Execute(null);
            shell.Workbench.UpdateTeachingSelectionCaptureState(true, 2, 2, true, "Verification candidate");
            stageHost.UpdateLayout();
            var numericEditorVisibleDuringCapture = numericRoiEditor.IsVisible;
            shell.Workbench.CancelTeachingSelectionCaptureCommand.Execute(null);
            stageHost.UpdateLayout();
            Check(
                "ROI numeric controls appear for an editable candidate and disappear after cancel",
                numericEditorVisibleDuringCapture && !numericRoiEditor.IsVisible
                && shell.Workbench.IsDirty == recipeDirty
                && !shell.Workbench.IsSelectedStepPreviewRunning
                && !shell.Workbench.IsOrderedRunRunning,
                $"editing={numericEditorVisibleDuringCapture}; afterCancel={numericRoiEditor.IsVisible}; dirty={shell.Workbench.IsDirty}");

            settingsDetails.IsExpanded = true;
            selectedToolView.UpdateLayout();
            settingsDetails.BringIntoView();
            stageHost.UpdateLayout();
            Check(
                "Details exposes secondary context and output while required settings remain present",
                detailContextGroup.IsVisible && detailOutputGroup.IsVisible
                && parametersExpander.IsVisible && regionsExpander.IsVisible
                && stageWorkbench.HasContextualSelectedStepSetup && stageWorkbench.HasVisibleInspectionContext
                && shell.Workbench.IsDirty == recipeDirty
                && !shell.Workbench.IsSelectedStepPreviewRunning && !shell.Workbench.IsOrderedRunRunning,
                $"context={detailContextGroup.IsVisible};output={detailOutputGroup.IsVisible};setup={stageWorkbench.ContextualSelectedStepSetupState}");
            var identityDetails = (Expander)selectedToolView.FindName("InspectionContextDetails");
            var stepIdentity = (TextBlock)selectedToolView.FindName("InspectionContextStepId");
            var selectedToolScrollViewer = (ScrollViewer)selectedToolView.FindName("SelectedToolScrollViewer");
            Check(
                "Selected Tool starts with internal identifiers collapsed",
                !identityDetails.IsExpanded && !stepIdentity.IsVisible,
                stageWorkbench.InspectionContextState);
            identityDetails.IsExpanded = true;
            stageHost.UpdateLayout();
            Check(
                "Expanded details retain the selected step identity without editing or execution",
                stepIdentity.IsVisible
                && stepIdentity.Text == selectedStep?.Id
                && ReferenceEquals(selectedStep, shell.Workbench.SelectedPipelineStep)
                && shell.Workbench.IsDirty == recipeDirty
                && shell.Workbench.SelectedPipelineStep?.State == selectedStepState,
                stageWorkbench.InspectionContextState);
            identityDetails.IsExpanded = false;
            settingsDetails.IsExpanded = false;
            editorAllTab.IsChecked = true;
            stageHost.UpdateLayout();
            var actionBar = (Border)selectedToolView.FindName("SelectedToolActionBar");
            var explicitRunHint = (TextBlock)selectedToolView.FindName("SelectedToolExplicitRunHint");
            var actionButtons = new[] { "PreviewActionButton", "CancelPreviewActionButton" }
                .Select(name => (FrameworkElement)selectedToolView.FindName(name)).ToArray();
            Check("Selected Tool exposes one Run command instead of separate Preview/Publish actions",
                ReferenceEquals(((System.Windows.Controls.Primitives.ButtonBase)actionButtons[0]).Command, shell.Workbench.RunSelectedStepCommand)
                && selectedToolView.FindName("SelectedToolSetupPrimaryActionPreview") is null
                && selectedToolView.FindName("SaveRecipeActionButton") is null
                && selectedToolView.FindName("PublishActionButton") is null,
                "Selected tool has one Run and Cancel surface; recipe Save stays in the shell header.");
            var cancelButton = actionButtons[^1];
            // Exercise the busiest layout without starting an inspection or replacing its binding.
            cancelButton.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Visible);
            foreach (var width in new[] { 1280d, 1920d })
            {
                stageHost.Width = width;
                stageWorkbench.ActivateSelectedToolPane();
                stageHost.UpdateLayout();
                var buttonBounds = actionButtons.Select(button => button.TransformToAncestor(actionBar).TransformBounds(new Rect(button.RenderSize))).ToArray();
                Check(
                    $"Selected Tool actions including cancellation stay inside the {width:0}px layout",
                    actionButtons.All(button => button.IsVisible && button.ActualWidth > 0)
                    && actionButtons[0].ActualWidth >= actionBar.ActualWidth * 0.55
                    && !string.IsNullOrWhiteSpace(explicitRunHint.Text)
                    && buttonBounds.All(bounds => bounds.Left >= 0 && bounds.Right <= actionBar.ActualWidth + 1 && bounds.Top >= 0 && bounds.Bottom <= actionBar.ActualHeight + 1),
                    $"bar={actionBar.ActualWidth:0.0}x{actionBar.ActualHeight:0.0}; hint={explicitRunHint.Text}; visible={string.Join(',', actionButtons.Select(button => button.IsVisible))}; buttons={string.Join(';', buttonBounds)}");
            }
            System.Windows.Data.BindingOperations.GetBindingExpression(cancelButton, UIElement.VisibilityProperty)?.UpdateTarget();
            selectedToolScrollViewer.ScrollToEnd();
            stageHost.UpdateLayout();
            var pinnedActionBounds = actionBar.TransformToAncestor(selectedToolView).TransformBounds(new Rect(actionBar.RenderSize));
            Check(
                "Selected Tool Run remains visible while long settings are scrolled",
                actionBar.IsVisible
                && pinnedActionBounds.Bottom <= selectedToolView.ActualHeight + 1
                && pinnedActionBounds.Bottom >= selectedToolView.ActualHeight - actionBar.ActualHeight - 20,
                $"action={pinnedActionBounds}; viewHeight={selectedToolView.ActualHeight:0.0}; barHeight={actionBar.ActualHeight:0.0}");
            selectedToolScrollViewer.ScrollToTop();
            stageHost.Width = 1600;
            stageHost.UpdateLayout();
            Check(
                "Collapsing details and resizing preserve selected inspection state",
                !identityDetails.IsExpanded && !stepIdentity.IsVisible
                && ReferenceEquals(selectedStep, shell.Workbench.SelectedPipelineStep)
                && shell.Workbench.PipelineSteps.Count == recipeStepCount
                && shell.Workbench.IsDirty == recipeDirty
                && shell.Workbench.SelectedPipelineStep?.State == selectedStepState,
                stageWorkbench.InspectionContextState);
            Check(
                "Initial Workbench mode composes the R19 authoring layout with bottom Results",
                stageWorkbench.OperatorStage == OpenVisionOperatorStage.Teach
                && stageWorkbench.HasTeachStageComposition
                && stageWorkbench.HasAuthoringStageComposition
                && stageWorkbench.HasAdjacentViewerOutputs
                && stageWorkbench.HasStableStageHostedDataContexts,
                $"stage={stageWorkbench.OperatorStage}; authoring={stageWorkbench.HasAuthoringStageComposition}; results={stageWorkbench.DockWorkspace.HasAuthoringResultsPaneComposition}; outputs={stageWorkbench.HasAdjacentViewerOutputs}; contexts={stageWorkbench.HasStableStageHostedDataContexts}; evidence={stageWorkbench.IsBottomPaneAttached}");

            shell.SelectWorkspaceCommand.Execute(ShellWorkspaceMode.Teach);
            stageHost.UpdateLayout();
            Check(
                "Teach composes the compact step rail, Selected Tool, Viewer, and bottom Results",
                shell.IsTeachWorkspaceSelected
                && stageWorkbench.OperatorStage == OpenVisionOperatorStage.Teach
                && stageWorkbench.HasTeachStageComposition
                && stageWorkbench.AuthoringSupportTabCount == (stageWorkbench.IsCompactDockLayout ? 2 : 1)
                && stageWorkbench.IsToolLibraryDetachedFromAuthoring
                && stageWorkbench.HasStableStageHostedDataContexts,
                $"stage={stageWorkbench.OperatorStage}; teach={stageWorkbench.HasTeachStageComposition}; tabs={stageWorkbench.AuthoringSupportTabCount}; libraryDetached={stageWorkbench.IsToolLibraryDetachedFromAuthoring}; results={stageWorkbench.DockWorkspace.HasAuthoringResultsPaneComposition}; contexts={stageWorkbench.HasStableStageHostedDataContexts}; evidence={stageWorkbench.IsBottomPaneAttached}");
            Check(
                "Authoring entry normalization preserves recipe and selected-step identity",
                ReferenceEquals(selectedStep, shell.Workbench.SelectedPipelineStep)
                && shell.Workbench.PipelineSteps.Count == recipeStepCount
                && shell.Workbench.IsDirty == recipeDirty
                && string.Equals(selectedStepState, shell.Workbench.SelectedPipelineStep?.State, StringComparison.Ordinal),
                $"selected={shell.Workbench.SelectedPipelineStep?.Id}; steps={shell.Workbench.PipelineSteps.Count}; dirty={shell.Workbench.IsDirty}");

            foreach (var catalogWidth in new[] { 1280d, 1920d })
            {
                stageHost.Width = catalogWidth;
                stageHost.Height = catalogWidth == 1280 ? 760 : 1040;
                stageWorkbench.ActivateToolLibraryPane();
                stageHost.UpdateLayout();
                Check($"R19 Tool Library opens as a modal overlay at {catalogWidth:0}px",
                    stageWorkbench.IsToolLibraryPaneVisible
                    && stageWorkbench.IsToolLibraryDetachedFromAuthoring,
                    $"visible={stageWorkbench.IsToolLibraryPaneVisible}; detached={stageWorkbench.IsToolLibraryDetachedFromAuthoring}; tabs={stageWorkbench.AuthoringSupportTabCount}");
                if (catalogWidth == 1280)
                {
                    var escape = new KeyEventArgs(
                        Keyboard.PrimaryDevice,
                        PresentationSource.FromVisual(stageWorkbench),
                        Environment.TickCount,
                        Key.Escape)
                    {
                        RoutedEvent = Keyboard.PreviewKeyDownEvent,
                    };
                    stageWorkbench.ToolLibraryOverlay.RaiseEvent(escape);
                    stageHost.UpdateLayout();
                    Check("R19 Tool Library Escape closes the overlay without changing recipe state",
                        escape.Handled
                        && !stageWorkbench.IsToolLibraryPaneVisible
                        && shell.Workbench.PipelineSteps.Count == recipeStepCount
                        && shell.Workbench.IsDirty == recipeDirty,
                        $"handled={escape.Handled}; visible={stageWorkbench.IsToolLibraryPaneVisible}; steps={shell.Workbench.PipelineSteps.Count}/{recipeStepCount}; dirty={shell.Workbench.IsDirty}/{recipeDirty}");
                    stageWorkbench.ActivateToolLibraryPane();
                    stageHost.UpdateLayout();
                }
                var library = FindVisualDescendants<ToolLibraryView>(stageWorkbench).First(view => view.IsVisible);
                var suggestions = FindVisualDescendants<ScrollViewer>(library).Single(view =>
                    System.Windows.Automation.AutomationProperties.GetAutomationId(view) == "CompatibleToolScroller");
                var catalog = FindVisualDescendants<ListBox>(library).Single();
                var details = FindVisualDescendants<Expander>(library).First(view =>
                    System.Windows.Automation.AutomationProperties.GetAutomationId(view) == "CompatibleToolDetails");
                details.IsExpanded = true;
                stageHost.UpdateLayout();
                var catalogBounds = catalog.TransformToAncestor(library).TransformBounds(new Rect(catalog.RenderSize));
                Check($"Tool details leave the complete catalog reachable at {catalogWidth:0}px", suggestions.ActualHeight <= 250
                    && catalog.ActualHeight >= 100 && catalogBounds.Bottom <= library.ActualHeight + 1
                    && shell.Workbench.IsDirty == recipeDirty && shell.Workbench.PipelineSteps.Count == recipeStepCount,
                    $"suggestions={suggestions.ActualHeight};catalog={catalogBounds};library={library.ActualHeight}");
                details.IsExpanded = false;
                stageWorkbench.CloseToolLibraryPane();
            }
            stageHost.Width = 1600;
            stageHost.Height = 900;
            stageWorkbench.ActivateSelectedToolPane();
            stageHost.UpdateLayout();

            Check(
                "ROI capture can start for the selected Thickness step",
                shell.Workbench.BeginTeachingSelectionCaptureCommand.CanExecute(null),
                shell.Workbench.TeachingSelectionCaptureInstruction);
            shell.Workbench.BeginTeachingSelectionCaptureCommand.Execute(null);
            Check(
                "active ROI Review blocks stage navigation",
                shell.Workbench.IsSelectionCandidateActive
                && !shell.SelectWorkspaceCommand.CanExecute(ShellWorkspaceMode.Inspect),
                shell.InspectionStageNavigationStatus);
            shell.SelectWorkspaceCommand.Execute(ShellWorkspaceMode.Inspect);
            Check(
                "blocked stage execution leaves Teach active",
                shell.IsTeachWorkspaceSelected,
                $"stage={shell.SelectedWorkspaceMode}; status={shell.StatusText}");
            shell.Workbench.CancelTeachingSelectionCaptureCommand.Execute(null);

            shell.SelectWorkspaceCommand.Execute(ShellWorkspaceMode.Inspect);
            stageHost.UpdateLayout();
            var stageReview = FindVisualDescendants<RecipePipelineReviewView>(stageWorkbench)
                .SingleOrDefault();
            var inspectionQueueView = FindVisualDescendants<InspectionQueueWorkspaceView>(stageWorkbench)
                .SingleOrDefault(view => view.IsVisible);
            var inspectionQueue = inspectionQueueView?.DataContext as InspectionQueueWorkspaceViewModel;
            var inspectionRunButton = FindVisualDescendants<ButtonBase>(stageWorkbench)
                .SingleOrDefault(button => string.Equals(
                    AutomationProperties.GetAutomationId(button),
                    "StartInspectionQueueRun",
                    StringComparison.Ordinal));
            var inspectionResultsButton = FindVisualDescendants<ButtonBase>(stageWorkbench)
                .SingleOrDefault(button => string.Equals(
                    AutomationProperties.GetAutomationId(button),
                    "OpenInspectionResultsButton",
                    StringComparison.Ordinal));
            Check(
                "Validate opens the checked-input queue without running until explicit start",
                shell.IsValidateWorkspaceSelected
                && stageWorkbench.OperatorStage == OpenVisionOperatorStage.Validate
                && stageWorkbench.HasValidateStageComposition
                && stageWorkbench.HasEvidenceLinkedViewerComposition
                && stageReview?.IsInspectionQueueSelected == true
                && inspectionQueue is { } queue
                && queue.Samples.Count == 0
                && !queue.IsRunning
                && !queue.RunCheckedCommand.CanExecute(null)
                && inspectionRunButton is { } startButton
                && ReferenceEquals(startButton.Command, queue.RunCheckedCommand)
                && startButton is Wpf.Ui.Controls.Button startAction
                && startAction.Icon is not null
                && startAction.CornerRadius.TopLeft == 5
                && startAction.MinHeight == 32
                && inspectionResultsButton is Wpf.Ui.Controls.Button resultsAction
                && resultsAction.IsVisible
                && resultsAction.Icon is not null
                && resultsAction.CornerRadius.TopLeft == 5
                && resultsAction.MinHeight == 32
                && stageWorkbench.HasStableStageHostedDataContexts
                && !stageWorkbench.IsBottomPaneAttached,
                $"stage={stageWorkbench.OperatorStage}; validate={stageWorkbench.HasValidateStageComposition}; linkedViewer={stageWorkbench.HasEvidenceLinkedViewerComposition}; queueTab={stageReview?.IsInspectionQueueSelected}; queueItems={inspectionQueue?.Samples.Count}; running={inspectionQueue?.IsRunning}; canRunChecked={inspectionQueue?.RunCheckedCommand.CanExecute(null)}; primaryBound={inspectionRunButton is not null && inspectionQueue is not null && ReferenceEquals(inspectionRunButton.Command, inspectionQueue.RunCheckedCommand)}; contexts={stageWorkbench.HasStableStageHostedDataContexts}; bottom={stageWorkbench.IsBottomPaneAttached}");
            stageHost.Width = 1280;
            stageHost.UpdateLayout();
            Check(
                "Compact Validate clamps restored ratios to a readable action-pane width",
                stageWorkbench.IsCompactDockLayout
                && stageWorkbench.HasReadableValidationPaneRatio
                && ReferenceEquals(selectedStep, shell.Workbench.SelectedPipelineStep)
                && shell.Workbench.PipelineSteps.Count == recipeStepCount
                && shell.Workbench.IsDirty == recipeDirty,
                $"compact={stageWorkbench.IsCompactDockLayout}; readableRatio={stageWorkbench.HasReadableValidationPaneRatio}; selected={shell.Workbench.SelectedPipelineStep?.Id}; steps={shell.Workbench.PipelineSteps.Count}; dirty={shell.Workbench.IsDirty}");
            stageHost.Width = 1600;
            stageHost.UpdateLayout();
            foreach (var section in Enum.GetValues<ValidationWorkspaceSection>())
            {
                stageWorkbench.SetValidationWorkspaceSection(section);
            }
            Check(
                "Validate exposes five local drill-down sections without leaving the stage",
                stageWorkbench.ActiveValidationWorkspaceSection == ValidationWorkspaceSection.HeldOut
                && shell.IsValidateWorkspaceSelected,
                $"section={stageWorkbench.ActiveValidationWorkspaceSection}; stage={shell.SelectedWorkspaceMode}");
            stageWorkbench.ActivateSessionLogPane();
            stageHost.UpdateLayout();
            Check(
                "Validate reveals one explicitly requested advanced support pane without execution",
                shell.IsValidateWorkspaceSelected
                && stageWorkbench.IsSessionLogPaneSelected
                && stageWorkbench.VisibleValidationPaneTabCount == 2
                && ReferenceEquals(selectedStep, shell.Workbench.SelectedPipelineStep)
                && shell.Workbench.PipelineSteps.Count == recipeStepCount
                && shell.Workbench.IsDirty == recipeDirty
                && !shell.Workbench.IsValidationSetRunning
                && !shell.Workbench.IsSelectedStepPreviewRunning,
                $"stage={stageWorkbench.OperatorStage}; sessionLog={stageWorkbench.IsSessionLogPaneSelected}; visibleTabs={stageWorkbench.VisibleValidationPaneTabCount}; selected={shell.Workbench.SelectedPipelineStep?.Id}; steps={shell.Workbench.PipelineSteps.Count}; dirty={shell.Workbench.IsDirty}; validationRunning={shell.Workbench.IsValidationSetRunning}; previewRunning={shell.Workbench.IsSelectedStepPreviewRunning}");
            shell.SelectWorkspaceCommand.Execute(ShellWorkspaceMode.Review);
            stageHost.UpdateLayout();
            Check(
                "Results pairs one dedicated read-only evidence workspace with a dominant Viewer",
                shell.IsResultsWorkspaceSelected
                && stageWorkbench.OperatorStage == OpenVisionOperatorStage.Results
                && stageWorkbench.HasResultsStageComposition
                && stageWorkbench.HasEvidenceLinkedViewerComposition
                && stageWorkbench.IsDedicatedResultsWorkspace
                && stageWorkbench.HasStableStageHostedDataContexts
                && stageWorkbench.HasLocalizedResultsNavigation
                && !stageWorkbench.IsBottomPaneAttached,
                $"stage={stageWorkbench.OperatorStage}; results={stageWorkbench.HasResultsStageComposition}; linkedViewer={stageWorkbench.HasEvidenceLinkedViewerComposition}; dedicated={stageWorkbench.IsDedicatedResultsWorkspace}; contexts={stageWorkbench.HasStableStageHostedDataContexts}; localized={stageWorkbench.HasLocalizedResultsNavigation}; bottom={stageWorkbench.IsBottomPaneAttached}");
            var resultsSelectedStep = shell.Workbench.SelectedPipelineStep;
            var resultsStepCount = shell.Workbench.PipelineSteps.Count;
            var resultsDirty = shell.Workbench.IsDirty;
            var resultsViewerOutput = shell.Workbench.CurrentViewerOutputSummary;
            var resultsRunSummary = shell.RunSnapshotSummary;
            foreach (var section in Enum.GetValues<ResultsWorkspaceSection>())
            {
                stageWorkbench.SetResultsWorkspaceSection(section);
            }
            Check(
                "Results exposes Run Record, Output Compare, and Reports locally without mutation",
                stageWorkbench.ActiveResultsWorkspaceSection == ResultsWorkspaceSection.Reports
                && shell.IsResultsWorkspaceSelected
                && ReferenceEquals(resultsSelectedStep, shell.Workbench.SelectedPipelineStep)
                && shell.Workbench.PipelineSteps.Count == resultsStepCount
                && shell.Workbench.IsDirty == resultsDirty
                && string.Equals(resultsViewerOutput, shell.Workbench.CurrentViewerOutputSummary, StringComparison.Ordinal)
                && string.Equals(resultsRunSummary, shell.RunSnapshotSummary, StringComparison.Ordinal),
                $"section={stageWorkbench.ActiveResultsWorkspaceSection}; stage={shell.SelectedWorkspaceMode}; steps={shell.Workbench.PipelineSteps.Count}; dirty={shell.Workbench.IsDirty}; output={shell.Workbench.CurrentViewerOutputSummary}");
            shell.SelectWorkspaceCommand.Execute(ShellWorkspaceMode.Expert);
            Check(
                "Advanced diagnostics remain an explicit full-layout route",
                shell.IsExpertWorkspaceSelected
                && stageWorkbench.OperatorStage == OpenVisionOperatorStage.Legacy
                && stageWorkbench.HasAllLegacySupportPaneTabs
                && stageWorkbench.GetDockingPaneContracts().Count == 12,
                $"stage={stageWorkbench.OperatorStage}; mode={shell.SelectedWorkspaceMode}; supportTabs={stageWorkbench.HasAllLegacySupportPaneTabs}; panes={stageWorkbench.GetDockingPaneContracts().Count}");
            shell.SelectWorkspaceCommand.Execute(ShellWorkspaceMode.Review);
            Check(
                "Results and Advanced navigation preserve recipe, output, and run evidence",
                ReferenceEquals(resultsSelectedStep, shell.Workbench.SelectedPipelineStep)
                && shell.Workbench.PipelineSteps.Count == resultsStepCount
                && shell.Workbench.IsDirty == resultsDirty
                && string.Equals(resultsViewerOutput, shell.Workbench.CurrentViewerOutputSummary, StringComparison.Ordinal)
                && string.Equals(resultsRunSummary, shell.RunSnapshotSummary, StringComparison.Ordinal),
                $"stage={stageWorkbench.OperatorStage}; selected={shell.Workbench.SelectedPipelineStep?.Id}; dirty={shell.Workbench.IsDirty}; output={shell.Workbench.CurrentViewerOutputSummary}");
            shell.SelectWorkspaceCommand.Execute(ShellWorkspaceMode.Workbench);
            Check(
                "all stage navigation is presentation-only",
                stageWorkbench.HasTeachStageComposition
                && stageWorkbench.HasAuthoringStageComposition
                && ReferenceEquals(selectedStep, shell.Workbench.SelectedPipelineStep)
                && shell.Workbench.PipelineSteps.Count == recipeStepCount
                && shell.Workbench.IsDirty == recipeDirty
                && string.Equals(selectedStepState, shell.Workbench.SelectedPipelineStep?.State, StringComparison.Ordinal),
                $"stage={stageWorkbench.OperatorStage}; selected={shell.Workbench.SelectedPipelineStep?.Id}; state={shell.Workbench.SelectedPipelineStep?.State}; dirty={shell.Workbench.IsDirty}");
            stageHost.Close();

            var ownerPathShell = new ShellMainWindowViewModel();
            var ownerPathRecipe = validationFixture.RecipePath;
            var ownerPathRunRecord = WriteOwnerPathRunRecord(
                validationFixture);
            var ownerPathOpenMessage = string.Empty;
            var ownerPathRunMessage = string.Empty;
            Check(
                "IA-4b owner path opens the controlled recipe, five labeled samples, and Fail Run Record",
                ownerPathShell.Workbench.TryOpenTeachingRecipe(
                    ownerPathRecipe,
                    out ownerPathOpenMessage)
                && ownerPathShell.Workbench.ValidationSetSamples.Count == 5
                && ownerPathShell.LoadRunRecord(
                    ownerPathRunRecord,
                    out ownerPathRunMessage)
                && ownerPathShell.InspectionSteps.Count == 1,
                $"{ownerPathOpenMessage}; run={ownerPathRunMessage}; samples={ownerPathShell.Workbench.ValidationSetSamples.Count}; runSteps={ownerPathShell.InspectionSteps.Count}");
            Task.Run(() =>
                    ownerPathShell.Workbench.RunValidationSetAsync())
                .GetAwaiter()
                .GetResult();
            ownerPathShell.SelectWorkspaceCommand.Execute(
                ShellWorkspaceMode.Inspect);
            var ownerPathWorkbench = new ToolRecipeWorkbenchView
            {
                DataContext = ownerPathShell,
                ViewerContent = viewerOwner,
            };
            var ownerPathHost = new Window
            {
                Content = ownerPathWorkbench,
                Width = 1600,
                Height = 900,
                Left = testLeft,
                Top = testTop,
                ShowActivated = false,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
            };
            ownerPathHost.Show();
            ownerPathHost.UpdateLayout();
            ShellWindowNativeInterop.AppendWindowMonitorEvidence(ownerPathHost, monitorReportPath);
            ownerPathWorkbench.SetValidationWorkspaceSection(
                ValidationWorkspaceSection.Samples);
            ownerPathHost.UpdateLayout();
            Check(
                "Validate exposes one stable keyboard-focusable sample-set action",
                ownerPathWorkbench.HasAccessibleValidationSampleSetAction
                && ownerPathWorkbench.HasValidationSamplesFirstUseClarity
                && !ownerPathWorkbench.IsValidationIssueNavigationVisible
                && ownerPathShell.Workbench.RunValidationSetCommand
                    .CanExecute(null),
                $"accessible={ownerPathWorkbench.HasAccessibleValidationSampleSetAction}; firstUse={ownerPathWorkbench.HasValidationSamplesFirstUseClarity}; issueNav={ownerPathWorkbench.IsValidationIssueNavigationVisible}; canRun={ownerPathShell.Workbench.RunValidationSetCommand.CanExecute(null)}");
            ownerPathWorkbench.SetValidationWorkspaceSection(
                ValidationWorkspaceSection.Failures);
            ownerPathHost.UpdateLayout();
            var ownerFailureStepId =
                ownerPathShell.Workbench.SelectedValidationSetStep?.StepId;
            Check(
                "Failure Analysis leads with selected sample rule and reason before technical evidence",
                ownerPathWorkbench.HasValidationFailureOperatorSummary
                && ownerPathWorkbench.HasValidationResultsReviewControls
                && ownerPathWorkbench.IsValidationIssueNavigationVisible,
                $"summary={ownerPathWorkbench.HasValidationFailureOperatorSummary}; reviewControls={ownerPathWorkbench.HasValidationResultsReviewControls}; issueNav={ownerPathWorkbench.IsValidationIssueNavigationVisible}; sample={ownerPathShell.Workbench.SelectedValidationSetSample?.FileName}; rule={ownerPathShell.Workbench.SelectedValidationSetStep?.ToolName}; reason={ownerPathShell.Workbench.SelectedValidationSetStep?.Evidence}");
            Check(
                "IA-4b explicit sample-set execution exposes a real failure-to-Teach route",
                ownerPathShell.Workbench.ValidationSetFailCount > 0
                && ownerPathShell.Workbench.SelectedValidationSetSample
                    ?.Status == "Fail"
                && ownerPathShell
                    .OpenSelectedValidationIssueInTeachCommand
                    .CanExecute(null),
                $"pass={ownerPathShell.Workbench.ValidationSetPassCount}; fail={ownerPathShell.Workbench.ValidationSetFailCount}; error={ownerPathShell.Workbench.ValidationSetErrorCount}; selected={ownerPathShell.Workbench.SelectedValidationSetSample?.Status}; step={ownerFailureStepId}");
            var ownerRecipePath = ownerPathShell.Workbench.RecipePath;
            var ownerSourcePath = ownerPathShell.Workbench.Source.Path;
            var ownerStepCount = ownerPathShell.Workbench.PipelineSteps.Count;
            var ownerDirty = ownerPathShell.Workbench.IsDirty;
            var ownerValidationSummary =
                ownerPathShell.Workbench.ValidationSetSummary;
            var ownerRunSummary = ownerPathShell.RunSnapshotSummary;
            var ownerInspectionSummary =
                ownerPathShell.InspectionStepSummary;
            ownerPathShell.OpenSelectedValidationIssueInTeachCommand
                .Execute(null);
            ownerPathHost.UpdateLayout();
            var ownerSelectedStepId =
                ownerPathShell.Workbench.SelectedPipelineStep?.Id;
            Check(
                "IA-4b failure opens its owning Teach step without mutation or hidden execution",
                ownerPathShell.IsTeachWorkspaceSelected
                && string.Equals(
                    ownerSelectedStepId,
                    ownerFailureStepId,
                    StringComparison.OrdinalIgnoreCase)
                && ownerPathShell.Workbench.IsDirty == ownerDirty
                && !ownerPathShell.Workbench.IsValidationSetRunning
                && !ownerPathShell.Workbench.IsSelectedStepPreviewRunning,
                $"stage={ownerPathShell.SelectedWorkspaceMode}; selected={ownerSelectedStepId}; failureStep={ownerFailureStepId}; dirty={ownerDirty}->{ownerPathShell.Workbench.IsDirty}; validationRunning={ownerPathShell.Workbench.IsValidationSetRunning}; previewRunning={ownerPathShell.Workbench.IsSelectedStepPreviewRunning}");
            var correctionContext =
                ownerPathShell.Workbench.ActiveValidationFailureCorrectionContext;
            Check(
                "Teach correction carries the failed sample, owning rule, reason, and cell summary",
                correctionContext is not null
                && correctionContext.SampleName.Length > 0
                && string.Equals(
                    correctionContext.StepId,
                    ownerFailureStepId,
                    StringComparison.OrdinalIgnoreCase)
                && correctionContext.ToolName.Length > 0
                && correctionContext.Reason.Length > 0
                && correctionContext.CellSummary.Contains(
                    "failed cells",
                    StringComparison.OrdinalIgnoreCase)
                && ownerPathWorkbench.IsFailureCorrectionContextVisible,
                $"sample={correctionContext?.SampleName}; step={correctionContext?.StepId}; rule={correctionContext?.ToolName}; reason={correctionContext?.Reason}; cells={correctionContext?.CellSummary}; card={ownerPathWorkbench.IsFailureCorrectionContextVisible}");
            ownerPathShell.SelectWorkspaceCommand.Execute(
                ShellWorkspaceMode.Inspect);
            ownerPathHost.Width = 1100;
            ownerPathHost.Height = 760;
            ownerPathHost.UpdateLayout();
            ownerPathShell.OpenSelectedValidationIssueInTeachCommand
                .Execute(null);
            ownerPathHost.UpdateLayout();
            Check(
                "Compact failure-to-Teach opens Selected Tool automatically without hidden execution",
                ownerPathShell.IsTeachWorkspaceSelected
                && ownerPathWorkbench.IsCompactDockLayout
                && ownerPathWorkbench.IsToolInspectorPaneSelected
                && ownerPathWorkbench.IsFailureCorrectionContextVisible
                && ownerPathShell.Workbench.IsDirty == ownerDirty
                && !ownerPathShell.Workbench.IsValidationSetRunning
                && !ownerPathShell.Workbench.IsSelectedStepPreviewRunning,
                $"stage={ownerPathShell.SelectedWorkspaceMode}; compact={ownerPathWorkbench.IsCompactDockLayout}; selectedTool={ownerPathWorkbench.IsToolInspectorPaneSelected}; card={ownerPathWorkbench.IsFailureCorrectionContextVisible}; dirty={ownerDirty}->{ownerPathShell.Workbench.IsDirty}; validationRunning={ownerPathShell.Workbench.IsValidationSetRunning}; previewRunning={ownerPathShell.Workbench.IsSelectedStepPreviewRunning}");
            ownerPathShell.SelectWorkspaceCommand.Execute(
                ShellWorkspaceMode.Review);
            ownerPathWorkbench.SetResultsWorkspaceSection(
                ResultsWorkspaceSection.RunRecord);
            ownerPathHost.UpdateLayout();
            Check(
                "Results leads with operator decision steps and a keyboard-focusable correction route",
                ownerPathWorkbench.HasResultsOperatorSummary
                && ownerPathShell
                    .OpenSelectedValidationIssueInTeachCommand
                    .CanExecute(null),
                $"summary={ownerPathWorkbench.HasResultsOperatorSummary}; decision={ownerPathShell.RunSnapshotSummary}; steps={ownerPathShell.InspectionStepSummary}; canFix={ownerPathShell.OpenSelectedValidationIssueInTeachCommand.CanExecute(null)}");
            // Check state on the actual Results owner; rendered disclosure is verified through the running Shell.
            var resultsDock = (OpenVisionDockWorkspaceView)ownerPathWorkbench.FindName("DockWorkspace");
            var resultsView = (ResultsWorkspaceView)resultsDock.ResultsContent!;
            var measurementDetails = (Expander)resultsView.FindName("ResultsValueValidityPanel");
            var measurementText = (TextBlock)resultsView.FindName("ResultsValueValidityValue");
            var resultBeforeDisclosure = ownerPathShell.ResultsOperatorDecisionDisplaySummary;
            var initiallyCollapsed = !measurementDetails.IsExpanded;
            measurementDetails.IsExpanded = true;
            var detailsExpanded = measurementDetails.IsExpanded;
            measurementDetails.IsExpanded = false;
            Check(
                "Results disclosure preserves the bound decision, selection, dirty state and explicit execution boundary",
                initiallyCollapsed && detailsExpanded && !measurementDetails.IsExpanded
                && measurementText.FontSize >= 12
                && ownerPathShell.ResultsOperatorDecisionDisplaySummary == resultBeforeDisclosure
                && ownerPathShell.Workbench.SelectedPipelineStep?.Id == ownerSelectedStepId
                && ownerPathShell.Workbench.IsDirty == ownerDirty
                && ownerPathShell.IsResultsWorkspaceSelected
                && !ownerPathShell.Workbench.IsValidationSetRunning
                && !ownerPathShell.Workbench.IsSelectedStepPreviewRunning,
                $"collapsed={initiallyCollapsed}; expanded={detailsExpanded}; font={measurementText.FontSize}; dirty={ownerPathShell.Workbench.IsDirty}; stage={ownerPathShell.SelectedWorkspaceMode}");
            ownerPathShell.SelectWorkspaceCommand.Execute(
                ShellWorkspaceMode.Expert);
            ownerPathHost.UpdateLayout();
            ownerPathShell.SelectWorkspaceCommand.Execute(
                ShellWorkspaceMode.Review);
            ownerPathWorkbench.SetResultsWorkspaceSection(
                ResultsWorkspaceSection.RunRecord);
            ownerPathHost.UpdateLayout();
            Check(
                "IA-4b Results to Advanced to Results preserves recipe, failure, validation, output, and Run Record evidence",
                ownerPathShell.IsResultsWorkspaceSelected
                && ownerPathWorkbench.HasResultsStageComposition
                && ownerPathWorkbench.HasStableStageHostedDataContexts
                && string.Equals(
                    ownerPathShell.Workbench.RecipePath,
                    ownerRecipePath,
                    StringComparison.OrdinalIgnoreCase)
                && string.Equals(
                    ownerPathShell.Workbench.Source.Path,
                    ownerSourcePath,
                    StringComparison.OrdinalIgnoreCase)
                && ownerPathShell.Workbench.PipelineSteps.Count
                    == ownerStepCount
                && string.Equals(
                    ownerPathShell.Workbench.SelectedPipelineStep?.Id,
                    ownerSelectedStepId,
                    StringComparison.OrdinalIgnoreCase)
                && ownerPathShell.Workbench.IsDirty == ownerDirty
                && string.Equals(
                    ownerPathShell.Workbench.ValidationSetSummary,
                    ownerValidationSummary,
                    StringComparison.Ordinal)
                && ownerPathShell.Workbench.ValidationSetFailCount > 0
                && string.Equals(
                    ownerPathShell.RunSnapshotSummary,
                    ownerRunSummary,
                    StringComparison.Ordinal)
                && string.Equals(
                    ownerPathShell.InspectionStepSummary,
                    ownerInspectionSummary,
                    StringComparison.Ordinal)
                && ownerPathShell.InspectionSteps.Count == 1,
                $"stage={ownerPathShell.SelectedWorkspaceMode}; recipe={ownerPathShell.Workbench.RecipePath}; selected={ownerPathShell.Workbench.SelectedPipelineStep?.Id}; dirty={ownerPathShell.Workbench.IsDirty}; validation={ownerPathShell.Workbench.ValidationSetSummary}; run={ownerPathShell.RunSnapshotSummary}; runSteps={ownerPathShell.InspectionSteps.Count}");

            var beforeLayoutRecipePath = ownerPathShell.Workbench.RecipePath;
            var beforeLayoutDirty = ownerPathShell.Workbench.IsDirty;
            var beforeLayoutStepCount = ownerPathShell.Workbench.PipelineSteps.Count;
            var beforeLayoutValidationSummary =
                ownerPathShell.Workbench.ValidationSetSummary;
            var customDockState = OpenVisionDockPresentationState.Default with
            {
                Wide = OpenVisionDockPresentationState.Default.Wide with
                {
                    ValidateEvidence = 1.25,
                    ValidateViewer = 3.05,
                    ResultsEvidence = 1.30,
                    ResultsViewer = 3.00,
                },
                PrimaryContentId = "displayed-outputs",
                SupportContentId = "tool-inspector",
            };
            ownerPathWorkbench.ApplyDockPresentationState(customDockState);
            ownerPathWorkbench.ResetDockPresentationState();
            var resetDockState = ownerPathWorkbench.CaptureDockPresentationState();
            Check(
                "R19 default layout keeps Recipe Flow selected beside the dedicated Tool settings panel",
                resetDockState.SupportContentId == "data-layers",
                $"support={resetDockState.SupportContentId}");
            Check(
                "layout apply and reset are presentation-only",
                string.Equals(
                    ownerPathShell.Workbench.RecipePath,
                    beforeLayoutRecipePath,
                    StringComparison.OrdinalIgnoreCase)
                && ownerPathShell.Workbench.IsDirty == beforeLayoutDirty
                && ownerPathShell.Workbench.PipelineSteps.Count
                    == beforeLayoutStepCount
                && string.Equals(
                    ownerPathShell.Workbench.ValidationSetSummary,
                    beforeLayoutValidationSummary,
                    StringComparison.Ordinal)
                && !ownerPathShell.Workbench.IsValidationSetRunning
                && !ownerPathShell.Workbench.IsSelectedStepPreviewRunning,
                $"recipe={beforeLayoutRecipePath}; dirty={beforeLayoutDirty}->{ownerPathShell.Workbench.IsDirty}; steps={beforeLayoutStepCount}->{ownerPathShell.Workbench.PipelineSteps.Count}; validationRunning={ownerPathShell.Workbench.IsValidationSetRunning}; previewRunning={ownerPathShell.Workbench.IsSelectedStepPreviewRunning}");
            ownerPathHost.Close();

            var layoutVerificationDirectory = Path.Combine(
                GetReportDirectory(reportPath),
                $"layout-profile-{Guid.NewGuid():N}");
            Directory.CreateDirectory(layoutVerificationDirectory);
            var layoutPath = Path.Combine(
                layoutVerificationDirectory,
                "studio-layout.json");
            var layoutStore = new StudioLayoutProfileStore(layoutPath);
            var savedProfile = StudioLayoutProfile.Default with
            {
                Workbench = customDockState,
                Window = new StudioWindowPlacement(
                    SystemParameters.VirtualScreenLeft + 20,
                    SystemParameters.VirtualScreenTop + 20,
                    1440,
                    900,
                    IsMaximized: false),
            };
            layoutStore.Save(savedProfile);
            var restoredLayout = layoutStore.Load();
            Check(
                "safe layout profile save and reload round-trip preserves allowlisted presentation state",
                restoredLayout.Status == StudioLayoutLoadStatus.Restored
                && restoredLayout.Profile.Workbench.Wide.ValidateEvidence
                    == 1.25
                && restoredLayout.Profile.Workbench.Wide.ValidateViewer
                    == 3.05
                && restoredLayout.Profile.Workbench.PrimaryContentId
                    == "displayed-outputs"
                && restoredLayout.Profile.Workbench.SupportContentId
                    == "tool-inspector",
                $"status={restoredLayout.Status}; evidence={restoredLayout.Profile.Workbench.Wide.ValidateEvidence}; viewer={restoredLayout.Profile.Workbench.Wide.ValidateViewer}; primary={restoredLayout.Profile.Workbench.PrimaryContentId}; support={restoredLayout.Profile.Workbench.SupportContentId}");
            Check(
                "layout save is atomic and leaves no temporary sidecar",
                !Directory.EnumerateFiles(
                        layoutVerificationDirectory,
                        "*.tmp",
                        SearchOption.TopDirectoryOnly)
                    .Any(),
                layoutVerificationDirectory);

            File.WriteAllText(layoutPath, "{ not-json");
            var corruptLayout = layoutStore.Load();
            Check(
                "corrupt layout fails safely to defaults and disables automatic overwrite",
                corruptLayout.Status == StudioLayoutLoadStatus.Corrupt
                && !corruptLayout.CanAutoSave
                && corruptLayout.Profile == StudioLayoutProfile.Default,
                $"status={corruptLayout.Status}; canAutoSave={corruptLayout.CanAutoSave}; message={corruptLayout.Message}");

            File.WriteAllText(
                layoutPath,
                JsonSerializer.Serialize(
                    savedProfile with { SchemaVersion = 999 },
                    new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    }));
            var incompatibleLayout = layoutStore.Load();
            Check(
                "incompatible layout schema fails safely to defaults",
                incompatibleLayout.Status
                    == StudioLayoutLoadStatus.Incompatible
                && !incompatibleLayout.CanAutoSave,
                $"status={incompatibleLayout.Status}; canAutoSave={incompatibleLayout.CanAutoSave}; message={incompatibleLayout.Message}");

            var unsafeDockState = customDockState with
            {
                Wide = customDockState.Wide with
                {
                    ValidateEvidence = 99,
                },
                PrimaryContentId = "unknown-content",
                SupportContentId = "unknown-support",
            };
            File.WriteAllText(
                layoutPath,
                JsonSerializer.Serialize(
                    savedProfile with
                    {
                        Workbench = unsafeDockState,
                        Window = new StudioWindowPlacement(
                            999999,
                            999999,
                            1200,
                            800,
                            IsMaximized: false),
                    },
                    new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    }));
            var sanitizedLayout = layoutStore.Load();
            Check(
                "unknown IDs invalid ratios and off-screen bounds are sanitized",
                sanitizedLayout.Status
                    == StudioLayoutLoadStatus.RestoredWithFallback
                && sanitizedLayout.Profile.Window is null
                && sanitizedLayout.Profile.Workbench.Wide.ValidateEvidence
                    == OpenVisionDockPresentationState.Default.Wide
                        .ValidateEvidence
                && sanitizedLayout.Profile.Workbench.PrimaryContentId
                    == OpenVisionDockPresentationState.Default.PrimaryContentId
                && sanitizedLayout.Profile.Workbench.SupportContentId
                    == OpenVisionDockPresentationState.Default.SupportContentId,
                $"status={sanitizedLayout.Status}; window={sanitizedLayout.Profile.Window}; evidence={sanitizedLayout.Profile.Workbench.Wide.ValidateEvidence}; primary={sanitizedLayout.Profile.Workbench.PrimaryContentId}; support={sanitizedLayout.Profile.Workbench.SupportContentId}");
            layoutStore.Reset();
            Check(
                "explicit layout reset removes only the selected profile",
                !File.Exists(layoutPath)
                && Directory.Exists(layoutVerificationDirectory),
                $"profileExists={File.Exists(layoutPath)}; directoryExists={Directory.Exists(layoutVerificationDirectory)}");

            var advancedMarker = new object();
            var advanced = new OpenVisionDockWorkspaceView
            {
                ToolLibraryContent = advancedMarker,
                DataLayersContent = advancedMarker,
                ViewerContent = advancedMarker,
                ToolInspectorContent = advancedMarker,
                EvidenceContent = advancedMarker,
                OutputCompareContent = advancedMarker,
                DisplayedOutputsContent = advancedMarker,
                LinkedViewContent = advancedMarker,
                ProfileContent = advancedMarker,
                FitDiagnosticsContent = advancedMarker,
                IntersectionEvidenceContent = advancedMarker,
                CorrespondenceEvidenceContent = advancedMarker,
            };
            var advancedContracts = advanced.GetDockingPaneContracts();
            Check("Advanced exposes twelve dock panes", advancedContracts.Count == 12 && HasExactIds(advancedContracts, WorkbenchContentIds), Describe(advancedContracts));
            Check("Advanced panes can float and remain required", advancedContracts.All(contract => contract.CanFloat && !contract.CanClose) && advancedContracts.Single(contract => contract.ContentId == "fit-diagnostics").CanHide == true, Describe(advancedContracts));
            var reactivatedAdvancedViewer = new object();
            Check(
                "Advanced reactivation owns the requested Viewer in its live presenter",
                advanced.ReactivateViewerContent(reactivatedAdvancedViewer),
                $"viewer={ReferenceEquals(advanced.ViewerContent, reactivatedAdvancedViewer)}");

            var repositoryRoot = FindRepositoryRoot();
            var xamlFiles = Directory.GetFiles(
                Path.Combine(repositoryRoot, "src"),
                "*.xaml",
                SearchOption.AllDirectories);
            var xamlDocuments = xamlFiles
                .Select(path => (Path: path, Document: XDocument.Load(path)))
                .ToArray();
            var comboBoxTags = xamlFiles
                .SelectMany(path => Regex.Matches(
                    File.ReadAllText(path),
                    "<ComboBox(?=\\s|>)[^>]*>",
                    RegexOptions.Singleline)
                    .Cast<Match>()
                    .Select(match => (Path: path, Tag: match.Value)))
                .ToArray();
            var undersizedComboBoxes = comboBoxTags
                .SelectMany(combo => Regex.Matches(
                    combo.Tag,
                    "(?:Height|MinHeight)=\"([0-9.]+)\"")
                    .Cast<Match>()
                    .Where(match => double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) < 30)
                    .Select(match => $"{Path.GetRelativePath(repositoryRoot, combo.Path)}:{match.Value}"))
                .ToArray();
            var animatedPopups = xamlFiles
                .Where(path => File.ReadAllText(path).Contains("PopupAnimation=", StringComparison.Ordinal))
                .Select(path => Path.GetRelativePath(repositoryRoot, path))
                .ToArray();
            var comboBoxStyles = xamlDocuments
                .SelectMany(owner => owner.Document
                    .Descendants()
                    .Where(element => element.Name.LocalName == "Style")
                    .Where(element => IsStyleTarget(element, "ComboBox"))
                    .Select(element => (owner.Path, Style: element)))
                .ToArray();
            var comboBoxItemStyles = xamlDocuments
                .SelectMany(owner => owner.Document
                    .Descendants()
                    .Where(element => element.Name.LocalName == "Style")
                    .Where(element => IsStyleTarget(element, "ComboBoxItem"))
                    .Select(element => (owner.Path, Style: element)))
                .ToArray();
            var undersizedComboBoxStyleSetters = comboBoxStyles
                .SelectMany(owner => owner.Style
                    .Descendants()
                    .Where(element => element.Name.LocalName == "Setter")
                    .Where(element => element.Attribute("Property")?.Value is "Height" or "MinHeight")
                    .Where(element => double.TryParse(
                        element.Attribute("Value")?.Value,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out var value) && value < 30)
                    .Select(element => $"{Path.GetRelativePath(repositoryRoot, owner.Path)}:{element.Attribute("Property")?.Value}={element.Attribute("Value")?.Value}"))
                .ToArray();
            var unsafeComboBoxTextStyles = comboBoxStyles
                .Where(owner =>
                    !HasIdealTextFormatting(owner.Style)
                    && owner.Style.Attribute("BasedOn") is null
                    || HasPixelSnapping(owner.Style))
                .Select(owner => Path.GetRelativePath(repositoryRoot, owner.Path))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var unsafeComboBoxItemTextStyles = comboBoxItemStyles
                .Where(owner =>
                    !HasIdealTextFormatting(owner.Style)
                    || HasPixelSnapping(owner.Style))
                .Select(owner => Path.GetRelativePath(repositoryRoot, owner.Path))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            Check(
                "All repository ComboBoxes use the shared 30-pixel minimum without undersized overrides",
                comboBoxTags.Length > 0
                && undersizedComboBoxes.Length == 0
                && undersizedComboBoxStyleSetters.Length == 0,
                $"count={comboBoxTags.Length}; tagUndersized={string.Join(',', undersizedComboBoxes)}; styleUndersized={string.Join(',', undersizedComboBoxStyleSetters)}");
            Check(
                "All ComboBox style owners preserve fractional-DPI glyph bounds",
                comboBoxStyles.Length == 6
                && comboBoxItemStyles.Length == 7
                && unsafeComboBoxTextStyles.Length == 0
                && unsafeComboBoxItemTextStyles.Length == 0,
                $"comboStyles={comboBoxStyles.Length}; itemStyles={comboBoxItemStyles.Length}; unsafeCombo={string.Join(',', unsafeComboBoxTextStyles)}; unsafeItems={string.Join(',', unsafeComboBoxItemTextStyles)}");
            Check(
                "Repository XAML contains no explicit popup animation",
                animatedPopups.Length == 0,
                $"animated={string.Join(',', animatedPopups)}");
            var buttonBaseTypeNames = new HashSet<string>(StringComparer.Ordinal)
            {
                "Button",
                "ToggleButton",
                "RadioButton",
                "CheckBox",
            };
            var buttonBaseControls = xamlDocuments
                .SelectMany(owner => owner.Document
                    .Descendants()
                    .Where(element => buttonBaseTypeNames.Contains(element.Name.LocalName))
                    .Select(element => (owner.Path, Control: element)))
                .ToArray();
            var buttonBaseStyles = xamlDocuments
                .SelectMany(owner => owner.Document
                    .Descendants()
                    .Where(element => element.Name.LocalName == "Style")
                    .Where(element => buttonBaseTypeNames.Contains(GetTargetTypeLocalName(element)))
                    .Select(element => (owner.Path, Style: element)))
                .ToArray();
            var unsafeButtonStyles = buttonBaseStyles
                .Where(owner => owner.Style.Attribute("BasedOn") is null
                    && !owner.Style.Descendants()
                        .Any(element => element.Name.LocalName == "Setter"
                            && element.Attribute("Property")?.Value == "Template"))
                .Select(owner => $"{Path.GetRelativePath(repositoryRoot, owner.Path)}:{GetTargetTypeLocalName(owner.Style)}")
                .ToArray();
            var appButtonTemplates = xamlDocuments
                .SelectMany(owner => owner.Document
                    .Descendants()
                    .Where(element => element.Name.LocalName == "ControlTemplate")
                    .Where(element => buttonBaseTypeNames.Contains(GetTargetTypeLocalName(element)))
                    .Where(element => !element.Ancestors().Any(ancestor =>
                        (ancestor.Name.LocalName is "Style" or "ControlTemplate")
                        && (GetTargetTypeLocalName(ancestor) is "ComboBox" or "ComboBoxItem")))
                    .Select(element => (owner.Path, Template: element)))
                .ToArray();
            var incompleteButtonTemplates = appButtonTemplates
                .Select(owner =>
                {
                    var states = owner.Template.Descendants()
                        .Where(element => element.Name.LocalName is "Trigger" or "Condition")
                        .Select(element => element.Attribute("Property")?.Value)
                        .Where(property => property is not null)
                        .ToHashSet(StringComparer.Ordinal);
                    var missing = new List<string>();
                    if (!states.Contains("IsMouseOver")) missing.Add("hover");
                    if (!states.Contains("IsPressed")) missing.Add("pressed");
                    if (!states.Contains("IsKeyboardFocused")
                        && !states.Contains("IsKeyboardFocusWithin")) missing.Add("focus");
                    if (!states.Contains("IsEnabled")) missing.Add("disabled");
                    if ((GetTargetTypeLocalName(owner.Template) is "ToggleButton" or "RadioButton")
                        && !states.Contains("IsChecked")) missing.Add("checked");
                    return (owner.Path, Type: GetTargetTypeLocalName(owner.Template), Missing: missing);
                })
                .Where(owner => owner.Missing.Count > 0)
                .Select(owner => $"{Path.GetRelativePath(repositoryRoot, owner.Path)}:{owner.Type}:{string.Join('+', owner.Missing)}")
                .ToArray();
            var hardCodedButtonGlyphs = buttonBaseControls
                .SelectMany(owner => owner.Control
                    .Descendants()
                    .Where(element => element.Name.LocalName is "Path" or "Polygon")
                    .Where(element => ReferenceEquals(
                        element.Ancestors().FirstOrDefault(ancestor => buttonBaseTypeNames.Contains(ancestor.Name.LocalName)),
                        owner.Control))
                    .SelectMany(element => element.Attributes()
                        .Where(attribute => attribute.Name.LocalName is "Stroke" or "Fill")
                        .Where(attribute => attribute.Value.StartsWith('#')
                            || attribute.Value is "White" or "Black")
                        .Select(attribute => $"{Path.GetRelativePath(repositoryRoot, owner.Path)}:{owner.Control.Name.LocalName}:{element.Name.LocalName}.{attribute.Name.LocalName}={attribute.Value}")))
                .ToArray();
            var messageDialogSource = File.ReadAllText(Path.Combine(
                repositoryRoot,
                "src",
                "OpenVisionLab.Wpf.MessageDialogs",
                "WpfMessageDialogControl.xaml.cs"));
            var shellSource = File.ReadAllText(Path.Combine(
                repositoryRoot,
                "src",
                "OpenVisionLab.ThreeD.Shell",
                "MainWindow.xaml.cs"));
            var smokeScenarioSource = File.ReadAllText(Path.Combine(
                repositoryRoot,
                "src",
                "OpenVisionLab.ThreeD.Shell",
                "Automation",
                "ShellSmokeScenarioRunner.cs"));
            var hasDialogPrimaryTarget = shellSource.Contains("new ShellSmokeScenarioRunner(", StringComparison.Ordinal)
                && smokeScenarioSource.Contains("CaptureMessageDialogForSmokeAsync(", StringComparison.Ordinal)
                && smokeScenarioSource.Contains("MessageDialogPrimaryButton", StringComparison.Ordinal);
            Check(
                "Button audit covers all declarations, style owners, custom templates, and dynamic dialog buttons",
                buttonBaseControls.Length > 0
                && buttonBaseStyles.Length > 0
                && appButtonTemplates.Length > 0
                && messageDialogSource.Contains("var button = new Button", StringComparison.Ordinal)
                && messageDialogSource.Contains("MessageDialogPrimaryButton", StringComparison.Ordinal),
                $"controls={buttonBaseControls.Length}; styles={buttonBaseStyles.Length}; templates={appButtonTemplates.Length}; dynamicDialog={messageDialogSource.Contains("var button = new Button", StringComparison.Ordinal)}; primaryId={messageDialogSource.Contains("MessageDialogPrimaryButton", StringComparison.Ordinal)}");
            Check(
                "Every explicit ButtonBase style derives from a themed base or owns its template",
                unsafeButtonStyles.Length == 0,
                $"unsafe={string.Join(',', unsafeButtonStyles)}");
            Check(
                "Every app-facing button template owns hover, pressed, focus, disabled, and checked states with semantic glyphs",
                incompleteButtonTemplates.Length == 0
                && hardCodedButtonGlyphs.Length == 0
                && hasDialogPrimaryTarget,
                $"incomplete={string.Join(',', incompleteButtonTemplates)}; glyphLeaks={string.Join(',', hardCodedButtonGlyphs)}; dialogPrimaryTarget={hasDialogPrimaryTarget}");
            var advancedXaml = File.ReadAllText(Path.Combine(
                repositoryRoot,
                "src",
                "OpenVisionLab.ThreeD.Shell",
                "MainWindow.xaml"));
            var legacyAdvancedColors = new[]
            {
                "#ffffff",
                "#d1d5db",
                "#111827",
                "#6b7280",
                "#374151",
                "#b45309",
                "#b91c1c",
                "#f9fafb",
            };
            var advancedThemeResources = new[]
            {
                "ThreeD.PanelBrush",
                "ThreeD.PanelAlternateBrush",
                "ThreeD.ViewportBrush",
                "ThreeD.DividerBrush",
                "ThreeD.TextBrush",
                "ThreeD.PrimaryTextBrush",
                "ThreeD.SecondaryTextBrush",
                "ThreeD.MutedTextBrush",
                "ThreeD.WarningBrush",
                "ThreeD.FailBrush",
            };
            Check(
                "Advanced content surfaces use semantic theme resources without the legacy light palette",
                legacyAdvancedColors.All(color => !advancedXaml.Contains(color, StringComparison.OrdinalIgnoreCase))
                && advancedThemeResources.All(resource => advancedXaml.Contains(resource, StringComparison.Ordinal)),
                $"legacy={string.Join(',', legacyAdvancedColors.Where(color => advancedXaml.Contains(color, StringComparison.OrdinalIgnoreCase)))}; semantic={advancedThemeResources.Count(resource => advancedXaml.Contains(resource, StringComparison.Ordinal))}/{advancedThemeResources.Length}");

            var themeXaml = File.ReadAllText(Path.Combine(
                repositoryRoot,
                "src",
                "OpenVisionLab.ThreeD.Shell",
                "Themes",
                "OpenVisionThreeDTheme.xaml"));
            Check(
                "Shell owns a fixed themed bottom status boundary",
                advancedXaml.Contains("x:Name=\"StudioBottomStatus\"", StringComparison.Ordinal)
                && advancedXaml.Contains("Height=\"30\"", StringComparison.Ordinal)
                && advancedXaml.Contains("Text=\"{Binding InspectionStageNavigationStatus, Converter={StaticResource ViewerRuntimeTextConverter}}\"", StringComparison.Ordinal)
                && advancedXaml.Contains("Text=\"{Binding StatusText, Converter={StaticResource ViewerRuntimeTextConverter}}\"", StringComparison.Ordinal),
                "StudioBottomStatus=30; stage and operation status bindings present");
            var dockXaml = File.ReadAllText(Path.Combine(
                repositoryRoot,
                "src",
                "OpenVisionLab.ThreeD.Docking.Controls",
                "Views",
                "OpenVisionDockWorkspaceView.xaml"));
            var controlStateMarkers = new[]
            {
                "IsMouseOver",
                "IsKeyboardFocusWithin",
                "Validation.HasError",
                "IsReadOnly",
                "IsEnabled",
                "ThreeD.AccentHoverBrush",
                "ThreeD.FocusBrush",
                "ThreeD.FailBrush",
                "ThreeD.DisabledSurfaceBrush",
            };
            var dockStateMarkers = new[]
            {
                "IsMouseOver",
                "IsSelected",
                "IsKeyboardFocused",
                "IsEnabled",
                "ThreeD.PanelAlternateBrush",
                "ThreeD.SelectedSurfaceBrush",
                "ThreeD.FocusBrush",
                "ThreeD.DisabledBrush",
            };
            Check(
                "Advanced generated controls retain semantic hover focus selected disabled read-only and validation states",
                controlStateMarkers.All(marker => themeXaml.Contains(marker, StringComparison.Ordinal))
                && dockStateMarkers.All(marker => dockXaml.Contains(marker, StringComparison.Ordinal)),
                $"controls={controlStateMarkers.Count(marker => themeXaml.Contains(marker, StringComparison.Ordinal))}/{controlStateMarkers.Length}; dockTabs={dockStateMarkers.Count(marker => dockXaml.Contains(marker, StringComparison.Ordinal))}/{dockStateMarkers.Length}");

            var calibrationMarker = new object();
            var calibration = new OpenVisionCalibrationDockWorkspaceView
            {
                ExplorerContent = calibrationMarker,
                WorkspaceContent = calibrationMarker,
                InspectorContent = calibrationMarker,
                EvidenceContent = calibrationMarker,
            };
            var calibrationContracts = calibration.GetDockingPaneContracts();
            Check("Calibration exposes four dock panes", calibrationContracts.Count == 4 && HasExactIds(calibrationContracts, CalibrationContentIds), Describe(calibrationContracts));
            Check("Calibration panes can float and cannot close", calibrationContracts.All(contract => contract.CanFloat && !contract.CanClose), Describe(calibrationContracts));
            Check("Calibration anchorables cannot hide", calibrationContracts.Where(contract => contract.CanHide.HasValue).All(contract => contract.CanHide == false), Describe(calibrationContracts));
        }
        catch (Exception exception)
        {
            lines.Add($"FAIL | unexpected exception | {exception}");
        }

        Directory.CreateDirectory(GetReportDirectory(reportPath));

        var succeeded = passed == total
            && total > 0
            && !lines.Any(line => line.StartsWith("FAIL | unexpected exception", StringComparison.Ordinal));
        lines.Add($"Result: {(succeeded ? "Pass" : "Fail")} ({passed}/{total} checks)");
        File.WriteAllLines(reportPath, lines);
        summary = $"Docking workspace verification: {(succeeded ? "Pass" : "Fail")} ({passed}/{total} checks)";
        return succeeded;
    }

    private static bool HasExactIds(
        IReadOnlyList<DockingPaneContract> contracts,
        IReadOnlyList<string> expectedIds) =>
        contracts.Select(contract => contract.ContentId).Order(StringComparer.Ordinal)
            .SequenceEqual(expectedIds.Order(StringComparer.Ordinal), StringComparer.Ordinal)
        && contracts.Select(contract => contract.ContentId).Distinct(StringComparer.Ordinal).Count() == contracts.Count;

    private static string WriteOwnerPathRunRecord(
        CompletenessValidationVerificationFixture fixture)
    {
        var path = Path.Combine(fixture.RootPath, "owner-path-run-record.json");
        var document = fixture.Document;
        var step = document.Steps.Single();
        var metrics = new[]
        {
            new InspectionRunMetric(
                "Passed cells",
                MetricKind.Count,
                2,
                "cells",
                ResultStatus.Fail),
            new InspectionRunMetric(
                "Failed cells",
                MetricKind.Count,
                2,
                "cells",
                ResultStatus.Fail),
        };
        var stepResult = new InspectionRunStepResult(
            0,
            step.Id,
            step.ToolId,
            step.ToolName,
            step.InputEntityIds,
            step.OutputEntityId,
            ResultStatus.Fail,
            "Completeness Grid Fail: 2 passed, 2 failed, 4 total cells.",
            0,
            metrics,
            []);
        var record = new InspectionRunRecord(
            "1.5",
            "run-workbench-owner-path-fixture",
            new DateTimeOffset(2026, 7, 29, 0, 0, 0, TimeSpan.Zero),
            new InspectionRunRecipe(
                "tool-recipe",
                document.SchemaVersion,
                fixture.RecipePath,
                Convert.ToHexString(SHA256.HashData(
                    File.ReadAllBytes(fixture.RecipePath)))),
            new InspectionRunSource(
                document.Source.Id,
                document.Source.Path,
                document.Source.ContentSha256!,
                document.Source.ByteLength
                    ?? new FileInfo(document.Source.Path).Length,
                document.Source.Unit),
            "Ordered Tool Recipe Replay",
            ResultStatus.Fail,
            "Executed 1 authored typed step in recipe order.",
            0,
            metrics,
            [],
            "NotCompared",
            new InspectionRunArtifacts(
                Path.Combine(fixture.RootPath, "owner-path-runner.txt"),
                null,
                null,
                path,
                null,
                null))
        {
            Steps = [stepResult],
        };
        File.WriteAllText(
            path,
            JsonSerializer.Serialize(
                record,
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Converters = { new JsonStringEnumConverter() },
                }));
        return path;
    }

    private static string FindRepositoryRoot()
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
        {
            for (var directory = new DirectoryInfo(start);
                 directory is not null;
                 directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, ThicknessRecipeRelativePath)))
                {
                    return directory.FullName;
                }
            }
        }

        throw new DirectoryNotFoundException(
            $"Could not locate repository file {ThicknessRecipeRelativePath}.");
    }

    private static void ArrangeForTextVerification(
        FrameworkElement element,
        double width)
    {
        element.Measure(new Size(width, 760));
        element.Arrange(new Rect(0, 0, width, 760));
        element.UpdateLayout();
    }

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

    private static string DescribeVisualAncestors(DependencyObject descendant, DependencyObject root)
    {
        var descriptions = new List<string>();
        for (var current = descendant; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is FrameworkElement element)
            {
                var description = $"{element.GetType().Name}#{element.Name}[{element.ActualWidth:F0}x{element.ActualHeight:F0};min={element.MinWidth:F0}x{element.MinHeight:F0};visibility={element.Visibility};row={Grid.GetRow(element)}]";
                if (element is Grid grid)
                {
                    description += $" rows={string.Join(',', grid.RowDefinitions.Select(row => $"{row.Height}:{row.ActualHeight:F0}/{row.MinHeight:F0}"))}";
                }

                descriptions.Add(description);
            }

            if (ReferenceEquals(current, root))
            {
                break;
            }
        }

        return string.Join(" <- ", descriptions);
    }

    private static bool IsStyleTarget(XElement style, string targetType) =>
        string.Equals(GetTargetTypeLocalName(style), targetType, StringComparison.Ordinal);

    private static string GetTargetTypeLocalName(XElement element)
    {
        var value = element.Attribute("TargetType")?.Value?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        const string xTypePrefix = "{x:Type ";
        if (value.StartsWith(xTypePrefix, StringComparison.Ordinal) && value.EndsWith('}'))
        {
            value = value[xTypePrefix.Length..^1];
        }

        var prefixSeparator = value.LastIndexOf(':');
        return prefixSeparator >= 0 ? value[(prefixSeparator + 1)..] : value;
    }

    private static bool HasIdealTextFormatting(XElement style) =>
        style.Descendants()
            .Where(element => element.Name.LocalName == "Setter")
            .Any(element =>
                element.Attribute("Property")?.Value == "TextOptions.TextFormattingMode"
                && element.Attribute("Value")?.Value == "Ideal");

    private static bool HasPixelSnapping(XElement style) =>
        style.DescendantsAndSelf().Any(element =>
            element.Attribute("SnapsToDevicePixels")?.Value == "True"
            || element.Attribute("UseLayoutRounding")?.Value == "True")
        || style.Descendants()
            .Where(element => element.Name.LocalName == "Setter")
            .Any(element =>
                (element.Attribute("Property")?.Value is "SnapsToDevicePixels" or "UseLayoutRounding")
                && element.Attribute("Value")?.Value == "True");

    private static string Describe(IEnumerable<DockingPaneContract> contracts) =>
        string.Join(
            "; ",
            contracts.Select(contract =>
                $"{contract.ContentId}:'{contract.Title}'[float={contract.CanFloat},close={contract.CanClose},hide={contract.CanHide?.ToString() ?? "n/a"},content={contract.HasContent}]"));
}
