using System.Text;
using System.Text.Json;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

internal static class ToolRecipeHeightThresholdBackgroundRemovalRunnerExecution
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
            var evaluation = ToolRecipeHeightThresholdBackgroundRemovalExecution.Execute(
                document,
                stepId,
                Path.GetDirectoryName(fullRecipePath));
            if (evaluation.Result.Status is not (ResultStatus.Pass or ResultStatus.Warning)
                || evaluation.Output is null
                || evaluation.Evidence is null)
            {
                Console.Error.WriteLine(evaluation.Result.Message);
                return 5;
            }

            var fullOutputPath = Path.GetFullPath(outputC3DPath);
            SaveC3DAtomically(evaluation.Output, fullOutputPath);
            var output = evaluation.Output;
            var evidence = evaluation.Evidence;
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
                    toolId = "height-threshold-background-removal",
                    status = evaluation.Result.Status.ToString(),
                    threshold = evidence.Threshold,
                    mode = evidence.Mode.ToString(),
                    comparisonPolicy = evidence.ComparisonPolicy,
                    missingValuePolicy = evidence.MissingValuePolicy,
                    backgroundPolicy = evidence.BackgroundPolicy
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
                    id = output.EntityId,
                    path = fullOutputPath,
                    byteLength = output.ByteLength,
                    contentSha256 = output.ContentSha256,
                    rootSourceSha256 = output.RootSourceSha256,
                    width = output.Width,
                    height = output.Height,
                    validCount = output.ValidCount,
                    missingCount = output.MissingCount,
                    provenance = output.Provenance
                },
                evidence = new
                {
                    contractVersion = C3DHeightThresholdBackgroundRemovalEvidence.ContractVersion,
                    contentSha256 = evidence.ContentSha256,
                    outputContentSha256 = evidence.OutputContentSha256,
                    inputValidSampleCount = evidence.InputValidSampleCount,
                    inputMissingSampleCount = evidence.InputMissingSampleCount,
                    retainedValidSampleCount = evidence.RetainedValidSampleCount,
                    removedBackgroundSampleCount = evidence.RemovedBackgroundSampleCount,
                    hasForeground = evidence.HasForeground
                },
                result = new
                {
                    status = evaluation.Result.Status.ToString(),
                    message = evaluation.Result.Message,
                    elapsedMilliseconds = evaluation.Result.Elapsed.TotalMilliseconds,
                    metrics = evaluation.Result.Metrics.Select(metric => new
                    {
                        name = metric.Name,
                        value = metric.Value,
                        unit = metric.Unit,
                        status = metric.Status?.ToString()
                    })
                },
                sourceMutation = false,
                claimBoundary =
                    "Deterministic raw-height preparation evidence; no physical calibration, measurement acceptance, or metrology claim."
            };
            Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
            WriteTextAtomically(
                fullReportPath,
                JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"Height-threshold output: {fullOutputPath}");
            Console.WriteLine($"Output SHA-256: {output.ContentSha256}");
            Console.WriteLine($"Evidence SHA-256: {evidence.ContentSha256}");
            return 0;
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or InvalidDataException
                or JsonException
                or ArgumentException
                or InvalidOperationException
                or OverflowException)
        {
            Console.Error.WriteLine(exception.Message);
            return 5;
        }
    }

    private static void SaveC3DAtomically(C3DHeightFieldSnapshot output, string path)
    {
        var temporaryPath = $"{path}.tmp.{Guid.NewGuid():N}";
        try
        {
            output.SaveC3D(temporaryPath);
            File.Move(temporaryPath, path, overwrite: true);
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
