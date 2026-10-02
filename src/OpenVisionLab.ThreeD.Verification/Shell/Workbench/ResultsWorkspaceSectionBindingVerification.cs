using System.IO;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using OpenVisionLab.ThreeD.Shell;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Shell.Views.Workbench;

namespace OpenVisionLab.ThreeD.Verification.Shell.Workbench;

internal static class ResultsWorkspaceSectionBindingVerification
{
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
            checks.Add(("StaSetup", false, failure.GetBaseException().Message));
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
        var detail = (TextBlock)view.FindName("ResultsWorkspaceDetail");
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
            "CompactResultsShowCurrentRunWithoutDuplicateHeader",
            header.Visibility == Visibility.Visible
                && detail.Visibility == Visibility.Collapsed
                && compactSnapshot.Visibility == Visibility.Visible
                && BindingOperations.GetBinding(compactSnapshot, TextBlock.TextProperty)?.Path.Path == "ResultsOperatorCompactDecision"
                && summary.Visibility == Visibility.Collapsed
                && immutableNotice.Visibility == Visibility.Collapsed
                && runRecordHeader.Visibility == Visibility.Visible
                && Grid.GetRow(content) == 0
                && Grid.GetRow(navigation) == 1,
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
                && header.Visibility == Visibility.Visible
                && detail.Visibility == Visibility.Visible
                && compactSnapshot.Visibility == Visibility.Collapsed
                && summary.Visibility == Visibility.Visible
                && details.Visibility == Visibility.Visible
                && immutableNotice.Visibility == Visibility.Visible
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
                && (string?)runRecordNavigation.Content == localization.ResultsWorkspaceRunResultTitle
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
            && ((Binding)((DataGridTextColumn)view.FindName("TeachingResultValueColumn")).Binding).Path.Path == "Value"
            && command.ExecutionCount == 1, "two real row projections; selection updates the binding without execution"));

        teachingOutput.CurrentOutput = output with { ResultStatus = "Not executed", HasExecutionResult = false };
        view.UpdateLayout();
        checks.Add((
            "StatusWithoutExecutionDoesNotCreateResultRow",
            teachingEmpty.Visibility == Visibility.Visible,
            $"empty={teachingEmpty.Visibility}"));

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

        return checks;
    }

    private sealed class ObservableOutputSelection : INotifyPropertyChanged
    {
        private SelectedToolOutputItem? currentOutput;

        public event PropertyChangedEventHandler? PropertyChanged;

        public ObservableOutputSelection SelectedToolWorkspace => this;
        public IReadOnlyList<TeachingMeasurementResultItem> TeachingResults { get; private set; } = [];
        public bool HasTeachingResults => TeachingResults.Count > 0;
        public TeachingMeasurementResultItem? SelectedTeachingResult { get; set; }
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
