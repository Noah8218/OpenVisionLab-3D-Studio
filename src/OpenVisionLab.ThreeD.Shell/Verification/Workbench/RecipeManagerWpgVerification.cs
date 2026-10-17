extern alias OvlMessageDialogs;

using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.WpfPropertyGrid;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Threading;
using OpenVisionLab;
using WpfMessageDialogButtons = OvlMessageDialogs::OpenVisionLab.Wpf.MessageDialogs.WpfMessageDialogButtons;
using WpfMessageDialogControl = OvlMessageDialogs::OpenVisionLab.Wpf.MessageDialogs.WpfMessageDialogControl;
using WpfMessageDialogKind = OvlMessageDialogs::OpenVisionLab.Wpf.MessageDialogs.WpfMessageDialogKind;
using WpfMessageDialogOptions = OvlMessageDialogs::OpenVisionLab.Wpf.MessageDialogs.WpfMessageDialogOptions;
using WpfMessageDialogResult = OvlMessageDialogs::OpenVisionLab.Wpf.MessageDialogs.WpfMessageDialogResult;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell.PropertyGrid;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Shell;

internal static class RecipeManagerWpgVerification
{
    public static bool VerifyFieldPresentation(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var lines = new List<string>
        {
            "OpenVisionLab 3D settings field presentation verification",
            $"Generated: {DateTimeOffset.Now:O}",
            $"Assembly: {typeof(RecipeManagerWpgVerification).Assembly.Location}",
            "Scope: mounted WPF field/adapter checks; actual Computer Use and physical pointer-down evidence are separate."
        };
        var passed = 0;
        var total = 0;
        var imageRoot = Path.ChangeExtension(Path.GetFullPath(reportPath), null) + "-images";
        Directory.CreateDirectory(imageRoot);
        var originalLanguage = OpenVisionLanguageService.CurrentLanguage;
        var originalShutdownMode = Application.Current.ShutdownMode;
        Window? window = null;
        void Check(string name, bool condition, string detail)
        {
            total++;
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
            if (condition) passed++;
        }
        void Layout()
        {
            window?.UpdateLayout();
            Application.Current.Dispatcher.Invoke(DispatcherPriority.ContextIdle, new Action(() => { }));
        }
        void Capture(FrameworkElement view, string name)
        {
            view.InvalidateVisual();
            foreach (var child in FindVisualChildren<FrameworkElement>(view)) child.InvalidateVisual();
            Layout();
            var offset = VisualTreeHelper.GetOffset(view);
            var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(view.ActualWidth + 6), (int)Math.Ceiling(view.ActualHeight + 6), 96, 96, PixelFormats.Pbgra32);
            var drawing = new DrawingVisual();
            using (var context = drawing.RenderOpen())
            {
                context.DrawRectangle((Brush)Application.Current.FindResource("ThreeD.PanelBrush"), null, new Rect(0, 0, view.ActualWidth + 6, view.ActualHeight + 6));
                var brush = new VisualBrush(view)
                {
                    Stretch = Stretch.None,
                    AlignmentX = AlignmentX.Left,
                    AlignmentY = AlignmentY.Top,
                    ViewboxUnits = BrushMappingMode.Absolute,
                    Viewbox = new Rect(0, 0, view.ActualWidth, view.ActualHeight)
                };
                context.DrawRectangle(brush, null, new Rect(0, 0, view.ActualWidth, view.ActualHeight));
            }
            bitmap.Render(drawing);
            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
            using var stream = File.Create(Path.Combine(imageRoot, name + ".png"));
            encoder.Save(stream);
        }
        try
        {
            Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            OpenVisionLab.ThreeD.Shell.Verification.Smoke.ShellWindowNativeInterop.TryGetLeftmostWorkArea(out var workArea);
            foreach (var language in new[] { OpenVisionLanguage.Korean, OpenVisionLanguage.English })
            {
                OpenVisionLanguageService.SetLanguage(language, save: false);
                foreach (var width in new[] { 266d, 296d })
                {
                    var label = $"{language}-{width:F0}";
                    var draft = new ThicknessStepProperties { MinimumThickness = 7.75, MaximumThickness = 8.25, MinimumValidSampleCount = 3000 };
                    var conditions = new RecipeStepPropertyGridHost
                    {
                        Width = width,
                        IsCompactPresentation = true,
                        AcceptanceParametersOnly = false,
                        SelectedObject = draft
                    };
                    var acceptance = new RecipeStepPropertyGridHost
                    {
                        Width = width,
                        IsCompactPresentation = true,
                        AcceptanceParametersOnly = true,
                        SelectedObject = draft,
                        Margin = new Thickness(0, 16, 0, 0)
                    };
                    var panel = new StackPanel { Children = { conditions, acceptance } };
                    window = new Window
                    {
                        Title = "Settings field verification",
                        Width = width + 24,
                        MinWidth = 0,
                        Height = 640,
                        Left = workArea.Left + 16,
                        Top = workArea.Top + 16,
                        WindowStyle = WindowStyle.None,
                        WindowStartupLocation = WindowStartupLocation.Manual,
                        ShowInTaskbar = false,
                        ShowActivated = false,
                        Background = (Brush)Application.Current.FindResource("ThreeD.PanelBrush"),
                        Content = panel
                    };
                    window.Show();
                    OpenVisionLab.ThreeD.Shell.Verification.Smoke.ShellWindowNativeInterop.AppendWindowMonitorEvidence(window, reportPath + ".monitor.txt");
                    Layout();
                    lines.Add($"Fields | {label}; condition metadata={conditions.VisiblePropertyCount}, editors={FindTextEditors(conditions).Count()}; acceptance metadata={acceptance.VisiblePropertyCount}, editors={FindTextEditors(acceptance).Count()}");
                    Capture(panel, label + "-normal");
                    var sample = FindTextEditors(conditions).Single(e => AutomationProperties.GetAutomationId(e) == nameof(ThicknessStepProperties.MinimumValidSampleCount));
                    var minimum = FindTextEditors(acceptance).Single(e => AutomationProperties.GetAutomationId(e) == nameof(ThicknessStepProperties.MinimumThickness));
                    var maximum = FindTextEditors(acceptance).Single(e => AutomationProperties.GetAutomationId(e) == nameof(ThicknessStepProperties.MaximumThickness));
                    var focusedSample = sample.Focus();
                    var movedToNext = sample.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                    Layout();
                    Check("compact Tab reaches the next editor without container focus", focusedSample && movedToNext && minimum.IsKeyboardFocused, $"{label}; next={Keyboard.FocusedElement?.GetType().FullName}");
                    Check("focused compact editor keeps description tooltip away from input", !ToolTipService.GetIsEnabled(minimum), label);
                    Check("same typed draft and split metadata", ReferenceEquals(conditions.SelectedObject, draft) && ReferenceEquals(acceptance.SelectedObject, draft)
                        && conditions.VisiblePropertyCount == 1 && acceptance.VisiblePropertyCount == 2,
                        $"{label}; condition={conditions.VisiblePropertyCount}; acceptance={acceptance.VisiblePropertyCount}");
                    Check("field geometry and font", new[] { sample, minimum, maximum }.All(e => Math.Abs(e.ActualHeight - 30) <= 0.5 && e.FontSize == 13 && e.FontFamily.Source.StartsWith("Segoe UI Variable Text", StringComparison.Ordinal))
                        && Math.Abs(minimum.ActualWidth - (width - 10) / 2) <= 0.5 && Math.Abs(maximum.ActualWidth - minimum.ActualWidth) <= 0.5
                        && Math.Abs(minimum.TranslatePoint(new Point(), acceptance).Y - maximum.TranslatePoint(new Point(), acceptance).Y) <= 0.5,
                        $"{label}; sample={sample.ActualWidth:F2}x{sample.ActualHeight:F2}; minimum={minimum.ActualWidth:F2}x{minimum.ActualHeight:F2}; maximum={maximum.ActualWidth:F2}x{maximum.ActualHeight:F2}");
                    var surface = (Border)sample.Template.FindName("ParameterInputSurface", sample);
                    Check("rendered normal surface uses field roles", surface.CornerRadius.TopLeft == 4 && surface.BorderThickness == new Thickness(1)
                        && surface.Background.ToString() == "#FF222A33" && surface.BorderBrush.ToString() == "#FF718293",
                        $"{label}; bg={surface.Background}; border={surface.BorderBrush}; radius={surface.CornerRadius}");
                    Check("one outer scroll owner", !FindVisualChildren<ScrollViewer>(conditions).Concat(FindVisualChildren<ScrollViewer>(acceptance)).Any(s => s.IsVisible && s.Name != "PART_ContentHost"), label);
                    var expression = minimum.GetBindingExpression(TextBox.TextProperty)!;
                    minimum.SetCurrentValue(TextBox.TextProperty, "7.8");
                    var validWithoutUpdate = expression.ValidateWithoutUpdate();
                    Check("display validation does not update source", validWithoutUpdate && draft.MinimumThickness == 7.75 && ReferenceEquals(expression, minimum.GetBindingExpression(TextBox.TextProperty)), label);
                    var validCommit = acceptance.CommitPendingEdit(out var validMessage);
                    Check("existing explicit commit still uses its converter", validCommit && draft.MinimumThickness == 7.8 && !acceptance.HasValidationErrors, $"{label}; {validMessage}");
                    minimum.SetCurrentValue(TextBox.TextProperty, "not-a-number");
                    var invalidCommit = acceptance.CommitPendingEdit(out var invalidMessage);
                    Layout();
                    var errorText = (TextBlock)minimum.Template.FindName("ParameterErrorText", minimum);
                    Check("invalid numeric input stays visible without source mutation", !invalidCommit && acceptance.HasValidationErrors && Validation.GetHasError(minimum)
                        && minimum.Text == "not-a-number" && draft.MinimumThickness == 7.8 && errorText.Visibility == Visibility.Visible && !string.IsNullOrWhiteSpace(errorText.Text)
                        && errorText.TextWrapping == TextWrapping.Wrap && minimum.ActualHeight > 30,
                        $"{label}; error={invalidMessage}; height={minimum.ActualHeight:F2}; errorHeight={errorText.ActualHeight:F2}");
                    var invalidSurface = (Border)minimum.Template.FindName("ParameterInputSurface", minimum);
                    Check("rendered changed error uses HTML colors without tooltip overlay", invalidSurface.BorderBrush.ToString() == "#FFF1C27A"
                        && invalidSurface.Background.ToString() == "#FF2A2226" && errorText.Foreground.ToString() == "#FFF29D9F" && !ToolTipService.GetIsEnabled(minimum),
                        $"{label}; border={invalidSurface.BorderBrush}; error={errorText.Foreground}; bg={invalidSurface.Background}");
                    Capture(panel, label + "-invalid");
                    minimum.SetCurrentValue(TextBox.TextProperty, "7.75");
                    var recovered = acceptance.CommitPendingEdit(out var recoveredMessage);
                    Layout();
                    Check("valid input recovers the same binding", recovered && !acceptance.HasValidationErrors && !Validation.GetHasError(minimum) && draft.MinimumThickness == 7.75
                        && ReferenceEquals(expression, minimum.GetBindingExpression(TextBox.TextProperty)), $"{label}; {recoveredMessage}");
                    maximum.Focus();
                    Layout();
                    Check("recovered unfocused editor restores description tooltip", !minimum.IsKeyboardFocusWithin && ToolTipService.GetIsEnabled(minimum), label);
                    conditions.IsEnabled = false;
                    Layout();
                    surface = (Border)sample.Template.FindName("ParameterInputSurface", sample);
                    Check("disabled editor is rendered with semantic roles", !sample.IsEnabled && surface.Background.ToString() == "#FF1C2229"
                        && sample.Foreground.ToString() == "#FF8B98A6", $"{label}; bg={surface.Background}; text={sample.Foreground}");
                    Capture(panel, label + "-disabled");
                    conditions.IsEnabled = true;
                    acceptance.IsCompactPresentation = false;
                    acceptance.AcceptanceParametersOnly = null;
                    Layout();
                    Check("noncompact presentation restores table and same source", ReferenceEquals(acceptance.SelectedObject, draft) && acceptance.VisiblePropertyCount == 3
                        && FindTextEditors(acceptance).Any(e => AutomationProperties.GetAutomationId(e) == nameof(ThicknessStepProperties.MinimumValidSampleCount))
                        && !FindVisualChildren<Border>(acceptance).Any(b => b.Name == "ParameterInputSurface"), label);
                    Capture(panel, label + "-noncompact");
                    window.Close();
                    window = null;
                }
            }
            var representative = new RecipeStepPropertyGridHost
            {
                Width = 266,
                IsCompactPresentation = true,
                SelectedObject = new WpgProbeProperties()
            };
            window = new Window
            {
                Title = "Settings editor consumers",
                Width = 290,
                MinWidth = 0,
                Height = 640,
                Left = workArea.Left + 16,
                Top = workArea.Top + 16,
                WindowStyle = WindowStyle.None,
                WindowStartupLocation = WindowStartupLocation.Manual,
                ShowInTaskbar = false,
                ShowActivated = false,
                Background = (Brush)Application.Current.FindResource("ThreeD.PanelBrush"),
                Content = representative
            };
            window.Show();
            OpenVisionLab.ThreeD.Shell.Verification.Smoke.ShellWindowNativeInterop.AppendWindowMonitorEvidence(window, reportPath + ".monitor.txt");
            Layout();
            var enumEditor = FindVisualChildren<ComboBox>(representative).First();
            Check("enum consumer preserves its real source and complete localized caption", enumEditor.FontSize == 13 && Math.Abs(enumEditor.ActualHeight - 30) <= 0.5 && enumEditor.ItemTemplate is not null
                && FindVisualChildren<TextBlock>(enumEditor).Any(text => text.IsVisible && text.Text == "Rising")
                && representative.VisiblePropertyCount == 3, $"height={enumEditor.ActualHeight:F2}; font={enumEditor.FontSize}; selected={enumEditor.SelectedValue}");
            Capture(representative, "representative-enum-bool-range");
            var enumFactory = (System.Windows.Controls.WpfPropertyGrid.Controls.EnumDropdown)enumEditor;
            var enumBinding = enumEditor.GetBindingExpression(System.Windows.Controls.WpfPropertyGrid.Controls.EnumDropdown.PropertyValueProperty);
            enumEditor.Focus();
            Layout();
            var enumOutline = FindVisualChildren<Border>(representative).Single(border => border.Name == "ParameterSelectionFocusOutline" && border.IsVisible);
            Check("focused enum has HTML outer outline without growing factory editor", enumOutline.BorderThickness == new Thickness(2) && enumOutline.CornerRadius == new CornerRadius(6)
                && enumOutline.BorderBrush.ToString() == "#FFA2DFD4" && Math.Abs(enumOutline.ActualWidth - enumEditor.ActualWidth - 4) <= 0.5
                && Math.Abs(enumOutline.ActualHeight - enumEditor.ActualHeight - 4) <= 0.5 && Math.Abs(enumEditor.ActualHeight - 30) <= 0.5,
                $"editor={enumEditor.ActualWidth:F2}x{enumEditor.ActualHeight:F2}; outline={enumOutline.ActualWidth:F2}x{enumOutline.ActualHeight:F2}");
            Check("enum outline preserves original typed factory binding", enumBinding is not null && ReferenceEquals(enumFactory.PropertyValue, enumEditor.DataContext)
                && ReferenceEquals(enumBinding, enumEditor.GetBindingExpression(System.Windows.Controls.WpfPropertyGrid.Controls.EnumDropdown.PropertyValueProperty))
                && ((WpgProbeProperties)representative.SelectedObject).Polarity == HeightDifferenceEdgePolarity.Rising, $"selected={enumEditor.SelectedValue}; DP=PropertyValue; source={enumBinding?.DataItem?.GetType().Name}");
            enumEditor.SetCurrentValue(System.Windows.Controls.Primitives.Selector.SelectedItemProperty, HeightDifferenceEdgePolarity.Falling);
            Layout();
            Check("original enum factory writes selected value into typed source", ((WpgProbeProperties)representative.SelectedObject).Polarity == HeightDifferenceEdgePolarity.Falling
                && Equals(enumFactory.PropertyValue.Value, HeightDifferenceEdgePolarity.Falling), $"selected={enumEditor.SelectedValue}; source={((WpgProbeProperties)representative.SelectedObject).Polarity}");
            Check("changed enum has HTML warning border and independent focus outline", RecipeStepPropertyGridHost.GetIsEditedValue(enumEditor)
                && enumEditor.BorderBrush.ToString() == "#FFF1C27A" && enumOutline.IsVisible && enumOutline.BorderBrush.ToString() == "#FFA2DFD4", $"border={enumEditor.BorderBrush}; outline={enumOutline.BorderBrush}");
            Capture(representative, "representative-enum-changed");
            enumFactory.PropertyValue.Value = HeightDifferenceEdgePolarity.Rising;
            Layout();
            Check("original enum factory restores rendered selection from source value", Equals(enumEditor.SelectedValue, HeightDifferenceEdgePolarity.Rising)
                && ReferenceEquals(enumBinding, enumEditor.GetBindingExpression(System.Windows.Controls.WpfPropertyGrid.Controls.EnumDropdown.PropertyValueProperty)), $"selected={enumEditor.SelectedValue}");
            Check("restored enum clears changed presentation without replacing binding", !RecipeStepPropertyGridHost.GetIsEditedValue(enumEditor)
                && enumEditor.BorderBrush.ToString() == "#FFA2DFD4", $"border={enumEditor.BorderBrush}");
            Capture(representative, "representative-enum-focused");
            enumEditor.SetCurrentValue(System.Windows.Controls.Primitives.Selector.SelectedItemProperty, HeightDifferenceEdgePolarity.Falling);
            representative.IsEnabled = false;
            Layout();
            Check("disabled enum removes outer outline", !enumOutline.IsVisible, "existing host enabled state");
            Check("disabled enum keeps disabled border despite changed selection", enumEditor.BorderBrush.ToString() == representative.FindResource("ThreeD.DividerBrush").ToString(), $"border={enumEditor.BorderBrush}");
            representative.IsEnabled = true;
            Layout();
            var booleanEditor = FindVisualChildren<CheckBox>(representative).Single();
            var booleanSource = (WpgProbeProperties)representative.SelectedObject;
            var booleanBinding = booleanEditor.GetBindingExpression(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty);
            var booleanSurface = (Border)booleanEditor.Template.FindName("ParameterCheckSurface", booleanEditor);
            Check("checked bool factory uses compact graphite surface", booleanEditor.IsChecked == true && booleanSurface.Background.ToString() == "#FF65B9AE"
                && ((System.Windows.Shapes.Path)booleanEditor.Template.FindName("ParameterCheckMark", booleanEditor)).Visibility == Visibility.Visible, $"bg={booleanSurface.Background}");
            booleanEditor.SetCurrentValue(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, false);
            booleanBinding!.UpdateSource();
            Layout();
            Check("bool factory retains two-way typed binding", !booleanSource.Enabled && ReferenceEquals(booleanBinding, booleanEditor.GetBindingExpression(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty))
                && booleanSurface.Background.ToString() == "#FF222A33", $"source={booleanSource.Enabled}; bg={booleanSurface.Background}");
            Capture(representative, "representative-bool-unchecked");
            booleanSource.Enabled = true;
            booleanBinding.UpdateTarget();
            Layout();
            Check("restored bool source renders checked mark", booleanEditor.IsChecked == true && ((System.Windows.Shapes.Path)booleanEditor.Template.FindName("ParameterCheckMark", booleanEditor)).Visibility == Visibility.Visible, "same factory editor");
            booleanEditor.IsEnabled = false;
            Layout();
            Check("disabled bool avoids platform-white surface", booleanSurface.Background.ToString() == "#FF1C2229" && booleanEditor.Foreground.ToString() == "#FF8B98A6", $"bg={booleanSurface.Background}; fg={booleanEditor.Foreground}");
            Capture(representative, "representative-bool-disabled");

            representative.SelectedObject = new FilterStepProperties { KernelSize = 3 };
            Layout();
            var kernelEditor = FindVisualChildren<TextBox>(representative).Single(editor => AutomationProperties.GetAutomationId(editor) == nameof(FilterStepProperties.KernelSize));
            var kernelUnit = FindVisualChildren<TextBlock>(representative).Single(text => text.IsVisible && text.Text == "cells");
            Check("filter kernel caption, cell unit and input width match HTML field roles", FindVisualChildren<TextBlock>(representative).Any(text => text.IsVisible && text.Text == "Kernel size")
                && kernelUnit.FontSize == 12 && Math.Abs(kernelEditor.ActualWidth + kernelUnit.ActualWidth + 6 - representative.ActualWidth) <= 0.5
                && Math.Abs(kernelEditor.ActualHeight - 30) <= 0.5, $"editor={kernelEditor.ActualWidth:F2}; unit={kernelUnit.ActualWidth:F2}; host={representative.ActualWidth:F2}");
            Capture(representative, "representative-filter-cell-unit");

            window.Close();
            window = null;
            var fixtureRoot = Path.Combine(imageRoot, "footer-fixture");
            Directory.CreateDirectory(fixtureRoot);
            var sourcePath = Path.Combine(fixtureRoot, "source.C3D");
            C3DHeightFieldSnapshot.CreateForVerification("source.settings-footer", 3, 3, [1, 2, 4, 2, 4, 8, 3, 6, 12]).SaveC3D(sourcePath);
            foreach (var language in new[] { OpenVisionLanguage.Korean, OpenVisionLanguage.English })
            {
                OpenVisionLanguageService.SetLanguage(language, save: false);
                foreach (var panelWidth in new[] { 290d, 320d })
                {
                    var width = panelWidth - 4;
                    using var workbench = new ToolWorkbenchViewModel(Path.Combine(fixtureRoot, $"recent-{language}-{width:F0}.json"));
                    workbench.SetC3DSource(sourcePath);
                    var step = AddTool(workbench, "thickness");
                    var view = new Views.Workbench.SelectedToolWorkspaceView { Width = width, DataContext = workbench };
                    window = new Window
                    {
                        Title = "Settings footer verification", Width = width + 24, MinWidth = 0, Height = 640,
                        Left = workArea.Left + 16, Top = workArea.Top + 16, WindowStyle = WindowStyle.None,
                        WindowStartupLocation = WindowStartupLocation.Manual, ShowInTaskbar = false, ShowActivated = false,
                        Background = (Brush)Application.Current.FindResource("ThreeD.PanelBrush"), Content = view
                    };
                    window.Show();
                    OpenVisionLab.ThreeD.Shell.Verification.Smoke.ShellWindowNativeInterop.AppendWindowMonitorEvidence(window, reportPath + ".monitor.txt");
                    Layout();
                    var label = $"footer-{language}-{panelWidth:F0}";
                    var footer = (Border)view.FindName("SelectedToolActionBar");
                    var draftBar = (Border)view.FindName("SelectedToolDraftBar");
                    var run = (Button)view.FindName("PreviewActionButton");
                    var menu = (Button)view.FindName("SelectedToolFooterMenuButton");
                    var apply = (Button)view.FindName("SelectedToolFooterApplyButton");
                    var discard = (Button)view.FindName("SelectedToolFooterDiscardButton");
                    var conditions = (RecipeStepPropertyGridHost)view.FindName("StepPropertyGrid");
                    var acceptance = (RecipeStepPropertyGridHost)view.FindName("AcceptancePropertyGrid");
                    var logCount = workbench.RunLog.Count;
                    var requiredSections = FindVisualChildren<Expander>(view).Where(section => section.IsVisible && ReferenceEquals(section.Style, view.FindResource("RequiredSettingsSectionStyle"))).ToArray();
                    var requiredHeaders = requiredSections.Select(section => (TextBlock)section.Template.FindName("RequiredSettingsHeader", section)).ToArray();
                    Check("Teaching section captions use the HTML type and color roles", requiredHeaders.Length >= 4
                        && requiredHeaders.All(header => header.FontFamily.Source == "Segoe UI Variable Text, Segoe UI, Malgun Gothic" && header.FontSize == 12
                            && header.FontWeight.ToOpenTypeWeight() == 650 && Math.Abs(header.ActualHeight - 17.4) <= 0.05 && header.Foreground.ToString() == "#FFADB9C6"),
                        $"{label}; headers={requiredHeaders.Length}; heights={string.Join(",", requiredHeaders.Select(header => header.ActualHeight.ToString("F2")))}");
                    var sampleField = FindVisualChildren<TextBox>(conditions).Single(editor => AutomationProperties.GetAutomationId(editor) == nameof(ThicknessStepProperties.MinimumValidSampleCount));
                    var minimumField = FindVisualChildren<TextBox>(acceptance).Single(editor => AutomationProperties.GetAutomationId(editor) == nameof(ThicknessStepProperties.MinimumThickness));
                    var sampleToLimitGap = minimumField.TranslatePoint(new Point(), view).Y - sampleField.TranslatePoint(new Point(), view).Y;
                    Check("Teaching condition-to-decision spacing matches the localhost HTML", Math.Abs(sampleToLimitGap - 110.78125) <= 0.05
                        && requiredSections.All(section => section.Margin == new Thickness()), $"{label}; gap={sampleToLimitGap:F5}; expected=110.78125");
                    Check("fixed footer compensates docking inset with HTML padding", footer.IsVisible && Math.Abs(footer.ActualWidth - panelWidth) <= 0.5
                        && footer.Margin == new Thickness(-2, 0, -2, 0)
                        && footer.Padding == new Thickness(12, 10, 12, 10) && footer.BorderThickness == new Thickness(0, 1, 0, 0), $"{label}; width={footer.ActualWidth:F2}; padding={footer.Padding}");
                    Check("run and menu geometry match HTML", Math.Abs(run.ActualHeight - 34) <= 0.5 && Math.Abs(menu.ActualHeight - 34) <= 0.5
                        && Math.Abs(menu.ActualWidth - 30) <= 0.5 && Math.Abs(run.ActualWidth - (panelWidth - 60)) <= 0.5
                        && Math.Abs(menu.TranslatePoint(new Point(), footer).X - run.TranslatePoint(new Point(run.ActualWidth, 0), footer).X - 6) <= 0.5
                        && run.FontSize == 13, $"{label}; run={run.ActualWidth:F2}x{run.ActualHeight:F2}; menu={menu.ActualWidth:F2}x{menu.ActualHeight:F2}");
                    Check("normal footer has no draft action gap or execution", draftBar.Visibility == Visibility.Collapsed && !apply.IsVisible && !discard.IsVisible
                        && workbench.RunLog.Count == logCount && !workbench.IsSelectedStepPreviewRunning && !workbench.IsOrderedRunRunning, label);
                    Check("footer keeps existing command and dual-grid commit owners", ReferenceEquals(run.Command, workbench.RunSelectedStepCommand)
                        && ReferenceEquals(discard.Command, workbench.DiscardSelectedStepParameterDraftCommand)
                        && ReferenceEquals(Behaviors.CommitPropertyGridCommandBehavior.GetCommand(apply), workbench.ApplySelectedStepParameterDraftCommand)
                        && ReferenceEquals(Behaviors.CommitPropertyGridCommandBehavior.GetPropertyGrid(apply), conditions)
                        && ReferenceEquals(Behaviors.CommitPropertyGridCommandBehavior.GetAdditionalPropertyGrid(apply), acceptance), label);
                    Capture(view, label + "-normal");
                    menu.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                    Layout();
                    Check("footer menu opens with existing workbench command owner", menu.ContextMenu!.IsOpen && ReferenceEquals(menu.ContextMenu.DataContext, workbench)
                        && ReferenceEquals(menu.ContextMenu.Items.OfType<MenuItem>().First().Command, workbench.MoveSelectedStepUpCommand), label);
                    Capture(menu.ContextMenu, label + "-menu");
                    menu.ContextMenu.IsOpen = false;
                    Layout();

                    workbench.MarkSelectedStepParameterDraftDirty();
                    Layout();
                    Check("pending draft actions stay inside fixed footer", draftBar.IsVisible && apply.IsVisible && discard.IsVisible && apply.IsEnabled && discard.IsEnabled
                        && Math.Abs(apply.ActualHeight - 26) <= 0.5 && Math.Abs(discard.ActualHeight - 26) <= 0.5
                        && apply.FontSize == 12 && discard.FontSize == 12
                        && draftBar.IsAncestorOf(apply), label);
                    Check("draft bar and primary button use HTML roles", draftBar.Padding == new Thickness(10, 8, 10, 8) && draftBar.CornerRadius.TopLeft == 4
                        && draftBar.Background.ToString() == "#FF382D20" && apply.Background.ToString() == "#FF65B9AE"
                        && apply.Foreground.ToString() == "#FF11171B" && discard.Foreground.ToString() == "#FFF3D9AE",
                        $"{label}; draft={draftBar.Background}; apply={apply.Background}/{apply.Foreground}; discard={discard.Foreground}");
                    Capture(view, label + "-pending");
                    discard.IsEnabled = false;
                    Layout();
                    Check("disabled Discard retains shared state roles", discard.Foreground.ToString() == "#FF8B98A6"
                        && discard.Background.ToString() == "#FF1E252C", $"{label}; fg={discard.Foreground}; bg={discard.Background}");
                    Capture(view, label + "-discard-disabled");
                    discard.ClearValue(UIElement.IsEnabledProperty);
                    Layout();
                    var sample = FindTextEditors(conditions).Single(e => AutomationProperties.GetAutomationId(e) == nameof(ThicknessStepProperties.MinimumValidSampleCount));
                    sample.SetCurrentValue(TextBox.TextProperty, "not-a-number");
                    _ = conditions.CommitPendingEdit(out var error);
                    Layout();
                    Check("invalid field disables fixed Apply and preserves Discard", conditions.HasValidationErrors && !apply.IsEnabled && discard.IsEnabled
                        && apply.Background.ToString() == "#FF2A3A3A" && apply.Foreground.ToString() == "#FF7F9C97", $"{label}; {error}");
                    Capture(view, label + "-invalid");
                    if (discard.Command?.CanExecute(discard.CommandParameter) == true) discard.Command.Execute(discard.CommandParameter);
                    Layout();
                    Check("fixed Discard command recovers rendered draft without execution", !workbench.HasPendingStepParameterChanges && !conditions.HasValidationErrors
                        && draftBar.Visibility == Visibility.Collapsed && workbench.RunLog.Count == logCount
                        && !workbench.IsSelectedStepPreviewRunning && !workbench.IsOrderedRunRunning, label);
                    view.IsLabPresentation = true;
                    Layout();
                    var labSections = FindVisualChildren<Expander>(view).Where(section => section.IsVisible && ReferenceEquals(section.Style, view.FindResource("RequiredSettingsSectionStyle"))).ToArray();
                    var labHeaders = labSections.Select(section => (TextBlock)section.Template.FindName("RequiredSettingsHeader", section)).ToArray();
                    Check("Lab section presentation retains its existing margin and caption roles", labSections.Length >= 4
                        && labSections.All(section => section.Margin == new Thickness(0, 0, 0, 7))
                        && labHeaders.All(header => header.FontWeight == FontWeights.SemiBold && header.Foreground.ToString() == "#FFE4E9EF" && header.Margin == new Thickness(0, 0, 0, 6)), label);
                    Check("Lab preserves body actions and hides Teaching footer", footer.Visibility == Visibility.Collapsed
                        && FindVisualChildren<Button>(view).Any(button => button.IsVisible && AutomationProperties.GetAutomationId(button) == "ApplySelectedToolParameters"
                            && ReferenceEquals(Behaviors.CommitPropertyGridCommandBehavior.GetCommand(button), workbench.ApplySelectedStepParameterDraftCommand))
                        && !conditions.IsCompactPresentation, label);
                    window.Close();
                    window = null;
                }
            }

        }
        catch (Exception exception)
        {
            Check("settings field verification completed without exception", false, exception.ToString());
        }
        finally
        {
            window?.Close();
            OpenVisionLanguageService.SetLanguage(originalLanguage, save: false);
            Application.Current.ShutdownMode = originalShutdownMode;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllLines(reportPath, lines);
        summary = $"Settings field presentation verification: {passed}/{total} checks passed.";
        return passed == total;
    }

    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var lines = new List<string>
        {
            "OpenVisionLab 3D Recipe Manager + WPG verification",
            $"Generated: {DateTimeOffset.Now:O}"
        };
        var passed = 0;
        var total = 0;
        var testDataRoot = Environment.GetEnvironmentVariable("OPENVISIONLAB_TEST_DATA_ROOT");
        var fixtureRoot = Path.Combine(
            string.IsNullOrWhiteSpace(testDataRoot) ? Path.GetTempPath() : testDataRoot,
            "OpenVisionLab.ThreeD",
            "RecipeManagerWpgVerification",
            Guid.NewGuid().ToString("N"));
        lines.Add($"Fixture root: {fixtureRoot}");

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
            Directory.CreateDirectory(fixtureRoot);
            var sourcePath = Path.Combine(fixtureRoot, "source.C3D");
            C3DHeightFieldSnapshot.CreateForVerification(
                "source.recipe-manager",
                3,
                3,
                [1, 2, 4, 2, 4, 8, 3, 6, 12]).SaveC3D(sourcePath);
            var recentPath = Path.Combine(fixtureRoot, "recent.json");
            var recipePath = Path.Combine(fixtureRoot, "recipe.ov3d-teach.json");

