using System.Text.Json;
using System.Text.Json.Serialization;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

internal static class C3DHeightMapNormalPreparationRunnerExecution
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
    };

    public static int Run(string specificationPath, string reportPath)
    {
        var fullReportPath = Path.GetFullPath(reportPath);
        try
        {
            var fullSpecificationPath = Path.GetFullPath(specificationPath);
            var specification = JsonSerializer.Deserialize<C3DHeightMapNormalPreparationRunnerSpecification>(
                File.ReadAllText(fullSpecificationPath),
                JsonOptions)
                ?? throw new InvalidDataException("Height-map normal-preparation Runner specification is empty.");
            var source = CreateSource(specification.Source);
            var validation = specification.Validation is null
                ? null
                : new C3DHeightMapNormalValidationOptions(
                    specification.Validation.ExpectedNormalX,
                    specification.Validation.ExpectedNormalY,
                    specification.Validation.ExpectedNormalZ,
                    specification.Validation.MinimumAlignmentCosine);
            var evaluation = C3DHeightMapNormalPreparationRule.Evaluate(
                new C3DHeightMapNormalPreparationInput(
                    specification.StepId,
                    source,
                    specification.OutputEntityId,
                    validation));
            if (evaluation.Evidence is null
                || evaluation.Result.Status is ResultStatus.Error or ResultStatus.NotRun)
            {
                throw new InvalidDataException(
                    $"Height-map normal-preparation Runner failed: {evaluation.Result.Message}");
            }

            var evidence = evaluation.Evidence;
            var report = new
            {
                schemaVersion = "1.0",
                specification = new
                {
                    path = fullSpecificationPath,
                    stepId = specification.StepId,
                    outputEntityId = specification.OutputEntityId,
                    derivativePolicy = C3DHeightMapNormalPreparationEvidence.DerivativePolicyName,
                    missingPolicy = C3DHeightMapNormalPreparationEvidence.MissingPolicyName,
                    validationRequested = validation is not null
                },
                source = new
                {
                    id = source.EntityId,
                    unit = source.Unit,
                    frameId = source.FrameId,
                    byteLength = source.ByteLength,
                    contentSha256 = source.ContentSha256,
                    rootSourceSha256 = source.RootSourceSha256,
                    width = source.Width,
                    height = source.Height,
                    validCount = source.ValidCount,
                    missingCount = source.MissingCount,
                    isDerived = source.IsDerived
                },
                evidence = new
                {
                    contractVersion = C3DHeightMapNormalPreparationEvidence.ContractVersion,
                    contentSha256 = evidence.ContentSha256,
                    sourceEntityId = evidence.SourceEntityId,
                    sourceContentSha256 = evidence.SourceContentSha256,
                    sourceRootSourceSha256 = evidence.SourceRootSourceSha256,
                    sourceByteLength = evidence.SourceByteLength,
                    outputEntityId = evidence.OutputEntityId,
                    outputContentSha256 = evidence.OutputContentSha256,
                    outputRootSourceSha256 = evidence.OutputRootSourceSha256,
                    unit = evidence.Unit,
                    frameId = evidence.FrameId,
                    coordinateConvention = evidence.CoordinateConvention,
                    derivativePolicy = evidence.DerivativePolicy,
                    missingPolicy = evidence.MissingPolicy,
                    lineagePolicy = evidence.LineagePolicy,
                    rowCount = evidence.RowCount,
                    columnCount = evidence.ColumnCount,
                    inputFiniteSampleCount = evidence.InputFiniteSampleCount,
                    calculatedNormalCount = evidence.CalculatedNormalCount,
                    unavailableNormalCount = evidence.UnavailableNormalCount,
                    centralDerivativeCount = evidence.CentralDerivativeCount,
                    oneSidedDerivativeCount = evidence.OneSidedDerivativeCount,
                    missingDerivativeCount = evidence.MissingDerivativeCount,
                    validationState = evidence.ValidationState.ToString(),
                    expectedNormal = evidence.ExpectedNormalX.HasValue
                        ? new { x = evidence.ExpectedNormalX.Value, y = evidence.ExpectedNormalY!.Value, z = evidence.ExpectedNormalZ!.Value }
                        : null,
                    minimumAlignmentCosine = evidence.MinimumAlignmentCosine,
                    validatedNormalCount = evidence.ValidatedNormalCount,
                    consistentNormalCount = evidence.ConsistentNormalCount,
                    reversedNormalCount = evidence.ReversedNormalCount,
                    minimumAlignment = evidence.MinimumAlignment,
                    meanAlignment = evidence.MeanAlignment,
                    maximumAngularErrorDegrees = evidence.MaximumAngularErrorDegrees,
                    samples = evidence.Samples.Select(sample => new
                    {
                        row = sample.Row,
                        column = sample.Column,
                        positionX = sample.PositionX,
                        positionY = sample.PositionY,
                        positionZ = sample.PositionZ,
                        normalX = sample.NormalX,
                        normalY = sample.NormalY,
                        normalZ = sample.NormalZ,
                        centralColumnDerivative = sample.CentralColumnDerivative,
                        centralRowDerivative = sample.CentralRowDerivative
                    }),
                    provenance = evidence.Provenance
                },
                result = new
                {
                    status = evaluation.Result.Status.ToString(),
                    message = evaluation.Result.Message,
                    elapsedMilliseconds = evaluation.Result.Elapsed.TotalMilliseconds,
                    metrics = evaluation.Result.Metrics.Select(metric => new
                    {
                        name = metric.Name,
                        kind = metric.Kind.ToString(),
                        value = metric.Value,
                        unit = metric.Unit,
                        status = metric.Status?.ToString()
                    })
                },
                sourceMutation = false,
                claimBoundary =
                    "Deterministic regular-height-map finite-difference normal preparation and optional explicit validation; no mesh repair, smoothing, point-cloud normal estimation, calibration, physical measurement, or production approval claim."
            };
            Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath) ?? Environment.CurrentDirectory);
            File.WriteAllText(fullReportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"Height-map normal-preparation evidence: {evidence.ContentSha256}");
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
                    "OpenVisionLab 3D Height-Map Normal Preparation Runner report",
                    $"Error|{exception.Message}"
                ]);
            Console.Error.WriteLine(exception.Message);
            return 5;
        }
    }

    private static C3DHeightFieldSnapshot CreateSource(
        C3DHeightMapNormalPreparationRunnerSource? source)
    {
        if (source is null || source.Values is null)
        {
            throw new InvalidDataException("Height-map normal-preparation source and values are required.");
        }

        var snapshot = C3DHeightFieldSnapshot.CreateForVerification(
            source.EntityId,
            source.Width,
            source.Height,
            source.Values,
            source.Unit,
            source.FrameId);
        if (snapshot.ByteLength != source.ByteLength
            || !string.Equals(snapshot.ContentSha256, source.ContentSha256, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(snapshot.RootSourceSha256, source.RootSourceSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Height-map normal-preparation source byte identity does not match the Runner specification.");
        }

        return snapshot;
    }
}

internal sealed class C3DHeightMapNormalPreparationRunnerSpecification
{
    public string StepId { get; set; } = "";
    public C3DHeightMapNormalPreparationRunnerSource? Source { get; set; }
    public string OutputEntityId { get; set; } = "";
    public C3DHeightMapNormalPreparationRunnerValidation? Validation { get; set; }
}

internal sealed class C3DHeightMapNormalPreparationRunnerSource
{
    public string EntityId { get; set; } = "";
    public int Width { get; set; }
    public int Height { get; set; }
    public string Unit { get; set; } = "";
    public string FrameId { get; set; } = "";
    public long ByteLength { get; set; }
    public string ContentSha256 { get; set; } = "";
    public string RootSourceSha256 { get; set; } = "";
    public List<double>? Values { get; set; }
}

internal sealed class C3DHeightMapNormalPreparationRunnerValidation
{
    public double ExpectedNormalX { get; set; }
    public double ExpectedNormalY { get; set; }
    public double ExpectedNormalZ { get; set; }
    public double MinimumAlignmentCosine { get; set; } = 0.999;
}
