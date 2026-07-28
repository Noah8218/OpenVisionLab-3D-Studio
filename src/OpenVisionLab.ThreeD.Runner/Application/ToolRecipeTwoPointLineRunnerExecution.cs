using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

internal static class ToolRecipeTwoPointLineRunnerExecution
{
    public static int Run(string recipePath, string twoPointLineStepId, string reportPath)
    {
        try
        {
            var fullRecipePath = Path.GetFullPath(recipePath);
            var document = ToolRecipeDocumentStore.Load(fullRecipePath);
            var step = document.Steps.Single(candidate => string.Equals(candidate.Id, twoPointLineStepId, StringComparison.OrdinalIgnoreCase));
            if (!string.Equals(step.ToolId, "two-point-line", StringComparison.Ordinal) || step.InputEntityIds.Count != 2)
            {
                throw new InvalidDataException("Runner 2-Point Line step must be one typed adapter with raw C3D and PointSet inputs.");
            }
            var evaluation = ToolRecipeTwoPointLineExecution.Execute(document, step.Id, Path.GetDirectoryName(fullRecipePath));
            if (evaluation.Result.Status != ResultStatus.Pass || evaluation.Output is null)
            {
                throw new InvalidDataException($"Runner 2-Point Line failed: {evaluation.Result.Message}");
            }
            var output = evaluation.Output;
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
            File.WriteAllLines(reportPath,
            [
                "OpenVisionLab 3D 2-Point Line Runner report",
                $"Recipe|path={fullRecipePath}|schema={document.SchemaVersion}|name={document.Name}",
                $"TwoPointLine|status={evaluation.Result.Status}|step={step.Id}|output={output.OutputEntityId}|sha256={output.ContentSha256}|role={output.OutputRole}",
                $"Inputs|source={output.RootSourceEntityId}|sourceSha256={output.RootSourceSha256}|selection={output.InputSelectionId}|selectionSha256={output.InputSelectionContentSha256}",
                $"Points|first=row:{output.FirstRow},column:{output.FirstColumn},xyz:{output.SegmentStartX:R},{output.SegmentStartY:R},{output.SegmentStartZ:R}|second=row:{output.SecondRow},column:{output.SecondColumn},xyz:{output.SegmentEndX:R},{output.SegmentEndY:R},{output.SegmentEndZ:R}",
                $"Line|anchor={output.AnchorX:R},{output.AnchorY:R},{output.AnchorZ:R}|direction={output.DirectionX:R},{output.DirectionY:R},{output.DirectionZ:R}|length={output.SegmentLength:R}",
                $"Policy|construction={output.ConstructionPolicy}|origin={output.OriginKind}|contract={C3DTwoPointLineFeature.ContractVersion}"
            ]);
            Console.WriteLine($"3D 2-Point Line Runner: Pass ({output.OutputRole}, length {output.SegmentLength:G6}, {output.ContentSha256})");
            return 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException or InvalidOperationException or OverflowException)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
            File.WriteAllLines(reportPath, ["OpenVisionLab 3D 2-Point Line Runner report", $"Error|{exception.Message}"]);
            Console.Error.WriteLine(exception.Message);
            return 5;
        }
    }
}
