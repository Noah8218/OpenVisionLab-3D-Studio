using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

internal static class ToolRecipeLandmarkCorrespondenceRunnerExecution
{
    public static int Run(string recipePath, string correspondenceStepId, string reportPath)
    {
        try
        {
            var fullRecipePath = Path.GetFullPath(recipePath);
            var document = ToolRecipeDocumentStore.Load(fullRecipePath);
            var step = document.Steps.Single(candidate => string.Equals(candidate.Id, correspondenceStepId, StringComparison.OrdinalIgnoreCase));
            if (!string.Equals(step.ToolId, "landmark-correspondence", StringComparison.Ordinal) || step.InputEntityIds.Count != 1)
            {
                throw new InvalidDataException("Runner Landmark Correspondence step must be one typed adapter with one correspondence selection input.");
            }
            var selection = (document.Selections ?? []).Single(candidate => string.Equals(candidate.Id, step.InputEntityIds[0], StringComparison.OrdinalIgnoreCase));
            if (!string.Equals(selection.Kind, ToolRecipeSelectionKinds.LandmarkCorrespondenceSet, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Runner Landmark Correspondence input must be landmark-correspondence-set.");
            }

            var corners = (selection.Rows ?? [])
                .Select(row => ExecuteCorner(document, fullRecipePath, row.SourceEntityId))
                .ToArray();
            var correspondence = ToolRecipeLandmarkCorrespondenceExecution.Execute(document, step.Id, corners.Select(item => item.Output).ToArray());
            if (correspondence.Result.Status != ResultStatus.Pass || correspondence.Output is null)
            {
                throw new InvalidDataException($"Runner Landmark Correspondence failed: {correspondence.Result.Message}");
            }

            var output = correspondence.Output;
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
            var lines = new List<string>
            {
                "OpenVisionLab 3D Landmark Correspondence Runner report",
                $"Recipe|path={fullRecipePath}|schema={document.SchemaVersion}|name={document.Name}",
                $"LandmarkCorrespondence|status={correspondence.Result.Status}|step={step.Id}|output={output.OutputEntityId}|sha256={output.ContentSha256}|pairs={output.Pairs.Count}",
                $"Source|entity={output.RootSourceEntityId}|sha256={output.RootSourceSha256}|unit={output.SourceUnit}|frame={output.SourceFrameId}|convention={output.SourceCoordinateConvention}",
                $"Reference|frame={output.ReferenceFrameId}|unit={output.ReferenceUnit}|provenance={output.ReferenceProvenance}|revision={output.ReferenceRevision}",
                $"Independence|sourceRank={output.SourceRank}|referenceRank={output.ReferenceRank}|sourceNormalizedVolume={output.SourceNormalizedTetrahedronVolume:R}|referenceNormalizedVolume={output.ReferenceNormalizedTetrahedronVolume:R}|minimum={output.MinimumNormalizedTetrahedronVolume:R}",
                $"Policy|pairs={output.PairCountPolicy}|source={output.SourceArtifactPolicy}|independence={output.AffineIndependencePolicy}|contract={C3DLandmarkCorrespondenceSet.ContractVersion}"
            };
            lines.AddRange(corners.Select((corner, index) =>
                $"Corner|index={index + 1}|step={corner.Step.Id}|output={corner.Output.OutputEntityId}|role={corner.Output.OutputRole}|sha256={corner.Output.ContentSha256}|anchor={corner.Output.CornerAnchorX:R},{corner.Output.CornerAnchorY:R},{corner.Output.CornerAnchorZ:R}"));
            lines.AddRange(output.Pairs.Select((pair, index) =>
                $"Pair|index={index + 1}|source={pair.SourceEntityId}|sourceSha256={pair.SourceContentSha256}|reference={pair.ReferenceLandmarkId}|referenceXyz={pair.ReferenceX:R},{pair.ReferenceY:R},{pair.ReferenceZ:R}"));
            File.WriteAllLines(reportPath, lines);
            Console.WriteLine($"3D Landmark Correspondence Runner: Pass ({output.Pairs.Count} pairs, {output.ContentSha256})");
            return 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException or InvalidOperationException or OverflowException)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
            File.WriteAllLines(reportPath, ["OpenVisionLab 3D Landmark Correspondence Runner report", $"Error|{exception.Message}"]);
            Console.Error.WriteLine(exception.Message);
            return 5;
        }
    }

    private static CornerExecution ExecuteCorner(ToolRecipeDocument document, string recipePath, string outputEntityId)
    {
        var intersectionStep = document.Steps.Single(step => string.Equals(step.ToolId, "line-intersection", StringComparison.Ordinal)
            && string.Equals(step.OutputEntityId, outputEntityId, StringComparison.OrdinalIgnoreCase));
        if (intersectionStep.InputEntityIds.Count != 2)
        {
            throw new InvalidDataException($"Runner CornerAnchor '{outputEntityId}' must originate from exactly two LineFeature inputs.");
        }
        var first = ExecuteLine(document, recipePath, intersectionStep.InputEntityIds[0]);
        var second = ExecuteLine(document, recipePath, intersectionStep.InputEntityIds[1]);
        var intersection = ToolRecipeLineIntersectionExecution.Execute(document, intersectionStep.Id, first.Output, second.Output);
        if (intersection.Result.Status != ResultStatus.Pass || intersection.Output is null)
        {
            throw new InvalidDataException($"Runner upstream Line Intersection '{intersectionStep.Id}' failed: {intersection.Result.Message}");
        }
        return new CornerExecution(intersectionStep, intersection.Output);
    }

    private static LineExecution ExecuteLine(ToolRecipeDocument document, string recipePath, string outputEntityId)
    {
        var lineStep = document.Steps.Single(step => string.Equals(step.ToolId, "three-d-line-fit", StringComparison.Ordinal)
            && string.Equals(step.OutputEntityId, outputEntityId, StringComparison.OrdinalIgnoreCase));
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
        return new LineExecution(lineStep, line.Output);
    }

    private sealed record CornerExecution(ToolRecipeStep Step, C3DLineIntersectionFeature Output);
    private sealed record LineExecution(ToolRecipeStep Step, C3DLineFeature Output);
}

