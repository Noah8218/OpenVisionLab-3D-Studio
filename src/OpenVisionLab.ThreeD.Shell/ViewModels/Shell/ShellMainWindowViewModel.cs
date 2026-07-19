using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Input;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell;

public enum ShellWorkspaceMode
{
    Workbench,
    Teach,
    Inspect,
    Review,
    Calibrate,
    Expert
}

public enum ShellInspectionTask
{
    Thickness,
    Warpage
}

public sealed class ShellMainWindowViewModel : INotifyPropertyChanged
{
    private bool c3DSampleVisible;
    private readonly string? comparisonContractPath;
    private readonly string? comparisonReportPath;
    private readonly string? shellScreenshotPath;
    private readonly string? runRecordPath;
    private readonly string? htmlReportPath;
    private readonly string? csvReportPath;
    private string? currentContractPath;
    private string? currentReportPath;
    private string? currentShellScreenshotPath;
    private string? currentRunRecordPath;
    private string? currentHtmlReportPath;
    private string? currentCsvReportPath;
    private string statusText = "Viewer hosted";
    private string recipeComparisonSummary = "No recipe comparison evidence loaded.";
    private string recipeComparisonHistory = "(pending)";
    private string recipeComparisonDetails = "(pending)";
    private string runSnapshotSummary = "No run snapshot evidence loaded.";
    private string runSnapshotEvidence = "(pending)";
    private string inspectionStepSummary = "No inspection steps loaded.";
    private int selectedEvidenceTabIndex;
    private ShellWorkspaceMode selectedWorkspaceMode = ShellWorkspaceMode.Workbench;
    private ShellInspectionTask selectedInspectionTask = ShellInspectionTask.Thickness;
    private static readonly JsonSerializerOptions RunRecordJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? ApplyRoiAlignmentRequested;
    public event EventHandler? FitPlaneRequested;
    public event EventHandler? RefreshRecipeComparisonRequested;
    public event EventHandler? SaveRecipeRequested;
    public event EventHandler? PublishInspectionResultRequested;
    public event EventHandler? InspectionTaskChanged;
    public event EventHandler<EvidenceArtifactOpenRequestEventArgs>? OpenEvidenceArtifactRequested;

    public ShellMainWindowViewModel(
        string? comparisonContractPath = null,
        string? comparisonReportPath = null,
        string? shellScreenshotPath = null,
        string? runRecordPath = null,
        string? htmlReportPath = null,
        string? csvReportPath = null)
    {
        this.comparisonContractPath = comparisonContractPath;
        this.comparisonReportPath = comparisonReportPath;
        this.shellScreenshotPath = shellScreenshotPath;
        this.runRecordPath = runRecordPath;
        this.htmlReportPath = htmlReportPath;
        this.csvReportPath = csvReportPath;
        Workbench = new ToolWorkbenchViewModel();
        Calibration = new CalibrationCenterViewModel();
        SelectWorkspaceCommand = new RelayCommand(
            parameter => SelectWorkspace(parameter),
            parameter => parameter is ShellWorkspaceMode mode
                && Enum.IsDefined(typeof(ShellWorkspaceMode), mode));
        ApplyRoiAlignmentCommand = new RelayCommand(_ => ApplyRoiAlignmentRequested?.Invoke(this, EventArgs.Empty), _ => c3DSampleVisible);
        FitPlaneCommand = new RelayCommand(_ => FitPlaneRequested?.Invoke(this, EventArgs.Empty), _ => c3DSampleVisible);
        RefreshRecipeComparisonCommand = new RelayCommand(_ => RefreshRecipeComparisonRequested?.Invoke(this, EventArgs.Empty));
        SaveRecipeCommand = new RelayCommand(_ => SaveRecipeRequested?.Invoke(this, EventArgs.Empty));
        PublishInspectionResultCommand = new RelayCommand(_ => PublishInspectionResultRequested?.Invoke(this, EventArgs.Empty));
        OpenUiContractCommand = new RelayCommand(_ => RequestEvidenceArtifact("UI contract", currentContractPath), _ => !string.IsNullOrWhiteSpace(currentContractPath));
        OpenRunnerReportCommand = new RelayCommand(_ => RequestEvidenceArtifact("Runner report", currentReportPath), _ => !string.IsNullOrWhiteSpace(currentReportPath));
        OpenShellScreenshotCommand = new RelayCommand(_ => RequestEvidenceArtifact("Shell screenshot", currentShellScreenshotPath), _ => !string.IsNullOrWhiteSpace(currentShellScreenshotPath));
        OpenRunRecordCommand = new RelayCommand(_ => RequestEvidenceArtifact("Run JSON", currentRunRecordPath), _ => !string.IsNullOrWhiteSpace(currentRunRecordPath));
        OpenHtmlReportCommand = new RelayCommand(_ => RequestEvidenceArtifact("HTML report", currentHtmlReportPath), _ => !string.IsNullOrWhiteSpace(currentHtmlReportPath));
        OpenCsvReportCommand = new RelayCommand(_ => RequestEvidenceArtifact("CSV report", currentCsvReportPath), _ => !string.IsNullOrWhiteSpace(currentCsvReportPath));
        RefreshRecipeComparison();
    }

