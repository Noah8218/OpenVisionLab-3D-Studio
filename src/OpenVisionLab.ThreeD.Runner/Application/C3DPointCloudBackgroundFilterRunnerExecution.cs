using System.Text.Json;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

internal static class C3DPointCloudBackgroundFilterRunnerExecution
{
    public static int Run(string specificationPath, string reportPath)
    {
        var fullReportPath = Path.GetFullPath(reportPath);
        try
        {
            var fullSpecificationPath = Path.GetFullPath(specificationPath);
            var specification = JsonSerializer.Deserialize<C3DPointCloudBackgroundFilterRunnerSpecification>(
                File.ReadAllText(fullSpecificationPath),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidDataException("Point-cloud background-filter Runner specification is empty.");
            var current = CreateSource(specification.Current, "Current");
            var background = CreateSource(specification.SavedBackground, "Saved background");
            var outputPath = Path.GetFullPath(specification.OutputPath);
            if (string.Equals(outputPath, Path.GetFullPath(current.SourcePath), StringComparison.OrdinalIgnoreCase)
                || string.Equals(outputPath, Path.GetFullPath(background.SourcePath), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Point-cloud background-filter output path must differ from both source paths.");
            }

            if (File.Exists(outputPath))
            {
                throw new InvalidDataException("Point-cloud background-filter output path already exists; overwrite is not allowed.");
            }

            var evaluation = C3DPointCloudBackgroundFilterRule.Evaluate(
                new C3DPointCloudBackgroundFilterInput(
                    specification.StepId,
                    current,
                    background,
                    specification.OutputEntityId,
                    specification.MaximumBackgroundDistance));
            if (evaluation.Output is null || evaluation.Evidence is null
                || (evaluation.Result.Status != ResultStatus.Pass
                    && evaluation.Result.Status != ResultStatus.Warning))
            {
                throw new InvalidDataException(
                    $"Point-cloud background-filter Runner failed: {evaluation.Result.Message}");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? Environment.CurrentDirectory);
            var output = evaluation.Output;
            var evidence = evaluation.Evidence;
            var outputDocument = new
            {
                schemaVersion = "1.0",
                output = new
                {
                    id = output.EntityId,
                    sourcePath = output.SourcePath,
                    sourceFormat = output.SourceFormat,
                    unit = output.Unit,
                    frameId = output.FrameId,
                    coordinateConvention = output.CoordinateConvention,
                    byteLength = output.ByteLength,
                    contentSha256 = output.ContentSha256,
                    rootSourceSha256 = output.RootSourceSha256,
                    isDerived = output.IsDerived,
                    points = output.Points.Select(point => new { x = point.X, y = point.Y, z = point.Z })
                }
            };
            File.WriteAllText(
                outputPath,
                JsonSerializer.Serialize(outputDocument, new JsonSerializerOptions { WriteIndented = true }));

            var report = new
            {
                schemaVersion = "1.0",
                specification = new
                {
                    path = fullSpecificationPath,
                    stepId = specification.StepId,
                    distancePolicy = C3DPointCloudBackgroundFilterEvidence.DistancePolicyName,
                    removalPolicy = C3DPointCloudBackgroundFilterEvidence.RemovalPolicyName,
                    matchingPolicy = C3DPointCloudBackgroundFilterEvidence.MatchingPolicyName,
                    lineagePolicy = C3DPointCloudBackgroundFilterEvidence.LineagePolicyName
                },
                current = SourceReport(current),
                savedBackground = SourceReport(background),
                output = new
                {
                    id = output.EntityId,
                    path = outputPath,
                    sourceFormat = output.SourceFormat,
                    unit = output.Unit,
                    frameId = output.FrameId,
                    coordinateConvention = output.CoordinateConvention,
                    byteLength = output.ByteLength,
                    contentSha256 = output.ContentSha256,
                    rootSourceSha256 = output.RootSourceSha256,
                    pointCount = output.ValidPointCount,
                    isDerived = output.IsDerived,
                    points = output.Points.Select(point => new { x = point.X, y = point.Y, z = point.Z })
                },
                evidence = new
                {
                    contractVersion = C3DPointCloudBackgroundFilterEvidence.ContractVersion,
                    contentSha256 = evidence.ContentSha256,
                    currentSourceEntityId = evidence.CurrentSourceEntityId,
                    currentSourceContentSha256 = evidence.CurrentSourceContentSha256,
                    currentRootSourceSha256 = evidence.CurrentRootSourceSha256,
                    backgroundEntityId = evidence.BackgroundEntityId,
                    backgroundContentSha256 = evidence.BackgroundContentSha256,
                    backgroundRootSourceSha256 = evidence.BackgroundRootSourceSha256,
                    outputEntityId = evidence.OutputEntityId,
                    outputContentSha256 = evidence.OutputContentSha256,
                    outputRootSourceSha256 = evidence.OutputRootSourceSha256,
                    unit = evidence.Unit,
                    frameId = evidence.FrameId,
                    coordinateConvention = evidence.CoordinateConvention,
                    maximumBackgroundDistance = evidence.MaximumBackgroundDistance,
                    distancePolicy = evidence.DistancePolicy,
                    removalPolicy = evidence.RemovalPolicy,
                    matchingPolicy = evidence.MatchingPolicy,
                    lineagePolicy = evidence.LineagePolicy,
                    inputPointCount = evidence.InputPointCount,
                    backgroundPointCount = evidence.BackgroundPointCount,
                    retainedPointCount = evidence.RetainedPointCount,
                    removedPointCount = evidence.RemovedPointCount,
                    minimumNearestBackgroundDistance = evidence.MinimumNearestBackgroundDistance,
                    maximumNearestBackgroundDistance = evidence.MaximumNearestBackgroundDistance,
                    meanNearestBackgroundDistance = evidence.MeanNearestBackgroundDistance,
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
                        value = metric.Value,
                        unit = metric.Unit,
                        status = metric.Status?.ToString()
                    })
                },
                currentMutation = false,
                backgroundMutation = false,
                claimBoundary =
                    "Deterministic XYZ nearest-background preparation evidence; no automatic alignment, calibration, physical measurement acceptance, or metrology claim."
            };
            Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath) ?? Environment.CurrentDirectory);
            File.WriteAllText(
                fullReportPath,
                JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"Point-cloud background-filter output: {outputPath}");
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
                    "OpenVisionLab 3D Point-Cloud Background Filter Runner report",
                    $"Error|{exception.Message}"
                ]);
            Console.Error.WriteLine(exception.Message);
            return 5;
        }
    }

    private static C3DPointCloudSnapshot CreateSource(
        C3DPointCloudBackgroundFilterRunnerSource? source,
        string label)
    {
        if (source is null)
        {
            throw new InvalidDataException($"{label} point-cloud source is required.");
        }

        var snapshot = C3DPointCloudSnapshot.CreateForVerification(
            source.EntityId,
            source.Path,
            source.SourceFormat,
            source.Unit,
            source.FrameId,
            source.CoordinateConvention,
            source.Points?.Select(point => new C3DPoint3(point.X, point.Y, point.Z)).ToArray()
                ?? throw new InvalidDataException($"{label} point-cloud points are required."));
        if (snapshot.ByteLength != source.ByteLength
            || !string.Equals(snapshot.ContentSha256, source.ContentSha256, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(snapshot.RootSourceSha256, source.RootSourceSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"{label} point-cloud canonical byte identity does not match the Runner specification.");
        }

        return snapshot;
    }

    private static object SourceReport(C3DPointCloudSnapshot source) => new
    {
        id = source.EntityId,
        path = source.SourcePath,
        sourceFormat = source.SourceFormat,
        unit = source.Unit,
        frameId = source.FrameId,
        coordinateConvention = source.CoordinateConvention,
        byteLength = source.ByteLength,
        contentSha256 = source.ContentSha256,
        rootSourceSha256 = source.RootSourceSha256,
        pointCount = source.ValidPointCount,
        isDerived = source.IsDerived
    };
}

internal sealed class C3DPointCloudBackgroundFilterRunnerSpecification
{
    public string StepId { get; set; } = "";
    public C3DPointCloudBackgroundFilterRunnerSource? Current { get; set; }
    public C3DPointCloudBackgroundFilterRunnerSource? SavedBackground { get; set; }
    public string OutputEntityId { get; set; } = "";
    public string OutputPath { get; set; } = "";
    public double MaximumBackgroundDistance { get; set; }
}

internal sealed class C3DPointCloudBackgroundFilterRunnerSource
{
    public string Path { get; set; } = "";
    public string EntityId { get; set; } = "";
    public string SourceFormat { get; set; } = "";
    public string Unit { get; set; } = "";
    public string FrameId { get; set; } = "";
    public string CoordinateConvention { get; set; } = "";
    public long ByteLength { get; set; }
    public string ContentSha256 { get; set; } = "";
    public string RootSourceSha256 { get; set; } = "";
    public List<C3DPointCloudBackgroundFilterRunnerPoint>? Points { get; set; }
}

internal sealed class C3DPointCloudBackgroundFilterRunnerPoint
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
}
