using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

internal static class ToolRecipeLineFitRunnerExecution
{
    public static int Run(string recipePath, string lineFitStepId, string reportPath)
    {
        try
        {
            var fullRecipePath = Path.GetFullPath(recipePath);
            var document = ToolRecipeDocumentStore.Load(fullRecipePath);
            var lineStep = document.Steps.Single(step => string.Equals(step.Id, lineFitStepId, StringComparison.OrdinalIgnoreCase));
            if (!string.Equals(lineStep.ToolId, "three-d-line-fit", StringComparison.Ordinal) || lineStep.InputEntityIds.Count != 1)
            {
                throw new InvalidDataException("Runner Line Fit step must be one typed 3D Line Fit with one EdgePointSet input.");
            }
            var edgeStep = document.Steps.Single(step =>
                string.Equals(step.ToolId, "height-difference-edge", StringComparison.Ordinal)
                && string.Equals(step.OutputEntityId, lineStep.InputEntityIds[0], StringComparison.OrdinalIgnoreCase));
            var filterStep = document.Steps.Single(step =>
                string.Equals(step.ToolId, "filter", StringComparison.Ordinal)
                && string.Equals(step.OutputEntityId, edgeStep.InputEntityIds[0], StringComparison.OrdinalIgnoreCase));
            var recipeDirectory = Path.GetDirectoryName(fullRecipePath);
            var filter = ToolRecipeFilterExecution.Execute(document, filterStep.Id, recipeDirectory);
            if (filter.Result.Status != ResultStatus.Pass || filter.Output is null) throw new InvalidDataException($"Runner upstream Filter failed: {filter.Result.Message}");
            var edge = ToolRecipeHeightDifferenceEdgeExecution.Execute(document, edgeStep.Id, filter.Output);
            if (edge.Result.Status != ResultStatus.Pass || edge.Output is null) throw new InvalidDataException($"Runner upstream Edge failed: {edge.Result.Message}");
            var line = ToolRecipeLineFitExecution.Execute(document, lineStep.Id, edge.Output);
            if (line.Result.Status != ResultStatus.Pass || line.Output is null) throw new InvalidDataException($"Runner 3D Line Fit failed: {line.Result.Message}");
            var output = line.Output;
            var diagnostics = output.Diagnostics;
            var lines = new List<string>
            {
                "OpenVisionLab 3D Line Fit Runner report",
                $"Recipe|path={fullRecipePath}|schema={document.SchemaVersion}|name={document.Name}",
                $"Filter|step={filterStep.Id}|output={filter.Output.EntityId}|sha256={filter.Output.ContentSha256}",
                $"HeightDifferenceEdge|step={edgeStep.Id}|output={edge.Output.OutputEntityId}|sha256={edge.Output.ContentSha256}",
                $"LineFit|status={line.Result.Status}|step={lineStep.Id}|input={output.InputEdgePointSetEntityId}|inputSha256={output.InputContentSha256}|output={output.OutputEntityId}|sha256={output.ContentSha256}",
                $"Parameters|maxResidual={output.MaximumOrthogonalResidual:R}|minimumCount={output.MinimumInlierCount}|minimumRatio={output.MinimumInlierRatio:R}|minimumSpan={output.MinimumInlierScanlineSpan}",
                $"Line|anchor={output.AnchorX:R},{output.AnchorY:R},{output.AnchorZ:R}|direction={output.DirectionX:R},{output.DirectionY:R},{output.DirectionZ:R}|segmentStart={output.SegmentStartX:R},{output.SegmentStartY:R},{output.SegmentStartZ:R}|segmentEnd={output.SegmentEndX:R},{output.SegmentEndY:R},{output.SegmentEndZ:R}",
                $"Diagnostics|points={diagnostics.InputPointCount}|inliers={diagnostics.InlierCount}|outliers={diagnostics.OutlierCount}|ratio={diagnostics.InlierRatio:R}|scanlineSpan={diagnostics.InlierScanlineSpan}|rms={diagnostics.ResidualRms:R}|max={diagnostics.ResidualMaximum:R}|median={diagnostics.ResidualMedian:R}|hypotheses={diagnostics.HypothesisCount}|refinement={diagnostics.RefinementIterationCount}",
                $"Policy|method=DeterministicConsensusOrthogonalTls|hypotheses=Sha256PairSchedule/256|refinement=OrthogonalTlsUntilStable10|direction=PositiveScanlineAxis|segment=InlierProjectionExtents|contract={C3DLineFeature.ContractVersion}"
            };
            lines.AddRange(output.PointDiagnostics.Select(point => $"Point|index={point.InputPointIndex}|scanline={point.ScanlineIndex}|xyz={point.X:R},{point.Y:R},{point.Z:R}|projected={point.ProjectedX:R},{point.ProjectedY:R},{point.ProjectedZ:R}|residual={point.OrthogonalResidual:R}|inlier={point.IsInlier}"));
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
            File.WriteAllLines(reportPath, lines);
            Console.WriteLine($"3D Line Fit Runner: Pass ({diagnostics.InlierCount}/{diagnostics.InputPointCount} inliers, {output.ContentSha256})");
            return 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException or InvalidOperationException or OverflowException)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
            File.WriteAllLines(reportPath, ["OpenVisionLab 3D Line Fit Runner report", $"Error|{exception.Message}"]);
            Console.Error.WriteLine(exception.Message);
            return 5;
        }
    }
}