            var workbench = new ToolWorkbenchViewModel(recentPath);
            workbench.RecipeName = "Recipe Manager Fixture";
            workbench.SetC3DSource(sourcePath);
            var originalLanguage = OpenVisionLanguageService.CurrentLanguage;

            var firstUseRoot = Path.Combine(fixtureRoot, "first-use");
            Directory.CreateDirectory(firstUseRoot);
            var firstUseRecentPath = Path.Combine(firstUseRoot, "recent.json");
            var firstUseRecipePath = Path.Combine(firstUseRoot, "First Recipe Fixture.ov3d-recipe.json");
            var firstUse = new ToolWorkbenchViewModel(firstUseRecentPath);
            firstUse.SetInspectionFileView(true);
            Check("inspection shortcuts target the existing checked-input runner", ReferenceEquals(firstUse.RunCurrentWorkspaceCommand, firstUse.InspectionQueue.RunCheckedCommand)
                && ReferenceEquals(firstUse.RunCurrentRecipeCommand, firstUse.InspectionQueue.RunCheckedCommand)
                && ReferenceEquals(firstUse.RunCurrentValidationSetCommand, firstUse.InspectionQueue.RunCheckedCommand)
                && ReferenceEquals(firstUse.CancelCurrentWorkspaceCommand, firstUse.InspectionQueue.StopCommand), "F5/Ctrl+F5/Ctrl+Shift+F5/Escape use the queue owner");
            firstUse.SetInspectionFileView(false);
            Check("teaching shortcuts restore their existing owners", ReferenceEquals(firstUse.RunCurrentWorkspaceCommand, firstUse.RunSelectedStepCommand)
                && ReferenceEquals(firstUse.RunCurrentRecipeCommand, firstUse.RunTeachingRecipeCommand)
                && ReferenceEquals(firstUse.RunCurrentValidationSetCommand, firstUse.RunValidationSetCommand)
                && ReferenceEquals(firstUse.CancelCurrentWorkspaceCommand, firstUse.CancelActiveWorkbenchEditCommand), "context changes do not execute commands");
            var firstUseLogCount = firstUse.RunLog.Count;
            firstUse.BeginFirstRecipeSetup();
            Check(
                "first-use setup opens as a visible draft without mutating authored or execution state",
                firstUse.IsFirstRecipeSetupVisible
                && string.IsNullOrWhiteSpace(firstUse.RecipePath)
                && string.IsNullOrWhiteSpace(firstUse.Source.Path)
                && firstUse.PipelineSteps.Count == 0
                && !firstUse.IsDirty
                && firstUse.RunLog.Count == firstUseLogCount,
                $"visible={firstUse.IsFirstRecipeSetupVisible}; recipe={firstUse.RecipePath}; source={firstUse.Source.Path}; steps={firstUse.PipelineSteps.Count}; dirty={firstUse.IsDirty}; logs={firstUse.RunLog.Count}/{firstUseLogCount}");

