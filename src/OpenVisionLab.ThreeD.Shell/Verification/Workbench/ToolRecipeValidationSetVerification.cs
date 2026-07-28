using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Shell;

internal static class ToolRecipeValidationSetVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        var lines = new List<string> { "Validation Set ordered graph verification" };
        var passed = 0;
        var total = 0;
        var artifactRoot = Path.Combine(
            Path.GetDirectoryName(Path.GetFullPath(reportPath))!,
            "validation-set-fixture");

        void Check(string name, bool condition, string detail)
        {
            total++;
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
            if (condition) passed++;
        }

        try
        {
            Directory.CreateDirectory(artifactRoot);
            var taughtPath = Path.Combine(artifactRoot, "taught.C3D");
            var passPath = Path.Combine(artifactRoot, "sample-pass.C3D");
            var failPath = Path.Combine(artifactRoot, "sample-fail.C3D");
            var mismatchPath = Path.Combine(artifactRoot, "sample-grid-mismatch.C3D");
            C3DHeightFieldSnapshot.CreateForVerification(
                "source.validation", 4, 4,
                [10, 11, 12, 13, 11, 12, 13, 14, 12, 13, 14, 15, 13, 14, 15, 16]).SaveC3D(taughtPath);
            C3DHeightFieldSnapshot.CreateForVerification(
                "source.validation", 4, 4,
                [9, 10, 11, 12, 10, 11, 12, 13, 11, 12, 13, 14, 12, 13, 14, 15]).SaveC3D(passPath);
            C3DHeightFieldSnapshot.CreateForVerification(
                "source.validation", 4, 4,
                [10, 11, 12, 13, 11, 12, 13, 14, 32, 33, 34, 35, 33, 34, 35, 36]).SaveC3D(failPath);
            C3DHeightFieldSnapshot.CreateForVerification(
                "source.validation", 3, 3,
                [10, 11, 12, 11, 12, 13, 12, 13, 14]).SaveC3D(mismatchPath);

            var binding = ToolRecipeSelectionSourceBindingVerifier.ReadIdentity(taughtPath);
            var sourceInfo = new FileInfo(taughtPath);
            var source = new ToolRecipeSource(
                "source.validation",
                "Validation taught source",
                "C3D",
                "model",
                "frame.c3d-grid-index",
                taughtPath,
                sourceInfo.Length,
                binding.ContentSha256,
                binding.GridWidth,
                binding.GridHeight);
            var referenceSelection = new ToolRecipeSelection(
                "selection.validation.reference-roi",
                "Validation Reference ROI",
                ToolRecipeSelectionKinds.GridRectangle,
                source.Id,
                source.FrameId,
                binding,
                new ToolRecipeGridRectangle(0, 0, 2, 4),
                null,
                null);
            var measurementSelection = new ToolRecipeSelection(
                "selection.validation.measurement-roi",
                "Validation Measurement ROI",
                ToolRecipeSelectionKinds.GridRectangle,
                source.Id,
                source.FrameId,
                binding,
                new ToolRecipeGridRectangle(2, 0, 2, 4),
                null,
                null);
            var step = new ToolRecipeStep(
                "step.validation.measurement",
                "thickness",
                "Dual-surface Thickness",
                3,
                [source.Id, referenceSelection.Id, measurementSelection.Id],
                "result.validation.measurement",
                [
                    new ToolRecipeParameter("MinimumThickness", "0"),
                    new ToolRecipeParameter("MaximumThickness", "10"),
                    new ToolRecipeParameter("MinimumValidSampleCount", "1")
                ]);
            var document = new ToolRecipeDocument(
                ToolRecipeDocument.CurrentSchemaVersion,
                "Validation Set fixture",
                source,
                [],
                [step],
                [referenceSelection, measurementSelection]);
            var recipePath = Path.Combine(artifactRoot, "validation-set-fixture.ov3d-recipe.json");
            ToolRecipeDocumentStore.Save(recipePath, document);

            Check(
                "supported contract is explicit",
                ToolRecipeValidationSetExecution.CanExecute(document, out var capability),
                capability);

            var originalPath = document.Source.Path;
            var originalHash = document.Source.ContentSha256;
            var result = ToolRecipeValidationSetExecution.Execute(
                document,
                [passPath, failPath, mismatchPath]);
            Check(
                "all selected samples complete",
                result.Samples.Count == 3,
                result.Message);
            Check(
                "passing sample remains Pass",
                result.Samples[0].Status == ResultStatus.Pass,
                $"{result.Samples[0].Status} | {result.Samples[0].Message}");
            Check(
                "out-of-tolerance sample is Fail",
                result.Samples[1].Status == ResultStatus.Fail,
                $"{result.Samples[1].Status} | {result.Samples[1].Steps.Single().Evidence}");
            Check(
                "grid mismatch fails closed",
                result.Samples[2].Status == ResultStatus.Error
                && result.Samples[2].Message.Contains("Grid mismatch", StringComparison.Ordinal),
                $"{result.Samples[2].Status} | {result.Samples[2].Message}");
            Check(
                "failure does not stop later evidence",
                result.Samples[2].Order == 3,
                string.Join(",", result.Samples.Select(sample => $"{sample.Order}:{sample.Status}")));
            Check(
                "aggregate preserves failure and error",
                result.Status == ResultStatus.Error,
                result.Status.ToString());
            Check(
                "authored recipe is not mutated",
                string.Equals(document.Source.Path, originalPath, StringComparison.OrdinalIgnoreCase)
                && string.Equals(document.Source.ContentSha256, originalHash, StringComparison.OrdinalIgnoreCase),
                $"{document.Source.Path} | {document.Source.ContentSha256}");

            var unsupported = document with
            {
                Steps =
                [
                    step with
                    {
                        ToolId = "roi-crop",
                        ToolName = "ROI / Crop",
                        MinimumInputCount = 1,
                        InputEntityIds = [source.Id],
                        Parameters = []
                    }
                ]
            };
            Check(
                "tool without an executable adapter is reported, not fabricated",
                !ToolRecipeValidationSetExecution.CanExecute(unsupported, out var unsupportedMessage)
                && !string.IsNullOrWhiteSpace(unsupportedMessage),
                unsupportedMessage);
            Check(
                "fixture recipe remains reopenable",
                ToolRecipeDocumentStore.Load(recipePath).Steps.Count == 1,
                recipePath);

            var graphPackage = Path.GetFullPath(Path.Combine(
                Environment.CurrentDirectory,
                "3D",
                "SyntheticValidation",
                "AffineInspectionPlateV1"));
            var graphRecipePath = Path.Combine(graphPackage, "inspection-recipe.ov3d-recipe.json");
            var graphSourcePath = Path.Combine(graphPackage, "source-affine-inspection-plate-v1.C3D");
            var graphDocument = ToolRecipeDocumentStore.Load(graphRecipePath);
            var graphIdentity = ToolRecipeSelectionSourceBindingVerifier.ReadIdentity(graphSourcePath);
            var graphSourceInfo = new FileInfo(graphSourcePath);
            var graphSource = C3DHeightFieldSnapshot.LoadVerified(
                graphSourcePath,
                graphDocument.Source.Id,
                graphDocument.Source.Unit,
                graphDocument.Source.FrameId,
                graphSourceInfo.Length,
                graphIdentity.ContentSha256,
                graphIdentity.GridWidth,
                graphIdentity.GridHeight);
            var graphPassPath = Path.Combine(artifactRoot, "graph-pass.C3D");
            File.Copy(graphSourcePath, graphPassPath, overwrite: true);

            var graphSelections = graphDocument.Selections
                ?? throw new InvalidDataException("Synthetic graph recipe requires authored selections.");
            var graphFailValues = graphSource.Values.ToArray();
            var thicknessStep = graphDocument.Steps.Single(candidate => candidate.ToolId == "thickness");
            var thicknessSelection = graphSelections.Single(candidate =>
                string.Equals(candidate.Id, thicknessStep.InputEntityIds[2], StringComparison.Ordinal));
            AddFinite(graphFailValues, graphSource.Width, thicknessSelection.GridRectangle!, 100);
            var graphFailPath = Path.Combine(artifactRoot, "graph-measurement-fail.C3D");
            C3DHeightFieldSnapshot.CreateForVerification(
                graphDocument.Source.Id,
                graphSource.Width,
                graphSource.Height,
                graphFailValues).SaveC3D(graphFailPath);

            var graphErrorValues = graphSource.Values.ToArray();
            var firstEdgeStep = graphDocument.Steps.First(candidate => candidate.ToolId == "height-difference-edge");
            var firstEdgeSelection = graphSelections.Single(candidate =>
                string.Equals(candidate.Id, firstEdgeStep.InputEntityIds[1], StringComparison.Ordinal));
            Fill(graphErrorValues, graphSource.Width, firstEdgeSelection.GridRectangle!, double.NaN);
            var graphErrorPath = Path.Combine(artifactRoot, "graph-upstream-error.C3D");
            C3DHeightFieldSnapshot.CreateForVerification(
                graphDocument.Source.Id,
                graphSource.Width,
                graphSource.Height,
                graphErrorValues).SaveC3D(graphErrorPath);

            Check(
                "full synthetic affine graph is executable",
                ToolRecipeValidationSetExecution.CanExecute(graphDocument, out var graphCapability),
                graphCapability);
            var graphOriginalPath = graphDocument.Source.Path;
            var graphOriginalHash = graphDocument.Source.ContentSha256;
            var graphResult = ToolRecipeValidationSetExecution.Execute(
                graphDocument,
                [graphPassPath, graphFailPath, graphErrorPath]);
            Check(
                "full graph validation completes Pass Fail Error samples",
                graphResult.Samples.Count == 3
                && graphResult.Samples[0].Status == ResultStatus.Pass
                && graphResult.Samples[1].Status == ResultStatus.Fail
                && graphResult.Samples[2].Status == ResultStatus.Error,
                string.Join(",", graphResult.Samples.Select(sample => $"{sample.Order}:{sample.Status}:{sample.Steps.Count}")));
            Check(
                "passing sample reaches every authored tool in order",
                graphResult.Samples[0].Steps.Count == graphDocument.Steps.Count
                && graphResult.Samples[0].Steps.Select(item => item.StepId)
                    .SequenceEqual(graphDocument.Steps.Select(item => item.Id)),
                $"{graphResult.Samples[0].Steps.Count}/{graphDocument.Steps.Count}");
            Check(
                "measurement failure preserves later ordered evidence",
                graphResult.Samples[1].Steps.Count == graphDocument.Steps.Count
                && graphResult.Samples[1].Steps[^1].StepId == graphDocument.Steps[^1].Id,
                $"{graphResult.Samples[1].Steps.Count}/{graphDocument.Steps.Count};last={graphResult.Samples[1].Steps.LastOrDefault()?.StepId}");
            Check(
                "upstream error stops dependent graph",
                graphResult.Samples[2].Steps.Count < graphDocument.Steps.Count
                && graphResult.Samples[2].Message.Contains("stopped ordered replay", StringComparison.Ordinal),
                $"{graphResult.Samples[2].Steps.Count}/{graphDocument.Steps.Count};{graphResult.Samples[2].Message}");
            Check(
                "full graph replay leaves authored recipe identity unchanged",
                string.Equals(graphDocument.Source.Path, graphOriginalPath, StringComparison.OrdinalIgnoreCase)
                && string.Equals(graphDocument.Source.ContentSha256, graphOriginalHash, StringComparison.OrdinalIgnoreCase),
                $"{graphDocument.Source.Path} | {graphDocument.Source.ContentSha256}");

            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                var canceled = false;
                try
                {
                    ToolRecipeValidationSetExecution.Execute(
                        graphDocument,
                        [graphPassPath],
                        cancellation.Token);
                }
                catch (OperationCanceledException)
                {
                    canceled = true;
                }

                Check(
                    "pre-canceled replay exits without a partial result",
                    canceled,
                    canceled ? "OperationCanceledException" : "execution was not canceled");
            }

            var workbench = new ToolWorkbenchViewModel(Path.Combine(artifactRoot, "recent-validation-set.json"));
            Check(
                "workbench reopens the full graph recipe",
                workbench.TryOpenTeachingRecipe(graphRecipePath, out var openMessage),
                openMessage);
            var workbenchSourcePath = workbench.Source.Path;
            Check(
                "current recipe input stages as one pending sample without execution",
                workbench.AddCurrentSourceToValidationSetCommand.CanExecute(null)
                && StageCurrentSource(workbench, graphSourcePath),
                $"count={workbench.ValidationSetAllCount};status={workbench.ValidationSetSamples.FirstOrDefault()?.Status};source={workbench.ValidationSetSamples.FirstOrDefault()?.SourcePath}");
            workbench.SetValidationSetSources([graphPassPath, graphFailPath, graphErrorPath]);
            workbench.RunValidationSetAsync().GetAwaiter().GetResult();
            Check(
                "workbench exposes Pass Fail Error counts and selects the first issue",
                workbench.ValidationSetAllCount == 3
                && workbench.ValidationSetPassCount == 1
                && workbench.ValidationSetFailCount == 1
                && workbench.ValidationSetErrorCount == 1
                && workbench.SelectedValidationSetSample?.Status == "Fail",
                $"{workbench.ValidationSetAllCount}/{workbench.ValidationSetPassCount}/{workbench.ValidationSetFailCount}/{workbench.ValidationSetErrorCount};selected={workbench.SelectedValidationSetSample?.Status}");
            Check(
                "selected failed step retains metric and overlay evidence",
                workbench.SelectedValidationSetStep is { Metrics.Count: > 0, Overlays.Count: > 0 },
                $"step={workbench.SelectedValidationSetStep?.ToolName};metrics={workbench.SelectedValidationSetStep?.Metrics.Count};overlays={workbench.SelectedValidationSetStep?.Overlays.Count}");

            workbench.SetValidationSetFilterCommand.Execute("Fail");
            Check(
                "status filter reduces the visible sample list",
                workbench.ValidationSetSamples.Count == 1
                && workbench.ValidationSetSamples[0].Status == "Fail",
                $"{workbench.ValidationSetFilter};visible={workbench.ValidationSetSamples.Count}");
            workbench.SetValidationSetFilterCommand.Execute("All");
            workbench.NextValidationSetIssueCommand.Execute(null);
            var movedToError = workbench.SelectedValidationSetSample?.Status == "Error";
            workbench.PreviousValidationSetIssueCommand.Execute(null);
            Check(
                "issue navigation moves between Fail and Error",
                movedToError && workbench.SelectedValidationSetSample?.Status == "Fail",
                $"nextError={movedToError};previous={workbench.SelectedValidationSetSample?.Status}");

            var comparisonRequested = false;
            workbench.ValidationSetComparisonRequested += (_, _) => comparisonRequested = true;
            workbench.OpenValidationSetComparisonCommand.Execute(null);
            Check(
                "selected failure opens a read-only source comparison",
                comparisonRequested
                && string.Equals(workbench.CompareSlotAArtifactId, graphDocument.Source.Id, StringComparison.Ordinal)
                && workbench.GetCompareCandidate(workbench.CompareSlotBArtifactId)?.C3DPath == graphFailPath,
                $"requested={comparisonRequested};A={workbench.CompareSlotAArtifactId};B={workbench.CompareSlotBArtifactId}");
            Check(
                "analysis workflow leaves the authored source unchanged",
                string.Equals(workbench.Source.Path, workbenchSourcePath, StringComparison.OrdinalIgnoreCase)
                && string.Equals(workbench.Source.Path, graphSourcePath, StringComparison.OrdinalIgnoreCase),
                workbench.Source.Path);
        }
        catch (Exception exception)
        {
            lines.Add($"FAIL | unhandled verification exception | {exception}");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllLines(reportPath, lines);
        summary = $"Validation Set verification: {passed}/{total} passed | {Path.GetFullPath(reportPath)}";
        return passed == total && total == 25;
    }

    private static bool StageCurrentSource(ToolWorkbenchViewModel workbench, string expectedSourcePath)
    {
        workbench.AddCurrentSourceToValidationSetCommand.Execute(null);
        return workbench.ValidationSetAllCount == 1
               && workbench.ValidationSetSamples.Count == 1
               && workbench.ValidationSetSamples[0].Status == "Pending"
               && string.Equals(
                   workbench.ValidationSetSamples[0].SourcePath,
                   expectedSourcePath,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static void AddFinite(
        double[] values,
        int width,
        ToolRecipeGridRectangle rectangle,
        double offset)
    {
        for (var row = rectangle.Row; row < rectangle.Row + rectangle.RowCount; row++)
        for (var column = rectangle.Column; column < rectangle.Column + rectangle.ColumnCount; column++)
        {
            var index = row * width + column;
            if (double.IsFinite(values[index])) values[index] += offset;
        }
    }

    private static void Fill(
        double[] values,
        int width,
        ToolRecipeGridRectangle rectangle,
        double value)
    {
        for (var row = rectangle.Row; row < rectangle.Row + rectangle.RowCount; row++)
        for (var column = rectangle.Column; column < rectangle.Column + rectangle.ColumnCount; column++)
            values[row * width + column] = value;
    }
}
