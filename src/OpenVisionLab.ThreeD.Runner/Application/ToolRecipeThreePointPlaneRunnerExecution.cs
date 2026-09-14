using System.Text;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

internal static class ToolRecipeThreePointPlaneRunnerExecution
{
    public static int Run(string recipePath, string threePointPlaneStepId, string reportPath)
    {
        var fullReportPath = Path.GetFullPath(reportPath);
        try
        {
            var fullRecipePath = Path.GetFullPath(recipePath);
            var document = ToolRecipeDocumentStore.Load(fullRecipePath);
            var step = document.Steps.Single(candidate => string.Equals(candidate.Id, threePointPlaneStepId, StringComparison.OrdinalIgnoreCase));
            if (!string.Equals(step.ToolId, "three-point-plane", StringComparison.Ordinal) || step.InputEntityIds.Count != 2)
            {
                throw new InvalidDataException("Runner 3-Point Plane step must be one typed adapter with raw C3D and PointSet inputs.");
            }
            var evaluation = ToolRecipeThreePointPlaneExecution.Execute(document, step.Id, Path.GetDirectoryName(fullRecipePath));
            if (evaluation.Result.Status != ResultStatus.Pass || evaluation.Output is null)
            {
                throw new InvalidDataException($"Runner 3-Point Plane failed: {evaluation.Result.Message}");
            }
            var output = evaluation.Output;
            Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
            WriteLinesAtomically(fullReportPath,
            [
                "OpenVisionLab 3D 3-Point Plane Runner report",
                $"Recipe|path={fullRecipePath}|schema={document.SchemaVersion}|name={document.Name}",
                $"ThreePointPlane|status={evaluation.Result.Status}|step={step.Id}|output={output.OutputEntityId}|sha256={output.ContentSha256}|role={output.OutputRole}",
                $"Inputs|source={output.RootSourceEntityId}|sourceSha256={output.RootSourceSha256}|selection={output.InputSelectionId}|selectionSha256={output.InputSelectionContentSha256}",
                $"Points|first=row:{output.FirstRow},column:{output.FirstColumn},xyz:{output.AnchorX:R},{output.AnchorY:R},{output.AnchorZ:R}|second=row:{output.SecondRow},column:{output.SecondColumn},xyz:{output.SecondX:R},{output.SecondY:R},{output.SecondZ:R}|third=row:{output.ThirdRow},column:{output.ThirdColumn},xyz:{output.ThirdX:R},{output.ThirdY:R},{output.ThirdZ:R}",
                $"Plane|normal={output.NormalX:R},{output.NormalY:R},{output.NormalZ:R}|offset={output.PlaneOffset:R}|normalizedCrossMagnitude={output.NormalizedCrossMagnitude:R}",
                $"Policy|construction={output.ConstructionPolicy}|contract={C3DThreePointPlaneFeature.ContractVersion}"
            ]);
            Console.WriteLine($"3D 3-Point Plane Runner: Pass ({output.OutputRole}, normal {output.NormalX:G6},{output.NormalY:G6},{output.NormalZ:G6}, {output.ContentSha256})");
            return 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException or InvalidOperationException or OverflowException)
        {
            TryWriteErrorReport(fullReportPath, exception);
            Console.Error.WriteLine(exception.Message);
            return 5;
        }
    }

    private static void TryWriteErrorReport(string fullReportPath, Exception exception)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
            WriteLinesAtomically(
                fullReportPath,
                [
                    "OpenVisionLab 3D 3-Point Plane Runner report",
                    $"Error|{exception.Message}"
                ]);
        }
        catch (Exception reportException) when (
            reportException is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or InvalidOperationException
            or OverflowException)
        {
            Console.Error.WriteLine($"3-Point Plane report could not be written: {reportException.Message}");
        }
    }

    private static void WriteLinesAtomically(string path, IEnumerable<string> lines)
    {
        var fullPath = Path.GetFullPath(path);
        var temporaryPath = $"{fullPath}.tmp.{Guid.NewGuid():N}";
        try
        {
            using (var stream = new FileStream(
                       temporaryPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       4096,
                       FileOptions.WriteThrough))
            using (var writer = new StreamWriter(
                       stream,
                       new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                       4096,
                       leaveOpen: true))
            {
                foreach (var line in lines)
                {
                    writer.WriteLine(line);
                }

                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
