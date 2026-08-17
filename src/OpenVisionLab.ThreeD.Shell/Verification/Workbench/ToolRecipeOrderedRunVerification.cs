using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Shell;

internal static class ToolRecipeOrderedRunVerification
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static bool Verify(string reportPath, out string summary)
    {
        var lines = new List<string>
        {
            "OpenVisionLab 3D Studio current-recipe ordered Run verification",
            $"Generated: {DateTimeOffset.Now:O}"
        };
        var total = 0;
        var passed = 0;
        void Check(string name, bool condition, string detail)
        {
            total++;
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
            if (condition) passed++;
        }

        try
        {
            var reportDirectory = Path.GetDirectoryName(Path.GetFullPath(reportPath))
                ?? throw new InvalidOperationException("Ordered Run verification report has no directory.");
            Directory.CreateDirectory(reportDirectory);
            var root = Path.Combine(
                reportDirectory,
                "ordered-run-verification",
                Guid.NewGuid().ToString("N"));
            var runRoot = Path.Combine(root, "runs");
            Directory.CreateDirectory(root);

            var sourcePath = Path.Combine(root, "thickness-pass.C3D");
            C3DHeightFieldSnapshot.CreateForVerification(
                "source.c3d.height-map",
                4,
                4,
                [10, 10, 10, 10, 10, 10, 10, 10, 15, 15, 15, 15, 15, 15, 15, 15])
                .SaveC3D(sourcePath);
            var passRecipePath = Path.Combine(root, "thickness-pass.ov3d-recipe.json");
            var passDocument = CreateThicknessDocument(sourcePath, 4.5, 5.5);
            ToolRecipeDocumentStore.Save(passRecipePath, passDocument);

            var shell = CreateShell(root, "pass", runRoot);
            var fullRunCount = 0;
            shell.Workbench.OrderedRunCompleted += (_, _) => fullRunCount++;
            var opened = shell.Workbench.TryOpenTeachingRecipe(
                passRecipePath,
                out var openMessage);
            Check(
                "saved valid Thickness enables current-recipe Run",
                opened
                && shell.Workbench.RunTeachingRecipeCommand.CanExecute(null)
                && shell.Workbench.OrderedRunStatus == shell.Workbench.Localization.RecipeHealthReady
                && shell.Workbench.OrderedRunCapabilitySummary.Contains("1", StringComparison.Ordinal),
                opened ? shell.Workbench.OrderedRunCapabilitySummary : openMessage);
            Check(
                "open does not auto-run",
                fullRunCount == 0
                && !shell.Workbench.HasOrderedRunResult
                && shell.InspectionSteps.Count == 0,
                $"count={fullRunCount}; result={shell.Workbench.HasOrderedRunResult}; recordSteps={shell.InspectionSteps.Count}");

            var previewed = shell.Workbench.PreviewSelectedMeasurementAsync()
                .GetAwaiter().GetResult();
            shell.Workbench.PublishSelectedStepCommand.Execute(null);
            Check(
                "Preview and Publish do not invoke full Run",
                previewed
                && shell.Workbench.IsMeasurementPreviewPublished
                && fullRunCount == 0
                && !shell.Workbench.HasOrderedRunResult,
                $"preview={previewed}; published={shell.Workbench.IsMeasurementPreviewPublished}; fullRuns={fullRunCount}");
            var previewOutput = shell.Workbench.CurrentMeasurementOutput;
            var saved = shell.Workbench.TrySaveTeachingRecipe(
                passRecipePath,
                out var saveMessage);
            Check(
                "save does not invoke full Run or change the published Preview output",
                saved
                && fullRunCount == 0
                && ReferenceEquals(previewOutput, shell.Workbench.CurrentMeasurementOutput),
                saved ? $"fullRuns={fullRunCount}" : saveMessage);

            var reopenedShell = CreateShell(root, "reopen", runRoot);
            var reopenRunCount = 0;
            reopenedShell.Workbench.OrderedRunCompleted += (_, _) => reopenRunCount++;
            var reopened = reopenedShell.Workbench.TryOpenTeachingRecipe(
                passRecipePath,
                out var reopenMessage);
            Check(
                "reopen restores the saved recipe without running it",
                reopened
                && reopenRunCount == 0
                && !reopenedShell.Workbench.HasOrderedRunResult
                && reopenedShell.Workbench.RunTeachingRecipeCommand.CanExecute(null),
                reopened ? reopenedShell.Workbench.OrderedRunCapabilitySummary : reopenMessage);

            var runCompleted = reopenedShell.Workbench.RunTeachingRecipeAsync()
                .GetAwaiter().GetResult();
            var recordPath = reopenedShell.Workbench.CurrentOrderedRunRecordPath;
            var record = ReadRecord(recordPath);
            Check(
                "explicit Run executes once and persists a Results-ready Run Record",
                runCompleted
                && reopenRunCount == 1
                && record is not null
                && record.Status == ResultStatus.Pass
                && record.Steps?.Count == 1
                && reopenedShell.InspectionSteps.Count == 1
                && reopenedShell.RunSnapshotSummary.Contains("Pass", StringComparison.OrdinalIgnoreCase),
                $"completed={runCompleted}; fullRuns={reopenRunCount}; record={recordPath}; status={record?.Status}");

            var directExecution = ToolRecipeOrderedGraphExecution.Execute(
                passDocument,
                sourcePath);
            var runnerProjection = ToolRecipeOrderedGraphRunRecordProjection.Create(
                passDocument,
                directExecution);
            var recordStep = record?.Steps?.SingleOrDefault();
            var projectedStep = runnerProjection.SingleOrDefault();
            Check(
                "Studio and Runner share status, metric, step, output, and content identities",
                record is not null
                && record.Status == directExecution.Status
                && recordStep is not null
                && projectedStep is not null
                && recordStep.Id == projectedStep.Id
                && recordStep.Status == projectedStep.Status
                && recordStep.OutputEntityId == projectedStep.OutputEntityId
                && recordStep.OutputContentSha256 == projectedStep.OutputContentSha256
                && recordStep.Metrics.SequenceEqual(projectedStep.Metrics),
                $"status={record?.Status}/{directExecution.Status}; step={recordStep?.Id}/{projectedStep?.Id}; output={recordStep?.OutputEntityId}; hash={recordStep?.OutputContentSha256}");
            Check(
                "Thickness Pass metric remains exact",
                recordStep?.Metrics.Single(metric => metric.Name == "Mean").Value is { } mean
                && Math.Abs(mean - 5d) <= 1e-12,
                $"mean={recordStep?.Metrics.SingleOrDefault(metric => metric.Name == "Mean")?.Value:G17}");

            var selectedStep = reopenedShell.Workbench.SelectedPipelineStep!;
            selectedStep.Parameters.Single(parameter => parameter.Name == "MaximumThickness").Value = "4.9";
            Check(
                "editing invalidates current evidence and requires save without auto-run",
                reopenedShell.Workbench.IsDirty
                && !reopenedShell.Workbench.HasOrderedRunResult
                && reopenedShell.Workbench.CurrentOrderedRunRecordPath is null
                && reopenedShell.InspectionSteps.Count == 0
                && reopenRunCount == 1
                && !reopenedShell.Workbench.RunTeachingRecipeCommand.CanExecute(null)
                && reopenedShell.Workbench.OrderedRunCapabilitySummary.Contains("저장", StringComparison.Ordinal),
                $"dirty={reopenedShell.Workbench.IsDirty}; fullRuns={reopenRunCount}; summary={reopenedShell.Workbench.OrderedRunCapabilitySummary}");

            var failRecipePath = Path.Combine(root, "thickness-fail.ov3d-recipe.json");
            var failSaved = reopenedShell.Workbench.TrySaveTeachingRecipe(
                failRecipePath,
                out var failSaveMessage);
            Check(
                "saving the edited Fail recipe still does not auto-run",
                failSaved
                && reopenRunCount == 1
                && !reopenedShell.Workbench.HasOrderedRunResult,
                failSaved ? $"fullRuns={reopenRunCount}" : failSaveMessage);
            var failRun = reopenedShell.Workbench.RunTeachingRecipeAsync()
                .GetAwaiter().GetResult();
            var failRecord = ReadRecord(
                reopenedShell.Workbench.CurrentOrderedRunRecordPath);
            Check(
                "explicit out-of-tolerance Run records Fail as a completed execution",
                failRun
                && reopenRunCount == 2
                && failRecord?.Status == ResultStatus.Fail
                && failRecord.Steps?.Single().Status == ResultStatus.Fail,
                $"completed={failRun}; status={failRecord?.Status}; fullRuns={reopenRunCount}");

            var corruptSourcePath = Path.Combine(root, "corrupt.C3D");
            File.Copy(sourcePath, corruptSourcePath);
            var errorDocument = passDocument with
            {
                Source = passDocument.Source with { Path = corruptSourcePath }
            };
            var errorRecipePath = Path.Combine(root, "thickness-error.ov3d-recipe.json");
            ToolRecipeDocumentStore.Save(errorRecipePath, errorDocument);
            File.WriteAllText(corruptSourcePath, "not a C3D file");
            var errorExecution = ToolRecipeOrderedGraphExecution.Execute(
                errorDocument,
                corruptSourcePath);
            var errorArtifact = ShellOrderedRunRecordWriter.Write(
                errorRecipePath,
                errorDocument,
                corruptSourcePath,
                errorExecution,
                runRoot);
            var errorRecord = ReadRecord(errorArtifact.JsonPath);
            Check(
                "Error execution remains Error in the same Run Record projection",
                errorExecution.Status == ResultStatus.Error
                && errorRecord?.Status == ResultStatus.Error
                && errorRecord.Metrics.Count == 0,
                $"execution={errorExecution.Status}; record={errorRecord?.Status}; message={errorExecution.Message}");

            var unsupportedDocument = passDocument with
            {
                Steps =
                [
                    passDocument.Steps[0] with
                    {
                        ToolId = "unsupported-tool",
                        ToolName = "Unsupported Tool"
                    }
                ]
            };
            var unsupported = ToolRecipeOrderedGraphExecution.CanExecute(
                unsupportedDocument,
                out var unsupportedMessage);
            Check(
                "unsupported ordered step fails closed with its exact reason",
                !unsupported
                && unsupportedMessage.Contains("unsupported", StringComparison.OrdinalIgnoreCase),
                unsupportedMessage);
        }
        catch (Exception exception)
        {
            Check("unhandled exception", false, exception.ToString());
        }

        summary = $"ToolRecipeOrderedRunVerification|{(passed == total ? "Pass" : "Fail")}|checks={total}|passed={passed}|failed={total - passed}";
        lines.Add(summary);
        var fullReportPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
        File.WriteAllLines(fullReportPath, lines);
        return passed == total;
    }

    private static ShellMainWindowViewModel CreateShell(
        string root,
        string name,
        string runRoot) =>
        new(
            recentRunRecordsPath: Path.Combine(root, $"recent-runs-{name}.json"),
            recentRecipesPath: Path.Combine(root, $"recent-recipes-{name}.json"),
            orderedRunRecordRoot: runRoot);

    private static ToolRecipeDocument CreateThicknessDocument(
        string sourcePath,
        double minimum,
        double maximum)
    {
        var identity = ToolRecipeSelectionSourceBindingVerifier.ReadIdentity(
            sourcePath);
        var source = new ToolRecipeSource(
            "source.c3d.height-map",
            "Thickness verification",
            "C3D",
            "raw-height",
            "frame.c3d-grid-index",
            Path.GetFullPath(sourcePath),
            new FileInfo(sourcePath).Length,
            identity.ContentSha256,
            identity.GridWidth,
            identity.GridHeight);
        var reference = new ToolRecipeSelection(
            "selection.reference",
            "Reference surface ROI",
            ToolRecipeSelectionKinds.GridRectangle,
            source.Id,
            source.FrameId,
            identity,
            new ToolRecipeGridRectangle(0, 0, 2, 4),
            null,
            null);
        var measurement = new ToolRecipeSelection(
            "selection.measurement",
            "Measurement ROI",
            ToolRecipeSelectionKinds.GridRectangle,
            source.Id,
            source.FrameId,
            identity,
            new ToolRecipeGridRectangle(2, 0, 2, 4),
            null,
            null);
        var step = new ToolRecipeStep(
            "step.thickness.01",
            "thickness",
            "Thickness",
            3,
            [source.Id, reference.Id, measurement.Id],
            "derived.measurementresult.01",
            [
                new ToolRecipeParameter("MinimumThickness", minimum.ToString("G17", System.Globalization.CultureInfo.InvariantCulture)),
                new ToolRecipeParameter("MaximumThickness", maximum.ToString("G17", System.Globalization.CultureInfo.InvariantCulture)),
                new ToolRecipeParameter("MinimumValidSampleCount", "1")
            ]);
        return new ToolRecipeDocument(
            ToolRecipeDocument.CurrentSchemaVersion,
            "Thickness ordered Run verification",
            source,
            [],
            [step],
            [reference, measurement]);
    }

    private static InspectionRunRecord? ReadRecord(string? path) =>
        string.IsNullOrWhiteSpace(path) || !File.Exists(path)
            ? null
            : JsonSerializer.Deserialize<InspectionRunRecord>(
                File.ReadAllText(path),
                JsonOptions);
}
