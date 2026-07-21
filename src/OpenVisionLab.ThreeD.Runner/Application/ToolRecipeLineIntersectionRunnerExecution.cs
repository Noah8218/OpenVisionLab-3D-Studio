using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

internal static class ToolRecipeLineIntersectionRunnerExecution
{
    public static int Run(string recipePath, string intersectionStepId, string reportPath)
    {
        try
        {
            var fullRecipePath = Path.GetFullPath(recipePath);
            var document = ToolRecipeDocumentStore.Load(fullRecipePath);
            var intersectionStep = document.Steps.Single(step => string.Equals(step.Id, intersectionStepId, StringComparison.OrdinalIgnoreCase));
            if (!string.Equals(intersectionStep.ToolId, "line-intersection", StringComparison.Ordinal) || intersectionStep.InputEntityIds.Count != 2)
            {
                throw new InvalidDataException("Runner Line Intersection step must be one typed adapter with two published line inputs.");
            }

            var first = ExecuteLine(document, fullRecipePath, intersectionStep.InputEntityIds[0]);
            var second = ExecuteLine(document, fullRecipePath, intersectionStep.InputEntityIds[1]);
            var intersection = ToolRecipeLineIntersectionExecution.Execute(document, intersectionStep.Id, first.Output, second.Output);
            if (intersection.Result.Status != ResultStatus.Pass || intersection.Output is null)
            {
                throw new InvalidDataException($"Runner Line Intersection failed: {intersection.Result.Message}");
            }

            var output = intersection.Output;
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
            File.WriteAllLines(reportPath,
            [
                "OpenVisionLab 3D Line Intersection Runner report",
                $"Recipe|path={fullRecipePath}|schema={document.SchemaVersion}|name={document.Name}",
                DescribeLine("FirstLine", first),
                DescribeLine("SecondLine", second),
                $"LineIntersection|status={intersection.Result.Status}|step={intersectionStep.Id}|output={output.OutputEntityId}|sha256={output.ContentSha256}|role={output.OutputRole}",
                $"Parameters|maxGap={output.MaximumClosestApproachDistance:R}|minimumAngleDegrees={output.MinimumAcuteAngleDegrees:R}|maximumSupportExtension={output.MaximumSupportExtension:R}",
                $"Corner|anchor={output.CornerAnchorX:R},{output.CornerAnchorY:R},{output.CornerAnchorZ:R}|firstClosest={output.FirstClosestX:R},{output.FirstClosestY:R},{output.FirstClosestZ:R}|secondClosest={output.SecondClosestX:R},{output.SecondClosestY:R},{output.SecondClosestZ:R}",
                $"Evidence|gap={output.ClosestApproachDistance:R}|acuteAngleDegrees={output.AcuteAngleDegrees:R}|firstParameter={output.FirstLineParameter:R}|secondParameter={output.SecondLineParameter:R}|firstSupport={output.FirstSupportMinimum:R},{output.FirstSupportMaximum:R},{output.FirstSupportExtension:R}|secondSupport={output.SecondSupportMinimum:R},{output.SecondSupportMaximum:R},{output.SecondSupportExtension:R}",
                $"Policy|closest={output.ClosestApproachPolicy}|parallel={output.ParallelPolicy}|support={output.SupportPolicy}|contract={C3DLineIntersectionFeature.ContractVersion}"
            ]);
            Console.WriteLine($"3D Line Intersection Runner: Pass ({output.OutputRole}, gap {output.ClosestApproachDistance:G6}, {output.ContentSha256})");
            return 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException or InvalidOperationException or OverflowException)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
            File.WriteAllLines(reportPath, ["OpenVisionLab 3D Line Intersection Runner report", $"Error|{exception.Message}"]);
            Console.Error.WriteLine(exception.Message);
            return 5;
        }
    }

    private static LineExecution ExecuteLine(ToolRecipeDocument document, string recipePath, string outputEntityId)
    {
        var lineStep = document.Steps.Single(step => (string.Equals(step.ToolId, "three-d-line-fit", StringComparison.Ordinal)
                || string.Equals(step.ToolId, "two-point-line", StringComparison.Ordinal))
            && string.Equals(step.OutputEntityId, outputEntityId, StringComparison.OrdinalIgnoreCase));
        if (string.Equals(lineStep.ToolId, "two-point-line", StringComparison.Ordinal))
        {
            var twoPointLine = ToolRecipeTwoPointLineExecution.Execute(document, lineStep.Id, Path.GetDirectoryName(recipePath));
            if (twoPointLine.Result.Status != ResultStatus.Pass || twoPointLine.Output is null)
            {
                throw new InvalidDataException($"Runner upstream 2-Point Line failed: {twoPointLine.Result.Message}");
            }
            return new LineExecution(lineStep, twoPointLine.Output,
                $"selection={twoPointLine.Output.InputSelectionId}|selectionSha256={twoPointLine.Output.InputSelectionContentSha256}|points=row:{twoPointLine.Output.FirstRow},column:{twoPointLine.Output.FirstColumn};row:{twoPointLine.Output.SecondRow},column:{twoPointLine.Output.SecondColumn}");
        }
        var edgeStep = document.Steps.Single(step => string.Equals(step.ToolId, "height-difference-edge", StringComparison.Ordinal)
            && string.Equals(step.OutputEntityId, lineStep.InputEntityIds.Single(), StringComparison.OrdinalIgnoreCase));
        var filteredHeightFieldId = edgeStep.InputEntityIds.Single(inputId => document.Steps.Any(step =>
            string.Equals(step.ToolId, "filter", StringComparison.Ordinal)
            && string.Equals(step.OutputEntityId, inputId, StringComparison.OrdinalIgnoreCase)));
        var filterStep = document.Steps.Single(step => string.Equals(step.ToolId, "filter", StringComparison.Ordinal)
            && string.Equals(step.OutputEntityId, filteredHeightFieldId, StringComparison.OrdinalIgnoreCase));
        var filter = ToolRecipeFilterExecution.Execute(document, filterStep.Id, Path.GetDirectoryName(recipePath));
        if (filter.Result.Status != ResultStatus.Pass || filter.Output is null) throw new InvalidDataException($"Runner upstream Filter failed: {filter.Result.Message}");
        var edge = ToolRecipeHeightDifferenceEdgeExecution.Execute(document, edgeStep.Id, filter.Output);
        if (edge.Result.Status != ResultStatus.Pass || edge.Output is null) throw new InvalidDataException($"Runner upstream Edge failed: {edge.Result.Message}");
        var line = ToolRecipeLineFitExecution.Execute(document, lineStep.Id, edge.Output);
        if (line.Result.Status != ResultStatus.Pass || line.Output is null) throw new InvalidDataException($"Runner upstream Line Fit failed: {line.Result.Message}");
        return new LineExecution(lineStep, line.Output,
            $"edge={edge.Output.OutputEntityId}|edgeSha256={edge.Output.ContentSha256}");
    }

    private static string DescribeLine(string label, LineExecution line) =>
        $"{label}|step={line.LineStep.Id}|tool={line.LineStep.ToolId}|output={line.Output.OutputEntityId}|sha256={line.Output.ContentSha256}|origin={line.Output.OriginKind}|{line.Evidence}";

    private sealed record LineExecution(ToolRecipeStep LineStep, IC3DLineGeometry Output, string Evidence);
}
