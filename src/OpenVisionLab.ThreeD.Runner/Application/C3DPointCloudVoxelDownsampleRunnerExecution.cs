using System.Text.Json;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

internal static class C3DPointCloudVoxelDownsampleRunnerExecution
{
    public static int Run(string specificationPath, string reportPath)
    {
        var fullReportPath = Path.GetFullPath(reportPath);
        try
        {
            var fullSpecificationPath = Path.GetFullPath(specificationPath);
            var specification = JsonSerializer.Deserialize<C3DPointCloudVoxelDownsampleRunnerSpecification>(
                File.ReadAllText(fullSpecificationPath),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidDataException("Point-cloud voxel-downsample Runner specification is empty.");
            ArgumentException.ThrowIfNullOrWhiteSpace(specification.OutputPath);
            var source = CreateSource(specification.Source);
            var outputPath = Path.GetFullPath(specification.OutputPath);
            if (string.Equals(outputPath, Path.GetFullPath(source.SourcePath), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Point-cloud voxel-downsample output path must differ from the source path.");
            }

            if (File.Exists(outputPath))
            {
                throw new InvalidDataException("Point-cloud voxel-downsample output path already exists; overwrite is not allowed.");
            }

            var evaluation = C3DPointCloudVoxelDownsampleRule.Evaluate(
                new C3DPointCloudVoxelDownsampleInput(
                    specification.StepId,
                    source,
                    specification.OutputEntityId,
                    specification.VoxelEdgeLength,
                    specification.OriginX,
                    specification.OriginY,
                    specification.OriginZ));
            if (evaluation.Output is null
                || evaluation.Evidence is null
                || evaluation.Result.Status != ResultStatus.Pass)
            {
                throw new InvalidDataException(
                    $"Point-cloud voxel-downsample Runner failed: {evaluation.Result.Message}");
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
                    voxelIndexPolicy = C3DPointCloudVoxelDownsampleEvidence.VoxelIndexPolicyName,
                    representativePolicy = C3DPointCloudVoxelDownsampleEvidence.RepresentativePolicyName,
                    outputOrderPolicy = C3DPointCloudVoxelDownsampleEvidence.OutputOrderPolicyName,
                    lineagePolicy = C3DPointCloudVoxelDownsampleEvidence.LineagePolicyName
                },
                source = SourceReport(source),
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
                    contractVersion = C3DPointCloudVoxelDownsampleEvidence.ContractVersion,
                    contentSha256 = evidence.ContentSha256,
                    sourceEntityId = evidence.SourceEntityId,
                    sourceContentSha256 = evidence.SourceContentSha256,
                    sourceRootSourceSha256 = evidence.SourceRootSourceSha256,
                    outputEntityId = evidence.OutputEntityId,
                    outputContentSha256 = evidence.OutputContentSha256,
                    outputRootSourceSha256 = evidence.OutputRootSourceSha256,
                    unit = evidence.Unit,
                    frameId = evidence.FrameId,
                    coordinateConvention = evidence.CoordinateConvention,
                    voxelEdgeLength = evidence.VoxelEdgeLength,
                    originX = evidence.OriginX,
                    originY = evidence.OriginY,
                    originZ = evidence.OriginZ,
                    voxelIndexPolicy = evidence.VoxelIndexPolicy,
                    representativePolicy = evidence.RepresentativePolicy,
                    outputOrderPolicy = evidence.OutputOrderPolicy,
                    lineagePolicy = evidence.LineagePolicy,
                    inputPointCount = evidence.InputPointCount,
                    outputPointCount = evidence.OutputPointCount,
                    reducedPointCount = evidence.ReducedPointCount,
                    representativeSourceIndexes = evidence.RepresentativeSourceIndexes,
                    inputBounds = new
                    {
                        minimumX = evidence.InputMinimumX,
                        minimumY = evidence.InputMinimumY,
                        minimumZ = evidence.InputMinimumZ,
                        maximumX = evidence.InputMaximumX,
                        maximumY = evidence.InputMaximumY,
                        maximumZ = evidence.InputMaximumZ
                    },
                    outputBounds = new
                    {
                        minimumX = evidence.OutputMinimumX,
                        minimumY = evidence.OutputMinimumY,
                        minimumZ = evidence.OutputMinimumZ,
                        maximumX = evidence.OutputMaximumX,
                        maximumY = evidence.OutputMaximumY,
                        maximumZ = evidence.OutputMaximumZ
                    },
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
                sourceMutation = false,
                claimBoundary =
                    "Deterministic XYZ voxel-downsample preparation evidence; no interpolation, alignment, calibration, physical measurement acceptance, or metrology claim."
            };
            Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath) ?? Environment.CurrentDirectory);
            File.WriteAllText(
                fullReportPath,
                JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"Point-cloud voxel-downsample output: {outputPath}");
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
                    "OpenVisionLab 3D Point-Cloud Voxel Downsample Runner report",
                    $"Error|{exception.Message}"
                ]);
            Console.Error.WriteLine(exception.Message);
            return 5;
        }
    }

    private static C3DPointCloudSnapshot CreateSource(
        C3DPointCloudVoxelDownsampleRunnerSource? source)
    {
        if (source is null)
        {
            throw new InvalidDataException("Point-cloud voxel-downsample source is required.");
        }

        var snapshot = C3DPointCloudSnapshot.CreateForVerification(
            source.EntityId,
            source.Path,
            source.SourceFormat,
            source.Unit,
            source.FrameId,
            source.CoordinateConvention,
            source.Points?.Select(point => new C3DPoint3(point.X, point.Y, point.Z)).ToArray()
                ?? throw new InvalidDataException("Point-cloud voxel-downsample source points are required."));
        if (snapshot.ByteLength != source.ByteLength
            || !string.Equals(snapshot.ContentSha256, source.ContentSha256, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(snapshot.RootSourceSha256, source.RootSourceSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Point-cloud voxel-downsample canonical byte identity does not match the Runner specification.");
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

internal sealed class C3DPointCloudVoxelDownsampleRunnerSpecification
{
    public string StepId { get; set; } = "";
    public C3DPointCloudVoxelDownsampleRunnerSource? Source { get; set; }
    public string OutputEntityId { get; set; } = "";
    public string OutputPath { get; set; } = "";
    public double VoxelEdgeLength { get; set; }
    public double OriginX { get; set; }
    public double OriginY { get; set; }
    public double OriginZ { get; set; }
}

internal sealed class C3DPointCloudVoxelDownsampleRunnerSource
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
    public List<C3DPointCloudVoxelDownsampleRunnerPoint>? Points { get; set; }
}

internal sealed class C3DPointCloudVoxelDownsampleRunnerPoint
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
}
