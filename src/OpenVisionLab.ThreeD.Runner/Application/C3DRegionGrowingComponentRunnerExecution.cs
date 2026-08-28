using System.Text.Json;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

internal static class C3DRegionGrowingComponentRunnerExecution
{
    public static int Run(string specificationPath, string reportPath)
    {
        var fullReportPath = Path.GetFullPath(reportPath);
        try
        {
            var fullSpecificationPath = Path.GetFullPath(specificationPath);
            var specification = JsonSerializer.Deserialize<C3DRegionGrowingComponentRunnerSpecification>(
                File.ReadAllText(fullSpecificationPath),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidDataException("Region-growing component Runner specification is empty.");

            var source = LoadSource(specification.Source);
            var connectedRegionPath = Path.GetFullPath(specification.ConnectedRegionArtifactPath);
            var connectedRegion = C3DConnectedRegionArtifactStore.Load(connectedRegionPath);
            if (!string.Equals(
                    connectedRegion.ArtifactId,
                    specification.ConnectedRegionArtifactId,
                    StringComparison.OrdinalIgnoreCase)
                || !string.Equals(
                    connectedRegion.ContentSha256,
                    specification.ConnectedRegionContentSha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "Region-growing component connected-region artifact identity does not match the Runner specification.");
            }

            var outputPath = Path.GetFullPath(specification.OutputPath);
            if (string.Equals(outputPath, Path.GetFullPath(source.SourcePath), StringComparison.OrdinalIgnoreCase)
                || string.Equals(outputPath, connectedRegionPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "Region-growing component output path must differ from both source artifacts.");
            }

            if (File.Exists(outputPath))
            {
                throw new InvalidDataException(
                    "Region-growing component output path already exists; overwrite is not allowed.");
            }

            var evaluation = C3DRegionGrowingComponentRule.Evaluate(
                new C3DRegionGrowingComponentInput(
                    specification.StepId,
                    source,
                    connectedRegion,
                    specification.SelectedRegionIndex,
                    specification.OutputEntityId));
            if (evaluation.Output is null
                || evaluation.Evidence is null
                || (evaluation.Result.Status != ResultStatus.Pass
                    && evaluation.Result.Status != ResultStatus.Warning))
            {
                throw new InvalidDataException(
                    $"Region-growing component Runner failed: {evaluation.Result.Message}");
            }

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
                    selectedRegionIndex = specification.SelectedRegionIndex,
                    projectionPolicy = C3DRegionGrowingComponentEvidence.ProjectionPolicyName,
                    missingValuePolicy = C3DRegionGrowingComponentEvidence.MissingValuePolicyName,
                    lineagePolicy = C3DRegionGrowingComponentEvidence.LineagePolicyName,
                    coordinatePolicy = C3DRegionGrowingComponentEvidence.CoordinatePolicyName
                },
                source = SourceReport(source),
                connectedRegion = new
                {
                    path = connectedRegionPath,
                    id = connectedRegion.ArtifactId,
                    contentSha256 = connectedRegion.ContentSha256,
                    maskContentSha256 = connectedRegion.MaskContentSha256,
                    sourceEntityId = connectedRegion.SourceEntityId,
                    sourceContentSha256 = connectedRegion.SourceContentSha256,
                    rootSourceSha256 = connectedRegion.RootSourceSha256,
                    connectivity = connectedRegion.Connectivity,
                    regionCount = connectedRegion.Regions.Count
                },
                output = new
                {
                    id = output.EntityId,
                    path = outputPath,
                    unit = output.Unit,
                    frameId = output.FrameId,
                    width = output.Width,
                    height = output.Height,
                    byteLength = output.ByteLength,
                    contentSha256 = output.ContentSha256,
                    rootSourceSha256 = output.RootSourceSha256,
                    validCount = output.ValidCount,
                    missingCount = output.MissingCount,
                    isDerived = output.IsDerived
                },
                evidence = new
                {
                    contractVersion = C3DRegionGrowingComponentEvidence.ContractVersion,
                    contentSha256 = evidence.ContentSha256,
                    sourceEntityId = evidence.SourceEntityId,
                    sourceContentSha256 = evidence.SourceContentSha256,
                    sourceRootSourceSha256 = evidence.SourceRootSourceSha256,
                    sourceByteLength = evidence.SourceByteLength,
                    connectedRegionArtifactId = evidence.ConnectedRegionArtifactId,
                    connectedRegionContentSha256 = evidence.ConnectedRegionContentSha256,
                    connectedRegionMaskContentSha256 = evidence.ConnectedRegionMaskContentSha256,
                    selectedRegionIndex = evidence.SelectedRegionIndex,
                    connectivity = evidence.Connectivity,
                    outputEntityId = evidence.OutputEntityId,
                    outputContentSha256 = evidence.OutputContentSha256,
                    outputRootSourceSha256 = evidence.OutputRootSourceSha256,
                    unit = evidence.Unit,
                    frameId = evidence.FrameId,
                    gridWidth = evidence.GridWidth,
                    gridHeight = evidence.GridHeight,
                    selectedCellCount = evidence.SelectedCellCount,
                    inputValidSampleCount = evidence.InputValidSampleCount,
                    inputMissingSampleCount = evidence.InputMissingSampleCount,
                    retainedValidSampleCount = evidence.RetainedValidSampleCount,
                    reducedBackgroundSampleCount = evidence.ReducedBackgroundSampleCount,
                    projectionPolicy = evidence.ProjectionPolicy,
                    missingValuePolicy = evidence.MissingValuePolicy,
                    lineagePolicy = evidence.LineagePolicy,
                    coordinatePolicy = evidence.CoordinatePolicy,
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
                connectedRegionMutation = false,
                claimBoundary =
                    "Deterministic selected connected-region component preparation; no automatic region selection, calibration, physical measurement acceptance, or metrology claim."
            };
            Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath) ?? Environment.CurrentDirectory);
            File.WriteAllText(
                fullReportPath,
                JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"Region-growing component output: {outputPath}");
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
                    "OpenVisionLab 3D Region-Growing Component Runner report",
                    $"Error|{exception.Message}"
                ]);
            Console.Error.WriteLine(exception.Message);
            return 5;
        }
    }

    private static C3DHeightFieldSnapshot LoadSource(
        C3DRegionGrowingComponentRunnerSource? source)
    {
        if (source is null)
        {
            throw new InvalidDataException("Region-growing component source is required.");
        }

        var snapshot = C3DHeightFieldSnapshot.LoadVerified(
            source.Path,
            source.EntityId,
            source.Unit,
            source.FrameId,
            source.ByteLength,
            source.ContentSha256,
            source.Width,
            source.Height);
        if (!string.Equals(
                snapshot.RootSourceSha256,
                source.RootSourceSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Region-growing component source root identity does not match the Runner specification.");
        }

        return snapshot;
    }

    private static object SourceReport(C3DHeightFieldSnapshot source) => new
    {
        path = source.SourcePath,
        id = source.EntityId,
        unit = source.Unit,
        frameId = source.FrameId,
        width = source.Width,
        height = source.Height,
        byteLength = source.ByteLength,
        contentSha256 = source.ContentSha256,
        rootSourceSha256 = source.RootSourceSha256,
        validCount = source.ValidCount,
        missingCount = source.MissingCount,
        isDerived = source.IsDerived
    };
}

internal sealed class C3DRegionGrowingComponentRunnerSpecification
{
    public string StepId { get; set; } = "";
    public C3DRegionGrowingComponentRunnerSource? Source { get; set; }
    public string ConnectedRegionArtifactPath { get; set; } = "";
    public string ConnectedRegionArtifactId { get; set; } = "";
    public string ConnectedRegionContentSha256 { get; set; } = "";
    public int SelectedRegionIndex { get; set; }
    public string OutputEntityId { get; set; } = "";
    public string OutputPath { get; set; } = "";
}

internal sealed class C3DRegionGrowingComponentRunnerSource
{
    public string Path { get; set; } = "";
    public string EntityId { get; set; } = "";
    public string Unit { get; set; } = "";
    public string FrameId { get; set; } = "";
    public long ByteLength { get; set; }
    public string ContentSha256 { get; set; } = "";
    public string RootSourceSha256 { get; set; } = "";
    public int Width { get; set; }
    public int Height { get; set; }
}
