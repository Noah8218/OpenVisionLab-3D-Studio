using System.IO;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using OpenVisionLab.ThreeD.Shell;
using OpenVisionLab.ThreeD.Shell.Verification.Smoke;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Shell.Views.Workbench;

namespace OpenVisionLab.ThreeD.Verification.Shell.Workbench;

internal static class ResultsWorkspaceSectionBindingVerification
{
    public static bool VerifyCopyState(string reportPath, out string summary)
    {
        var passed = false;
        var result = string.Empty;
        var thread = new Thread(() => passed = VerifyCopyStateOnSta(reportPath, out result));
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        summary = result;
        return passed;
    }

    private static bool VerifyCopyStateOnSta(string reportPath, out string summary)
    {
        var checks = new List<(string Name, bool Passed)>();
        var originalLanguage = OpenVisionLanguageService.CurrentLanguage;
        try
        {
            using var workspace = new ResultsWorkspaceViewModel();
            var notifications = new List<string?>();
            workspace.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);
            foreach (var language in new[] { OpenVisionLanguage.English, OpenVisionLanguage.Korean })
            {
                OpenVisionLanguageService.SetLanguage(language, save: false);
                notifications.Clear();
                workspace.RefreshLocalization();
                checks.Add(($"EmptyHistorySummaryNotifies-{language}", notifications.Contains(nameof(workspace.HistorySummary))
                    && workspace.HistorySummary == (language == OpenVisionLanguage.Korean
                        ? "저장 기록 0개 · 유한 측정값 0개" : "0 saved records · 0 finite values")));
            }

            var metric = new InspectionRunMetric("ValidSampleCount", MetricKind.Count, 12960, "count", ResultStatus.Pass);
            var saved = new InspectionRunRecord(InspectionRunRecord.CurrentSchemaVersion, "copy-state",
                DateTimeOffset.Parse("2026-10-27T00:00:00Z"), new("ToolRecipe", "1", "fixture.ov3d-recipe.json", new string('A', 64)),
                new("input", Path.Combine(Path.GetDirectoryName(Path.GetFullPath(reportPath))!, "not-loaded.C3D"), new string('1', 64), 0, "raw-height"),
                "Thickness", ResultStatus.Pass, "Saved", 12, [new("Mean", MetricKind.Deviation, 8, "raw-height", ResultStatus.Pass), metric], [], "Stored", new("", null, null, null, null, null));
            var snapshot = ResultsRunSnapshot.FromRecord(saved)!;
            workspace.RefreshHistory([new("copy-state.json", "copy-state", "", "Pass", 1, true) { Snapshot = snapshot }]);
            var selector = new ComboBox { DataContext = workspace, DisplayMemberPath = "DisplayName" };
            selector.SetBinding(ItemsControl.ItemsSourceProperty, new Binding(nameof(workspace.Measurements)));
            selector.SetBinding(Selector.SelectedItemProperty, new Binding(nameof(workspace.SelectedMeasurement)) { Mode = BindingMode.TwoWay });
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
            selector.SelectedIndex = 1;
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
            var storedMetric = snapshot.Metrics.Single(item => item.Name == "ValidSampleCount");
            var key = storedMetric.Key;
            var points = workspace.TrendPoints;
            checks.Add(("ControlSelectionUpdatesMetric", workspace.SelectedMeasurement?.Key == key
                && workspace.TrendPoints.Single().Metric.Value == 12960));
            foreach (var language in new[] { OpenVisionLanguage.English, OpenVisionLanguage.Korean, OpenVisionLanguage.English })
            {
                OpenVisionLanguageService.SetLanguage(language, save: false);
                workspace.RefreshLocalization();
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
                var label = language == OpenVisionLanguage.Korean ? "유효 데이터 수" : "Valid sample count";
                checks.Add(($"MetricDisplayName-{language}", storedMetric.DisplayName == label
                    && workspace.Measurements.Single(item => item.Key == key).DisplayName == $"Thickness · {label}"));
                checks.Add(($"LanguagePreservesStoredValuesAndSelection-{language}", workspace.SelectedMeasurement?.Key == key
                    && selector.SelectedItem is ResultsMeasurementOption selected && selected.Key == key
                    && ReferenceEquals(points, workspace.TrendPoints) && !workspace.InputPreview.IsLoading
                    && storedMetric.Name == "ValidSampleCount" && storedMetric.Value == 12960
                    && storedMetric.Unit == "count" && storedMetric.Key == key));
            }
            checks.Add(("UnknownMetricNamesArePreserved", (storedMetric with { Name = "operator-custom-name" }).DisplayName == "operator-custom-name"));
            workspace.RefreshHistory([]);
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
            checks.Add(("EmptyHistoryClearsControlAndTrend", selector.SelectedItem is null
                && workspace.SelectedMeasurement is null && workspace.TrendPoints.Count == 0));
            BindingOperations.ClearAllBindings(selector);
        }
        catch (Exception exception)
        {
            checks.Add(($"Unexpected-{exception.GetBaseException().Message}", false));
        }
        finally
        {
            OpenVisionLanguageService.SetLanguage(originalLanguage, save: false);
        }
        var passed = checks.All(check => check.Passed);
        summary = $"Result={(passed ? "PASS" : "FAIL")}|{checks.Count(check => check.Passed)}/{checks.Count}";
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllLines(reportPath, checks.Select(check => $"{check.Name}={(check.Passed ? "PASS" : "FAIL")}").Append(summary));
        return passed;
    }

    public static bool Verify(string reportPath, out string summary)
    {
        List<(string Name, bool Passed, string Detail)>? checks = null;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                checks = VerifyOnSta();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        checks ??= [];
        if (failure is not null)
        {
            checks.Add(("StaSetup", false, failure.ToString().Replace(Environment.NewLine, " | ")));
        }

        var passed = checks.All(check => check.Passed);
        var lines = new List<string>
        {
            "OpenVisionLab 3D ResultsWorkspace section binding verification",
        };
        lines.AddRange(checks.Select(check =>
            $"{check.Name}={(check.Passed ? "PASS" : "FAIL")}|{check.Detail}"));
        lines.Add($"Result={(passed ? "PASS" : "FAIL")}|{checks.Count(check => check.Passed)}/{checks.Count}");

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllLines(reportPath, lines);
        summary = lines[^1];
        foreach (var line in lines)
        {
            Console.WriteLine(line);
        }

        return passed;
    }

    private static List<(string Name, bool Passed, string Detail)> VerifyOnSta()
    {
        var checks = new List<(string Name, bool Passed, string Detail)>();
        if (Application.Current is null)
        {
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.Resources.MergedDictionaries.Add(new global::Wpf.Ui.Markup.ThemesDictionary { Theme = global::Wpf.Ui.Appearance.ApplicationTheme.Dark });
            app.Resources.MergedDictionaries.Add(new global::Wpf.Ui.Markup.ControlsDictionary());
            app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/OpenVisionLab.ThreeD.Shell;component/Themes/OpenVisionThreeDTheme.xaml") });
        }

        using var view = new ResultsWorkspaceView();
        var command = new RecordingCommand();
        view.SelectSectionCommand = command;
        view.SetSection(ResultsWorkspaceSection.OutputCompare);
        checks.Add((
            "BoundCommandReceivesSection",
            command.ExecutionCount == 1
                && command.LastParameter is ResultsWorkspaceSection.OutputCompare,
            $"count={command.ExecutionCount};parameter={command.LastParameter}"));

        var disabled = new RecordingCommand { CanExecuteValue = false };
        view.SelectSectionCommand = disabled;
        view.SetSection(ResultsWorkspaceSection.Reports);
        checks.Add((
            "CanExecuteIsRespected",
            disabled.ExecutionCount == 0,
            $"count={disabled.ExecutionCount}"));

        view.SelectSectionCommand = null;
        view.SetSection(ResultsWorkspaceSection.RunRecord);
        checks.Add((
            "MissingCommandIsSafe",
            disabled.ExecutionCount == 0,
            $"count={disabled.ExecutionCount}"));

        view.ActiveSection = ResultsWorkspaceSection.Reports;
        checks.Add((
            "ActiveSectionDependencyPropertyRoundTrip",
            view.ActiveSection == ResultsWorkspaceSection.Reports,
            $"section={view.ActiveSection}"));
        checks.Add((
            "ResultsValueValiditySurfaceIsAccessible",
            view.HasResultsValueValiditySurface,
            $"surface={view.HasResultsValueValiditySurface}"));

        var details = (Expander)view.FindName("ResultsValueValidityPanel");
        var decision = (TextBlock)view.FindName("OperatorDecisionValue");
        var steps = (TextBlock)view.FindName("OperatorAffectedStepsValue");
        var meaning = (TextBlock)view.FindName("ResultsValueValidityValue");
        var header = (Border)view.FindName("ResultsWorkspaceHeader");
        var detail = view.FindName("ResultsWorkspaceDetail");
        var compactSnapshot = (TextBlock)view.FindName("CompactResultSnapshot");
        var summary = (Border)view.FindName("OperatorResultSummary");
        var immutableNotice = (Border)view.FindName("ResultsImmutableNotice");
        var navigation = (Border)view.FindName("ResultsSectionNavigation");
        var runRecordNavigation = (RadioButton)view.FindName("RunRecordNavigation");
        var content = (Grid)view.FindName("ResultsWorkspaceContent");
        var runRecordReview = (RecipePipelineReviewView)view.FindName("RunRecordReview");
        var runRecordHeader = (StackPanel)runRecordReview.FindName("RunRecordHeader");
        var runRecordEvidence = (Border)runRecordReview.FindName("RunRecordEvidenceSummary");
        var runRecordActions = (StackPanel)runRecordReview.FindName("RunRecordActionPanel");
        var drawerProfile = (HeightProfileView)view.FindName("DrawerHeightProfile");
        drawerProfile.UpdateLayout();
        checks.Add(("HtmlSectionDrawerHasNoDuplicateCoordinateFooter",
            drawerProfile.UseEmbeddedChrome && !drawerProfile.ShowCoordinateFooter
                && ((Border)drawerProfile.FindName("ProfileFooter")).Visibility == Visibility.Collapsed,
            $"embedded={drawerProfile.UseEmbeddedChrome};footer={((Border)drawerProfile.FindName("ProfileFooter")).Visibility}"));
        using var mainProfile = new HeightProfileView { UseEmbeddedChrome = true };
        mainProfile.Measure(new Size(440, 240));
        mainProfile.Arrange(new Rect(0, 0, 440, 240));
        mainProfile.UpdateLayout();
        var mainFooter = (Border)mainProfile.FindName("ProfileFooter");
        checks.Add(("MainSectionRetainsItsCoordinateFooter",
            mainProfile.ShowCoordinateFooter && mainFooter.Visibility == Visibility.Visible && mainFooter.ActualHeight == 24,
            $"footer={mainFooter.Visibility};height={mainFooter.ActualHeight}"));
        checks.Add((
            "DetailsStartCollapsedWithoutRemovingResultBindings",
            !details.IsExpanded
                && decision.FontSize >= 12
                && BindingOperations.GetBinding(decision, TextBlock.TextProperty)?.Path.Path == "ResultsOperatorDecisionDisplaySummary"
                && BindingOperations.GetBinding(steps, TextBlock.TextProperty)?.Path.Path == "ResultsOperatorAffectedStepsSummary"
                && BindingOperations.GetBinding(meaning, TextBlock.TextProperty)?.Path.Path == "ResultsValueValidityDisplaySummary",
            $"expanded={details.IsExpanded};font={decision.FontSize}"));

        var workspace = new ResultsWorkspaceViewModel();
        var localization = ThreeDLocalization.Shared;
        view.DataContext = new
        {
            IsAuthoringWorkspaceSelected = false,
            ResultsWorkspace = workspace,
            Localization = localization,
            Workbench = new { SelectedToolWorkspace = new { CurrentOutput = (SelectedToolOutputItem?)null } }
        };

        details.IsExpanded = true;
        view.ApplyResponsiveLayout(600);
        view.Measure(new Size(440, 600));
        view.Arrange(new Rect(0, 0, 440, 600));
        view.UpdateLayout();
        var expanded = details.IsExpanded;
        checks.Add((
            "V3DrawerKeepsTopTabsWithoutDuplicateHeader",
            header.Visibility == Visibility.Collapsed
                && detail is null
                && compactSnapshot.Visibility == Visibility.Visible
                && BindingOperations.GetBinding(compactSnapshot, TextBlock.TextProperty)?.Path.Path == "ResultsOperatorCompactDecision"
                && summary.Visibility == Visibility.Collapsed
                && immutableNotice.Visibility == Visibility.Collapsed
                && runRecordHeader.Visibility == Visibility.Visible
                && Grid.GetRow(content) == 1
                && Grid.GetRow(navigation) == 0,
            $"header={header.Visibility};snapshot={compactSnapshot.Visibility};compact={view.IsCompactLayout};record={runRecordReview.Visibility};runRecordHeader={runRecordHeader.Visibility};rows={Grid.GetRow(content)}/{Grid.GetRow(navigation)}"));

        view.ApplyResponsiveLayout(240);
        view.Measure(new Size(440, 240));
        view.Arrange(new Rect(0, 0, 440, 240));
        view.UpdateLayout();
        checks.Add((
            "TeachingResultsShowTableBeforeRecordManagement",
            runRecordReview.ActualHeight > 0
                && runRecordReview.ActualHeight < 360
                && runRecordHeader.Visibility == Visibility.Collapsed
                && runRecordEvidence.Visibility == Visibility.Collapsed
                && runRecordActions.Visibility == Visibility.Collapsed,
            $"height={runRecordReview.ActualHeight:F0};record={runRecordReview.Visibility};compact={view.IsCompactLayout};header={runRecordHeader.Visibility};evidence={runRecordEvidence.Visibility};actions={runRecordActions.Visibility}"));
        var columns = (Grid)runRecordReview.FindName("RunRecordColumns");
        var emptyHint = (TextBlock)runRecordReview.FindName("RunRecordEmptyHint");
        var stepsList = (ListBox)runRecordReview.FindName("RunRecordStepsList");
        runRecordReview.RunRecordSteps = Array.Empty<InspectionStepItem>();
        view.UpdateLayout();
        var emptyVisible = columns.Visibility == Visibility.Collapsed
            && emptyHint.Visibility == Visibility.Visible
            && stepsList.Visibility == Visibility.Collapsed;
        runRecordReview.RunRecordSteps = new[] { new InspectionStepItem("1", "Pad 1", "Pass", "8 raw-height") };
        view.UpdateLayout();
        checks.Add((
            "RunRecordEmptyAndPopulatedStatesAreDistinct",
            emptyVisible
                && columns.Visibility == Visibility.Visible
                && emptyHint.Visibility == Visibility.Collapsed
                && stepsList.Visibility == Visibility.Visible,
            $"empty={emptyVisible};columns={columns.Visibility};hint={emptyHint.Visibility};list={stepsList.Visibility}"));
        details.IsExpanded = false;
        view.ApplyResponsiveLayout(900);
        view.Measure(new Size(640, 900));
        view.Arrange(new Rect(0, 0, 640, 900));
        view.UpdateLayout();
        checks.Add((
            "DisclosureAndResizeDoNotExecuteSectionCommands",
            expanded && !details.IsExpanded
                && header.Visibility == Visibility.Collapsed
                && detail is null
                && compactSnapshot.Visibility == Visibility.Collapsed
                && summary.Visibility == Visibility.Visible
                && details.Visibility == Visibility.Visible
                && immutableNotice.Visibility == Visibility.Collapsed
                && Grid.GetRow(navigation) == 0
                && Grid.GetRow(content) == 1
                && runRecordEvidence.Visibility == Visibility.Visible
                && runRecordActions.Visibility == Visibility.Visible
                && runRecordHeader.Visibility == Visibility.Visible
                && view.ActiveSection == ResultsWorkspaceSection.Reports
                && command.ExecutionCount == 1 && disabled.ExecutionCount == 0,
            $"section={view.ActiveSection};compact={view.IsCompactLayout};snapshot={compactSnapshot.Visibility};summary={summary.Visibility};record={runRecordReview.Visibility};count={command.ExecutionCount};disabled={disabled.ExecutionCount}"));

        var teachingSurface = (Border)view.FindName("TeachingResultSurface");
        var teachingEmpty = (TextBlock)view.FindName("TeachingResultEmptyHint");
        var teachingTable = (DataGrid)view.FindName("TeachingResultsTable");
        var teachingSnapshot = (TextBlock)view.FindName("TeachingCompactResultSnapshot");
        var teachingOutput = new ObservableOutputSelection();
        view.DataContext = new
        {
            IsAuthoringWorkspaceSelected = true,
            ResultsWorkspace = workspace,
            Localization = localization,
            Workbench = teachingOutput
        };
        view.ApplyResponsiveLayout(240);
        view.Measure(new Size(900, 240));
        view.Arrange(new Rect(0, 0, 900, 240));
        view.UpdateLayout();
        checks.Add((
            "TeachingEmptyDoesNotShowOldRunDecision",
            teachingSurface.Visibility == Visibility.Visible
                && (string?)runRecordNavigation.Content == "결과"
                && teachingEmpty.Visibility == Visibility.Visible
                && teachingEmpty.HorizontalAlignment == HorizontalAlignment.Left
                && teachingEmpty.VerticalAlignment == VerticalAlignment.Top
                && runRecordReview.Visibility == Visibility.Collapsed
                && compactSnapshot.Visibility == Visibility.Collapsed
                && teachingSnapshot.Visibility == Visibility.Visible
                && summary.Visibility == Visibility.Collapsed,
            $"teaching={teachingSurface.Visibility};empty={teachingEmpty.Visibility};record={runRecordReview.Visibility};snapshot={compactSnapshot.Visibility}/{teachingSnapshot.Visibility}"));

        var output = new SelectedToolOutputItem(
            "thickness-output", "Pad 1", "MeasurementResult", "Preview", "",
            "Mean", "8", "raw-height", "Pass", false, false, false, false, false,
            "", "", true, "Height map → Reference", "7–9", "Within limits", true);
        teachingOutput.CurrentOutput = output;
        view.UpdateLayout();
        checks.Add((
            "TeachingCurrentResultUpdatesWithoutStageSwitch",
            teachingEmpty.Visibility == Visibility.Collapsed
                && teachingOutput.TeachingResults.Single().Status == "Pass"
                && teachingOutput.TeachingResults.Single().Value == "8"
                && teachingOutput.TeachingResults.Single().Limits == "7–9"
                && teachingOutput.TeachingResults.Single().Reason == "Within limits"
                && teachingSnapshot.Text == "Pass",
            $"empty={teachingEmpty.Visibility};decision={teachingOutput.TeachingResults.Single().Status};value={teachingOutput.TeachingResults.Single().Value};limits={teachingOutput.TeachingResults.Single().Limits};reason={teachingOutput.TeachingResults.Single().Reason}"));


        teachingOutput.AppendResult(teachingOutput.TeachingResults.Single() with { Value = "9", Status = "Fail" });
        view.UpdateLayout();
        teachingTable.SelectedItem = teachingOutput.TeachingResults[1];
        checks.Add(("MultipleTeachingRowsAndSelectionBinding",
            teachingTable.Items.Count == 2
            && ReferenceEquals(teachingTable.SelectedItem, teachingOutput.SelectedTeachingResult)
            && view.FindName("TeachingResultValueColumn") is DataGridTemplateColumn
            && teachingOutput.TeachingResults[1].Value == "9"
            && command.ExecutionCount == 1, "two real row projections; selection updates the binding without execution"));

        drawerProfile.DataContext = new { IsIndependentProfileContext = true };
        view.UpdateLayout();
        checks.Add(("IndependentProfileContextCannotCoverResults",
            drawerProfile.Visibility == Visibility.Collapsed && teachingTable.Visibility == Visibility.Visible
                && teachingSurface.Visibility == Visibility.Visible,
            $"profile={drawerProfile.Visibility};results={teachingTable.Visibility}"));
        workspace.SelectSectionCommand.Execute("Section");
        view.UpdateLayout();
        checks.Add(("IndependentProfileContextStillOpensTheSectionTab",
            drawerProfile.Visibility == Visibility.Visible && teachingSurface.Visibility == Visibility.Collapsed,
            $"profile={drawerProfile.Visibility};results={teachingSurface.Visibility}"));
        workspace.SelectSectionCommand.Execute("RunRecord");

        var otherStep = new ToolWorkbenchPipelineStepItem("step.other", ToolWorkbenchToolCatalog.Create().First(item => item.Id == "thickness"), "", "other-output");
        teachingOutput.AppendResult(teachingOutput.TeachingResults[0] with { Step = otherStep, Value = "8.1234567890123456", Unit = "raw-height" });
        view.Measure(new Size(640, 240));
        view.Arrange(new Rect(0, 0, 640, 240));
        view.UpdateLayout();
        var filter = (CheckBox)view.FindName("TeachingSelectedToolFilter");
        filter.SetCurrentValue(CheckBox.IsCheckedProperty, true);
        view.UpdateLayout();
        var sameStepRow = (DataGridRow)teachingTable.ItemContainerGenerator.ContainerFromIndex(0);
        var selectedRow = (DataGridRow)teachingTable.ItemContainerGenerator.ContainerFromIndex(1);
        var otherRow = (DataGridRow)teachingTable.ItemContainerGenerator.ContainerFromIndex(2);
        checks.Add(("SelectedToolFilterKeepsEveryOutputOfTheSameTool",
            workspace.ShowOnlySelectedTool && sameStepRow.Visibility == Visibility.Visible
                && selectedRow.Visibility == Visibility.Visible && otherRow.Visibility == Visibility.Collapsed
                && teachingTable.Items.Count == 3 && command.ExecutionCount == 1,
            $"same={sameStepRow.Visibility};selected={selectedRow.Visibility};other={otherRow.Visibility};rows={teachingTable.Items.Count}"));
        teachingOutput.SelectedTeachingResult = teachingOutput.TeachingResults[2];
        view.UpdateLayout();
        checks.Add(("SelectedToolChangeRefreshesFilterWithoutExecution",
            sameStepRow.Visibility == Visibility.Collapsed && otherRow.Visibility == Visibility.Visible
                && ReferenceEquals(teachingTable.SelectedItem, teachingOutput.SelectedTeachingResult)
                && teachingOutput.TeachingResults[2].Value == "8.1234567890123456"
                && teachingOutput.TeachingResults[2].Unit == "raw-height" && command.ExecutionCount == 1,
            $"other={otherRow.Visibility};value={teachingOutput.TeachingResults[2].Value};count={command.ExecutionCount}"));
        workspace.ShowOnlySelectedTool = false;
        view.UpdateLayout();
        checks.Add(("FilterRestoresAllRowsAndBindingAtCompactWidth",
            filter.IsChecked == false && sameStepRow.Visibility == Visibility.Visible && otherRow.Visibility == Visibility.Visible
                && teachingTable.Columns.All(column => column.ActualWidth >= column.MinWidth)
                && teachingTable.Columns.Sum(column => column.ActualWidth) > teachingTable.ActualWidth,
            $"table={teachingTable.ActualWidth:F1};columns={teachingTable.Columns.Sum(column => column.ActualWidth):F1}"));
        teachingOutput.AppendResult(teachingOutput.TeachingResults[0] with { Value = "Mean: 7.99999998656911", Unit = "raw-height", Reason = "기준면과의 높이 차이가 모두 규격 안에 있습니다." });
        view.Measure(new Size(434, 240));
        view.Arrange(new Rect(0, 0, 434, 240));
        view.UpdateLayout();
        var narrowRow = (DataGridRow)teachingTable.ItemContainerGenerator.ContainerFromIndex(0);
        checks.Add(("NarrowPanelKeepsRowsReadableWithHorizontalScroll",
            narrowRow.ActualHeight <= 33 && teachingTable.Columns.All(column => column.ActualWidth >= column.MinWidth),
            $"rowHeight={narrowRow.ActualHeight:F1};table={teachingTable.ActualWidth:F1};columns={string.Join(",", teachingTable.Columns.Select(column => column.ActualWidth.ToString("F1")))}"));

        teachingOutput.CurrentOutput = output with { ResultStatus = "Not executed", HasExecutionResult = false };
        view.UpdateLayout();
        checks.Add((
            "StatusWithoutExecutionDoesNotCreateResultRow",
            teachingEmpty.Visibility == Visibility.Visible,
            $"empty={teachingEmpty.Visibility}"));

        teachingOutput.CurrentOutput = output;
        view.UpdateLayout();
        var exact = 8.1234567890123456;
        var metrics = new Metric[] { new("Mean", MetricKind.Length, exact, "raw-height", ResultStatus.Pass),
            new("ValidSampleCount", MetricKind.Count, 7196, "count"), new("LowerLimit", MetricKind.Length, 7.75, "raw-height"),
            new("UpperLimit", MetricKind.Length, 8.25, "raw-height") };
        var dense = teachingOutput.TeachingResults[0] with { Value = exact.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            Metrics = metrics, ExecutionStatus = ResultStatus.Pass, Sequence = 1, RecordedAt = DateTimeOffset.Parse("2026-10-08T00:40:00Z") };
        var labelSource = new ToolRecipeSelectionSourceBinding(Format: "C3D", ContentSha256: new string('A', 64), GridWidth: 1280, GridHeight: 840);
        var labelAnchor = new ToolRecipeSelection("selection.fixture.measurement-roi", "Measure", "GridRectangle", "source.fixture", "frame.fixture",
            labelSource, new(156, 196, 144, 90), null, null);
        dense = dense with { Step = new(dense.Step.Id, dense.Step.Tool, "source.fixture", dense.Step.OutputEntityId,
            dualRoiRouting: new(null, labelAnchor.Id)), ResultLabel = new() };
        workspace.RefreshTeachingResultLabels([dense], [labelAnchor], "fixture.recipe");
        checks.Add(("ResultDisplayKeepsExactValueAndUsesTypedUnitSamplesAndTime",
            dense.DisplayValue == "8.12" && dense.Value == "8.123456789012346" && dense.Unit == "raw-height"
                && dense.DisplayLimits == "7.75 – 8.25" && dense.ValidSamples == "7,196" && dense.OutputName == "두께"
                && dense.RunTime == dense.RecordedAt.Value.ToLocalTime().ToString("HH:mm:ss") && dense.StatusText == "OK"
                && ReferenceEquals(dense.Metrics, metrics), $"display={dense.DisplayValue};exact={dense.Value};samples={dense.ValidSamples};time={dense.RunTime}"));
        var stale = dense with { IsCurrent = false };
        var error = dense with { ExecutionStatus = ResultStatus.Error };
        var mismatchedUnit = dense with { Metrics = [metrics[0], metrics[1], metrics[2] with { Unit = "mm" }, metrics[3] with { Unit = "mm" }] };
        checks.Add(("UnavailableStaleErrorAndDifferentUnitDoNotInventMarginOrMeasurement",
            !stale.HasMarginScale && stale.DisplayValue == dense.DisplayValue && stale.StatusText == "이전 결과"
                && !error.HasMarginScale && error.DisplayValue == "—" && !mismatchedUnit.HasMarginScale
                && (dense with { Metrics = [] , RecordedAt = null }).ValidSamples == "—"
                && (dense with { Metrics = [] , RecordedAt = null }).RunTime == "—"
                && dense.HasMarginScale && dense.MarginMarkerOffset > 30 && dense.MarginMarkerOffset < 80,
            $"stale={stale.StatusText};error={error.DisplayValue};marker={dense.MarginMarkerOffset:F1}"));
        teachingOutput.AppendResult(dense);
        teachingOutput.SelectedTeachingResult = dense;
        view.Measure(new Size(970, 230)); view.Arrange(new Rect(0, 0, 970, 230)); view.UpdateLayout();
        var denseRow = (DataGridRow)teachingTable.ItemContainerGenerator.ContainerFromIndex(teachingTable.Items.Count - 1);
        checks.Add(("NineColumnSelectedRowKeepsMockupDensityAndFullUnit",
            teachingTable.Columns.Count == 9 && denseRow.ActualHeight <= 33 && teachingTable.RowDetailsVisibilityMode == DataGridRowDetailsVisibilityMode.Collapsed,
            $"columns={teachingTable.Columns.Count};selectedRow={denseRow.ActualHeight:F2}"));
        var detailsButton = (ToggleButton)view.FindName("TeachingDetailsButton");
        var detailPopup = (Popup)view.FindName("TeachingDetailsPopup");
        var detailContent = (Border)view.FindName("TeachingDetailsContent");
        var fixtureRoot = Environment.GetEnvironmentVariable("OVL3D_RESULTS_PARITY_EVIDENCE");
        var application = Application.Current!;
        var previousShutdown = application.ShutdownMode;
        application.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var fixtureWindow = new Window { Title = "Results parity isolated fixture", Width = 970, Height = 330,
            WindowStyle = WindowStyle.None, ShowActivated = false, ShowInTaskbar = false, Content = view };
        ShellWindowNativeInterop.TryGetLeftmostWorkAreaOrigin(out var left, out var top);
        fixtureWindow.Left = left; fixtureWindow.Top = top + 160;
        try
        {
            fixtureWindow.Show();
            view.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
            if (!string.IsNullOrWhiteSpace(fixtureRoot))
            {
                Directory.CreateDirectory(fixtureRoot);
                ShellWindowNativeInterop.AppendWindowMonitorEvidence(fixtureWindow, Path.Combine(fixtureRoot, "results-layout.txt"));
            }
            var grip = (AvalonDock.Controls.AnchorablePaneTitle)view.FindName("TeachingResultsGrip");
            var layers = (FrameworkElement)view.FindName("TeachingResultsLayersIcon");
            var dockMenu = (MenuItem)view.FindName("TeachingResultsDockMenu");
            var dockPin = (Button)view.FindName("TeachingResultsAutoHide");
            var drawerHeader = (Border)view.FindName("ResultsSectionNavigation");
            checks.Add(("TeachingHeaderRendersOneRowWithGripLayersAndDockActions",
                Math.Abs(drawerHeader.ActualHeight - 34) < .1 && grip.ActualWidth == 10 && grip.ActualHeight == 16
                    && layers.ActualWidth == 14 && layers.ActualHeight == 14 && dockMenu.ActualWidth == 24 && dockMenu.ActualHeight == 24
                    && dockPin.ActualWidth == 24 && dockPin.ActualHeight == 24
                    && grip.TransformToAncestor(drawerHeader).Transform(new Point()).X < layers.TransformToAncestor(drawerHeader).Transform(new Point()).X
                    && layers.TransformToAncestor(drawerHeader).Transform(new Point()).X < runRecordNavigation.TransformToAncestor(drawerHeader).Transform(new Point()).X
                    && dockMenu.TransformToAncestor(drawerHeader).Transform(new Point()).X < dockPin.TransformToAncestor(drawerHeader).Transform(new Point()).X,
                $"header={drawerHeader.ActualHeight};grip={grip.ActualWidth}x{grip.ActualHeight};layers={layers.ActualWidth}x{layers.ActualHeight};menu={dockMenu.ActualWidth}x{dockMenu.ActualHeight};pin={dockPin.ActualWidth}x{dockPin.ActualHeight}"));
            var headerEdges = new List<string>();
            var headersAligned = true;
            for (var columnIndex = 1; columnIndex < 9; columnIndex++)
            {
                var columnHeader = FindVisual<DataGridColumnHeader>(teachingTable, header => ReferenceEquals(header.Column, teachingTable.Columns[columnIndex]));
                var headerText = FindVisual<ContentPresenter>(columnHeader);
                var rightAligned = columnIndex is 3 or 4 or 7;
                var edge = headerText is null ? double.NaN : headerText.TransformToAncestor(columnHeader!).Transform(new Point()).X + (rightAligned ? headerText.ActualWidth : 0);
                var expectedEdge = rightAligned ? columnHeader?.ActualWidth - 10 : 10;
                headersAligned &= expectedEdge.HasValue && Math.Abs(edge - expectedEdge.Value) < 1;
                headerEdges.Add($"{columnIndex}:{(rightAligned ? "right" : "left")}:{edge:F2}/{expectedEdge:F2}");
            }
            checks.Add(("TeachingNumericHeaderAlignmentUsesRenderedContentEdges", headersAligned, string.Join(",", headerEdges)));
            var headerTimeCell = FindVisual<TextBlock>(teachingTable.Columns[8].GetCellContent(dense), text => text.Text == dense.RunTime);
            var limitsCell = FindVisual<TextBlock>(teachingTable.Columns[4].GetCellContent(dense), text => text.Text == dense.DisplayLimits);
            var samplesCell = FindVisual<TextBlock>(teachingTable.Columns[7].GetCellContent(dense), text => text.Text == dense.ValidSamples);
            checks.Add(("TeachingTimeStartsLeftAndMetadataUsesSecondaryColor",
                headerTimeCell?.TextAlignment == TextAlignment.Left && new[] { headerTimeCell, limitsCell, samplesCell }.All(text => text?.Foreground is SolidColorBrush brush && brush.Color == Color.FromRgb(139, 152, 166)),
                $"time={headerTimeCell?.TextAlignment};colors={string.Join(",", new[] { headerTimeCell, limitsCell, samplesCell }.Select(text => text?.Foreground))}"));
            checks.Add(("TeachingDockActionsRemainDisabledWithoutOwner", !dockPin.IsEnabled && !grip.IsEnabled && !((MenuItem)view.FindName("TeachingResultsAutoHideAction")).IsEnabled, "missing native layout owner cannot invoke docking"));
            var hideCommand = new RecordingCommand();
            var dockModel = new AvalonDock.Layout.LayoutAnchorable { CanFloat = false, CanClose = false, CanHide = true };
            view.DockPaneCommands = new { LayoutElement = dockModel, AutoHideCommand = hideCommand, FloatCommand = disabled };
            view.UpdateLayout();
            var pinClick = typeof(Button).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            pinClick.Invoke(dockPin, null);
            checks.Add(("TeachingPinUsesBoundCommandAndExistingNativeModel",
                ReferenceEquals(grip.Model, dockModel) && ReferenceEquals(dockPin.Command, hideCommand)
                    && ReferenceEquals(((MenuItem)view.FindName("TeachingResultsAutoHideAction")).Command, hideCommand)
                    && hideCommand.ExecutionCount == 1 && hideCommand.LastParameter is null && command.ExecutionCount == 1,
                "isolated native OnClick; bound command once; no result command; not actual pointer input"));
            view.DockPaneCommands = null;
            view.UpdateLayout();
            checks.Add(("TeachingDockOwnerRemovalClearsModelAndDisablesActions", grip.Model is null && !dockPin.IsEnabled && !grip.IsEnabled, "binding teardown restores unavailable state"));
            var eye = FindVisual<ToggleButton>(teachingTable.Columns[0].GetCellContent(dense), _ => true)!;
            // IToggleProvider only calls OnToggle; this isolated fixture exercises native OnClick and its bound command.
            var nativeClick = typeof(ToggleButton).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            void ClickEye() => nativeClick.Invoke(eye, null);
            checks.Add(("NativeResultEyeBindsActualLabelAndRawSource",
                eye.IsEnabled && eye.IsChecked == true && ReferenceEquals(eye.Command, workspace.ToggleTeachingResultLabelCommand)
                    && ReferenceEquals(eye.CommandParameter, dense.ResultLabel) && workspace.ToggleTeachingResultLabelCommand.CanExecute(dense.ResultLabel)
                    && workspace.TeachingResultLabels.Single().Value == exact && workspace.TeachingResultLabels[0].Anchor == labelAnchor,
                $"enabled={eye.IsEnabled};checked={eye.IsChecked};raw={workspace.TeachingResultLabels[0].Value:R}"));
            ClickEye();
            view.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
            checks.Add(("NativeResultEyeClickUpdatesBindingWithoutExecuting",
                eye.IsChecked == false && !dense.ResultLabel.IsVisible && !workspace.TeachingResultLabels[0].IsVisible
                    && command.ExecutionCount == 1 && ReferenceEquals(dense.Metrics, metrics) && ReferenceEquals(teachingOutput.SelectedTeachingResult, dense),
                $"checked={eye.IsChecked};visible={dense.ResultLabel.IsVisible};execution={command.ExecutionCount}"));
            var neighbor = dense with { Step = new("step.neighbor", dense.Step.Tool, "source.fixture", "output.neighbor",
                dualRoiRouting: new(null, labelAnchor.Id)), ResultLabel = new() };
            workspace.RefreshTeachingResultLabels([dense, neighbor], [labelAnchor], "fixture.recipe");
            checks.Add(("OneRowTogglePreservesOtherRowVisibilityAndRawValues",
                !dense.ResultLabel.IsVisible && neighbor.ResultLabel.IsVisible && workspace.TeachingResultLabels.Count == 2
                    && workspace.TeachingResultLabels.All(label => label.Value == exact) && command.ExecutionCount == 1,
                $"selected={dense.ResultLabel.IsVisible};neighbor={neighbor.ResultLabel.IsVisible};execution={command.ExecutionCount}"));
            var rebuiltLabelRow = dense with { ResultLabel = new() };
            workspace.RefreshTeachingResultLabels([rebuiltLabelRow], [labelAnchor], "fixture.recipe");
            checks.Add(("SameRecipeAndSourceRetainHiddenChoiceAcrossProjection",
                !rebuiltLabelRow.ResultLabel.IsVisible && !workspace.TeachingResultLabels[0].IsVisible
                    && workspace.TeachingResultLabels[0].Value == exact,
                $"visible={rebuiltLabelRow.ResultLabel.IsVisible};raw={workspace.TeachingResultLabels[0].Value:R}"));
            var changedSourceAnchor = labelAnchor with { SourceBinding = labelSource with { ContentSha256 = new string('B', 64) } };
            workspace.RefreshTeachingResultLabels([rebuiltLabelRow], [changedSourceAnchor], "fixture.recipe");
            checks.Add(("DifferentSourceAndRecipeDoNotInheritHiddenChoice",
                rebuiltLabelRow.ResultLabel.IsVisible && workspace.TeachingResultLabels[0].Anchor.SourceBinding.ContentSha256 == new string('B', 64),
                $"visible={rebuiltLabelRow.ResultLabel.IsVisible};source={workspace.TeachingResultLabels[0].Anchor.SourceBinding.ContentSha256}"));
            workspace.ToggleTeachingResultLabelCommand.Execute(rebuiltLabelRow.ResultLabel);
            workspace.RefreshTeachingResultLabels([rebuiltLabelRow], [changedSourceAnchor], "another.recipe");
            checks.Add(("DifferentRecipeWithSameSourceResetsOnlyLabelVisibility",
                rebuiltLabelRow.ResultLabel.IsVisible && workspace.TeachingResultLabels[0].Value == exact
                    && workspace.TeachingResultLabels[0].Anchor == changedSourceAnchor && command.ExecutionCount == 1,
                $"visible={rebuiltLabelRow.ResultLabel.IsVisible};raw={workspace.TeachingResultLabels[0].Value:R};execution={command.ExecutionCount}"));
            workspace.RefreshTeachingResultLabels([dense], [labelAnchor], "another.recipe");
            ClickEye(); ClickEye();
            view.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
            checks.Add(("RepeatedNativeToggleRestoresVisibleWithoutChangingMeasurement",
                dense.ResultLabel.IsVisible && eye.IsChecked == true && workspace.TeachingResultLabels[0].Value == exact
                    && command.ExecutionCount == 1 && ReferenceEquals(dense.Metrics, metrics),
                $"visible={dense.ResultLabel.IsVisible};checked={eye.IsChecked};execution={command.ExecutionCount}"));
            workspace.RefreshTeachingResultLabels([dense], [], "another.recipe");
            view.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
            var noRoiDisabled = !eye.IsEnabled && !workspace.ToggleTeachingResultLabelCommand.CanExecute(dense.ResultLabel)
                && workspace.TeachingResultLabels.Count == 0 && dense.Metrics[0].Value == exact;
            workspace.RefreshTeachingResultLabels([dense], [labelAnchor], "fixture.recipe");
            view.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
            checks.Add(("NoRoiDisablesEyeAndValidRecoveryRestoresIt",
                noRoiDisabled && eye.IsEnabled && eye.IsChecked == true && workspace.TeachingResultLabels.Single().Value == exact,
                $"disabledWithoutRoi={noRoiDisabled};recoveredEnabled={eye.IsEnabled};checked={eye.IsChecked}"));
            var errorLabelRow = error with { Step = dense.Step, ResultLabel = new() };
            var staleLabelRow = stale with { Step = dense.Step, ResultLabel = new() };
            workspace.RefreshTeachingResultLabels([errorLabelRow], [labelAnchor], "fixture.recipe");
            var errorLabel = workspace.TeachingResultLabels.Single();
            workspace.RefreshTeachingResultLabels([staleLabelRow], [labelAnchor], "fixture.recipe");
            checks.Add(("ErrorAndPreviousLabelsKeepActualStatusAndValue",
                errorLabel.Value is null && errorLabel.Status is ResultStatus.Error && errorLabel.Caption.Contains("오류")
                    && !workspace.TeachingResultLabels[0].IsCurrent && workspace.TeachingResultLabels[0].Value == exact
                    && workspace.TeachingResultLabels[0].Caption.StartsWith("↻"),
                $"error={errorLabel.Caption};previous={workspace.TeachingResultLabels[0].Caption}"));
            workspace.RefreshTeachingResultLabels([dense], [labelAnchor], "fixture.recipe");
            detailsButton.IsChecked = true;
            view.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
            view.UpdateLayout();
            checks.Add(("DetailPopupUsesSelectedExactResultWithoutInspectionExecution",
                detailPopup.IsOpen && ReferenceEquals(detailContent.DataContext, dense) && command.ExecutionCount == 1,
                $"open={detailPopup.IsOpen};selected={ReferenceEquals(detailContent.DataContext, dense)};execute={command.ExecutionCount}"));
            if (!string.IsNullOrWhiteSpace(fixtureRoot))
            {
                Directory.CreateDirectory(fixtureRoot);
                ShellWindowNativeInterop.AppendWindowMonitorEvidence(fixtureWindow, Path.Combine(fixtureRoot, "results-fixture-monitor.txt"));
                SaveFixture(view, Path.Combine(fixtureRoot, "results-wide-fixture.png"));
                SaveFixture(detailContent, Path.Combine(fixtureRoot, "results-detail-fixture.png"));
            }
            detailsButton.IsChecked = false;
            checks.Add(("DetailPopupClosesAndResultRemainsExact", !detailPopup.IsOpen && ReferenceEquals(teachingOutput.SelectedTeachingResult, dense)
                && metrics[0].Value == exact && command.ExecutionCount == 1, $"closed={!detailPopup.IsOpen};execute={command.ExecutionCount}"));
            fixtureWindow.Width = 434; fixtureWindow.Height = 230;
            view.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle); view.UpdateLayout();
            if (!string.IsNullOrWhiteSpace(fixtureRoot)) SaveFixture(view, Path.Combine(fixtureRoot, "results-compact-fixture.png"));
            var tableScroll = FindVisual<ScrollViewer>(teachingTable)!;
            tableScroll.ScrollToRightEnd();
            view.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
            view.UpdateLayout();
            var timeCell = teachingTable.Columns[8].GetCellContent(dense);
            var timePosition = timeCell?.TransformToAncestor(teachingTable).Transform(new Point());
            checks.Add(("CompactHorizontalScrollReachesActualTimeAndPreservesSelection",
                tableScroll.HorizontalOffset > 0 && timePosition is { X: >= 0 } && timePosition.Value.X < teachingTable.ActualWidth
                    && (timeCell as TextBlock)?.Text == dense.RunTime && ReferenceEquals(teachingOutput.SelectedTeachingResult, dense)
                    && command.ExecutionCount == 1,
                $"offset={tableScroll.HorizontalOffset:F1};timeX={timePosition?.X:F1};time={(timeCell as TextBlock)?.Text}"));
            if (!string.IsNullOrWhiteSpace(fixtureRoot)) SaveFixture(view, Path.Combine(fixtureRoot, "results-compact-scrolled-fixture.png"));
            tableScroll.ScrollToLeftEnd();

            var longStep = new ToolWorkbenchPipelineStepItem("step.long", dense.Step.Tool, "", "long-output",
                toolName: "상단 이송 프레임과 기준 높이를 사용하는 긴 측정 영역 두께 검사 이름");
            var longResult = dense with { Step = longStep };
            teachingOutput.AppendResult(longResult);
            teachingOutput.SelectedTeachingResult = longResult;
            teachingTable.ScrollIntoView(longResult);
            view.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
            view.UpdateLayout();
            var longRow = (DataGridRow)teachingTable.ItemContainerGenerator.ContainerFromItem(longResult);
            var longCell = teachingTable.Columns[1].GetCellContent(longResult);
            var longText = FindVisual<TextBlock>(longCell, text => text.Text == longStep.ToolName);
            checks.Add(("LongStoredToolNameWrapsWithoutRemovingAnyText",
                longText is { TextWrapping: TextWrapping.Wrap, TextTrimming: TextTrimming.None } && longText.Text == longStep.ToolName
                    && longRow.ActualHeight > 32 && longText.ActualHeight <= longRow.ActualHeight && longCell is not null
                    && longText.DesiredSize.Height <= longText.ActualHeight + 1 && command.ExecutionCount == 1,
                $"row={longRow.ActualHeight:F1};text={longText?.ActualHeight:F1};desired={longText?.DesiredSize.Height:F1}"));
            if (!string.IsNullOrWhiteSpace(fixtureRoot)) SaveFixture(view, Path.Combine(fixtureRoot, "results-long-name-fixture.png"));

            teachingOutput.AppendResult(error);
            teachingOutput.AppendResult(stale);
            fixtureWindow.Width = 970; fixtureWindow.Height = 330;
            view.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
            teachingTable.ScrollIntoView(stale);
            view.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle); view.UpdateLayout();
            var previousText = FindVisual<TextBlock>(teachingTable.Columns[6].GetCellContent(stale), text => text.Text.Contains("이전 결과"));
            checks.Add(("FailureAndPreviousPillsRetainTextAndHaveNoInventedNeedle",
                FindVisual<TextBlock>(teachingTable.Columns[6].GetCellContent(error), text => text.Text.Contains("오류")) is not null
                    && previousText is { TextWrapping: TextWrapping.Wrap, TextTrimming: TextTrimming.None }
                    && previousText.DesiredSize.Width <= previousText.ActualWidth + 1 && previousText.DesiredSize.Height <= previousText.ActualHeight + 1
                    && !error.HasMarginScale && !stale.HasMarginScale && error.Metrics[0].Value == exact && command.ExecutionCount == 1,
                $"error={error.StatusText};previous={stale.StatusText};exact={error.Metrics[0].Value:R};previousSize={previousText?.ActualWidth:F1}x{previousText?.ActualHeight:F1};desired={previousText?.DesiredSize}"));
            if (!string.IsNullOrWhiteSpace(fixtureRoot)) SaveFixture(view, Path.Combine(fixtureRoot, "results-failure-previous-fixture.png"));
            teachingOutput.CurrentOutput = output;
            teachingOutput.SelectedTeachingResult = teachingOutput.TeachingResults.Single();
            view.UpdateLayout();
            checks.Add(("RecoveryRestoresOneCurrentResultWithoutExecuting",
                teachingTable.Items.Count == 1 && teachingEmpty.Visibility == Visibility.Collapsed
                    && teachingOutput.TeachingResults.Single().StatusText == "OK" && command.ExecutionCount == 1,
                $"rows={teachingTable.Items.Count};status={teachingOutput.TeachingResults.Single().StatusText}"));
            var actualDrawerContext = drawerProfile.DataContext;
            drawerProfile.DataContext = new { ProfileHasEndpoints = true, ProfileAllSamplesMissing = false, ProfileHasInput = true, ProfileIsCollapsed = false, ProfileBodyVisible = true, ProfileActionCommand = disabled };
            drawerProfile.SetCurrentValue(HeightProfileView.ProfileStateProperty, OpenVisionLab.ThreeD.Viewer.Hosting.ViewerHostProfileState.Empty with { Visible = true, HasEndpoints = true });
            workspace.SelectSectionCommand.Execute("Section");
            fixtureWindow.Width = 434;
            view.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
            view.UpdateLayout();
            var profileToolbar = (StackPanel)view.FindName("DrawerProfileToolbar");
            var toolbarTop = profileToolbar.TransformToAncestor(drawerHeader).Transform(new Point()).Y;
            var tabBottom = runRecordNavigation.TransformToAncestor(drawerHeader).Transform(new Point()).Y + runRecordNavigation.ActualHeight;
            checks.Add(("D54NarrowSectionHeaderKeepsTabsAndToolbarInSeparateRows",
                drawerProfile.IsTeachingDrawerNarrow && view.MinHeight == 0 && Math.Abs(drawerHeader.ActualHeight - 64) < .1
                    && profileToolbar.IsVisible && toolbarTop >= tabBottom && profileToolbar.ActualHeight == 26
                    && profileToolbar.TransformToAncestor(drawerHeader).Transform(new Point()).X + profileToolbar.ActualWidth <= drawerHeader.ActualWidth,
                $"width={drawerProfile.ActualWidth};header={drawerHeader.ActualHeight};tabBottom={tabBottom};toolbarTop={toolbarTop};toolbarHeight={profileToolbar.ActualHeight}"));
            if (!string.IsNullOrWhiteSpace(fixtureRoot)) SaveFixture(fixtureWindow, Path.Combine(fixtureRoot, "section-header-434.png"));
            var sectionScroll = (ScrollViewer)view.FindName("DrawerProfileScroll");
            fixtureWindow.Height = 170;
            view.Height = 170;
            view.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
            view.UpdateLayout();
            checks.Add(("D70NarrowSectionRetainsHtmlChartMinimumInsideShortViewport",
                Math.Abs(sectionScroll.ViewportHeight - 106) < .1 && Math.Abs(drawerProfile.ActualHeight - 120) < .1 && Math.Abs(sectionScroll.ScrollableHeight - 14) < .1,
                $"viewport={sectionScroll.ViewportHeight};chart={drawerProfile.ActualHeight};scrollable={sectionScroll.ScrollableHeight};view={view.ActualWidth}x{view.ActualHeight};window={fixtureWindow.ActualWidth}x{fixtureWindow.ActualHeight}"));
            sectionScroll.ScrollToEnd();
            view.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
            checks.Add(("D70NarrowSectionScrollReachesTheChartBottom", sectionScroll.VerticalOffset == sectionScroll.ScrollableHeight,
                $"offset={sectionScroll.VerticalOffset};extent={sectionScroll.ExtentHeight}"));
            if (!string.IsNullOrWhiteSpace(fixtureRoot)) SaveFixture(fixtureWindow, Path.Combine(fixtureRoot, "section-narrow-scroll-fixture.png"));
            sectionScroll.ScrollToTop();
            view.ClearValue(FrameworkElement.HeightProperty);
            fixtureWindow.Height = 330;
            fixtureWindow.Width = 970;
            view.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
            view.UpdateLayout();
            var toolbarRight = profileToolbar.TransformToAncestor(drawerHeader).Transform(new Point()).X + profileToolbar.ActualWidth;
            checks.Add(("D54WideSectionHeaderKeepsOneRowAndDockActionsReachable",
                !drawerProfile.IsTeachingDrawerNarrow && Math.Abs(drawerHeader.ActualHeight - 34) < .1
                    && profileToolbar.IsVisible && toolbarRight <= dockMenu.TransformToAncestor(drawerHeader).Transform(new Point()).X,
                $"width={drawerProfile.ActualWidth};header={drawerHeader.ActualHeight};toolbarRight={toolbarRight};dockMenuLeft={dockMenu.TransformToAncestor(drawerHeader).Transform(new Point()).X}"));
            if (!string.IsNullOrWhiteSpace(fixtureRoot)) SaveFixture(fixtureWindow, Path.Combine(fixtureRoot, "section-header-970.png"));
            workspace.SelectSectionCommand.Execute("RunRecord");
            view.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
            view.UpdateLayout();
            checks.Add(("D54ReturnToResultsRestoresProtectedHeaderAndHidesSectionActions",
                view.MinHeight == 0 && Math.Abs(drawerHeader.ActualHeight - 34) < .1 && !profileToolbar.IsVisible && teachingSurface.IsVisible,
                $"header={drawerHeader.ActualHeight};sectionToolbar={profileToolbar.IsVisible};results={teachingSurface.IsVisible}"));
            drawerProfile.DataContext = actualDrawerContext;
            fixtureWindow.Content = null;
            var nativeDock = new OpenVisionLab.ThreeD.Docking.Controls.OpenVisionDockWorkspaceView { ResultsContent = view };
            nativeDock.SetBinding(OpenVisionLab.ThreeD.Docking.Controls.OpenVisionDockWorkspaceView.IsSectionSelectedProperty, new Binding(nameof(ResultsWorkspaceViewModel.IsSectionSelected)) { Source = workspace });
            nativeDock.SetOperatorStage(OpenVisionLab.ThreeD.Docking.Controls.OpenVisionOperatorStage.Teach);
            fixtureWindow.Width = 1280;
            fixtureWindow.Height = 720;
            fixtureWindow.Content = nativeDock;
            view.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
            var nativeResultsPane = (AvalonDock.Layout.LayoutAnchorablePane)nativeDock.FindName("resultsPane");
            var defaultMinimum = nativeResultsPane.DockMinHeight;
            var defaultHeightPreference = nativeResultsPane.DockHeight;
            workspace.SelectSectionCommand.Execute("Section");
            view.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
            view.UpdateLayout();
            var nativePaneControl = FindVisual<AvalonDock.Controls.LayoutAnchorablePaneControl>(nativeDock, pane => ReferenceEquals(pane.Model, nativeResultsPane))!;
            var sectionBounds = view.TransformToAncestor(nativePaneControl).TransformBounds(new Rect(view.RenderSize));
            checks.Add(("D70NativeDockAllocatesHtmlCompactSectionHeightInsideItsRenderedParent",
                view.MinHeight == 0 && nativeResultsPane.DockMinHeight == defaultMinimum && nativeResultsPane.DockHeight.IsAbsolute && Math.Abs(view.ActualHeight - 170) < .1
                    && Math.Abs(sectionScroll.ViewportHeight - 136) < .1
                    && sectionBounds.Bottom <= nativePaneControl.ActualHeight + 1 && sectionBounds.Top >= -1,
                $"minimum={nativeResultsPane.DockMinHeight};actual={view.ActualHeight};parent={nativePaneControl.ActualHeight};bounds={sectionBounds};heightPreference={nativeResultsPane.DockHeight}"));
            nativeDock.IsOutputCompareViewerActive = true;
            view.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
            checks.Add(("D54ComparisonKeepsItsExistingZeroResultsMinimum",
                nativeResultsPane.DockMinHeight == 0, $"minimum={nativeResultsPane.DockMinHeight};sectionMinimum={view.MinHeight}"));
            nativeDock.IsOutputCompareViewerActive = false;
            workspace.SelectSectionCommand.Execute("RunRecord");
            view.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
            view.UpdateLayout();
            checks.Add(("D54NativeDockRestoresTheProtectedResultsMinimumAfterSection",
                view.MinHeight == 0 && nativeResultsPane.DockMinHeight == defaultMinimum && view.ActualHeight < 232
                    && nativeResultsPane.DockHeight == defaultHeightPreference,
                $"minimum={nativeResultsPane.DockMinHeight}/{defaultMinimum};actual={view.ActualHeight};heightPreference={nativeResultsPane.DockHeight}"));
            nativeDock.ResultsContent = null;
        }
        finally
        {
            detailsButton.IsChecked = false;
            fixtureWindow.Content = null;
            fixtureWindow.Close();
            application.ShutdownMode = previousShutdown;
        }

        teachingOutput.CurrentOutput = output with
        {
            ResultStatus = "Previous result · Run again",
            LimitSummary = "—",
            Reason = "Result from previous input or conditions"
        };
        view.UpdateLayout();
        checks.Add((
            "ChangedConditionsKeepPreviousResultDistinct",
            teachingOutput.TeachingResults.Single().Status == "Previous result · Run again"
                && teachingOutput.TeachingResults.Single().Limits == "—"
                && teachingOutput.TeachingResults.Single().Reason == "Result from previous input or conditions",
            $"decision={teachingOutput.TeachingResults.Single().Status};limits={teachingOutput.TeachingResults.Single().Limits};reason={teachingOutput.TeachingResults.Single().Reason}"));

        view.DataContext = new
        {
            IsAuthoringWorkspaceSelected = false,
            ResultsWorkspace = workspace,
            Localization = localization,
            Workbench = teachingOutput
        };
        view.UpdateLayout();
        checks.Add((
            "ReviewRestoresRunRecordWithoutExecuting",
            teachingSurface.Visibility == Visibility.Collapsed
                && (string?)runRecordNavigation.Content == localization.ResultsWorkspaceRunRecord
                && runRecordReview.Visibility == Visibility.Visible
                && command.ExecutionCount == 1,
            $"teaching={teachingSurface.Visibility};record={runRecordReview.Visibility};count={command.ExecutionCount}"));


        using var savedWorkspace = new ResultsWorkspaceViewModel();
        var savedMetrics = new[]
        {
            new InspectionRunMetric("Mean", MetricKind.Number, 8, "raw-height", ResultStatus.Fail),
            new InspectionRunMetric("NotFinite", MetricKind.Number, double.NaN, "raw-height", null)
        };
        var saved = new InspectionRunRecord(InspectionRunRecord.CurrentSchemaVersion, "saved-A",
            DateTimeOffset.Parse("2026-10-05T00:00:00Z"), new("ToolRecipe", "1", "coupon.ov3d-recipe.json", new string('A', 64)),
            new("input.coupon", Path.Combine(Path.GetTempPath(), "saved-projection.C3D"), new string('1', 64), 0, "raw-height"),
            "Thickness", ResultStatus.Fail, "Recorded failure", 12, savedMetrics, [], "Stored",
            new("", null, null, null, null, null));
        var snapshot = ResultsRunSnapshot.FromRecord(saved)!;
        savedMetrics[0] = savedMetrics[0] with { Value = 999, Status = ResultStatus.Pass };
        checks.Add(("SavedSnapshotOwnsValuesAndVerdicts",
            snapshot.Metrics[0].Value == 8 && snapshot.Metrics[0].Status == ResultStatus.Fail
                && snapshot.Status == ResultStatus.Fail && snapshot.Failures.Single().Message == "Recorded failure",
            $"value={snapshot.Metrics[0].Value};metric={snapshot.Metrics[0].Status};record={snapshot.Status}"));
        var second = ResultsRunSnapshot.FromRecord(saved with
        {
            RunId = "saved-B", RecordedAtUtc = saved.RecordedAtUtc.AddMinutes(1),
            Recipe = saved.Recipe with { Sha256 = new string('B', 64) },
            Metrics = [new("Mean", MetricKind.Number, 12, "raw-height", null)]
        })!;
        var firstItem = new RunRecordRecentItem("saved-A.json", "saved-A", "", "Fail", 1, true) { Snapshot = snapshot };
        var secondItem = new RunRecordRecentItem("saved-B.json", "saved-B", "", "Limited", 1, true) { Snapshot = second };
        savedWorkspace.RefreshHistory([secondItem, firstItem]);
        checks.Add(("StoredTrendIsChronologicalAndNeverRejudges",
            savedWorkspace.Measurements.Count == 1 && savedWorkspace.TrendPoints.Count == 2
                && savedWorkspace.TrendPoints[0].Metric.Value == 8 && savedWorkspace.TrendPoints[1].Metric.Value == 12
                && savedWorkspace.TrendPoints[0].Metric.Status == ResultStatus.Fail
                && savedWorkspace.TrendPoints[1].Metric.Status is null,
            $"points={savedWorkspace.TrendPoints.Count};values={string.Join(',', savedWorkspace.TrendPoints.Select(point => point.Value))}"));
        checks.Add(("NonFiniteValuesRemainExplicitInReportAndAreAbsentFromTrend",
            snapshot.Metrics.Count == 2 && snapshot.Metrics[1].ValueText == "—"
                && savedWorkspace.TrendPoints.All(point => double.IsFinite(point.Metric.Value)),
            $"report={snapshot.Metrics.Count};trend={savedWorkspace.TrendPoints.Count}"));
        checks.Add(("DifferentStoredSettingsRemainDistinct",
            savedWorkspace.TrendPoints[0].Metric.SettingsIdentity == new string('A', 64)
                && savedWorkspace.TrendPoints[1].Metric.SettingsIdentity == new string('B', 64),
            $"settings={savedWorkspace.TrendPoints[0].Metric.SettingsIdentity}/{savedWorkspace.TrendPoints[1].Metric.SettingsIdentity}"));
        checks.Add(("StatisticsPartitionDifferentExecutedSettings", savedWorkspace.MeasurementStatistics.Count == 2
            && savedWorkspace.MeasurementStatistics.All(row => row.ValueCount == 1 && row.SampleStandardDeviation is null), "each setting identity owns an independent statistics row"));
        var sameSettings = second with { RunId = "saved-A2", Metrics = second.Metrics.Select(metric => metric with { SettingsIdentity = snapshot.Metrics[0].SettingsIdentity }).ToArray() };
        savedWorkspace.RefreshHistory([firstItem, new("same-settings.json", "saved-A2", "", "Pass", 1, true) { Snapshot = sameSettings }, firstItem]);
        var statistics = savedWorkspace.MeasurementStatistics.Single();
        checks.Add(("StatisticsUseOnlyImmutableRunSnapshots", statistics.ValueCount == 2 && statistics.Mean == 10 && statistics.Minimum == 8
            && statistics.Maximum == 12 && statistics.SampleStandardDeviation is { } sigma && Math.Abs(sigma - Math.Sqrt(8)) < 1e-12
            && statistics.FailCount == 1 && !savedWorkspace.InputPreview.IsLoading, "SDK mean10/min8/max12/sample sigma sqrt8; duplicate RunId excluded; input not loaded"));
        savedWorkspace.RefreshHistory([firstItem, new("new-frame.json", "saved-A3", "", "Pass", 1, true) { Snapshot = sameSettings with { RunId = "saved-A3", FrameId = "another.frame" } }]);
        checks.Add(("StatisticsPartitionStoredFrames", savedWorkspace.MeasurementStatistics.Count == 2, "equal setting identity cannot merge different executed frames"));
        var physical = ResultsRunSnapshot.FromRecord(saved with
        {
            RunId = "saved-mm", Metrics = [new("Mean", MetricKind.Number, 3, "mm", ResultStatus.Pass)]
        })!;
        savedWorkspace.RefreshHistory([firstItem, new("saved-mm.json", "saved-mm", "", "Pass", 1, true) { Snapshot = physical }]);
        savedWorkspace.SelectedMeasurement = savedWorkspace.Measurements.Single(item => item.Unit == "raw-height");
        checks.Add(("TrendSelectionNeverMixesStoredUnits",
            savedWorkspace.Measurements.Count == 2 && savedWorkspace.TrendPoints.Count == 1
                && savedWorkspace.TrendPoints[0].Metric.Value == 8 && savedWorkspace.TrendPoints[0].Unit == "raw-height",
            $"choices={savedWorkspace.Measurements.Count};points={savedWorkspace.TrendPoints.Count}"));
        savedWorkspace.SelectSection(ResultsWorkspaceSection.OutputCompare);
        savedWorkspace.SelectHistorySectionCommand.Execute("Failures");
        checks.Add(("GlobalHistoryNavigationPreservesTheTeachingDrawer",
            savedWorkspace.IsFailuresSelected && savedWorkspace.ActiveSection == ResultsWorkspaceSection.OutputCompare
                && savedWorkspace.InputPreview.Frame is null && !savedWorkspace.InputPreview.IsLoading,
            $"history={savedWorkspace.HistorySection};drawer={savedWorkspace.ActiveSection};load={savedWorkspace.InputPreview.IsLoading}"));
        savedWorkspace.RefreshHistory([]);
        checks.Add(("EmptyHistoryClearsStaleChartChoicesWithoutStartingInputLoad",
            savedWorkspace.Measurements.Count == 0 && savedWorkspace.TrendPoints.Count == 0 && savedWorkspace.MeasurementStatistics.Count == 0
                && savedWorkspace.SelectedMeasurement is null && !savedWorkspace.InputPreview.IsLoading,
            $"choices={savedWorkspace.Measurements.Count};points={savedWorkspace.TrendPoints.Count}"));
        checks.Add(("PartialOrUnsupportedSavedRecordsHaveNoInventedSnapshot",
            ResultsRunSnapshot.FromRecord(saved with { Source = null! }) is null
                && ResultsRunSnapshot.FromRecord(saved with { SchemaVersion = "999" }) is null,
            "Missing source and unsupported schema are unavailable."));

        using var disposedLabels = new ResultsWorkspaceViewModel();
        disposedLabels.RefreshTeachingResultLabels([dense], [labelAnchor], "dispose.recipe");
        disposedLabels.Dispose();
        checks.Add(("ResultLabelDisposeReleasesSnapshotsAndDisablesCommand",
            disposedLabels.TeachingResultLabels.Count == 0 && !disposedLabels.ToggleTeachingResultLabelCommand.CanExecute(dense.ResultLabel)
                && command.ExecutionCount == 1, $"labels={disposedLabels.TeachingResultLabels.Count};execution={command.ExecutionCount}"));
        var layoutRoot = Path.Combine(Path.GetTempPath(), "OpenVisionLab-Results-Layout-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(layoutRoot);
        var layoutRecordPath = Path.Combine(layoutRoot, "stored-result.json");
        OpenVisionLab.ThreeD.Reporting.RunRecords.InspectionRunRecordJson.Write(layoutRecordPath, saved with
        {
            RunId = "layout-stored-result",
            Metrics = [new InspectionRunMetric("Mean", MetricKind.Number, 8, "raw-height", ResultStatus.Fail)]
        });
        using (var layoutShell = new ShellMainWindowViewModel(
            recentRunRecordsPath: Path.Combine(layoutRoot, "recent-runs.json"),
            recentRecipesPath: Path.Combine(layoutRoot, "recent-recipes.json"),
            orderedRunRecordRoot: Path.Combine(layoutRoot, "records"),
            integrationSettingsPath: Path.Combine(layoutRoot, "integration.json")))
        {
            using var layoutView = new ResultsWorkspaceView();
            var layoutWindow = new Window { Title = "Results layout context fixture", Width = 1920, Height = 900,
                WindowStyle = WindowStyle.None, ShowActivated = false, ShowInTaskbar = false, Content = layoutView };
            ShellWindowNativeInterop.TryGetLeftmostWorkAreaOrigin(out var layoutLeft, out var layoutTop);
            layoutWindow.Left = layoutLeft;
            layoutWindow.Top = layoutTop;
            var layoutRunCount = 0;
            layoutShell.Workbench.OrderedRunCompleted += (_, _) => layoutRunCount++;
            layoutView.DataContext = layoutShell;
            layoutView.SetBinding(ResultsWorkspaceView.IsDedicatedResultsLayoutProperty, new Binding("IsResultsWorkspaceSelected") { Mode = BindingMode.OneWay });
            layoutView.SetBinding(ResultsWorkspaceView.IsAuthoringLayoutProperty, new Binding("IsAuthoringWorkspaceSelected") { Mode = BindingMode.OneWay });
            var layoutRecordLoaded = layoutShell.LoadRunRecord(layoutRecordPath, out var layoutRecordMessage);
            checks.Add(("ResultsLayoutUsesStoredRecord", layoutRecordLoaded, layoutRecordMessage));
            var historyColumn = (ColumnDefinition)layoutView.FindName("ResultsHistoryColumn");
            var layoutNavigation = (Border)layoutView.FindName("ResultsSectionNavigation");
            var layoutHeader = (FrameworkElement)layoutView.FindName("ResultsWorkspaceHeader");
            try
            {
                layoutWindow.Show();
                layoutView.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
                foreach (var mode in new[] { ShellWorkspaceMode.Review, ShellWorkspaceMode.Teach })
                {
                    layoutShell.SelectWorkspaceCommand.Execute(mode);
                    layoutView.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
                    foreach (var size in new[] { new Size(1920, 900), new Size(1280, 600) })
                    {
                        layoutWindow.Width = size.Width;
                        layoutWindow.Height = size.Height;
                        layoutView.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
                        layoutView.ApplyResponsiveLayout(size.Height);
                        layoutView.Measure(size);
                        layoutView.Arrange(new Rect(new Point(), size));
                        layoutView.UpdateLayout();
                        var dedicated = mode == ShellWorkspaceMode.Review;
                        checks.Add(($"ResultsLayoutContext-{mode}-{size.Width}",
                            layoutView.IsDedicatedResultsLayout == dedicated && layoutView.IsAuthoringLayout == !dedicated
                            && historyColumn.Width.Value == (dedicated ? 280 : 0)
                            && layoutHeader.Visibility == (dedicated ? Visibility.Visible : Visibility.Collapsed)
                            && layoutNavigation.Padding.Right == (dedicated ? 6 : 4),
                            $"results={layoutView.IsDedicatedResultsLayout};authoring={layoutView.IsAuthoringLayout};history={historyColumn.Width};header={layoutHeader.Visibility};padding={layoutNavigation.Padding};size={size}"));
                        if (!string.IsNullOrWhiteSpace(fixtureRoot))
                        {
                            ShellWindowNativeInterop.AppendWindowMonitorEvidence(layoutWindow, Path.Combine(fixtureRoot, "results-context-window.txt"));
                            SaveFixture(layoutView, Path.Combine(fixtureRoot, $"results-context-{mode}-{size.Width}.png"));
                        }
                    }
                }
                checks.Add(("ResultsLayoutContextDoesNotExecuteInspection",
                    !layoutShell.Workbench.HasOrderedRunResult && layoutRunCount == 0,
                    "Workspace presentation changes retain zero completed inspection records."));
                checks.Add(("ResultsLayoutBindingsAreOneWay",
                    BindingOperations.GetBinding(layoutView, ResultsWorkspaceView.IsDedicatedResultsLayoutProperty)?.Mode == BindingMode.OneWay
                    && BindingOperations.GetBinding(layoutView, ResultsWorkspaceView.IsAuthoringLayoutProperty)?.Mode == BindingMode.OneWay,
                    "Only Shell workspace selection supplies the presentation flags."));
                BindingOperations.ClearBinding(layoutView, ResultsWorkspaceView.IsAuthoringLayoutProperty);
                layoutView.IsAuthoringLayout = false;
                checks.Add(("ResultsLayoutPresentationDoesNotWriteWorkspaceSelection", layoutShell.IsAuthoringWorkspaceSelected,
                    "A local presentation value does not change the selected Shell workspace."));
                layoutView.DataContext = null;
            }
            finally
            {
                layoutWindow.Content = null;
                layoutWindow.Close();
            }
        }
        return checks;
    }

    private static void SaveFixture(FrameworkElement view, string path)
    {
        view.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(view.ActualWidth), (int)Math.Ceiling(view.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(view);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path); encoder.Save(stream);
    }

    private static T? FindVisual<T>(DependencyObject? root, Func<T, bool>? predicate = null) where T : DependencyObject
    {
        if (root is T match && (predicate is null || predicate(match))) return match;
        if (root is null) return null;
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var found = FindVisual(VisualTreeHelper.GetChild(root, index), predicate);
            if (found is not null) return found;
        }
        return null;
    }

    private sealed class ObservableOutputSelection : INotifyPropertyChanged
    {
        private SelectedToolOutputItem? currentOutput;
        private TeachingMeasurementResultItem? selectedTeachingResult;

        public event PropertyChangedEventHandler? PropertyChanged;

        public ObservableOutputSelection SelectedToolWorkspace => this;
        public IReadOnlyList<TeachingMeasurementResultItem> TeachingResults { get; private set; } = [];
        public bool HasTeachingResults => TeachingResults.Count > 0;
        public RecordingCommand ShowDisplayedOutputInViewerCommand { get; } = new() { CanExecuteValue = false };
        public IReadOnlyList<TeachingResultSummaryItem> TeachingResultSummary => TeachingResults.GroupBy(row => row.State)
            .Select(group => new TeachingResultSummaryItem(group.Key, group.First().StatusText, group.First().StatusIcon, group.Count())).ToArray();
        public TeachingMeasurementResultItem? SelectedTeachingResult
        {
            get => selectedTeachingResult;
            set
            {
                selectedTeachingResult = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedTeachingResult)));
            }
        }
        public void AppendResult(TeachingMeasurementResultItem row)
        {
            TeachingResults = TeachingResults.Append(row).ToArray();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TeachingResults)));
        }

        public SelectedToolOutputItem? CurrentOutput
        {
            get => currentOutput;
            set
            {
                currentOutput = value;
                TeachingResults = value?.HasExecutionResult == true
                    ? [new(new("step.fixture", ToolWorkbenchToolCatalog.Create().First(item => item.Id == "thickness"), "", value.EntityId), value.ResultStatus, value.Value, value.Unit, value.LimitSummary, value.Reason, true, [])]
                    : [];
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TeachingResults)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasTeachingResults)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentOutput)));
            }
        }
    }

    private sealed class RecordingCommand : ICommand
    {
        public bool CanExecuteValue { get; init; } = true;

        public int ExecutionCount { get; private set; }

        public object? LastParameter { get; private set; }

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => CanExecuteValue;

        public void Execute(object? parameter)
        {
            ExecutionCount++;
            LastParameter = parameter;
        }
    }
}
