using System.Text.Json;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

internal static class ToolRecipeFilterRunnerExecution
{
    public static int Run(string recipePath, string stepId, string outputC3DPath, string reportPath)
    {
        try
        {
            var fullRecipePath = Path.GetFullPath(recipePath);
            var document = ToolRecipeDocumentStore.Load(fullRecipePath);
            var evaluation = ToolRecipeFilterExecution.Execute(
                document,
                stepId,
                Path.GetDirectoryName(fullRecipePath));
            if (evaluation.Result.Status != ResultStatus.Pass || evaluation.Output is null)
            {
                Console.Error.WriteLine(evaluation.Result.Message);
                return 5;
            }

            var fullOutputPath = Path.GetFullPath(outputC3DPath);
            evaluation.Output.SaveC3D(fullOutputPath);
            var report = new
            {
                schemaVersion = "1.0",
                recipe = new { path = fullRecipePath, schemaVersion = document.SchemaVersion, name = document.Name },
                step = new { id = stepId, toolId = "filter", status = evaluation.Result.Status.ToString() },
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
                result = new
                {
                    status = evaluation.Result.Status.ToString(),
                    message = evaluation.Result.Message,
                    elapsedMilliseconds = evaluation.Result.Elapsed.TotalMilliseconds,
                    metrics = evaluation.Result.Metrics.Select(metric => new { name = metric.Name, value = metric.Value, unit = metric.Unit })
                },
                claimBoundary = "Preprocessing output in the uncalibrated raw-height/display frame; no measurement OK/NG or physical metrology claim."
            };
            var fullReportPath = Path.GetFullPath(reportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
            File.WriteAllText(fullReportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"Filter output: {fullOutputPath}");
            Console.WriteLine($"Filter SHA-256: {evaluation.Output.ContentSha256}");
            return 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or JsonException or ArgumentException or OverflowException)
        {
            Console.Error.WriteLine(exception.Message);
            return 5;
        }
    }
}
