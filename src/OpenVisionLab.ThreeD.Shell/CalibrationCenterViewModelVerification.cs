using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;

namespace OpenVisionLab.ThreeD.Shell;

internal static class CalibrationCenterViewModelVerification
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var lines = new List<string>
        {
            "OpenVisionLab 3D Calibration Center ViewModel verification",
            $"Generated: {DateTimeOffset.Now:O}"
        };
        var passed = 0;
        var fixtureRoot = Path.Combine(
            Path.GetTempPath(),
            "OpenVisionLab.ThreeD",
            "CalibrationCenterViewModelVerification",
            Guid.NewGuid().ToString("N"));

        try
        {
            var shell = new ShellMainWindowViewModel();
            var shellPropertyChanges = new List<string?>();
            shell.PropertyChanged += (_, args) => shellPropertyChanges.Add(args.PropertyName);

            Check("default workspace", shell.SelectedWorkspaceMode == ShellWorkspaceMode.Inspect, shell.SelectedWorkspaceMode.ToString());
            Check("default workspace flags", shell.IsInspectWorkspaceSelected && !shell.IsCalibrationWorkspaceSelected, shell.WorkspaceSummary);
            Check("default workspace summary", shell.WorkspaceSummary == "Inspection workspace", shell.WorkspaceSummary);
            Check("workspace command accepts Inspect", shell.SelectWorkspaceCommand.CanExecute(ShellWorkspaceMode.Inspect), "Inspect accepted");
            Check("workspace command accepts Calibrate", shell.SelectWorkspaceCommand.CanExecute(ShellWorkspaceMode.Calibrate), "Calibrate accepted");
            Check("workspace command rejects unknown parameter", !shell.SelectWorkspaceCommand.CanExecute("Calibrate"), "string rejected");

            shell.SelectWorkspaceCommand.Execute(ShellWorkspaceMode.Calibrate);
            Check("calibration workspace selection", shell.SelectedWorkspaceMode == ShellWorkspaceMode.Calibrate, shell.SelectedWorkspaceMode.ToString());
            Check("calibration workspace flags", !shell.IsInspectWorkspaceSelected && shell.IsCalibrationWorkspaceSelected, shell.WorkspaceSummary);
            Check("calibration workspace summary", shell.WorkspaceSummary == "Calibration workspace | Offline datasets", shell.WorkspaceSummary);
            Check(
                "workspace property notifications",
                shellPropertyChanges.Contains(nameof(shell.SelectedWorkspaceMode))
                && shellPropertyChanges.Contains(nameof(shell.IsInspectWorkspaceSelected))
                && shellPropertyChanges.Contains(nameof(shell.IsCalibrationWorkspaceSelected))
                && shellPropertyChanges.Contains(nameof(shell.WorkspaceSummary)),
                string.Join(',', shellPropertyChanges));

            shell.SelectWorkspaceCommand.Execute("Calibrate");
            Check("invalid workspace execution is ignored", shell.SelectedWorkspaceMode == ShellWorkspaceMode.Calibrate, shell.SelectedWorkspaceMode.ToString());
            shell.SelectWorkspaceCommand.Execute(ShellWorkspaceMode.Inspect);
            Check("workspace returns to Inspect", shell.IsInspectWorkspaceSelected, shell.WorkspaceSummary);
            shell.IsCalibrationWorkspaceSelected = true;
            Check("two-way calibration selection", shell.SelectedWorkspaceMode == ShellWorkspaceMode.Calibrate, shell.WorkspaceSummary);
            shell.IsInspectWorkspaceSelected = true;
            Check("two-way Inspect selection", shell.SelectedWorkspaceMode == ShellWorkspaceMode.Inspect, shell.WorkspaceSummary);

            var viewModel = shell.Calibration;
            var propertyChanges = new List<string?>();
            viewModel.PropertyChanged += (_, args) => propertyChanges.Add(args.PropertyName);
            Check(
                "section choices",
                viewModel.Sections.SequenceEqual(
                    new[] { "Overview", "Height Calibration", "Sensor Alignment", "Repeatability", "History" },
                    StringComparer.Ordinal),
                string.Join(',', viewModel.Sections));
            Check("default section", viewModel.SelectedSection == CalibrationSection.Repeatability && viewModel.SelectedSectionIndex == 3, viewModel.SelectedSection.ToString());
            viewModel.SelectedSectionIndex = 0;
            Check("section index selection", viewModel.SelectedSection == CalibrationSection.Overview, viewModel.SelectedSection.ToString());
            viewModel.SelectedSection = CalibrationSection.History;
            Check("typed section selection", viewModel.SelectedSectionIndex == 4, viewModel.SelectedSectionIndex.ToString(CultureInfo.InvariantCulture));
            viewModel.SelectedSectionIndex = -1;
            Check("invalid section index ignored", viewModel.SelectedSection == CalibrationSection.History, viewModel.SelectedSection.ToString());
            Check(
                "section property notifications",
                propertyChanges.Contains(nameof(viewModel.SelectedSection))
                && propertyChanges.Contains(nameof(viewModel.SelectedSectionIndex)),
                string.Join(',', propertyChanges));

            Check("empty profile state", viewModel.ActiveProfileStatus == "No active calibration profile" && viewModel.CalibrationStatus.Contains("no active profile", StringComparison.Ordinal), viewModel.CalibrationStatus);
            Check("empty input state", viewModel.HeightTargetStatus.EndsWith("not loaded", StringComparison.Ordinal) && viewModel.RepeatabilityInputStatus == "No repeatability study loaded", viewModel.RepeatabilityInputStatus);
            Check("empty repeatability collection", viewModel.RepeatabilityRuns.Count == 0 && viewModel.SelectedRepeatabilityRun is null, viewModel.SelectedRepeatabilityRunSummary);
            Check("empty run chart", viewModel.RepeatabilityChartValues.Count == 0 && viewModel.RepeatabilityChartSeries.Count == 1 && viewModel.RepeatabilityChartSeries[0].Values.Count == 0 && viewModel.RepeatabilityChartRunLabels.Count == 0 && !viewModel.SelectRepeatabilityChartPointCommand.CanExecute(null), "empty");
            Check("single metric contract", viewModel.RepeatabilityMetricOptions.SequenceEqual(new[] { "Thickness" }) && !viewModel.HasMultipleRepeatabilityMetrics, viewModel.SelectedRepeatabilityMetric);
            Check("load command enabled", viewModel.LoadStudyCommand.CanExecute(null), "enabled");
            Check("workflow commands disabled without study", !viewModel.CalculateCommand.CanExecute(null) && !viewModel.ValidateCommand.CanExecute(null) && !viewModel.ActivateCommand.CanExecute(null), "all disabled");

            var loadRequests = 0;
            viewModel.LoadStudyRequested += (_, _) => loadRequests++;
            viewModel.LoadStudyCommand.Execute(null);
            Check("load command requests file bridge", loadRequests == 1, loadRequests.ToString(CultureInfo.InvariantCulture));

            var missingPath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(reportPath))!, "missing-study.json");
            Check("missing study is controlled", !viewModel.LoadStudy(missingPath), viewModel.RepeatabilitySummary);
            Check("failed load clears state", !viewModel.HasLoadedRepeatabilityStudy && viewModel.RepeatabilityRuns.Count == 0 && viewModel.RepeatabilityChartValues.Count == 0 && viewModel.RepeatabilityChartSeries.Count == 1 && viewModel.RepeatabilityChartSeries[0].Values.Count == 0 && !viewModel.CalculateCommand.CanExecute(null), viewModel.RepeatabilityInputStatus);
            Check("failed load is visible", viewModel.RepeatabilitySummary.Contains("Study load failed", StringComparison.Ordinal), viewModel.RepeatabilitySummary);

            var fixture = CreateFixture(fixtureRoot);
            propertyChanges.Clear();
            Check("valid study loads", viewModel.LoadStudy(fixture.StudyPath), viewModel.RepeatabilitySummary);
            Check("loaded identity retained", viewModel.HasLoadedRepeatabilityStudy && Path.GetFullPath(fixture.StudyPath) == viewModel.LoadedRepeatabilityStudyPath, viewModel.LoadedRepeatabilityStudyPath ?? "null");
            Check("load does not calculate", !viewModel.HasCalculatedRepeatability && viewModel.RepeatabilityMapState == "Not calculated", viewModel.RepeatabilityMapState);
            Check("verified input enables only Calculate", viewModel.CanCalculate && viewModel.CalculateCommand.CanExecute(null) && !viewModel.ValidateCommand.CanExecute(null) && !viewModel.ActivateCommand.CanExecute(null), "calculate only");
            Check("loaded run rows", viewModel.RepeatabilityRuns.Count == 3 && viewModel.RepeatabilityRuns.All(run => run.Status == "Ready"), string.Join(',', viewModel.RepeatabilityRuns.Select(run => run.Status)));
            Check("run value formatting", viewModel.RepeatabilityRuns[1].ValueText == "10.004 mm", viewModel.RepeatabilityRuns[1].ValueText);
            Check(
                "loaded run chart keeps raw values and labels",
                viewModel.RepeatabilityChartValues.Count == 3
                && viewModel.RepeatabilityChartSeries.Count == 1
                && ReferenceEquals(viewModel.RepeatabilityChartSeries[0].Values, viewModel.RepeatabilityChartValues)
                && viewModel.RepeatabilityChartRunLabels.SequenceEqual(new[] { "1", "2", "3" }, StringComparer.Ordinal)
                && viewModel.RepeatabilityChartValues.SequenceEqual(new[] { 10.000, 10.004, 9.998 }),
                string.Join(',', viewModel.RepeatabilityChartValues.Select(value => value.ToString("0.###", CultureInfo.InvariantCulture))));
            Check("loaded study fields", viewModel.ProfileNameValue == fixture.Document.StudyId && viewModel.UnitFrameValue == "mm | frame.synthetic-repeatability", $"{viewModel.ProfileNameValue}|{viewModel.UnitFrameValue}");
            Check("acceptance fields", viewModel.MinimumRunCountValue == "3" && viewModel.StandardDeviationLimitValue == "0.005 mm" && viewModel.RangeLimitValue == "0.01 mm", $"{viewModel.MinimumRunCountValue}|{viewModel.StandardDeviationLimitValue}|{viewModel.RangeLimitValue}");
            Check(
                "load property notifications",
                propertyChanges.Contains(nameof(viewModel.CanCalculate))
                && propertyChanges.Contains(nameof(viewModel.RepeatabilitySummary))
                && propertyChanges.Contains(nameof(viewModel.UnitFrameStatus)),
                string.Join(',', propertyChanges));

            var chartRun = viewModel.RepeatabilityRuns[2];
            Check("run chart command enabled", viewModel.SelectRepeatabilityChartPointCommand.CanExecute(chartRun), chartRun.ValueText);
            viewModel.SelectRepeatabilityChartPointCommand.Execute(chartRun);
            Check("run chart selects shared run", ReferenceEquals(viewModel.SelectedRepeatabilityRun, chartRun), viewModel.SelectedRepeatabilityRunSummary);

            var selected = viewModel.RepeatabilityRuns[1];
            viewModel.SelectedRepeatabilityRun = selected;
            Check("shared run selection", ReferenceEquals(viewModel.SelectedRepeatabilityRun, selected) && viewModel.SelectedRepeatabilityRunSummary.Contains(selected.RunId, StringComparison.Ordinal), viewModel.SelectedRepeatabilityRunSummary);

            viewModel.CalculateCommand.Execute(null);
            Check("explicit Calculate produces result", viewModel.HasCalculatedRepeatability && viewModel.RepeatabilityMapState == "Aggregate result: Pass", viewModel.RepeatabilityMapState);
            Check("calculation summary binds statistics", viewModel.RepeatabilitySummary.Contains("Mean 10.000667", StringComparison.Ordinal) && viewModel.RepeatabilitySummary.Contains("Range 0.006 mm", StringComparison.Ordinal) && viewModel.RepeatabilitySummary.EndsWith("Pass", StringComparison.Ordinal), viewModel.RepeatabilitySummary);
            Check("calculation keeps source rows", viewModel.RepeatabilityRuns.Count == 3 && viewModel.RepeatabilityRuns.All(run => run.Status == "Included"), string.Join(',', viewModel.RepeatabilityRuns.Select(run => run.Status)));
            Check("claim boundary remains visible", viewModel.RepeatabilityCalculationMessage.Contains("not Gauge R&R", StringComparison.Ordinal), viewModel.RepeatabilityCalculationMessage);
            Check("profile workflow remains blocked", !viewModel.ValidateCommand.CanExecute(null) && !viewModel.ActivateCommand.CanExecute(null), "validate/activate disabled");

            var constantFixture = CreateFixture(
                fixtureRoot,
                "calibration-viewmodel-constant-study-fixture",
                new[] { 10.0, 10.0, 10.0 });
            Check("constant study loads", viewModel.LoadStudy(constantFixture.StudyPath), viewModel.RepeatabilitySummary);
            Check(
                "constant run chart keeps finite raw values",
                viewModel.RepeatabilityChartValues.Count == 3
                && viewModel.RepeatabilityChartValues.All(value => Math.Abs(value - 10.0) < 0.000001)
                && viewModel.RepeatabilityChartRunLabels.SequenceEqual(new[] { "1", "2", "3" }, StringComparer.Ordinal),
                string.Join(',', viewModel.RepeatabilityChartValues.Select(value => value.ToString("0.###", CultureInfo.InvariantCulture))));
            Check("stale run chart datum rejected", !viewModel.SelectRepeatabilityChartPointCommand.CanExecute(chartRun), chartRun.ValueText);

            var invalidStudyPath = WriteModelInvalidStudy(fixture);
            Check("model-invalid study does not become ready", !viewModel.LoadStudy(invalidStudyPath), viewModel.RepeatabilitySummary);
            Check("model-invalid evidence remains inspectable", viewModel.HasLoadedRepeatabilityStudy && viewModel.RepeatabilityRuns.Count == 3 && viewModel.RepeatabilityRuns.All(run => run.Status == "Invalid"), viewModel.RepeatabilitySummary);
            Check("reload clears prior calculation", !viewModel.HasCalculatedRepeatability && !viewModel.CalculateCommand.CanExecute(null), viewModel.RepeatabilityMapState);

            summary = $"Calibration Center ViewModel verification: Pass ({passed} checks)";
            lines.Add(summary);
            WriteReport(reportPath, lines);
            return true;
        }
        catch (Exception exception)
        {
            summary = $"Calibration Center ViewModel verification: Fail after {passed} checks: {exception.Message}";
            lines.Add(summary);
            WriteReport(reportPath, lines);
            return false;
        }
        finally
        {
            if (Directory.Exists(fixtureRoot))
            {
                Directory.Delete(fixtureRoot, recursive: true);
            }
        }

        void Check(string name, bool condition, string detail)
        {
            if (!condition)
            {
                throw new InvalidOperationException($"{name}: {detail}");
            }

            passed++;
            lines.Add($"PASS|{name}|{detail}");
        }
    }

    private static StudyFixture CreateFixture(
        string fixtureRoot,
        string fixtureName = "calibration-viewmodel-study-fixture",
        IReadOnlyList<double>? values = null)
    {
        var directory = Path.Combine(fixtureRoot, fixtureName);
        Directory.CreateDirectory(directory);
        var capturedAt = new DateTimeOffset(2026, 7, 17, 1, 0, 0, TimeSpan.Zero);
        var runValues = values ?? new[] { 10.000, 10.004, 9.998 };
        var runs = new ThicknessRepeatabilityStudyRunDocument[runValues.Count];
        for (var index = 0; index < runValues.Count; index++)
        {
            var name = $"viewmodel-acquisition-{index + 1:000}.bin";
            var path = Path.Combine(directory, name);
            File.WriteAllBytes(path, Encoding.UTF8.GetBytes($"ViewModel synthetic acquisition {index + 1:000}\n"));
            var info = new FileInfo(path);
            runs[index] = new ThicknessRepeatabilityStudyRunDocument(
                $"run.viewmodel.{index + 1:000}",
                $"source.viewmodel.{index + 1:000}",
                name,
                info.Length,
                Hash(path),
                capturedAt.AddMinutes(index),
                "mm",
                "frame.synthetic-repeatability",
                runValues[index]);
        }

        var document = new ThicknessRepeatabilityStudyDocument(
            ThicknessRepeatabilityStudyLoader.SupportedStudyType,
            ThicknessRepeatabilityStudyLoader.SupportedSchemaVersion,
            "study.viewmodel-thickness-repeatability",
            "measurement.viewmodel-thickness",
            "roi.viewmodel-reference",
            "mm",
            "frame.synthetic-repeatability",
            new ThicknessRepeatabilityAcceptance(3, 0.005, 0.010),
            runs);
        var studyPath = Path.Combine(directory, "valid-study.json");
        WriteDocument(studyPath, document);
        return new StudyFixture(directory, studyPath, document);
    }

    private static string WriteModelInvalidStudy(StudyFixture fixture)
    {
        var runs = fixture.Document.Runs!.ToArray();
        runs[1] = runs[1] with { Unit = "um" };
        var path = Path.Combine(fixture.Directory, "model-invalid-study.json");
        WriteDocument(path, fixture.Document with { Runs = runs });
        return path;
    }

    private static void WriteDocument(string path, ThicknessRepeatabilityStudyDocument document) =>
        File.WriteAllText(
            path,
            JsonSerializer.Serialize(document, JsonOptions),
            new UTF8Encoding(false));

    private static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static void WriteReport(string reportPath, IEnumerable<string> lines)
    {
        var fullPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllLines(fullPath, lines);
    }

    private sealed record StudyFixture(
        string Directory,
        string StudyPath,
        ThicknessRepeatabilityStudyDocument Document);
}