    public ICommand ApplyRoiAlignmentCommand { get; }
    public ICommand SelectWorkspaceCommand { get; }
    public ICommand FitPlaneCommand { get; }
    public ICommand RefreshRecipeComparisonCommand { get; }
    public ICommand SaveRecipeCommand { get; }
    public ICommand PublishInspectionResultCommand { get; }
    public ICommand OpenUiContractCommand { get; }
    public ICommand OpenRunnerReportCommand { get; }
    public ICommand OpenShellScreenshotCommand { get; }
    public ICommand OpenRunRecordCommand { get; }
    public ICommand OpenHtmlReportCommand { get; }
    public ICommand OpenCsvReportCommand { get; }
    public ToolWorkbenchViewModel Workbench { get; }
    public CalibrationCenterViewModel Calibration { get; }

    public ShellWorkspaceMode SelectedWorkspaceMode
    {
        get => selectedWorkspaceMode;
        private set
        {
            if (!SetField(ref selectedWorkspaceMode, value))
            {
                return;
            }

            RaisePropertyChanged(nameof(IsInspectWorkspaceSelected));
            RaisePropertyChanged(nameof(IsTeachWorkspaceSelected));
            RaisePropertyChanged(nameof(IsReviewWorkspaceSelected));
            RaisePropertyChanged(nameof(IsCalibrationWorkspaceSelected));
            RaisePropertyChanged(nameof(IsWorkbenchWorkspaceSelected));
            RaisePropertyChanged(nameof(IsExpertWorkspaceSelected));
            RaisePropertyChanged(nameof(IsTaskWorkspaceSelected));
            RaisePropertyChanged(nameof(WorkspaceSummary));
        }
    }

    public bool IsTeachWorkspaceSelected
    {
        get => SelectedWorkspaceMode == ShellWorkspaceMode.Teach;
        set
        {
            if (value)
            {
                SelectedWorkspaceMode = ShellWorkspaceMode.Teach;
            }
        }
    }

    public bool IsWorkbenchWorkspaceSelected
    {
        get => SelectedWorkspaceMode == ShellWorkspaceMode.Workbench;
        set
        {
            if (value)
            {
                SelectedWorkspaceMode = ShellWorkspaceMode.Workbench;
            }
        }
    }

    public bool IsInspectWorkspaceSelected
    {
        get => SelectedWorkspaceMode == ShellWorkspaceMode.Inspect;
        set
        {
            if (value)
            {
                SelectedWorkspaceMode = ShellWorkspaceMode.Inspect;
            }
        }
    }

    public bool IsReviewWorkspaceSelected
    {
        get => SelectedWorkspaceMode == ShellWorkspaceMode.Review;
        set
        {
            if (value)
            {
                SelectedWorkspaceMode = ShellWorkspaceMode.Review;
            }
        }
    }

    public bool IsCalibrationWorkspaceSelected
    {
        get => SelectedWorkspaceMode == ShellWorkspaceMode.Calibrate;
        set
        {
            if (value)
            {
                SelectedWorkspaceMode = ShellWorkspaceMode.Calibrate;
            }
        }
    }

    public bool IsExpertWorkspaceSelected
    {
        get => SelectedWorkspaceMode == ShellWorkspaceMode.Expert;
        set
        {
            if (value)
            {
                SelectedWorkspaceMode = ShellWorkspaceMode.Expert;
            }
        }
    }

    public bool IsTaskWorkspaceSelected => IsTeachWorkspaceSelected
        || IsInspectWorkspaceSelected
        || IsReviewWorkspaceSelected;

