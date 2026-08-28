using System.Text.Json;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

internal static class C3DHeightThresholdBackgroundRemovalRunnerExecution
{
    public static int Run(string specificationPath, string reportPath)
    {
        var fullReportPath = Path.GetFullPath(reportPath);
        try
        {
            var fullSpecificationPath = Path.GetFullPath(specificationPath);
            var specification = JsonSerializer.Deserialize<C3DHeightThresholdBackgroundRemovalRunnerSpecification>(
                File.ReadAllText(fullSpecificationPath),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidDataException("Height threshold background-removal Runner specification is empty.");
            if (!Enum.TryParse<C3DHeightThresholdBackgroundRemovalMode>(
                    specification.Mode,
                    ignoreCase: true,
                    out var mode)
                || !Enum.IsDefined(mode))
            {
                throw new InvalidDataException("Height threshold background-removal Runner mode is invalid.");
            }

            var source = C3DHeightFieldSnapshot.LoadVerified(
                specification.SourcePath,
                specification.SourceEntityId,
                specification.SourceUnit,
                specification.SourceFrameId,
                specification.SourceByteLength,
                specification.SourceContentSha256,
                specification.SourceWidth,
                specification.SourceHeight);
            var evaluation = C3DHeightThresholdBackgroundRemovalRule.Evaluate(
                new C3DHeightThresholdBackgroundRemovalInput(
                    specification.StepId,
                    source,
                    specification.OutputEntityId,
                    specification.Threshold,
                    mode));
            if (evaluation.Output is null || evaluation.Evidence is null
                || evaluation.Result.Status is not (ResultStatus.Pass or ResultStatus.Warning))
            {
                throw new InvalidDataException(
                    $"Height threshold background-removal Runner failed: {evaluation.Result.Message}");
            }

            var outputPath = Path.GetFullPath(specification.OutputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? Environment.CurrentDirectory);
            evaluation.Output.SaveC3D(outputPath);
            var output = evaluation.Output;
            var evidence = evaluation.Evidence;
            var report = new
            {
                schemaVersion = "1.0",
                specification = new
                {
                    path = fullSpecificationPath,
                    stepId = specification.StepId,
                    threshold = specification.Threshold,
                    mode = mode.ToString()
                },
                source = new
                {
                    id = source.EntityId,
                    path = specification.SourcePath,
                    byteLength = source.ByteLength,
                    contentSha256 = source.ContentSha256,
                    rootSourceSha256 = source.RootSourceSha256,
                    width = source.Width,
                    height = source.Height,
                    unit = source.Unit,
                    frameId = source.FrameId
                },
                output = new
                {
                    id = output.EntityId,
                    path = outputPath,
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
                    comparisonPolicy = evidence.ComparisonPolicy,
                    missingValuePolicy = evidence.MissingValuePolicy,
                    backgroundPolicy = evidence.BackgroundPolicy,
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
            Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath) ?? Environment.CurrentDirectory);
            File.WriteAllText(
                fullReportPath,
                JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"Height-threshold output: {outputPath}");
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
            Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath) ?? Environment.CurrentDirectory);
            File.WriteAllLines(
                fullReportPath,
                [
                    "OpenVisionLab 3D Height-Threshold Background Removal Runner report",
                    $"Error|{exception.Message}"
                ]);
            Console.Error.WriteLine(exception.Message);
            return 5;
        }
    }
}

internal sealed class C3DHeightThresholdBackgroundRemovalRunnerSpecification
{
    public string StepId { get; set; } = "";
    public string SourcePath { get; set; } = "";
    public string SourceEntityId { get; set; } = "";
    public string SourceUnit { get; set; } = "";
    public string SourceFrameId { get; set; } = "";
    public long SourceByteLength { get; set; }
    public string SourceContentSha256 { get; set; } = "";
    public int SourceWidth { get; set; }
    public int SourceHeight { get; set; }
    public string OutputEntityId { get; set; } = "";
    public string OutputPath { get; set; } = "";
    public double Threshold { get; set; }
    public string Mode { get; set; } = "";
}
