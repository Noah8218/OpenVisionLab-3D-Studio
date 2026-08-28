using System.Text.Json;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

internal static class C3DHeightBackgroundSubtractionRunnerExecution
{
    public static int Run(string specificationPath, string reportPath)
    {
        var fullReportPath = Path.GetFullPath(reportPath);
        try
        {
            var fullSpecificationPath = Path.GetFullPath(specificationPath);
            var specification = JsonSerializer.Deserialize<C3DHeightBackgroundSubtractionRunnerSpecification>(
                File.ReadAllText(fullSpecificationPath),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidDataException("Saved-background subtraction Runner specification is empty.");
            var current = C3DHeightFieldSnapshot.LoadVerified(
                specification.CurrentPath,
                specification.CurrentEntityId,
                specification.CurrentUnit,
                specification.CurrentFrameId,
                specification.CurrentByteLength,
                specification.CurrentContentSha256,
                specification.CurrentWidth,
                specification.CurrentHeight);
            var background = C3DHeightFieldSnapshot.LoadVerified(
                specification.BackgroundPath,
                specification.BackgroundEntityId,
                specification.BackgroundUnit,
                specification.BackgroundFrameId,
                specification.BackgroundByteLength,
                specification.BackgroundContentSha256,
                specification.BackgroundWidth,
                specification.BackgroundHeight);
            var evaluation = C3DHeightBackgroundSubtractionRule.Evaluate(
                new C3DHeightBackgroundSubtractionInput(
                    specification.StepId,
                    current,
                    background,
                    specification.OutputEntityId));
            if (evaluation.Output is null || evaluation.Evidence is null
                || evaluation.Result.Status != ResultStatus.Pass)
            {
                throw new InvalidDataException(
                    $"Saved-background subtraction Runner failed: {evaluation.Result.Message}");
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
                    subtractionPolicy = C3DHeightBackgroundSubtractionEvidence.SubtractionPolicyName,
                    missingValuePolicy = C3DHeightBackgroundSubtractionEvidence.MissingValuePolicyName,
                    zeroDeltaPolicy = C3DHeightBackgroundSubtractionEvidence.ZeroDeltaPolicyName
                },
                current = new
                {
                    id = current.EntityId,
                    path = specification.CurrentPath,
                    byteLength = current.ByteLength,
                    contentSha256 = current.ContentSha256,
                    rootSourceSha256 = current.RootSourceSha256,
                    width = current.Width,
                    height = current.Height,
                    unit = current.Unit,
                    frameId = current.FrameId
                },
                savedBackground = new
                {
                    id = background.EntityId,
                    path = specification.BackgroundPath,
                    byteLength = background.ByteLength,
                    contentSha256 = background.ContentSha256,
                    rootSourceSha256 = background.RootSourceSha256,
                    width = background.Width,
                    height = background.Height,
                    unit = background.Unit,
                    frameId = background.FrameId
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
                    contractVersion = C3DHeightBackgroundSubtractionEvidence.ContractVersion,
                    contentSha256 = evidence.ContentSha256,
                    currentSourceEntityId = evidence.CurrentSourceEntityId,
                    currentSourceContentSha256 = evidence.CurrentSourceContentSha256,
                    backgroundEntityId = evidence.BackgroundEntityId,
                    backgroundContentSha256 = evidence.BackgroundContentSha256,
                    subtractionPolicy = evidence.SubtractionPolicy,
                    gridPolicy = evidence.GridPolicy,
                    missingValuePolicy = evidence.MissingValuePolicy,
                    zeroDeltaPolicy = evidence.ZeroDeltaPolicy,
                    pairedValidSampleCount = evidence.PairedValidSampleCount,
                    missingEitherSampleCount = evidence.MissingEitherSampleCount,
                    positiveDeltaSampleCount = evidence.PositiveDeltaSampleCount,
                    negativeDeltaSampleCount = evidence.NegativeDeltaSampleCount,
                    zeroDeltaSampleCount = evidence.ZeroDeltaSampleCount
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
                currentMutation = false,
                backgroundMutation = false,
                claimBoundary =
                    "Deterministic raw-height saved-background preparation evidence; no automatic alignment, physical calibration, measurement acceptance, or metrology claim."
            };
            Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath) ?? Environment.CurrentDirectory);
            File.WriteAllText(
                fullReportPath,
                JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"Saved-background subtraction output: {outputPath}");
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
                    "OpenVisionLab 3D Saved-Background Subtraction Runner report",
                    $"Error|{exception.Message}"
                ]);
            Console.Error.WriteLine(exception.Message);
            return 5;
        }
    }
}

internal sealed class C3DHeightBackgroundSubtractionRunnerSpecification
{
    public string StepId { get; set; } = "";
    public string CurrentPath { get; set; } = "";
    public string CurrentEntityId { get; set; } = "";
    public string CurrentUnit { get; set; } = "";
    public string CurrentFrameId { get; set; } = "";
    public long CurrentByteLength { get; set; }
    public string CurrentContentSha256 { get; set; } = "";
    public int CurrentWidth { get; set; }
    public int CurrentHeight { get; set; }
    public string BackgroundPath { get; set; } = "";
    public string BackgroundEntityId { get; set; } = "";
    public string BackgroundUnit { get; set; } = "";
    public string BackgroundFrameId { get; set; } = "";
    public long BackgroundByteLength { get; set; }
    public string BackgroundContentSha256 { get; set; } = "";
    public int BackgroundWidth { get; set; }
    public int BackgroundHeight { get; set; }
    public string OutputEntityId { get; set; } = "";
    public string OutputPath { get; set; } = "";
}