    public ShellInspectionTask SelectedInspectionTask
    {
        get => selectedInspectionTask;
        private set
        {
            if (!SetField(ref selectedInspectionTask, value))
            {
                return;
            }

            RaisePropertyChanged(nameof(IsThicknessTaskSelected));
            RaisePropertyChanged(nameof(IsWarpageTaskSelected));
            RaisePropertyChanged(nameof(CurrentInspectionTaskLabel));
            RaisePropertyChanged(nameof(WorkspaceSummary));
            InspectionTaskChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool IsThicknessTaskSelected
    {
        get => SelectedInspectionTask == ShellInspectionTask.Thickness;
        set
        {
            if (value)
            {
                SelectInspectionTask(ShellInspectionTask.Thickness);
            }
        }
    }

    public bool IsWarpageTaskSelected
    {
        get => SelectedInspectionTask == ShellInspectionTask.Warpage;
        set
        {
            if (value)
            {
                SelectInspectionTask(ShellInspectionTask.Warpage);
            }
        }
    }

    public string CurrentInspectionTaskLabel => SelectedInspectionTask == ShellInspectionTask.Warpage
        ? "Warpage"
        : "Thickness";

    public string WorkspaceSummary => SelectedWorkspaceMode switch
    {
        ShellWorkspaceMode.Workbench => "Tool Workbench | Compose typed 3D inspection steps",
        ShellWorkspaceMode.Teach => SelectedInspectionTask == ShellInspectionTask.Warpage
            ? "Warpage Teach | Define best-fit ROI and P2V limit"
            : "Thickness Teach | Define ROI and tolerance",
        ShellWorkspaceMode.Inspect => SelectedInspectionTask == ShellInspectionTask.Warpage
            ? "Warpage Inspect | Preview and publish"
            : "Thickness Inspect | Preview and publish",
        ShellWorkspaceMode.Review => $"{CurrentInspectionTaskLabel} Review | Published result evidence",
        ShellWorkspaceMode.Calibrate => "Calibration workspace | Offline datasets",
        ShellWorkspaceMode.Expert => "Expert workspace | Full inspection layout",
        _ => "Inspection workspace"
    };

    public string StatusText
    {
        get => statusText;
        private set => SetField(ref statusText, value);
    }

    public string RecipeComparisonSummary
    {
        get => recipeComparisonSummary;
        private set => SetField(ref recipeComparisonSummary, value);
    }

    public string RecipeComparisonHistory
    {
        get => recipeComparisonHistory;
        private set => SetField(ref recipeComparisonHistory, value);
    }

    public string RecipeComparisonDetails
    {
        get => recipeComparisonDetails;
        private set => SetField(ref recipeComparisonDetails, value);
    }

    public string RunSnapshotSummary
    {
        get => runSnapshotSummary;
        private set => SetField(ref runSnapshotSummary, value);
    }

    public string RunSnapshotEvidence
    {
        get => runSnapshotEvidence;
        private set => SetField(ref runSnapshotEvidence, value);
    }

    public string InspectionStepSummary
    {
        get => inspectionStepSummary;
        private set => SetField(ref inspectionStepSummary, value);
    }

    public int SelectedEvidenceTabIndex
    {
        get => selectedEvidenceTabIndex;
        set => SetField(ref selectedEvidenceTabIndex, Math.Clamp(value, 0, 4));
    }

    public ObservableCollection<RecipeRunHistoryItem> RecipeRunHistory { get; } = [];

    public ObservableCollection<InspectionStepItem> InspectionSteps { get; } = [];

    public void ShowReviewWorkspace() => SelectedWorkspaceMode = ShellWorkspaceMode.Review;

    public void SelectInspectionTask(ShellInspectionTask task)
    {
        if (Enum.IsDefined(typeof(ShellInspectionTask), task))
        {
            SelectedInspectionTask = task;
        }
    }

    private void SelectWorkspace(object? parameter)
    {
        if (parameter is ShellWorkspaceMode mode
            && Enum.IsDefined(typeof(ShellWorkspaceMode), mode))
        {
            SelectedWorkspaceMode = mode;
        }
    }

    public void SetViewerSmokeFailed(string viewerStatus)
    {
        var root = ResolveWorkspaceRoot();
        currentContractPath = null;
        currentReportPath = null;
        currentShellScreenshotPath = ResolveOptionalPath(root, shellScreenshotPath);
        currentRunRecordPath = ResolveOptionalPath(root, runRecordPath);
        currentHtmlReportPath = ResolveOptionalPath(root, htmlReportPath);
        currentCsvReportPath = ResolveOptionalPath(root, csvReportPath);
        RefreshCommandCanExecute();

        StatusText = "Viewer hosted | viewer smoke failed";
        RecipeComparisonSummary = string.IsNullOrWhiteSpace(viewerStatus)
            ? "Viewer smoke failed before recipe comparison."
            : $"Viewer smoke failed before recipe comparison.\n{viewerStatus}";
        RecipeComparisonHistory = "No recipe comparison was run for this failed viewer smoke.";
        RecipeComparisonDetails = "See Tool / Inspector and Viewer contract output for the loader failure details.";
        RunSnapshotSummary = "Viewer smoke failed | Status: ViewerFailed | Key metric: No recipe metric | Evidence: Blocked";
        RunSnapshotEvidence = $"Shell: {FormatShellScreenshotTarget(root)} | Runner: not created | UI: viewer smoke output";
        InspectionStepSummary = "Viewer smoke: Failed";
        InspectionSteps.Clear();
        InspectionSteps.Add(new InspectionStepItem("1", "Viewer smoke", "Failed", string.IsNullOrWhiteSpace(viewerStatus) ? "Viewer smoke failed before recipe comparison." : viewerStatus));
        RecipeRunHistory.Clear();
        RecipeRunHistory.Add(new RecipeRunHistoryItem(
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            "ViewerFailed",
            "No recipe metric",
            "Blocked",
            "(viewer smoke failure)"));
    }

    public void RefreshRecipeComparison()
    {
        var root = ResolveWorkspaceRoot();
        currentRunRecordPath = ResolveOptionalPath(root, runRecordPath);
        var runRecord = ReadRunRecord(currentRunRecordPath);
        var contractPath = ResolvePath(root, comparisonContractPath ?? runRecord?.Artifacts.ViewerContract, Path.Combine(root, "artifacts", "shell_recipe_ui_after.txt"));
        var reportPath = ResolvePath(root, comparisonReportPath ?? runRecord?.Artifacts.RunnerTextReport, Path.Combine(root, "artifacts", "runner_shell_recipe_ui_compare_after.txt"));
        currentContractPath = contractPath;
        currentReportPath = reportPath;
        currentShellScreenshotPath = ResolveOptionalPath(root, shellScreenshotPath ?? runRecord?.Artifacts.ViewerScreenshot);
        currentHtmlReportPath = ResolveOptionalPath(root, htmlReportPath ?? runRecord?.Artifacts.HtmlReport);
        currentCsvReportPath = ResolveOptionalPath(root, csvReportPath ?? runRecord?.Artifacts.CsvReport);
        RefreshCommandCanExecute();

        var contractLines = ReadLinesOrEmpty(contractPath);
        var reportLines = ReadLinesOrEmpty(reportPath);
        var recipePath = ExtractRecipePath(root, reportLines);
        var uiEvidence = ExtractUiEvidence(contractLines);
        var runnerEvidence = ExtractRunnerEvidence(reportLines);
        var comparisonState = uiEvidence.Matches(runnerEvidence)
            ? "Runner/UI contract matched"
            : "Comparison evidence missing or different";

        RecipeComparisonSummary =
            $"{comparisonState}\nUI: {uiEvidence.ToolName} / {uiEvidence.Status}\nRunner: {runnerEvidence.ToolName} / {runnerEvidence.Status}\nUI metric: {uiEvidence.KeyMetricSummary}\nRunner metric: {runnerEvidence.KeyMetricSummary}";
        RecipeComparisonHistory =
            $"Recipe: {FormatEvidencePath(root, recipePath)}\nUI contract: {FormatEvidencePath(root, contractPath)}\nRunner report: {FormatEvidencePath(root, reportPath)}";
        RecipeComparisonDetails =
            $"{PreviewLines(root, "Runner report", reportPath, reportLines)}\n\n{PreviewLines(root, "UI contract", contractPath, contractLines)}";
        RefreshRunSnapshot(root, recipePath, contractPath, reportPath, uiEvidence, runnerEvidence, comparisonState);
        RefreshInspectionSteps(root, recipePath, contractPath, reportPath, contractLines, reportLines, uiEvidence, runnerEvidence, comparisonState);
        RefreshRunHistory(root, contractPath, reportPath, uiEvidence, runnerEvidence, comparisonState);
        StatusText = comparisonState == "Runner/UI contract matched"
            ? "Viewer hosted | recipe comparison matched"
            : "Viewer hosted | recipe comparison pending";
    }

    private void RefreshRunSnapshot(
        string root,
        string recipePath,
        string contractPath,
        string reportPath,
        ToolComparisonEvidence uiEvidence,
        ToolComparisonEvidence runnerEvidence,
        string comparisonState)
    {
        var status = SelectEvidenceStatus(uiEvidence, runnerEvidence);
        var keyMetricSummary = SelectEvidenceMetric(uiEvidence, runnerEvidence);
        var evidenceState = comparisonState == "Runner/UI contract matched" ? "Matched" : "Pending";

        RunSnapshotSummary =
            $"{comparisonState} | Status: {status} | Key metric: {keyMetricSummary} | Evidence: {evidenceState} | Run: {FormatRunTime(reportPath, contractPath)}";
        RunSnapshotEvidence =
            $"Recipe: {FormatShortEvidencePath(root, recipePath)} | UI: {FormatShortEvidencePath(root, contractPath)} | Runner: {FormatShortEvidencePath(root, reportPath)} | Shell: {FormatShellScreenshotTarget(root)} | JSON: {FormatOptionalArtifact(root, currentRunRecordPath)} | HTML: {FormatOptionalArtifact(root, currentHtmlReportPath)} | CSV: {FormatOptionalArtifact(root, currentCsvReportPath)}";
    }

    private void RefreshInspectionSteps(
        string root,
        string recipePath,
        string contractPath,
        string reportPath,
        string[] contractLines,
        string[] reportLines,
        ToolComparisonEvidence uiEvidence,
        ToolComparisonEvidence runnerEvidence,
        string comparisonState)
    {
        InspectionSteps.Clear();

        var evidenceState = comparisonState == "Runner/UI contract matched" ? "Matched" : "Pending";
        var order = 1;
        var recipeSteps = contractLines
            .Concat(reportLines)
            .Where(line => line.StartsWith(InspectionContractText.InspectionStepMarker + "|", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        foreach (var stepLine in recipeSteps)
        {
            var parts = stepLine.Split('|');
            var enabled = ExtractTaggedValue(parts, "enabled=");
            var status = enabled?.Equals("False", StringComparison.OrdinalIgnoreCase) == true ? "Disabled" : uiEvidence.Status;
            var tool = ExtractTaggedValue(parts, "tool=") ?? "Inspection step";
            var id = ExtractTaggedValue(parts, "id=") ?? "(missing ID)";
            var source = ExtractTaggedValue(parts, "source=") ?? "(missing source)";
            var reference = ExtractTaggedValue(parts, "reference=") ?? "(missing reference)";
            InspectionSteps.Add(new InspectionStepItem(
                (order++).ToString(CultureInfo.InvariantCulture),
                tool,
                status,
                $"{id} | source {source} | reference {reference}"));
        }

        InspectionSteps.Add(new InspectionStepItem((order++).ToString(CultureInfo.InvariantCulture), "Recipe", File.Exists(recipePath) ? "Loaded" : "Missing", FormatShortEvidencePath(root, recipePath)));
        InspectionSteps.Add(new InspectionStepItem((order++).ToString(CultureInfo.InvariantCulture), "Source", ExtractSourceLoadStatus(reportLines), ExtractSourceSummary(root, reportLines, contractLines)));

        InspectionSteps.Add(new InspectionStepItem((order++).ToString(CultureInfo.InvariantCulture), "Viewer preview", uiEvidence.Status, $"{uiEvidence.ToolName} | {uiEvidence.KeyMetricSummary}"));
        InspectionSteps.Add(new InspectionStepItem((order++).ToString(CultureInfo.InvariantCulture), "Runner replay", runnerEvidence.Status, $"{runnerEvidence.ToolName} | {runnerEvidence.KeyMetricSummary}"));
        InspectionSteps.Add(new InspectionStepItem(order.ToString(CultureInfo.InvariantCulture), "Evidence compare", evidenceState, $"{comparisonState} | UI {FormatShortEvidencePath(root, contractPath)} | Runner {FormatShortEvidencePath(root, reportPath)}"));

        InspectionStepSummary = recipeSteps.Length == 0
            ? $"Recipe: {InspectionSteps[0].Status} | Source: {InspectionSteps[1].Status} | Viewer: {uiEvidence.Status} | Runner: {runnerEvidence.Status} | Compare: {evidenceState}"
            : $"Recipe steps: {recipeSteps.Length} | Viewer: {uiEvidence.Status} | Runner: {runnerEvidence.Status} | Compare: {evidenceState}";
    }

    private void RefreshRunHistory(
        string root,
        string contractPath,
        string reportPath,
        ToolComparisonEvidence uiEvidence,
        ToolComparisonEvidence runnerEvidence,
        string comparisonState)
    {
        RecipeRunHistory.Clear();

        var status = SelectEvidenceStatus(uiEvidence, runnerEvidence);
        var keyMetricSummary = SelectEvidenceMetric(uiEvidence, runnerEvidence);
        var evidenceState = comparisonState == "Runner/UI contract matched" ? "Matched" : "Pending";

        RecipeRunHistory.Add(new RecipeRunHistoryItem(
            FormatRunTime(reportPath, contractPath),
            status,
            keyMetricSummary,
            evidenceState,
            FormatShortEvidencePath(root, reportPath)));
    }

    private static string SelectEvidenceStatus(ToolComparisonEvidence uiEvidence, ToolComparisonEvidence runnerEvidence) =>
        runnerEvidence.Status != "(missing)" ? runnerEvidence.Status : uiEvidence.Status;

    private static string SelectEvidenceMetric(ToolComparisonEvidence uiEvidence, ToolComparisonEvidence runnerEvidence) =>
        runnerEvidence.KeyMetricSummary != "(missing)" ? runnerEvidence.KeyMetricSummary : uiEvidence.KeyMetricSummary;

    private static string ResolveWorkspaceRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "OpenVisionLab.ThreeDStudio.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return Directory.GetCurrentDirectory();
    }

    private static string ResolvePath(string root, string? requestedPath, string fallbackPath)
    {
        if (string.IsNullOrWhiteSpace(requestedPath))
        {
            return fallbackPath;
        }

        return Path.IsPathRooted(requestedPath)
            ? requestedPath
            : Path.Combine(root, requestedPath);
    }

    private static string? ResolveOptionalPath(string root, string? requestedPath) =>
        string.IsNullOrWhiteSpace(requestedPath)
            ? null
            : Path.IsPathRooted(requestedPath)
                ? requestedPath
                : Path.Combine(root, requestedPath);

    private static string[] ReadLinesOrEmpty(string path) =>
        File.Exists(path) ? File.ReadAllLines(path) : [];

    private static InspectionRunRecord? ReadRunRecord(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
        try
        {
            return JsonSerializer.Deserialize<InspectionRunRecord>(File.ReadAllText(path), RunRecordJsonOptions);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    public void UpdateC3DSampleVisible(bool isVisible)
    {
        if (c3DSampleVisible != isVisible)
        {
            c3DSampleVisible = isVisible;
            RefreshCommandCanExecute();
        }
    }

    private void RefreshCommandCanExecute()
    {
        ((RelayCommand)ApplyRoiAlignmentCommand).RaiseCanExecuteChanged();
        ((RelayCommand)FitPlaneCommand).RaiseCanExecuteChanged();
        ((RelayCommand)OpenUiContractCommand).RaiseCanExecuteChanged();
        ((RelayCommand)OpenRunnerReportCommand).RaiseCanExecuteChanged();
        ((RelayCommand)OpenShellScreenshotCommand).RaiseCanExecuteChanged();
        ((RelayCommand)OpenRunRecordCommand).RaiseCanExecuteChanged();
        ((RelayCommand)OpenHtmlReportCommand).RaiseCanExecuteChanged();
        ((RelayCommand)OpenCsvReportCommand).RaiseCanExecuteChanged();
    }

    private void RequestEvidenceArtifact(string label, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            StatusText = $"Viewer hosted | {label} artifact path is not available";
            return;
        }

        OpenEvidenceArtifactRequested?.Invoke(this, new EvidenceArtifactOpenRequestEventArgs(label, path));
    }

    private static ToolComparisonEvidence ExtractUiEvidence(string[] lines)
    {
        var nominalActual = ExtractNominalActualUiEvidence(lines);
        if (nominalActual is not null)
        {
            return nominalActual;
        }

        var resultLine = FindLineAfterMarker(lines, InspectionContractText.PreviewToolResultMarker);
        var parts = resultLine?.Split('|');
        var metrics = ExtractMetrics(lines, InspectionContractText.PreviewMetricsMarker);
        return new ToolComparisonEvidence(
            parts is { Length: > 0 } ? parts[0] : "(missing)",
            parts is { Length: > 1 } ? parts[1] : "(missing)",
            FormatKeyMetricSummary(metrics));
    }

    private static ToolComparisonEvidence ExtractRunnerEvidence(string[] lines)
    {
        var resultLine = lines.FirstOrDefault(line => line.StartsWith(InspectionContractText.ToolResultPrefix + "|", StringComparison.Ordinal));
        var parts = resultLine?.Split('|');
        var metrics = ExtractMetrics(lines, InspectionContractText.MetricsMarker);
        return new ToolComparisonEvidence(
            parts is { Length: > 1 } ? parts[1] : "(missing)",
            parts is { Length: > 2 } ? parts[2] : "(missing)",
            FormatKeyMetricSummary(metrics));
    }

    private static string ExtractRecipePath(string root, string[] reportLines)
    {
        var recipeLine = reportLines.FirstOrDefault(line => line.StartsWith("Recipe|", StringComparison.Ordinal));
        var path = recipeLine is null ? null : ExtractTaggedValue(recipeLine.Split('|'), "path=");
        return string.IsNullOrWhiteSpace(path)
            ? Path.Combine(root, "recipes", "c3d-height-deviation.recipe.json")
            : ResolvePath(root, path, path);
    }

    private static string ExtractSourceLoadStatus(string[] reportLines) =>
        reportLines.Any(line =>
            line.StartsWith("Source|", StringComparison.Ordinal)
            || line.StartsWith("NominalActualActualSource|", StringComparison.Ordinal))
            ? "Loaded"
            : "Pending";

    private static string ExtractSourceSummary(string root, string[] reportLines, string[] contractLines)
    {
        var actual = reportLines.FirstOrDefault(line =>
            line.StartsWith("NominalActualActualSource|", StringComparison.Ordinal));
        var nominal = reportLines.FirstOrDefault(line =>
            line.StartsWith("NominalActualNominalSource|", StringComparison.Ordinal));
        var query = reportLines.FirstOrDefault(line =>
            line.StartsWith("NominalActualQuerySource|", StringComparison.Ordinal));
        if (actual is not null && nominal is not null && query is not null)
        {
            return string.Join(
                " | ",
                FormatNominalActualSource(root, "actual", actual),
                FormatNominalActualSource(root, "nominal", nominal),
                FormatNominalActualSource(root, "query", query));
        }

        var sourceLine = reportLines.FirstOrDefault(line => line.StartsWith("Source|", StringComparison.Ordinal));
        if (sourceLine is not null)
        {
            var parts = sourceLine.Split('|');
            var name = ExtractTaggedValue(parts, "name=") ?? (parts.Length > 1 ? parts[1] : "source");
            var unit = ExtractTaggedValue(parts, "unit=") ?? "(unit unknown)";
            var path = ExtractTaggedValue(parts, "path=");
            var shortPath = string.IsNullOrWhiteSpace(path) ? "(path unknown)" : ShortenWorkspacePaths(root, path);
            return $"{name} | unit {unit} | {shortPath}";
        }

        var sourceEntitiesIndex = Array.FindIndex(contractLines, line => line.Equals("SourceEntities", StringComparison.Ordinal));
        if (sourceEntitiesIndex >= 0 && sourceEntitiesIndex + 1 < contractLines.Length)
        {
            return ShortenWorkspacePaths(root, contractLines[sourceEntitiesIndex + 1]);
        }

        return "No source evidence found.";
    }

    private static ToolComparisonEvidence? ExtractNominalActualUiEvidence(string[] lines)
    {
        var resultLine = lines.FirstOrDefault(line =>
            line.StartsWith("NominalActualResult|", StringComparison.Ordinal));
        var statisticsLine = lines.FirstOrDefault(line =>
            line.StartsWith("NominalActualSignedStatistics|", StringComparison.Ordinal));
        if (resultLine is null || statisticsLine is null)
        {
            return null;
        }

        var resultParts = resultLine.Split('|');
        if (!string.Equals(
                ExtractTaggedValue(resultParts, "available="),
                "True",
                StringComparison.OrdinalIgnoreCase)
            || !long.TryParse(
                ExtractTaggedValue(resultParts, "below="),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var below)
            || !long.TryParse(
                ExtractTaggedValue(resultParts, "above="),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var above))
        {
            return null;
        }

        var statisticsParts = statisticsLine.Split('|');
        if (!double.TryParse(
                ExtractTaggedValue(statisticsParts, "mean="),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var signedMean))
        {
            return null;
        }

        var unit = ExtractTaggedValue(statisticsParts, "unit=") ?? "(unitless)";
        return new ToolComparisonEvidence(
            NominalActualComparisonContract.ToolName,
            ExtractTaggedValue(resultParts, "status=") ?? "(missing)",
            string.Create(
                CultureInfo.InvariantCulture,
                $"Signed mean deviation {signedMean:F3} {unit} | Out-of-tolerance point count {below + above:F3} count"));
    }

    private static string FormatNominalActualSource(
        string root,
        string role,
        string line)
    {
        var parts = line.Split('|');
        var id = ExtractTaggedValue(parts, "id=") ?? "(missing ID)";
        var path = ExtractTaggedValue(parts, "path=");
        return $"{role} {id} ({(string.IsNullOrWhiteSpace(path) ? "path unknown" : ShortenWorkspacePaths(root, path))})";
    }

    private static string? FindLineAfterMarker(string[] lines, string marker)
    {
        var index = Array.FindIndex(lines, line => line.Equals(marker, StringComparison.Ordinal));
        if (index < 0)
        {
            return null;
        }

        for (var i = index + 1; i < lines.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(lines[i]))
            {
                return lines[i];
            }
        }

        return null;
    }

    private static List<MetricEvidence> ExtractMetrics(string[] lines, string marker)
    {
        var index = Array.FindIndex(lines, line => line.Equals(marker, StringComparison.Ordinal));
        if (index < 0)
        {
            return [];
        }

        var metrics = new List<MetricEvidence>();
        for (var i = index + 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var parts = line.Split('|');
            var value = ExtractTaggedValue(parts, "value=");
            if (parts.Length < 3 || value is null)
            {
                break;
            }

            metrics.Add(new MetricEvidence(
                parts[0],
                value,
                ExtractTaggedValue(parts, "unit=") ?? "(unitless)",
                ExtractTaggedValue(parts, "status=") ?? "(none)"));
        }

        return metrics;
    }

    private static string? ExtractTaggedValue(string[] parts, string prefix) =>
        parts.FirstOrDefault(part => part.StartsWith(prefix, StringComparison.Ordinal))?[prefix.Length..];

    private static string FormatKeyMetricSummary(IReadOnlyList<MetricEvidence> metrics)
    {
        var signedMean = metrics.FirstOrDefault(metric =>
            metric.Name.Equals("Signed mean deviation", StringComparison.Ordinal));
        var outOfTolerance = metrics.FirstOrDefault(metric =>
            metric.Name.Equals("Out-of-tolerance point count", StringComparison.Ordinal));
        if (signedMean is not null && outOfTolerance is not null)
        {
            return $"Signed mean deviation {FormatMetricValue(signedMean)} | Out-of-tolerance point count {FormatMetricValue(outOfTolerance)}";
        }

        var peakDeviation = metrics.FirstOrDefault(metric => metric.Name.Equals("Peak absolute deviation", StringComparison.Ordinal));
        if (peakDeviation is not null)
        {
            return $"Peak {FormatMetricValue(peakDeviation)}";
        }

        var distance = metrics.FirstOrDefault(metric => metric.Name.Equals("Distance", StringComparison.Ordinal));
        var heightDelta = metrics.FirstOrDefault(metric => metric.Name.Equals("Source Z height delta", StringComparison.Ordinal));
        if (distance is not null && heightDelta is not null)
        {
            return $"Distance {FormatMetricValue(distance)} | Height {FormatMetricValue(heightDelta)}";
        }

        var fallback = metrics.FirstOrDefault();
        return fallback is null ? "(missing)" : $"{fallback.Name} {FormatMetricValue(fallback)}";
    }

    private static string FormatMetricValue(MetricEvidence metric) =>
        string.IsNullOrWhiteSpace(metric.Unit) || metric.Unit.Equals("(unitless)", StringComparison.Ordinal)
            ? metric.Value
            : $"{metric.Value} {metric.Unit}";

    private static string FormatEvidencePath(string root, string path)
    {
        var displayPath = Path.GetRelativePath(root, path);
        if (!File.Exists(path))
        {
            return $"missing: {displayPath}";
        }

        var timestamp = File.GetLastWriteTime(path).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        return $"{displayPath}\n  modified: {timestamp}";
    }

    private static string FormatShortEvidencePath(string root, string path) =>
        File.Exists(path) ? Path.GetRelativePath(root, path) : $"missing: {Path.GetRelativePath(root, path)}";

    private static string FormatOptionalArtifact(string root, string? path) =>
        string.IsNullOrWhiteSpace(path) ? "(not requested)" : FormatShortEvidencePath(root, path);

    private string FormatShellScreenshotTarget(string root)
    {
        if (string.IsNullOrWhiteSpace(shellScreenshotPath))
        {
            return "(not requested)";
        }

        var path = Path.IsPathRooted(shellScreenshotPath)
            ? shellScreenshotPath
            : Path.Combine(root, shellScreenshotPath);
        return Path.GetRelativePath(root, path);
    }

    private static string FormatRunTime(string reportPath, string contractPath)
    {
        var evidencePath = File.Exists(reportPath) ? reportPath : contractPath;
        if (!File.Exists(evidencePath))
        {
            return "(pending)";
        }

        return File.GetLastWriteTime(evidencePath).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    }

    private static string PreviewLines(string root, string title, string path, string[] lines)
    {
        var displayPath = FormatShortEvidencePath(root, path);
        if (lines.Length == 0)
        {
            return $"{title}: missing or empty: {displayPath}";
        }

        return $"{title}: {displayPath}\n{string.Join(Environment.NewLine, lines.Take(18).Select(line => ShortenWorkspacePaths(root, line)))}";
    }

    private static string ShortenWorkspacePaths(string root, string text)
    {
        var normalizedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return text
            .Replace(normalizedRoot + Path.DirectorySeparatorChar, string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(normalizedRoot + Path.AltDirectorySeparatorChar, string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    private void RaisePropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed class RecipeRunHistoryItem
{
    public RecipeRunHistoryItem(
        string runTime,
        string status,
        string keyMetricSummary,
        string evidenceState,
        string reportPath)
    {
        RunTime = runTime;
        Status = status;
        KeyMetricSummary = keyMetricSummary;
        EvidenceState = evidenceState;
        ReportPath = reportPath;
    }

    public string RunTime { get; }

    public string Status { get; }

    public string KeyMetricSummary { get; }

    public string EvidenceState { get; }

    public string ReportPath { get; }
}

public sealed class InspectionStepItem
{
    public InspectionStepItem(string order, string stage, string status, string evidence)
    {
        Order = order;
        Stage = stage;
        Status = status;
        Evidence = evidence;
    }

    public string Order { get; }

    public string Stage { get; }

    public string Status { get; }

    public string Evidence { get; }
}

public sealed class EvidenceArtifactOpenRequestEventArgs : EventArgs
{
    public EvidenceArtifactOpenRequestEventArgs(string label, string path)
    {
        Label = label;
        Path = path;
    }

    public string Label { get; }

    public string Path { get; }
}

internal sealed record ToolComparisonEvidence(string ToolName, string Status, string KeyMetricSummary)
{
    public bool Matches(ToolComparisonEvidence other) =>
        !Status.Equals("(missing)", StringComparison.Ordinal)
        && ToolName.Equals(other.ToolName, StringComparison.Ordinal)
        && Status.Equals(other.Status, StringComparison.Ordinal)
        && KeyMetricSummary.Equals(other.KeyMetricSummary, StringComparison.Ordinal);
}

internal sealed record MetricEvidence(string Name, string Value, string Unit, string Status);
