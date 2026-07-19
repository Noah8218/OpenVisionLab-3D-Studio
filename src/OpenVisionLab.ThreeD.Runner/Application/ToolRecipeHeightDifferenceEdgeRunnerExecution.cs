using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

internal static class ToolRecipeHeightDifferenceEdgeRunnerExecution
{
    public static int Run(string recipePath, string edgeStepId, string reportPath)
    {
        try
        {
            var fullRecipePath = Path.GetFullPath(recipePath);
            var document = ToolRecipeDocumentStore.Load(fullRecipePath);
            var edgeStep = document.Steps.Single(step => string.Equals(step.Id, edgeStepId, StringComparison.OrdinalIgnoreCase));
            if (!string.Equals(edgeStep.ToolId, "height-difference-edge", StringComparison.Ordinal)
                || edgeStep.InputEntityIds.Count != 2)
            {
                throw new InvalidDataException("Runner Edge step must be one typed Height Difference Edge with a height field and GridRectangle.");
            }

            var filterStep = document.Steps.Single(step =>
                string.Equals(step.ToolId, "filter", StringComparison.Ordinal)
                && string.Equals(step.OutputEntityId, edgeStep.InputEntityIds[0], StringComparison.OrdinalIgnoreCase));
            var recipeDirectory = Path.GetDirectoryName(fullRecipePath);
            var filter = ToolRecipeFilterExecution.Execute(document, filterStep.Id, recipeDirectory);
            if (filter.Result.Status != ResultStatus.Pass || filter.Output is null)
            {
                throw new InvalidDataException($"Runner upstream Filter failed: {filter.Result.Message}");
            }

            var edge = ToolRecipeHeightDifferenceEdgeExecution.Execute(document, edgeStep.Id, filter.Output);
            if (edge.Result.Status != ResultStatus.Pass || edge.Output is null)
            {
                throw new InvalidDataException($"Runner Height Difference Edge failed: {edge.Result.Message}");
            }

            var output = edge.Output;
            var diagnostics = output.Diagnostics;
            var lines = new List<string>
            {
                "OpenVisionLab 3D Height Difference Edge Runner report",
                $"Recipe|path={fullRecipePath}|schema={document.SchemaVersion}|name={document.Name}",
                $"Filter|step={filterStep.Id}|output={filter.Output.EntityId}|sha256={filter.Output.ContentSha256}|rootSha256={filter.Output.RootSourceSha256}",
                $"HeightDifferenceEdge|status={edge.Result.Status}|step={edgeStep.Id}|input={output.InputEntityId}|selection={output.SelectionId}|output={output.OutputEntityId}|axis={output.ComparisonAxis}|polarity={output.Polarity}|minimumDelta={output.MinimumDelta:R}|points={output.Points.Count}|sha256={output.ContentSha256}",
                $"Diagnostics|scanlines={diagnostics.ScanlineCount}|eligiblePairs={diagnostics.EligiblePairCount}|missingPairSkips={diagnostics.SkippedMissingPairCount}|accepted={diagnostics.AcceptedScanlineCount}|noCandidate={diagnostics.NoCandidateScanlineCount}|magnitudeMin={diagnostics.AcceptedMagnitudeMinimum:R}|magnitudeMax={diagnostics.AcceptedMagnitudeMaximum:R}|magnitudeMean={diagnostics.AcceptedMagnitudeMean:R}",
                $"Policy|candidate=StrongestPerScanline|tie=LowestStartIndex|point=PairMidpoint|missing=SkipPair|boundary=WithinSelection|contract={C3DHeightDifferenceEdgePointSet.ContractVersion}"
            };
            lines.AddRange(output.Points.Select(point =>
                $"Point|scanline={point.ScanlineIndex}|first={point.FirstRow},{point.FirstColumn},{point.FirstHeight:R}|second={point.SecondRow},{point.SecondColumn},{point.SecondHeight:R}|delta={point.SignedDelta:R}|magnitude={point.Magnitude:R}|xyz={point.X:R},{point.Y:R},{point.Z:R}"));
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
            File.WriteAllLines(reportPath, lines);
            Console.WriteLine($"Height Difference Edge Runner: Pass ({output.Points.Count} points, {output.ContentSha256})");
            return 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException or InvalidOperationException or OverflowException)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
            File.WriteAllLines(reportPath, ["OpenVisionLab 3D Height Difference Edge Runner report", $"Error|{exception.Message}"]);
            Console.Error.WriteLine(exception.Message);
            return 5;
        }
    }
}
