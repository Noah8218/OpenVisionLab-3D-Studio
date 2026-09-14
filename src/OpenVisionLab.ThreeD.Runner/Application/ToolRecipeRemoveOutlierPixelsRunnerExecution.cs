using System.Text;
using System.Text.Json;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

internal static class ToolRecipeRemoveOutlierPixelsRunnerExecution
{
    public static int Run(
        string recipePath,
        string stepId,
        string outputC3DPath,
        string reportPath)
    {
        var fullReportPath = Path.GetFullPath(reportPath);
        try
        {
            var fullRecipePath = Path.GetFullPath(recipePath);
            var document = ToolRecipeDocumentStore.Load(fullRecipePath);
            var evaluation = ToolRecipeRemoveOutlierPixelsExecution.Execute(
                document,
                stepId,
                Path.GetDirectoryName(fullRecipePath));
            if (evaluation.Result.Status != ResultStatus.Pass
                || evaluation.Output is null
                || evaluation.OutlierMask is null)
            {
                Console.Error.WriteLine(evaluation.Result.Message);
                return 5;
            }

            var fullOutputPath = Path.GetFullPath(outputC3DPath);
            SaveC3DAtomically(evaluation.Output, fullOutputPath);
            var report = new
            {
                schemaVersion = "1.0",
                recipe = new
                {
                    path = fullRecipePath,
                    schemaVersion = document.SchemaVersion,
                    name = document.Name
                },
                step = new
                {
                    id = stepId,
                    toolId = "remove-outlier-pixels",
                    status = evaluation.Result.Status.ToString()
                },
                source = new
                {
                    id = document.Source.Id,
                    path = document.Source.Path,
                    byteLength = document.Source.ByteLength,
                    contentSha256 = document.Source.ContentSha256,
                    width = document.Source.GridWidth,
                    height = document.Source.GridHeight,
                    unit = document.Source.Unit,
                    frameId = document.Source.FrameId
                },
                output = new
                {
                    id = evaluation.Output.EntityId,
                    path = fullOutputPath,
                    byteLength = evaluation.Output.ByteLength,
                    contentSha256 = evaluation.Output.ContentSha256,
                    rootSourceSha256 = evaluation.Output.RootSourceSha256,
                    width = evaluation.Output.Width,
                    height = evaluation.Output.Height,
                    validCount = evaluation.Output.ValidCount,
                    missingCount = evaluation.Output.MissingCount,
                    minimum = evaluation.Output.Minimum,
                    maximum = evaluation.Output.Maximum,
                    mean = evaluation.Output.Mean,
                    provenance = evaluation.Output.Provenance
                },
                outlierMask = new
                {
                    contractVersion = C3DOutlierCellMap.ContractVersion,
                    encoding = C3DOutlierCellMap.Encoding,
                    width = evaluation.OutlierMask.Width,
                    height = evaluation.OutlierMask.Height,
                    outlierCellCount = evaluation.OutlierMask.OutlierCellCount,
                    packedByteLength = evaluation.OutlierMask.PackedByteLength,
                    sha256 = evaluation.OutlierMask.Sha256
                },
                result = new
                {
                    status = evaluation.Result.Status.ToString(),
                    message = evaluation.Result.Message,
                    elapsedMilliseconds =
                        evaluation.Result.Elapsed.TotalMilliseconds,
                    metrics = evaluation.Result.Metrics.Select(
                        metric => new
                        {
                            name = metric.Name,
                            value = metric.Value,
                            unit = metric.Unit
                        })
                },
                sourceMutation = false,
                claimBoundary =
                    "Deterministic raw-height preparation evidence; no physical calibration, measurement OK/NG, or metrology claim."
            };
            Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
            WriteTextAtomically(
                fullReportPath,
                JsonSerializer.Serialize(
                    report,
                    new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"Outlier-removed output: {fullOutputPath}");
            Console.WriteLine(
                $"Output SHA-256: {evaluation.Output.ContentSha256}");
            Console.WriteLine(
                $"Outlier mask SHA-256: {evaluation.OutlierMask.Sha256}");
            return 0;
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or InvalidDataException
                or JsonException
                or ArgumentException
                or OverflowException)
        {
            Console.Error.WriteLine(exception.Message);
            return 5;
        }
    }

    private static void SaveC3DAtomically(C3DHeightFieldSnapshot output, string path)
    {
        var fullPath = Path.GetFullPath(path);
        var temporaryPath = $"{fullPath}.tmp.{Guid.NewGuid():N}";
        try
        {
            output.SaveC3D(temporaryPath);
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

    private static void WriteTextAtomically(string path, string text)
    {
        var fullPath = Path.GetFullPath(path);
        var temporaryPath = $"{fullPath}.tmp.{Guid.NewGuid():N}";
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), 4096, leaveOpen: true))
            {
                writer.Write(text);
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