            firstUse.FirstRecipeName = "First Recipe Fixture";
            firstUse.FirstRecipeCategory = "측정";
            firstUse.FirstRecipeInspectionTarget = "전원 모듈 A-01 패드";
            firstUse.FirstRecipeDescription = "기준 영역과 패드 영역의 높이 차이를 측정합니다.";
            firstUse.FirstRecipeFolderPath = firstUseRoot;
            firstUse.FirstRecipeSourcePath = string.Empty;
            firstUse.SelectedFirstRecipeStarter = firstUse.FirstRecipeStarterOptions.Single(option =>
                option.Id == ToolWorkbenchViewModel.ThicknessFirstRecipeStarterId);
            Check(
                "a starter requiring input stays blocked until a C3D source is chosen",
                !firstUse.IsFirstRecipeSetupValid
                && firstUse.FirstRecipeSetupStatus == firstUse.Localization.FirstRecipeSourceRequired,
                firstUse.FirstRecipeSetupStatus);
            firstUse.SelectedFirstRecipeStarter = firstUse.FirstRecipeStarterOptions.Single(option =>
                option.Id == ToolWorkbenchViewModel.EmptyFirstRecipeStarterId);
            firstUse.RememberFirstRecipeSetup = true;
            var draftReady = firstUse.TryGetFirstRecipeSetup(out var confirmedSetup, out var draftMessage);
            Check(
                "first-use setup permits creating an empty recipe before loading its optional C3D input",
                draftReady
                && firstUse.IsFirstRecipeSetupValid
                && confirmedSetup.RecipeName == "First Recipe Fixture"
                && confirmedSetup.Category == "측정"
                && confirmedSetup.InspectionTarget == "전원 모듈 A-01 패드"
                && confirmedSetup.Description == "기준 영역과 패드 영역의 높이 차이를 측정합니다."
                && string.Equals(confirmedSetup.FolderPath, firstUseRoot, StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(confirmedSetup.SourcePath)
                && confirmedSetup.StarterId == ToolWorkbenchViewModel.EmptyFirstRecipeStarterId
                && string.Equals(confirmedSetup.RecipePath, firstUseRecipePath, StringComparison.OrdinalIgnoreCase),
                draftMessage);
            Check(
                "editing a valid first-use draft still does not load a source, add a step, save, Preview, Publish, or Run",
                string.IsNullOrWhiteSpace(firstUse.RecipePath)
                && string.IsNullOrWhiteSpace(firstUse.Source.Path)
                && firstUse.PipelineSteps.Count == 0
                && !firstUse.IsDirty
                && firstUse.RunLog.Count == firstUseLogCount,
                $"recipe={firstUse.RecipePath}; source={firstUse.Source.Path}; steps={firstUse.PipelineSteps.Count}; dirty={firstUse.IsDirty}; logs={firstUse.RunLog.Count}/{firstUseLogCount}");

            var firstUsePreferenceMessage = string.Empty;
            var firstUseSaved = firstUse.TryCreateEmptyTeachingRecipe(
                confirmedSetup,
                out var firstUseSaveMessage);
            var firstUsePreferenceSaved = firstUseSaved
                && firstUse.CompleteFirstRecipeSetup(out firstUsePreferenceMessage);
            var firstUseDocument = firstUseSaved
                ? ToolRecipeDocumentStore.Load(firstUseRecipePath)
                : null;
            Check(
                "Create saves and opens a source-less zero-step recipe without running inspection",
                firstUsePreferenceSaved
                && firstUseDocument is not null
                && firstUseDocument.SchemaVersion == ToolRecipeDocument.CurrentSchemaVersion
                && firstUseDocument.Category == confirmedSetup.Category
                && firstUseDocument.InspectionTarget == confirmedSetup.InspectionTarget
                && firstUseDocument.Description == confirmedSetup.Description
                && string.IsNullOrWhiteSpace(firstUseDocument.Source.Path)
                && firstUseDocument.Steps.Count == 0
                && string.IsNullOrWhiteSpace(firstUse.Source.Path)
                && firstUse.PipelineSteps.Count == 0
                && !firstUse.IsDirty
                && !firstUse.IsFirstRecipeSetupVisible,
                $"create={firstUseSaveMessage}; preference={firstUsePreferenceMessage}; steps={firstUseDocument?.Steps.Count}");

            firstUse.SetC3DSource(sourcePath);
            var starterApplied = firstUse.TryApplyFirstRecipeStarter(
                ToolWorkbenchViewModel.ThicknessFirstRecipeStarterId,
                out var starterMessage);
            var firstUseUpdateMessage = string.Empty;
            var firstUseUpdated = starterApplied
                && firstUse.IsDirty
                && firstUse.TrySaveTeachingRecipe(firstUseRecipePath, out firstUseUpdateMessage);
            firstUseDocument = firstUseUpdated ? ToolRecipeDocumentStore.Load(firstUseRecipePath) : null;
            Check(
                "loading a C3D input and adding a starter after Create requires an explicit Save",
                firstUseUpdated
                && firstUseDocument is not null
                && string.Equals(
                    ResolvePersistedSourcePath(firstUseRecipePath, firstUseDocument.Source.Path),
                    sourcePath,
                    StringComparison.OrdinalIgnoreCase)
                && firstUseDocument.Steps.Count == 1
                && firstUseDocument.Steps[0].ToolId == "thickness"
                && !firstUse.IsDirty,
                $"starter={starterMessage}; save={firstUseUpdateMessage}; steps={firstUseDocument?.Steps.Count}");

            var firstUseBytesBeforeCollision = File.ReadAllBytes(firstUseRecipePath);
            var firstUsePathBeforeCollision = firstUse.RecipePath;
            var firstUseSourceBeforeCollision = firstUse.Source.Path;
            var duplicateCreate = firstUse.TryCreateEmptyTeachingRecipe(
                confirmedSetup with { RecipeName = "Overwrite Attempt" },
                out var duplicateCreateMessage);
            Check(
                "a colliding create leaves the open recipe, input, steps, and saved bytes unchanged",
                !duplicateCreate
                && firstUse.RecipePath == firstUsePathBeforeCollision
                && firstUse.Source.Path == firstUseSourceBeforeCollision
                && firstUse.PipelineSteps.Count == 1
                && firstUse.PipelineSteps[0].ToolId == "thickness"
                && firstUseBytesBeforeCollision.SequenceEqual(File.ReadAllBytes(firstUseRecipePath)),
                duplicateCreateMessage);

            var reopenedRecipe = new ToolWorkbenchViewModel(Path.Combine(firstUseRoot, "reopen-recent.json"));
            var recipeReopened = reopenedRecipe.TryOpenTeachingRecipe(firstUseRecipePath, out var reopenMessage);
            Check(
                "created recipe survives a save and reopen round trip with stable source and starter identity",
                recipeReopened
                && string.Equals(reopenedRecipe.Source.Path, sourcePath, StringComparison.OrdinalIgnoreCase)
                && reopenedRecipe.PipelineSteps.Count == 1
                && reopenedRecipe.PipelineSteps[0].ToolId == "thickness"
                && !reopenedRecipe.IsDirty,
                reopenMessage);

            reopenedRecipe.SelectPipelineStep(reopenedRecipe.PipelineSteps[0].Id);
            var limitsLogCount = reopenedRecipe.RunLog.Count;
            Check("new measurement reopens without acceptance limits", reopenedRecipe.IsSelectedStepAcceptanceLimitsUnconfigured
                && reopenedRecipe.ConfigureSelectedStepAcceptanceLimitsCommand.CanExecute(null), "persisted false; explicit command available");
            var readinessMethod = typeof(ToolWorkbenchViewModel).GetMethod("GetInspectionQueueRecipeReadiness", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            var limitsReadiness = ((bool CanRun, string Message))readinessMethod.Invoke(reopenedRecipe, [ToolRecipeDocumentStore.Load(firstUseRecipePath)])!;
            Check("saved unconfigured recipe blocks repeat inspection", !limitsReadiness.CanRun && limitsReadiness.Message.Contains("규격", StringComparison.Ordinal), limitsReadiness.Message);
            reopenedRecipe.ConfigureSelectedStepAcceptanceLimitsCommand.Execute(null);
            Check("explicit limits configuration is dirty and does not execute", reopenedRecipe.PipelineSteps[0].AcceptanceLimitsConfigured == true
                && !reopenedRecipe.IsSelectedStepAcceptanceLimitsUnconfigured && reopenedRecipe.IsDirty
                && !reopenedRecipe.IsSelectedStepPreviewRunning && reopenedRecipe.CurrentMeasurementOutput is null,
                $"logs before={limitsLogCount};after={reopenedRecipe.RunLog.Count}");
            var limitsSaved = reopenedRecipe.TrySaveTeachingRecipe(firstUseRecipePath, out var limitsSaveMessage);
            Check("configured limits save and reload", limitsSaved && ToolRecipeDocumentStore.Load(firstUseRecipePath).Steps[0].AcceptanceLimitsConfigured == true, limitsSaveMessage);

            var variantSourcePath = Path.Combine(firstUseRoot, "variant-source.C3D");
            C3DHeightFieldSnapshot.CreateForVerification(
                "source.recipe-manager.variant",
                3,
                3,
                [2, 3, 5, 3, 5, 9, 4, 7, 13]).SaveC3D(variantSourcePath);
            var incompatibleSourcePath = Path.Combine(firstUseRoot, "variant-incompatible.C3D");
            C3DHeightFieldSnapshot.CreateForVerification(
                "source.recipe-manager.variant-incompatible",
                4,
                4,
                Enumerable.Range(1, 16).Select(value => (double)value).ToArray()).SaveC3D(incompatibleSourcePath);
            var variantBasePath = Path.Combine(firstUseRoot, "variant-base.ov3d-recipe.json");
            var variantPath = Path.Combine(firstUseRoot, "variant-copy.ov3d-recipe.json");
            var variantBinding = ToolRecipeSelectionSourceBindingVerifier.ReadIdentity(sourcePath);
            reopenedRecipe.Source.Id = "source.variant.custom";
            reopenedRecipe.Source.SensorId = "original-source-sensor";
            reopenedRecipe.Source.MeasurementEvidence = HeightMeasurementEvidence.CalibratedPhysical(
                reopenedRecipe.Source.SensorId, "original-calibration", reopenedRecipe.Source.FrameId, "Owned fixture evidence for the original input only.");
            reopenedRecipe.SourceSession.SetSourceAcquisitionProvenance(new ToolRecipeAcquisitionProvenance(
                ToolRecipeAcquisitionProvenanceState.Available, "Original input acquisition", "Not evidence for another file."));
            var variantReference = new ToolRecipeSelection(
                "selection.variant.reference",
                "Variant reference ROI",
                ToolRecipeSelectionKinds.GridRectangle,
                reopenedRecipe.Source.Id,
                reopenedRecipe.Source.FrameId,
                variantBinding,
                new ToolRecipeGridRectangle(0, 0, 2, 1),
                null,
                null);
            var variantMeasurement = new ToolRecipeSelection(
                "selection.variant.measurement",
                "Variant measurement ROI",
                ToolRecipeSelectionKinds.GridRectangle,
                reopenedRecipe.Source.Id,
                reopenedRecipe.Source.FrameId,
                variantBinding,
                new ToolRecipeGridRectangle(0, 1, 2, 1),
                null,
                null);
            reopenedRecipe.Selections.Add(variantReference);
            reopenedRecipe.Selections.Add(variantMeasurement);
            var variantStep = reopenedRecipe.PipelineSteps.Single();
            variantStep.InputEntityIdsText = string.Join(
                "; ",
                reopenedRecipe.Source.Id,
                variantReference.Id,
                variantMeasurement.Id);
            variantStep.DualRoiRouting = new ToolRecipeDualRoiRouting(
                variantReference.Id,
                variantMeasurement.Id);
            var variantBaseSaved = reopenedRecipe.TrySaveTeachingRecipe(
                variantBasePath,
                out var variantBaseSaveMessage);
            var variantLogCount = reopenedRecipe.RunLog.Count;
            reopenedRecipe.BeginCompatibleSourceVariantSetup();
            Check(
                "compatible variant setup pre-fills a separate draft without mutating or executing the current recipe",
                variantBaseSaved
                && reopenedRecipe.IsFirstRecipeSetupVisible
                && reopenedRecipe.IsCompatibleVariantSetup
                && reopenedRecipe.FirstRecipeName == "First Recipe Fixture-variant"
                && string.Equals(reopenedRecipe.FirstRecipeFolderPath, firstUseRoot, StringComparison.OrdinalIgnoreCase)
                && string.Equals(reopenedRecipe.FirstRecipeSourcePath, sourcePath, StringComparison.OrdinalIgnoreCase)
                && !reopenedRecipe.IsFirstRecipeSetupValid
                && reopenedRecipe.FirstRecipeSetupStatus == reopenedRecipe.Localization.CompatibleVariantSourceMustDiffer
                && reopenedRecipe.RecipePath == variantBasePath
                && !reopenedRecipe.HasCurrentMeasurementPreview
                && reopenedRecipe.RunLog.Count == variantLogCount,
                $"base={variantBaseSaveMessage}; status={reopenedRecipe.FirstRecipeSetupStatus}; logs={variantLogCount}->{reopenedRecipe.RunLog.Count}");

            reopenedRecipe.FirstRecipeName = "variant-copy";
            reopenedRecipe.FirstRecipeSourcePath = incompatibleSourcePath;
            Check(
                "compatible variant setup rejects a different C3D grid before Create",
                !reopenedRecipe.IsFirstRecipeSetupValid
                && reopenedRecipe.FirstRecipeSetupStatus.Contains("3 x 3", StringComparison.Ordinal)
                && reopenedRecipe.FirstRecipeSetupStatus.Contains("4 x 4", StringComparison.Ordinal),
                reopenedRecipe.FirstRecipeSetupStatus);

            reopenedRecipe.FirstRecipeSourcePath = variantSourcePath;
            Check("variant requires per-input ROI reuse review", !reopenedRecipe.IsFirstRecipeSetupValid
                && !reopenedRecipe.IsVariantRoiReuseConfirmed, reopenedRecipe.FirstRecipeSetupStatus);
            reopenedRecipe.IsVariantRoiReuseConfirmed = true;
            reopenedRecipe.FirstRecipeSourcePath = incompatibleSourcePath;
            Check("changing input clears ROI reuse confirmation", !reopenedRecipe.IsVariantRoiReuseConfirmed, reopenedRecipe.FirstRecipeSetupStatus);
            reopenedRecipe.FirstRecipeSourcePath = variantSourcePath;
            reopenedRecipe.IsVariantRoiReuseConfirmed = true;
            var variantDraftReady = reopenedRecipe.TryGetFirstRecipeSetup(
                out var variantSetup,
                out var variantDraftMessage);
            var reviewedBinding = ToolRecipeSelectionSourceBindingVerifier.ReadIdentity(variantSourcePath);
            var rejectedBindings = new[]
            {
                reviewedBinding with { Format = "HeightField" },
                reviewedBinding with { Unit = "mm" },
                reviewedBinding with { FrameId = "flipped-frame" },
                reviewedBinding with { OwnerEntityId = "derived-output" },
                reviewedBinding with { ContentSha256 = new string('a', 64) },
                reviewedBinding with { GridWidth = 0 }
            };
            foreach (var rejectedBinding in rejectedBindings)
            {
                var rejected = !reopenedRecipe.TryCreateCompatibleSourceVariant(variantSetup, rejectedBinding, out var rejection);
                Check("variant rejects changed format/metadata/identity before mutation", rejected
                    && reopenedRecipe.RecipePath == variantBasePath && !File.Exists(variantPath), rejection);
            }
            var originalBytesBeforeCopy = File.ReadAllBytes(variantBasePath);
            Check("variant rejects overwriting the original at execution boundary",
                !reopenedRecipe.TryCreateCompatibleSourceVariant(variantSetup with { RecipePath = variantBasePath }, reviewedBinding, out var overwriteMessage)
                && originalBytesBeforeCopy.SequenceEqual(File.ReadAllBytes(variantBasePath))
                && reopenedRecipe.RecipePath == variantBasePath, overwriteMessage);
            var preservedSelection = reopenedRecipe.Selections[0];
            foreach (var invalidSelection in new[]
            {
                preservedSelection with { SourceBinding = preservedSelection.SourceBinding with { ContentSha256 = new string('b', 64) } },
                preservedSelection with { GridRectangle = new ToolRecipeGridRectangle(0, 0, int.MaxValue, 2) },
                preservedSelection with { GridRectangle = new ToolRecipeGridRectangle(-1, 0, 1, 1) },
                preservedSelection with { GridPolygon = new ToolRecipeGridPolygon([]) }
            })
            {
                reopenedRecipe.Selections[0] = invalidSelection;
                Check("variant rejects stale or unsupported ROI", !reopenedRecipe.BeginCompatibleSourceVariantCommand.CanExecute(null), invalidSelection.Id);
            }
            reopenedRecipe.Selections[0] = preservedSelection;
            Check("input contract summary preserves declared units/frame without calibration claim",
                reopenedRecipe.SourceContractSummary.Contains(reopenedRecipe.Source.Unit, StringComparison.Ordinal)
                && reopenedRecipe.SourceContractSummary.Contains(reopenedRecipe.Source.FrameId, StringComparison.Ordinal)
                && (reopenedRecipe.SourceContractSummary.Contains("Declared", StringComparison.Ordinal)
                    || reopenedRecipe.SourceContractSummary.Contains("선언", StringComparison.Ordinal)), reopenedRecipe.SourceContractSummary);
            using (var failedCopy = new ToolWorkbenchViewModel(Path.Combine(firstUseRoot, "failed-copy-recent.json")))
            {
                failedCopy.TryOpenTeachingRecipe(variantBasePath, out _);
                failedCopy.SetValidationSetSources([sourcePath]);
                failedCopy.BeginCompatibleSourceVariantSetup();
                failedCopy.FirstRecipeSourcePath = variantSourcePath;
                failedCopy.IsVariantRoiReuseConfirmed = true;
                failedCopy.MarkSelectedStepParameterDraftDirty();
                var draftRejected = !failedCopy.TryCreateCompatibleSourceVariant(variantSetup, reviewedBinding, out var pendingDraftMessage);
                Check("copy rejects an unapplied parameter draft before changing state or writing",
                    draftRejected && failedCopy.HasPendingStepParameterChanges && failedCopy.RecipePath == variantBasePath
                    && failedCopy.Source.Path == sourcePath && !File.Exists(variantPath), pendingDraftMessage);
                failedCopy.DiscardSelectedStepParameterDraft();
                var runningOwner = (ToolWorkbenchSignedDeltaExecutionOwner)typeof(ToolWorkbenchViewModel)
                    .GetField("signedDeltaBackgroundExecutionOwner", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                    .GetValue(failedCopy)!;
                runningOwner.SetRunning(true);
                var runningRejected = !failedCopy.TryCreateCompatibleSourceVariant(variantSetup, reviewedBinding, out var runningMessage);
                Check("copy rejects mutation while an execution owner is running", runningRejected
                    && runningMessage == failedCopy.Localization.CompatibleVariantBusy && failedCopy.RecipePath == variantBasePath
                    && failedCopy.Source.Path == sourcePath && !File.Exists(variantPath), runningMessage);
                runningOwner.SetRunning(false);
                failedCopy.BeginTeachingSelectionCaptureCommand.Execute(null);
                var captureWasActive = failedCopy.IsTeachingSelectionCaptureActive;
                var previousSelections = failedCopy.Selections.ToArray();
                var previousStep = failedCopy.SelectedPipelineStep;
                var previousValidationSample = failedCopy.ValidationSetSamples.Single();
                var previousName = failedCopy.RecipeName;
                var previousDirty = failedCopy.IsDirty;
                var previousValidationDirty = failedCopy.IsValidationSetDefinitionDirty;
                var blockedParent = Path.Combine(firstUseRoot, "copy-parent-is-a-file");
                File.WriteAllText(blockedParent, "Owned failure fixture; not a directory.");
                var blockedSetup = variantSetup with { RecipePath = Path.Combine(blockedParent, "copy.ov3d-recipe.json") };
                var copyRejected = !failedCopy.TryCreateCompatibleSourceVariant(blockedSetup, reviewedBinding, out var failureMessage);
                Check("copy write failure preserves the original editor, selections, validation set, and file",
                    copyRejected && failedCopy.RecipePath == variantBasePath && failedCopy.RecipeName == previousName
                    && failedCopy.Source.Path == sourcePath && failedCopy.SourceSession.SourceBinding == variantBinding
                    && failedCopy.Selections.SequenceEqual(previousSelections) && ReferenceEquals(failedCopy.SelectedPipelineStep, previousStep)
                    && failedCopy.ValidationSetSamples.Count == 1 && ReferenceEquals(failedCopy.ValidationSetSamples[0], previousValidationSample)
                    && failedCopy.IsDirty == previousDirty && failedCopy.IsValidationSetDefinitionDirty == previousValidationDirty
                    && failedCopy.IsFirstRecipeSetupVisible && !File.Exists(blockedSetup.RecipePath)
                    && originalBytesBeforeCopy.SequenceEqual(File.ReadAllBytes(variantBasePath)), failureMessage);
                Check("failed copy leaves an active ROI capture intact", captureWasActive && failedCopy.IsTeachingSelectionCaptureActive,
                    $"captureBefore={captureWasActive};captureAfter={failedCopy.IsTeachingSelectionCaptureActive}");

                var retryPath = Path.Combine(firstUseRoot, "retry-copy.ov3d-recipe.json");
                var retryCreated = failedCopy.TryCreateCompatibleSourceVariant(variantSetup with { RecipePath = retryPath }, reviewedBinding, out var retryMessage);
                using var retryOpened = new ToolWorkbenchViewModel(Path.Combine(firstUseRoot, "retry-reopen-recent.json"));
                Check("copy can be retried and reopened after a write failure without implicit execution",
                    retryCreated && retryOpened.TryOpenTeachingRecipe(retryPath, out _)
                    && retryOpened.Source.Path == variantSourcePath && retryOpened.PipelineSteps.Count == 1
                    && retryOpened.Source.Id == "source.variant.custom" && retryOpened.PipelineSteps[0].InputEntityIds[0] == retryOpened.Source.Id
                    && retryOpened.Selections.Select(selection => selection.GridRectangle).SequenceEqual(previousSelections.Select(selection => selection.GridRectangle))
                    && !retryOpened.HasValidationSetSamples && !failedCopy.IsDirty && !failedCopy.HasCurrentMeasurementPreview
                    && !failedCopy.IsTeachingSelectionCaptureActive && !failedCopy.IsOrderedRunRunning && !failedCopy.IsSelectedStepPreviewRunning, retryMessage);

                var committedBytes = File.ReadAllBytes(retryPath);
                var duplicateRejected = !failedCopy.TryCreateCompatibleSourceVariant(variantSetup with { RecipePath = retryPath }, reviewedBinding, out var duplicateMessage);
                Check("repeated copy does not overwrite the committed destination", duplicateRejected
                    && committedBytes.SequenceEqual(File.ReadAllBytes(retryPath)) && failedCopy.RecipePath == retryPath, duplicateMessage);
            }
            var variantCreateMessage = string.Empty;
            var variantCreated = variantDraftReady
                && reopenedRecipe.TryCreateCompatibleSourceVariant(
                    variantSetup,
                    ToolRecipeSelectionSourceBindingVerifier.ReadIdentity(variantSourcePath),
                    out variantCreateMessage);
            var variantCompleted = variantCreated
                && reopenedRecipe.CompleteFirstRecipeSetup(out _);
            var variantDocument = variantCreated ? ToolRecipeDocumentStore.Load(variantPath) : null;
            var originalVariantDocument = variantBaseSaved
                ? ToolRecipeDocumentStore.Load(variantBasePath)
                : null;
            var variantSelections = variantDocument?.Selections ?? Array.Empty<ToolRecipeSelection>();
            var originalVariantSelections = originalVariantDocument?.Selections ?? Array.Empty<ToolRecipeSelection>();
            Check(
                "compatible variant preserves steps, routes, ROI coordinates, and parameters while rebinding source identity",
                variantCompleted
                && variantDocument is not null
                && originalVariantDocument is not null
                && variantSetup.IsCompatibleSourceVariant
                && string.Equals(variantSetup.RecipePath, variantPath, StringComparison.OrdinalIgnoreCase)
                && variantDocument.Name == "variant-copy"
                && string.Equals(
                    ResolvePersistedSourcePath(variantPath, variantDocument.Source.Path),
                    variantSourcePath,
                    StringComparison.OrdinalIgnoreCase)
                && variantDocument.Steps.Count == originalVariantDocument.Steps.Count
                && variantDocument.Steps[0].Id == originalVariantDocument.Steps[0].Id
                && variantDocument.Steps[0].ToolId == originalVariantDocument.Steps[0].ToolId
                && variantDocument.Steps[0].InputEntityIds.SequenceEqual(originalVariantDocument.Steps[0].InputEntityIds)
                && variantDocument.Steps[0].DualRoiRouting == originalVariantDocument.Steps[0].DualRoiRouting
                && variantDocument.Steps[0].Parameters.Select(parameter => $"{parameter.Name}={parameter.Value}")
                    .SequenceEqual(originalVariantDocument.Steps[0].Parameters.Select(parameter => $"{parameter.Name}={parameter.Value}"))
                && variantSelections.Select(selection => selection.Id)
                    .SequenceEqual(originalVariantSelections.Select(selection => selection.Id))
                && variantSelections.Select(selection => selection.GridRectangle)
                    .SequenceEqual(originalVariantSelections.Select(selection => selection.GridRectangle))
                && variantSelections.All(selection =>
                    selection.SourceBinding.ContentSha256
                    == ToolRecipeSelectionSourceBindingVerifier.ReadIdentity(variantSourcePath).ContentSha256)
                && originalVariantSelections.All(selection =>
                    selection.SourceBinding.ContentSha256 == variantBinding.ContentSha256)
                && reopenedRecipe.BeginCompatibleSourceVariantCommand.CanExecute(null)
                && !reopenedRecipe.HasCurrentMeasurementPreview
                && !reopenedRecipe.IsMeasurementPreviewPublished
                && !reopenedRecipe.IsFirstRecipeSetupVisible,
                $"draft={variantDraftMessage}; create={variantCreateMessage}; variant={variantDocument?.Source.Path}");
            Check("copy retains the source entity ID but not original acquisition or calibration evidence",
                variantDocument?.Source.Id == "source.variant.custom" && reopenedRecipe.Source.Id == variantDocument.Source.Id
                && variantDocument.Source.SensorId is null && variantDocument.Source.MeasurementEvidence is null
                && variantDocument.Source.AcquisitionProvenance?.State == ToolRecipeAcquisitionProvenanceState.Unavailable
                && reopenedRecipe.Source.SensorId is null && reopenedRecipe.Source.MeasurementEvidence is null
                && originalVariantDocument?.Source.MeasurementEvidence?.CalibrationId == "original-calibration"
                && originalBytesBeforeCopy.SequenceEqual(File.ReadAllBytes(variantBasePath)), $"source={variantDocument?.Source.Id}");
            var collisionRejected = false;
            try
            {
                ToolRecipeDocumentStore.SaveNew(variantBasePath, originalVariantDocument! with { Name = "Must not overwrite" });
            }
            catch (IOException)
            {
                collisionRejected = true;
            }
            Check("create-only storage rejects final-commit collisions and removes the temporary sibling",
                collisionRejected && originalBytesBeforeCopy.SequenceEqual(File.ReadAllBytes(variantBasePath))
                && !Directory.EnumerateFiles(firstUseRoot, "*.tmp.*", SearchOption.AllDirectories).Any(), $"collisionRejected={collisionRejected}");

            var restoredSetup = new ToolWorkbenchViewModel(firstUseRecentPath);
            var restoredLogCount = restoredSetup.RunLog.Count;
            restoredSetup.BeginFirstRecipeSetup();
            Check(
                "remembered first-use values restore visibly and editably without opening, adding, or executing",
                restoredSetup.IsFirstRecipeSetupVisible
                && restoredSetup.FirstRecipeName == "First Recipe Fixture"
                && string.Equals(restoredSetup.FirstRecipeFolderPath, firstUseRoot, StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(restoredSetup.FirstRecipeSourcePath)
                && restoredSetup.SelectedFirstRecipeStarter?.Id == ToolWorkbenchViewModel.EmptyFirstRecipeStarterId
                && restoredSetup.RememberFirstRecipeSetup
                && string.IsNullOrWhiteSpace(restoredSetup.RecipePath)
                && string.IsNullOrWhiteSpace(restoredSetup.Source.Path)
                && restoredSetup.PipelineSteps.Count == 0
                && restoredSetup.RunLog.Count == restoredLogCount,
                $"name={restoredSetup.FirstRecipeName}; starter={restoredSetup.SelectedFirstRecipeStarter?.Id}; recipe={restoredSetup.RecipePath}; source={restoredSetup.Source.Path}; logs={restoredSetup.RunLog.Count}/{restoredLogCount}");

            var staleRoot = Path.Combine(firstUseRoot, "stale");
            Directory.CreateDirectory(staleRoot);
            var staleSource = Path.Combine(staleRoot, "stale.C3D");
            File.Copy(sourcePath, staleSource);
            restoredSetup.FirstRecipeName = "Stale Fixture";
            restoredSetup.FirstRecipeFolderPath = staleRoot;
            restoredSetup.FirstRecipeSourcePath = staleSource;
            restoredSetup.RememberFirstRecipeSetup = true;
            var stalePreferenceSaved = restoredSetup.CompleteFirstRecipeSetup(out var stalePreferenceMessage);
            Directory.Delete(staleRoot, recursive: true);
            var staleSetup = new ToolWorkbenchViewModel(firstUseRecentPath);
            var staleLogCount = staleSetup.RunLog.Count;
            OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
            staleSetup.BeginFirstRecipeSetup();
            Check(
                "stale remembered paths remain visible, explain the unavailable folder, and disable Create",
                stalePreferenceSaved
                && staleSetup.FirstRecipeName == "Stale Fixture"
                && string.Equals(staleSetup.FirstRecipeFolderPath, staleRoot, StringComparison.OrdinalIgnoreCase)
                && string.Equals(staleSetup.FirstRecipeSourcePath, staleSource, StringComparison.OrdinalIgnoreCase)
                && !staleSetup.IsFirstRecipeSetupValid
                && !staleSetup.CreateFirstRecipeCommand.CanExecute(null)
                && staleSetup.FirstRecipeSetupStatus == "The saved recipe folder is unavailable. Select it again."
                && string.IsNullOrWhiteSpace(staleSetup.RecipePath)
                && string.IsNullOrWhiteSpace(staleSetup.Source.Path)
                && staleSetup.PipelineSteps.Count == 0
                && staleSetup.RunLog.Count == staleLogCount,
                $"saved={stalePreferenceSaved}; {stalePreferenceMessage}; status={staleSetup.FirstRecipeSetupStatus}; logs={staleSetup.RunLog.Count}/{staleLogCount}");

            staleSetup.ResetFirstRecipeSetupCommand.Execute(null);
            var resetSetup = new ToolWorkbenchViewModel(firstUseRecentPath);
            resetSetup.BeginFirstRecipeSetup();
            Check(
                "Reset clears remembered first-use values and has no authored or execution side effect",
                staleSetup.FirstRecipeName == "new-inspection"
                && string.IsNullOrWhiteSpace(staleSetup.FirstRecipeFolderPath)
                && string.IsNullOrWhiteSpace(staleSetup.FirstRecipeSourcePath)
                && !staleSetup.RememberFirstRecipeSetup
                && resetSetup.FirstRecipeName == "new-inspection"
                && string.IsNullOrWhiteSpace(resetSetup.FirstRecipeFolderPath)
                && string.IsNullOrWhiteSpace(resetSetup.FirstRecipeSourcePath)
                && string.IsNullOrWhiteSpace(resetSetup.RecipePath)
                && string.IsNullOrWhiteSpace(resetSetup.Source.Path)
                && resetSetup.PipelineSteps.Count == 0,
                $"resetName={resetSetup.FirstRecipeName}; folder={resetSetup.FirstRecipeFolderPath}; source={resetSetup.FirstRecipeSourcePath}");

            OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
            var englishFirstUse = $"{staleSetup.Localization.FirstRecipeSetup}|{staleSetup.Localization.FirstRecipeFolder}|{staleSetup.Localization.FirstRecipeSource}|{staleSetup.Localization.FirstRecipeCreate}";
            OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
            var koreanFirstUse = $"{staleSetup.Localization.FirstRecipeSetup}|{staleSetup.Localization.FirstRecipeFolder}|{staleSetup.Localization.FirstRecipeSource}|{staleSetup.Localization.FirstRecipeCreate}";
            OpenVisionLanguageService.SetLanguage(originalLanguage, save: false);
            Check(
                "first-use labels and actions switch between English and Korean",
                englishFirstUse == "New recipe setup|Recipe folder|C3D input|Create recipe"
                && koreanFirstUse == "새 레시피 설정|레시피 폴더|C3D 입력|레시피 만들기",
                $"en={englishFirstUse} | ko={koreanFirstUse}");

            var localizedPropertyChanges = new List<string?>();
            workbench.PropertyChanged += (_, args) => localizedPropertyChanges.Add(args.PropertyName);
            OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
            var englishSource = workbench.LocalizedSourceReadinessSummary;
            var englishPath = workbench.LocalizedRecipePathSummary;
            var englishState = workbench.LocalizedRecipeStateSummary;
            OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
            var koreanSource = workbench.LocalizedSourceReadinessSummary;
            var koreanPath = workbench.LocalizedRecipePathSummary;
            var koreanState = workbench.LocalizedRecipeStateSummary;
            OpenVisionLanguageService.SetLanguage(originalLanguage, save: false);
            Check(
                "Recipe Center source and save-path status switch between English and Korean",
                englishSource == "Input ready · 3 x 3"
                && englishPath == "Not saved yet"
                && koreanSource == "입력 준비됨 · 3 x 3"
                && koreanPath == "아직 저장하지 않음",
                $"en={englishSource}; {englishPath} | ko={koreanSource}; {koreanPath}");
            Check(
                "saveable empty recipe is labeled as execution-incomplete rather than structurally invalid",
                englishState == "Recipe: 1 execution requirement(s) | Modified"
                && koreanState == "레시피: 실행 준비 필요 1개 | 수정됨",
                $"en={englishState} | ko={koreanState}");
            Check(
                "language change notifies all Recipe Center computed status properties",
                localizedPropertyChanges.Contains(nameof(ToolWorkbenchViewModel.LocalizedSourceReadinessSummary), StringComparer.Ordinal)
                && localizedPropertyChanges.Contains(nameof(ToolWorkbenchViewModel.LocalizedRecipePathSummary), StringComparer.Ordinal)
                && localizedPropertyChanges.Contains(nameof(ToolWorkbenchViewModel.LocalizedRecipeStateSummary), StringComparer.Ordinal),
                string.Join(",", localizedPropertyChanges.Distinct(StringComparer.Ordinal)));
            Check(
                "empty recipe enables Save and Save As before inspection steps exist",
                !workbench.IsRecipeSaveBlocked
                && workbench.CanSaveTeachingRecipe
                && workbench.SaveTeachingRecipeCommand.CanExecute(null)
                && workbench.SaveTeachingRecipeAsCommand.CanExecute(null)
                && !workbench.IsTeachingRecipeExecutionReady,
                $"blocked={workbench.IsRecipeSaveBlocked}; save={workbench.SaveTeachingRecipeCommand.CanExecute(null)}; saveAs={workbench.SaveTeachingRecipeAsCommand.CanExecute(null)}");
            var emptyRecipePath = Path.Combine(fixtureRoot, "empty-recipe.ov3d-recipe.json");
            var emptySaved = workbench.TrySaveTeachingRecipe(emptyRecipePath, out var emptySaveMessage);
            var emptyStored = emptySaved ? ToolRecipeDocumentStore.Load(emptyRecipePath) : null;
            Check(
                "empty recipe saves without fabricating an inspection step",
                emptySaved
                && emptyStored is not null
                && emptyStored.Steps.Count == 0
                && ToolRecipeValidator.ValidateForStorage(emptyStored).IsValid
                && !ToolRecipeValidator.Validate(emptyStored).IsValid,
                emptySaveMessage);
            OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
            var savedLocalizedState = workbench.LocalizedRecipeStateSummary;
            OpenVisionLanguageService.SetLanguage(originalLanguage, save: false);
            Check(
                "successful zero-step save refreshes the localized saved state",
                savedLocalizedState == "Recipe: 1 execution requirement(s) | Saved",
                savedLocalizedState);
            var filter = AddTool(workbench, "filter");
            Check(
                "first valid inspection step keeps Save enabled and makes execution validation ready",
                workbench.CanSaveTeachingRecipe
                && workbench.IsTeachingRecipeExecutionReady
                && workbench.SaveTeachingRecipeCommand.CanExecute(null)
                && workbench.SaveTeachingRecipeAsCommand.CanExecute(null),
                $"valid={workbench.CanSaveTeachingRecipe}; save={workbench.SaveTeachingRecipeCommand.CanExecute(null)}; saveAs={workbench.SaveTeachingRecipeAsCommand.CanExecute(null)}");
            var validationSourceRequestRaised = false;
            EventHandler validationSourceRequestHandler = (_, _) => validationSourceRequestRaised = true;
            workbench.SelectValidationSetSourcesRequested += validationSourceRequestHandler;
            workbench.SelectValidationSetSourcesCommand.Execute(null);
            workbench.SelectValidationSetSourcesRequested -= validationSourceRequestHandler;
            Check(
                "Validation Set source selection is a ViewModel command request",
                validationSourceRequestRaised,
                $"requested={validationSourceRequestRaised}");
            workbench.SelectedReviewTabIndex = 4;
            var validationTabRestored = workbench.SelectedReviewTabIndex == 4;
            workbench.SelectedReviewTabIndex = 5;
            Check(
                "review-tab presentation state restores validation and inspection queue tabs",
                validationTabRestored && workbench.SelectedReviewTabIndex == 5,
                $"selectedIndex={workbench.SelectedReviewTabIndex}");

            var propertySession = new ToolWorkbenchStepPropertySession();
            propertySession.Refresh(filter);
            var sessionDraft = propertySession.Draft as FilterStepProperties;
            Check(
                "PropertyGrid session owns a detached typed draft",
                sessionDraft is not null
                && propertySession.IsSupported
                && !propertySession.HasPendingChanges
                && sessionDraft.KernelSize == 3,
                $"draft={propertySession.Draft?.GetType().Name}; pending={propertySession.HasPendingChanges}");
            sessionDraft!.KernelSize = 4;
            propertySession.MarkDirty();
            var invalidSessionValues = propertySession.TryCreateParameterValues(filter, out _, out var invalidSessionMessage);
            Check(
                "PropertyGrid session rejects invalid draft values without recipe mutation",
                !invalidSessionValues
                && propertySession.HasPendingChanges
                && filter.Parameters.Single(parameter => parameter.Name == "KernelSize").Value == "3",
                invalidSessionMessage);
            sessionDraft.KernelSize = 5;
            var validSessionValues = propertySession.TryCreateParameterValues(filter, out var sessionValues, out var validSessionMessage);
            Check(
                "PropertyGrid session serializes valid values without committing the recipe",
                validSessionValues
                && sessionValues["KernelSize"] == "5"
                && filter.Parameters.Single(parameter => parameter.Name == "KernelSize").Value == "3",
                validSessionMessage);

            filter.Parameters.Remove(filter.Parameters.Single(parameter => parameter.Name == "BoundaryPolicy"));
            filter.Parameters.Add(new ToolWorkbenchParameterItem("FuturePolicy", "RetainMe"));
            workbench.DiscardSelectedStepParameterDraft();
            var filterDraft = workbench.SelectedStepPropertyDraft as FilterStepProperties;
            Check(
                "Filter maps to a detached typed draft",
                filterDraft is not null && filterDraft.KernelSize == 3 && filterDraft.UnmappedParameters.Contains("FuturePolicy=RetainMe", StringComparison.Ordinal),
                $"draft={workbench.SelectedStepPropertyDraft?.GetType().Name}; kernel={filterDraft?.KernelSize}; unmapped={filterDraft?.UnmappedParameters}");

            filterDraft!.KernelSize = 5;
            workbench.MarkSelectedStepParameterDraftDirtyCommand.Execute(null);
            Check(
                "WPG change command marks a draft without mutating the recipe or auto-running",
                filter.Parameters.Single(parameter => parameter.Name == "KernelSize").Value == "3"
                && workbench.HasPendingStepParameterChanges
                && !workbench.HasCurrentFilterPreview
                && !workbench.PreviewSelectedStepCommand.CanExecute(null),
                $"storedKernel={filter.Parameters.Single(parameter => parameter.Name == "KernelSize").Value}; preview={workbench.HasCurrentFilterPreview}");

            workbench.DiscardSelectedStepParameterDraft();
            Check(
                "preparation-preset assistant starts as an explicit Analyze action",
                workbench.AnalyzePreparationPresetsCommand.CanExecute(null)
                && !workbench.IsPreparationPresetAnalysisReady
                && workbench.SelectedPreparationPreset is null
                && !workbench.HasPendingStepParameterChanges
                && filter.Parameters.Single(parameter => parameter.Name == "KernelSize").Value == "3",
                $"canAnalyze={workbench.AnalyzePreparationPresetsCommand.CanExecute(null)}; analyzed={workbench.IsPreparationPresetAnalysisReady}; selected={workbench.SelectedPreparationPreset?.Id ?? "(none)"}");
            workbench.AnalyzePreparationPresetsCommand.Execute(null);
            Check(
                "Analyze discovers the bounded Filter presets without changing the draft or recipe",
                workbench.IsPreparationPresetAnalysisReady
                && workbench.PreparationPresetOptions.Count == 3
                && workbench.SelectedPreparationPreset?.KernelSize == 3
                && (workbench.SelectedStepPropertyDraft as FilterStepProperties)?.KernelSize == 3
                && filter.Parameters.Single(parameter => parameter.Name == "KernelSize").Value == "3"
                && !workbench.HasPendingStepParameterChanges
                && !workbench.HasCurrentFilterPreview,
                $"options={workbench.PreparationPresetOptions.Count}; selected={workbench.SelectedPreparationPreset?.KernelSize}; draft={(workbench.SelectedStepPropertyDraft as FilterStepProperties)?.KernelSize}; stored={filter.Parameters.Single(parameter => parameter.Name == "KernelSize").Value}");
            OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
            var englishPreparationPresetName = workbench.PreparationPresetOptions
                .Single(option => option.KernelSize == 5)
                .DisplayName;
            OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
            var koreanPreparationPresetName = workbench.PreparationPresetOptions
                .Single(option => option.KernelSize == 5)
                .DisplayName;
            OpenVisionLanguageService.SetLanguage(originalLanguage, save: false);
            Check(
                "Preparation preset options localize while preserving their stable IDs",
                englishPreparationPresetName == "Median 5 x 5"
                && koreanPreparationPresetName == "중앙값 5 × 5"
                && workbench.PreparationPresetOptions.Select(option => option.Id).SequenceEqual(
                    ["filter-median-3", "filter-median-5", "filter-median-7"]),
                $"english={englishPreparationPresetName}; korean={koreanPreparationPresetName}; ids={string.Join(",", workbench.PreparationPresetOptions.Select(option => option.Id))}");
            workbench.SelectedPreparationPreset = workbench.PreparationPresetOptions.Single(option => option.KernelSize == 5);
            workbench.ProposePreparationPresetCommand.Execute(null);
            Check(
                "Propose keeps the selected preset transient and non-mutating",
                workbench.ProposedPreparationPreset?.KernelSize == 5
                && !workbench.IsPreparationPresetReviewActive
                && (workbench.SelectedStepPropertyDraft as FilterStepProperties)?.KernelSize == 3
                && filter.Parameters.Single(parameter => parameter.Name == "KernelSize").Value == "3"
                && !workbench.HasPendingStepParameterChanges
                && !workbench.HasCurrentFilterPreview,
                $"proposed={workbench.ProposedPreparationPreset?.KernelSize}; review={workbench.IsPreparationPresetReviewActive}; draft={(workbench.SelectedStepPropertyDraft as FilterStepProperties)?.KernelSize}; stored={filter.Parameters.Single(parameter => parameter.Name == "KernelSize").Value}");
            workbench.ReviewPreparationPresetCommand.Execute(null);
            Check(
                "Review is read-only and enables only the draft application boundary",
                workbench.IsPreparationPresetReviewActive
                && workbench.ApplyPreparationPresetDraftCommand.CanExecute(null)
                && (workbench.SelectedStepPropertyDraft as FilterStepProperties)?.KernelSize == 3
                && filter.Parameters.Single(parameter => parameter.Name == "KernelSize").Value == "3"
                && !workbench.HasPendingStepParameterChanges
                && !workbench.HasCurrentFilterPreview,
                $"review={workbench.IsPreparationPresetReviewActive}; canApplyDraft={workbench.ApplyPreparationPresetDraftCommand.CanExecute(null)}");
            workbench.CancelPreparationPresetReviewCommand.Execute(null);
            Check(
                "Cancel clears the transient preset review without changing recipe or draft",
                workbench.ProposedPreparationPreset is null
                && !workbench.IsPreparationPresetReviewActive
                && !workbench.IsPreparationPresetDraftApplied
                && (workbench.SelectedStepPropertyDraft as FilterStepProperties)?.KernelSize == 3
                && filter.Parameters.Single(parameter => parameter.Name == "KernelSize").Value == "3"
                && !workbench.HasPendingStepParameterChanges
                && !workbench.HasCurrentFilterPreview,
                $"proposed={workbench.ProposedPreparationPreset?.KernelSize}; review={workbench.IsPreparationPresetReviewActive}; draft={(workbench.SelectedStepPropertyDraft as FilterStepProperties)?.KernelSize}");
            workbench.SelectedPreparationPreset = workbench.PreparationPresetOptions.Single(option => option.KernelSize == 5);
            workbench.ProposePreparationPresetCommand.Execute(null);
            workbench.ReviewPreparationPresetCommand.Execute(null);
            workbench.ApplyPreparationPresetDraftCommand.Execute(null);
            Check(
                "Apply draft changes only the typed PropertyGrid draft and leaves normal Apply explicit",
                workbench.IsPreparationPresetDraftApplied
                && !workbench.IsPreparationPresetReviewActive
                && (workbench.SelectedStepPropertyDraft as FilterStepProperties)?.KernelSize == 5
                && filter.Parameters.Single(parameter => parameter.Name == "KernelSize").Value == "3"
                && workbench.HasPendingStepParameterChanges
                && workbench.ApplySelectedStepParameterDraftCommand.CanExecute(null)
                && !workbench.HasCurrentFilterPreview,
                $"draftApplied={workbench.IsPreparationPresetDraftApplied}; draft={(workbench.SelectedStepPropertyDraft as FilterStepProperties)?.KernelSize}; stored={filter.Parameters.Single(parameter => parameter.Name == "KernelSize").Value}; pending={workbench.HasPendingStepParameterChanges}; canNormalApply={workbench.ApplySelectedStepParameterDraftCommand.CanExecute(null)}");
            workbench.DiscardSelectedStepParameterDraft();
            filterDraft = workbench.SelectedStepPropertyDraft as FilterStepProperties
                ?? throw new InvalidOperationException("Filter draft was not restored after preset cancellation.");

            filterDraft.KernelSize = 4;
            var invalidApplied = workbench.TryApplySelectedStepParameterDraft(out var invalidMessage);
            Check(
                "invalid typed value cannot alter recipe",
                !invalidApplied && filter.Parameters.Single(parameter => parameter.Name == "KernelSize").Value == "3",
                invalidMessage);

            filterDraft.KernelSize = 5;
            var filterApplied = workbench.TryApplySelectedStepParameterDraft(out var filterMessage);
            Check(
                "Apply updates known values and preserves unknown values",
                filterApplied
                && filter.Parameters.Single(parameter => parameter.Name == "KernelSize").Value == "5"
                && filter.Parameters.Single(parameter => parameter.Name == "BoundaryPolicy").Value == "AvailableNeighbors"
                && filter.Parameters.Single(parameter => parameter.Name == "FuturePolicy").Value == "RetainMe"
                && workbench.IsDirty
                && !workbench.HasCurrentFilterPreview,
                filterMessage);

            var edge = AddTool(workbench, "height-difference-edge");
            edge.InputEntityIdsText = filter.OutputEntityId;
            edge.Parameters.Add(new ToolWorkbenchParameterItem("FutureTiePolicy", "Stable"));
            workbench.SelectPipelineStepCommand.Execute(filter.Id);
            var selectionCommandSelectedFilter = ReferenceEquals(workbench.SelectedPipelineStep, filter);
            workbench.SelectPipelineStepCommand.Execute(edge.Id);
            Check(
                "Tool Lab step activation routes through the ViewModel selection command",
                selectionCommandSelectedFilter && ReferenceEquals(workbench.SelectedPipelineStep, edge),
                $"filterSelected={selectionCommandSelectedFilter}; current={workbench.SelectedPipelineStep?.Id}");

            var actionWorkbench = new ToolWorkbenchViewModel(Path.Combine(fixtureRoot, "step-actions-recent.json"));
            actionWorkbench.SetC3DSource(sourcePath);
            var actionFilter = AddTool(actionWorkbench, "filter");
            var actionEdge = AddTool(actionWorkbench, "height-difference-edge");
            var actionSelection = new ToolRecipeSelection(
                "selection.action-edge",
                "Action Edge ROI",
                ToolRecipeSelectionKinds.GridRectangle,
                actionWorkbench.Source.Id,
                actionWorkbench.Source.FrameId,
                ToolRecipeSelectionSourceBindingVerifier.ReadIdentity(sourcePath),
                new ToolRecipeGridRectangle(0, 0, 2, 2),
                null,
                null);
            actionWorkbench.Selections.Add(actionSelection);
            actionEdge.InputEntityIdsText = $"{actionFilter.OutputEntityId}; {actionSelection.Id}";
            actionWorkbench.MoveSelectedStepUpCommand.Execute(null);
            Check(
                "selected-step move command reorders without inspection execution",
                ReferenceEquals(actionWorkbench.PipelineSteps[0], actionEdge)
                && ReferenceEquals(actionWorkbench.SelectedPipelineStep, actionEdge)
                && !actionWorkbench.HasCurrentFilterPreview
                && !actionWorkbench.HasCurrentEdgePreview,
                $"order={string.Join(",", actionWorkbench.PipelineSteps.Select(step => step.ToolId))}; filterPreview={actionWorkbench.HasCurrentFilterPreview}; edgePreview={actionWorkbench.HasCurrentEdgePreview}");
            ToolWorkbenchStepRemovalRequestEventArgs? removalRequest = null;
            actionWorkbench.RemoveSelectedStepRequested += (_, args) => removalRequest = args;
            var removalDirtyBefore = actionWorkbench.IsDirty;
            var removalLogCountBefore = actionWorkbench.RunLog.Count;
            actionWorkbench.RemoveSelectedStepCommand.Execute(null);
            Check(
                "selected-step remove command requests impact-aware confirmation without mutation",
                removalRequest is
                {
                    StepId: var requestedStepId,
                    StepName: var requestedStepName,
                    OrphanedSelectionNames.Count: 1
                }
                && requestedStepId == actionEdge.Id
                && requestedStepName == actionEdge.ToolName
                && removalRequest.OrphanedSelectionNames[0] == actionSelection.Name
                && actionWorkbench.PipelineSteps.Count == 2
                && actionWorkbench.Selections.Contains(actionSelection)
                && ReferenceEquals(actionWorkbench.SelectedPipelineStep, actionEdge)
                && actionWorkbench.IsDirty == removalDirtyBefore
                && actionWorkbench.RunLog.Count == removalLogCountBefore,
                $"request={removalRequest?.StepId}; selections={removalRequest?.OrphanedSelectionNames.Count}; steps={actionWorkbench.PipelineSteps.Count}; dirty={removalDirtyBefore}->{actionWorkbench.IsDirty}; logs={removalLogCountBefore}->{actionWorkbench.RunLog.Count}");
            var validationWorkspaceField = typeof(ToolWorkbenchViewModel).GetField(
                "validationSetWorkspace",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var validationWorkspace = validationWorkspaceField?.GetValue(actionWorkbench)
                as ValidationSetWorkspaceViewModel;
            var validationExecutionOwnerField = typeof(ValidationSetWorkspaceViewModel).GetField(
                "validationSetExecutionOwner",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var validationExecutionOwner = validationExecutionOwnerField?.GetValue(validationWorkspace)
                as ToolWorkbenchValidationSetExecutionOwner;
            var validationRunningProperty = typeof(ToolWorkbenchValidationSetExecutionOwner).GetProperty(
                nameof(ToolWorkbenchValidationSetExecutionOwner.IsRunning));
            validationRunningProperty?.SetValue(validationExecutionOwner, true);
            var removalBlocked = !actionWorkbench.RemoveSelectedStepCommand.CanExecute(null)
                && !actionWorkbench.ConfirmSelectedStepRemoval(actionEdge.Id)
                && actionWorkbench.PipelineSteps.Count == 2
                && actionWorkbench.Selections.Contains(actionSelection);
            validationRunningProperty?.SetValue(validationExecutionOwner, false);
            Check(
                "selected-step removal is unavailable and fails closed during validation",
                validationExecutionOwner is not null
                && validationRunningProperty is not null
                && removalBlocked,
                $"owner={validationExecutionOwner is not null};runningProperty={validationRunningProperty is not null};blocked={removalBlocked}; steps={actionWorkbench.PipelineSteps.Count}; selections={actionWorkbench.Selections.Count}");
            var removalConfirmed = actionWorkbench.ConfirmSelectedStepRemoval(actionEdge.Id);
            Check(
                "confirmed selected-step removal deletes only the step and its orphaned selection",
                removalConfirmed
                && actionWorkbench.PipelineSteps.Count == 1
                && ReferenceEquals(actionWorkbench.PipelineSteps[0], actionFilter)
                && !actionWorkbench.Selections.Contains(actionSelection)
                && !actionWorkbench.HasCurrentFilterPreview
                && !actionWorkbench.HasCurrentEdgePreview
                && actionWorkbench.RunLog.Count == removalLogCountBefore + 1
                && actionWorkbench.RunLog[0].Category == "Teach",
                $"confirmed={removalConfirmed}; remaining={string.Join(",", actionWorkbench.PipelineSteps.Select(step => step.ToolId))}; selections={actionWorkbench.Selections.Count}; logs={removalLogCountBefore}->{actionWorkbench.RunLog.Count}; filterPreview={actionWorkbench.HasCurrentFilterPreview}; edgePreview={actionWorkbench.HasCurrentEdgePreview}");

            actionFilter.InputEntityIdsText = $"{actionWorkbench.Source.Id}; {actionSelection.Id}";
            actionEdge.Parameters[0].Value = "modified detached object";
            var restorationSucceeded = actionWorkbench.TryRestoreLastRemovedStep(out var restoreMessage);
            var restoredEdge = actionWorkbench.SelectedPipelineStep;
            Check("last removed tool restores its snapshot and orphaned ROI without replacing later connections",
                restorationSucceeded && restoredEdge is not null && !ReferenceEquals(restoredEdge, actionEdge)
                && actionWorkbench.PipelineSteps[0] == restoredEdge && restoredEdge.Id == actionEdge.Id
                && restoredEdge.InputEntityIdsText == $"{actionFilter.OutputEntityId}; {actionSelection.Id}"
                && restoredEdge.Parameters[0].Value != "modified detached object"
                && actionFilter.InputEntityIdsText == $"{actionWorkbench.Source.Id}; {actionSelection.Id}"
                && actionWorkbench.Selections.Contains(actionSelection) && !actionWorkbench.HasCurrentEdgePreview,
                restoreMessage);
            Check("last removed tool restoration is consumed once and does not execute", !actionWorkbench.TryRestoreLastRemovedStep(out _)
                && !actionWorkbench.RestoreLastRemovedStepCommand.CanExecute(null) && !actionWorkbench.HasCurrentFilterPreview && !actionWorkbench.HasCurrentEdgePreview, "one restored tool, no output");
            actionWorkbench.ConfirmSelectedStepRemoval(restoredEdge!.Id);
            Check("shared ROI survives removal", actionWorkbench.Selections.Contains(actionSelection), actionSelection.Id);
            var conflict = AddTool(actionWorkbench, "height-difference-edge");
            var conflictStepCount = actionWorkbench.PipelineSteps.Count;
            Check("restoration rejects identity collisions atomically", !actionWorkbench.TryRestoreLastRemovedStep(out _)
                && actionWorkbench.PipelineSteps.Count == conflictStepCount && actionWorkbench.SelectedPipelineStep == conflict, conflict.Id);
            conflict.Id = "step.nonconflicting";
            conflict.OutputEntityId = "derived.nonconflicting";
            Check("restoration remains available after the collision is resolved", actionWorkbench.TryRestoreLastRemovedStep(out _)
                && actionWorkbench.PipelineSteps.Count == conflictStepCount + 1, actionWorkbench.SelectedPipelineStep?.Id ?? "none");
            actionWorkbench.ConfirmSelectedStepRemoval(actionWorkbench.SelectedPipelineStep!.Id);
            actionWorkbench.MarkSelectedStepParameterDraftDirty();
            Check("pending parameter draft blocks direct restoration", !actionWorkbench.TryRestoreLastRemovedStep(out _), actionWorkbench.StepParameterEditStatus);
            actionWorkbench.DiscardSelectedStepParameterDraftCommand.Execute(null);
            actionWorkbench.SetC3DSource(sourcePath);
            Check("refreshing the same unchanged input preserves restoration", actionWorkbench.CanRestoreLastRemovedStep, sourcePath);
            var replacementSource = Path.Combine(fixtureRoot, "replacement-input.C3D");
            File.Copy(sourcePath, replacementSource);
            actionWorkbench.SetC3DSource(replacementSource);
            Check("replacing the teaching input ends restoration history", !actionWorkbench.CanRestoreLastRemovedStep && !actionWorkbench.TryRestoreLastRemovedStep(out _), replacementSource);
            actionWorkbench.Dispose();

            var edgeDraft = (HeightDifferenceEdgeStepProperties)workbench.SelectedStepPropertyDraft!;
            edgeDraft.ComparisonAxis = HeightDifferenceEdgeComparisonAxis.AcrossColumns;
            edgeDraft.Polarity = HeightDifferenceEdgePolarity.Rising;
            edgeDraft.MinimumDelta = 0;
            workbench.MarkSelectedStepParameterDraftDirty();
            var invalidEdgeApplied = workbench.TryApplySelectedStepParameterDraft(out var invalidEdgeMessage);
            Check(
                "Height Difference Edge rejects non-positive delta",
                !invalidEdgeApplied && edge.Parameters.Single(parameter => parameter.Name == "MinimumDelta").Value == "Set explicitly",
                invalidEdgeMessage);

            edgeDraft.MinimumDelta = 2.5;
            var edgeApplied = workbench.TryApplySelectedStepParameterDraft(out var edgeMessage);
            Check(
                "Height Difference Edge applies invariant typed values without execution",
                edgeApplied
                && edge.Parameters.Single(parameter => parameter.Name == "ComparisonAxis").Value == "AcrossColumns"
                && edge.Parameters.Single(parameter => parameter.Name == "MinimumDelta").Value == "2.5"
                && edge.Parameters.Single(parameter => parameter.Name == "FutureTiePolicy").Value == "Stable"
                && !workbench.HasCurrentEdgePreview,
                edgeMessage);

            var affine = AddTool(workbench, "xyz-affine-solve");
            affine.InputEntityIdsText = edge.OutputEntityId;
            var affineDraft = (XYZAffineSolveStepProperties)workbench.SelectedStepPropertyDraft!;
            affineDraft.MaximumConditionEstimate = 500000;
            affineDraft.ArithmeticResidualWarning = 0.0025;
            workbench.MarkSelectedStepParameterDraftDirty();
            var affineApplied = workbench.TryApplySelectedStepParameterDraft(out var affineMessage);
            Check(
                "XYZ Affine Solve maps typed numerical limits without execution",
                affineApplied
                && affine.Parameters.Single(parameter => parameter.Name == "SolvePolicy").Value == "ExactFourPartialPivot"
                && MatchesInvariantNumber(affine.Parameters.Single(parameter => parameter.Name == "MaximumConditionEstimate").Value, 500000)
                && MatchesInvariantNumber(affine.Parameters.Single(parameter => parameter.Name == "ArithmeticResidualWarning").Value, 0.0025)
                && !workbench.HasCurrentAffineSolvePreview,
                affineMessage);

            var saved = workbench.TrySaveTeachingRecipe(recipePath, out var saveMessage);
            var storedBeforeUnsupported = saved
                ? ToolRecipeDocumentStore.Load(recipePath)
                : null;
            var importedOpenMessage = string.Empty;
            var importedUnsupported = storedBeforeUnsupported is not null;
            if (storedBeforeUnsupported is not null)
            {
                ToolRecipeDocumentStore.Save(
                    recipePath,
                    storedBeforeUnsupported with
                    {
                        Steps =
                        [
                            ..storedBeforeUnsupported.Steps,
                            new ToolRecipeStep(
                                "step.overlay-control-review.imported",
                                "overlay-control-review",
                                "Overlay / Control Review",
                                1,
                                [storedBeforeUnsupported.Source.Id],
                                "derived.review-overlay.imported",
                                [
                                    new ToolRecipeParameter("Overlay", "Selected results"),
                                    new ToolRecipeParameter("Publish", "Explicit")
                                ])
                        ]
                    });
                importedUnsupported = workbench.TryOpenTeachingRecipe(
                    recipePath,
                    out importedOpenMessage);
            }

            var unsupported = importedUnsupported
                ? workbench.PipelineSteps.Single(step => step.ToolId == "overlay-control-review")
                : null;
            var unsupportedValue = unsupported?.Parameters[0].Value;
            workbench.SelectedPipelineStep = unsupported;
            Check(
                "imported review-only step stays visible and read-only",
                importedUnsupported
                && unsupported is not null
                && workbench.SelectedStepPropertyDraft is null
                && workbench.SelectedStepAdapterStatus == workbench.Localization.StepAdapterPartiallySupported
                && workbench.UnsupportedStepCount == 1,
                $"open={importedOpenMessage};summary={workbench.RecipeAdapterCoverageSummary}");
            Check(
                "atomic save completes without a temporary sibling",
                saved && File.Exists(recipePath) && !Directory.EnumerateFiles(fixtureRoot, "*.tmp.*").Any(),
                saveMessage);

            var reopened = new ToolWorkbenchViewModel(recentPath);
            var opened = reopened.TryOpenTeachingRecipe(recipePath, out var openMessage);
            var reopenedFilter = reopened.PipelineSteps.Single(step => step.ToolId == "filter");
            var reopenedEdge = reopened.PipelineSteps.Single(step => step.ToolId == "height-difference-edge");
            var reopenedAffine = reopened.PipelineSteps.Single(step => step.ToolId == "xyz-affine-solve");
            var reopenedUnsupported = reopened.PipelineSteps.Single(step => step.ToolId == "overlay-control-review");
            Check(
                "save and reopen preserve typed and unknown parameters",
                opened
                && reopenedFilter.Parameters.Single(parameter => parameter.Name == "KernelSize").Value == "5"
                && reopenedFilter.Parameters.Single(parameter => parameter.Name == "FuturePolicy").Value == "RetainMe"
                && reopenedEdge.Parameters.Single(parameter => parameter.Name == "MinimumDelta").Value == "2.5"
                && reopenedEdge.Parameters.Single(parameter => parameter.Name == "FutureTiePolicy").Value == "Stable"
                && MatchesInvariantNumber(reopenedAffine.Parameters.Single(parameter => parameter.Name == "MaximumConditionEstimate").Value, 500000)
                && MatchesInvariantNumber(reopenedAffine.Parameters.Single(parameter => parameter.Name == "ArithmeticResidualWarning").Value, 0.0025)
                && unsupportedValue is not null
                && reopenedUnsupported.Parameters[0].Value == unsupportedValue,
                openMessage);
            Check(
                "open does not create Preview or Publish evidence",
                !reopened.HasCurrentFilterPreview && !reopened.HasCurrentEdgePreview,
                $"filterPreview={reopened.HasCurrentFilterPreview}; edgePreview={reopened.HasCurrentEdgePreview}");

            var priorName = reopened.RecipeName;
            var invalidPath = Path.Combine(fixtureRoot, "invalid.ov3d-teach.json");
            File.WriteAllText(invalidPath, "{ invalid json");
            var invalidOpened = reopened.TryOpenTeachingRecipe(invalidPath, out var invalidOpenMessage);
            Check(
                "invalid candidate leaves active session unchanged",
                !invalidOpened && reopened.RecipeName == priorName && reopened.RecipePath == recipePath,
                invalidOpenMessage);

            var stored = ToolRecipeDocumentStore.Load(recipePath);
            var missingPath = Path.Combine(fixtureRoot, "missing-source.ov3d-teach.json");
            ToolRecipeDocumentStore.Save(
                missingPath,
                stored with { Source = stored.Source with { Path = Path.Combine(fixtureRoot, "missing.C3D") } });
            var missing = new ToolWorkbenchViewModel(Path.Combine(fixtureRoot, "missing-recent.json"));
            var missingOpened = missing.TryOpenTeachingRecipe(missingPath, out _);
            Check(
                "missing source opens in repair state without execution",
                missingOpened && !missing.IsSourceReadyForRecipe && missing.SourceReadinessSummary.Contains("missing", StringComparison.OrdinalIgnoreCase),
                missing.SourceReadinessSummary);

            var mismatchPath = Path.Combine(fixtureRoot, "mismatch-source.ov3d-teach.json");
            ToolRecipeDocumentStore.Save(
                mismatchPath,
                stored with { Source = stored.Source with { ContentSha256 = new string('0', 64) } });
            var mismatch = new ToolWorkbenchViewModel(Path.Combine(fixtureRoot, "mismatch-recent.json"));
            var mismatchOpened = mismatch.TryOpenTeachingRecipe(mismatchPath, out _);
            Check(
                "source identity mismatch blocks readiness",
                mismatchOpened && !mismatch.IsSourceReadyForRecipe && mismatch.SourceReadinessSummary.Contains("mismatch", StringComparison.OrdinalIgnoreCase),
                mismatch.SourceReadinessSummary);

            var portableRoot = Path.Combine(fixtureRoot, "portable-bundle");
            Directory.CreateDirectory(portableRoot);
            var portableSourcePath = Path.Combine(portableRoot, "source.C3D");
            File.Copy(sourcePath, portableSourcePath);
            var portableRecipePath = Path.Combine(portableRoot, "portable.ov3d-recipe.json");
            var portableWorkbench = new ToolWorkbenchViewModel(Path.Combine(portableRoot, "recent.json"));
            portableWorkbench.RecipeName = "Portable Recipe";
            portableWorkbench.SetC3DSource(portableSourcePath);
            var portableSaved = portableWorkbench.TrySaveTeachingRecipe(portableRecipePath, out var portableSaveMessage);
            var portableDocument = portableSaved ? ToolRecipeDocumentStore.Load(portableRecipePath) : null;
            Check(
                "Save As stores an in-bundle source as a portable relative path",
                portableSaved
                && portableDocument is not null
                && !Path.IsPathFullyQualified(portableDocument.Source.Path)
                && portableDocument.Source.Path.Replace('\\', '/') == "source.C3D",
                portableSaveMessage);

            var movedBundleRoot = Path.Combine(fixtureRoot, "moved-bundle");
            Directory.CreateDirectory(movedBundleRoot);
            var movedSourcePath = Path.Combine(movedBundleRoot, "source.C3D");
            var movedRecipePath = Path.Combine(movedBundleRoot, "portable.ov3d-recipe.json");
            File.Copy(portableSourcePath, movedSourcePath);
            File.Copy(portableRecipePath, movedRecipePath);
            var movedBundle = new ToolWorkbenchViewModel(Path.Combine(movedBundleRoot, "recent.json"));
            var movedBundleOpened = movedBundle.TryOpenTeachingRecipe(movedRecipePath, out var movedBundleMessage);
            Check(
                "moving the recipe folder rebinds the relative source without copying input bytes",
                movedBundleOpened
                && string.Equals(movedBundle.Source.Path, movedSourcePath, StringComparison.OrdinalIgnoreCase)
                && movedBundle.IsSourceReadyForRecipe
                && File.Exists(movedSourcePath),
                movedBundleMessage);

            var recipeOnlyRoot = Path.Combine(fixtureRoot, "recipe-only-move");
            Directory.CreateDirectory(recipeOnlyRoot);
            var recipeOnlyPath = Path.Combine(recipeOnlyRoot, "portable.ov3d-recipe.json");
            File.Copy(portableRecipePath, recipeOnlyPath);
            var recipeOnly = new ToolWorkbenchViewModel(Path.Combine(recipeOnlyRoot, "recent.json"));
            var recipeOnlyOpened = recipeOnly.TryOpenTeachingRecipe(recipeOnlyPath, out var recipeOnlyMessage);
            Check(
                "moving only the recipe remains repairable and does not copy the source",
                recipeOnlyOpened
                && !recipeOnly.IsSourceReadyForRecipe
                && !File.Exists(Path.Combine(recipeOnlyRoot, "source.C3D")),
                recipeOnlyMessage);

            var absoluteSourceRoot = Path.Combine(fixtureRoot, "absolute-source");
            var absoluteRecipeRoot = Path.Combine(fixtureRoot, "absolute-recipe");
            Directory.CreateDirectory(absoluteSourceRoot);
            Directory.CreateDirectory(absoluteRecipeRoot);
            var absoluteSourcePath = Path.Combine(absoluteSourceRoot, "source.C3D");
            var absoluteRecipePath = Path.Combine(absoluteRecipeRoot, "absolute.ov3d-recipe.json");
            File.Copy(sourcePath, absoluteSourcePath);
            var absoluteDocument = stored with
            {
                Source = stored.Source with { Path = absoluteSourcePath }
            };
            ToolRecipeDocumentStore.Save(absoluteRecipePath, absoluteDocument);
            var storedAbsoluteDocument = ToolRecipeDocumentStore.Load(absoluteRecipePath);
            Check(
                "Save As keeps an out-of-bundle source absolute",
                Path.IsPathFullyQualified(storedAbsoluteDocument.Source.Path)
                && string.Equals(storedAbsoluteDocument.Source.Path, absoluteSourcePath, StringComparison.OrdinalIgnoreCase),
                storedAbsoluteDocument.Source.Path);

            var precedenceRoot = Path.Combine(fixtureRoot, "original-path-precedence");
            Directory.CreateDirectory(precedenceRoot);
            var precedenceRecipePath = Path.Combine(precedenceRoot, "absolute.ov3d-recipe.json");
            var precedenceCandidatePath = Path.Combine(precedenceRoot, "source.C3D");
            File.Copy(absoluteRecipePath, precedenceRecipePath);
            C3DHeightFieldSnapshot.CreateForVerification(
                "source.recipe-manager.precedence-mismatch",
                3,
                3,
                [10, 11, 14, 11, 14, 18, 12, 16, 22]).SaveC3D(precedenceCandidatePath);
            var precedence = new ToolWorkbenchViewModel(Path.Combine(precedenceRoot, "recent.json"));
            var precedenceOpened = precedence.TryOpenTeachingRecipe(precedenceRecipePath, out var precedenceMessage);
            Check(
                "an available original absolute source takes precedence over a same-name candidate",
                precedenceOpened
                && precedence.IsSourceReadyForRecipe
                && string.Equals(precedence.Source.Path, absoluteSourcePath, StringComparison.OrdinalIgnoreCase),
                precedenceMessage);

            var rejoinRoot = Path.Combine(fixtureRoot, "source-rejoin");
            Directory.CreateDirectory(rejoinRoot);
            var rejoinRecipePath = Path.Combine(rejoinRoot, "absolute.ov3d-recipe.json");
            var rejoinCandidatePath = Path.Combine(rejoinRoot, "source.C3D");
            File.Copy(absoluteRecipePath, rejoinRecipePath);
            File.Delete(absoluteSourcePath);
            File.Copy(sourcePath, rejoinCandidatePath);
            var rejoined = new ToolWorkbenchViewModel(Path.Combine(rejoinRoot, "recent.json"));
            var rejoinedOpened = rejoined.TryOpenTeachingRecipe(rejoinRecipePath, out var rejoinedMessage);
            Check(
                "a moved same-byte source is rejoined only after identity validation",
                rejoinedOpened
                && rejoined.IsSourceReadyForRecipe
                && string.Equals(rejoined.Source.Path, rejoinCandidatePath, StringComparison.OrdinalIgnoreCase),
                rejoinedMessage);

            var mismatchMoveRoot = Path.Combine(fixtureRoot, "source-rejoin-mismatch");
            Directory.CreateDirectory(mismatchMoveRoot);
            var mismatchMoveRecipePath = Path.Combine(mismatchMoveRoot, "absolute.ov3d-recipe.json");
            var mismatchMoveCandidatePath = Path.Combine(mismatchMoveRoot, "source.C3D");
            File.Copy(absoluteRecipePath, mismatchMoveRecipePath);
            C3DHeightFieldSnapshot.CreateForVerification(
                "source.recipe-manager.rejoin-mismatch",
                3,
                3,
                [101, 102, 104, 102, 104, 108, 103, 106, 112]).SaveC3D(mismatchMoveCandidatePath);
            var mismatchMove = new ToolWorkbenchViewModel(Path.Combine(mismatchMoveRoot, "recent.json"));
            var mismatchMoveOpened = mismatchMove.TryOpenTeachingRecipe(mismatchMoveRecipePath, out var mismatchMoveMessage);
            Check(
                "a same-name different-byte candidate is rejected instead of silently rebound",
                mismatchMoveOpened
                && !mismatchMove.IsSourceReadyForRecipe
                && string.Equals(mismatchMove.Source.Path, absoluteSourcePath, StringComparison.OrdinalIgnoreCase),
                mismatchMoveMessage);
            File.Copy(sourcePath, absoluteSourcePath);

            var failedSaveTarget = Path.Combine(fixtureRoot, "save-as-failure", "portable.ov3d-recipe.json");
            Directory.CreateDirectory(failedSaveTarget);
            var portableRecipeBytesBeforeFailure = File.ReadAllBytes(portableRecipePath);
            var portableRecipePathBeforeFailure = portableWorkbench.RecipePath;
            var failedSave = portableWorkbench.TrySaveTeachingRecipe(failedSaveTarget, out var failedSaveMessage);
            var temporaryFilesAfterFailedSave = Directory.EnumerateFiles(
                fixtureRoot,
                "*.tmp.*",
                SearchOption.AllDirectories).Any();
            Check(
                "Save As failure preserves the active recipe and cleans its temporary file",
                !failedSave
                && string.Equals(portableWorkbench.RecipePath, portableRecipePathBeforeFailure, StringComparison.OrdinalIgnoreCase)
                && portableRecipeBytesBeforeFailure.SequenceEqual(File.ReadAllBytes(portableRecipePath))
                && !temporaryFilesAfterFailedSave,
                failedSaveMessage);

            var retryRoot = Path.Combine(fixtureRoot, "save-as-retry");
            Directory.CreateDirectory(retryRoot);
            var retryRecipePath = Path.Combine(retryRoot, "portable.ov3d-recipe.json");
            var retrySaved = portableWorkbench.TrySaveTeachingRecipe(retryRecipePath, out var retrySaveMessage);
            var retryDocument = retrySaved ? ToolRecipeDocumentStore.Load(retryRecipePath) : null;
            Check(
                "a later Save As retry commits without changing the source bytes",
                retrySaved
                && retryDocument is not null
                && string.Equals(portableWorkbench.RecipePath, retryRecipePath, StringComparison.OrdinalIgnoreCase)
                && string.Equals(retryDocument.Source.ContentSha256, portableDocument?.Source.ContentSha256, StringComparison.OrdinalIgnoreCase)
                && !Directory.EnumerateFiles(fixtureRoot, "*.tmp.*", SearchOption.AllDirectories).Any(),
                retrySaveMessage);

            var recentCandidates = Enumerable.Range(0, RecipeRecentFileStore.MaximumRecipeEntries + 2)
                .Select(index => Path.Combine(fixtureRoot, $"recent-{index:000}.json"))
                .ToArray();
            RecipeRecentFileStore.Save(recentPath, recentCandidates, RecipeRecentFileStore.MaximumRecipeEntries);
            var recent = RecipeRecentFileStore.Load(recentPath, RecipeRecentFileStore.MaximumRecipeEntries);
            Check(
                "Recent recipe persistence retains the first one hundred distinct paths",
                recent.Count == RecipeRecentFileStore.MaximumRecipeEntries
                && recent.SequenceEqual(recentCandidates.Take(RecipeRecentFileStore.MaximumRecipeEntries)),
                $"count={recent.Count}; max={RecipeRecentFileStore.MaximumRecipeEntries}");

            var hundredRecentPath = Path.Combine(fixtureRoot, "hundred-recent.json");
            RecipeRecentFileStore.Save(hundredRecentPath, recentCandidates, RecipeRecentFileStore.MaximumRecipeEntries);
            var hundredRecent = new ToolWorkbenchViewModel(hundredRecentPath);
            Check(
                "Recipe Manager loads the complete one hundred-item recent list",
                hundredRecent.RecentRecipes.Count == RecipeRecentFileStore.MaximumRecipeEntries
                && hundredRecent.RecentRecipes.Select(item => item.Path)
                    .SequenceEqual(recentCandidates.Take(RecipeRecentFileStore.MaximumRecipeEntries), StringComparer.OrdinalIgnoreCase),
                $"count={hundredRecent.RecentRecipes.Count}");

            var modifiedSortPath = Path.Combine(fixtureRoot, "modified-sort-recent.json");
            var alphabeticalSortPath = Path.Combine(fixtureRoot, "alphabetical-sort-recent.json");
            File.WriteAllText(modifiedSortPath, "{}");
            File.WriteAllText(alphabeticalSortPath, "{}");
            File.SetLastWriteTimeUtc(modifiedSortPath, new DateTime(2026, 9, 24, 0, 0, 0, DateTimeKind.Utc));
            File.SetLastWriteTimeUtc(alphabeticalSortPath, new DateTime(2026, 9, 23, 0, 0, 0, DateTimeKind.Utc));
            var modifiedSortRecentPath = Path.Combine(fixtureRoot, "modified-sort-list.json");
            RecipeRecentFileStore.Save(modifiedSortRecentPath, [alphabeticalSortPath, modifiedSortPath]);
            using var modifiedSortRecent = new ToolWorkbenchViewModel(modifiedSortRecentPath);
            Check(
                "Recipe Manager defaults to recently modified order and keeps name sorting explicit",
                modifiedSortRecent.RecipeSortMode == "modified"
                && string.Equals(modifiedSortRecent.FilteredRecentRecipes[0].Path, modifiedSortPath, StringComparison.OrdinalIgnoreCase),
                $"mode={modifiedSortRecent.RecipeSortMode}; first={modifiedSortRecent.FilteredRecentRecipes[0].Path}");
            Check(
                "recipe search keeps corrupt recent entries searchable by filename and path",
                modifiedSortRecent.RecentRecipes.All(item => item.IsAvailable)
                && modifiedSortRecent.RecentRecipes.All(item => item.SearchContent.Contains(item.Name, StringComparison.OrdinalIgnoreCase)),
                $"available={modifiedSortRecent.RecentRecipes.Count(item => item.IsAvailable)}; count={modifiedSortRecent.RecentRecipes.Count}");
            modifiedSortRecent.RecipeSortMode = "name";
            Check(
                "Recipe Manager name sort uses deterministic case-insensitive names",
                modifiedSortRecent.FilteredRecentRecipes.Select(item => item.Name)
                    .SequenceEqual(modifiedSortRecent.FilteredRecentRecipes.Select(item => item.Name)
                        .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)),
                $"mode={modifiedSortRecent.RecipeSortMode}; first={modifiedSortRecent.FilteredRecentRecipes[0].Name}");

            var startupRecentPath = Path.Combine(fixtureRoot, "startup-recent.json");
            RecipeRecentFileStore.Save(startupRecentPath, [Path.Combine(fixtureRoot, "missing-recipe.json"), recipePath]);
            using var startupRecent = new ToolWorkbenchViewModel(startupRecentPath);
            Check(
                "startup recipe selection skips missing recent entries",
                string.Equals(startupRecent.MostRecentAvailableRecipePath, recipePath, StringComparison.OrdinalIgnoreCase),
                startupRecent.MostRecentAvailableRecipePath ?? "<none>");
            var startupRunCount = startupRecent.RunLog.Count;
            Check(
                "opening Recipe Center can select the most recent available recipe without loading or running it",
                startupRecent.SelectDefaultRecentRecipeIfNeeded()
                && string.Equals(startupRecent.SelectedRecentRecipe?.Path, recipePath, StringComparison.OrdinalIgnoreCase)
                && startupRecent.BrowsedRecipe?.Name == ToolRecipeDocumentStore.Load(recipePath).Name
                && string.IsNullOrWhiteSpace(startupRecent.RecipePath)
                && startupRecent.RunLog.Count == startupRunCount
                && !startupRecent.SelectDefaultRecentRecipeIfNeeded(),
                $"selected={startupRecent.SelectedRecentRecipe?.Path}; active={startupRecent.RecipePath}; runs={startupRecent.RunLog.Count}");

            var metadataRecipePath = Path.Combine(fixtureRoot, "metadata-filter-recipe.json");
            var readyRecipePath = Path.Combine(fixtureRoot, "ready-filter-recipe.json");
            using var readyWorkbench = new ToolWorkbenchViewModel(Path.Combine(fixtureRoot, "ready-filter-recent.json"));
            readyWorkbench.RecipeName = "Configured recipe";
            readyWorkbench.SetC3DSource(sourcePath);
            AddTool(readyWorkbench, "filter");
            var readySaved = readyWorkbench.TrySaveTeachingRecipe(readyRecipePath, out var readySaveMessage);
            var readyDocument = readySaved ? ToolRecipeDocumentStore.Load(readyRecipePath) : ToolRecipeDocumentStore.Load(recipePath);
            Check(
                "a configured recipe fixture passes the existing inspection validator",
                readySaved && ToolRecipeValidator.Validate(readyDocument).IsValid && readyWorkbench.IsTeachingRecipeExecutionReady,
                readySaveMessage);
            var metadataDocument = readyDocument with
            {
                Name = "Power Module Pad Check",
                Category = "방열 패드",
                InspectionTarget = "전원 모듈 A-01 상면 패드",
                Description = "기준 영역과 패드 사이의 높이 차이를 검사합니다."
            };
            ToolRecipeDocumentStore.Save(metadataRecipePath, metadataDocument);
            var searchableRecentPath = Path.Combine(fixtureRoot, "metadata-search-recent.json");
            RecipeRecentFileStore.Save(searchableRecentPath, [recipePath, metadataRecipePath]);
            using var searchableRecent = new ToolWorkbenchViewModel(searchableRecentPath);
            var searchableRunCount = searchableRecent.RunLog.Count;
            Check(
                "recipe browser loads saved names, category, target, description, and inspection readiness",
                searchableRecent.RecentRecipes.Single(item => item.Path == metadataRecipePath).Name == metadataDocument.Name
                && searchableRecent.RecentRecipes.Single(item => item.Path == metadataRecipePath).Category == metadataDocument.Category
                && searchableRecent.RecentRecipes.Single(item => item.Path == metadataRecipePath).InspectionTarget == metadataDocument.InspectionTarget
                && searchableRecent.RecentRecipes.Single(item => item.Path == metadataRecipePath).Description == metadataDocument.Description
                && searchableRecent.RecentRecipes.Single(item => item.Path == metadataRecipePath).IsInspectionReady
                && searchableRecent.RecipeCategoryOptions.Any(option => option.Key == metadataDocument.Category && option.Value == metadataDocument.Category),
                $"name={searchableRecent.RecentRecipes.Single(item => item.Path == metadataRecipePath).Name}; category={searchableRecent.RecentRecipes.Single(item => item.Path == metadataRecipePath).Category}; ready={searchableRecent.RecentRecipes.Single(item => item.Path == metadataRecipePath).IsInspectionReady}");
            searchableRecent.RecipeSearchText = "상면 패드";
            Check(
                "recipe browser search finds inspection target metadata without opening or executing",
                searchableRecent.FilteredRecentRecipes.Count == 1
                && searchableRecent.FilteredRecentRecipes[0].Path == metadataRecipePath
                && string.IsNullOrWhiteSpace(searchableRecent.RecipePath)
                && searchableRecent.RunLog.Count == searchableRunCount,
                $"matches={searchableRecent.FilteredRecentRecipes.Count}; active={searchableRecent.RecipePath}; runs={searchableRecent.RunLog.Count}");
            searchableRecent.RecipeSearchText = "기준 영역과 패드 사이";
            Check(
                "recipe browser search finds the saved description",
                searchableRecent.FilteredRecentRecipes.Count == 1
                && searchableRecent.FilteredRecentRecipes[0].Path == metadataRecipePath,
                $"matches={searchableRecent.FilteredRecentRecipes.Count}");
            var firstStoredStep = metadataDocument.Steps.FirstOrDefault();
            var firstStoredParameter = firstStoredStep?.Parameters.FirstOrDefault();
            searchableRecent.RecipeSearchText = firstStoredStep?.ToolName ?? string.Empty;
            Check(
                "recipe browser search includes saved tool names and condition fields",
                firstStoredStep is not null && firstStoredParameter is not null
                && searchableRecent.FilteredRecentRecipes.Count == 2
                && searchableRecent.FilteredRecentRecipes.All(item => item.SearchContent.Contains(firstStoredParameter.Name, StringComparison.OrdinalIgnoreCase)),
                $"steps={metadataDocument.Steps.Count}; matches={searchableRecent.FilteredRecentRecipes.Count}; parameter={firstStoredParameter?.Name}");
            searchableRecent.RecipeSearchText = string.Empty;
            searchableRecent.RecipeStatusFilter = "ready";
            searchableRecent.RecipeCategoryFilter = metadataDocument.Category!;
            Check(
                "recipe browser combines category and inspection-readiness filters",
                searchableRecent.FilteredRecentRecipes.Count == 1
                && searchableRecent.FilteredRecentRecipes[0].Path == metadataRecipePath
                && searchableRecent.HasRecentRecipeFilter,
                $"category={searchableRecent.RecipeCategoryFilter}; matches={searchableRecent.FilteredRecentRecipes.Count}");
            var setupStatusRecentPath = Path.Combine(fixtureRoot, "setup-status-recent.json");
            var draftRecipePath = Path.Combine(fixtureRoot, "teaching-draft-recipe.json");
            var missingRecentPath = Path.Combine(fixtureRoot, "missing-recent-recipe.json");
            ToolRecipeDocumentStore.Save(draftRecipePath, metadataDocument with { Name = "Teaching draft", Steps = [] });
            RecipeRecentFileStore.Save(setupStatusRecentPath, [metadataRecipePath, draftRecipePath, missingRecentPath]);
            using var setupStatusRecent = new ToolWorkbenchViewModel(setupStatusRecentPath);
            setupStatusRecent.RecipeStatusFilter = "ready";
            Check(
                "recipe browser ready filter requires the saved recipe to pass inspection validation",
                setupStatusRecent.FilteredRecentRecipes.Count == 1
                && setupStatusRecent.FilteredRecentRecipes[0].Path == metadataRecipePath,
                $"matches={setupStatusRecent.FilteredRecentRecipes.Count}");
            setupStatusRecent.RecipeStatusFilter = "draft";
            Check(
                "recipe browser draft filter identifies a saved recipe that still needs teaching",
                setupStatusRecent.FilteredRecentRecipes.Count == 1
                && setupStatusRecent.FilteredRecentRecipes[0].Path == draftRecipePath
                && !setupStatusRecent.FilteredRecentRecipes[0].IsInspectionReady,
                $"matches={setupStatusRecent.FilteredRecentRecipes.Count}");
            setupStatusRecent.RecipeStatusFilter = "missing";
            Check(
                "recipe browser missing status identifies unavailable recent paths",
                setupStatusRecent.FilteredRecentRecipes.Count == 1
                && setupStatusRecent.FilteredRecentRecipes[0].Path == missingRecentPath,
                $"matches={setupStatusRecent.FilteredRecentRecipes.Count}");
            searchableRecent.ClearRecentRecipeSearchCommand.Execute(null);
            Check(
                "clearing recipe search, category, and setup status restores the list without opening or running",
                string.IsNullOrEmpty(searchableRecent.RecipeSearchText)
                && string.IsNullOrEmpty(searchableRecent.RecipeCategoryFilter)
                && string.IsNullOrEmpty(searchableRecent.RecipeStatusFilter)
                && searchableRecent.FilteredRecentRecipes.Count == 2
                && string.IsNullOrWhiteSpace(searchableRecent.RecipePath)
                && searchableRecent.RunLog.Count == searchableRunCount,
                $"matches={searchableRecent.FilteredRecentRecipes.Count}; active={searchableRecent.RecipePath}; runs={searchableRecent.RunLog.Count}");

            var appResourcesHadThemeKey = Application.Current.Resources.Contains("Ovl3D.Wpg.SurfaceBrush");
            var browseRunCount = startupRecent.RunLog.Count;
            var browseOpenCount = 0;
            ToolWorkbenchRecipePathRequestEventArgs? browseRequest = null;
            var setupRequestCount = 0;
            startupRecent.FirstRecipeSetupRequested += (_, _) => setupRequestCount++;
            startupRecent.NewTeachingRecipeCommand.Execute(null);
            startupRecent.NewTeachingRecipeCommand.Execute(null);
            Check("new recipe requests the existing setup surface on every explicit invocation", setupRequestCount == 2
                && startupRecent.IsFirstRecipeSetupVisible && string.IsNullOrWhiteSpace(startupRecent.RecipePath), $"requests={setupRequestCount}");
            startupRecent.CancelFirstRecipeSetupCommand.Execute(null);
            startupRecent.OpenRecentTeachingRecipeRequested += (_, args) =>
            {
                browseOpenCount++;
                browseRequest = args;
            };
            startupRecent.SelectedRecentRecipe = startupRecent.RecentRecipes.Single(item => item.Path == recipePath);
            Check(
                "recipe browser reads saved content without loading the active recipe or executing",
                startupRecent.BrowsedRecipe?.Name == ToolRecipeDocumentStore.Load(recipePath).Name
                && string.IsNullOrWhiteSpace(startupRecent.RecipePath)
                && string.IsNullOrWhiteSpace(startupRecent.Source.Path)
                && startupRecent.RunLog.Count == browseRunCount && browseOpenCount == 0,
                $"preview={startupRecent.BrowsedRecipe?.Name}; opens={browseOpenCount}");
            var expectedPreviewHash = ToolRecipeDocumentStore.Load(recipePath).Source.ContentSha256;
            var previewLoaded = WaitForHeightImagePreview(startupRecent.RecipePreviewViewer);
            Check(
                "selecting a saved recipe shows its verified C3D input without changing the active recipe, source, or run log",
                previewLoaded
                && startupRecent.RecipePreviewViewer.Frame is { } previewFrame
                && string.Equals(previewFrame.SourceContentSha256, expectedPreviewHash, StringComparison.OrdinalIgnoreCase)
                && startupRecent.IsRecipePreviewVisible
                && string.IsNullOrWhiteSpace(startupRecent.RecipePath)
                && string.IsNullOrWhiteSpace(startupRecent.Source.Path)
                && startupRecent.RunLog.Count == browseRunCount,
                $"loaded={previewLoaded}; frame={startupRecent.RecipePreviewViewer.Frame?.SourceContentSha256}; expected={expectedPreviewHash}; run-log={startupRecent.RunLog.Count}/{browseRunCount}");
            var mismatchedPreviewPath = Path.Combine(fixtureRoot, "mismatched-preview.json");
            var mismatchedPreviewDocument = ToolRecipeDocumentStore.Load(recipePath);
            ToolRecipeDocumentStore.Save(
                mismatchedPreviewPath,
                mismatchedPreviewDocument with
                {
                    Source = mismatchedPreviewDocument.Source with { ContentSha256 = new string('0', 64) }
                });
            startupRecent.SelectedRecentRecipe = ToolWorkbenchRecentRecipeItem.From(mismatchedPreviewPath);
            var mismatchPreviewLoaded = WaitForHeightImagePreview(startupRecent.RecipePreviewViewer);
            Check(
                "saved-source identity mismatch clears the previous image and reports a Viewer error",
                mismatchPreviewLoaded
                && startupRecent.RecipePreviewViewer.Frame is null
                && startupRecent.RecipePreviewViewer.Error.Contains("identity", StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(startupRecent.Source.Path)
                && startupRecent.RunLog.Count == browseRunCount,
                $"loaded={mismatchPreviewLoaded}; frame={startupRecent.RecipePreviewViewer.Frame?.SourceContentSha256}; error={startupRecent.RecipePreviewViewer.Error}");
            startupRecent.SelectedRecentRecipe = startupRecent.RecentRecipes.Single(item =>
                string.Equals(item.Path, recipePath, StringComparison.OrdinalIgnoreCase));
            var previewRecovered = WaitForHeightImagePreview(startupRecent.RecipePreviewViewer);
            Check(
                "selecting a valid recipe after preview identity failure restores the matching image",
                previewRecovered
                && string.Equals(
                    startupRecent.RecipePreviewViewer.Frame?.SourceContentSha256,
                    expectedPreviewHash,
                    StringComparison.OrdinalIgnoreCase)
                && startupRecent.RunLog.Count == browseRunCount,
                $"loaded={previewRecovered}; frame={startupRecent.RecipePreviewViewer.Frame?.SourceContentSha256}; expected={expectedPreviewHash}");
            var readyRecentItem = ToolWorkbenchRecentRecipeItem.From(readyRecipePath);
            Check(
                "inspection entry is enabled only for a saved recipe that passes inspection validation",
                readyRecentItem.IsInspectionReady
                && startupRecent.OpenRecentInspectionRecipeCommand.CanExecute(readyRecentItem)
                && !startupRecent.OpenRecentInspectionRecipeCommand.CanExecute(startupRecent.SelectedRecentRecipe)
                && !startupRecent.OpenRecentInspectionRecipeCommand.CanExecute(ToolWorkbenchRecentRecipeItem.From(emptyRecipePath)),
                $"ready={readyRecentItem.IsInspectionReady}; draftCanInspect={startupRecent.OpenRecentInspectionRecipeCommand.CanExecute(startupRecent.SelectedRecentRecipe)}; incompleteCanInspect={startupRecent.OpenRecentInspectionRecipeCommand.CanExecute(ToolWorkbenchRecentRecipeItem.From(emptyRecipePath))}");
            startupRecent.OpenRecentTeachingRecipeCommand.Execute(startupRecent.SelectedRecentRecipe);
            Check("recipe browser opens for teaching only through its explicit request", browseOpenCount == 1
                && browseRequest is { Path: var teachingPath, OpenForInspection: false } && teachingPath == recipePath,
                $"opens={browseOpenCount}; inspection={browseRequest?.OpenForInspection}");
            startupRecent.OpenRecentInspectionRecipeCommand.Execute(readyRecentItem);
            Check("recipe browser explicitly requests inspection without executing the recipe", browseOpenCount == 2
                && browseRequest is { Path: var inspectionPath, OpenForInspection: true } && inspectionPath == readyRecipePath
                && startupRecent.RunLog.Count == browseRunCount, $"opens={browseOpenCount}; inspection={browseRequest?.OpenForInspection}");
            startupRecent.RecipeSortMode = "name";
            Check("recipe browser sorts name matches deterministically without opening or running",
                startupRecent.FilteredRecentRecipes.Select(item => item.Name)
                    .SequenceEqual(startupRecent.FilteredRecentRecipes.Select(item => item.Name).OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
                && startupRecent.RunLog.Count == browseRunCount && browseOpenCount == 2,
                $"sort={startupRecent.RecipeSortMode}; count={startupRecent.FilteredRecentRecipes.Count}");
            startupRecent.RecipeSortMode = "modified";
            startupRecent.RecipeSearchText = "missing-recipe";
            Check("recipe browser search filters paths without changing the active recipe", startupRecent.FilteredRecentRecipes.Count == 1
                && string.IsNullOrWhiteSpace(startupRecent.RecipePath), $"matches={startupRecent.FilteredRecentRecipes.Count}");
            startupRecent.RecipeSearchText = "r19-no-matching-recipe-000";
            Check("empty recipe search exposes an explicit reset without loading or running",
                startupRecent.FilteredRecentRecipes.Count == 0 && startupRecent.HasRecentRecipeSearchText
                && startupRecent.ClearRecentRecipeSearchCommand.CanExecute(null)
                && string.IsNullOrWhiteSpace(startupRecent.RecipePath) && startupRecent.RunLog.Count == browseRunCount,
                $"matches={startupRecent.FilteredRecentRecipes.Count}");
            startupRecent.ClearRecentRecipeSearchCommand.Execute(null);
            Check("clearing recipe search restores the list without execution",
                string.IsNullOrEmpty(startupRecent.RecipeSearchText) && !startupRecent.HasRecentRecipeSearchText
                && startupRecent.FilteredRecentRecipes.Count == startupRecent.RecentRecipes.Count
                && !startupRecent.ClearRecentRecipeSearchCommand.CanExecute(null)
                && string.IsNullOrWhiteSpace(startupRecent.RecipePath) && startupRecent.RunLog.Count == browseRunCount,
                $"matches={startupRecent.FilteredRecentRecipes.Count}");
            startupRecent.SelectedRecentRecipe = startupRecent.RecentRecipes.Single(item => !item.IsAvailable);
            Check("missing saved recipe clears previous preview and disables open", startupRecent.BrowsedRecipe is null
                && startupRecent.SelectedBrowsedStep is null && !string.IsNullOrWhiteSpace(startupRecent.RecipeBrowseError)
                && !startupRecent.OpenRecentTeachingRecipeCommand.CanExecute(startupRecent.SelectedRecentRecipe), startupRecent.RecipeBrowseError);
            startupRecent.SelectedRecentRecipe = startupRecent.RecentRecipes.Single(item => item.Path == recipePath);
            Check("saved recipe selection recovers after a failed browse", startupRecent.BrowsedRecipe is not null
                && string.IsNullOrWhiteSpace(startupRecent.RecipeBrowseError)
                && startupRecent.OpenRecentTeachingRecipeCommand.CanExecute(startupRecent.SelectedRecentRecipe), "valid selection after missing file");
            var malformedBrowsePath = Path.Combine(fixtureRoot, "malformed-browse.json");
            File.WriteAllText(malformedBrowsePath, "{ truncated");
            startupRecent.SelectedRecentRecipe = ToolWorkbenchRecentRecipeItem.From(malformedBrowsePath);
            Check("malformed browse clears saved details without changing current input or running", startupRecent.BrowsedRecipe is null
                && !string.IsNullOrWhiteSpace(startupRecent.RecipeBrowseError)
                && string.IsNullOrWhiteSpace(startupRecent.Source.Path) && startupRecent.RunLog.Count == browseRunCount,
                startupRecent.RecipeBrowseError);

            var host = new RecipeStepPropertyGridHost
            {
                Width = 620,
                Height = 360,
                SelectedObject = new WpgProbeProperties()
            };
            host.Measure(new Size(620, 360));
            host.Arrange(new Rect(0, 0, 620, 360));
            host.UpdateLayout();
            var probeCount = host.VisiblePropertyCount;
            host.SetPropertyFilter("Delta");
            host.UpdateLayout();
            var matchingCount = host.MatchingPropertyCount;
            host.SelectedObject = FilterStepProperties.From(reopenedFilter);
            host.UpdateLayout();
            var swapCount = host.VisiblePropertyCount;
            var committed = host.CommitPendingEdit(out var commitMessage);
            Check(
                "WPG host renders bool, enum, double/range categories and search",
                probeCount == 3 && host.HasCategories && matchingCount == 1,
                $"properties={probeCount}; categories={host.HasCategories}; matchingDelta={matchingCount}");
            Check(
                "WPG SelectedObject swap and CommitPendingEdit succeed",
                swapCount == 5 && committed,
                $"swapProperties={swapCount}; commit={committed}; {commitMessage}");
            Check(
                "WPG theme keys stay view-local",
                !appResourcesHadThemeKey && !Application.Current.Resources.Contains("Ovl3D.Wpg.SurfaceBrush"),
                "Application resources contain no Ovl3D.Wpg.SurfaceBrush key before or after host creation.");
            var themeResourcePairs = new (string LocalKey, string ProductKey)[]
            {
                ("Ovl3D.Wpg.SurfaceBrush", "ThreeD.PanelAlternateBrush"),
                ("Ovl3D.Wpg.PanelBrush", "ThreeD.PanelBrush"),
                ("Ovl3D.Wpg.RowHoverBrush", "ThreeD.SelectedSurfaceBrush"),
                ("Ovl3D.Wpg.NameColumnBrush", "ThreeD.CommandBarBrush"),
                ("Ovl3D.Wpg.LineBrush", "ThreeD.DividerBrush"),
                ("Ovl3D.Wpg.TextBrush", "ThreeD.TextBrush"),
                ("Ovl3D.Wpg.MutedTextBrush", "ThreeD.MutedTextBrush"),
                ("Ovl3D.Wpg.AccentBrush", "ThreeD.AccentBrush"),
                ("Ovl3D.Wpg.EditorFocusBrush", "ThreeD.FocusBrush"),
                ("Ovl3D.Wpg.EditorBackgroundBrush", "ThreeD.InputBrush"),
                ("Ovl3D.Wpg.EditorReadOnlyBrush", "ThreeD.DisabledSurfaceBrush"),
                ("Ovl3D.Wpg.EditorTextBrush", "ThreeD.PrimaryTextBrush"),
                ("Ovl3D.Wpg.EditorMutedBrush", "ThreeD.DisabledBrush")
            };
            var themeMismatches = themeResourcePairs
                .Where(pair =>
                    host.TryFindResource(pair.LocalKey) is not SolidColorBrush localBrush
                    || Application.Current.TryFindResource(pair.ProductKey) is not SolidColorBrush productBrush
                    || localBrush.Color != productBrush.Color)
                .Select(pair => $"{pair.LocalKey}->{pair.ProductKey}")
                .ToArray();
            Check(
                "WPG surface, text, editor, focus, read-only, and disabled roles alias the product theme",
                themeMismatches.Length == 0,
                themeMismatches.Length == 0
                    ? $"aliases={themeResourcePairs.Length}"
                    : string.Join(",", themeMismatches));

            host.SetPropertyFilter(string.Empty);
            host.SelectedObject = new TwoPointLineStepProperties { OutputRole = "FirstEdge" };
            host.Width = 285;
            host.Measure(new Size(285, 360));
            host.Arrange(new Rect(0, 0, 285, 360));
            host.UpdateLayout();
            host.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            host.UpdateLayout();
            Check("two-field tool hides search and clears the old filter on selection change",
                ((Grid)host.FindName("PropertySearchRow")).Visibility == Visibility.Collapsed
                && ((TextBox)host.FindName("PropertyFilterBox")).Text.Length == 0 && host.MatchingPropertyCount == host.VisiblePropertyCount,
                $"count={host.VisiblePropertyCount};matching={host.MatchingPropertyCount}");
            var policyDisplay = LocalizedPropertyGridObject.LocalizeOption(nameof(TwoPointLineConstructionPolicy.OrderedPointsDefineSegment));
            var fixedPolicy = FindVisualChildren<TextBlock>(host).FirstOrDefault(editor => editor.Text == policyDisplay);
            Check("read-only construction policy displays the complete localized value in a narrow editor",
                fixedPolicy is not null && fixedPolicy.TextWrapping == TextWrapping.Wrap && fixedPolicy.TextTrimming == TextTrimming.None
                && fixedPolicy.ActualHeight > 0 && fixedPolicy.ActualHeight >= fixedPolicy.DesiredSize.Height - 0.5,
                fixedPolicy is null ? string.Join(";", FindVisualChildren<ComboBox>(host).Select(editor => $"text={editor.Text};enabled={editor.IsEnabled}")) : $"text={fixedPolicy.Text};height={fixedPolicy.ActualHeight};wrap={fixedPolicy.TextWrapping}");

            var propertyCaptionLanguage = OpenVisionLanguageService.CurrentLanguage;
            var captionEvidenceRoot = Path.ChangeExtension(Path.GetFullPath(reportPath), null) + "-images";
            Directory.CreateDirectory(captionEvidenceRoot);
            lines.Add($"Property caption captures: {captionEvidenceRoot}");
            try
            {
                foreach (var language in new[] { OpenVisionLanguage.Korean, OpenVisionLanguage.English })
                {
                    OpenVisionLanguageService.SetLanguage(language, save: false);
                    foreach (var width in new[] { 285, 360 })
                    {
                        var cornerDraft = new LineIntersectionStepProperties
                        {
                            MaximumClosestApproachDistance = 0.001,
                            MinimumAcuteAngleDegrees = 45,
                            MaximumSupportExtension = 0,
                            OutputRole = "Corner caption check"
                        };
                        host.SelectedObject = cornerDraft;
                        host.Width = width;
                        host.Height = 500;
                        host.Measure(new Size(width, 500));
                        host.Arrange(new Rect(0, 0, width, 500));
                        host.UpdateLayout();
                        host.ScrollToTop();
                        host.UpdateLayout();
                        var captions = FindVisualChildren<TextBlock>(host)
                            .Where(text => text.GetType().Name == "PropertyNameTextBlock").ToArray();
                        var clipped = captions.Where(text =>
                        {
                            var fullText = new FormattedText(text.Text, CultureInfo.CurrentUICulture, text.FlowDirection,
                                new Typeface(text.FontFamily, text.FontStyle, text.FontWeight, text.FontStretch), text.FontSize,
                                text.Foreground, VisualTreeHelper.GetDpi(text).PixelsPerDip)
                            {
                                MaxTextWidth = Math.Max(1, text.ActualWidth)
                            };
                            return text.TextTrimming != TextTrimming.None || text.ActualHeight + 0.5 < fullText.Height;
                        }).Select(text => $"{text.Text}: {text.ActualWidth:G4}x{text.ActualHeight:G4}/{text.TextTrimming}").ToArray();
                        Check("WPG shows complete condition names in narrow Korean and English panels",
                            captions.Length == host.VisiblePropertyCount && captions.Length >= 3 && clipped.Length == 0
                            && cornerDraft.MaximumClosestApproachDistance == 0.001 && cornerDraft.MinimumAcuteAngleDegrees == 45
                            && cornerDraft.MaximumSupportExtension == 0 && cornerDraft.OutputRole == "Corner caption check",
                            $"language={language};width={width};captions={captions.Length};clipped={string.Join(";", clipped)}");
                        var captionBitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(width, 500, 96, 96, PixelFormats.Pbgra32);
                        captionBitmap.Render(host);
                        var captionEncoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                        captionEncoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(captionBitmap));
                        using var captionFile = File.Create(Path.Combine(captionEvidenceRoot, $"wpg-captions-{language}-{width}.png"));
                        captionEncoder.Save(captionFile);
                        host.ShowAdvancedParameters = false;
                        host.CommitPendingEdit(out _);
                        host.Height = 150;
                        host.Measure(new Size(width, 150));
                        host.Arrange(new Rect(0, 0, width, 150));
                        host.UpdateLayout();
                        host.ScrollToTop();
                        host.UpdateLayout();
                        var basicCaptions = FindVisualChildren<TextBlock>(host)
                            .Where(text => text.GetType().Name == "PropertyNameTextBlock").ToArray();
                        var basicBounds = basicCaptions.Select(text => text.TransformToAncestor(host)
                            .TransformBounds(new Rect(text.RenderSize))).ToArray();
                        var scrollViewer = FindVisualChildren<ScrollViewer>(host)
                            .FirstOrDefault(scroll => scroll.ScrollableHeight > 0 && scroll.ViewportHeight > 0);
                        scrollViewer?.ScrollToBottom();
                        host.UpdateLayout();
                        var lastCaptionBounds = basicCaptions[^1].TransformToAncestor(host).TransformBounds(new Rect(basicCaptions[^1].RenderSize));
                        Check("WPG conditions can be reached by scrolling at the production panel height",
                            basicCaptions.Length == 7 && basicBounds[0].Top >= 0 && basicBounds[0].Bottom <= 150
                            && scrollViewer?.VerticalOffset > 0 && lastCaptionBounds.Top >= 0 && lastCaptionBounds.Bottom <= 150,
                            $"language={language};width={width};names={basicCaptions.Length};offset={scrollViewer?.VerticalOffset:G4};lastBottom={lastCaptionBounds.Bottom:G4}");
                        var basicBitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(width, 150, 96, 96, PixelFormats.Pbgra32);
                        basicBitmap.Render(host);
                        var basicEncoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                        basicEncoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(basicBitmap));
                        using var basicCaptionFile = File.Create(Path.Combine(captionEvidenceRoot, $"wpg-captions-basic-{language}-{width}.png"));
                        basicEncoder.Save(basicCaptionFile);
                        host.ShowAdvancedParameters = true;
                        host.CommitPendingEdit(out _);
                    }
                }
            }
            finally
            {
                OpenVisionLanguageService.SetLanguage(propertyCaptionLanguage, save: false);
            }
            host.Width = 285;
            host.Height = 360;

            host.SelectedObject = new RegridHeightMapStepProperties();
            host.UpdateLayout();
            host.SetPropertyFilter("Pitch");
            Check("many-field tool retains useful search",
                host.VisiblePropertyCount > 6 && ((Grid)host.FindName("PropertySearchRow")).Visibility == Visibility.Visible
                && host.MatchingPropertyCount > 0 && host.MatchingPropertyCount < host.VisiblePropertyCount,
                $"count={host.VisiblePropertyCount};matching={host.MatchingPropertyCount}");
            host.SelectedObject = new TwoPointLineStepProperties { OutputRole = "SecondEdge" };
            host.UpdateLayout();
            Check("returning from filtered complex tool exposes all simple fields",
                ((Grid)host.FindName("PropertySearchRow")).Visibility == Visibility.Collapsed
                && ((TextBox)host.FindName("PropertyFilterBox")).Text.Length == 0 && host.MatchingPropertyCount == host.VisiblePropertyCount,
                $"count={host.VisiblePropertyCount};matching={host.MatchingPropertyCount}");

            OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
            var localizedRegrid = LocalizedPropertyGridObject.Create(new RegridHeightMapStepProperties());
            var koreanRegridProperties = TypeDescriptor.GetProperties(localizedRegrid);
            var koreanReferenceFrame = koreanRegridProperties[nameof(RegridHeightMapStepProperties.ReferenceFrameId)];
            var koreanCoverage = koreanRegridProperties[nameof(RegridHeightMapStepProperties.MinimumCoverageRatio)];
            var koreanReferenceFrameDisplayName = koreanReferenceFrame?.DisplayName;
            var koreanReferenceFrameCategory = koreanReferenceFrame?.Category;
            var koreanCoverageDisplayName = koreanCoverage?.DisplayName;
            var koreanCoverageCategory = koreanCoverage?.Category;
            var lineDraft = new TwoPointLineStepProperties { OutputRole = "SecondEdge" };
            var localizedLine = LocalizedPropertyGridObject.Create(lineDraft);
            var constructionDescriptor = TypeDescriptor.GetProperties(localizedLine)[nameof(TwoPointLineStepProperties.ConstructionPolicy)]!;
            Check("Korean option formatting preserves the typed read-only policy and output name",
                constructionDescriptor.Converter.ConvertToInvariantString(constructionDescriptor.GetValue(localizedLine)) == "두 점을 순서대로 연결"
                && constructionDescriptor.GetValue(localizedLine) is TwoPointLineConstructionPolicy.OrderedPointsDefineSegment
                && constructionDescriptor.IsReadOnly && lineDraft.OutputRole == "SecondEdge",
                $"display={constructionDescriptor.Converter.ConvertToInvariantString(constructionDescriptor.GetValue(localizedLine))};raw={lineDraft.ConstructionPolicy};role={lineDraft.OutputRole}");
            host.SelectedObject = lineDraft;
            host.Measure(new Size(285, 360));
            host.Arrange(new Rect(0, 0, 285, 360));
            host.UpdateLayout();
            var localizedPolicyText = FindVisualChildren<TextBlock>(host).FirstOrDefault(text => text.Text == "두 점을 순서대로 연결");
            Check("WPG renders the Korean fixed option without replacing the source value",
                localizedPolicyText is not null && localizedPolicyText.TextWrapping == TextWrapping.Wrap
                && localizedPolicyText.ActualWidth > 0 && localizedPolicyText.ActualHeight > 0
                && lineDraft.ConstructionPolicy == TwoPointLineConstructionPolicy.OrderedPointsDefineSegment,
                localizedPolicyText is null ? string.Join(";", FindVisualChildren<TextBlock>(host).Select(text => text.Text)) : $"text={localizedPolicyText.Text};bounds={localizedPolicyText.ActualWidth}x{localizedPolicyText.ActualHeight}");
            var initialEnumDraft = FilterStepProperties.From(reopenedFilter);
            var enumValuesBefore = new[] { initialEnumDraft.Method.ToString(), initialEnumDraft.MissingValuePolicy.ToString(), initialEnumDraft.BoundaryPolicy.ToString() };
            host.SelectedObject = initialEnumDraft;
            host.UpdateLayout();
            var closedEnumCaptions = new[]
            {
                (nameof(FilterStepProperties.Method), "중앙값"),
                (nameof(FilterStepProperties.MissingValuePolicy), "누락 위치 유지"),
                (nameof(FilterStepProperties.BoundaryPolicy), "사용 가능한 주변 셀")
            };
            var initialCaptionFailures = closedEnumCaptions.Where(expected =>
            {
                var editor = FindVisualChildren<ComboBox>(host).Single(combo => System.Windows.Automation.AutomationProperties.GetAutomationId(combo) == expected.Item1);
                var presenter = FindVisualChildren<ContentPresenter>(editor).First(content => Equals(content.Content, editor.SelectionBoxItem));
                return !FindVisualChildren<TextBlock>(presenter).Any(text => text.Text == expected.Item2);
            }).Select(expected => expected.Item1).ToArray();
            Check("initial closed enum captions are localized without changing the stored options",
                initialCaptionFailures.Length == 0 && enumValuesBefore.SequenceEqual(new[] { initialEnumDraft.Method.ToString(), initialEnumDraft.MissingValuePolicy.ToString(), initialEnumDraft.BoundaryPolicy.ToString() }),
                $"missingCaptions={string.Join(",", initialCaptionFailures)};raw={string.Join(",", enumValuesBefore)}");
            var termEdgeDraft = new HeightDifferenceEdgeStepProperties { Polarity = HeightDifferenceEdgePolarity.Rising };
            var localizedEdge = LocalizedPropertyGridObject.Create(termEdgeDraft);
            var polarityDescriptor = TypeDescriptor.GetProperties(localizedEdge)[nameof(HeightDifferenceEdgeStepProperties.Polarity)]!;
            var fallingValue = polarityDescriptor.Converter.ConvertFromInvariantString("하강 에지");
            polarityDescriptor.SetValue(localizedEdge, fallingValue);
            var standardPolarities = polarityDescriptor.Converter.GetStandardValues()!.Cast<object>().ToArray();
            Check("localized enum input round-trips to the original option and retains standard values",
                termEdgeDraft.Polarity == HeightDifferenceEdgePolarity.Falling
                && polarityDescriptor.Converter.ConvertToInvariantString(termEdgeDraft.Polarity) == "하강 에지"
                && polarityDescriptor.Converter.ConvertFromInvariantString("Rising") is HeightDifferenceEdgePolarity.Rising
                && standardPolarities.All(value => value is HeightDifferenceEdgePolarity),
                $"raw={termEdgeDraft.Polarity};options={standardPolarities.Length}");
            host.SelectedObject = termEdgeDraft;
            host.UpdateLayout();
            var polarityEditor = FindVisualChildren<ComboBox>(host).Single(editor => System.Windows.Automation.AutomationProperties.GetAutomationId(editor) == nameof(HeightDifferenceEdgeStepProperties.Polarity));
            var risingOption = polarityEditor.Items.Cast<object>().Single(value => value.ToString() == nameof(HeightDifferenceEdgePolarity.Rising));
            polarityEditor.SelectedItem = risingOption;
            host.UpdateLayout();
            Check("rendered localized enum selection updates the same typed draft",
                termEdgeDraft.Polarity == HeightDifferenceEdgePolarity.Rising
                && FindVisualChildren<TextBlock>(polarityEditor).Any(text => text.Text == "상승 에지"),
                $"raw={termEdgeDraft.Polarity};rendered={string.Join(";", FindVisualChildren<TextBlock>(polarityEditor).Select(text => text.Text))}");
            var domainDraft = new DomainMaskStepProperties();
            host.SelectedObject = domainDraft;
            host.UpdateLayout();
            Check("read-only string policy uses operator text while preserving its stored policy",
                FindVisualChildren<TextBlock>(host).Any(text => text.Text == "적용된 전체 연결 영역")
                && domainDraft.DomainPolicy == "Complete Published ConnectedRegionArtifact",
                $"raw={domainDraft.DomainPolicy};rendered={string.Join(";", FindVisualChildren<TextBlock>(host).Select(text => text.Text))}");
            var physicalDraft = new PhysicalEdgeGapFlushStepProperties { ToleranceMode = "Enabled", SectionId = "Enabled" };
            var localizedPhysical = LocalizedPropertyGridObject.Create(physicalDraft);
            var physicalDescriptors = TypeDescriptor.GetProperties(localizedPhysical);
            var toleranceDescriptor = physicalDescriptors[nameof(PhysicalEdgeGapFlushStepProperties.ToleranceMode)]!;
            toleranceDescriptor.SetValue(localizedPhysical, toleranceDescriptor.Converter.ConvertFromInvariantString("사용 안 함"));
            Check("localized string options preserve protocol values and operator identifiers",
                physicalDraft.ToleranceMode == "Disabled" && physicalDraft.SectionId == "Enabled"
                && physicalDescriptors[nameof(PhysicalEdgeGapFlushStepProperties.SectionId)]!.Converter.ConvertToInvariantString(physicalDraft.SectionId) == "Enabled",
                $"mode={physicalDraft.ToleranceMode};section={physicalDraft.SectionId}");
            var lineTool = ToolWorkbenchToolCatalog.Create().Single(tool => tool.Id == "two-point-line");
            var localizedLineStep = new ToolWorkbenchPipelineStepItem("line", lineTool, "source;points", "line.output");
            Check("normal node contracts use operator terms while raw routes remain unchanged",
                lineTool.InputContractDisplay == "높이 데이터 + 점(2)" && lineTool.OutputContractDisplay == "직선"
                && localizedLineStep.FlowInputs[0].ContractDisplay == "높이 데이터" && localizedLineStep.FlowInputs[1].ContractDisplay == "점(2)"
                && lineTool.InputContract == "Raw C3D HeightField + PointSet(2)" && lineTool.OutputContract == "LineFeature"
                && ThreeDLocalization.Shared.DisplayedOutputs == "뷰어 결과" && ThreeDLocalization.Shared.Discard == "변경 취소",
                $"input={lineTool.InputContractDisplay};output={lineTool.OutputContractDisplay};ids={localizedLineStep.InputEntityIdsText}");

            OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
            host.SelectedObject = new TwoPointLineStepProperties();
            host.CommitPendingEdit(out _);
            host.Measure(new Size(285, 360));
            host.Arrange(new Rect(0, 0, 285, 360));
            host.UpdateLayout();
            var englishPolicy = FindVisualChildren<TextBlock>(host).FirstOrDefault(text => text.Text == nameof(TwoPointLineConstructionPolicy.OrderedPointsDefineSegment));
            Check("long English fixed policy still wraps after language change",
                englishPolicy is not null && englishPolicy.TextWrapping == TextWrapping.Wrap && englishPolicy.ActualHeight > 24
                && englishPolicy.ActualHeight >= englishPolicy.DesiredSize.Height - 0.5,
                englishPolicy is null ? "English policy missing" : $"text={englishPolicy.Text};height={englishPolicy.ActualHeight};desired={englishPolicy.DesiredSize.Height}");
            var englishRegrid = LocalizedPropertyGridObject.Create(new RegridHeightMapStepProperties());
            var englishRegridProperties = TypeDescriptor.GetProperties(englishRegrid);
            var englishReferenceFrame = englishRegridProperties[nameof(RegridHeightMapStepProperties.ReferenceFrameId)];
            var englishReferenceFrameDisplayName = englishReferenceFrame?.DisplayName;
            var englishReferenceFrameCategory = englishReferenceFrame?.Category;
            OpenVisionLanguageService.SetLanguage(originalLanguage, save: false);
            Check(
                "WPG display metadata localizes without renaming the typed Re-grid properties",
                koreanReferenceFrame?.Name == nameof(RegridHeightMapStepProperties.ReferenceFrameId)
                && koreanReferenceFrameDisplayName == "기준 좌표계 ID"
                && koreanReferenceFrameCategory == "기준 그리드"
                && koreanCoverageDisplayName == "최소 유효 셀 비율"
                && koreanCoverageCategory == "결과 적용 조건"
                && englishReferenceFrame?.Name == nameof(RegridHeightMapStepProperties.ReferenceFrameId)
                && englishReferenceFrameDisplayName == "Reference frame ID"
                && englishReferenceFrameCategory == "A3 reference grid",
                $"ko={koreanReferenceFrameDisplayName}/{koreanReferenceFrameCategory}; coverage={koreanCoverageDisplayName}/{koreanCoverageCategory}; en={englishReferenceFrameDisplayName}/{englishReferenceFrameCategory}");

            var emptyMappedDraft = LocalizedPropertyGridObject.Create(new FilterStepProperties());
            var extensionDraft = LocalizedPropertyGridObject.Create(new FilterStepProperties
            {
                UnmappedParameters = "FuturePolicy=RetainMe"
            });
            var emptyMappedProperties = TypeDescriptor.GetProperties(emptyMappedDraft);
            var extensionProperties = TypeDescriptor.GetProperties(extensionDraft);
            Check(
                "WPG hides an empty unmapped-parameter row but keeps actual extension data visible",
                emptyMappedProperties[nameof(FilterStepProperties.UnmappedParameters)] is null
                && extensionProperties[nameof(FilterStepProperties.UnmappedParameters)]?.GetValue(extensionDraft) is "FuturePolicy=RetainMe",
                $"empty={emptyMappedProperties[nameof(FilterStepProperties.UnmappedParameters)] is null}; extension={extensionProperties[nameof(FilterStepProperties.UnmappedParameters)]?.GetValue(extensionDraft)}");

            var thicknessDraft = new ThicknessStepProperties { MinimumThickness = 1, MaximumThickness = 2, MinimumValidSampleCount = 3000 };
            host.SelectedObject = thicknessDraft;
            host.ShowAdvancedParameters = false;
            host.CommitPendingEdit(out _);
            host.Dispatcher.Invoke(DispatcherPriority.ContextIdle, new Action(() => host.UpdateLayout()));
            var basicThicknessCount = host.VisiblePropertyCount;
            var basicGridObject = ((System.Windows.Controls.WpfPropertyGrid.PropertyGrid)host.FindName("InnerGrid")).SelectedObject;
            var basicDescriptorNames = string.Join(",", TypeDescriptor.GetProperties(basicGridObject).Cast<PropertyDescriptor>().Select(property => property.Name));
            host.ShowAdvancedParameters = true;
            host.CommitPendingEdit(out _);
            host.Dispatcher.Invoke(DispatcherPriority.ContextIdle, new Action(() => host.UpdateLayout()));
            var advancedThicknessCount = host.VisiblePropertyCount;
            host.ShowAdvancedParameters = false;
            host.CommitPendingEdit(out _);
            host.Dispatcher.Invoke(DispatcherPriority.ContextIdle, new Action(() => host.UpdateLayout()));
            Check("WPG basic and advanced display preserve the same sampling draft", basicThicknessCount == 2
                && advancedThicknessCount == 3 && host.VisiblePropertyCount == 2
                && ReferenceEquals(host.SelectedObject, thicknessDraft) && thicknessDraft.MinimumValidSampleCount == 3000,
                $"basic={basicThicknessCount};advanced={advancedThicknessCount};restored={host.VisiblePropertyCount};samples={thicknessDraft.MinimumValidSampleCount};descriptors={basicDescriptorNames};advancedFlag={host.ShowAdvancedParameters}");

            host.SetPropertyFilter(string.Empty);
            host.UpdateLayout();
            var editor = FindTextEditors(host).FirstOrDefault(candidate => System.Windows.Automation.AutomationProperties.GetAutomationId(candidate) == nameof(ThicknessStepProperties.MinimumThickness));
            if (editor is null) throw new InvalidOperationException("Rendered minimum thickness editor was not found.");
            editor.Text = "9";
            editor.RaiseEvent(new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice, 0, editor, new Button()) { RoutedEvent = Keyboard.LostKeyboardFocusEvent });
            Check("WPG mouse focus departure retains the edited typed draft", thicknessDraft.MinimumThickness == 9 && thicknessDraft.MaximumThickness == 2,
                $"minimum={thicknessDraft.MinimumThickness};maximum={thicknessDraft.MaximumThickness}");
            host.ShowAdvancedParameters = true;
            host.CommitPendingEdit(out _);
            Check("WPG display recomposition retains an invalid-range draft without applying it", thicknessDraft.MinimumThickness == 9 && thicknessDraft.MaximumThickness == 2,
                $"minimum={thicknessDraft.MinimumThickness};maximum={thicknessDraft.MaximumThickness}");

            var originalCulture = CultureInfo.CurrentCulture;
            try
            {
                foreach (var cultureName in new[] { "en-US", "de-DE" })
                {
                    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
                    var numericDraft = new ThicknessStepProperties { MinimumThickness = 1, MaximumThickness = 2, MinimumValidSampleCount = 1 };
                    host.IsEnabled = false;
                    host.SelectedObject = numericDraft;
                    host.CommitPendingEdit(out _);
                    host.UpdateLayout();
                    host.IsEnabled = true;
                    foreach (var field in new[] { nameof(ThicknessStepProperties.MinimumThickness), nameof(ThicknessStepProperties.MinimumValidSampleCount) })
                    {
                        var numericEditor = FindTextEditors(host).Single(candidate => System.Windows.Automation.AutomationProperties.GetAutomationId(candidate) == field);
                        var binding = numericEditor.GetBindingExpression(TextBox.TextProperty)!;
                        var numericProperty = ((System.Windows.Controls.WpfPropertyGrid.PropertyItemValue)binding.DataItem).ParentProperty;
                        foreach (var malformed in new[] { "invalid-number", string.Empty, field == nameof(ThicknessStepProperties.MinimumValidSampleCount) ? "2147483648" : "1e9999x" })
                        {
                            numericEditor.SetCurrentValue(TextBox.TextProperty, malformed);
                            numericEditor.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
                            var numericCommitted = host.CommitPendingEdit(out var numericMessage);
                            Check($"WPG rejects malformed {field} in {cultureName}", !numericCommitted && Validation.GetHasError(numericEditor)
                                && numericEditor.Text == malformed && numericDraft.MinimumThickness == 1 && numericDraft.MinimumValidSampleCount == 1
                                && ReferenceEquals(binding, numericEditor.GetBindingExpression(TextBox.TextProperty))
                                && numericMessage.StartsWith(numericProperty.DisplayName + ":", StringComparison.Ordinal),
                                $"input={malformed};commit={numericCommitted};error={numericMessage}");
                        }
                        var valid = field == nameof(ThicknessStepProperties.MinimumThickness) ? (cultureName == "de-DE" ? "1,25e0" : "1.25e0") : "0x10";
                        numericEditor.SetCurrentValue(TextBox.TextProperty, valid);
                        var recoveredNumeric = host.CommitPendingEdit(out var recoveredNumericMessage);
                        Check($"WPG numeric editor recovers with its existing converter in {cultureName}", recoveredNumeric && !Validation.GetHasError(numericEditor)
                            && (field == nameof(ThicknessStepProperties.MinimumThickness) ? numericDraft.MinimumThickness == 1.25 : numericDraft.MinimumValidSampleCount == 16),
                            $"input={valid};commit={recoveredNumeric};{recoveredNumericMessage}");
                        numericEditor.SetCurrentValue(TextBox.TextProperty, "1");
                        host.CommitPendingEdit(out _);
                    }
                }
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
            }

            var messageDialog = new WpfMessageDialogControl();
            OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
            messageDialog.Configure(new WpfMessageDialogOptions
            {
                Title = "레시피 저장",
                Message = "레시피 파일을 저장할 수 없습니다.",
                Details = "Access to the selected recipe folder was denied.",
                Kind = WpfMessageDialogKind.Warning,
                Buttons = WpfMessageDialogButtons.YesNoCancel
            });
            var koreanDialogButtons = ((StackPanel)messageDialog.FindName("ButtonPanel")).Children
                .OfType<Button>()
                .Select(button => button.Content?.ToString())
                .ToArray();
            var koreanDetailsLabel = ((Button)messageDialog.FindName("DetailsToggleButton")).Content?.ToString();
            OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
            messageDialog.Configure(new WpfMessageDialogOptions
            {
                Title = "Save Recipe",
                Message = "The recipe file could not be saved.",
                Details = "Access to the selected recipe folder was denied.",
                Kind = WpfMessageDialogKind.Warning,
                Buttons = WpfMessageDialogButtons.YesNoCancel
            });
            var englishDialogButtons = ((StackPanel)messageDialog.FindName("ButtonPanel")).Children
                .OfType<Button>()
                .Select(button => button.Content?.ToString())
                .ToArray();
            var englishDetailsLabel = ((Button)messageDialog.FindName("DetailsToggleButton")).Content?.ToString();
            OpenVisionLanguageService.SetLanguage(originalLanguage, save: false);
            Check(
                "shared WPF message dialog localizes buttons and technical-details affordance",
                koreanDialogButtons.SequenceEqual(["예", "아니오", "취소"])
                && koreanDetailsLabel == "상세 정보"
                && englishDialogButtons.SequenceEqual(["Yes", "No", "Cancel"])
                && englishDetailsLabel == "Technical Details",
                $"ko={string.Join('/', koreanDialogButtons)}; details={koreanDetailsLabel} | en={string.Join('/', englishDialogButtons)}; details={englishDetailsLabel}");

            WpfMessageDialogResult requestedResult = WpfMessageDialogResult.None;
            messageDialog.DialogResultRequested += result => requestedResult = result;
            OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
            messageDialog.Configure(new WpfMessageDialogOptions
            {
                Title = "저장하지 않은 레시피",
                Message = "현재 레시피의 변경 내용을 저장하시겠습니까?",
                Kind = WpfMessageDialogKind.Question,
                Buttons = WpfMessageDialogButtons.YesNoCancel,
                PrimaryButtonText = "저장",
                SecondaryButtonText = "저장 안 함",
                TertiaryButtonText = "취소"
            });
            var lifecycleButtons = ((StackPanel)messageDialog.FindName("ButtonPanel")).Children
                .OfType<Button>()
                .ToArray();
            lifecycleButtons[1].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            OpenVisionLanguageService.SetLanguage(originalLanguage, save: false);
            Check(
                "unsaved recipe dialog maps 저장 안 함 to the No continuation branch",
                lifecycleButtons.Select(button => button.Content?.ToString()).SequenceEqual(["저장", "저장 안 함", "취소"])
                && requestedResult == WpfMessageDialogResult.No,
                $"buttons={string.Join('/', lifecycleButtons.Select(button => button.Content))}; result={requestedResult}");
        }
        catch (Exception exception)
        {
            total++;
            lines.Add($"FAIL | unhandled exception | {exception}");
        }
        finally
        {
            try
            {
                Directory.Delete(fixtureRoot, recursive: true);
            }
            catch
            {
                // The report still records a functional result if temporary cleanup is delayed.
            }
        }

        var success = total > 0 && passed == total;
        lines.Add($"RESULT | {(success ? "PASS" : "FAIL")} | {passed}/{total}");
        var fullReportPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
        File.WriteAllLines(fullReportPath, lines);
        summary = $"RecipeManagerWpg|pass={success}|checks={passed}/{total}|report={fullReportPath}";
        return success;
    }

    private static ToolWorkbenchPipelineStepItem AddTool(ToolWorkbenchViewModel workbench, string toolId)
    {
        var tool = workbench.Tools.Single(item => item.Id == toolId);
        if (SourceQualityToolGate.RequiresSourceQuality(tool.Id))
        {
            WaitForSourceQuality(workbench.SourceQuality);
        }

        workbench.SelectedTool = tool;
        if (!workbench.AddSelectedToolCommand.CanExecute(null))
        {
            throw new InvalidOperationException(
                $"Tool '{toolId}' cannot be added: {workbench.SelectedToolProposedRouteDetail}");
        }

        workbench.AddSelectedToolCommand.Execute(null);
        return workbench.SelectedPipelineStep
            ?? throw new InvalidOperationException($"Tool '{toolId}' was not added.");
    }

    private static SourceQualityReport? WaitForSourceQuality(
        SourceQualityWorkspaceViewModel workspace)
    {
        if (workspace.Report is not null || workspace.HasError)
        {
            return workspace.Report;
        }

        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(5);
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(10)
        };
        timer.Tick += (_, _) =>
        {
            if (workspace.Report is not null
                || workspace.HasError
                || DateTimeOffset.UtcNow >= deadline)
            {
                frame.Continue = false;
            }
        };
        timer.Start();
        try
        {
            Dispatcher.PushFrame(frame);
        }
        finally
        {
            timer.Stop();
        }

        return workspace.Report;
    }

    private static bool WaitForHeightImagePreview(HeightImageViewerViewModel viewer)
    {
        if (!viewer.IsObservedLoadRunning)
        {
            return true;
        }

        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(5);
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(10)
        };
        timer.Tick += (_, _) =>
        {
            if (!viewer.IsObservedLoadRunning || DateTimeOffset.UtcNow >= deadline)
            {
                frame.Continue = false;
            }
        };
        timer.Start();
        try
        {
            Dispatcher.PushFrame(frame);
        }
        finally
        {
            timer.Stop();
        }

        return !viewer.IsObservedLoadRunning;
    }

    private static string ResolvePersistedSourcePath(string recipePath, string sourcePath)
    {
        if (Path.IsPathFullyQualified(sourcePath))
        {
            return Path.GetFullPath(sourcePath);
        }

        var recipeDirectory = Path.GetDirectoryName(Path.GetFullPath(recipePath))
            ?? Environment.CurrentDirectory;
        return Path.GetFullPath(Path.Combine(recipeDirectory, sourcePath));
    }

    private static bool MatchesInvariantNumber(string text, double expected) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var actual)
        && Math.Abs(actual - expected) <= 1e-12;

    private static IEnumerable<TextBox> FindTextEditors(DependencyObject root) => FindVisualChildren<TextBox>(root);

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T editor) yield return editor;
            foreach (var descendant in FindVisualChildren<T>(child)) yield return descendant;
        }
    }

    [CategoryOrder("Probe", 0)]
    private sealed class WpgProbeProperties
    {
        [Category("Probe")]
        [PropertyOrder(0)]
        public bool Enabled { get; set; } = true;

        [Category("Probe")]
        [PropertyOrder(1)]
        public HeightDifferenceEdgePolarity Polarity { get; set; } = HeightDifferenceEdgePolarity.Rising;

        [Category("Probe")]
        [PropertyOrder(2)]
        [NumberRange(0, 100, 0.5, 2)]
        public double Delta { get; set; } = 2.5;
    }
}
